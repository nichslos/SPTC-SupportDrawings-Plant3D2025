using System;
using System.Collections.Generic;
using System.Linq;
using SPTC.SupportDrawings;

/// <summary>
/// REV22.65: reproduce ACTUAL TEST5 8A3/8A4 LEFT-view annotation
/// rows observed in the 2026-11-13 01:27 diagnostic TXT.
/// A legitimate 1.8 mm inter-row gap is NOT a collision, whereas a
/// genuine overlapping text box must still block every paper trial.
/// Candidate decisions are simulated; this is not a substitute for
/// validating a freshly generated drawing in Plant 3D.
/// </summary>
internal static class Rev265LabelClearanceTests
{
    private static void Need(bool value,string reason)
    {
        if(!value)throw new Exception("REV22.65: "+reason);
    }

    internal static void Run()
    {
        var cameras=new[]{
            new SupportOrthoMemberCalloutPlan17.Camera(
                "LEFT",118.8,588.7,86.376,42.234,
                -1,0,0,-714.341,1955.379,0,1.0/15),
            new SupportOrthoMemberCalloutPlan17.Camera(
                "RIGHT",338.58,588.7,86.376,42.234,
                1,0,0,714.341,1955.379,0,1.0/15)
        };
        var titles=NativeOrthoViewTitlePlan.BuildTitles(
            cameras.Select(c=>new NativeOrthoViewTitlePlan.Frame(
                c.Name,c.X,c.Y,c.W,c.H)).ToArray(),
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
        var baseline=SupportOrthoReferenceDimPlan47.Build(
            cameras,members,594,841,1,titles:titles);
        Need(baseline.PriorityOverallDimensions==4&&
            baseline.UnintendedAcceptedWitnessIntersections==0,
            "both native orthographic views retain their four overall axes");
        var blocked=baseline with {Skipped=new[]{
            "LEFT/NATIVE:8A4:V_LOCAL_MEMBER_WIDTH_NO_CLEAR_LANE"}};
        var recovered=blocked with {
            Skipped=Array.Empty<string>(),
            Dimensions=baseline.Dimensions.Append(
                baseline.Dimensions[0] with {
                    Source="DETAIL:LEFT/V/MEMBER_REV265_TEST_ONLY"
                }).ToArray(),
            LocalMemberDimensions=baseline.LocalMemberDimensions+1
        };
        const double labelX=179.488;
        const double width=110.904;
        var notes=new[]{
            new SupportOrthoMemberCalloutPlan27.Callout(
                "LEFT","8A3","[8A3] Sloping channel 100x50",
                false,146.409,588.7,163.98775904429115,
                labelX,604.45,width,2.7),
            new SupportOrthoMemberCalloutPlan27.Callout(
                "LEFT","8AA","100A-TEST-001\nBOP EL. +1803.9855 mm",
                true,133.796,582.417,164.68775904429114,
                labelX,599.95,width,5.4,IsPipe:true),
            new SupportOrthoMemberCalloutPlan27.Callout(
                "LEFT","8AB","100A-TEST-002\nBOP EL. +1803.9855 mm",
                true,114.348,582.417,165.38775904429116,
                labelX,592.75,width,5.4,IsPipe:true),
            new SupportOrthoMemberCalloutPlan27.Callout(
                "LEFT","8AC","100A-TEST-003\nBOP EL. +1803.9855 mm",
                true,96.64,582.417,166.08775904429115,
                labelX,585.55,width,5.4,IsPipe:true),
            new SupportOrthoMemberCalloutPlan27.Callout(
                "LEFT","8A4","[8A4] Sloping channel 100x50\nTOS ~ +1803.9855 mm",
                true,115.467,575.274,166.78775904429116,
                labelX,578.35,width,5.4)
        };
        Need(Math.Abs((notes[0].LabelTop-notes[0].LabelHeight)-
            notes[1].LabelTop-1.8)<1e-6,
            "TEST5 native 1.8 mm reserved row gap is represented");
        SupportOrthoReferenceDimPlan47.Result Replan(
            IReadOnlyList<SupportOrthoMemberCalloutPlan27.Callout> proposed)=>
            proposed[0].LaneX>notes[0].LaneX+2.0?
                recovered:blocked;
        var coordinated=SupportOrthoLeaderDimensionLayout64.Coordinate(
            cameras,titles,notes,594,841,Replan);
        Need(coordinated.ProposedLayouts>0&&coordinated.Attempts>0,
            "normal TEST5 1.8 mm row spacing must allow actual dimension replanning");
        Need(coordinated.AddedDimensions==1&&
            coordinated.RemainingLocalOmissions==0&&
            coordinated.ShiftedViews==1,
            "a safe shifted leader stack can be accepted when it recovers a local detail");
        Need(coordinated.Callouts.Select(n=>n.Handle)
            .SequenceEqual(notes.Select(n=>n.Handle))&&
            coordinated.Callouts.Zip(notes,(a,b)=>
                a.AnchorX==b.AnchorX&&a.AnchorY==b.AnchorY&&
                a.Text==b.Text).All(x=>x),
            "member IDs, anchors, TOS/BOP annotations are never modified");
        Need(!(coordinated.LayoutRejections??Array.Empty<string>())
            .Any(r=>r.Contains("LABEL_TO_LABEL_OVERLAP",
                StringComparison.Ordinal)),
            "1.8 mm separate rows must not be classified as overlapping");

        // A REAL text collision remains disallowed: the lower row's
        // top meets the previous row's bottom, with no clearance.
        var overlapping=notes.ToArray();
        overlapping[1]=overlapping[1] with {LabelTop=601.75};
        var unsafeLayout=SupportOrthoLeaderDimensionLayout64.Coordinate(
            cameras,titles,overlapping,594,841,Replan);
        Need(unsafeLayout.ProposedLayouts>0&&unsafeLayout.Attempts==0&&
            unsafeLayout.AddedDimensions==0&&
            unsafeLayout.ShiftedViews==0,
            "genuine overlapping labels must reject all coordinated trials");
        Need((unsafeLayout.LayoutRejections??Array.Empty<string>())
            .Any(r=>r.Contains("LEFT:LABEL_TO_LABEL_OVERLAP=",
                StringComparison.Ordinal)),
            "preflight must diagnose the precise rejected view and label collision");

        Console.WriteLine(
            "REV22.65 real TEST5 label-row gap accepted; genuine collision rejected; conservative dimensions/source protection PASS");
    }
}
