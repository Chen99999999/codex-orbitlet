using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Orbitlet;

class NativeAttachmentTests {
    [DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr window,uint command);
    [DllImport("user32.dll")]static extern bool IsWindow(IntPtr window);
    static int count;

    static void Assert(bool condition,string label) {
        if(!condition)throw new Exception(label);
        count++;
    }
    static Window OffscreenWindow(double width,double height) {
        return new Window {
            Width=width,Height=height,Left=-20000,Top=-20000,
            ShowActivated=false,ShowInTaskbar=false,WindowStyle=WindowStyle.None,
            ResizeMode=ResizeMode.NoResize,Background=Brushes.White
        };
    }
    static bool Inside(WindowAttachment attachment,Window widget) {
        Rect? bounds=attachment.ClientBounds();
        return bounds.HasValue&&bounds.Value.Contains(new Rect(widget.Left,widget.Top,widget.Width,widget.Height));
    }
    static async Task Run() {
        var host=OffscreenWindow(600,450);
        var widget=OffscreenWindow(104,106);
        try {
            host.Show();
            widget.Show();
            await Task.Delay(100);
            IntPtr hostHandle=new WindowInteropHelper(host).Handle;
            IntPtr own=new WindowInteropHelper(widget).Handle;
            IntPtr foreground=hostHandle;
            var attachment=new WindowAttachment(widget,()=>hostHandle,()=>foreground);
            widget.Topmost=true;
            attachment.Apply(false,false,20,20);
            await Task.Delay(100);
            Assert(GetWindow(own,4)==hostHandle,"Native owner is the host window");
            Assert(!widget.Topmost,"Bound widget cannot remain globally on top");
            Assert(widget.IsVisible&&widget.Opacity==1,"Visible when host is active");
            Assert(Inside(attachment,widget),"Native client bounds contain the widget");
            double oldLeft=widget.Left,oldTop=widget.Top;
            host.Left+=180;
            host.Top+=120;
            await Task.Delay(100);
            attachment.Apply(false,false,20,20);
            Assert(Math.Abs(widget.Left-oldLeft-180)<2&&Math.Abs(widget.Top-oldTop-120)<2,"Follows native host movement");
            oldLeft=widget.Left;
            oldTop=widget.Top;
            host.Width+=160;
            host.Height+=90;
            await Task.Delay(100);
            attachment.Apply(false,false,20,20);
            Assert(Math.Abs(widget.Left-oldLeft-160)<2&&Math.Abs(widget.Top-oldTop-90)<2,"Follows native host resizing");
            Assert(Inside(attachment,widget),"Remains inside resized host");
            foreground=IntPtr.Zero;
            attachment.Apply(false,false,20,20);
            Assert(!widget.IsVisible&&widget.Opacity==0,"Hidden when another application is active");
            foreground=hostHandle;
            attachment.Apply(false,false,20,20);
            Assert(widget.IsVisible,"Returns with its host");
            attachment.Apply(true,false,20,20);
            Assert(!widget.IsVisible,"Manual hide remains hidden");
            attachment.Apply(true,false,20,20);
            Assert(!widget.IsVisible,"Polling does not undo manual hide");
            attachment.Apply(false,false,20,20);
            host.WindowState=WindowState.Minimized;
            await Task.Delay(100);
            attachment.Apply(false,false,20,20);
            Assert(!widget.IsVisible,"Minimizing the host hides the widget");
            host.WindowState=WindowState.Normal;
            await Task.Delay(100);
            attachment.Apply(false,false,20,20);
            Assert(widget.IsVisible&&Inside(attachment,widget),"Restoring the host restores the docked widget");
            host.Width=80;
            host.Height=80;
            await Task.Delay(100);
            attachment.Apply(false,false,20,20);
            Assert(!widget.IsVisible,"A small host cannot leave the widget on the desktop");
            host.Width=600;
            host.Height=450;
            await Task.Delay(100);
            attachment.Apply(false,false,20,20);
            attachment.Detach();
            Assert(GetWindow(own,4)==IntPtr.Zero,"Standalone detaches native ownership");
            attachment.Apply(false,false,20,20);
            bool closed=false;
            widget.Closed+=(s,e)=>closed=true;
            host.Close();
            await Task.Delay(100);
            Assert(!IsWindow(own),"Closing the owner destroys the owned native window");
            Assert(closed,"Owner closure reaches the widget lifecycle handler");
        }
        finally {
            widget.Close();
            host.Close();
        }
        Console.WriteLine("PASS: "+count+" native offscreen ownership, visibility, movement and lifecycle assertions.");
    }
    [STAThread]
    static void Main() {
        var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        app.Dispatcher.BeginInvoke(new Action(async()=> {
            try {
                await Run();
                app.Shutdown(0);
            }
            catch(Exception error) {
                Console.Error.WriteLine(error);
                app.Shutdown(1);
            }
        }));
        Environment.ExitCode=app.Run();
    }
}
