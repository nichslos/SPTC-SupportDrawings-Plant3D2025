using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.62 count structural TOS labels from actual verified source
/// records and actually selected paper-space callout text. Earlier
/// code counted every DESCRIPTION-only steel callout as if it had
/// a second TOS line, causing spurious STEP10 ... PARTIAL on TEST5.
/// </summary>
public static class SupportOrthoTosAudit62
{
    public sealed record Counts(int Expected,int Visible);

    public static Counts Count(
        IReadOnlyList<SupportOrthoMemberCalloutPlan27.Member> source,
        IReadOnlyList<SupportOrthoMemberCalloutPlan27.Callout> displayed,
        IReadOnlyCollection<string> groupMemberHandles,
        int selectedViews,bool tosEnabled)
    {
        if(source==null||displayed==null||groupMemberHandles==null||
           selectedViews<0||selectedViews>10)
            throw new ArgumentException("REV22.62 TOS source/viewport inputs invalid.");
        if(!tosEnabled)return new Counts(0,0);
        var nativeGroup=groupMemberHandles.ToHashSet(
            StringComparer.OrdinalIgnoreCase);
        var realTosMembers=source.Where(m=>
            m.XrefDrawingName==null&&m.PipeBopSource==null&&
            nativeGroup.Contains(m.Handle)&&m.HasTos)
            .Select(m=>m.Handle)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        int expected=realTosMembers.Count*selectedViews;
        // A callout whose first line is a member description is not
        // evidence of TOS. The TOS may be first line when the user
        // disables Description but retains TOS; in that case HasTos
        // is false because there is no SECOND line.
        int visible=displayed.Count(c=>
            !c.IsPipe&&!c.IsXrefMember&&
            realTosMembers.Contains(c.Handle)&&
            HasWrittenTosLine(c.Text));
        return new Counts(expected,visible);
    }

    private static bool HasWrittenTosLine(string? label) =>
        !string.IsNullOrWhiteSpace(label)&&
        label.Replace("\r\n","\n").Split('\n')
            .Any(line=>line.TrimStart().StartsWith("TOS",
                StringComparison.OrdinalIgnoreCase));
}
