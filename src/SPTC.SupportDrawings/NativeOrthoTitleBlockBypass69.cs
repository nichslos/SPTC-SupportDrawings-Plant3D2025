using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV69: explicit 1:N All10 A0 fallback for models that FAIL the
/// conservative right title-block reservation. The existing registered
/// native seed, static printed title block, page setup and plotter are
/// never edited. Only the allowable PAPER LAYOUT AREA changes.
/// The printed title block MAY be obscured; mark DRAFT and require
/// review. Never allow a native viewport past the physical page margin.
/// </summary>
internal static class NativeOrthoTitleBlockBypass69
{
    internal const double RightRatio=.97;
    internal const double BottomRatio=.10;
    internal const double TopRatio=.94;
    internal const double LeftRatio=.03;

    internal static bool Eligible(double paperW,double paperH,
        int denominator,int count) =>
        denominator>0&&count==10&&
        double.IsFinite(paperW)&&double.IsFinite(paperH)&&
        paperW>=1100&&paperH>=800;

    internal static double Right(double paperW,bool bypass) =>
        paperW*(bypass?RightRatio:
            NativeOrthoEffectivePaper59.RightTitleBlockStartRatio);
    internal static double Bottom(double paperH,bool bypass) =>
        paperH*(bypass?BottomRatio:
            NativeOrthoEffectivePaper59.BottomMarginRatio);
    internal static double Top(double paperH,bool bypass) =>
        paperH*(bypass?TopRatio:
            NativeOrthoEffectivePaper59.TopMarginRatio);
    internal static bool EntersOldTitleBlockZone(
        IReadOnlyList<NativeOrthoTenViewPlan.Placement> placements,
        IReadOnlyList<NativeOrthoViewTitlePlan.Title> captions,
        double paperW)
    {
        double oldLimit=paperW*
            NativeOrthoEffectivePaper59.RightTitleBlockStartRatio;
        return placements.Any(v=>v.CenterX+v.PaperFrameWidth/2>oldLimit)||
            captions.Any(t=>t.CenterX+t.Width/2>oldLimit);
    }
}
