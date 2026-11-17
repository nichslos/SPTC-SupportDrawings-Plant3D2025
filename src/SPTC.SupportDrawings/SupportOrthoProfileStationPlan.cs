using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// Safe supplementary paper-space stations for a projected profile
/// WIDTH when a genuine native member Start/End axis is known.
/// Interpolates ALONG that measured native axis; never invents model
/// dimensions, modifies a source member, or claims an exact BREP edge.
/// Profile widths remain marked projected-envelope approximations
/// unless an authenticated edge is available separately.
/// </summary>
public static class SupportOrthoProfileStationPlan
{
    public static IReadOnlyList<double> AlongVerifiedAxis(
        bool verifiedNativeAxis,
        double midpoint,double longitudinalDelta,
        double transverseMin,double transverseMax,
        bool supplementalA0=false)
    {
        if(!verifiedNativeAxis||
           !new[]{midpoint,longitudinalDelta,transverseMin,
                   transverseMax}.All(double.IsFinite)||
           transverseMax<=transverseMin||
           Math.Abs(longitudinalDelta)<.80)
            return Array.Empty<double>();

        // A small clearance based on member projected span prevents
        // attaching an approximate section width at an end face.
        double span=transverseMax-transverseMin;
        double margin=Math.Max(.35,span*.015);
        var stations=new List<double>();
        // REV68: the previous four interpolated stations remain the
        // first choices. On a true A0 sheet ONLY, optionally sample
        // additional fractions along the same EXPLICIT, straight Plant
        // Start/End axis. These are verified axis locations, not
        // fabricated width endpoints, bolt pitches or added model data.
        // Actual extension lines, dimension text and paper keepouts
        // must still independently pass every REV58 collision check.
        var fractions=new[]{-.25,.25,-.375,.375};
        if(supplementalA0)
            fractions=fractions.Concat(new[]{
                -.10,.10,-.18,.18,-.30,.30,-.42,.42,-.46,.46
            }).ToArray();
        foreach(double fraction in fractions)
        {
            double candidate=midpoint+longitudinalDelta*fraction;
            if(candidate<=transverseMin+margin||
               candidate>=transverseMax-margin||
               Math.Abs(candidate-midpoint)<.35||
               stations.Any(x=>Math.Abs(x-candidate)<.35))
                continue;
            stations.Add(candidate);
        }
        return stations;
    }
}
