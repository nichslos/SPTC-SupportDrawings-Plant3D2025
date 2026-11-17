using System;
using SPTC.SupportDrawings;

internal static class Rev76ProductionUiTests
{
    private static void Need(bool value,string why)
    {
        if(!value)throw new Exception("REV76 UI: "+why);
    }

    internal static void Run()
    {
        var normal=SupportDrawingOutputMode76.FromIndex(0);
        var draft=SupportDrawingOutputMode76.FromIndex(1);
        var split=SupportDrawingOutputMode76.FromIndex(2);
        Need(normal==SupportDrawingOutputMode76.Mode.Physical &&
             SupportDrawingOutputMode76.Eligible(normal,3,0,true),
             "default physical output retains ordinary subset, auto and batch behavior");
        Need(SupportDrawingOutputMode76.AllowDraftOverflow(draft) &&
             !SupportDrawingOutputMode76.BypassOneSheetLimit(draft) &&
             !SupportDrawingOutputMode76.AllowDraftOverflow(split) &&
             SupportDrawingOutputMode76.BypassOneSheetLimit(split),
             "exactly one output mode maps to the existing REV74/75 flags");
        Need(SupportDrawingOutputMode76.Eligible(draft,10,10,false)&&
             SupportDrawingOutputMode76.Eligible(split,10,10,false)&&
             !SupportDrawingOutputMode76.Eligible(split,6,10,false)&&
             !SupportDrawingOutputMode76.Eligible(draft,10,0,false)&&
             !SupportDrawingOutputMode76.Eligible(draft,10,10,true),
             "strict options require All 10, explicit scale and no batch");
        bool invalid=false;
        try {SupportDrawingOutputMode76.FromIndex(-1);}
        catch(ArgumentOutOfRangeException){invalid=true;}
        Need(invalid,"unknown UI selection is never silently downgraded");

        string report="2026-11-17T03:12:46.000+00:00 "+
            "REV75_DRAFT_OVERFLOW_ACTIVE=True\n"+
            "2026-11-17T03:12:46.002+00:00 NATIVE_ORTHO_VIEW_COUNT=10\n"+
            "2026-11-17T03:12:46.003+00:00 "+
            "REV22_41_DIMENSIONS_CREATED=33\n"+
            "2026-11-17T03:12:46.004+00:00 "+
            "REV22_61_EXPECTED_HV_OVERALLS=12\n"+
            "2026-11-17T03:12:46.005+00:00 "+
            "REV22_61_PRESENT_HV_OVERALLS=12\n"+
            "2026-11-17T03:12:46.006+00:00 "+
            "REV22_61_QA_OPTIONAL_OMISSIONS=4\n"+
            "2026-11-17T03:12:46.007+00:00 "+
            "STEP10_ACTUAL_VIEWPORT_OVERLAP_DETECTED=False\n"+
            "2026-11-17T03:12:46.008+00:00 "+
            "FINAL_STATUS=TEN_NATIVE_VIEWS_AND_TITLES_MEMBER_LEADERS_PASS_DRAFT_ONLY\n";
        var summary=SupportDrawingQaSummary76.Read(report);
        Need(summary.NativeViews=="10"&&summary.Dimensions=="33"&&
             summary.Overalls=="12/12"&&summary.OptionalOmissions=="4"&&
             summary.ViewOverlap=="False"&&summary.DraftOnly,
             "the palette reports only actual RESULT.txt measured values");
        Need(SupportDrawingQaSummary76.Format(summary).Contains(
                "OFF-SHEET DRAFT",StringComparison.Ordinal),
             "draft-only output is never portrayed as approved to publish");
        var absent=SupportDrawingQaSummary76.Read("");
        Need(absent.NativeViews=="Unknown"&&
             absent.Dimensions=="Unknown"&&!absent.DraftOnly,
             "missing reports never fabricate QA PASS");
        Console.WriteLine("REV76 UI output mapping, eligibility, nonpersisted "+
            "defaults and source-verified QA formatting PASS");
    }
}
