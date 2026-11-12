using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev258WitnessIntersectionsTests
{
    private static void Need(bool ok,string why)
    {
        if(!ok)throw new Exception("REV22.58: "+why);
    }

    private static SupportOrthoDimIntersections58.Segment L(
        double ax,double ay,double bx,double by) =>
        new(ax,ay,bx,by);

    private static SupportOrthoSmartDimPlan46.Rect R(
        double left,double bottom,double right,double top) =>
        new(left,bottom,right,top);

    private static SupportOrthoDimIntersections58.Shape Shape(
        SupportOrthoDimIntersections58.Segment line,
        SupportOrthoDimIntersections58.Segment first,
        SupportOrthoDimIntersections58.Segment second,
        SupportOrthoSmartDimPlan46.Rect text) =>
        new(line,first,second,text);

    internal static void Run()
    {
        // User REV22.57 PDF: overall and chain can SHARE their genuine
        // outer native station. Their extension lines may be collinear,
        // but there is NO cross through the interior of a dimension.
        var overall=Shape(L(0,12,62,12),
            L(0,0,0,12),L(62,0,62,12),
            R(27,14,35,16));
        var chain=Shape(L(0,6,54,6),
            L(0,0,0,6),L(54,0,54,6),
            R(22,7.8,32,9.8));
        Need(SupportOrthoDimIntersections58
            .Conflict(chain,overall)==null,
            "shared end datum remains valid with inner chain/outer overall");

        // The old code failed here: a local member width 100mm
        // could place its extension at x=25 straight THROUGH an
        // unrelated overall dimension baseline at y=12.
        var bad=Shape(L(25,17,32,17),
            L(25,0,25,17),L(32,0,32,17),
            R(38,19,46,21));
        Need(SupportOrthoDimIntersections58
            .Conflict(bad,overall)==
                "WITNESS_CROSSES_DIMENSION_LINE_INTERIOR",
            "unrelated local extension cannot cross outer overall baseline");
        Need(SupportOrthoDimIntersections58
            .CrossesBaselineInterior(overall.Baseline,bad.WitnessA),
            "detect intersection at interior rather than shared datum");

        // Moving only the paper-space dimension lane CLOSER to its
        // actual native member clears the witness path. Dimensions
        // still measure the same projected model endpoints.
        var placedInside=Shape(L(25,5,32,5),
            L(25,0,25,5),L(32,0,32,5),
            R(36,6,44,8));
        Need(SupportOrthoDimIntersections58
            .Conflict(placedInside,overall)==null,
            "inside lane can place 100mm member detail without crossing overall");

        // An apparently clear baseline is insufficient: prior witness
        // runs right through the label printed on a neighboring dim.
        var textCrossesWitness=Shape(L(12,5,20,5),
            L(12,0,12,5),L(20,0,20,5),
            R(-1,5,1,7));
        Need(SupportOrthoDimIntersections58
            .Conflict(textCrossesWitness,overall)==
                "TEXT_CROSSES_PRIOR_DIMENSION_WITNESS",
            "dimension text must not overprint an earlier extension line");

        var earlierText=Shape(L(0,12,62,12),
            L(0,0,0,12),L(62,0,62,12),
            R(24,7,28,10));
        var witnessCrossesText=Shape(L(26,15,32,15),
            L(26,0,26,15),L(32,0,32,15),
            R(37,17,44,19));
        Need(SupportOrthoDimIntersections58
            .Conflict(witnessCrossesText,earlierText)==
                "WITNESS_CROSSES_PRIOR_DIMENSION_TEXT",
            "extension lines cannot run through an already placed dimension label");

        var crossingWitness=Shape(L(10,0,10,35),
            L(-5,10,10,10),L(-5,30,10,30),
            R(12,16,15,23));
        var normalWitness=Shape(L(-2,20,45,20),
            L(0,0,0,20),L(45,0,45,20),
            R(20,22,28,24));
        Need(SupportOrthoDimIntersections58
            .Conflict(crossingWitness,normalWitness)==
                "WITNESS_WITNESS_INTERSECTION",
            "independent vertical/horizontal extension lines cannot cross");

        // Verify real four-view source dimensions retain both overall
        // axes, native-only profile detail, and measured numeric values.
        var members=new[]{
            new SupportOrthoAllViewDimPlan44.Member(
                "NATIVE:8A4","STRUCTURE",
                7760.82,250.201,1703.985,
                7810.82,1178.481,1803.985),
            new SupportOrthoAllViewDimPlan44.Member(
                "NATIVE:8A3","STRUCTURE",
                7760.82,250.201,1703.985,
                7810.82,350.201,2206.772)
        };
        var views=new[]{
            new SupportOrthoMemberCalloutPlan17.Camera(
                "TOP",118.8,723.26,28,86.376,
                0,0,1,7785.82,714.341,0,1.0/15),
            new SupportOrthoMemberCalloutPlan17.Camera(
                "BOTTOM",338.58,723.26,36,86.376,
                0,0,-1,7785.82,-714.341,Math.PI,1.0/15),
            new SupportOrthoMemberCalloutPlan17.Camera(
                "LEFT",118.8,588.7,86.376,42.234,
                -1,0,0,-714.341,1955.379,0,1.0/15),
            new SupportOrthoMemberCalloutPlan17.Camera(
                "RIGHT",338.58,588.7,86.376,42.234,
                1,0,0,714.341,1955.379,0,1.0/15)
        };
        var result=SupportOrthoReferenceDimPlan47.Build(
            views,members,594,841,1);
        Need(result.UnintendedAcceptedWitnessIntersections==0,
            "all accepted DIMLINEARs are free of unintended witness intersections");
        Need(result.PriorityOverallDimensions==8,
            "overall H/V measurements retained for all four native views");
        Need(result.Dimensions.All(
            SupportOrthoDynamicDimension492.MatchesDiagnostic),
            "geometry-only collision handling never overrides measurements");

        Console.WriteLine(
            "REV22.58 complete witness/line/text intersection model, shared datum and TEST5 four-view verification PASS");
    }
}
