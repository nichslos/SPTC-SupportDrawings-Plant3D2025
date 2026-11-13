using System;
using System.Collections.Generic;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.51: dimension placement convention observed in the manually
/// adjusted six-view support PDF (2026-11-12). This describes placement
/// ONLY. No dimension lengths, fabrication data or 50/100 mm values
/// are ever prescribed here.
/// </summary>
public static class SupportOrthoReferenceLayout51
{
    private static readonly HashSet<string> Orthographic=new(
        new[]{"TOP","BOTTOM","LEFT","RIGHT","FRONT","BACK"},
        StringComparer.OrdinalIgnoreCase);

    public static bool UsesReferenceStyle(string? view) =>
        !string.IsNullOrWhiteSpace(view)&&Orthographic.Contains(view);

    // User's manual PDF: horizontal dimensions above structure.
    // Vertical dimensions are to the left, preserving the right-hand
    // column exclusively for BOP, TOS and member callouts.
    public static double PreferredSide(string view,bool horizontal)
    {
        if(!UsesReferenceStyle(view))
            throw new ArgumentException(
                "REV22.51 reference placement requires a native orthographic view.");
        return horizontal?1.0:-1.0;
    }

    // In paper mm, rather than hardcoded model mm. Computed from the
    // 2 mm reference DIMSTYLE text height plus safe line/text clearance.
    public static double ChainOffset(double textHeight=2.0) =>
        Math.Max(3.4,textHeight*1.7);
    public static double OverallSeparation(double textHeight=2.0) =>
        Math.Max(4.2,textHeight*2.1);

    public static IEnumerable<double> LaneOffsets(double textHeight=2.0)
    {
        double first=ChainOffset(textHeight);
        double step=OverallSeparation(textHeight);
        for(int i=0;i<16;i++)
            yield return first+i*step;
    }

    // REV22.54: the overall is one lane OUTSIDE the preferred local
    // chain (3.4 + 4.2 mm with the current paper DIMSTYLE), not two
    // empty lanes beyond it. This matters in the shared inter-column
    // gutter: the LEFT view's text may occupy the far-left edge of the
    // RIGHT view's overall dimension band.
    // Search a few safe intermediate offsets without altering model
    // lengths. A minimum paper clearance from the inner chain is kept.
    public static IEnumerable<double> OverallLaneOffsets(
        double textHeight=2.0)
    {
        double chain=ChainOffset(textHeight);
        double separation=OverallSeparation(textHeight);
        yield return chain+separation;
        yield return chain+separation*.80;
        yield return chain+separation*.65;
        foreach(double lane in LaneOffsets(textHeight))
            if(lane>chain+separation+.01)
                yield return lane;
    }

    // REV22.58: a long vertical intermediate dimension must fit
    // BETWEEN its outer overall line and the nearby native-member
    // witness at the section edge. The original 3.4mm lane only
    // offered insufficient text clearance at 1:15, while the next
    // 7.6mm lane coincided with the OUTER overall. Reserve several
    // measured intermediate paper lanes (not model-size constants).
    public static IEnumerable<double> IntermediateMemberLaneOffsets(
        double textHeight=2.0)
    {
        double inner=ChainOffset(textHeight);
        yield return inner+textHeight*.60;
        yield return inner+textHeight*.80;
        yield return inner+textHeight;
        yield return inner;
        foreach(double lane in LaneOffsets(textHeight))
            if(lane>inner+textHeight+.1)
                yield return lane;
    }

    // A locally measured member profile width sits INSIDE its
    // overall line. Paper-space section labels are placed on the
    // member-facing side, so reserve enough room between the
    // projected member silhouette and the text (rather than pushing
    // the whole local dimension beyond the overall dimension).
    // These are paper mm based on text height, never fixed member
    // model widths.
    public static IEnumerable<double> LocalMemberLaneOffsets(
        double textHeight=2.0)
    {
        double first=Math.Max(ChainOffset(textHeight)+1.7,
            textHeight*2.55);
        yield return first;
        foreach(double lane in LaneOffsets(textHeight))
            if(Math.Abs(lane-first)>.18)
                yield return lane;
    }

    // REV22.62: supplementary PAPER millimetre lanes for optional
    // member widths when the old local lanes coincide with overall
    // witness/leader geometry. The legacy lanes are ALWAYS tried
    // first; these compact lanes never change model dimensions.
    public static IEnumerable<double> CompactLocalMemberLaneOffsets(
        double textHeight=2.0)
    {
        double first=ChainOffset(textHeight);
        double gap=OverallSeparation(textHeight);
        yield return first;
        for(int i=1;i<=6;i++)
            yield return first+gap*i/16.0;
    }

    // REV22.63 optional recovery: explore closer paper-space offsets,
    // without changing model endpoints or the protected overall lane.
    // The planner still rejects candidates outside the overall hierarchy.
    public static IEnumerable<double> AdaptiveLocalMemberLaneOffsets(
        double textHeight=2.0)
    {
        double first=ChainOffset(textHeight);
        yield return first;
        yield return Math.Max(textHeight*.85,first-textHeight*.55);
        yield return Math.Max(textHeight*.65,first-textHeight);
        yield return first+textHeight*.30;
        yield return first+textHeight*.65;
        yield return first+textHeight*1.0;
    }

    // Move a SHORT optional dimension label along its existing
    // dimension line only; this NEVER modifies its witness points,
    // DIMLFAC, measurement or source member. Earlier compact tiers
    // are retained first and additional bounded offsets tried later.
    public static double AdaptiveLocalLabelAdvance(int tier,
        double textHeight=2.0)
    {
        if(tier<0)throw new ArgumentOutOfRangeException(nameof(tier));
        int pair=tier/2;
        return pair<4?pair*textHeight*.375:
            textHeight*(1.125+(pair-3)*.9);
    }

    // REV22.62: the FIRST chain lane is unchanged, so verified
    // successful drawings retain their placement. Where the only
    // first lane collides with a view leader, try intermediate lanes
    // inside the protected outer overall before omitting the chain.
    public static IEnumerable<double> LocalChainLaneOffsets(
        double textHeight=2.0)
    {
        double first=ChainOffset(textHeight);
        double gap=OverallSeparation(textHeight);
        yield return first;
        for(int i=1;i<=6;i++)
            yield return first+gap*i/9.0;
        foreach(double lane in LaneOffsets(textHeight))
            if(lane>first+.1)
                yield return lane;
    }

    // Text may be outside both witnesses for a short dimension. Prefer
    // RIGHT of a short horizontal segment, as in TOP/BOTTOM/FRONT/BACK.
    // For a tightly packed chain, alternate sides if collision checks
    // reject the initial location. No label or model length is inserted.
    public static bool ShortTextBefore(int index,int attempt) =>
        attempt>0&&(index+attempt)%2==1;
}
