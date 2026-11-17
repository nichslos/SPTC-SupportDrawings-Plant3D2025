using System;
using System.Collections.Generic;
using System.Globalization;

namespace SPTC.SupportDrawings;

/// <summary>REV76 read-only per-output status derived from real RESULT.txt data.</summary>
internal static class SupportDrawingQaSummary76
{
    internal sealed record Result(string NativeViews,string Dimensions,
        string Overalls,string OptionalOmissions,string ViewOverlap,
        string GenerationStatus,bool DraftOnly);

    internal static Result Read(string reportText)
    {
        var values=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(string raw in (reportText??"").Split('\n'))
        {
            string line=raw.Trim();
            // Log lines carry ISO timestamp then whitespace and KEY=value.
            int space=line.IndexOf(' ');
            if(space>0 && char.IsDigit(line[0]))
                line=line[(space+1)..].TrimStart();
            int eq=line.IndexOf('=');
            if(eq<=0)continue;
            string key=line[..eq].Trim();
            if(key.Length>100||key.Contains(' '))continue;
            values[key]=line[(eq+1)..].Trim();
        }
        string Get(string key)=>
            values.TryGetValue(key,out string? s)&&s.Length>0?s:"Unknown";
        string expected=Get("REV22_61_EXPECTED_HV_OVERALLS");
        string present=Get("REV22_61_PRESENT_HV_OVERALLS");
        return new Result(Get("NATIVE_ORTHO_VIEW_COUNT"),
            Get("REV22_41_DIMENSIONS_CREATED"),
            present+"/"+expected,
            Get("REV22_61_QA_OPTIONAL_OMISSIONS"),
            Get("STEP10_ACTUAL_VIEWPORT_OVERLAP_DETECTED"),
            Get("FINAL_STATUS"),
            Get("REV75_DRAFT_OVERFLOW_ACTIVE").Equals("True",
                StringComparison.OrdinalIgnoreCase));
    }

    internal static string Format(Result result) =>
        "Views: "+result.NativeViews+
        "  |  Dimensions: "+result.Dimensions+
        "  |  Overall: "+result.Overalls+
        "  |  Optional omitted: "+result.OptionalOmissions+
        "\r\nViewport overlap: "+result.ViewOverlap+
        "  |  Output: "+(result.DraftOnly?
            "OFF-SHEET DRAFT — PDF blocked":"Native sheet output")+
        "\r\nStatus: "+result.GenerationStatus;

}
