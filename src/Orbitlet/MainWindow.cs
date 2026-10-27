// Orbitlet v0.5 · real quota, lifecycle and window attachment
// Only reads official account/rateLimits/read; never starts inference turns.
using System;
using System.IO;
using System.Globalization;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Forms=System.Windows.Forms;
using Drawing=System.Drawing;
namespace Orbitlet {
    public class QuotaArcs : FrameworkElement {
        public static readonly DependencyProperty FiveHourProperty=DependencyProperty.Register("FiveHour",typeof(double),typeof(QuotaArcs),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty WeeklyProperty=DependencyProperty.Register("Weekly",typeof(double),typeof(QuotaArcs),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
        public double FiveHour {
            get {
                return (double)GetValue(FiveHourProperty);
            }
            set {
                SetValue(FiveHourProperty,value);
            }
        }
        public double Weekly {
            get {
                return (double)GetValue(WeeklyProperty);
            }
            set {
                SetValue(WeeklyProperty,value);
            }
        }
        public void Update(double five,double week,bool animated) {
            double[] values= {
                five,week
            };
            DependencyProperty[] props= {
                FiveHourProperty,WeeklyProperty
            };
            for(int i=0; i<2; i++) {
                if(animated) {
                    var a=new DoubleAnimation(values[i],TimeSpan.FromMilliseconds(850)) {
                        EasingFunction=new CubicEase {
                            EasingMode=EasingMode.EaseOut
                        }
                    };
                    BeginAnimation(props[i],a);
                }
                else {
                    BeginAnimation(props[i],null);
                    SetValue(props[i],values[i]);
                }
            }
        }
        Color Tone(double value,bool outer) {
            return value<=10?Color.FromRgb(227,170,161):value<=20?Color.FromRgb(221,191,157):outer?Color.FromRgb(40,46,53):Color.FromRgb(161,170,180);
        }
        void Arc(DrawingContext dc,Point center,double radius,double percent,Pen pen) {
            if(percent<=0)return;
            double angle=(135+270*Math.Min(percent,100)/100)*Math.PI/180;
            var path=new StreamGeometry();
            using(var c=path.Open()) {
                c.BeginFigure(new Point(center.X-radius*.70710678,center.Y+radius*.70710678),false,false);
                c.ArcTo(new Point(center.X+Math.Cos(angle)*radius,center.Y+Math.Sin(angle)*radius),new Size(radius,radius),0,percent>66.6666667,SweepDirection.Clockwise,true,false);
            }
            path.Freeze();
            dc.DrawGeometry(null,pen,path);
        }
        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);
            Point center=new Point(112,112);
            double[] radii= {
                96,79
            },values= {
                FiveHour,Weekly
            };
            for(int i=0; i<2; i++) {
                double thickness=i==0?14:12;
                var track=new Pen(new SolidColorBrush(Color.FromRgb(233,237,241)),thickness) {
                    StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round
                };
                Arc(dc,center,radii[i],100,track);
                var line=new Pen(new SolidColorBrush(Tone(values[i],i==0)),thickness) {
                    StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round
                };
                Arc(dc,center,radii[i],values[i],line);
            }
        }
    }
    public class Companion : Window {
        public static bool Inspect,QuitAfterCapture,FollowCodex,Standalone;
        public static string CaptureDirectory;
        const int HotkeyId=0x514F;
        [DllImport("user32.dll")]static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint key);
        [DllImport("user32.dll")]static extern bool UnregisterHotKey(IntPtr hwnd,int id);
        [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr icon);
        Grid scene,labels;
        Border surface;
        QuotaArcs arcs;
        TextBlock fiveCaption,weekCaption,fiveLabel,weekLabel,updated;
        Forms.NotifyIcon tray;
        Drawing.Icon trayIcon;
        bool small,folded,reduceMotion,syncing,closing,manuallyHidden,dragging,manualQuit,bindToCodex=true,standaloneTopmost=true;
        double dockRight=20,dockBottom=20;
        WindowAttachment attachment;
        System.Windows.Threading.DispatcherTimer bindingTimer;
        MenuItem pinMenu;
        bool IsBound {
            get {
                return bindToCodex&&!Standalone;
            }
        }
        QuotaSnapshot lastSnapshot;
        readonly CodexQuotaClient quotaClient=new CodexQuotaClient();
        QuotaPreviewServer previewServer;
        System.Windows.Threading.DispatcherTimer refreshTimer,followTimer;
        readonly DesktopAbsenceGuard desktopAbsence=new DesktopAbsenceGuard();
        EventWaitHandle closeEvent,detachEvent,attachEvent;
        RegisteredWaitHandle closeWait,detachWait,attachWait;
        volatile string previewJson="{\"fiveHourRemainingPercent\":null,\"weeklyRemainingPercent\":null,\"updatedAtUtc\":null,\"syncStatus\":\"connecting\",\"source\":\"codex-app-server\"}";
        string settings=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"orbitlet.settings");
        public Companion() {
            Title="Orbitlet · Codex companion";
            Width=280;
            Height=278;
            WindowStyle=WindowStyle.None;
            ResizeMode=ResizeMode.NoResize;
            AllowsTransparency=true;
            Background=Brushes.Transparent;
            Topmost=false;
            ShowInTaskbar=Inspect;
            ShowActivated=false;
            FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI");
            UseLayoutRounding=true;
            ReadSettings();
            scene=new Grid {
                Width=280,Height=278
            };
            Content=scene;
            surface=new Border {
                Width=252,Height=250,CornerRadius=new CornerRadius(27),BorderThickness=new Thickness(.75),BorderBrush=Brush("#DCE1E6"),Background=new LinearGradientBrush(Color.FromArgb(250,255,255,255),Color.FromArgb(250,249,250,251),85),Effect=new DropShadowEffect {
                    Color=Colors.Black,BlurRadius=22,ShadowDepth=8,Opacity=.12
                }
            };
            scene.Children.Add(surface);
            var layout=new Canvas();
            surface.Child=layout;
            arcs=new QuotaArcs {
                Width=224,Height=224
            };
            Canvas.SetLeft(arcs,13);
            Canvas.SetTop(arcs,8);
            layout.Children.Add(arcs);
            labels=new Grid();
            labels.RowDefinitions.Add(new RowDefinition());
            labels.RowDefinitions.Add(new RowDefinition());
            layout.Children.Add(labels);
            var firstRow=new StackPanel {
                Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center
            };
            Grid.SetRow(firstRow,0);
            labels.Children.Add(firstRow);
            var secondRow=new StackPanel {
                Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center
            };
            Grid.SetRow(secondRow,1);
            labels.Children.Add(secondRow);
            fiveCaption=AddInline(firstRow,"5h",8.5,"#68717B");
            fiveLabel=AddInline(firstRow,"--",13,"#171B20");
            weekCaption=AddInline(secondRow,"本周",8.5,"#68717B");
            weekLabel=AddInline(secondRow,"--",13,"#171B20");
            fiveLabel.FontWeight=FontWeights.Normal;
            weekLabel.FontWeight=FontWeights.Normal;
            updated=new TextBlock {
                Width=224,TextAlignment=TextAlignment.Center,FontSize=10,Foreground=Brush("#747E88")
            };
            Canvas.SetLeft(updated,13);
            Canvas.SetTop(updated,211);
            layout.Children.Add(updated);
            AutomationProperties.SetName(surface,"两环额度：外环五小时剩余，内环本周剩余");
            surface.MouseLeftButtonDown+=(s,e)=> {
                if(e.ClickCount==2) {
                    ToggleFold();
                    return;
                }
                dragging=true;
                try {
                    DragMove();
                }
                catch(InvalidOperationException) {
                }
                finally {
                    dragging=false;
                }
                if(IsBound&&attachment!=null)attachment.CaptureAnchor(ref dockRight,ref dockBottom);
                KeepOnScreen();
                SaveSettings();
            };
            surface.MouseEnter+=(s,e)=>Highlight(true);
            surface.MouseLeave+=(s,e)=>Highlight(false);
            BuildMenu();
            Opacity=IsBound?0:1;
            SourceInitialized+=(s,e)=>InitNative();
            Loaded+=async(s,e)=> {
                ApplySize();
                arcs.Update(0,0,false);
                updated.Text="正在同步";
                followTimer=new System.Windows.Threading.DispatcherTimer {
                    Interval=TimeSpan.FromSeconds(2)
                };
                followTimer.Tick+=(ss,ee)=> {
                    if(FollowCodex&&desktopAbsence.Observe(CodexDesktop.IsOpen(),DateTime.UtcNow))Close();
                };
                followTimer.Start();
                try {
                    previewServer=new QuotaPreviewServer(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview.html"),()=>previewJson);
                    File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview-url.txt"),previewServer.Url);
                }
                catch(IOException) {
                }
                catch(System.Net.Sockets.SocketException) {
                }
                await RefreshQuota();
                if(closing)return;
                if(CaptureDirectory!=null)Export();
                else {
                    refreshTimer=new System.Windows.Threading.DispatcherTimer {
                        Interval=TimeSpan.FromSeconds(60)
                    };
                    refreshTimer.Tick+=async(ss,ee)=>await RefreshQuota();
                    refreshTimer.Start();
                }
            };
            KeyDown+=async(s,e)=> {
                if(e.Key==Key.Escape)HideWidget();
                if(e.Key==Key.F5)await RefreshQuota();
            };
            Closed+=(s,e)=> {
                if(FollowCodex&&!manualQuit)Environment.ExitCode=42;
            };
            Closing+=(s,e)=> {
                closing=true;
                if(bindingTimer!=null)bindingTimer.Stop();
                if(refreshTimer!=null)refreshTimer.Stop();
                if(followTimer!=null)followTimer.Stop();
                if(closeWait!=null)closeWait.Unregister(null);
                if(detachWait!=null)detachWait.Unregister(null);
                if(attachWait!=null)attachWait.Unregister(null);
                if(closeEvent!=null)closeEvent.Dispose();
                if(detachEvent!=null)detachEvent.Dispose();
                if(attachEvent!=null)attachEvent.Dispose();
                quotaClient.Dispose();
                if(previewServer!=null)previewServer.Dispose();
                SaveSettings();
                tray.Dispose();
                trayIcon.Dispose();
                UnregisterHotKey(new WindowInteropHelper(this).Handle,HotkeyId);
            };
        }
        static SolidColorBrush Brush(string color) {
            return (SolidColorBrush)new BrushConverter().ConvertFromString(color);
        }
        TextBlock AddInline(StackPanel row,string text,double size,string color) {
            var t=new TextBlock {
                Text=text,FontSize=size,Foreground=Brush(color),VerticalAlignment=VerticalAlignment.Center
            };
            row.Children.Add(t);
            return t;
        }
        void Highlight(bool active) {
            var color=active?Color.FromRgb(191,199,208):Color.FromRgb(220,225,230);
            var brush=new SolidColorBrush(color);
            surface.BorderBrush=brush;
        }
        async Task RefreshQuota() {
            if(syncing||closing)return;
            syncing=true;
            try {
                QuotaSnapshot snapshot=await quotaClient.ReadAsync();
                if(closing)return;
                lastSnapshot=snapshot;
                double? five=snapshot.FiveHourRemainingPercent,week=snapshot.WeeklyRemainingPercent;
                arcs.Update(five??0,week??0,!reduceMotion&&SystemParameters.ClientAreaAnimation);
                fiveLabel.Text=Percent(five);
                weekLabel.Text=Percent(week);
                fiveLabel.Foreground=Brush("#171B20");
                weekLabel.Foreground=Brush("#171B20");
                updated.Text=snapshot.UpdatedAtUtc.ToLocalTime().ToString("HH:mm");
                updated.ToolTip="官方 Codex · 更新于 "+snapshot.UpdatedAtUtc.ToLocalTime().ToString("HH:mm:ss")+"\n5h / 本周（7 天窗口）剩余额度";
                previewJson=snapshot.ToJson("ok",null);
                tray.Text="Orbitlet · 5h "+Percent(five)+" · 本周 "+Percent(week);
                AutomationProperties.SetName(arcs,"五小时剩余 "+Percent(five)+"，本周剩余 "+Percent(week)+"，官方 Codex 实时额度");
            }
            catch(Exception ex) {
                if(closing)return;
                string error=ex is QuotaReadException?ex.Message:"连接暂不可用，请稍后重试";
                updated.Text=lastSnapshot==null?"未连接":lastSnapshot.UpdatedAtUtc.ToLocalTime().ToString("HH:mm")+" · 离线";
                updated.ToolTip=error+(lastSnapshot==null?"":"\n最后成功更新于 "+lastSnapshot.UpdatedAtUtc.ToLocalTime().ToString("HH:mm:ss"));
                previewJson=lastSnapshot==null?Json.Encode(new {
                    fiveHourRemainingPercent=(double?)null,weeklyRemainingPercent=(double?)null,updatedAtUtc=(string)null,syncStatus="error",error=error,source="codex-app-server"
                }):lastSnapshot.ToJson("error",error);
            }
            finally {
                syncing=false;
            }
        }
        static string Percent(double? value) {
            return value.HasValue?value.Value.ToString("0",CultureInfo.InvariantCulture)+"%":"--";
        }
        void OpenPreview() {
            if(previewServer!=null)Process.Start(new ProcessStartInfo(previewServer.Url) {
                UseShellExecute=true
            });
        }
        MenuItem Item(string label,Action action) {
            var m=new MenuItem {
                Header=label
            };
            m.Click+=(s,e)=>action();
            return m;
        }
        void BuildMenu() {
            var menu=new ContextMenu {
                Background=Brush("#FDFFFFFF"),Foreground=Brush("#252B32"),BorderBrush=Brush("#DCE2E8"),FontSize=12
            };
            pinMenu=new MenuItem {
                Header="始终置顶（独立模式）",IsCheckable=true,IsChecked=standaloneTopmost,IsEnabled=!IsBound
            };
            pinMenu.Click+=(s,e)=> {
                standaloneTopmost=pinMenu.IsChecked;
                Topmost=standaloneTopmost;
                SaveSettings();
            };
            menu.Items.Add(pinMenu);
            var locked=new MenuItem {
                Header="锁定在 Codex 窗口",IsCheckable=true,IsChecked=IsBound
            };
            locked.Click+=(s,e)=> {
                bindToCodex=locked.IsChecked;
                Standalone=false;
                ApplyAttachment();
                SaveSettings();
            };
            menu.Items.Add(locked);
            var follow=new MenuItem {
                Header="随 Codex 启动 / 关闭",IsCheckable=true,IsChecked=FollowRegistration.Enabled
            };
            follow.Click+=(s,e)=> {
                try {
                    if(follow.IsChecked) {
                        FollowRegistration.Enable();
                        FollowCodex=true;
                    }
                    else {
                        FollowRegistration.Disable();
                        FollowCodex=false;
                    }
                }
                catch(Exception) {
                    follow.IsChecked=FollowRegistration.Enabled;
                    MessageBox.Show("未能更新跟随启动项，请检查当前用户启动目录的访问权限。","Orbitlet");
                }
            };
            menu.Items.Add(follow);
            menu.Items.Add(Item("小号 / 标准",()=> {
                small=!small;
                ApplySize();
                SaveSettings();
            }));
            menu.Items.Add(Item("折叠 / 展开",ToggleFold));
            var reduced=new MenuItem {
                Header="减少动态效果",IsCheckable=true,IsChecked=reduceMotion
            };
            reduced.Click+=(s,e)=> {
                reduceMotion=reduced.IsChecked;
                SaveSettings();
            };
            menu.Items.Add(reduced);
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("立即刷新 · F5",async()=>await RefreshQuota()));
            menu.Items.Add(Item("打开实时预览",OpenPreview));
            menu.Items.Add(Item("隐藏 · Ctrl+Alt+O 恢复",HideWidget));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("退出",()=> {
                manualQuit=true;
                Close();
            }));
            surface.ContextMenu=menu;
            tray=new Forms.NotifyIcon {
                Text="Orbitlet · 正在同步额度"
            };
            using(var bmp=new Drawing.Bitmap(64,64)) {
                using(var g=Drawing.Graphics.FromImage(bmp)) {
                    g.SmoothingMode=Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Drawing.Color.Transparent);
                    using(var a=new Drawing.Pen(Drawing.Color.FromArgb(185,219,237),4)) {
                        a.StartCap=a.EndCap=Drawing.Drawing2D.LineCap.Round;
                        g.DrawArc(a,5,5,54,54,135,270);
                    }
                    using(var b=new Drawing.Pen(Drawing.Color.FromArgb(203,213,224),3)) {
                        b.StartCap=b.EndCap=Drawing.Drawing2D.LineCap.Round;
                        g.DrawArc(b,15,15,34,34,135,270);
                    }
                }
                IntPtr h=bmp.GetHicon();
                trayIcon=(Drawing.Icon)Drawing.Icon.FromHandle(h).Clone();
                DestroyIcon(h);
            }
            tray.Icon=trayIcon;
            tray.Visible=true;
            tray.DoubleClick+=(s,e)=>Dispatcher.Invoke(new Action(ShowWidget));
            var trayMenu=new Forms.ContextMenuStrip();
            trayMenu.Items.Add("显示 / 隐藏",null,(s,e)=>Dispatcher.Invoke(new Action(ToggleVisible)));
            trayMenu.Items.Add("立即刷新",null,(s,e)=>Dispatcher.Invoke(new Action(async()=>await RefreshQuota())));
            trayMenu.Items.Add("打开实时预览",null,(s,e)=>Dispatcher.Invoke(new Action(OpenPreview)));
            trayMenu.Items.Add("退出",null,(s,e)=>Dispatcher.Invoke(new Action(()=> {
                manualQuit=true;
                Close();
            })));
            tray.ContextMenuStrip=trayMenu;
        }
        void ToggleFold() {
            folded=!folded;
            ApplySize();
        }
        void ApplySize() {
            double width=small?88:100,height=folded?(small?52:56):(small?90:102),arcScale=small?.36:.42;
            Width=width+16;
            Height=height+16;
            scene.LayoutTransform=Transform.Identity;
            scene.Width=Width;
            scene.Height=Height;
            surface.Width=width;
            surface.Height=height;
            surface.CornerRadius=new CornerRadius(small?13:15);
            surface.Effect=new DropShadowEffect {
                Color=Colors.Black,BlurRadius=14,ShadowDepth=4,Opacity=.12
            };
            arcs.LayoutTransform=new ScaleTransform(arcScale,arcScale);
            Canvas.SetLeft(arcs,(width-1.5-224*arcScale)/2);
            Canvas.SetTop(arcs,1);
            labels.Width=width-20;
            labels.Height=small?30:34;
            Canvas.SetLeft(labels,(width-1.5-labels.Width)/2);
            Canvas.SetTop(labels,folded?(height-labels.Height)/2:1+112*arcScale-labels.Height/2);
            fiveCaption.FontSize=weekCaption.FontSize=small?7.5:8.5;
            fiveLabel.FontSize=weekLabel.FontSize=small?11.5:13;
            fiveCaption.Margin=weekCaption.Margin=new Thickness(0,0,small?3:4,0);
            updated.Width=width-9.5;
            updated.FontSize=8;
            Canvas.SetLeft(updated,4);
            Canvas.SetTop(updated,small?74:84);
            arcs.Visibility=folded?Visibility.Collapsed:Visibility.Visible;
            updated.Visibility=folded?Visibility.Collapsed:Visibility.Visible;
            KeepOnScreen();
        }
        void ShowWidget() {
            manuallyHidden=false;
            if(IsBound)ApplyAttachment();
            else {
                Opacity=1;
                Show();
                Activate();
                KeepOnScreen();
            }
        }
        void HideWidget() {
            manuallyHidden=true;
            Hide();
        }
        void ToggleVisible() {
            if(IsVisible&&!manuallyHidden)HideWidget();
            else ShowWidget();
        }
        void ApplyAttachment() {
            if(attachment==null||closing)return;
            if(pinMenu!=null)pinMenu.IsEnabled=!IsBound;
            if(IsBound)attachment.Apply(manuallyHidden,dragging,dockRight,dockBottom);
            else {
                attachment.Detach();
                Topmost=standaloneTopmost;
                if(!manuallyHidden&&!IsVisible)Show();
            }
        }
        void KeepOnScreen() {
            if(IsBound&&attachment!=null) {
                ApplyAttachment();
                return;
            }
            var area=SystemParameters.WorkArea;
            Left=Math.Max(area.Left,Math.Min(Left,area.Right-Width));
            Top=Math.Max(area.Top,Math.Min(Top,area.Bottom-Height));
        }
        void InitNative() {
            var handle=new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle).AddHook((IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled)=> {
                if(msg==0x312&&w.ToInt32()==HotkeyId) {
                    ToggleVisible();
                    handled=true;
                }
                return IntPtr.Zero;
            });
            if(!RegisterHotKey(handle,HotkeyId,0x4003,0x4F))tray.Text="Orbitlet · 热键被占用 · 双击托盘恢复";
            attachment=new WindowAttachment(this);
            bindingTimer=new System.Windows.Threading.DispatcherTimer {
                Interval=TimeSpan.FromMilliseconds(150)
            };
            bindingTimer.Tick+=(s,e)=>ApplyAttachment();
            bindingTimer.Start();
            closeEvent=new EventWaitHandle(false,EventResetMode.AutoReset,CodexFollower.CloseEvent);
            detachEvent=new EventWaitHandle(false,EventResetMode.AutoReset,CodexFollower.DetachEvent);
            attachEvent=new EventWaitHandle(false,EventResetMode.AutoReset,CodexFollower.AttachEvent);
            closeWait=ThreadPool.RegisterWaitForSingleObject(closeEvent,(s,t)=>Dispatcher.BeginInvoke(new Action(()=> {
                if(!closing) {
                    manualQuit=true;
                    Close();
                }
            })),null,Timeout.Infinite,false);
            detachWait=ThreadPool.RegisterWaitForSingleObject(detachEvent,(s,t)=>Dispatcher.BeginInvoke(new Action(()=>FollowCodex=false)),null,Timeout.Infinite,false);
            attachWait=ThreadPool.RegisterWaitForSingleObject(attachEvent,(s,t)=>Dispatcher.BeginInvoke(new Action(()=> {
                FollowCodex=true;
                Standalone=false;
                bindToCodex=true;
                ApplyAttachment();
            })),null,Timeout.Infinite,false);
        }
        void ReadSettings() {
            Left=SystemParameters.WorkArea.Right-320;
            Top=SystemParameters.WorkArea.Bottom-332;
            try {
                foreach(var line in File.ReadAllLines(settings)) {
                    var p=line.Split('=');
                    if(p.Length!=2)continue;
                    double value;
                    if(p[0]=="left"&&double.TryParse(p[1],NumberStyles.Float,CultureInfo.InvariantCulture,out value))Left=value;
                    if(p[0]=="top"&&double.TryParse(p[1],NumberStyles.Float,CultureInfo.InvariantCulture,out value))Top=value;
                    if(p[0]=="dockRight"&&double.TryParse(p[1],NumberStyles.Float,CultureInfo.InvariantCulture,out value))dockRight=value;
                    if(p[0]=="dockBottom"&&double.TryParse(p[1],NumberStyles.Float,CultureInfo.InvariantCulture,out value))dockBottom=value;
                    if(p[0]=="bindCodex")bindToCodex=p[1]=="1";
                    if(p[0]=="small")small=p[1]=="1";
                    if(p[0]=="topmost")standaloneTopmost=p[1]=="1";
                    if(p[0]=="reduceMotion")reduceMotion=p[1]=="1";
                }
            }
            catch(IOException) {
            }
            catch(UnauthorizedAccessException) {
            }
        }
        void SaveSettings() {
            try {
                File.WriteAllLines(settings,new[] {
                    "left="+Left.ToString(CultureInfo.InvariantCulture),"top="+Top.ToString(CultureInfo.InvariantCulture),"dockRight="+dockRight.ToString(CultureInfo.InvariantCulture),"dockBottom="+dockBottom.ToString(CultureInfo.InvariantCulture),"bindCodex="+(bindToCodex?"1":"0"),"small="+(small?"1":"0"),"topmost="+(standaloneTopmost?"1":"0"),"reduceMotion="+(reduceMotion?"1":"0")
                });
            }
            catch(IOException) {
            }
            catch(UnauthorizedAccessException) {
            }
        }
        void Export() {
            Directory.CreateDirectory(CaptureDirectory);
            bool originalMotion=reduceMotion,originalSmall=small,originalFold=folded;
            reduceMotion=true;
            if(lastSnapshot!=null)arcs.Update(lastSnapshot.FiveHourRemainingPercent??0,lastSnapshot.WeeklyRemainingPercent??0,false);
            small=false;
            folded=false;
            ApplySize();
            Capture("native-standard.png");
            small=true;
            ApplySize();
            Capture("native-small.png");
            reduceMotion=originalMotion;
            small=originalSmall;
            folded=originalFold;
            ApplySize();
            if(QuitAfterCapture)Close();
        }
        void Capture(string name) {
            scene.UpdateLayout();
            var visual=new DrawingVisual();
            using(var dc=visual.RenderOpen()) {
                dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(240,242,245),Color.FromRgb(230,234,238),45),null,new Rect(0,0,Width,Height));
                dc.DrawRectangle(new VisualBrush(scene),null,new Rect(0,0,Width,Height));
            }
            var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(Width*3),(int)Math.Ceiling(Height*3),288,288,PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using(var file=File.Create(System.IO.Path.Combine(CaptureDirectory,name)))encoder.Save(file);
        }
        [STAThread]public static void Main(string[] args) {
            foreach(var arg in args) {
                if(arg=="--standalone") {
                    Standalone=true;
                    CodexFollower.Signal(CodexFollower.DetachEvent);
                }
                if(arg=="--watch-codex") {
                    CodexFollower.Run();
                    return;
                }
                if(arg=="--enable-follow"||arg=="--disable-follow") {
                    try {
                        if(arg=="--enable-follow")FollowRegistration.Enable();
                        else FollowRegistration.Disable();
                    }
                    catch(Exception ex) {
                        File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"follow-error.log"),ex.Message);
                        Environment.ExitCode=1;
                    }
                    return;
                }
                if(arg=="--request-exit") {
                    CodexFollower.Signal(CodexFollower.CloseEvent);
                    return;
                }
                if(arg.StartsWith("--follow-status=")) {
                    File.WriteAllText(arg.Substring(16),Json.Encode(FollowRegistration.Status()));
                    return;
                }
            }
            foreach(var arg in args)if(arg.StartsWith("--sync-once=")) {
                using(var client=new CodexQuotaClient()) {
                    try {
                        var snapshot=client.ReadAsync().GetAwaiter().GetResult();
                        File.WriteAllText(arg.Substring(12),snapshot.ToJson("ok",null));
                    }
                    catch(Exception ex) {
                        File.WriteAllText(arg.Substring(12),Json.Encode(new {
                            syncStatus="error",error=ex is QuotaReadException?ex.Message:"连接暂不可用"
                        }));
                        Environment.ExitCode=1;
                    }
                }
                return;
            }
            bool created;
            using(var mutex=new System.Threading.Mutex(true,CodexFollower.WidgetMutex,out created)) {
                if(!created)return;
                try {
                    foreach(var arg in args) {
                        if(arg=="--inspect")Inspect=true;
                        if(arg=="--follow-codex")FollowCodex=true;
                        if(arg=="--quit-after-capture")QuitAfterCapture=true;
                        if(arg.StartsWith("--capture="))CaptureDirectory=arg.Substring(10);
                    }
                    var app=new Application {
                        ShutdownMode=ShutdownMode.OnMainWindowClose
                    };
                    app.Run(new Companion());
                }
                catch(Exception ex) {
                    File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"startup-error.log"),ex.ToString());
                    MessageBox.Show(ex.Message,"Orbitlet");
                }
            }
        }
    }
}
