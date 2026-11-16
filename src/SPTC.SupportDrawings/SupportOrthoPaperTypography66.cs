using System;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV66: larger, correctly plotted native A0 (or larger) Ortho paper
/// can support wider external callout lanes and more legible DBText.
/// Never changes the REV59 A1 automatic-scale typography, viewports,
/// source geometry, OrthoCube, native PDF publisher or paper scale.
/// </summary>
internal static class SupportOrthoPaperTypography66
{
    internal const double LargeSheetMinWidthMm=1000;
    internal const double LargeSheetMinHeightMm=700;
    internal const double A0LabelWidthCapMm=160;
    internal const double A0TextHeightMm=2.15;
    internal const double A0LineAdvanceMm=3.5;
    internal const double A0RowSpacingMm=2.2;
    internal const double MinimumA0TextMm=1.5;
    internal const double A0CaptionExtraWidthMm=14;

    internal static bool IsLargePaper(double widthMm,double heightMm) =>
        double.IsFinite(widthMm)&&double.IsFinite(heightMm)&&
        widthMm>=LargeSheetMinWidthMm&&
        heightMm>=LargeSheetMinHeightMm;

    internal static double WidthCap(double widthMm,double heightMm)=>
        IsLargePaper(widthMm,heightMm)?A0LabelWidthCapMm:114;

    internal static double LineAdvance(double widthMm,double heightMm)=>
        IsLargePaper(widthMm,heightMm)?A0LineAdvanceMm:
            SupportOrthoMemberCalloutPlan27.TextLineHeightMm;

    internal static double RowSpacing(double widthMm,double heightMm)=>
        IsLargePaper(widthMm,heightMm)?A0RowSpacingMm:
            SupportOrthoMemberCalloutPlan27.RowSpacingMm;

    internal static double TextHeight(double widthMm,double heightMm)=>
        IsLargePaper(widthMm,heightMm)?A0TextHeightMm:
            SupportOrthoMemberCalloutPlan27.TextHeightMm;

    internal static double MinimumRenderedText(double widthMm,double heightMm)=>
        IsLargePaper(widthMm,heightMm)?MinimumA0TextMm:1.05;

    internal static double CaptionWidth(string name,double frameWidth,
        double paperWidth,double paperHeight)
    {
        if(!double.IsFinite(frameWidth)||frameWidth<=0)
            throw new ArgumentException("Invalid native view caption frame.");
        // Widen *only the external caption band*, not the native view
        // frame or model projection. Existing BuildTitles border, title
        // and viewport-intersection tests validate this expanded width.
        return IsLargePaper(paperWidth,paperHeight)?
            Math.Max(frameWidth,
                NativeOrthoViewTitlePlan.MinimumFrameWidth(name)+
                    A0CaptionExtraWidthMm):
            frameWidth;
    }

    internal static int ExtraDimTrials(double widthMm,double heightMm)=>
        IsLargePaper(widthMm,heightMm)?20:14;
}
