using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// Maps actual physical media dimensions (mm), not plotter-dependent
/// canonical names. Pure and testable without a running AutoCAD process.
/// Orientation is preserved, with one deliberate 90-degree compensation
/// only if the destination PDF device exposes the same physical sheet
/// in the opposite orientation. Never scales an A1 sheet to A2/A0.
/// </summary>
internal static class SupportProductionPdfMediaMatcher
{
    // Physical PlotPaperSize may differ fractionally due to inch/mm PC3
    // rounding. A maximum edge error of 1 mm is conservative for ISO A1.
    internal const double ToleranceMm=1.0;

    internal sealed record Candidate(
        string Name,double WidthMm,double HeightMm);

    internal sealed record Match(
        string? MediaName,bool Rotate90,double MaxErrorMm)
    {
        internal bool Found=>!string.IsNullOrEmpty(MediaName);
    }

    internal static Match Select(
        double originalWidthMm,double originalHeightMm,
        IEnumerable<Candidate> available,string? previousCanonical=null)
    {
        if(!IsPhysical(originalWidthMm,originalHeightMm))
            return new(null,false,double.PositiveInfinity);
        if(available==null)
            return new(null,false,double.PositiveInfinity);

        var candidates=available
            .Where(x=>x!=null&&!string.IsNullOrWhiteSpace(x.Name)&&
                IsPhysical(x.WidthMm,x.HeightMm))
            .Select(x=>
            {
                double direct=Math.Max(
                    Math.Abs(x.WidthMm-originalWidthMm),
                    Math.Abs(x.HeightMm-originalHeightMm));
                double swapped=Math.Max(
                    Math.Abs(x.WidthMm-originalHeightMm),
                    Math.Abs(x.HeightMm-originalWidthMm));
                bool rotate=swapped<direct;
                return new {x.Name,Error=rotate?swapped:direct,Rotate=rotate};
            })
            .Where(x=>x.Error<=ToleranceMm)
            .OrderBy(x=>x.Rotate) // Never rotate when direct match exists
            .ThenBy(x=>string.Equals(x.Name,previousCanonical,
                StringComparison.Ordinal)?0:1)
            .ThenBy(x=>x.Name.Contains("full_bleed",
                StringComparison.OrdinalIgnoreCase)?0:1)
            .ThenBy(x=>x.Error)
            .ThenBy(x=>x.Name,StringComparer.Ordinal)
            .FirstOrDefault();

        return candidates==null?
            new(null,false,double.PositiveInfinity):
            new(candidates.Name,candidates.Rotate,candidates.Error);
    }

    internal static bool IsPhysical(double width,double height)=>
        double.IsFinite(width)&&double.IsFinite(height)&&
        width>=25&&height>=25&&width<=5000&&height<=5000;
}
