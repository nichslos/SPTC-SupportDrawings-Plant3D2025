using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev86AppearanceProbeTests
{
    private sealed class FakeViewDef
    {
        public int HiddenLineMode { get; set; }
        public int InsulationDisplay { get; set; }
        public string Name { get; set; } = "";
        private int RenderStyle;
    }

    internal static void Run()
    {
        var d=NativeOrthoAppearanceProbe86.Describe(typeof(FakeViewDef));
        if(!d.Any(x=>x.Contains("HiddenLineMode",StringComparison.Ordinal))||
           !d.Any(x=>x.Contains("InsulationDisplay",StringComparison.Ordinal))||
           !d.Any(x=>x.Contains("RenderStyle",StringComparison.Ordinal))||
           d.Any(x=>x.StartsWith("Name ",StringComparison.Ordinal)))
            throw new Exception("REV86 discovery must report appearance "+
                "metadata only without untrusted numeric enum mappings.");

        if(!NativeOrthoAppearanceProbe86.IsAppearanceMember("HiddenLineMode")||
           NativeOrthoAppearanceProbe86.IsAppearanceMember("ViewCenter"))
            throw new Exception("REV86 appearance naming guard failed");

        Console.WriteLine("REV86 read-only ViewDef appearance schema discovery PASS");
    }
}
