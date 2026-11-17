using System;
using System.Linq;
using SPTC.SupportDrawings;

// REV22.63: representative TEST5 8A3/8A4 paper-space geometry.
// Source dimensions and positions are taken from real model extents;
// the code under test must always derive measurements dynamically.
internal static class Rev263AdaptiveLocalDimensionTests
{
    private static void Need(bool condition,string message)
    {
        if(!condition)throw new Exception("REV22.63: "+message);
    }

    internal static void Run()
    {
        var oldLanes=SupportOrthoReferenceLayout51
            .LocalMemberLaneOffsets().ToArray();
        var newLanes=SupportOrthoReferenceLayout51
            .AdaptiveLocalMemberLaneOffsets().ToArray();
        Need(oldLanes.Length>0&&newLanes.Length>=6&&
            newLanes.All(v=>double.IsFinite(v)&&v>0)&&
            Math.Abs(newLanes[0]-
                SupportOrthoReferenceLayout51.ChainOffset())<1e-8,
            "adaptive paper lanes are positive, bounded and retain the old chain start");
        Need(SupportOrthoReferenceLayout51
            .AdaptiveLocalLabelAdvance(0)==0 &&
            SupportOrthoReferenceLayout51
                .AdaptiveLocalLabelAdvance(18)>
            SupportOrthoReferenceLayout51
                .AdaptiveLocalLabelAdvance(6) &&
            SupportOrthoReferenceLayout51
                .AdaptiveLocalLabelAdvance(19)<13,
            "extra text offsets are bounded and increase only for recovery");

        var members=new []{
            new SupportOrthoAllViewDimPlan44.Member(
                "NATIVE:8A3","STRUCTURE",
                7760.82,714.341,1703.985,
                7810.82,814.341,2206.773),
            new SupportOrthoAllViewDimPlan44.Member(
                "NATIVE:8A4","STRUCTURE",
                7760.82,250.201,1703.985,
                7810.82,1178.481,1803.985)
        };
        var cameras=new []{
            new SupportOrthoMemberCalloutPlan17.Camera(
                "LEFT",118.8,588.7,86.376,42.234,
                -1,0,0,-714.341,1955.379,0,1.0/15),
            new SupportOrthoMemberCalloutPlan17.Camera(
                "RIGHT",338.58,588.7,86.376,42.234,
                1,0,0,714.341,1955.379,0,1.0/15),
            new SupportOrthoMemberCalloutPlan17.Camera(
                "FRONT",118.8,454.14,34,42.234,
                0,-1,0,7785.82,1955.379,0,1.0/15),
            new SupportOrthoMemberCalloutPlan17.Camera(
                "BACK",338.58,454.14,31,42.234,
                0,1,0,-7785.82,1955.379,0,1.0/15)
        };
        var titles=NativeOrthoViewTitlePlan.BuildTitles(
            cameras.Select(c=>new NativeOrthoViewTitlePlan.Frame(
                c.Name,c.X,c.Y,c.W,c.H)).ToArray(),
            594,841,allowSubset:true);
        Need(titles.Count==4&&
            titles.All(t=>t.Width>=
                NativeOrthoViewTitlePlan.MinimumFrameWidth(t.Name)),
            "actual titles are still fully protected by their minimum measured width");
        var result=SupportOrthoReferenceDimPlan47.Build(
            cameras,members,594,841,1,titles:titles);
        Need(result.PriorityOverallDimensions==8,
            "all essential H/V overall axes remain protected in four views");
        Need(result.UnintendedAcceptedWitnessIntersections==0,
            "adaptive optional recovery must not introduce witness intersections");
        Need(result.Dimensions.All(
            SupportOrthoDynamicDimension492.MatchesDiagnostic),
            "all measurements and source factors remain derived from geometry");
        Need(result.Dimensions.All(d=>
            !d.Source.Contains("XREF",StringComparison.OrdinalIgnoreCase)),
            "only host-native geometry can produce DIMLINEAR");
        Need(result.AdaptiveLocalWidthDimensionsRecovered>=0&&
            result.AdaptiveLocalWidthDimensionsRecovered<=
                result.LocalMemberDimensions,
            "adaptive recovery must be tracked independently of old passes");
        var xref=members[1] with {Key="XREF_MEMBER:OTHER/8A4"};
        var isolated=SupportOrthoReferenceDimPlan47.Build(cameras,
            members.Append(xref).ToArray(),594,841,1,
            titles:titles);
        Need(isolated.PriorityOverallDimensions==8&&
            isolated.Dimensions.All(d=>
                !d.Source.Contains("XREF",StringComparison.OrdinalIgnoreCase)),
            "adding an XREF cannot change host-native overall count");
        Console.WriteLine(
            "REV22.63 adaptive paper-only local search, TEST5 representative, title clearance, protected overall and intersection safety PASS");
    }
}
