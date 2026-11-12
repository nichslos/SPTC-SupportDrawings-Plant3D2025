using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.58: inspect the geometry AutoCAD will actually draw for
/// a native DIMLINEAR: dimension baseline, BOTH extension/witness
/// lines, and reserved text area. A check against only the dimension
/// baseline misses crossings visible in the user's REV22.57 PDF.
/// Only shared physical dimension datum stations may touch/continue
/// another dimension line; an interior crossing must move or be
/// omitted rather than be drawn incorrectly.
/// All coordinates are in PAPER millimetres, not model dimensions.
/// </summary>
public static class SupportOrthoDimIntersections58
{
    public readonly record struct Segment(
        double Ax,double Ay,double Bx,double By)
    {
        public bool Horizontal => Math.Abs(Ay-By)<1e-7;
        public bool Vertical => Math.Abs(Ax-Bx)<1e-7;

        public SupportOrthoSmartDimPlan46.Rect Bounds(
            double inflation=.12) =>
            new(Math.Min(Ax,Bx)-inflation,
                Math.Min(Ay,By)-inflation,
                Math.Max(Ax,Bx)+inflation,
                Math.Max(Ay,By)+inflation);
    }

    public readonly record struct Shape(
        Segment Baseline,Segment WitnessA,Segment WitnessB,
        SupportOrthoSmartDimPlan46.Rect Text)
    {
        public IEnumerable<Segment> Witnesses
        {
            get { yield return WitnessA;yield return WitnessB; }
        }
    }

    public static Shape Project(
        SupportOrthoDimLinearPlan41.Dimension dimension,
        SupportOrthoSmartDimPlan46.Rect text)
    {
        if(dimension.Horizontal)
            return new Shape(
                new Segment(dimension.Ax,dimension.LineY,
                    dimension.Bx,dimension.LineY),
                new Segment(dimension.Ax,dimension.Ay,
                    dimension.Ax,dimension.LineY),
                new Segment(dimension.Bx,dimension.By,
                    dimension.Bx,dimension.LineY),text);
        return new Shape(
            new Segment(dimension.LineX,dimension.Ay,
                dimension.LineX,dimension.By),
            new Segment(dimension.Ax,dimension.Ay,
                dimension.LineX,dimension.Ay),
            new Segment(dimension.Bx,dimension.By,
                dimension.LineX,dimension.By),text);
    }

    /// <summary>Null when clear; otherwise a precise rejection cause.
    /// A witness passing through the INTERIOR of another dimension
    /// baseline is not a valid shared drafting datum.</summary>
    public static string? Conflict(Shape proposed,Shape earlier)
    {
        foreach(var line in proposed.Witnesses)
            if(earlier.Text.Intersects(line.Bounds(),.10))
                return "WITNESS_CROSSES_PRIOR_DIMENSION_TEXT";
        foreach(var line in earlier.Witnesses)
            if(proposed.Text.Intersects(line.Bounds(),.10))
                return "TEXT_CROSSES_PRIOR_DIMENSION_WITNESS";

        foreach(var line in proposed.Witnesses)
            if(CrossesBaselineInterior(earlier.Baseline,line))
                return "WITNESS_CROSSES_DIMENSION_LINE_INTERIOR";
        foreach(var line in earlier.Witnesses)
            if(CrossesBaselineInterior(proposed.Baseline,line))
                return "DIMENSION_LINE_CROSSES_PRIOR_WITNESS_INTERIOR";

        foreach(var a in proposed.Witnesses)
        foreach(var b in earlier.Witnesses)
            if(PerpendicularInteriorCrossing(a,b))
                return "WITNESS_WITNESS_INTERSECTION";
        return null;
    }

    // Shared datum intersections at the START or END of a dimension
    // baseline are conventional and remain permitted, including a
    // collinear extension continued to the outer overall dimension.
    // A witness at an unrelated interior station is rejected.
    public static bool CrossesBaselineInterior(
        Segment baseline,Segment witness,double tolerance=.16)
    {
        if(baseline.Horizontal&&witness.Vertical)
        {
            double x=witness.Ax,y=baseline.Ay;
            return Between(x,baseline.Ax,baseline.Bx,tolerance)&&
                   BetweenInclusive(y,witness.Ay,witness.By,tolerance);
        }
        if(baseline.Vertical&&witness.Horizontal)
        {
            double y=witness.Ay,x=baseline.Ax;
            return Between(y,baseline.Ay,baseline.By,tolerance)&&
                   BetweenInclusive(x,witness.Ax,witness.Bx,tolerance);
        }
        return false;
    }

    public static bool PerpendicularInteriorCrossing(
        Segment first,Segment second,double tolerance=.16)
    {
        if(first.Horizontal&&second.Vertical)
            return Between(second.Ax,first.Ax,first.Bx,tolerance)&&
                Between(first.Ay,second.Ay,second.By,tolerance);
        if(first.Vertical&&second.Horizontal)
            return Between(first.Ax,second.Ax,second.Bx,tolerance)&&
                Between(second.Ay,first.Ay,first.By,tolerance);
        return false;
    }

    private static bool Between(
        double value,double a,double b,double tolerance) =>
        value>Math.Min(a,b)+tolerance&&
        value<Math.Max(a,b)-tolerance;

    private static bool BetweenInclusive(
        double value,double a,double b,double tolerance) =>
        value>=Math.Min(a,b)-tolerance&&
        value<=Math.Max(a,b)+tolerance;
}
