using System;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV75. Draft-only virtual paper canvas for native 10-view layouts.
/// This expands PLANNING space, never changes the actual registered seed,
/// its plot configuration or the original view scale. Every resulting
/// viewport/caption still passes the existing virtual non-overlap guards.
/// Actual physical media is reported as NOT PRINTABLE if it is exceeded.
/// </summary>
internal static class NativeOrthoDraftOverflow75
{
    internal sealed record Canvas(double WidthMm,double HeightMm,
        int Enlargements,bool ExceedsActualMedia);

    internal static Canvas Choose(double physicalWidthMm,
        double physicalHeightMm,Func<double,double,bool> allTenFit)
    {
        if(!double.IsFinite(physicalWidthMm)||
           !double.IsFinite(physicalHeightMm)||
           physicalWidthMm<=0||physicalHeightMm<=0)
            throw new ArgumentOutOfRangeException(nameof(physicalWidthMm));
        if(allTenFit==null)throw new ArgumentNullException(nameof(allTenFit));
        double width=physicalWidthMm,height=physicalHeightMm;
        // 5000 mm is the existing maximum supported by
        // NativeOrthoEffectivePaper59.FromMedia, not a project dimension.
        const double maxPlanningMm=5000.0;
        for(int step=0;step<=20;step++)
        {
            if(width>maxPlanningMm||height>maxPlanningMm)
                break;
            if(allTenFit(width,height))
                return new Canvas(width,height,step,step>0);
            // Dimensionless multiplicative expansion depends ONLY on
            // the caller's actual paper and geometry-fit predicate.
            width*=1.15;
            height*=1.15;
        }
        throw new InvalidOperationException(
            "REV75 cannot form a collision-free ten-view virtual draft "+
            "within the native paper planner's supported numeric range.");
    }
}
