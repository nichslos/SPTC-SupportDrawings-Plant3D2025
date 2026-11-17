using System;

namespace SPTC.SupportDrawings;

/// <summary>REV77 presentation-only risk label for a real RESULT.txt.
/// No generation or project-write decisions are based on this label.</summary>
internal static class SupportDrawingQaStatus77
{
    internal static string Assess(string reportText,
        SupportDrawingQaSummary76.Result? result)
    {
        if(string.IsNullOrWhiteSpace(reportText)||result==null)
            return "UNVERIFIED";
        if(result.GenerationStatus.Contains("FAIL",
            StringComparison.OrdinalIgnoreCase)||
           result.GenerationStatus.Contains("ERROR",
            StringComparison.OrdinalIgnoreCase))
            return "FAIL (reported)";
        if(result.DraftOnly||
           reportText.Contains("NEEDS_REVIEW=True",
               StringComparison.OrdinalIgnoreCase)||
           reportText.Contains("ISSUE_STATUS=REVIEW",
               StringComparison.OrdinalIgnoreCase))
            return "REVIEW";
        if(result.GenerationStatus.Contains("PASS",
                StringComparison.OrdinalIgnoreCase)&&
           (result.ViewOverlap.Equals("False",
                StringComparison.OrdinalIgnoreCase)||
            result.ViewOverlap.Equals("0",StringComparison.OrdinalIgnoreCase)))
            return "PASS (reported)";
        return "UNVERIFIED";
    }
}
