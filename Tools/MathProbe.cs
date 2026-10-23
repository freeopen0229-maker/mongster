using System;
using UnityEngine;
using Mongle;

public static class MathProbe
{
    static void Require(bool ok, string message) { if(!ok) throw new Exception(message); }
    public static void Main()
    {
        double maxError = 0;
        for(int i=0;i<20000;i++)
        {
            var hip = new Vector2(0,.4f);
            var foot = new Vector2((float)Math.Sin(i*.131)*.25f, (float)Math.Abs(Math.Cos(i*.173))*.15f);
            foreach(float bend in new[]{-1f,1f})
            {
                var knee=NeulguiMath.Knee(hip,foot,bend);
                double a=Math.Abs(Vector2.Distance(hip,knee)-NeulguiMath.Upper);
                double b=Math.Abs(Vector2.Distance(knee,foot)-NeulguiMath.Lower);
                maxError=Math.Max(maxError,Math.Max(a,b));
                Require(a<.00001&&b<.00001,"IK changed limb length");
            }
        }
        for(int i=0;i<=1000;i++)
        {
            float t=i/1000f,v=NeulguiMath.Ease(t);
            Require(v>=0&&v<=1,"Invalid easing range");
            if(i>0)Require(v>=NeulguiMath.Ease((i-1)/1000f),"Non-monotonic easing");
        }
        var stop=new HitStopState();
        Require(stop.Trigger(10,.05,.5f)==0,"Hit stop did not freeze");
        Require(stop.Evaluate(10.049)==0,"Hit stop ended early");
        Require(stop.Evaluate(10.051)==.5f&&!stop.Frozen,"Previous time scale not restored");
        stop.Trigger(20,.05,1);stop.Trigger(20.04,.05,0);
        Require(stop.Evaluate(20.06)==0,"Overlapping hit stops ended early");
        Require(stop.Evaluate(20.091)==1,"Overlap lost original scale");
        stop.Trigger(30,.05,.25f);Require(stop.Release()==.25f&&!stop.Frozen,"Disable cleanup failed");
        Require(stop.Trigger(40,0,.75f)==.75f&&!stop.Frozen,"Zero duration froze time");
        Console.WriteLine("PASS: 0.05s hit-stop duration, overlap extension, original slow-motion scale restore, disable cleanup and zero-duration handling.");
        Console.WriteLine("PASS: 40000 IK poses, both bend directions, maximum segment length error="+maxError.ToString("G8")+". Easing range and monotonicity pass.");
        Console.WriteLine("Scope: Unity managed Vector2/Mathf and production NeulguiMath.cs under the Unity-bundled Mono runtime. This is not Play Mode or rendering.");
    }
}
