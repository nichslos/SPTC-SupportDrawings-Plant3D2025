using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV70: explicit 1:N All10 fallback for EVERY physically valid
/// project-registered EMPTY native Ortho sheet, irrespective of paper
/// name, size, aspect ratio or page orientation. The existing registered
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

    // The caller has already verified the seed is an EMPTY native
    // Ortho drawing registered in the active Plant 3D project.
    // This pure gate validates the ACTUAL oriented media dimensions
    // using the same physical bounds as the REV59 sheet validator.
    // Do NOT whitelist A0/A1/A2/custom names, landscape aspect ratio,
    // or arbitrary minimum 1100x800 mm: physical fit is checked later.
    internal static bool Eligible(double paperW,double paperH,
        int denominator,int count)
    {
        if(denominator<=0||count!=10)return false;
        try
        {
            NativeOrthoEffectivePaper59.FromMedia(paperW,paperH,0);
            return true;
        }
        catch(ArgumentException)
        {
            return false;
        }
    }

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
