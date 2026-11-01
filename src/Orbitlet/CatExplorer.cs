using System;
using System.Windows;
namespace Orbitlet {
    // Fixed-step, mass-one 2D body. Gravity, tangential friction, claw traction,
    // contact loss and collision impulses determine motion; AI chooses intentions.
    public sealed class CatExplorer {
        public const double Gravity=330;
        const double Step=1d/120;
        readonly Random random;
        CatPlaygroundLayout layout;
        Point foot;
        Vector velocity,pendingImpulse;
        double angle=-Math.PI/2,speed,time,remainder,deadline,externalNow,quota=75;
        double climbTarget=-Math.PI/2,walkTarget,compressionVelocity;
        bool grip,pendingFacing,firstRelease=true;
        string pending="",mode="perch";
        public Point Position {get;private set;}
        public string Action {get;private set;}
        public bool FacingRight {get;private set;}
        public double ActionStarted {get;private set;}
        public double Tilt {get{return 0;}}
        public double? ContactPercent {get;private set;}
        public Vector Velocity {get{return velocity;}}
        public string PhysicsMode {get{return mode;}}
        public double GaitDistance {get;private set;}
        public double LandingCompression {get;private set;}
        public int DataCollisionCount {get;private set;}
        public double Energy {get{return .5*velocity.LengthSquared-Gravity*foot.Y;}}
        double Half {get{return layout.CatSize/2;}}
        double BodyHeight {get{return layout.CatSize*28/32;}}
        double Floor {get{return layout.InnerHeight-6;}}
        public CatExplorer(int seed){random=new Random(seed);Action="balance";FacingRight=true;}
        public void SetQuota(double value){quota=Math.Max(0,Math.Min(100,value));}
        public bool Safe(Point point){
            if(layout==null)return false;
            var box=new Rect(point,new Size(layout.CatSize,BodyHeight));
            if(!new Rect(4,4,layout.InnerWidth-8,layout.InnerHeight-8).Contains(box))return false;
            foreach(Rect obstacle in layout.Protected)if(box.IntersectsWith(obstacle))return false;
            return true;
        }
        void Pose(string action){if(Action==action)return;Action=action;ActionStarted=externalNow;}
        void UpdatePosition(){Position=new Point(foot.X-Half,foot.Y-BodyHeight);}
        public void Configure(CatPlaygroundLayout geometry,double now){
            layout=geometry;externalNow=now;time=0;remainder=0;deadline=.85;mode="perch";angle=-Math.PI/2;
            foot=new Point(layout.Center.X,layout.Center.Y-layout.Radius);velocity=new Vector();speed=0;
            grip=true;firstRelease=true;pending="";LandingCompression=0;compressionVelocity=0;
            FacingRight=true;Action="balance";ActionStarted=now;ContactPercent=50;UpdatePosition();
        }
        void Release(int direction){
            FacingRight=direction>0;mode="rail";grip=false;speed=direction*9;
            velocity=new Vector(-Math.Sin(angle)*speed,Math.Cos(angle)*speed);
            Pose("slide");deadline=time+5;
        }
        void Rest(){
            mode="ground";grip=false;ContactPercent=null;pending="";
            string[] actions={"look","sit","groom","stretch","nap","sniff"};
            string action=actions[random.Next(actions.Length)];Pose(action);
            deadline=time+(action=="nap"?3+random.NextDouble()*3:1.2+random.NextDouble()*1.8);
        }
        void GroundIntention(){
            bool right=foot.X>layout.Center.X;
            if(random.NextDouble()<.28){
                Rect data=layout.Protected[1];
                double lo=right?data.Right+Half+1:4+Half,hi=right?layout.InnerWidth-4-Half:data.Left-Half-1;
                walkTarget=lo+random.NextDouble()*Math.Max(0,hi-lo);
                pendingFacing=walkTarget>=foot.X;pending="walk";
            }else{
                double targetAngle=right?-Math.PI/4:-3*Math.PI/4;
                Point target=new Point(layout.Center.X+Math.Cos(targetAngle)*layout.Radius,layout.Center.Y+Math.Sin(targetAngle)*layout.Radius);
                double rise=foot.Y-target.Y,vy=-Math.Sqrt(2*Gravity*(Math.Max(0,rise)+6));
                double flight=(-vy+Math.Sqrt(Math.Max(0,vy*vy-2*Gravity*rise)))/Gravity;
                pendingImpulse=new Vector((target.X-foot.X)/Math.Max(.1,flight),vy);
                pendingFacing=!right;pending="jump";
            }
            if(pendingFacing!=FacingRight){Pose("turn");deadline=time+.24;}
            else StartGroundIntention();
        }
        void StartGroundIntention(){
            FacingRight=pendingFacing;
            if(pending=="jump"){
                mode="air";velocity=pendingImpulse;grip=true;climbTarget=-Math.PI/2;Pose("hop");pending="";
            }else{Pose("walk");deadline=time+2.5;}
        }
        void RailStep(double dt){
            double sine=Math.Sin(angle),cosine=Math.Cos(angle);
            double normal=-Gravity*sine-speed*speed/layout.Radius;
            if(!grip&&normal<0){mode="air";velocity=new Vector(-sine*speed,cosine*speed);ContactPercent=null;Pose("hop");return;}
            double drive=0;
            if(grip){
                double desired=Math.Max(-30,Math.Min(30,(climbTarget-angle)*layout.Radius*4));
                drive=Math.Max(-700,Math.Min(700,(desired-speed)*14-Gravity*cosine));
            }
            double friction=.055*Math.Max(0,normal)*(speed==0?0:Math.Sign(speed));
            speed+=(Gravity*cosine+drive-friction-.22*speed)*dt;
            angle+=speed/layout.Radius*dt;
            foot=new Point(layout.Center.X+Math.Cos(angle)*layout.Radius,layout.Center.Y+Math.Sin(angle)*layout.Radius);
            velocity=new Vector(-Math.Sin(angle)*speed,Math.Cos(angle)*speed);
            ContactPercent=(angle*180/Math.PI+225)/270*100;
            if(grip){
                GaitDistance+=Math.Abs(speed)*dt;
                if(Math.Abs((climbTarget-angle)*layout.Radius)<.5&&Math.Abs(speed)<2){
                    angle=climbTarget;foot=new Point(layout.Center.X+Math.Cos(angle)*layout.Radius,layout.Center.Y+Math.Sin(angle)*layout.Radius);
                    ContactPercent=(angle*180/Math.PI+225)/270*100;velocity=new Vector();speed=0;mode="perch";
                    Pose(Math.Abs(angle+Math.PI/2)>.2?"paw":"balance");deadline=time+1.1+random.NextDouble()*.7;
                }
            }
        }
        void AirStep(double dt){
            Point previous=foot;
            velocity.Y+=Gravity*dt;foot+=velocity*dt;
            Vector radial=foot-layout.Center;
            double distance=radial.Length;
            if(distance>0&&distance<=layout.Radius&&radial.Y/distance<-.12&&(previous-layout.Center).Length>=layout.Radius-.01){
                Vector normal=radial/distance;
                if(Vector.Multiply(velocity,normal)<0){
                    angle=Math.Atan2(normal.Y,normal.X);Vector tangent=new Vector(-normal.Y,normal.X);
                    speed=Vector.Multiply(velocity,tangent);foot=layout.Center+normal*layout.Radius;
                    mode="rail";Pose(grip?"climb":"slide");ContactPercent=(angle*180/Math.PI+225)/270*100;
                    velocity=tangent*speed;
                }
            }
        }
        void Collisions(){
            if(foot.X<4+Half){foot.X=4+Half;velocity.X=Math.Abs(velocity.X)*.08;}
            if(foot.X>layout.InnerWidth-4-Half){foot.X=layout.InnerWidth-4-Half;velocity.X=-Math.Abs(velocity.X)*.08;}
            if(foot.Y<4+BodyHeight){foot.Y=4+BodyHeight;velocity.Y=Math.Abs(velocity.Y)*.08;}
            if(foot.Y>Floor+.001||(foot.Y>=Floor-.001&&velocity.Y>=0)){
                double impact=Math.Max(0,velocity.Y);foot.Y=Floor;velocity.Y=0;
                if(mode!="ground"){
                    mode="ground";ContactPercent=null;grip=false;pending="";
                    compressionVelocity+=Math.Min(8,impact/30);Pose("land");deadline=time+.3;
                }
            }
            var body=new Rect(foot.X-Half,foot.Y-BodyHeight,layout.CatSize,BodyHeight);
            foreach(Rect obstacle in layout.Protected)if(body.IntersectsWith(obstacle)){
                // A final safety collider. Chosen jumps travel up the side corridors.
                DataCollisionCount++;
                if(foot.X<layout.Center.X){foot.X=obstacle.Left-Half-.02;velocity.X=-Math.Abs(velocity.X)*.08;}
                else{foot.X=obstacle.Right+Half+.02;velocity.X=Math.Abs(velocity.X)*.08;}
                if(mode=="rail"||mode=="perch"){mode="air";ContactPercent=null;}
                body=new Rect(foot.X-Half,foot.Y-BodyHeight,layout.CatSize,BodyHeight);
            }
        }
        void Simulate(double dt){
            time+=dt;
            compressionVelocity+=(-180*LandingCompression-22*compressionVelocity)*dt;
            LandingCompression+=compressionVelocity*dt;
            if(LandingCompression<0){LandingCompression=0;compressionVelocity=0;}
            LandingCompression=Math.Min(.7,LandingCompression);
            if(mode=="perch"){
                velocity=new Vector();
                if(time>=deadline){
                    if(pending=="release-left"||pending=="release-right"){
                        int direction=pending=="release-right"?1:-1;pending="";Release(direction);
                    }else if(!firstRelease&&Math.Abs(angle+Math.PI/2)<.15&&random.NextDouble()<.3){
                        climbTarget=(135+270*Math.Max(35,Math.Min(65,quota))/100-360)*Math.PI/180;
                        grip=true;mode="rail";speed=0;Pose("climb");
                    }else{
                        int direction=firstRelease?1:(angle<-Math.PI/2?-1:angle>-Math.PI/2?1:random.Next(2)==0?-1:1);firstRelease=false;
                        if(FacingRight!=(direction>0)){pending=direction>0?"release-right":"release-left";Pose("turn");deadline=time+.22;}
                        else Release(direction);
                    }
                }
            }else if(mode=="rail")RailStep(dt);
            else if(mode=="air")AirStep(dt);
            else{
                double desired=0;
                if(pending=="walk"&&Action=="walk"){
                    desired=Math.Max(-16,Math.Min(16,(walkTarget-foot.X)*6));
                    if(Math.Abs(walkTarget-foot.X)<.5&&Math.Abs(velocity.X)<2)Rest();
                }
                velocity.X+=((desired-velocity.X)*18-3*velocity.X)*dt;
                foot.X+=velocity.X*dt;GaitDistance+=Math.Abs(velocity.X)*dt;
                if(time>=deadline){if(Action=="turn")StartGroundIntention();else if(Action=="land"||Action=="walk")Rest();else GroundIntention();}
            }
            Collisions();UpdatePosition();
        }
        public void Tick(double now,double elapsed){
            if(layout==null)return;externalNow=now;remainder+=Math.Min(.075,Math.Max(0,elapsed));
            while(remainder>=Step){Simulate(Step);remainder-=Step;}
        }
    }
}
