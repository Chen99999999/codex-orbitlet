using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Orbitlet {
    public class QuotaArcs : FrameworkElement {
        public static readonly DependencyProperty FiveHourProperty=DependencyProperty.Register("FiveHour",typeof(double),typeof(QuotaArcs),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender,QuotaChanged));
        public static readonly DependencyProperty WeeklyProperty=DependencyProperty.Register("Weekly",typeof(double),typeof(QuotaArcs),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender,QuotaChanged));
        readonly Stopwatch clock=Stopwatch.StartNew();
        readonly DispatcherTimer flowTimer;
        bool motion=true;
        double contact=-1;
        public void SetContact(double? percent) { contact=percent??-1;InvalidateVisual(); }
        public QuotaArcs() {
            flowTimer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(33) };
            flowTimer.Tick+=(s,e)=>InvalidateVisual();
            IsVisibleChanged+=(s,e)=>UpdateFlowTimer();
            Loaded+=(s,e)=>UpdateFlowTimer();
            Unloaded+=(s,e)=>flowTimer.Stop();
        }
        public double FiveHour {
            get { return (double)GetValue(FiveHourProperty); }
            set { SetValue(FiveHourProperty,value); }
        }
        public double Weekly {
            get { return (double)GetValue(WeeklyProperty); }
            set { SetValue(WeeklyProperty,value); }
        }
        static void QuotaChanged(DependencyObject element,DependencyPropertyChangedEventArgs args) {
            ((QuotaArcs)element).UpdateFlowTimer();
        }
        void UpdateFlowTimer() {
            if(flowTimer==null)return;
            bool active=motion&&IsLoaded&&IsVisible&&(FiveHour>0||Weekly>0);
            if(active&&!flowTimer.IsEnabled)flowTimer.Start();
            else if(!active&&flowTimer.IsEnabled)flowTimer.Stop();
        }
        public void SetMotion(bool enabled) {
            motion=enabled;
            UpdateFlowTimer();
            InvalidateVisual();
        }
        public void Update(double five,double week,bool animated) {
            double[] values={ five,week };
            DependencyProperty[] props={ FiveHourProperty,WeeklyProperty };
            for(int i=0;i<2;i++) {
                double target=double.IsNaN(values[i])||double.IsInfinity(values[i])?0:Math.Max(0,Math.Min(values[i],100));
                if(animated) {
                    var animation=new DoubleAnimation(target,TimeSpan.FromMilliseconds(850)) {
                        EasingFunction=new CubicEase { EasingMode=EasingMode.EaseOut }
                    };
                    BeginAnimation(props[i],animation);
                }
                else {
                    BeginAnimation(props[i],null);
                    SetValue(props[i],target);
                }
            }
            UpdateFlowTimer();
        }
        static Color Tone(double value,bool outer) {
            return value<=10?Color.FromRgb(227,170,161):value<=20?Color.FromRgb(221,191,157):outer?Color.FromRgb(40,46,53):Color.FromRgb(161,170,180);
        }
        static Point At(double radius,double percent) {
            double angle=(135+270*percent/100)*Math.PI/180;
            return new Point(112+Math.Cos(angle)*radius,112+Math.Sin(angle)*radius);
        }
        static Pen Stroke(Brush brush,double width) {
            return new Pen(brush,width) { StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round };
        }
        static void Arc(DrawingContext dc,double radius,double start,double end,Pen pen) {
            if(end-start<.001)return;
            var path=new StreamGeometry();
            using(var geometry=path.Open()) {
                geometry.BeginFigure(At(radius,start),false,false);
                geometry.ArcTo(At(radius,end),new Size(radius,radius),0,end-start>66.6666667,SweepDirection.Clockwise,true,false);
            }
            path.Freeze();
            dc.DrawGeometry(null,pen,path);
        }
        void Energy(DrawingContext dc,double radius,double value,int index) {
            if(!motion||value<=.001)return;
            double phase=(clock.Elapsed.TotalSeconds/(index==0?2:2.4)+(index==0?0:.38))%1;
            if(motion) {
                double head=value*Math.Min(1,phase/.92),tail=Math.Max(0,head-Math.Min(16,value*.27));
                double fade=Math.Min(1,phase/.1)*Math.Min(1,(1-phase)/.18);
                var gradient=new LinearGradientBrush {
                    MappingMode=BrushMappingMode.Absolute,StartPoint=At(radius,tail),EndPoint=At(radius,head)
                };
                gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0,237,245,251),0));
                gradient.GradientStops.Add(new GradientStop(Color.FromArgb(100,229,240,248),.4));
                gradient.GradientStops.Add(new GradientStop(Color.FromArgb(240,248,252,255),.78));
                gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0,248,252,255),1));
                gradient.Freeze();
                dc.PushOpacity(fade*.08);
                Arc(dc,radius,tail,head,Stroke(gradient,11));
                dc.Pop();
                dc.PushOpacity(fade*.15);
                Arc(dc,radius,tail,head,Stroke(gradient,5.5));
                dc.Pop();
            }

        }
        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);
            double[] radii={96,79},values={Math.Max(0,Math.Min(100,FiveHour)),Math.Max(0,Math.Min(100,Weekly))};
            for(int i=0;i<2;i++) {
                double thickness=i==0?14:12;
                Arc(dc,radii[i],0,100,Stroke(new SolidColorBrush(Color.FromRgb(233,237,241)),thickness));
                Arc(dc,radii[i],0,values[i],Stroke(new SolidColorBrush(Tone(values[i],i==0)),thickness));
                Energy(dc,radii[i],values[i],i);
                if(i==0&&contact>=0)Arc(dc,radii[i],Math.Max(0,contact-2.5),Math.Min(100,contact+2.5),Stroke(new SolidColorBrush(Color.FromArgb(180,183,207,222)),thickness+1));
            }
        }
    }
}
