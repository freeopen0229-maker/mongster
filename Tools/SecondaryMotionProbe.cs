using System;
using Mongle.VisualRenewal;
class SecondaryMotionProbe
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static PetBlinkClock Simulate(int fps, int seconds)
    {
        var c = new PetBlinkClock(717);
        for (int i = 0; i < fps * seconds; i++)
        {
            c.Tick(1.0 / fps);
            Check(c.Closure >= 0 && c.Closure <= 1, "closure bounds");
        }
        return c;
    }
    static void Main()
    {
        var a = Simulate(30, 3600); var b = Simulate(60, 3600); var c = Simulate(120, 3600);
        Check(a.BlinkCount == b.BlinkCount && b.BlinkCount == c.BlinkCount, "fps blink count");
        Check(Math.Abs(a.Closure - c.Closure) < .0001, "fps phase");
        float frozen = a.Closure; int count = a.BlinkCount;
        a.Tick(0); Check(a.Closure == frozen && a.BlinkCount == count, "pause");
        var many = Simulate(30, 36000);
        double fraction = (double)many.DoubleCount / many.BurstCount;
        Check(fraction > .13 && fraction < .17, "15 percent distribution");
        var once = new PetBlinkClock(31); var split = new PetBlinkClock(31);
        once.Tick(100); for (int i = 0; i < 10000; i++) split.Tick(.01);
        Check(once.BlinkCount == split.BlinkCount && Math.Abs(once.Closure-split.Closure)<.001, "boundary remainder");
        var guaranteed = new PetBlinkClock(1, 2.5, 2.5, 1);
        guaranteed.Tick(2.49); Check(guaranteed.BlinkCount == 0, "early blink");
        guaranteed.Tick(.02); Check(guaranteed.BlinkCount == 1 && guaranteed.DoubleCount == 1, "first blink");
        guaranteed.Tick(.25); Check(guaranteed.BlinkCount == 2, "second blink");
        double x1=10,v1=0,x2=10,v2=0;
        for(int i=0;i<30;i++)PetSpringMath.Step(ref x1,ref v1,0,2.5,1.0/30);
        for(int i=0;i<120;i++)PetSpringMath.Step(ref x2,ref v2,0,2.5,1.0/120);
        Check(Math.Abs(x1-x2)<1e-10 && Math.Abs(v1-v2)<1e-10,"spring fps");
        Console.WriteLine("PASS: 30/60/120Hz blink sequence/closure, pause, remainder, fixed double-blink boundaries, spring equivalence.");
        Console.WriteLine("Observed doubles: "+many.DoubleCount+"/"+many.BurstCount+" = "+fraction.ToString("P2"));
        Console.WriteLine("NOT TESTED: Unity API compilation, Animator/rig binding, viewport, rendering, animation quality, mobile.");
    }
}
