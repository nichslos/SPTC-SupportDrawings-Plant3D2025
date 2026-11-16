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
    internal const double LegacyRightRatio=.75;
    internal const double LegacyBottomRatio=.12;
    internal const double LegacyTopRatio=.94;

    internal static bool Eligible(double paperW,double paperH,
        int denominator,int count) =>
        denominator>0&&count==10&&
        double.IsFinite(paperW)&&double.IsFinite(paperH)&&
        paperW>=1100&&paperH>=800;

    internal static double Right(double paperW,bool bypass) =>
        paperW*(bypass?RightRatio:LegacyRightRatio);
    internal static double Bottom(double paperH,bool bypass) =>
        paperH*(bypass?BottomRatio:LegacyBottomRatio);
    internal static double Top(double paperH,bool bypass) =>
        paperH*(bypass?TopRatio:LegacyTopRatio);
    internal static bool EntersOldTitleBlockZone(
        IEnumerable<double> rightmostEdgesMm,double paperW)
    {
        if(rightmostEdgesMm==null)
            throw new ArgumentNullException(nameof(rightmostEdgesMm));
        double oldLimit=paperW*LegacyRightRatio;
        return rightmostEdgesMm.Any(edge=>
            double.IsFinite(edge)&&edge>oldLimit);
    }
}
