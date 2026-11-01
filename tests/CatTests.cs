using System;
using System.Collections.Generic;
using System.Windows;
using Orbitlet;
class CatTests {
    static void Check(CatExplorer cat,CatPlaygroundLayout layout) {
        var box=new Rect(cat.Position,new Size(layout.CatSize,layout.CatSize*28/32));
        if(!new Rect(4,4,layout.InnerWidth-8,layout.InnerHeight-8).Contains(box))throw new Exception("Cat left the card: "+cat.Action);
        foreach(Rect text in layout.Protected)if(box.IntersectsWith(text))throw new Exception("Cat covers text: "+cat.Action);
        if(cat.ContactPercent.HasValue) {
            Point paws=cat.Position+new Vector(layout.CatSize/2,layout.CatSize*28/32);
            if(Math.Abs((paws-layout.Center).Length-layout.Radius)>.01)throw new Exception("Contact does not follow the progress arc: "+cat.Action);
        }
    }
    public static void Main() {
        try { Run(); } catch(Exception error) { Console.Error.WriteLine(error.GetType().Name+": "+error.Message);Environment.ExitCode=1; }
    }
    static void Run() {
        var actions=new HashSet<string>();var modes=new HashSet<string>();double distance=0;int slideFrames=0,scenarios=0,detachments=0,dataHits=0;
        for(int variant=0;variant<2;variant++)for(int seed=0;seed<8;seed++) {
            var layout=new CatPlaygroundLayout(variant==0);var cat=new CatExplorer(seed);cat.Configure(layout,0);
            if(layout.Width!=layout.Height)throw new Exception("The card must remain square.");
            cat.SetQuota(seed*14);
            for(int frame=0;frame<5400;frame++) {
                Point old=cat.Position;string oldMode=cat.PhysicsMode,oldAction=cat.Action;double oldEnergy=cat.Energy;
                cat.Tick(frame/30d,1/30d);Check(cat,layout);modes.Add(cat.PhysicsMode);
                if(oldMode=="rail"&&oldAction=="slide"&&cat.PhysicsMode=="air")detachments++;
                if(oldMode=="rail"&&oldAction=="slide"&&cat.PhysicsMode=="rail"&&cat.Action=="slide"&&cat.Energy>oldEnergy+2)throw new Exception("Passive sliding created energy.");
                if(double.IsNaN(cat.Velocity.X)||double.IsInfinity(cat.Velocity.Y)||cat.LandingCompression<0||cat.LandingCompression>.7)throw new Exception("Unstable integration.");
                actions.Add(cat.Action);distance+=(cat.Position-old).Length;
                if(cat.Action=="slide"&&cat.ContactPercent.HasValue)slideFrames++;
            }
            dataHits+=cat.DataCollisionCount;
            scenarios++;
        }
        foreach(string action in new[] {"walk","look","sit","groom","stretch","nap","sniff","balance","slide","climb","paw","hop","turn","land"})if(!actions.Contains(action))throw new Exception("Missing action: "+action);
        if(distance<500||slideFrames<500)throw new Exception("Insufficient exploration or sliding.");
        foreach(string mode in new[] {"perch","rail","air","ground"})if(!modes.Contains(mode))throw new Exception("Missing physical mode: "+mode);
        if(detachments<10)throw new Exception("The cat never loses surface support.");
        var resized=new CatExplorer(99);
        for(int i=0;i<200;i++) { var layout=new CatPlaygroundLayout(i%2==0);resized.Configure(layout,i);resized.Tick(i+.1,.1);Check(resized,layout); }
        Console.WriteLine("PASS: "+scenarios+" square playground scenarios, 86,400 bounded steps, passive energy checks, "+detachments+" support-loss transitions, 14 actions, "+slideFrames+" rail frames and 200 size switches. Text safety contacts: "+dataHits+".");
    }
}
