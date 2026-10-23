using System;
using Mongle.VisualRenewal;

public static class VisualRenewalProbe
{
    static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
    static double Run(int hz)
    {
        double x = 0, v = 0;
        for (int i = 0; i < hz * 2; i++) PetSpringMath.Step(ref x, ref v, 12, 2.5, 1.0 / hz);
        return x;
    }
    public static void Main()
    {
        double a = Run(30), b = Run(60), c = Run(120);
        Require(Math.Abs(a-b) < 1e-10 && Math.Abs(b-c) < 1e-10, "Frame partition agreement");
        double x = 4, v = 3;
        PetSpringMath.Step(ref x, ref v, 0, 2, 0);
        Require(x == 4 && v == 3, "Paused clock");
        PetSpringMath.Step(ref x, ref v, 0, 2, 100);
        Require(Math.Abs(x) < 1e-9 && Math.Abs(v) < 1e-9, "Long delta stability");
        var random = new Random(20261022);
        x = v = 0;
        for (int i = 0; i < 100000; i++)
        {
            PetSpringMath.Step(ref x, ref v, random.NextDouble()*36-18,
                              .1+random.NextDouble()*10, .001+random.NextDouble()*.1);
            Require(!double.IsNaN(x) && !double.IsInfinity(x) && Math.Abs(x)<100, "Variable input stability");
        }
        Require(PetSpringMath.WrapDegrees(358) == -2 && PetSpringMath.WrapDegrees(-358) == 2, "Turn wrap");
        bool rejected = false;
        try { PetSpringMath.Step(ref x, ref v, double.NaN, 2, .01); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        Require(rejected, "Invalid input rejection");
        Console.WriteLine("PASS: 30/60/120 Hz spring partition; pause; long delta; 100000 variable inputs; turn wrap; invalid input. Managed math only, not Unity animation.");
    }
}
