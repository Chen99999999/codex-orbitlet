// Follow the official desktop window, never the codex CLI or quota app-server.
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
namespace Orbitlet {
    internal sealed class DesktopTarget {
        public readonly string Id,Name;
        public readonly string[] Processes;
        public DesktopTarget(string id,string name,params string[] processes) {
            Id=id;
            Name=name;
            Processes=processes;
        }
        public bool MatchesName(string name) {
            foreach(string process in Processes)if(string.Equals(process,name,StringComparison.OrdinalIgnoreCase))return true;
            return false;
        }
        public bool Matches(Process process) {
            if(!MatchesName(process.ProcessName))return false;
            if(Id!="codex")return true;
            string family=CodexDesktop.Package(process.Id);
            string path=string.IsNullOrEmpty(family)?process.MainModule.FileName:null;
            return CodexDesktop.IsDesktopIdentity(process.ProcessName,family,path);
        }
        public bool? IsOpen() {
            if(Id=="codex")return CodexDesktop.IsOpen();
            bool unknown=false;
            int session=Process.GetCurrentProcess().SessionId;
            foreach(string name in Processes)foreach(var process in Process.GetProcessesByName(name))using(process) {
                try {
                    if(process.SessionId==session&&process.MainWindowHandle!=IntPtr.Zero)return true;
                }
                catch(InvalidOperationException) {}
                catch(System.ComponentModel.Win32Exception) { unknown=true; }
            }
            return unknown?(bool?)null:false;
        }
    }

    internal static class DesktopTargets {
        public static readonly DesktopTarget[] All={
            new DesktopTarget("codex","Codex","ChatGPT"),
            new DesktopTarget("qoder","Qoder","Qoder","QoderCN"),
            new DesktopTarget("qoder-work","QoderWork","QoderWork","QoderWorkCN"),
            new DesktopTarget("claude","Claude Desktop","Claude"),
            new DesktopTarget("cursor","Cursor","Cursor"),
            new DesktopTarget("windsurf","Windsurf","Windsurf"),
            new DesktopTarget("vscode","VS Code","Code","Code - Insiders"),
            new DesktopTarget("trae","TRAE","Trae","Trae CN"),
            new DesktopTarget("kiro","Kiro","Kiro"),
            new DesktopTarget("antigravity","Antigravity","Antigravity"),
            new DesktopTarget("zed","Zed","zed"),
            new DesktopTarget("terminal","Windows Terminal","WindowsTerminal","WindowsTerminalPreview")
        };
        public static string SettingsPath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"orbitlet.settings"); } }
        public static DesktopTarget Find(string id) {
            foreach(var target in All)if(target.Id==id)return target;
            return null;
        }
        public static DesktopTarget ReadSelected() {
            try {
                foreach(var line in File.ReadLines(SettingsPath))
                    if(line.StartsWith("desktopTarget=",StringComparison.Ordinal))
                        return Find(line.Substring("desktopTarget=".Length))??All[0];
            }
            catch(IOException) {}
            catch(UnauthorizedAccessException) {}
            return All[0];
        }
    }

    public static class CodexDesktop {
        const string Family="OpenAI.Codex_2p2nqsd0c76g0";
        [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern int GetPackageFamilyName(IntPtr process,ref uint length,StringBuilder name);
        [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
        public static bool IsDesktopIdentity(string processName,string family,string path) {
            if(!string.Equals(processName,"ChatGPT",StringComparison.OrdinalIgnoreCase))return false;
            if(string.Equals(family,Family,StringComparison.OrdinalIgnoreCase))return true;
            string normalized=(path??"").Replace('/','\\');
            return normalized.IndexOf("\\WindowsApps\\OpenAI.Codex_",StringComparison.OrdinalIgnoreCase)>=0&&normalized.EndsWith("\\app\\ChatGPT.exe",StringComparison.OrdinalIgnoreCase);
        }
        internal static string Package(int pid) {
            IntPtr handle=OpenProcess(0x1000,false,pid);
            if(handle==IntPtr.Zero)return null;
            try {
                uint length=0;
                int result=GetPackageFamilyName(handle,ref length,null);
                if(result!=122||length==0)return "";
                var name=new StringBuilder((int)length);
                return GetPackageFamilyName(handle,ref length,name)==0?name.ToString():null;
            }
            finally {
                CloseHandle(handle);
            }
        }
        // null means inspection was unavailable; this never counts as a desktop exit.
        public static bool? IsOpen() {
            bool unknown=false;
            int session=Process.GetCurrentProcess().SessionId;
            foreach(var process in Process.GetProcessesByName("ChatGPT"))using(process) {
                try {
                    if(process.SessionId!=session)continue;
                    string family=Package(process.Id),path=null;
                    if(family==null||family.Length==0) {
                        try {
                            path=process.MainModule.FileName;
                        }
                        catch(System.ComponentModel.Win32Exception) {
                            unknown=true;
                            continue;
                        }
                    }
                    if(IsDesktopIdentity(process.ProcessName,family,path)&&process.MainWindowHandle!=IntPtr.Zero)return true;
                }
                catch(InvalidOperationException) {
                }
                catch(System.ComponentModel.Win32Exception) {
                    unknown=true;
                }
            }
            return unknown?(bool?)null:false;
        }
    }
    public sealed class DesktopAbsenceGuard {
        DateTime? since;
        public bool Observe(bool? open,DateTime now) {
            if(open!=false) {
                since=null;
                return false;
            }
            if(!since.HasValue)since=now;
            return (now-since.Value).TotalSeconds>=6;
        }
    }
    public sealed class DesktopLaunchGate {
        bool active;
        readonly DesktopAbsenceGuard absence=new DesktopAbsenceGuard();
        public bool Observe(bool? open,DateTime now) {
            if(open==true) {
                absence.Observe(true,now);
                if(!active) {
                    active=true;
                    return true;
                }
            }
            else if(absence.Observe(open,now))active=false;
            return false;
        }
    }
    public static class CodexFollower {
        public const string WidgetMutex="QuietOrbit.TwoArcs.v2",WatcherMutex="QuietOrbit.CodexFollower.v1";
        public const string StopEvent="QuietOrbit.CodexFollower.Stop.v1",DetachEvent="QuietOrbit.CodexFollower.Detach.v1",AttachEvent="QuietOrbit.CodexFollower.Attach.v1",CloseEvent="QuietOrbit.Widget.Close.v1";
        public static bool IsRunning(string name) {
            Mutex mutex;
            if(!Mutex.TryOpenExisting(name,out mutex))return false;
            mutex.Dispose();
            return true;
        }
        public static bool Signal(string name) {
            try {
                using(var handle=EventWaitHandle.OpenExisting(name)) {
                    handle.Set();
                    return true;
                }
            }
            catch(WaitHandleCannotBeOpenedException) {
                return false;
            }
        }
        public static void Start() {
            Process.Start(new ProcessStartInfo {
                FileName=Assembly.GetExecutingAssembly().Location,Arguments="--watch-codex",UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory
            });
        }
        public static void Run() {
            bool created;
            using(var mutex=new Mutex(true,WatcherMutex,out created)) {
                if(!created)return;
                using(var stop=new EventWaitHandle(false,EventResetMode.ManualReset,StopEvent)) {
                    var gate=new DesktopLaunchGate();
                    var target=DesktopTargets.ReadSelected();
                    Process child=null;
                    bool rebind=false;
                    do {
                        var selected=DesktopTargets.ReadSelected();
                        if(selected!=target) {
                            target=selected;
                            gate=new DesktopLaunchGate();
                        }
                        bool? open=target.IsOpen();
                        bool launch=gate.Observe(open,DateTime.UtcNow);
                        if(child!=null&&child.HasExited) {
                            rebind=child.ExitCode==42;
                            child.Dispose();
                            child=null;
                        }
                        if((launch||rebind&&open==true)&&!IsRunning(WidgetMutex)) {
                            try {
                                child=Process.Start(new ProcessStartInfo {
                                    FileName=Assembly.GetExecutingAssembly().Location,Arguments="--follow-codex",UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory
                                });
                                rebind=false;
                            }
                            catch(System.ComponentModel.Win32Exception) {
                            }
                            catch(IOException) {
                            }
                        }
                    }
                    while(!stop.WaitOne(2000));
                }
            }
        }
    }
    public static class FollowRegistration {
        public static string ShortcutPath {
            get {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"Orbitlet - Codex Follow.lnk");
            }
        }
        public static bool Enabled {
            get {
                return File.Exists(ShortcutPath);
            }
        }
        static object Invoke(object target,string name,params object[] arguments) {
            return target.GetType().InvokeMember(name,BindingFlags.InvokeMethod,null,target,arguments);
        }
        static void Set(object target,string name,object value) {
            target.GetType().InvokeMember(name,BindingFlags.SetProperty,null,target,new[] {
                value
            });
        }
        static object Get(object target,string name) {
            return target.GetType().InvokeMember(name,BindingFlags.GetProperty,null,target,null);
        }
        public static void Enable() {
            object shell=null,shortcut=null;
            try {
                shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                shortcut=Invoke(shell,"CreateShortcut",ShortcutPath);
                if(Enabled&&!string.Equals(Convert.ToString(Get(shortcut,"TargetPath")),Assembly.GetExecutingAssembly().Location,StringComparison.OrdinalIgnoreCase))throw new IOException("已有同名启动项，请先检查其目标路径");
                Set(shortcut,"TargetPath",Assembly.GetExecutingAssembly().Location);
                Set(shortcut,"Arguments","--watch-codex");
                Set(shortcut,"WorkingDirectory",AppDomain.CurrentDomain.BaseDirectory);
                Set(shortcut,"WindowStyle",7);
                Set(shortcut,"Description","Orbitlet：随选定的桌面应用启动和退出");
                Invoke(shortcut,"Save");
            }
            finally {
                if(shortcut!=null)Marshal.FinalReleaseComObject(shortcut);
                if(shell!=null)Marshal.FinalReleaseComObject(shell);
            }
            string legacy=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"Quiet Orbit - Codex Follow.lnk");
            if(File.Exists(legacy)) {
                object oldShell=null,oldLink=null;
                try {
                    oldShell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                    oldLink=Invoke(oldShell,"CreateShortcut",legacy);
                    string target=Convert.ToString(Get(oldLink,"TargetPath"));
                    if(Path.GetFileName(target).Equals("QuietOrbit.exe",StringComparison.OrdinalIgnoreCase)&&Convert.ToString(Get(oldLink,"Arguments"))=="--watch-codex")File.Delete(legacy);
                }
                finally {
                    if(oldLink!=null)Marshal.FinalReleaseComObject(oldLink);
                    if(oldShell!=null)Marshal.FinalReleaseComObject(oldShell);
                }
            }
            CodexFollower.Signal(CodexFollower.AttachEvent);
            StartWatcher();
        }
        static void StartWatcher() {
            if(!CodexFollower.IsRunning(CodexFollower.WatcherMutex))CodexFollower.Start();
        }
        public static void Disable() {
            if(Enabled) {
                object shell=null,shortcut=null;
                try {
                    shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                    shortcut=Invoke(shell,"CreateShortcut",ShortcutPath);
                    if(!string.Equals(Convert.ToString(Get(shortcut,"TargetPath")),Assembly.GetExecutingAssembly().Location,StringComparison.OrdinalIgnoreCase))throw new IOException("启动项目标不属于此程序，未修改");
                    File.Delete(ShortcutPath);
                }
                finally {
                    if(shortcut!=null)Marshal.FinalReleaseComObject(shortcut);
                    if(shell!=null)Marshal.FinalReleaseComObject(shell);
                }
            }
            CodexFollower.Signal(CodexFollower.StopEvent);
            CodexFollower.Signal(CodexFollower.DetachEvent);
        }
        public static object Status() {
            return new {
                enabled=Enabled,desktopOpen=DesktopTargets.ReadSelected().IsOpen(),watcherRunning=CodexFollower.IsRunning(CodexFollower.WatcherMutex),widgetRunning=CodexFollower.IsRunning(CodexFollower.WidgetMutex),startupShortcut=ShortcutPath
            };
        }
    }
}
