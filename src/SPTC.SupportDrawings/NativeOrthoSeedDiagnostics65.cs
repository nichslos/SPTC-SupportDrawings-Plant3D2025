using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV65 read-only source of command-window seed diagnostics.
/// A registered EMPTY Ortho is never accepted merely because it has
/// A0-sized media; it must also pass native validation and exact
/// ten-view/annotation/title-block safe placement. No file writes.
/// </summary>
internal static class NativeOrthoSeedDiagnostics65
{
    internal sealed record Result(string FileName,string Status,
        double WidthMm,double HeightMm,string Detail);

    internal static IReadOnlyList<string> Format(
        IReadOnlyList<Result> candidates,int maximumDetails=20)
    {
        if(candidates==null)throw new ArgumentNullException(nameof(candidates));
        if(maximumDetails<1)throw new ArgumentOutOfRangeException(nameof(maximumDetails));
        var lines=new List<string>
        {
            "REV65_REGISTERED_EMPTY_SEEDS_CHECKED="+candidates.Count+
            " EXACT_FITS="+candidates.Count(x=>x.Status=="PASS")+
            " REJECTED="+candidates.Count(x=>x.Status!="PASS")
        };
        foreach(var result in candidates.Take(maximumDetails))
        {
            string dimensions=double.IsFinite(result.WidthMm)&&
                double.IsFinite(result.HeightMm)&&
                result.WidthMm>0&&result.HeightMm>0?
                result.WidthMm.ToString("0.###",CultureInfo.InvariantCulture)+
                "x"+result.HeightMm.ToString("0.###",
                    CultureInfo.InvariantCulture)+"mm":
                "UNREADABLE";
            lines.Add("REV65_SEED_CANDIDATE="+
                Safe(result.FileName,90)+
                " MEDIA="+dimensions+
                " RESULT="+Safe(result.Status,32)+
                " REASON="+Safe(result.Detail,475));
        }
        if(candidates.Count>maximumDetails)
            lines.Add("REV65_ADDITIONAL_SEED_CANDIDATES="+
                (candidates.Count-maximumDetails)+
                " (not shown; first "+maximumDetails+" evaluated above)");
        return lines;
    }

    private static string Safe(string? value,int max) =>
        new string((value??"").Take(max).Select(c=>
            char.IsControl(c)?' ':c).ToArray());
}
