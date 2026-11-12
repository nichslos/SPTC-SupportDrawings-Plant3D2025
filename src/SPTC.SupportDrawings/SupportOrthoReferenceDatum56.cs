using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.56: select a projected terminal MEMBER FACE datum from
/// native structural geometry. The manual drawing shows a long
/// structure with a short structural cross-section at one end:
/// overall = local member width + the projected remaining distance.
/// These are dynamic PROJECTION dimensions, never fabrication cut
/// lengths, centerline-to-centerline spacing or assumed bolt pitches.
/// The algorithm uses no literal 50, 100, 402 or 828 mm values.
/// </summary>
public static class SupportOrthoReferenceDatum56
{
    public readonly record struct Span(
        string SourceKey,double Start,double End);

    public readonly record struct Datum(
        string SourceKey,
        double MemberFrom,double MemberTo,
        double RemainingFrom,double RemainingTo,
        bool AtStart)
    {
        public double LocalWidth => MemberTo-MemberFrom;
        public double RemainingLength => RemainingTo-RemainingFrom;
    }

    public static Datum? Choose(
        IEnumerable<Span> projectedNativeMembers,
        double overallFrom,double overallTo)
    {
        if(projectedNativeMembers==null||
           !double.IsFinite(overallFrom)||
           !double.IsFinite(overallTo)||
           overallTo<=overallFrom)
            return null;
        const double paperTolerance=.18; // paper units, NOT model length
        double whole=overallTo-overallFrom;
        if(whole<1)return null;

        var members=projectedNativeMembers.Where(x=>
            x.SourceKey.StartsWith("NATIVE:",
                StringComparison.OrdinalIgnoreCase)&&
            double.IsFinite(x.Start)&&double.IsFinite(x.End)&&
            x.End-x.Start>.25&&
            x.Start>=overallFrom-paperTolerance&&
            x.End<=overallTo+paperTolerance).ToArray();

        // A member itself covers virtually all of this projected axis.
        // Without one, two touching sections could be independent
        // members rather than one continuous support; do not infer
        // an overall-complement detail from such ambiguous contact.
        var full=members.Where(m=>
            Math.Abs(m.Start-overallFrom)<=paperTolerance&&
            Math.Abs(m.End-overallTo)<=paperTolerance).ToArray();
        if(full.Length==0)return null;

        var terminals=members.Select(m=>
        {
            double width=m.End-m.Start;
            bool touchesFirst=Math.Abs(
                m.Start-overallFrom)<=paperTolerance;
            bool touchesLast=Math.Abs(
                m.End-overallTo)<=paperTolerance;
            bool usable=width>=.50&&width<=whole*.40&&
                (touchesFirst!=touchesLast)&&
                full.Any(w=>!w.SourceKey.Equals(m.SourceKey,
                    StringComparison.OrdinalIgnoreCase));
            if(!usable)return (Datum?)null;
            double remainingFrom=touchesFirst?
                m.End:overallFrom;
            double remainingTo=touchesFirst?
                overallTo:m.Start;
            if(remainingTo-remainingFrom<=Math.Max(width*.75,.5))
                return (Datum?)null;
            return new Datum(m.SourceKey,m.Start,m.End,
                remainingFrom,remainingTo,touchesFirst);
        }).Where(d=>d.HasValue)
          .Select(d=>d!.Value)
          .OrderByDescending(d=>d.LocalWidth)
          .ThenBy(d=>d.SourceKey,StringComparer.OrdinalIgnoreCase)
          .ToArray();

        // Don't create a double-accounted length. One prominent
        // terminal profile is selected; tied equally wide ends are
        // ambiguous and require verified connection geometry instead.
        if(terminals.Length>1&&
           Math.Abs(terminals[0].LocalWidth-
                    terminals[1].LocalWidth)<paperTolerance&&
           terminals[0].AtStart!=terminals[1].AtStart)
            return null;
        return terminals.Length>0?terminals[0]:null;
    }
}
