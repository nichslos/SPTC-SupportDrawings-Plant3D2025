using System;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV69 opt-in exact-scale single-sheet drawing mode. The legacy A0
/// right-side "title block" region is a heuristic (75%-78%), not a
/// measured polygon. Bypass it ONLY when the operator explicitly
/// checks the palette option. The real printable paper bounds, view
/// overlaps, reserved captions and dimension integrity still apply.
/// Title block entities/text/attributes are NEVER modified.
/// WARNING: content can visually overprint existing title block.
/// </summary>
internal static class NativeOrthoTitleBlockBypass69
{
    internal const double RightBorderRatio=.97;
    internal const double ExactRowGapMm=2.0;

    internal static double RightBoundaryRatio(bool optedIn) =>
        optedIn?RightBorderRatio:
            NativeOrthoEffectivePaper59.RightTitleBlockStartRatio;

    internal static double RowGapMm(bool optedIn) =>
        optedIn?ExactRowGapMm:5.0;

    internal static bool IsActive(bool requested,int scaleDenominator,
        int viewCount) =>
        requested&&scaleDenominator>0&&viewCount==10;

    internal static string Warning(bool active) => active?
        "REV69 WARNING: RIGHT-SIDE TITLE BLOCK RESERVE BYPASSED. "+
        "Real printed title-block content may be obscured. "+
        "Drawing remains DRAFT and must be reviewed visually. "+
        "No title-block entities changed.":
        "REV69 legacy protected title-block area in effect.";
}
