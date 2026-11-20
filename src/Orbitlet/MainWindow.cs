// Orbitlet desktop widget, quota display and window attachment.
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
        CatCompanion cat;
        TextBlock fiveCaption,weekCaption,fiveLabel,weekLabel,resetTimes;
        Forms.NotifyIcon tray;
        Drawing.Icon trayIcon;
        bool small,folded,reduceMotion,syncing,closing,manuallyHidden,dragging,manualQuit,bindToCodex=true,standaloneTopmost=true;
        double dockRight=20,dockBottom=20;
        WindowAttachment attachment;
        System.Windows.Threading.DispatcherTimer bindingTimer;
        MenuItem pinMenu,lockMenu;
        DesktopTarget desktopTarget=DesktopTargets.All[0];
        QuotaSource quotaSource=QuotaSources.All[0];
        int quotaGeneration;
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
        volatile string previewJson;
        readonly string settings=DesktopTargets.SettingsPath;
        public Companion() {
            Title="Orbitlet";
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
            cat=new CatCompanion();
            Panel.SetZIndex(cat,1);
            layout.Children.Add(cat);
            arcs=new QuotaArcs {
                Width=224,Height=224
            };
            Canvas.SetLeft(arcs,13);
            Canvas.SetTop(arcs,8);
            layout.Children.Add(arcs);
            cat.ContactChanged+=value=>arcs.SetContact(value);
            labels=new Grid();
            Panel.SetZIndex(labels,2);
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
            resetTimes=new TextBlock {
                Width=224,TextAlignment=TextAlignment.Center,FontSize=8,LineHeight=10,LineStackingStrategy=LineStackingStrategy.BlockLineHeight,Foreground=Brush("#747E88")
            };
            Panel.SetZIndex(resetTimes,2);
            Canvas.SetLeft(resetTimes,13);
            Canvas.SetTop(resetTimes,211);
            layout.Children.Add(resetTimes);
            UpdateProviderDisplay(quotaSource.Empty(),"connecting",null,false);
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
                followTimer=new System.Windows.Threading.DispatcherTimer {
                    Interval=TimeSpan.FromSeconds(2)
                };
                followTimer.Tick+=(ss,ee)=> {
                    if(FollowCodex&&desktopAbsence.Observe(desktopTarget.IsOpen(),DateTime.UtcNow))Close();
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
            int requested=quotaGeneration;
            var source=quotaSource;
            try {
                QuotaSnapshot snapshot=await QuotaSources.ReadAsync(source,quotaClient);
                if(closing||requested!=quotaGeneration)return;
                lastSnapshot=snapshot;
                UpdateProviderDisplay(snapshot,source.Id=="none"?"disabled":"ok",null,true);
            }
            catch(Exception ex) {
                if(closing||requested!=quotaGeneration)return;
                string error=ex is QuotaReadException?ex.Message:"连接暂不可用，请稍后重试";
                if(source.Id=="claude")lastSnapshot=null;
                UpdateProviderDisplay(lastSnapshot??source.Empty(),"error",error,false);
            }
            finally {
                if(requested==quotaGeneration)syncing=false;
            }
        }
        async Task SelectQuota(QuotaSource selected) {
            if(selected==quotaSource)return;
            quotaSource=selected;
            quotaGeneration++;
            syncing=false;
            lastSnapshot=null;
            UpdateProviderDisplay(selected.Empty(),"connecting",null,false);
            SaveSettings();
            await RefreshQuota();
        }
        void UpdateProviderDisplay(QuotaSnapshot snapshot,string status,string error,bool animate) {
            var first=snapshot.FirstPercent;
            var second=snapshot.SecondPercent;
            cat.SetQuota(first??50);
            arcs.Update(first??0,second??0,animate&&!reduceMotion&&SystemParameters.ClientAreaAnimation);
            bool disabled=snapshot.Source=="none";
            fiveCaption.Text=disabled?"":snapshot.OuterLabel;
            weekCaption.Text=disabled?"":snapshot.InnerLabel;
            fiveLabel.Text=disabled?"":snapshot.OuterAmount??Percent(first);
            weekLabel.Text=disabled?"":snapshot.InnerAmount??Percent(second);
            SetValueFontSizes(snapshot);
            ShowResetTimes(snapshot,error);
            previewJson=snapshot.ToJson(disabled?"disabled":status,error);
            string description=snapshot.ProviderName+" · "+fiveCaption.Text+" "+fiveLabel.Text+" · "+weekCaption.Text+" "+weekLabel.Text;
            AutomationProperties.SetName(surface,description);
            AutomationProperties.SetName(arcs,description);
            if(tray!=null)tray.Text="Orbitlet · "+snapshot.ProviderName;
        }
        void SetValueFontSizes(QuotaSnapshot snapshot) {
            fiveLabel.FontSize=snapshot!=null&&snapshot.OuterAmount!=null?(small?8:9):(small?11.5:13);
            weekLabel.FontSize=snapshot!=null&&snapshot.InnerAmount!=null?(small?8:9):(small?11.5:13);
        }
        static string ResetTime(long? seconds,string format,string unknown) {
            if(!seconds.HasValue||seconds.Value<0||seconds.Value>253402300799)return unknown;
            try {
                return new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(seconds.Value).ToLocalTime().ToString(format,CultureInfo.InvariantCulture);
            }
            catch(ArgumentOutOfRangeException) {
                return unknown;
            }
        }
        void ShowResetTimes(QuotaSnapshot snapshot,string error) {
            long? first=snapshot.FirstEnd;
            long? second=snapshot.SecondEnd;
            if(snapshot.ShowsTimes) {
                resetTimes.Text=snapshot.OuterLabel+" "+ResetTime(first,"HH:mm","--:--")+"\n"+snapshot.InnerLabel+" "+ResetTime(second,"MM/dd","--/--");
            } else {
                string end=first.HasValue?ResetTime(first,"MM/dd","--"):second.HasValue?ResetTime(second,"MM/dd","--"):null;
                resetTimes.Text=snapshot.ProviderName+(end!=null?"\n"+(snapshot.Source.StartsWith("qoder",StringComparison.Ordinal)?"到期 ":"重置 ")+end:error!=null?"\n未连接":"");
            }
            string description=snapshot.ProviderName;
            if(snapshot.ShowsTimes||first.HasValue||second.HasValue) {
                description+="\n"+snapshot.OuterLabel+" 结束于 "+ResetTime(first,"yyyy/MM/dd HH:mm","未知")+"\n"+snapshot.InnerLabel+" 结束于 "+ResetTime(second,"yyyy/MM/dd HH:mm","未知")+"（本地时间）";
            }
            if(snapshot.Detail!=null)description+="\n"+snapshot.Detail;
            if(error!=null)description+="\n"+error;
            resetTimes.ToolTip=description;
            AutomationProperties.SetName(resetTimes,description);
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
            lockMenu=new MenuItem {
                Header="锁定在目标窗口",IsCheckable=true,IsChecked=IsBound
            };
            lockMenu.Click+=(s,e)=> {
                bindToCodex=lockMenu.IsChecked;
                Standalone=false;
                ApplyAttachment();
                SaveSettings();
            };
            menu.Items.Add(lockMenu);
            var targetOptions=new MenuItem { Header="跟随窗口" };
            foreach(var target in DesktopTargets.All)targetOptions.Items.Add(Item(target.Name,()=>SelectDesktop(target)));
            targetOptions.SubmenuOpened+=(s,e)=> {
                for(int i=0;i<targetOptions.Items.Count;i++)((MenuItem)targetOptions.Items[i]).IsChecked=desktopTarget==DesktopTargets.All[i];
            };
            menu.Items.Add(targetOptions);
            var sourceOptions=new MenuItem { Header="额度来源" };
            foreach(var source in QuotaSources.All)sourceOptions.Items.Add(Item(source.Name,async()=>await SelectQuota(source)));
            sourceOptions.SubmenuOpened+=(s,e)=> {
                for(int i=0;i<sourceOptions.Items.Count;i++)((MenuItem)sourceOptions.Items[i]).IsChecked=quotaSource==QuotaSources.All[i];
            };
            menu.Items.Add(sourceOptions);
            var follow=new MenuItem {
                Header="随目标应用启动 / 关闭",IsCheckable=true,IsChecked=FollowRegistration.Enabled
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
                arcs.SetMotion(!reduceMotion&&SystemParameters.ClientAreaAnimation);
                cat.SetMotion(!reduceMotion&&SystemParameters.ClientAreaAnimation);
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
                Text="Orbitlet · "+quotaSource.Name
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
            var trayTargets=new Forms.ToolStripMenuItem("跟随窗口");
            foreach(var target in DesktopTargets.All)trayTargets.DropDownItems.Add(target.Name,null,(s,e)=>Dispatcher.Invoke(new Action(()=>SelectDesktop(target))));
            trayTargets.DropDownOpening+=(s,e)=> {
                for(int i=0;i<trayTargets.DropDownItems.Count;i++)((Forms.ToolStripMenuItem)trayTargets.DropDownItems[i]).Checked=desktopTarget==DesktopTargets.All[i];
            };
            trayMenu.Items.Add(trayTargets);
            var traySources=new Forms.ToolStripMenuItem("额度来源");
            foreach(var source in QuotaSources.All)traySources.DropDownItems.Add(source.Name,null,(s,e)=>Dispatcher.Invoke(new Action(async()=>await SelectQuota(source))));
            traySources.DropDownOpening+=(s,e)=> {
                for(int i=0;i<traySources.DropDownItems.Count;i++)((Forms.ToolStripMenuItem)traySources.DropDownItems[i]).Checked=quotaSource==QuotaSources.All[i];
            };
            trayMenu.Items.Add(traySources);
            trayMenu.Items.Add("立即刷新",null,(s,e)=>Dispatcher.Invoke(new Action(async()=>await RefreshQuota())));
            trayMenu.Items.Add("打开实时预览",null,(s,e)=>Dispatcher.Invoke(new Action(OpenPreview)));
            trayMenu.Items.Add("退出",null,(s,e)=>Dispatcher.Invoke(new Action(()=> {
                manualQuit=true;
                Close();
            })));
            tray.ContextMenuStrip=trayMenu;
        }
        void SelectDesktop(DesktopTarget target) {
            desktopTarget=target;
            bindToCodex=true;
            Standalone=false;
            desktopAbsence.Observe(true,DateTime.UtcNow);
            if(attachment!=null)attachment.SelectTarget(target);
            ApplyAttachment();
            SaveSettings();
        }
        void ToggleFold() {
            folded=!folded;
            ApplySize();
        }
        void ApplySize() {
            var playground=new CatPlaygroundLayout(small);
            double width=playground.Width,height=folded?(small?52:56):playground.Height,arcScale=playground.ArcScale;
            Width=width+16;
            Height=height+16;
            scene.LayoutTransform=Transform.Identity;
            scene.Width=Width;
            scene.Height=Height;
            surface.Width=width;
            surface.Height=height;
            surface.CornerRadius=new CornerRadius(small?16:18);
            surface.Effect=new DropShadowEffect {
                Color=Colors.Black,BlurRadius=14,ShadowDepth=4,Opacity=.12
            };
            arcs.SetMotion(!reduceMotion&&SystemParameters.ClientAreaAnimation);
            arcs.LayoutTransform=new ScaleTransform(arcScale,arcScale);
            Canvas.SetLeft(arcs,(width-1.5-224*arcScale)/2);
            Canvas.SetTop(arcs,playground.ArcTop);
            labels.Width=width-20;
            labels.Height=small?30:34;
            Canvas.SetLeft(labels,(width-1.5-labels.Width)/2);
            Canvas.SetTop(labels,folded?(height-labels.Height)/2:playground.Center.Y-labels.Height/2);
            fiveCaption.FontSize=weekCaption.FontSize=small?7.5:8.5;
            SetValueFontSizes(lastSnapshot);
            fiveCaption.Margin=weekCaption.Margin=new Thickness(0,0,small?3:4,0);
            resetTimes.Width=width-9.5;
            resetTimes.FontSize=8;
            Canvas.SetLeft(resetTimes,4);
            Canvas.SetTop(resetTimes,playground.FooterTop);
            cat.Configure(width,height,small,folded,!reduceMotion&&SystemParameters.ClientAreaAnimation);
            arcs.Visibility=folded?Visibility.Collapsed:Visibility.Visible;
            resetTimes.Visibility=folded?Visibility.Collapsed:Visibility.Visible;
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
            if(lockMenu!=null)lockMenu.IsChecked=IsBound;
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
            attachment.SelectTarget(desktopTarget);
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
                    if(p[0]=="desktopTarget")desktopTarget=DesktopTargets.Find(p[1])??DesktopTargets.All[0];
                    if(p[0]=="quotaSource")quotaSource=QuotaSources.Find(p[1])??QuotaSources.All[0];
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
            string temporary=settings+".new.tmp";
            try {
                File.WriteAllLines(temporary,new[] {
                    "left="+Left.ToString(CultureInfo.InvariantCulture),"top="+Top.ToString(CultureInfo.InvariantCulture),"dockRight="+dockRight.ToString(CultureInfo.InvariantCulture),"dockBottom="+dockBottom.ToString(CultureInfo.InvariantCulture),"bindCodex="+(bindToCodex?"1":"0"),"small="+(small?"1":"0"),"topmost="+(standaloneTopmost?"1":"0"),"reduceMotion="+(reduceMotion?"1":"0"),"desktopTarget="+desktopTarget.Id,"quotaSource="+quotaSource.Id
                });
                if(File.Exists(settings))File.Replace(temporary,settings,null);
                else File.Move(temporary,settings);
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
            if(lastSnapshot!=null)arcs.Update(lastSnapshot.FirstPercent??0,lastSnapshot.SecondPercent??0,false);
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
            if(Array.IndexOf(args,"--claude-statusline")>=0) {
                QuotaSources.IngestClaudeStatusLine();
                return;
            }
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
            string sourceId=Array.Find(args,arg=>arg.StartsWith("--provider=",StringComparison.Ordinal));
            var source=sourceId==null?QuotaSources.All[0]:QuotaSources.Find(sourceId.Substring(11));
            foreach(var arg in args)if(arg.StartsWith("--sync-once=")) {
                using(var client=new CodexQuotaClient()) {
                    try {
                        if(source==null)throw new QuotaReadException("未知额度来源");
                        var snapshot=QuotaSources.ReadAsync(source,client).GetAwaiter().GetResult();
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
