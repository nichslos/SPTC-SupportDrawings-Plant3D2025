using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev70ProjectMediaTests
{
    private static void Need(bool yes,string reason)
    {
        if(!yes)throw new Exception("REV70 registered Ortho media: "+reason);
    }

    internal static void VerifyFallback(
        NativeOrthoLayout.Plan model,
        string[] names,double width,double height,
        double modelWidth,double modelHeight)
    {
        // Source-independent projected geometry: the same 10-view
        // native set is checked against physical media measured from
        // the registered EMPTY Ortho seed, not a named ISO format.
        var plan=NativeOrthoTenViewPlan.Build(model,width,height) with{
            Denominator=10,Scale=.1
        };
        plan=plan with{
            Views=plan.Views.Select(v=>v with{
                ModelWmm=modelWidth,ModelHmm=modelHeight
            }).ToArray()
        };
        Need(NativeOrthoTitleBlockBypass69.Eligible(width,height,10,10),
            "positive, valid project paper is eligible for actual physical fit");
        bool oldFailed=false;
        try
        {
            NativeOrthoExactScalePlacement61.Build(
                plan,names,width,height);
        }
        catch(NativeOrthoExactScalePlacement61.DoesNotFitException)
        {
            oldFailed=true;
        }
        Need(oldFailed,
            "strict old title-block reserved planner must fail first");
        var result=NativeOrthoExactScalePlacement61.Build(
            plan,names,width,height,bypassTitleBlock:true);
        Need(result.Views.Count==10&&result.Titles.Count==10&&
            result.Views.Select(v=>v.Name).SequenceEqual(names),
            "native view order and count unchanged for every valid project media");
        var sheet=NativeOrthoEffectivePaper59.FromMedia(width,height,0);
        NativeOrthoEffectivePaper59.RequireInsideUsableArea(
            sheet,
            result.Views.Select(v=>
                new NativeOrthoEffectivePaper59.ViewFrame(
                    v.Name,v.CenterX,v.CenterY,v.PaperFrameWidth,
                    v.PaperFrameHeight)).ToArray(),
            result.Titles.Select(t=>
                new NativeOrthoEffectivePaper59.CaptionFrame(
                    t.Name,t.CenterX,t.TopY,t.Width,t.Height)).ToArray(),
            requireTen:true,bypassTitleBlock:true);
        Need(result.Views.All(v=>Math.Abs(v.PaperFrameWidth-
                Math.Max(NativeOrthoViewTitlePlan.MinimumFrameWidth(v.Name),
                    modelWidth*.1*1.05))<1e-6),
            "1:10 dimensions not reduced to hide paper shortage");
    }

    internal static void Run()
    {
        string[] names={"TOP","BOTTOM","LEFT","RIGHT","FRONT","BACK",
            "NW","NE","SW","SE"};
        var model=NativeOrthoLayout.Build(
            new BoundingBox3(0,0,0,180,1234,603),1.0,0);

        // No hardcoded A0 threshold or landscape-only filter:
        // these represent possible registered paper media, not any
        // claim that the user has these particular seeds.
        VerifyFallback(model,names,841,594,1150,900); // A1 landscape
        VerifyFallback(model,names,594,841,1150,900); // A1 portrait
        VerifyFallback(model,names,920,650,1150,900); // custom paper
        VerifyFallback(model,names,594,420,550,445);  // smaller A2-like

        Need(NativeOrthoTitleBlockBypass69.Eligible(420,297,10,10),
            "A3-like media is eligible, not promised to fit");
        Need(!NativeOrthoTitleBlockBypass69.Eligible(
                double.NaN,594,10,10)&&
             !NativeOrthoTitleBlockBypass69.Eligible(20,594,10,10)&&
             !NativeOrthoTitleBlockBypass69.Eligible(9000,594,10,10)&&
             !NativeOrthoTitleBlockBypass69.Eligible(841,594,0,10)&&
             !NativeOrthoTitleBlockBypass69.Eligible(841,594,10,6),
            "invalid media, nonmanual scale, or incomplete view set rejected");
        Need(!NativeOrthoSingleSheet64.TryExactTenFit(
            model,names,10,420,297,out _,
            bypassTitleBlock:true),
            "a physically insufficient sheet still fails even if eligible");
        var small=NativeOrthoLayout.Build(
            new BoundingBox3(0,0,0,10,25,10),1.0,0);
        var chosen=NativeOrthoSingleSheet64.SmallestFitting(
            small,names,10,new[]{
                new NativeOrthoSingleSheet64.Seed(
                    "custom_920x650.dwg",920,650,true),
                new NativeOrthoSingleSheet64.Seed(
                    "a1_841x594.dwg",841,594,true),
                new NativeOrthoSingleSheet64.Seed(
                    "a2_594x420.dwg",594,420,true),
                new NativeOrthoSingleSheet64.Seed(
                    "not_registered.dwg",300,220,false)
            },bypassTitleBlock:true);
        Need(chosen!=null&&chosen.Path=="a2_594x420.dwg",
            "registered, EMPTY native seed selected by actual fit and "+
            "smallest area regardless of paper name");
        Console.WriteLine(
            "REV70 all project-registered EMPTY Ortho media: A1 landscape, "+
            "A1 portrait, custom, A2-size strict-first fallback; A3-size "+
            "not falsely declared to fit; dynamic registered media selection PASS");
    }
}
