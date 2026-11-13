using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev264LeaderDimensionCoordinationTests
{
    private static void Need(bool pass,string message)
    {
        if(!pass)throw new Exception("REV22.64: "+message);
    }

    internal static void Run()
    {
        var cameras=new[]{
            new SupportOrthoMemberCalloutPlan17.Camera(
                "LEFT",118.8,588.7,86.376,42.234,
                -1,0,0,-714.341,1955.379,0,1.0/15)
        };
        var titles=NativeOrthoViewTitlePlan.BuildTitles(
            new[]{new NativeOrthoViewTitlePlan.Frame(
                "LEFT",118.8,588.7,86.376,42.234)},
            594,841,allowSubset:true);
        var members=new[]{
            new SupportOrthoAllViewDimPlan44.Member(
                "NATIVE:8A3","STRUCTURE",
                7760.82,714.341,1703.985,
                7810.82,814.341,2206.773),
            new SupportOrthoAllViewDimPlan44.Member(
                "NATIVE:8A4","STRUCTURE",
                7760.82,250.201,1703.985,
                7810.82,1178.481,1803.985)
        };
        var actual=SupportOrthoReferenceDimPlan47.Build(
            cameras,members,594,841,1,titles:titles);
        Need(actual.PriorityOverallDimensions==2,
            "single LEFT native view protects both H/V overall dimensions");
        Need(actual.UnintendedAcceptedWitnessIntersections==0,
            "baseline must be verified as collision-free");
        var original=new SupportOrthoMemberCalloutPlan27.Callout(
            "LEFT","8A4","[8A4] Sloping channel 100x50",
            false,120,588.7,164,180,598,70,2.7);
        // Isolate a real failure mode: a valid native local measurement
        // cannot fit beside an annotation leader at the original lane.
        // The callback represents the exact geometry planner; the
        // unit test focuses on the conservative joint decision policy.
        var blocked=actual with {Skipped=new[]{
            "LEFT/NATIVE:8A4:V_LOCAL_MEMBER_WIDTH_NO_CLEAR_LANE"}};
        var extra=actual.Dimensions[0] with {
            Source="DETAIL:LEFT/V/MEMBER_TEST_RECOVERY"
        };
        var clear=actual with {
            Skipped=Array.Empty<string>(),
            Dimensions=actual.Dimensions.Append(extra).ToArray(),
            LocalMemberDimensions=actual.LocalMemberDimensions+1
        };
        SupportOrthoReferenceDimPlan47.Result Evaluate(
            System.Collections.Generic.IReadOnlyList<
                SupportOrthoMemberCalloutPlan27.Callout> notes)=>
            notes[0].LaneX>original.LaneX+2.0 ? clear : blocked;
        var selected=SupportOrthoLeaderDimensionLayout64.Coordinate(
            cameras,titles,new[]{original},594,841,Evaluate);
        Need(selected.Attempts>0&&selected.ShiftedViews==1&&
            selected.AddedDimensions==1&&
            selected.OriginalLocalOmissions==1&&
            selected.RemainingLocalOmissions==0,
            "a relocated external leader must be selected ONLY when it adds a local detail");
        Need(selected.Callouts.Single().Handle==original.Handle&&
            selected.Callouts.Single().AnchorX==original.AnchorX&&
            selected.Callouts.Single().AnchorY==original.AnchorY&&
            selected.Callouts.Single().Text==original.Text,
            "member identity, model anchor and annotation content remain immutable");

        // The count alone is not a sufficient reason to move a label.
        // A proposed dimension which shifts a previously accepted
        // overall, even by 1 mm on paper, MUST be rejected.
        var changed=clear with {Dimensions=clear.Dimensions.Select(
            (d,index)=>index==0?d with {LineX=d.LineX+1.0}:d)
            .ToArray()};
        var refused=SupportOrthoLeaderDimensionLayout64.Coordinate(
            cameras,titles,new[]{original},594,841,
            notes=>notes[0].LaneX>original.LaneX+2.0?changed:blocked);
        Need(refused.ShiftedViews==0&&refused.AddedDimensions==0&&
            refused.Callouts.Single()==original,
            "an otherwise useful local candidate may not move any approved dimension");
        var complete=SupportOrthoLeaderDimensionLayout64.Coordinate(
            cameras,titles,new[]{original},594,841,_=>actual);
        Need(complete.Attempts==0&&complete.ShiftedViews==0&&
            complete.Callouts.Single()==original,
            "already complete drawings must not be relaid out");
        Console.WriteLine(
            "REV22.64 annotation/dimension coordination, source immutability, protected overall, and no-change fast path PASS");
    }
}
