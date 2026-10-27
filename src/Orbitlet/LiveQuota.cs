// Official read-only Codex app-server integration. No credentials are read by this app.
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace Orbitlet {
    public sealed class QuotaSnapshot {
        public double? FiveHourRemainingPercent,WeeklyRemainingPercent;
        public DateTime UpdatedAtUtc;
        public long? FiveHourResetsAt,WeeklyResetsAt;
        public string ToJson(string status,string error) {
            return Json.Encode(new Dictionary<string,object> {
                {
                    "fiveHourRemainingPercent",FiveHourRemainingPercent
                }, {
                    "weeklyRemainingPercent",WeeklyRemainingPercent
                }, {
                    "updatedAtUtc",UpdatedAtUtc.ToString("o",CultureInfo.InvariantCulture)
                }, {
                    "fiveHourResetsAt",FiveHourResetsAt
                }, {
                    "weeklyResetsAt",WeeklyResetsAt
                }, {
                    "syncStatus",status
                }, {
                    "error",error
                }, {
                    "source","codex-app-server"
                }
            });
        }
    }
    public static class Json {
        public static string Encode(object value) {
            return new JavaScriptSerializer().Serialize(value);
        }
        public static Dictionary<string,object> Map(object value) {
            return value as Dictionary<string,object>;
        }
        public static object Get(Dictionary<string,object> map,string key) {
            object value;
            return map!=null&&map.TryGetValue(key,out value)?value:null;
        }
        public static double? Number(object value) {
            if(!(value is int||value is long||value is double||value is decimal||value is float))return null;
            double number=Convert.ToDouble(value,CultureInfo.InvariantCulture);
            return double.IsNaN(number)||double.IsInfinity(number)?(double?)null:number;
        }
    }
    public sealed class QuotaReadException : Exception {
        public QuotaReadException(string message):base(message) {
        }
    }
    public sealed class CodexQuotaClient : IDisposable {
        Process process;
        int nextId;
        bool disposed;
        readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
        public static string FindCodex() {
            string configured=Environment.GetEnvironmentVariable("ORBITLET_CODEX_PATH")??Environment.GetEnvironmentVariable("QUIET_ORBIT_CODEX_PATH");
            if(!string.IsNullOrWhiteSpace(configured)&&File.Exists(configured))return configured;
            string known=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","OpenAI","Codex","bin","codex.exe");
            if(File.Exists(known))return known;
            string bundled=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenAI","Codex","bin");
            if(Directory.Exists(bundled)) {
                string direct=Path.Combine(bundled,"codex.exe");
                if(File.Exists(direct))return direct;
                string[] versions=Directory.GetDirectories(bundled);
                Array.Sort(versions,(a,b)=>Directory.GetLastWriteTimeUtc(b).CompareTo(Directory.GetLastWriteTimeUtc(a)));
                foreach(string version in versions) {
                    string candidate=Path.Combine(version,"codex.exe");
                    if(File.Exists(candidate))return candidate;
                }
            }
            foreach(string entry in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator)) {
                try {
                    string candidate=Path.Combine(entry.Trim('"'),"codex.exe");
                    if(File.Exists(candidate))return candidate;
                }
                catch(ArgumentException) {
                }
            }
            throw new QuotaReadException("未找到 Codex，请先安装或指定 CLI 路径");
        }
        public static QuotaSnapshot Parse(object result,DateTime timestamp) {
            var root=Json.Map(result);
            var buckets=Json.Map(Json.Get(root,"rateLimitsByLimitId"));
            Dictionary<string,object> bucket=null;
            if(buckets!=null&&buckets.ContainsKey("codex"))bucket=Json.Map(buckets["codex"]);
            else if(buckets==null||buckets.Count==0)bucket=Json.Map(Json.Get(root,"rateLimits"));
            if(bucket==null)throw new QuotaReadException("未返回 Codex 额度窗口");
            object id=Json.Get(bucket,"limitId");
            if(id!=null&&Convert.ToString(id)!="codex")throw new QuotaReadException("未返回 Codex 额度窗口");
            var snapshot=new QuotaSnapshot {
                UpdatedAtUtc=timestamp
            };
            foreach(string key in new[] {
                "primary","secondary"
            }) {
                var window=Json.Map(Json.Get(bucket,key));
                double? minutes=Json.Number(Json.Get(window,"windowDurationMins"));
                double? used=Json.Number(Json.Get(window,"usedPercent"));
                if(!used.HasValue)continue;
                double remaining=Math.Max(0,Math.Min(100,100-used.Value));
                double? reset=Json.Number(Json.Get(window,"resetsAt"));
                long? resets=reset.HasValue&&(reset.Value>=0&&reset.Value<long.MaxValue)?(long?)reset.Value:null;
                if(minutes==300) {
                    snapshot.FiveHourRemainingPercent=remaining;
                    snapshot.FiveHourResetsAt=resets;
                }
                if(minutes==10080) {
                    snapshot.WeeklyRemainingPercent=remaining;
                    snapshot.WeeklyResetsAt=resets;
                }
            }
            return snapshot;
        }
        public async Task<QuotaSnapshot> ReadAsync() {
            await gate.WaitAsync().ConfigureAwait(false);
            try {
                if(disposed)throw new ObjectDisposedException("CodexQuotaClient");
                if(process==null||process.HasExited) {
                    StopProcess();
                    process=new Process {
                        StartInfo=new ProcessStartInfo {
                            FileName=FindCodex(),Arguments="app-server --listen stdio://",WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory, UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true, StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8
                        }
                    };
                    process.ErrorDataReceived+=(s,e)=> {
                    };
                    process.Start();
                    process.BeginErrorReadLine();
                    process.StandardInput.AutoFlush=true;
                    await Request("initialize",new {
                        clientInfo=new {
                            name="quiet_orbit",title="Orbitlet",version="0.5.0"
                        }
                    }).ConfigureAwait(false);
                    Send(new {
                        method="initialized",@params=new {
                        }
                    });
                }
                object result=await Request("account/rateLimits/read",null).ConfigureAwait(false);
                return Parse(result,DateTime.UtcNow);
            }
            catch {
                StopProcess();
                throw;
            }
            finally {
                gate.Release();
            }
        }
        void Send(object message) {
            process.StandardInput.WriteLine(Json.Encode(message));
        }
        async Task<object> Request(string method,object parameters) {
            int id=++nextId;
            Send(new {
                id=id,method=method,@params=parameters
            });
            DateTime deadline=DateTime.UtcNow.AddSeconds(20);
            while(true) {
                int remaining=(int)(deadline-DateTime.UtcNow).TotalMilliseconds;
                if(remaining<=0)throw new QuotaReadException("同步超时");
                Task<string> read=process.StandardOutput.ReadLineAsync();
                if(await Task.WhenAny(read,Task.Delay(remaining)).ConfigureAwait(false)!=read)throw new QuotaReadException("同步超时");
                string line=await read.ConfigureAwait(false);
                if(line==null)throw new QuotaReadException("Codex 连接已断开");
                Dictionary<string,object> message;
                try {
                    message=Json.Map(new JavaScriptSerializer {
                        MaxJsonLength=1024*1024
                    }
                    .DeserializeObject(line));
                }
                catch(ArgumentException) {
                    continue;
                }
                if(message==null)continue;
                if(Json.Get(message,"method")!=null) {
                    if(Json.Get(message,"id")!=null)Send(new {
                        id=Json.Get(message,"id"),error=new {
                            code=-32601,message="Read-only quota client"
                        }
                    });
                    continue;
                }
                if(Json.Number(Json.Get(message,"id"))!=id)continue;
                var error=Json.Map(Json.Get(message,"error"));
                if(error!=null) {
                    string text=Convert.ToString(Json.Get(error,"message"));
                    if(text.IndexOf("auth",StringComparison.OrdinalIgnoreCase)>=0||text.IndexOf("login",StringComparison.OrdinalIgnoreCase)>=0)throw new QuotaReadException("请先在 Codex 中登录 ChatGPT 账户");
                    throw new QuotaReadException("官方额度读取失败，请稍后重试");
                }
                return Json.Get(message,"result");
            }
        }
        void StopProcess() {
            var old=process;
            process=null;
            if(old==null)return;
            try {
                if(!old.HasExited)old.Kill();
            }
            catch(InvalidOperationException) {
            }
            catch(System.ComponentModel.Win32Exception) {
            }
            finally {
                old.Dispose();
            }
        }
        public void Dispose() {
            disposed=true;
            StopProcess();
        }
    }
    // Local preview only. No account details, tokens, arbitrary files, CORS or write endpoints.
    public sealed class QuotaPreviewServer : IDisposable {
        readonly TcpListener listener;
        readonly Func<string> state;
        readonly string htmlPath;
        volatile bool stopped;
        readonly SemaphoreSlim clients=new SemaphoreSlim(8,8);
        public string Url {
            get;
            private set;
        }
        public QuotaPreviewServer(string html,Func<string> getState) {
            htmlPath=html;
            state=getState;
            listener=new TcpListener(IPAddress.Loopback,0);
            listener.Start();
            int port=((IPEndPoint)listener.LocalEndpoint).Port;
            Url="http://127.0.0.1:"+port+"/";
            Task.Run(new Func<Task>(Accept));
        }
        async Task Accept() {
            while(!stopped) {
                TcpClient client;
                try {
                    client=await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch(ObjectDisposedException) {
                    break;
                }
                catch(SocketException) {
                    break;
                }
                if(!clients.Wait(0)) {
                    client.Close();
                    continue;
                }
                Dispatch(client);
            }
        }
        void Dispatch(TcpClient client) {
            Task.Run(async ()=> {
                try {
                    await Handle(client).ConfigureAwait(false);
                }
                catch {
                }
                finally {
                    client.Close();
                    clients.Release();
                }
            });
        }
        async Task<string> Line(StreamReader reader) {
            var task=reader.ReadLineAsync();
            if(await Task.WhenAny(task,Task.Delay(5000)).ConfigureAwait(false)!=task)throw new IOException("timeout");
            return await task.ConfigureAwait(false);
        }
        async Task Handle(TcpClient client) {
            using(var stream=client.GetStream())using(var reader=new StreamReader(stream,Encoding.ASCII,false,1024,true)) {
                string request=await Line(reader).ConfigureAwait(false);
                if(request==null||request.Length>2048)return;
                var parts=request.Split(' ');
                if(parts.Length!=3)return;
                var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                int total=request.Length;
                while(true) {
                    string line=await Line(reader).ConfigureAwait(false);
                    if(line==null)return;
                    if(line.Length==0)break;
                    total+=line.Length;
                    if(total>8192)return;
                    int colon=line.IndexOf(':');
                    if(colon>0)headers[line.Substring(0,colon)]=line.Substring(colon+1).Trim();
                }
                string expected=new Uri(Url).Authority;
                string host,origin;
                if(!headers.TryGetValue("Host",out host)||host!=expected||(headers.TryGetValue("Origin",out origin)&&origin!=Url.TrimEnd('/'))) {
                    await Reply(stream,403,"text/plain","Forbidden").ConfigureAwait(false);
                    return;
                }
                if(parts[0]!="GET") {
                    await Reply(stream,405,"text/plain","Method not allowed").ConfigureAwait(false);
                    return;
                }
                string path=parts[1].Split('?')[0];
                if(path=="/api/quota")await Reply(stream,200,"application/json; charset=utf-8",state()).ConfigureAwait(false);
                else if(path=="/"||path=="/preview.html")await Reply(stream,200,"text/html; charset=utf-8",File.ReadAllText(htmlPath,Encoding.UTF8)).ConfigureAwait(false);
                else await Reply(stream,404,"text/plain","Not found").ConfigureAwait(false);
            }
        }
        static async Task Reply(NetworkStream stream,int code,string type,string body) {
            byte[] bytes=Encoding.UTF8.GetBytes(body);
            string reason=code==200?"OK":code==403?"Forbidden":code==405?"Method Not Allowed":"Not Found";
            string header="HTTP/1.1 "+code+" "+reason+"\r\nContent-Type: "+type+"\r\nContent-Length: "+bytes.Length+"\r\nCache-Control: no-store\r\nConnection: close\r\nX-Content-Type-Options: nosniff\r\nCross-Origin-Resource-Policy: same-origin\r\nReferrer-Policy: no-referrer\r\nContent-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; connect-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'none'; form-action 'none'\r\n\r\n";
            byte[] head=Encoding.ASCII.GetBytes(header);
            await stream.WriteAsync(head,0,head.Length).ConfigureAwait(false);
            await stream.WriteAsync(bytes,0,bytes.Length).ConfigureAwait(false);
        }
        public void Dispose() {
            stopped=true;
            listener.Stop();
        }
    }
}
