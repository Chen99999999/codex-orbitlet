using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
namespace Orbitlet {
    public static class AttachmentLayout {
        public static bool ShouldShow(bool hostExists,bool minimized,bool foregroundAllowed,bool manuallyHidden,bool fits) {
            return hostExists&&!minimized&&foregroundAllowed&&!manuallyHidden&&fits;
        }
        public static Point Position(Rect host,Size widget,double right,double bottom) {
            double x=host.Right-widget.Width-Math.Max(8,right),y=host.Bottom-widget.Height-Math.Max(8,bottom);
            return new Point(Math.Max(host.Left+8,Math.Min(x,host.Right-widget.Width-8)),Math.Max(host.Top+8,Math.Min(y,host.Bottom-widget.Height-8)));
        }
    }
    public sealed class WindowAttachment {
        [StructLayout(LayoutKind.Sequential)]struct NativeRect {
            public int Left,Top,Right,Bottom;
        }
        [StructLayout(LayoutKind.Sequential)]struct NativePoint {
            public int X,Y;
        }
        [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
        [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr window,uint flags);
        [DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr window,uint command);
        [DllImport("user32.dll")]static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")]static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")]static extern bool GetClientRect(IntPtr window,out NativeRect rect);
        [DllImport("user32.dll")]static extern bool ClientToScreen(IntPtr window,ref NativePoint point);
        [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)]static extern IntPtr SetOwner64(IntPtr window,int index,IntPtr owner);
        [DllImport("user32.dll",EntryPoint="SetWindowLongW",SetLastError=true)]static extern int SetOwner32(IntPtr window,int index,int owner);
        [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        readonly Window widget;
        readonly int ownPid=Process.GetCurrentProcess().Id;
        readonly Dictionary<int,Tuple<bool,DateTime>> identity=new Dictionary<int,Tuple<bool,DateTime>>();
        IntPtr host;
        DateTime nextScan;
        string lastDiagnostic;
        public WindowAttachment(Window window) {
            widget=window;
        }
        public IntPtr Host {
            get {
                return host;
            }
        }
        static void SetOwner(IntPtr window,IntPtr owner) {
            if(IntPtr.Size==8)SetOwner64(window,-8,owner);
            else SetOwner32(window,-8,owner.ToInt32());
        }
        bool IsCodex(IntPtr window) {
            uint pid;
            GetWindowThreadProcessId(window,out pid);
            if(pid==0||pid==ownPid)return false;
            Tuple<bool,DateTime> cached;
            if(identity.TryGetValue((int)pid,out cached)&&cached.Item2>DateTime.UtcNow)return cached.Item1;
            bool matches=false;
            try {
                using(var process=Process.GetProcessById((int)pid)) {
                    if(process.ProcessName.Equals("ChatGPT",StringComparison.OrdinalIgnoreCase)) {
                        string family=CodexDesktop.Package(process.Id),path=null;
                        if(string.IsNullOrEmpty(family))path=process.MainModule.FileName;
                        matches=CodexDesktop.IsDesktopIdentity(process.ProcessName,family,path);
                    }
                }
            }
            catch(InvalidOperationException) {
            }
            catch(ArgumentException) {
            }
            catch(System.ComponentModel.Win32Exception) {
            }
            identity[(int)pid]=Tuple.Create(matches,DateTime.UtcNow.AddSeconds(5));
            return matches;
        }
        IntPtr ResolveHost(IntPtr foreground) {
            IntPtr root=GetAncestor(foreground,3);
            if(root==IntPtr.Zero)root=foreground;
            if(IsCodex(root))return root;
            if(host!=IntPtr.Zero&&IsWindow(host)&&IsCodex(host))return host;
            if(DateTime.UtcNow<nextScan)return IntPtr.Zero;
            nextScan=DateTime.UtcNow.AddSeconds(2);
            foreach(var process in Process.GetProcessesByName("ChatGPT"))using(process) {
                try {
                    IntPtr handle=process.MainWindowHandle;
                    if(handle!=IntPtr.Zero&&IsCodex(handle))return handle;
                }
                catch(InvalidOperationException) {
                }
                catch(System.ComponentModel.Win32Exception) {
                }
            }
            return IntPtr.Zero;
        }
        public Rect? ClientBounds() {
            if(host==IntPtr.Zero||!IsWindow(host))return null;
            NativeRect rect;
            if(!GetClientRect(host,out rect))return null;
            var first=new NativePoint {
                X=rect.Left,Y=rect.Top
            };
            var last=new NativePoint {
                X=rect.Right,Y=rect.Bottom
            };
            if(!ClientToScreen(host,ref first)||!ClientToScreen(host,ref last))return null;
            var source=PresentationSource.FromVisual(widget);
            Matrix transform=source!=null?source.CompositionTarget.TransformFromDevice:Matrix.Identity;
            Point a=transform.Transform(new Point(first.X,first.Y)),b=transform.Transform(new Point(last.X,last.Y));
            return new Rect(a,b);
        }
        public void Detach() {
            IntPtr own=new WindowInteropHelper(widget).Handle;
            if(own!=IntPtr.Zero)SetOwner(own,IntPtr.Zero);
            host=IntPtr.Zero;
            widget.Opacity=1;
        }
        public void Apply(bool manuallyHidden,bool dragging,double right,double bottom) {
            IntPtr own=new WindowInteropHelper(widget).Handle;
            if(own==IntPtr.Zero)return;
            IntPtr foreground=GetForegroundWindow(),candidate=ResolveHost(foreground);
            bool changed=candidate!=host;
            if(changed) {
                host=candidate;
                if(host!=IntPtr.Zero)SetOwner(own,host);
            }
            widget.Topmost=false;
            bool exists=host!=IntPtr.Zero&&IsWindow(host)&&IsWindowVisible(host),minimized=exists&&IsIconic(host);
            bool active=exists&&(foreground==host||GetAncestor(foreground,3)==host||foreground==own);
            Rect? bounds=ClientBounds();
            bool fits=bounds.HasValue&&bounds.Value.Width>=widget.Width+16&&bounds.Value.Height>=widget.Height+16;
            bool show=AttachmentLayout.ShouldShow(exists,minimized,active,manuallyHidden,fits);
            if(show) {
                if(!dragging) {
                    Point point=AttachmentLayout.Position(bounds.Value,new Size(widget.Width,widget.Height),right,bottom);
                    widget.Left=point.X;
                    widget.Top=point.Y;
                }
                bool wasHidden=!widget.IsVisible;
                widget.Opacity=1;
                if(wasHidden) {
                    widget.ShowActivated=false;
                    widget.Show();
                }
                if(wasHidden||changed)SetWindowPos(own,IntPtr.Zero,0,0,0,0,0x0010|0x0001|0x0002);
            }
            else {
                widget.Opacity=0;
                if(widget.IsVisible)widget.Hide();
            }
            if(Environment.GetEnvironmentVariable("ORBITLET_BINDING_DIAGNOSTICS")=="1") {
                bool ownerMatches=host!=IntPtr.Zero&&GetWindow(own,4)==host;
                bool inside=bounds.HasValue&&bounds.Value.Contains(new Rect(widget.Left,widget.Top,widget.Width,widget.Height));
                string state=Json.Encode(new {
                    bound=true,hostDetected=exists,visible=widget.IsVisible,foregroundAllowed=active,hostMinimized=minimized,ownerMatchesHost=ownerMatches,insideHost=inside
                });
                if(state!=lastDiagnostic) {
                    lastDiagnostic=state;
                    try {
                        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"binding-diagnostics.json"),state);
                    }
                    catch(IOException) {
                    }
                    catch(UnauthorizedAccessException) {
                    }
                }
            }
        }
        public void CaptureAnchor(ref double right,ref double bottom) {
            Rect? bounds=ClientBounds();
            if(!bounds.HasValue)return;
            right=Math.Max(8,bounds.Value.Right-widget.Left-widget.Width);
            bottom=Math.Max(8,bounds.Value.Bottom-widget.Top-widget.Height);
        }
    }
}
