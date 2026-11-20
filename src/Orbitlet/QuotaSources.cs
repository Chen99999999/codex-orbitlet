using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Orbitlet {
    internal sealed class QuotaSource {
        public readonly string Id,Name,Outer,Inner;
        public readonly bool TimeWindows;
        public readonly string Endpoint,KeyVariable;

        public QuotaSource(string id,string name,string outer,string inner,bool timeWindows=false,string endpoint=null,string keyVariable=null) {
            Id=id;
            Name=name;
            Outer=outer;
            Inner=inner;
            TimeWindows=timeWindows;
            Endpoint=endpoint;
            KeyVariable=keyVariable;
        }

        public QuotaSnapshot Empty() {
            return new QuotaSnapshot {
                Source=Id,ProviderName=Name,OuterLabel=Outer,InnerLabel=Inner,TimeWindows=TimeWindows
            };
        }
    }

    internal static class QuotaSources {
        public static readonly QuotaSource[] All={
            new QuotaSource("codex-app-server","Codex","5h","本周",true),
            new QuotaSource("claude","Claude Code","5h","本周",true),
            new QuotaSource("qoder","Qoder","套餐","附加"),
            new QuotaSource("qoder-cn","Qoder CN","套餐","附加"),
            new QuotaSource("zai","Z.ai GLM","模型","MCP",false,"https://api.z.ai/api/monitor/usage/quota/limit","ZAI_API_KEY"),
            new QuotaSource("bigmodel","智谱 GLM","模型","MCP",false,"https://open.bigmodel.cn/api/monitor/usage/quota/limit","BIGMODEL_API_KEY"),
            new QuotaSource("minimax","MiniMax","5h","本周",true,"https://api.minimax.io/v1/token_plan/remains","MINIMAX_API_KEY"),
            new QuotaSource("minimax-cn","MiniMax CN","5h","本周",true,"https://api.minimax.cn/v1/token_plan/remains","MINIMAX_CN_API_KEY"),
            new QuotaSource("openrouter","OpenRouter","剩余","已用",false,"https://openrouter.ai/api/v1/key","OPENROUTER_API_KEY"),
            new QuotaSource("deepseek","DeepSeek","总额","赠金",false,"https://api.deepseek.com/user/balance","DEEPSEEK_API_KEY"),
            new QuotaSource("moonshot","Kimi API CN","可用","现金",false,"https://api.moonshot.cn/v1/users/me/balance","MOONSHOT_API_KEY"),
            new QuotaSource("moonshot-global","Kimi API","可用","现金",false,"https://api.moonshot.ai/v1/users/me/balance","MOONSHOT_GLOBAL_API_KEY"),
            new QuotaSource("none","仅桌宠","额度","额度")
        };
        static readonly DateTime Epoch=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc);

        public static QuotaSource Find(string id) {
            if(id=="codex")return All[0];
            foreach(var source in All)if(source.Id==id)return source;
            return null;
        }

        public static async Task<QuotaSnapshot> ReadAsync(QuotaSource source,CodexQuotaClient codex) {
            if(source==All[0])return await codex.ReadAsync().ConfigureAwait(false);
            if(source.Id=="none")return source.Empty();
            if(source.Id=="claude")return ReadClaude();
            if(source.Id=="qoder"||source.Id=="qoder-cn") {
                var result=await ReadQoder(source.Id).ConfigureAwait(false);
                return Parse(source,result,DateTime.UtcNow);
            }
            var response=await Task.Run(()=>ReadEndpoint(source)).ConfigureAwait(false);
            return Parse(source,response,DateTime.UtcNow);
        }

        static Dictionary<string,object> Map(Dictionary<string,object> root,string key) {
            return Json.Map(Json.Get(root,key));
        }

        static double? Number(Dictionary<string,object> root,string key) {
            return Json.Number(Json.Get(root,key));
        }

        static double Clamp(double value) {
            return Math.Max(0,Math.Min(100,value));
        }

        static double? Remaining(double? used) {
            return used.HasValue ? Clamp(100-used.Value) : (double?)null;
        }

        static double? Ratio(double? remaining,double? total) {
            if(!remaining.HasValue||!total.HasValue||total.Value<=0)return null;
            return Clamp(remaining.Value/total.Value*100);
        }

        static long? Timestamp(double? seconds) {
            if(!seconds.HasValue||seconds.Value<=0||seconds.Value>253402300799)return null;
            return (long)seconds.Value;
        }

        static double? DecimalNumber(Dictionary<string,object> root,string key) {
            var value=Json.Get(root,key);
            double parsed;
            if(value is string&&double.TryParse((string)value,NumberStyles.Float,CultureInfo.InvariantCulture,out parsed))
                return Json.Number(parsed);
            return Json.Number(value);
        }

        static string Money(double? value,string symbol) {
            if(!value.HasValue)return null;
            double absolute=Math.Abs(value.Value);
            if(absolute>=100000000)return symbol+(value.Value<0?"-":"")+">99M";
            if(absolute>=1000000)return symbol+(value.Value/1000000).ToString("0.#",CultureInfo.InvariantCulture)+"M";
            if(absolute>=1000)return symbol+(value.Value/1000).ToString("0.#",CultureInfo.InvariantCulture)+"k";
            return symbol+value.Value.ToString(absolute>=100?"0":"0.##",CultureInfo.InvariantCulture);
        }

        public static QuotaSnapshot Parse(QuotaSource source,object payload,DateTime now) {
            var root=Json.Map(payload);
            var snapshot=source.Empty();
            snapshot.UpdatedAtUtc=now;
            switch(source.Id) {
                case "qoder":
                case "qoder-cn":
                    var plan=Map(root,"userQuota");
                    var addOn=Map(root,"addOnQuota");
                    var organization=Map(root,"orgResourcePackage");
                    snapshot.OuterPercent=CreditBucket(plan,"total");
                    snapshot.InnerPercent=CreditBucket(addOn,"total");
                    if(snapshot.InnerPercent==null&&organization!=null&&Equals(Json.Get(organization,"available"),true)) {
                        snapshot.InnerLabel="组织";
                        snapshot.InnerPercent=CreditBucket(organization,"cap");
                    }
                    snapshot.OuterEndsAt=Timestamp(Number(root,"expiresAt")/1000);
                    break;
                case "zai":
                case "bigmodel":
                    var limits=Json.Get(Map(root,"data"),"limits") as object[];
                    ReadGlmLimits(limits,snapshot);
                    break;
                case "minimax":
                case "minimax-cn":
                    if(Number(Map(root,"base_resp"),"status_code").GetValueOrDefault()!=0)break;
                    ReadMiniMax(Json.Get(root,"model_remains") as object[],snapshot);
                    break;
                case "openrouter":
                    var key=Map(root,"data");
                    var balance=Number(key,"limit_remaining");
                    snapshot.OuterPercent=Ratio(balance,Number(key,"limit"));
                    if(!snapshot.OuterPercent.HasValue)snapshot.OuterAmount=Money(balance,"$");
                    snapshot.InnerAmount=Money(Number(key,"usage"),"$");
                    if(balance.HasValue)snapshot.Detail="Key 余额 USD "+balance.Value.ToString("G",CultureInfo.InvariantCulture);
                    break;
                case "deepseek":
                    var balances=Json.Get(root,"balance_infos") as object[];
                    ReadDeepSeek(balances,snapshot);
                    break;
                case "moonshot":
                case "moonshot-global":
                    var wallet=Map(root,"data");
                    if(Number(root,"code").GetValueOrDefault()!=0||Equals(Json.Get(root,"status"),false))break;
                    var symbol=source.Id=="moonshot"?"¥":"$";
                    snapshot.OuterAmount=Money(Number(wallet,"available_balance"),symbol);
                    snapshot.InnerAmount=Money(Number(wallet,"cash_balance"),symbol);
                    break;
                case "claude":
                    var limitsByWindow=Map(root,"rate_limits");
                    ReadClaudeWindow(Map(limitsByWindow,"five_hour"),now,out snapshot.OuterPercent,out snapshot.OuterEndsAt);
                    ReadClaudeWindow(Map(limitsByWindow,"seven_day"),now,out snapshot.InnerPercent,out snapshot.InnerEndsAt);
                    break;
            }
            if(snapshot.FirstPercent==null&&snapshot.SecondPercent==null&&snapshot.OuterAmount==null&&snapshot.InnerAmount==null)
                throw new QuotaReadException(source.Id=="claude"?"等待 Claude Code 状态栏提供有效额度":"平台未返回可显示的额度");
            return snapshot;
        }

        static double? CreditBucket(Dictionary<string,object> bucket,string limitKey) {
            var total=Number(bucket,limitKey);
            var remaining=Number(bucket,"remaining");
            var used=Number(bucket,"used");
            if(total.HasValue&&total.Value<=0)return null;
            return Ratio(remaining??(total-used),total);
        }

        static void ReadGlmLimits(object[] limits,QuotaSnapshot snapshot) {
            if(limits==null)return;
            var model=new List<Dictionary<string,object>>();
            var mcp=new List<Dictionary<string,object>>();
            foreach(var entry in limits) {
                var item=Json.Map(entry);
                var type=Json.Get(item,"type") as string;
                if(type=="TOKENS_LIMIT"&&Number(item,"percentage").HasValue)model.Add(item);
                if(type=="TIME_LIMIT"&&Number(item,"percentage").HasValue)mcp.Add(item);
            }
            // The vendor plugin distinguishes the model and MCP buckets by type.
            // If the API includes more than one window, label the tightest remaining allowance as such.
            var tightest=Tightest(model);
            snapshot.OuterPercent=Remaining(Number(tightest,"percentage"));
            snapshot.OuterEndsAt=Timestamp(Number(tightest,"nextResetTime")/1000);
            var tools=Tightest(mcp);
            snapshot.InnerPercent=Remaining(Number(tools,"percentage"));
            snapshot.InnerEndsAt=Timestamp(Number(tools,"nextResetTime")/1000);
            if(model.Count>1||mcp.Count>1)snapshot.Detail="显示同类额度中剩余比例最低的窗口";
        }

        static Dictionary<string,object> Tightest(List<Dictionary<string,object>> items) {
            Dictionary<string,object> chosen=null;
            foreach(var item in items)if(chosen==null||Number(item,"percentage")>Number(chosen,"percentage"))chosen=item;
            return chosen;
        }

        static double? MiniMaxRemaining(Dictionary<string,object> model,string window) {
            string prefix="current_"+window+"_";
            var percent=Number(model,prefix+"remaining_percent");
            if(percent.HasValue&&percent>=0&&percent<=100)return percent;
            var total=Number(model,prefix+"total_count");
            var count=Number(model,prefix+"usage_count");
            // Older responses express the count as remaining. Prefer an explicit percentage.
            return count>=0&&count<=total?Ratio(count,total):null;
        }

        static void ReadMiniMax(object[] entries,QuotaSnapshot snapshot) {
            if(entries==null)return;
            foreach(var entry in entries) {
                var model=Json.Map(entry);
                var name=Json.Get(model,"model_name") as string;
                if(name==null||!name.StartsWith("MiniMax-M",StringComparison.OrdinalIgnoreCase))continue;
                var end=Number(model,"end_time");
                var interval=end-Number(model,"start_time");
                if(interval>=17940000&&interval<=18060000) {
                    var remaining=MiniMaxRemaining(model,"interval");
                    if(remaining.HasValue&&(!snapshot.OuterPercent.HasValue||remaining<snapshot.OuterPercent)) {
                        snapshot.OuterPercent=remaining;
                        snapshot.OuterEndsAt=Timestamp(end/1000);
                    }
                }
                if(Number(model,"current_weekly_status")==3&&Number(model,"current_weekly_total_count")==0)continue;
                var weekly=MiniMaxRemaining(model,"weekly");
                if(weekly.HasValue&&(!snapshot.InnerPercent.HasValue||weekly<snapshot.InnerPercent)) {
                    snapshot.InnerPercent=weekly;
                    snapshot.InnerEndsAt=Timestamp(Number(model,"weekly_end_time")/1000);
                }
            }
        }

        static void ReadDeepSeek(object[] balances,QuotaSnapshot snapshot) {
            if(balances==null)return;
            foreach(var entry in balances) {
                var wallet=Json.Map(entry);
                var currency=Json.Get(wallet,"currency") as string;
                if(currency!="CNY"&&currency!="USD")continue;
                string symbol=currency=="CNY"?"¥":"$";
                string total=Money(DecimalNumber(wallet,"total_balance"),symbol);
                string granted=Money(DecimalNumber(wallet,"granted_balance"),symbol);
                if(total==null&&granted==null)continue;
                if(snapshot.OuterAmount==null&&snapshot.InnerAmount==null) {
                    snapshot.OuterAmount=total;
                    snapshot.InnerAmount=granted;
                }
                var amount=DecimalNumber(wallet,"total_balance");
                if(amount.HasValue)snapshot.Detail=(snapshot.Detail==null?"":snapshot.Detail+"\n")+currency+" "+amount.Value.ToString("G",CultureInfo.InvariantCulture);
            }
        }

        static void ReadClaudeWindow(Dictionary<string,object> window,DateTime now,out double? percent,out long? endsAt) {
            endsAt=Timestamp(Number(window,"resets_at"));
            percent=endsAt.HasValue&&endsAt.Value<=(now-Epoch).TotalSeconds?null:Remaining(Number(window,"used_percentage"));
            if(!percent.HasValue)endsAt=null;
        }

        static QuotaSnapshot ReadClaude() {
            var path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"claude-usage.settings");
            if(!File.Exists(path))throw new QuotaReadException("先按 README 配置 Claude Code 状态栏");
            var observed=File.GetLastWriteTimeUtc(path);
            var now=DateTime.UtcNow;
            if(observed<now.AddHours(-8))throw new QuotaReadException("Claude Code 状态栏数据已过期");
            var snapshot=Parse(Find("claude"),Decode(File.ReadAllText(path,Encoding.UTF8)),now);
            snapshot.UpdatedAtUtc=observed;
            return snapshot;
        }

        public static void IngestClaudeStatusLine() {
            try {
                var buffer=new char[128*1024+1];
                int count=Console.In.ReadBlock(buffer,0,buffer.Length);
                if(count==buffer.Length)return;
                var limits=Map(Json.Map(Decode(new string(buffer,0,count))),"rate_limits");
                var windows=new Dictionary<string,object>();
                var captions=new List<string>();
                foreach(string name in new[] {"five_hour","seven_day"}) {
                    var bucket=Map(limits,name);
                    var used=Number(bucket,"used_percentage");
                    var reset=Timestamp(Number(bucket,"resets_at"));
                    if(!used.HasValue||reset.HasValue&&reset.Value<=(DateTime.UtcNow-Epoch).TotalSeconds)continue;
                    windows[name]=new {used_percentage=used.Value,resets_at=reset};
                    captions.Add((name=="five_hour"?"5h":"7d")+" "+Remaining(used).Value.ToString("0",CultureInfo.InvariantCulture)+"% left");
                }
                if(windows.Count==0)return;
                var path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"claude-usage.settings");
                var temporary=path+"."+Process.GetCurrentProcess().Id+".tmp";
                try {
                    File.WriteAllText(temporary,Json.Encode(new {rate_limits=windows}));
                    if(File.Exists(path))File.Replace(temporary,path,null);
                    else File.Move(temporary,path);
                }
                finally {
                    if(File.Exists(temporary))File.Delete(temporary);
                }
                Console.WriteLine(string.Join(" | ",captions));
            }
            catch(IOException) {}
            catch(UnauthorizedAccessException) {}
            catch(ArgumentException) {}
            catch(InvalidOperationException) {}
        }

        static object ReadEndpoint(QuotaSource source) {
            var key=Environment.GetEnvironmentVariable(source.KeyVariable);
            if(string.IsNullOrWhiteSpace(key))
                key=Environment.GetEnvironmentVariable(source.KeyVariable,EnvironmentVariableTarget.User);
            if(string.IsNullOrWhiteSpace(key))throw new QuotaReadException("请设置 "+source.KeyVariable+" 后刷新");
            var request=(HttpWebRequest)WebRequest.Create(source.Endpoint);
            request.Method="GET";
            request.Timeout=10000;
            request.ReadWriteTimeout=10000;
            request.AllowAutoRedirect=false;
            request.Accept="application/json";
            request.Headers[HttpRequestHeader.Authorization]=(source.Id=="zai"||source.Id=="bigmodel"?"":"Bearer ")+key.Trim();
            try {
                using(var response=(HttpWebResponse)request.GetResponse())
                using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8)) {
                    var buffer=new char[128*1024+1];
                    var count=reader.ReadBlock(buffer,0,buffer.Length);
                    if(count==buffer.Length)throw new QuotaReadException("额度响应超出大小限制");
                    return Decode(new string(buffer,0,count));
                }
            }
            catch(WebException error) {
                using(var response=error.Response as HttpWebResponse) {
                    if(response!=null&&(response.StatusCode==HttpStatusCode.Unauthorized||response.StatusCode==HttpStatusCode.Forbidden))
                        throw new QuotaReadException("额度查询未获授权，请检查 "+source.KeyVariable);
                }
                throw new QuotaReadException("额度服务暂时不可用");
            }
        }

        static async Task<object> ReadQoder(string id) {
            var directory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"integrations");
            var folder=id=="qoder-cn"?"qoder-cn":"qoder";
            var package=id=="qoder-cn"?"@qodercn-ai/qodercn-agent-sdk":"@qoder-ai/qoder-agent-sdk";
            if(!Directory.Exists(Path.Combine(directory,folder,"node_modules",package)))
                throw new QuotaReadException("先运行 integrations 中的 Qoder SDK 安装脚本");
            var node=FindNode();
            var start=new ProcessStartInfo(node,"qoder-usage.mjs "+id) {
                WorkingDirectory=directory,UseShellExecute=false,CreateNoWindow=true,
                RedirectStandardOutput=true,RedirectStandardError=true,
                StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8
            };
            start.EnvironmentVariables.Remove("NODE_OPTIONS");
            start.EnvironmentVariables.Remove("NODE_PATH");
            using(var process=new Process { StartInfo=start }) {
                process.ErrorDataReceived+=(sender,args)=>{};
                process.Start();
                process.BeginErrorReadLine();
                Task<string> output=process.StandardOutput.ReadToEndAsync();
                if(await Task.WhenAny(output,Task.Delay(30000)).ConfigureAwait(false)!=output) {
                    try { if(!process.HasExited)process.Kill(); }
                    catch(InvalidOperationException) {}
                    throw new QuotaReadException("Qoder 读取超时");
                }
                var text=await output.ConfigureAwait(false);
                foreach(var line in text.Split('\n')) {
                    const string prefix="ORBITLET_USAGE ";
                    if(line.StartsWith(prefix,StringComparison.Ordinal))return Decode(line.Substring(prefix.Length));
                }
                throw new QuotaReadException("Qoder 额度不可用，请检查 CLI 登录和 SDK 版本");
            }
        }

        static string FindNode() {
            foreach(var entry in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator)) {
                string directory=entry.Trim().Trim('"');
                if(!Path.IsPathRooted(directory))continue;
                string path=Path.Combine(directory,"node.exe");
                if(File.Exists(path))return path;
            }
            throw new QuotaReadException("Qoder 适配需要 Node.js，请安装后重启 Orbitlet");
        }

        static object Decode(string json) {
            return new JavaScriptSerializer { MaxJsonLength=128*1024 }.DeserializeObject(json);
        }
    }
}
