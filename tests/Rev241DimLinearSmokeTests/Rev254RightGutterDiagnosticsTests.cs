using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev254RightGutterDiagnosticsTests
{
    private static void Need(bool ok,string message)
    {
        if(!ok)throw new Exception("REV22.54: "+message);
    }

    internal static void Run()
    {
        // Reproduce the real TEST5 A1, 1:15 inter-column geometry.
        // The prior REV22.53 test gave RIGHT all the space in the
        // world, omitting the LEFT view's printed steel/pipe labels.
        // These coordinates are isolated test fixtures, never numeric
        // values in the production dimension-selection code.
        var right=new SupportOrthoMemberCalloutPlan17.Camera(
            "RIGHT",338.58,588.7,86.376,42.234,
            1,0,0,714.341,1955.379,0,1.0/15);
        var left=right with {
            Name="LEFT",X=118.8,DirX=-1,ViewCenterX=-714.341
        };
        var horizontalBeam=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:8A4","STRUCTURE",
            7760.82,250.201,1703.985,
            7810.82,1178.481,1803.985);
        var upright=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:8A3","STRUCTURE",
            7760.82,250.201,1703.985,
            7810.82,350.201,2206.772);

        // Actual LEFT annotation gutter ends just 5mm before the
        // RIGHT viewport starts. RIGHT vertical overall cannot simply
        // be moved 11.8mm left of the native member; its text would
        // overlap these LEFT labels in the adjacent paper column.
        double leftFrameRight=left.X+left.W*.5;
        double rightFrameLeft=right.X-right.W*.5;
        double leftLabelX=leftFrameRight+
            SupportOrthoMemberCalloutPlan27.LabelGapFromFrameMm;
        double leftLabelWidth=rightFrameLeft-5-leftLabelX;
        var annotations=new[]{
            new SupportOrthoMemberCalloutPlan27.Callout(
                "LEFT","8A4","Sloping channel 100x50 web-5 fl-7.5",
                true,120,588,166,leftLabelX,592,
                leftLabelWidth,10)
        };
        var withNeighbor=SupportOrthoReferenceDimPlan47.Build(
            new[]{left,right},new[]{horizontalBeam,upright},
            594,841,1,callouts:annotations);

        var rightOverall=withNeighbor.Dimensions
            .Where(d=>d.View=="RIGHT"&&!d.Horizontal&&
                d.Source.EndsWith("/OVERALL"))
            .ToArray();
        Need(rightOverall.Length==1,
            "RIGHT vertical overall survives realistic LEFT label gutter");
        Need(rightOverall[0].Measurement=="502.787",
            "RIGHT vertical overall uses the native member's Z extent");
        Need(rightOverall[0].LineX<rightOverall[0].Ax&&
            rightOverall[0].LineX<rightOverall[0].Bx,
            "RIGHT overall remains outside-left of steel section");
        Need(rightOverall[0].PaperTextX.HasValue &&
            rightOverall[0].PaperTextX.Value>
                leftLabelX+leftLabelWidth+1.0,
            "RIGHT overall dimension text does not enter LEFT labels");
        Need(withNeighbor.Skipped.Any(s=>s.StartsWith(
            "RIGHT/V:REV22_54_OVERALL_DIAGNOSTICS=PLACED")),
            "diagnostics report verified successful RIGHT overall");
        Need(!withNeighbor.Skipped.Any(s=>
            s=="RIGHT/V:OVERALL_NO_CLEAR_DIMENSION_LANE"),
            "real neighbor layout no longer triggers omitted overall");
        Need(withNeighbor.Dimensions.All(
            SupportOrthoDynamicDimension492.MatchesDiagnostic),
            "all plotted native overall/chain/width values remain dynamic");

        // A truly occupied paper corridor must still suppress a
        // dimension instead of violating the non-overlap requirement.
        var occupied=SupportOrthoReferenceDimPlan47.Build(
            new[]{right},new[]{horizontalBeam,upright},
            594,841,1,additionalKeepouts:new[]{
                new SupportOrthoSmartDimPlan46.Rect(
                    180,565,307,612)
            });
        Need(!occupied.Dimensions.Any(d=>
            d.View=="RIGHT"&&!d.Horizontal&&
            d.Source.EndsWith("/OVERALL")),
            "a fully blocked left-side corridor cannot force an overall");
        Need(occupied.Skipped.Any(s=>s.Contains(
            "RIGHT/V:REV22_54_OVERALL_DIAGNOSTICS=UNPLACED")&&
            s.Contains("RESERVED_PAPER_AREA")),
            "failed RIGHT overall reports the actual paper obstacle type");
        Need(occupied.Skipped.Contains(
            "RIGHT/V:OVERALL_NO_CLEAR_DIMENSION_LANE"),
            "legacy warning is retained for downstream log checks");

        var allowed=SupportOrthoReferenceLayout51
            .OverallLaneOffsets().Take(4).ToArray();
        Need(allowed[0]>SupportOrthoReferenceLayout51
            .ChainOffset()&&
            allowed[0]<SupportOrthoReferenceLayout51
                .ChainOffset()+
                SupportOrthoReferenceLayout51.OverallSeparation()*2,
            "overall first lane is outside inner chain but not too far");

        Console.WriteLine(
            "REV22.54 real LEFT-label/RIGHT 502.787 gutter, obstacle diagnostics and fail-closed no-overlap PASS");
    }
}
