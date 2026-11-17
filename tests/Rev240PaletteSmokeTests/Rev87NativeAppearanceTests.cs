using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev87NativeAppearanceTests
{
    private sealed class FakeSavedView
    {
        public object HlrTrans { get; set; } = new();
        public string ViewType { get; set; } = "Top";
        private int HiddenLineDisplay;
        public void SetInsulationDisplay(bool ignored) { }
    }

    internal static void Run()
    {
        if(!NativeOrthoAppearanceProbe87.IsRelevantMember("HlrTrans")||
           !NativeOrthoAppearanceProbe87.IsRelevantMember(
               "SetInsulationDisplay")||
           NativeOrthoAppearanceProbe87.IsRelevantMember("ViewType"))
            throw new Exception("REV87 native appearance discovery member filtering failed");
        if(!NativeOrthoAppearanceProbe87.IsRelevantType("PlantOrthoView")||
           NativeOrthoAppearanceProbe87.IsRelevantType("UnrelatedGeometry"))
            throw new Exception("REV87 native appearance discovery type filtering failed");

        var summary=NativeOrthoAppearanceProbe87.Describe(typeof(FakeSavedView));
        if(!summary.Any(x=>x.Contains("PROPERTY HlrTrans TYPE=",
                StringComparison.Ordinal))||
           !summary.Any(x=>x.Contains("FIELD HiddenLineDisplay TYPE=",
                StringComparison.Ordinal))||
           !summary.Any(x=>x.Contains("METHOD SetInsulationDisplay",
                StringComparison.Ordinal))||
           summary.Any(x=>x.Contains("ViewType",StringComparison.Ordinal)))
            throw new Exception(
                "REV87 HlrTrans metadata and hidden-line member discovery failed");

        Console.WriteLine("REV87 native Ortho appearance read-only discovery PASS");
    }
}
