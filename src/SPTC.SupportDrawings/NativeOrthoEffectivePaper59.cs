using System;
using System.Collections.Generic;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV59: Native Ortho page frames are specified in paper-layout XY,
/// which must agree with the effective ORIENTED sheet, not just the
/// PC3 media dimensions. In TEST5 the layout medium was 594x841,
/// but the seed page was 270-degree rotated and the actual border
/// was landscape 841x594. The old 594x841 planner put two view rows
/// above the border. This pure policy also checks frame keep-outs.
/// </summary>
internal static class NativeOrthoEffectivePaper59
{
    internal const double LeftMarginRatio=0.03;
    internal const double RightTitleBlockStartRatio=0.75;
    internal const double BottomMarginRatio=0.12;
    internal const double TopMarginRatio=0.94;

    internal sealed record Sheet(
        double MediaWidthMm,double MediaHeightMm,int RotationDegrees,
        double WidthMm,double HeightMm)
    {
        internal bool Transposed=>RotationDegrees==90||RotationDegrees==270;
    }

    internal sealed record ViewFrame(
        string Name,double X,double Y,double Width,double Height);

    internal sealed record CaptionFrame(
        string Name,double X,double TopY,double Width,double Height);

    internal static Sheet FromMedia(double mediaWidthMm,
        double mediaHeightMm,int rotationDegrees)
    {
        if(!double.IsFinite(mediaWidthMm)||
           !double.IsFinite(mediaHeightMm)||
           mediaWidthMm<25||mediaHeightMm<25||
           mediaWidthMm>5000||mediaHeightMm>5000)
            throw new ArgumentException("Invalid physical native Ortho media.");
        if(rotationDegrees!=0&&rotationDegrees!=90&&
           rotationDegrees!=180&&rotationDegrees!=270)
            throw new ArgumentOutOfRangeException(nameof(rotationDegrees));
        bool transposed=rotationDegrees==90||rotationDegrees==270;
        return new Sheet(mediaWidthMm,mediaHeightMm,rotationDegrees,
            transposed?mediaHeightMm:mediaWidthMm,
            transposed?mediaWidthMm:mediaHeightMm);
    }

    // Existing title / dimension planners reserve at least 12% at
    // bottom and 6% at top. Use a more conservative right keep-out
    // for the actual side title block shown in the native TEST5 DWG.
    // This is a geometric containment gate, NOT an assumption that
    // every seed has exactly the same physical title-block polygon.
    internal static void RequireInsideUsableArea(Sheet sheet,
        IReadOnlyList<ViewFrame> views,
        IReadOnlyList<CaptionFrame>? captions=null,
        bool requireTen=false)
    {
        if(views==null||views.Count==0||
           (requireTen&&views.Count!=10))
            throw new InvalidOperationException(
                "REV59 missing native viewport frames to validate.");
        double left=sheet.WidthMm*LeftMarginRatio;
        double right=sheet.WidthMm*RightTitleBlockStartRatio;
        double bottom=sheet.HeightMm*BottomMarginRatio;
        double top=sheet.HeightMm*TopMarginRatio;
        const double tol=0.001;
        foreach(var frame in views)
        {
            if(string.IsNullOrWhiteSpace(frame.Name)||
               !double.IsFinite(frame.X)||!double.IsFinite(frame.Y)||
               !double.IsFinite(frame.Width)||!double.IsFinite(frame.Height)||
               frame.Width<=0||frame.Height<=0||
               frame.X-frame.Width/2<left-tol||
               frame.X+frame.Width/2>right+tol||
               frame.Y-frame.Height/2<bottom-tol||
               frame.Y+frame.Height/2>top+tol)
                throw new InvalidOperationException(
                    "REV59 native Ortho "+frame.Name+
                    " lies outside the oriented sheet's usable border/title-block keep-out.");
        }
        if(captions!=null)
            foreach(var title in captions)
            {
                if(string.IsNullOrWhiteSpace(title.Name)||
                   !double.IsFinite(title.X)||
                   !double.IsFinite(title.TopY)||
                   !double.IsFinite(title.Width)||
                   !double.IsFinite(title.Height)||
                   title.Width<=0||title.Height<=0||
                   title.X-title.Width/2<left-tol||
                   title.X+title.Width/2>right+tol||
                   title.TopY>top+tol||
                   title.TopY-title.Height<bottom-tol)
                    throw new InvalidOperationException(
                        "REV59 Ortho caption "+title.Name+
                        " lies outside the oriented sheet's usable area.");
            }
    }
}
