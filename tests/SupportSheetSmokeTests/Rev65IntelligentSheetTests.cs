using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev65IntelligentSheetTests
{
    private static void Need(bool condition,string reason)
    {
        if(!condition)throw new Exception("REV65 intelligent one-sheet: "+reason);
    }

    internal static void Run()
    {
        var model=NativeOrthoLayout.Build(
            new BoundingBox3(0,0,0,180,1234,603),1.0,0);
        string[] presets={"TOP","BOTTOM","LEFT","RIGHT","FRONT",
            "BACK","NW","NE","SW","SE"};
        var original=NativeOrthoTenViewPlan.Build(model,841,594);
        Need(original.Denominator==25,
            "REV59 automatic 1:25 default cannot change");
        var originalFit=NativeOrthoTenViewAutoPlacement26.Build(
            original,841,594);
        Need(originalFit.Views.Count==10&&originalFit.Titles.Count==10,
            "REV59 default 10-view native placement remains available");
        var oneToTen=original with{Denominator=10,Scale=.1};
        bool a1Rejected=false;
        string fitReason="";
        try
        {
            NativeOrthoExactScalePlacement61.Build(
                oneToTen,presets,841,594);
        }
        catch(NativeOrthoExactScalePlacement61.DoesNotFitException ex)
        {
            a1Rejected=true;fitReason=ex.Message;
        }
        Need(a1Rejected&&fitReason.Contains("REV65 closest layout:")&&
            fitReason.Contains("required ")&&
            fitReason.Contains("shortage "),
            "A1 1:10 all 10 rejected with measured shortage, not generic denial");
        Need(NativeOrthoSingleSheet64.TryExactTenFit(
            model,presets,10,1189,841,out string a0Reason),
            "exact 1:10 TEST5 remains one A0 with validated titles: "+a0Reason);

        // Purpose-built projections: original alternating TALL/SHORT
        // yields five tall rows at 2 columns. Height-ranked orders
        // group tall frames into three rows plus two short rows.
        // Three or four columns exceed the right-title-block space,
        // so an actual layout search (not fallback to more columns)
        // is necessary to fit a 700 x 500 mm custom sheet.
        var envelope=original.Views.Select((v,i)=>v with {
            ModelWmm=50,
            ModelHmm=i%2==0?80:10
        }).ToArray();
        var alternating=original with {
            Denominator=1,Scale=1,Views=envelope
        };
        var fitted=NativeOrthoExactScalePlacement61.Build(
            alternating,presets,700,500);
        Need(fitted.Views.Count==10&&fitted.Titles.Count==10,
            "height-aware layout fits exact scale with all ten title bands");
        Need(fitted.Views.Select(v=>v.Name).SequenceEqual(presets),
            "adaptive paper positions do not change native preset order");
        var top=fitted.Views.First(v=>v.Name=="TOP");
        var bottom=fitted.Views.First(v=>v.Name=="BOTTOM");
        Need(Math.Abs(top.CenterY-bottom.CenterY)>1,
            "optimized layout repositions originally paired tall/short views");
        NativeOrthoEffectivePaper59.RequireInsideUsableArea(
            NativeOrthoEffectivePaper59.FromMedia(700,500,0),
            fitted.Views.Select(v=>new NativeOrthoEffectivePaper59.ViewFrame(
                v.Name,v.CenterX,v.CenterY,
                v.PaperFrameWidth,v.PaperFrameHeight)).ToArray(),
            fitted.Titles.Select(t=>new NativeOrthoEffectivePaper59.CaptionFrame(
                t.Name,t.CenterX,t.TopY,t.Width,t.Height)).ToArray(),
            requireTen:true);

        var diagnostic=NativeOrthoSeedDiagnostics65.Format(new[]{
            new NativeOrthoSeedDiagnostics65.Result(
                "SPTC_A1_SEED.dwg","NO_FIT",841,594,
                "Nearest row too high; shortage 130 mm"),
            new NativeOrthoSeedDiagnostics65.Result(
                "SPTC_A0_SEED.dwg","PASS",1189,841,
                "Ten native frames safe")
        });
        Need(diagnostic.Count==3&&
            diagnostic[0].Contains("SEEDS_CHECKED=2")&&
            diagnostic[0].Contains("EXACT_FITS=1")&&
            diagnostic[1].Contains("841x594mm")&&
            diagnostic[1].Contains("RESULT=NO_FIT")&&
            diagnostic[2].Contains("1189x841mm")&&
            diagnostic[2].Contains("RESULT=PASS"),
            "command diagnostic names each registered empty seed, media and why");
        Need(NativeOrthoSeedDiagnostics65.Format(new[]{
            new NativeOrthoSeedDiagnostics65.Result(
                "Bad\nSeed.dwg","MEDIA_UNREADABLE",0,0,
                "Failed\r\nread")
        })[1].Contains("UNREADABLE")&&
            !NativeOrthoSeedDiagnostics65.Format(new[]{
                new NativeOrthoSeedDiagnostics65.Result(
                    "Bad\nSeed.dwg","MEDIA_UNREADABLE",0,0,
                    "Failed\r\nread")
            })[1].Contains("\n"),
            "diagnostics escape control characters instead of command injection");
        Need(NativeOrthoSeedDiagnostics65.Format(
            Array.Empty<NativeOrthoSeedDiagnostics65.Result>())[0]
            .Contains("SEEDS_CHECKED=0"),
            "no registered empty seed reports zero rather than fake 2");

        Console.WriteLine(
            "REV65 single sheet: exact A1 failure quantified, A0 safe, "+
            "deterministic height-aware ten-view reflow, individual seed "+
            "media/reason diagnostics, untrusted filenames escaped PASS");
    }
}
