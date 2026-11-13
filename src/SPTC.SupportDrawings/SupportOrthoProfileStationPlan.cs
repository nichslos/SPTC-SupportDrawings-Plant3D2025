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
        double transverseMin,double transverseMax)
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
        foreach(double fraction in new[]{-.25,.25,-.375,.375})
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
