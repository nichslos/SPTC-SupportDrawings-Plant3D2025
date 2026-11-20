using System;
using System.Collections.Generic;

namespace SPTC.SupportDrawings;

internal static class NativeOrthoDraftOverflow75
{
    // Only the working area grows. Keep the seed's registered media and
    // rotation available to the generator for plotting and diagnostics.
    internal static NativeOrthoEffectivePaper59.Sheet Choose(
        NativeOrthoEffectivePaper59.Sheet physical,NativeOrthoLayout.Plan layout,
        IReadOnlyList<SupportOrthoMemberCalloutPlan27.Member> annotations,
        IReadOnlyList<string> views,int denominator)
    {
        if(denominator<=0||views.Count!=10)
            throw new ArgumentException(
                "One DWG draft overflow requires ten views and an explicit scale.");
        var plan=NativeOrthoTenViewPlan.Build(layout,physical.WidthMm,physical.HeightMm)
            with {Denominator=denominator,Scale=1.0/denominator};
        var canvas=physical;
        for(int attempt=0;;attempt++)
        {
            double labelHeight=SupportOrthoAnnotationCapacity71.RequiredHeight(
                annotations,canvas.WidthMm,canvas.HeightMm);
            try
            {
                NativeOrthoExactScalePlacement61.Build(plan,views,canvas,
                    mandatoryAnnotationHeightMm:labelHeight);
                return canvas;
            }
            catch(NativeOrthoExactScalePlacement61.DoesNotFitException ex)
            {
                // Grid widths/heights give the minimum expansion in one step.
                // Additional clearance can be needed for titles and callouts.
                double factor=Math.Max(1.15,ex.RequiredExpansion);
                double width=canvas.WidthMm*factor,height=canvas.HeightMm*factor;
                if(attempt>=20||!double.IsFinite(width)||!double.IsFinite(height))
                    throw new InvalidOperationException(
                        "Unable to place ten views and their annotations in one draft at 1:"+
                        denominator+". "+ex.Message,ex);
                canvas=canvas with {WidthMm=width,HeightMm=height};
            }
        }
    }
}
