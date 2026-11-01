using System;
using System.Windows;

namespace Orbitlet {
    public sealed class CatPlaygroundLayout {
        public readonly double Width,Height,CatSize,ArcScale,ArcTop,FooterTop,LabelsHeight,LabelWidth;
        readonly Rect[] protectedAreas;
        public double InnerWidth { get { return Width-1.5; } }
        public double InnerHeight { get { return Height-1.5; } }
        public Point Center { get { return new Point(InnerWidth/2,ArcTop+112*ArcScale); } }
        public double Radius { get { return 96*ArcScale; } }
        public Rect[] Protected {
            get { return protectedAreas; }
        }
        public CatPlaygroundLayout(bool small) {
            Width=small?116:136;Height=Width;CatSize=small?24:28;
            ArcScale=small?.42:.48;ArcTop=small?19:23;FooterTop=small?86:104;
            LabelsHeight=small?30:34;LabelWidth=small?46:52;
            protectedAreas=new[] {new Rect(Center.X-LabelWidth/2,Center.Y-LabelsHeight/2-2,LabelWidth,LabelsHeight+4),new Rect(Center.X-23,FooterTop-2,46,23)};
        }
        public Point Rail(double percent) {
            double angle=(135+270*percent/100)*Math.PI/180;
            return new Point(Center.X+Math.Cos(angle)*Radius-CatSize/2,Center.Y+Math.Sin(angle)*Radius-CatSize*28/32);
        }
    }
}
