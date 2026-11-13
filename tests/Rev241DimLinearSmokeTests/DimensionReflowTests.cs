using System;
using System.Collections.Generic;
using System.Linq;
using SPTC.SupportDrawings;

/// <summary>
/// Tests the revision-independent source preservation / dimension tier
/// optimization using actual TEST5 member extents (fixtures only).
/// There are NO project-specific dimensions in production logic.
/// </summary>
internal static class DimensionReflowTests
{
    private static void Need(bool ok,string why)
    {
        if(!ok)throw new Exception("Dimension reflow: "+why);
    }

    internal static void Run()
    {
        var overall=SupportOrthoReferenceLayout51
            .OverallLaneOffsets().ToArray();
        var original=SupportOrthoReferenceDimPlan47
            .PreferLaneRank(overall,0).ToArray();
        var moved=SupportOrthoReferenceDimPlan47
            .PreferLaneRank(overall,3).ToArray();
        Need(original.SequenceEqual(overall)&&
            moved.Length==original.Length&&moved[0]==overall[3]&&
            moved.OrderBy(x=>x).SequenceEqual(
                original.OrderBy(x=>x)),
            "paper lane re-ranking does not create a new dimension size");

        var cameras=new[]{
            new SupportOrthoMemberCalloutPlan17.Camera(
                "LEFT",118.8,588.7,86.376,42.234,
                -1,0,0,-714.341,1955.379,0,1.0/15),
            new SupportOrthoMemberCalloutPlan17.Camera(
                "RIGHT",338.58,588.7,86.376,42.234,
                1,0,0,714.341,1955.379,0,1.0/15)
        };
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
        var titles=NativeOrthoViewTitlePlan.BuildTitles(
            cameras.Select(x=>new NativeOrthoViewTitlePlan.Frame(
                x.Name,x.X,x.Y,x.W,x.H)).ToArray(),
            594,841,allowSubset:true);
        var truePlan=SupportOrthoReferenceDimPlan47.Build(
            cameras,members,594,841,1,titles:titles);
        Need(truePlan.PriorityOverallDimensions==4&&
            truePlan.UnintendedAcceptedWitnessIntersections==0&&
            truePlan.Dimensions.All(
                SupportOrthoDynamicDimension492.MatchesDiagnostic),
            "four essential source-grounded overall axes remain valid");
        var baseline=truePlan with {
            Skipped=new[]{
                "LEFT/NATIVE:8A4:V_LOCAL_MEMBER_WIDTH_NO_CLEAR_LANE"
            }
        };
        var extra=truePlan.Dimensions[0] with {
            Source="DETAIL:LEFT/V/MEMBER_SOURCE_AUTHENTIC_TEST"
        };
        var enhanced=baseline with {
            Skipped=Array.Empty<string>(),
            Dimensions=baseline.Dimensions.Append(extra).ToArray(),
            LocalMemberDimensions=baseline.LocalMemberDimensions+1
        };
        SupportOrthoReferenceDimPlan47.Result Success(
            IReadOnlyDictionary<string,
                SupportOrthoReferenceDimPlan47.PlacementPreference> selected) =>
            selected.ContainsKey("LEFT")?enhanced:baseline;
        var accepted=SupportOrthoDimensionReflow.Improve(
            cameras,baseline,Success,maxAttemptsPerView:4);
        Need(accepted.AlternativesTested>0&&
            accepted.ChangedViews==1&&
            accepted.OriginalOptionalOmissions==1&&
            accepted.RemainingOptionalOmissions==0&&
            accepted.Plan.Dimensions.Count==
                baseline.Dimensions.Count+1,
            "safe additional local dimension selected by dynamic reflow");

        var badGeometry=enhanced with {
            Dimensions=enhanced.Dimensions.Select((d,i)=>
                i==0?d with {Ax=d.Ax+1}:d).ToArray()
        };
        SupportOrthoReferenceDimPlan47.Result InvalidSource(
            IReadOnlyDictionary<string,
                SupportOrthoReferenceDimPlan47.PlacementPreference> selected) =>
            selected.ContainsKey("LEFT")?badGeometry:baseline;
        var refused=SupportOrthoDimensionReflow.Improve(
            cameras,baseline,InvalidSource,maxAttemptsPerView:4);
        Need(refused.ChangedViews==0&&
            refused.RemainingOptionalOmissions==1&&
            refused.Rejections.Any(x=>
                x.Contains("SOURCE_OR_MEASUREMENT_CHANGED",
                    StringComparison.Ordinal)),
            "a changed projected model witness is always rejected");

        var lost=enhanced with {
            Dimensions=enhanced.Dimensions.Where(d=>
                d.Source!=baseline.Dimensions[0].Source).ToArray(),
            PriorityOverallDimensions=baseline.PriorityOverallDimensions-1
        };
        var loseOverall=SupportOrthoDimensionReflow.Improve(
            cameras,baseline,_=>lost,maxAttemptsPerView:4);
        Need(loseOverall.ChangedViews==0&&
            loseOverall.Rejections.Any(x=>
                x.Contains("ESSENTIAL_DIMENSION_MISSING",
                    StringComparison.Ordinal)),
            "local recovery cannot sacrifice an overall dimension");

        var complete=truePlan with {Skipped=Array.Empty<string>()};
        var unchanged=SupportOrthoDimensionReflow.Improve(
            cameras,complete,_=>throw new Exception(
                "complete layout must not be replanned"));
        Need(unchanged.AlternativesTested==0&&
            unchanged.ChangedViews==0,
            "already completed source-grounded drawings use the fast path");
        Console.WriteLine(
            "Revision-independent dimension reflow, hierarchy, original source witnesses and optional-only improvement PASS");
    }
}
