using System;
using Mongle.Slice.Server.Testing;
using Mongle.Slice.Testing;

public static class ViewportLocalProbe
{
    public static void Main()
    {
        PetTradeMockScenario.Run(Console.WriteLine);
        var egg=new LocalHatchSimulation(2000000);
        if(egg.Remaining(2000000)!=28800)throw new Exception("8h initial duration");
        if(!egg.TryUseInstantScroll(2000010)||egg.Remaining(2000010)!=0||egg.Scrolls!=0)throw new Exception("Instant hatch");
        if(egg.TryUseInstantScroll(2000010)||egg.Scrolls!=0)throw new Exception("Repeated use");
        var ready=new LocalHatchSimulation(100);
        if(ready.TryUseInstantScroll(28900)||ready.Scrolls!=1)throw new Exception("Ready egg must not consume scroll");
        Console.WriteLine("[PASS LOCAL SCROLL] 8h -> 0s; one scroll; repeat/ready-egg consumption blocked. Managed logic only.");
    }
}
