using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// Revision-independent, source-preserving paper-space lane optimizer.
/// Rebuilds the WHOLE native dimension plan when optional member widths
/// are missing or an outer-overall/inner-chain order is inverted.
/// No member size, measurement, view identity or project coordinate is
/// encoded here; each trial uses native source geometry via the caller.
/// </summary>
public static class SupportOrthoDimensionReflow
{
    public sealed record Result(
        SupportOrthoReferenceDimPlan47.Result Plan,
        int AlternativesTested,
        int OriginalOptionalOmissions,int RemainingOptionalOmissions,
        int OriginalHierarchyErrors,int RemainingHierarchyErrors,
        int ChangedViews,
        IReadOnlyList<string> Rejections,
        IReadOnlyDictionary<string,
            SupportOrthoReferenceDimPlan47.PlacementPreference> Preferences);

    private static readonly HashSet<string> Views=new(
        new[]{"TOP","BOTTOM","LEFT","RIGHT","FRONT","BACK"},
        StringComparer.OrdinalIgnoreCase);

    public static Result Improve(
        IReadOnlyList<SupportOrthoMemberCalloutPlan17.Camera> cameras,
        SupportOrthoReferenceDimPlan47.Result baseline,
        Func<IReadOnlyDictionary<string,
            SupportOrthoReferenceDimPlan47.PlacementPreference>,
            SupportOrthoReferenceDimPlan47.Result> rebuild,
        int maxAttemptsPerView=12)
    {
        if(cameras==null||baseline==null||rebuild==null||
           maxAttemptsPerView<1||maxAttemptsPerView>48)
            throw new ArgumentException("Invalid native dimension reflow inputs.");

        var preferences=new Dictionary<string,
            SupportOrthoReferenceDimPlan47.PlacementPreference>(
                StringComparer.OrdinalIgnoreCase);
        var current=baseline;
        int tried=0,changed=0;
        int originalMissing=Missing(baseline);
        int originalHierarchy=HierarchyErrors(baseline);
        var rejections=new SortedDictionary<string,int>(
            StringComparer.Ordinal);
        void Reject(string view,string cause)
        {
            var key=view+":"+cause;
            rejections[key]=rejections.TryGetValue(key,out int n)?n+1:1;
        }

        // A complete, correctly tiered drawing is not re-planned.
        if(originalMissing==0&&originalHierarchy==0)
            return new Result(current,0,0,0,0,0,0,
                Array.Empty<string>(),preferences);

        var overallRanks=SupportOrthoReferenceLayout51
            .OverallLaneOffsets().ToArray();
        var chainRanks=SupportOrthoReferenceLayout51
            .LocalChainLaneOffsets().ToArray();
        // Reorder existing paper-only lanes; never provide a model
        // length, handle, view coordinate, nominal member section
        // or fabricated dimension. Prioritize wider outer-overall
        // offsets, then progressively varying inner-chain lanes.
        var trials=Enumerable.Range(1,
                Math.Min(overallRanks.Length-1,9))
            .SelectMany(o=>Enumerable.Range(0,
                Math.Min(chainRanks.Length,3))
                .Select(c=>new SupportOrthoReferenceDimPlan47
                    .PlacementPreference(o,c)))
            .OrderBy(p=>p.ChainLaneRank)
            .ThenByDescending(p=>overallRanks[p.OverallLaneRank])
            .ToArray();

        foreach(var camera in cameras)
        {
            string view=camera.Name;
            if(!Views.Contains(view))continue;
            bool issue=current.Skipped.Any(s=>
                s.StartsWith(view+"/",StringComparison.OrdinalIgnoreCase)&&
                IsLocalOmission(s))||
                HierarchyErrors(current,view)>0;
            if(!issue)continue;
            int attemptsForView=0;
            SupportOrthoReferenceDimPlan47.Result best=current;
            SupportOrthoReferenceDimPlan47.PlacementPreference? bestPref=null;
            foreach(var pref in trials)
            {
                if(attemptsForView++>=maxAttemptsPerView)break;
                var proposed=new Dictionary<string,
                    SupportOrthoReferenceDimPlan47.PlacementPreference>(
                        preferences,StringComparer.OrdinalIgnoreCase)
                    {[view]=pref};
                tried++;
                var candidate=rebuild(proposed);
                if(candidate.PriorityOverallDimensions<
                        baseline.PriorityOverallDimensions||
                   candidate.OverallDimensions<baseline.OverallDimensions||
                   candidate.ChainDimensions<baseline.ChainDimensions||
                   candidate.TerminalMemberRemainderDimensions<
                        baseline.TerminalMemberRemainderDimensions)
                {
                    Reject(view,"ESSENTIAL_DIMENSION_MISSING");
                    continue;
                }
                if(candidate.UnintendedAcceptedWitnessIntersections!=0)
                {
                    Reject(view,"WITNESS_INTERSECTION");
                    continue;
                }
                if(!PreservesSourceGeometry(baseline,candidate))
                {
                    Reject(view,"SOURCE_OR_MEASUREMENT_CHANGED");
                    continue;
                }
                if(!candidate.Dimensions.All(
                    SupportOrthoDynamicDimension492.MatchesDiagnostic))
                {
                    Reject(view,"DYNAMIC_MEASUREMENT_MISMATCH");
                    continue;
                }
                int missing=Missing(candidate);
                int hierarchy=HierarchyErrors(candidate);
                int bestMissing=Missing(best);
                int bestHierarchy=HierarchyErrors(best);
                // No loss of accepted local detail or drafting order
                // is permitted to obtain a different profile width.
                if(missing>bestMissing||hierarchy>bestHierarchy)
                {
                    Reject(view,"COMPLETENESS_OR_HIERARCHY_REGRESSION");
                    continue;
                }
                if(missing==bestMissing&&hierarchy==bestHierarchy)
                {
                    Reject(view,"NO_VERIFIED_IMPROVEMENT");
                    continue;
                }
                best=candidate;
                bestPref=pref;
                // No later move can improve a zero-omission,
                // fully correct drafting hierarchy.
                if(missing==0&&hierarchy==0)break;
            }
            if(bestPref==null)continue;
            current=best;
            preferences[view]=bestPref;
            changed++;
        }

        return new Result(current,tried,originalMissing,
            Missing(current),originalHierarchy,HierarchyErrors(current),
            changed,rejections.Select(x=>x.Key+"="+x.Value).ToArray(),
            preferences);
    }

    private static bool IsLocalOmission(string text) =>
        text.Contains("_LOCAL_MEMBER_WIDTH_NO_CLEAR_LANE",
            StringComparison.Ordinal)||
        text.Contains("OPTIONAL_PROFILE_NO_COMPATIBLE_LAYOUT",
            StringComparison.Ordinal);

    private static int Missing(SupportOrthoReferenceDimPlan47.Result result) =>
        result.Skipped.Count(IsLocalOmission);

    // The chain must be closer to the actual support than its
    // overall dimension. Use the actual plotted paper-space
    // DIMLINEAR coordinates, not hard-coded source lengths.
    public static int HierarchyErrors(
        SupportOrthoReferenceDimPlan47.Result plan,string? view=null)
    {
        int count=0;
        foreach(var overall in plan.Dimensions.Where(d=>
            d.Source.EndsWith("/OVERALL",StringComparison.Ordinal)&&
            (view==null||d.View.Equals(view,
                StringComparison.OrdinalIgnoreCase))))
        {
            double outer=overall.Horizontal?overall.LineY:overall.LineX;
            foreach(var chain in plan.Dimensions.Where(d=>
                d.View==overall.View&&d.Horizontal==overall.Horizontal&&
                (d.Source.Contains("/CHAIN_",StringComparison.Ordinal)||
                 d.Source.Contains("/TERMINAL_REMAINDER_",
                    StringComparison.Ordinal))))
            {
                double inner=chain.Horizontal?chain.LineY:chain.LineX;
                if(!SupportOrthoManualDetailStyle57.InsideOverall(
                    overall.Horizontal,inner,outer))count++;
            }
        }
        return count;
    }

    // Accepted source measurements and true 3D witness projections are
    // immutable. The line and text paper positions may be changed
    // by this explicitly requested drafting optimization.
    private static bool PreservesSourceGeometry(
        SupportOrthoReferenceDimPlan47.Result original,
        SupportOrthoReferenceDimPlan47.Result candidate)
    {
        var next=candidate.Dimensions.GroupBy(d=>
            d.View+"|"+d.Source+"|"+d.Horizontal,
            StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g=>g.Key,g=>g.ToArray(),
                StringComparer.OrdinalIgnoreCase);
        foreach(var old in original.Dimensions)
        {
            string key=old.View+"|"+old.Source+"|"+old.Horizontal;
            if(!next.TryGetValue(key,out var matches)||
                matches.Length!=1)return false;
            var d=matches[0];
            if(d.Measurement!=old.Measurement||
               d.Provenance!=old.Provenance||
               !Close(d.PaperToModelMm,old.PaperToModelMm)||
               !Close(d.Ax,old.Ax)||!Close(d.Ay,old.Ay)||
               !Close(d.Bx,old.Bx)||!Close(d.By,old.By))
                return false;
        }
        return true;
    }

    private static bool Close(double? a,double? b) =>
        !a.HasValue&&!b.HasValue||
        a.HasValue&&b.HasValue&&Close(a.Value,b.Value);
    private static bool Close(double a,double b) =>
        double.IsFinite(a)&&double.IsFinite(b)&&
        Math.Abs(a-b)<1e-6;
}
