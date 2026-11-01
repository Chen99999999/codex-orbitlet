using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Orbitlet {
    public sealed class CatCompanion : FrameworkElement {
        readonly CatExplorer explorer=new CatExplorer(Environment.TickCount);
        readonly Stopwatch clock=Stopwatch.StartNew();
        readonly DispatcherTimer timer;
        readonly BitmapSource[] frames=LoadFrames();
        bool motion=true;
        double last;
        public event Action<double?> ContactChanged;
        public void SetQuota(double value) { explorer.SetQuota(value); }
        static BitmapSource[] LoadFrames() {
            try {
                string directory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets");
                var image=new BitmapImage();
                image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;
                image.UriSource=new Uri(Path.Combine(directory,"cat-real-atlas.png"),UriKind.Absolute);
                image.EndInit();image.Freeze();
                var metadata=Json.Map(new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path.Combine(directory,"cat-real-atlas.json"))));
                var rectangles=Json.Get(metadata,"frames") as object[];
                if(rectangles==null||rectangles.Length!=12)return new BitmapSource[0];
                var result=new BitmapSource[12];
                for(int i=0;i<result.Length;i++) {
                    var values=rectangles[i] as object[];
                    if(values==null||values.Length!=4)return new BitmapSource[0];
                    var rect=new Int32Rect(Convert.ToInt32(values[0],CultureInfo.InvariantCulture),Convert.ToInt32(values[1],CultureInfo.InvariantCulture),Convert.ToInt32(values[2],CultureInfo.InvariantCulture),Convert.ToInt32(values[3],CultureInfo.InvariantCulture));
                    var frame=new CroppedBitmap(image,rect);frame.Freeze();result[i]=frame;
                }
                return result;
            }
            catch(Exception) { return new BitmapSource[0]; }
        }
        public CatCompanion() {
            IsHitTestVisible=false;
            RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.HighQuality);
            timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(33) };
            timer.Tick+=(s,e)=> {
                double now=clock.Elapsed.TotalSeconds;
                explorer.Tick(now,now-last);last=now;
                Canvas.SetLeft(this,explorer.Position.X);Canvas.SetTop(this,explorer.Position.Y);
                if(ContactChanged!=null)ContactChanged(explorer.ContactPercent);
                InvalidateVisual();
            };
            IsVisibleChanged+=(s,e)=>UpdateTimer();
            Loaded+=(s,e)=>UpdateTimer();
            Unloaded+=(s,e)=>timer.Stop();
        }
        void UpdateTimer() {
            if(motion&&IsVisible&&IsLoaded&&frames.Length==12) { last=clock.Elapsed.TotalSeconds;timer.Start(); }
            else timer.Stop();
        }
        public void Configure(double width,double height,bool small,bool folded,bool enabled) {
            var layout=new CatPlaygroundLayout(small);
            double size=layout.CatSize;
            explorer.Configure(layout,clock.Elapsed.TotalSeconds);
            Width=size;Height=size*28/32;
            Visibility=folded?Visibility.Collapsed:Visibility.Visible;
            SetMotion(enabled);
            Canvas.SetLeft(this,explorer.Position.X);Canvas.SetTop(this,explorer.Position.Y);
            InvalidateVisual();
        }
        public void SetMotion(bool enabled) { motion=enabled;UpdateTimer();InvalidateVisual(); }
        internal static int Frame(string action,double age) {
            switch(action) {
                case "walk":return (int)(age*7)%4;
                case "climb":return (int)(age*8)%4;
                case "hop":return 0;
                case "slide":return 8;
                case "balance":return 5;
                case "paw":return 6+(int)(age*3)%2;
                case "sit":return 5;
                case "groom":return 6+(int)(age*3)%2;
                case "stretch":return 8;
                case "sniff":return 9;
                case "nap":return 10+(int)(age*.6)%2;
                case "land":return 9;
                case "turn":return 4;
                default:return 4;
            }
        }
        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);
            if(frames.Length!=12)return;
            double now=clock.Elapsed.TotalSeconds,age=Math.Max(0,now-explorer.ActionStarted);
            string action=motion?explorer.Action:"sit";
            int index=Frame(action,age);
            if(action=="walk"||action=="climb")index=(int)(explorer.GaitDistance/(Width*.45)*4)%4;
            if(action=="slide"||action=="nap")index=action=="slide"?9:10;
            BitmapSource image=frames[index];
            // One photographic scale across the gait; never resize the cat to fit
            // each pose or tilt a rigid rectangular photograph with the trajectory.
            double scale=Math.Min((Width-.6)/345,(Height-.6)/327);
            scale=Math.Min(scale,(Width-.6)/image.PixelWidth);
            double width=image.PixelWidth*scale,height=image.PixelHeight*scale;
            double compression=explorer.LandingCompression;
            width*=1+compression*.04;height*=1-compression*.18;
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(18,30,40,50)),null,new Point(Width/2,Height-.6),Width*.22,.5);
            if(!explorer.FacingRight)dc.PushTransform(new ScaleTransform(-1,1,Width/2,Height/2));
            dc.DrawImage(image,new Rect((Width-width)/2,Height-height-.2,width,height));
            if(!explorer.FacingRight)dc.Pop();
        }
    }
}
