using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.57 visual/datum policy from the user's manually dimensioned
/// 2026-11-12 six-orthographic-view reference PDF.
/// No sample 50/100/402/828/928 mm values occur in production.
/// Geometry remains authoritative; this only chooses which verified
/// dimensions to display and orders their paper-space lanes.
/// </summary>
public static class SupportOrthoManualDetailStyle57
{
    // The reference RIGHT view has an overall vertical + a local
    // terminal-profile thickness; the LEFT/FRONT/BACK reference shows
    // the additional vertical remaining span. This is a DRAFTING
    // selection, never a modification of the underlying measurement.
    public static bool ShowTerminalRemainder(
        string view,bool horizontal) =>
        !(view.Equals("RIGHT",StringComparison.OrdinalIgnoreCase)&&
          !horizontal);

    // Only the 2D *projected section of a selected native member*
    // counts as physical end-profile evidence. A 50mm centerline chain
    // INSIDE the selected profile cannot be shown as an additional
    // fabrication datum in the PDF reference drafting convention.
    public static bool ObscuredByTerminalMember(
        double chainStart,double chainEnd,
        double assemblyFrom,double assemblyTo,
        IReadOnlyList<SupportOrthoReferenceDatum56.Span> spans)
    {
        if(!double.IsFinite(chainStart)||
           !double.IsFinite(chainEnd)||chainEnd<=chainStart||
           spans==null)return false;
        const double tolerance=.18; // paper mm
        foreach(var member in spans)
        {
            if(!member.SourceKey.StartsWith("NATIVE:",
                StringComparison.OrdinalIgnoreCase))continue;
            double width=member.End-member.Start;
            double length=chainEnd-chainStart;
            bool atEnd=Math.Abs(member.Start-assemblyFrom)<=tolerance||
                       Math.Abs(member.End-assemblyTo)<=tolerance;
            bool inside=chainStart>=member.Start-tolerance&&
                        chainEnd<=member.End+tolerance;
            bool local=width>.5&&width<(assemblyTo-assemblyFrom)*.45;
            // Keep an actual member width dimension; suppress a
            // strictly smaller subinterval only when it lies entirely
            // inside that measured native end section.
            if(atEnd&&inside&&local&&length<width-tolerance)
                return true;
        }
        return false;
    }

    // Horizontal overall above all detail dimensions; vertical overall
    // to the LEFT of all detail dimensions. Compare ACTUAL line centers
    // rather than candidate offsets from DIFFERENT member bounds,
    // which caused overall/detail order to flip in REV22.56.
    public static bool InsideOverall(
        bool horizontal,double proposedLine,
        double overallLine,double paperGap=.55) =>
        horizontal?proposedLine<overallLine-paperGap:
                   proposedLine>overallLine+paperGap;

    // Enforce one selected, prominent terminal profile width for the
    // same axis when an end-member datum was recognized; do not add
    // another incidental 50 mm profile measurement beside a genuine
    // section width (e.g. 100 mm) in the same orientation.
    public static bool PreferLocalMember(
        string key,double from,double to,
        SupportOrthoReferenceDatum56.Datum? terminalDatum)
    {
        if(!terminalDatum.HasValue)return true;
        var datum=terminalDatum.Value;
        return key.Equals(datum.SourceKey,
            StringComparison.OrdinalIgnoreCase)&&
            Math.Abs(from-datum.MemberFrom)<.18&&
            Math.Abs(to-datum.MemberTo)<.18;
    }
}
