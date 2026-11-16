using System;
using System.Linq;
using SPTC.SupportDrawings;

/// <summary>
/// REV69: reproduce the measured multi-member failure mode (three
/// columns/four rows: width and height exceeded REV68's title block
/// reserve) with source-independent geometric projections. Must only
/// fit after explicit user-authorized A0 bypass. Printed title-block
/// entities are not part of this pure layout policy.
/// </summary>
internal static class Rev69TitleBlockBypassTests
{
    private static void Need(bool yes,string message)
    {
        if(!yes)throw new Exception("REV69 A0 title-block bypass: "+message);
    }
    internal static void Run()
    {
        string[] names={"TOP","BOTTOM","LEFT","RIGHT","FRONT",
            "BACK","NW","NE","SW","SE"};
        var source=NativeOrthoLayout.Build(
            new BoundingBox3(0,0,0,180,1234,603),1.0,0);
        var basePlan=NativeOrthoTenViewPlan.Build(source,1189,841);
        // Simulate a LARGE, densely projected support assembly,
        // rather than hardcoding the user's member geometry or tags.
        // In three columns each view reserves its 194.25mm frame and
        // its 136.5mm external annotation lane. 4 rows take 700mm.
        var wide=basePlan with {
            Denominator=10,Scale=.1,
            Views=basePlan.Views.Select(v=>v with{
                ModelWmm=1850,ModelHmm=1450
            }).ToArray()
        };
        bool strictFailed=false;
        try
        {
            NativeOrthoExactScalePlacement61.Build(
                wide,names,1189,841);
        }
        catch(NativeOrthoExactScalePlacement61.DoesNotFitException)
        {
            strictFailed=true;
        }
        Need(strictFailed,
            "original REV68 A0 .75 width/.12 bottom cannot fit wide assembly");
        Need(!NativeOrthoTitleBlockBypass69.Eligible(
                841,594,10,10)&&
             !NativeOrthoTitleBlockBypass69.Eligible(
                1189,841,0,10)&&
             !NativeOrthoTitleBlockBypass69.Eligible(
                1189,841,10,6)&&
             NativeOrthoTitleBlockBypass69.Eligible(
                1189,841,10,10),
            "bypass ONLY applies to exact 10-view landscape A0 and larger");
        var safe=NativeOrthoExactScalePlacement61.Build(
            wide,names,1189,841,bypassTitleBlock:true);
        Need(safe.Views.Count==10&&safe.Titles.Count==10&&
            safe.Views.Select(x=>x.Name).SequenceEqual(names),
            "ALL 10 native presets preserved at same exact scale");
        Need(safe.Views.All(v=>Math.Abs(v.PaperFrameWidth-194.25)<1e-6&&
            Math.Abs(v.PaperFrameHeight-152.25)<1e-6),
            "native viewport projected geometry and 1:10 remain unchanged");
        var page=NativeOrthoEffectivePaper59.FromMedia(1189,841,0);
        var frames=safe.Views.Select(v=>
            new NativeOrthoEffectivePaper59.ViewFrame(
                v.Name,v.CenterX,v.CenterY,
                v.PaperFrameWidth,v.PaperFrameHeight)).ToArray();
        var titles=safe.Titles.Select(t=>
            new NativeOrthoEffectivePaper59.CaptionFrame(
                t.Name,t.CenterX,t.TopY,t.Width,t.Height)).ToArray();
        NativeOrthoEffectivePaper59.RequireInsideUsableArea(
            page,frames,titles,requireTen:true,bypassTitleBlock:true);
        bool oldBoundaryFailed=false;
        try
        {
            NativeOrthoEffectivePaper59.RequireInsideUsableArea(
                page,frames,titles,requireTen:true);
        }
        catch(InvalidOperationException){oldBoundaryFailed=true;}
        Need(oldBoundaryFailed,
            "strict title-block reservation still protected when bypass is OFF");
        Need(NativeOrthoTitleBlockBypass69.EntersOldTitleBlockZone(
                safe.Views.Select(v=>v.CenterX+v.PaperFrameWidth/2)
                .Concat(safe.Titles.Select(t=>t.CenterX+t.Width/2)),
                1189),
            "layout warns of potential title-block OVERPRINT instead of claiming no overlap");
        var last=safe.Views.OrderByDescending(v=>
            v.CenterX+v.PaperFrameWidth/2).First();
        var actual=new NativeOrthoViewportPaperGuard.Frame(
            last.Name,last.CenterX,last.CenterY,
            last.PaperFrameWidth,last.PaperFrameHeight,.1,
            last.PaperFrameHeight/.1);
        Need(NativeOrthoViewportPaperGuard.Validate(
            actual,actual,1189,841,
            Array.Empty<NativeOrthoViewportPaperGuard.Frame>(),
            bypassTitleBlock:true)=="PASS",
            "real native viewport guard permits the bounded A0 fallback");
        Need(NativeOrthoViewportPaperGuard.Validate(
            actual,actual,1189,841,
            Array.Empty<NativeOrthoViewportPaperGuard.Frame>())!=
            "PASS",
            "the old strict A0 viewport border gate remains unchanged");
        Need(!NativeOrthoSingleSheet64.TryExactTenFit(
                source,names,10,841,594,out _,
                bypassTitleBlock:true),
            "A1 is never accidentally widened into false A0 printable media");

        // Previous successful A0 TEST5 must remain strict-first
        // (bypass is not an automatic alternate whenever strict fits).
        Need(NativeOrthoSingleSheet64.TryExactTenFit(
            source,names,10,1189,841,out _),
            "REV66–REV68 successful exact 1:10 TEST5 still passes strict layout");
        Console.WriteLine(
            "REV69 strict-first A0 1:10 All10, optional title-block "+
            "reservation bypass, 3x4 multi-member fit, actual physical "+
            "border/scale checks, printed title-block overlap warning PASS");
    }
}
