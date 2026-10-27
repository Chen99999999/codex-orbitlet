using System;
using System.Windows;
using Orbitlet;
class AttachmentTests {
    static int count;
    static void Assert(bool condition,string label) {
        if(!condition)throw new Exception(label);
        count++;
    }
    static void Equal(double actual,double expected,string label) {
        Assert(Math.Abs(actual-expected)<.001,label);
    }
    public static void Main() {
        Assert(AttachmentLayout.ShouldShow(true,false,true,false,true),"Show only while Codex is active");
        Assert(!AttachmentLayout.ShouldShow(false,false,true,false,true),"No desktop window means hidden");
        Assert(!AttachmentLayout.ShouldShow(true,true,true,false,true),"Minimized Codex means hidden");
        Assert(!AttachmentLayout.ShouldShow(true,false,false,false,true),"Switching applications means hidden");
        Assert(!AttachmentLayout.ShouldShow(true,false,true,true,true),"Manual hide is respected");
        Assert(!AttachmentLayout.ShouldShow(true,false,true,false,false),"Too-small client area cannot leak onto desktop");
        var host=new Rect(100,100,800,600);
        var size=new Size(116,118);
        var point=AttachmentLayout.Position(host,size,20,20);
        Equal(point.X,764,"Anchored to Codex right edge");
        Equal(point.Y,562,"Anchored to Codex bottom edge");
        var moved=AttachmentLayout.Position(new Rect(300,250,800,600),size,20,20);
        Equal(moved.X-point.X,200,"Follows horizontal movement");
        Equal(moved.Y-point.Y,150,"Follows vertical movement");
        var resized=AttachmentLayout.Position(new Rect(100,100,1000,700),size,20,20);
        Equal(resized.X-point.X,200,"Follows window width");
        Equal(resized.Y-point.Y,100,"Follows window height");
        var clamped=AttachmentLayout.Position(host,size,10000,10000);
        Equal(clamped.X,108,"Drag is clamped inside left edge");
        Equal(clamped.Y,108,"Drag is clamped inside top edge");
        var negative=AttachmentLayout.Position(new Rect(-1200,-100,1000,700),size,20,20);
        Assert(new Rect(-1200,-100,1000,700).Contains(new Rect(negative,size)),"Negative monitor coordinates remain inside host");
        Console.WriteLine("PASS: "+count+" attachment visibility, docking, resize, drag and multi-monitor assertions.");
    }
}
