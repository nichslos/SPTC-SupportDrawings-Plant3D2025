using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev61ExactScaleTests
{
    private static void Require(bool condition,string problem)
    {
        if(!condition)throw new Exception("REV61 exact scale: "+problem);
    }

    private static void MustNotFit(Action action,string problem)
    {
        try{action();}
        catch(InvalidOperationException){return;}
        throw new Exception("REV61 accepted unsafe paper fit: "+problem);
    }

    internal static void Run()
    {
        // Physically comparable to supplied TEST5: its 1:25 native
        // TOP frame was 28x51.825 and its side face 51.825x25.34.
        // No project path, source handle or hard-coded production viewport.
        var geometry=NativeOrthoLayout.Build(
            new BoundingBox3(0,0,0,180,1234,603),1.0,0);
        var baseline=NativeOrthoTenViewPlan.Build(geometry,841,594);
        Require(baseline.Denominator==25,
            "original conservative A1 10-view AUTO baseline stays 1:25");
        var auto=NativeOrthoTenViewAutoPlacement26.Build(
            baseline,841,594);
        Require(auto.Views.Count==10&&auto.Titles.Count==10&&
            !NativeOrthoExactScalePlacement61.NeedsAdaptiveLayout(
                0,baseline.Denominator),
            "automatic uses unmodified legacy two-column/five-row placement");

        var exact20=baseline with {Denominator=20,Scale=1.0/20};
        Require(NativeOrthoExactScalePlacement61.NeedsAdaptiveLayout(
            20,baseline.Denominator),
            "requested 1:20 must be physically evaluated, not blindly denied");
        var all10=NativeOrthoExactScalePlacement61.Build(
            exact20,baseline.Views.Select(v=>v.Name).ToArray(),841,594);
        Require(all10.Views.Count==10&&all10.Titles.Count==10,
            "TEST5-like 1:20 accepts all ten if conservatively within A1");
        NativeOrthoEffectivePaper59.RequireInsideUsableArea(
            NativeOrthoEffectivePaper59.FromMedia(841,594,0),
            all10.Views.Select(v=>new NativeOrthoEffectivePaper59.ViewFrame(
                v.Name,v.CenterX,v.CenterY,
                v.PaperFrameWidth,v.PaperFrameHeight)).ToArray(),
            all10.Titles.Select(t=>
                new NativeOrthoEffectivePaper59.CaptionFrame(
                    t.Name,t.CenterX,t.TopY,t.Width,t.Height)).ToArray(),
            requireTen:true);
        // A larger-scale six-ortho drawing fits in fewer rows while the
        // same ten-view drawing cannot fit physically on A1.
        var exact10=baseline with {Denominator=10,Scale=1.0/10};
        string[] six={"TOP","BOTTOM","LEFT","RIGHT","FRONT","BACK"};
        var ortho=NativeOrthoExactScalePlacement61.Build(
            exact10,six,841,594);
        Require(ortho.Views.Count==6&&ortho.Titles.Count==6&&
            ortho.Views.All(v=>six.Contains(v.Name)),
            "selected six orthos fit a true 1:10 without false ten-view rejection");
        MustNotFit(()=>NativeOrthoExactScalePlacement61.Build(
            exact10,baseline.Views.Select(v=>v.Name).ToArray(),841,594),
            "ten magnified 1:10 frames do not all fit A1 label and caption keepouts");

        // A single 1:1 projection is also physically larger than TEST5
        // A1. A genuinely larger configured seed can accommodate it.
        var exactOne=baseline with {Denominator=1,Scale=1};
        MustNotFit(()=>NativeOrthoExactScalePlacement61.Build(
            exactOne,new[]{"TOP"},841,594),
            "1:1 cannot put a 1.25-meter view into A1's 487-mm safe height");
        var large=NativeOrthoExactScalePlacement61.Build(
            exactOne,new[]{"TOP"},3500,2000);
        Require(large.Views.Count==1&&large.Titles.Count==1,
            "1:1 works if the registered plot sheet is sufficiently large");
        Require(!NativeOrthoExactScalePlacement61.NeedsAdaptiveLayout(
            30,baseline.Denominator),
            "1:30 and smaller printed sizes retain legacy 10-view placement");
        Console.WriteLine(
            "REV61 all dropdown scales remain literal 1:N; TEST5-like 1:20 x10, "+
            "1:10 x6 and large sheet 1:1 succeed without clipping, "+
            "overlarge A1 requests reject before project writes PASS");
    }
}
