using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.60: select compatible native profile-width dimensions for
/// the WHOLE orthographic view rather than greedily placing H before V.
/// OUTER overall + verified member-end chains are reserved by the caller.
/// This bounded beam search selects AT MOST one candidate for each
/// distinct native member/axis group, and may omit an optional detail
/// instead of colliding with an essential dimension or annotation.
/// </summary>
public static class SupportOrthoCoordinatedLayout60
{
    public sealed record Alternative(int Id,double PaperCost);
    public sealed record Group(string Key,int EvidencePriority,
        IReadOnlyList<Alternative> Alternatives);
    public sealed record Chosen(string Key,int AlternativeId);
    public sealed record Result(
        IReadOnlyList<Chosen> Chosen,
        IReadOnlyList<string> Omitted,
        int CandidateComparisons,int ExploredStates,
        double TotalCost);

    private sealed record State(
        IReadOnlyList<Chosen> Selected,
        IReadOnlyList<string> Omitted,
        double Score,double Cost);

    public static Result Solve(
        IReadOnlyList<Group> groups,
        Func<int,int,bool> compatible,
        int maxStates=64,int maxChoicesPerGroup=32)
    {
        if(groups==null||compatible==null||
           maxStates<1||maxStates>512||
           maxChoicesPerGroup<1||maxChoicesPerGroup>128)
            throw new ArgumentException(
                "REV22.60 invalid bounded layout optimization inputs.");
        if(groups.Count>24)
            throw new ArgumentException(
                "REV22.60 too many optional native profile dimensions.");
        if(groups.Any(g=>string.IsNullOrWhiteSpace(g.Key)||
            g.Alternatives==null||g.EvidencePriority<0||
            g.EvidencePriority>100||
            g.Alternatives.Any(a=>a.Id<0||
                !double.IsFinite(a.PaperCost)||a.PaperCost<0||
                a.PaperCost>100)))
            throw new ArgumentException(
                "REV22.60 invalid candidate group or nonfinite cost.");
        if(groups.Select(g=>g.Key).Distinct(
               StringComparer.OrdinalIgnoreCase).Count()!=groups.Count||
           groups.SelectMany(g=>g.Alternatives.Select(a=>a.Id))
               .Distinct().Count()!=
           groups.Sum(g=>g.Alternatives.Count))
            throw new ArgumentException(
                "REV22.60 duplicate group key or alternative identity.");

        // Prefer fitting two nonoverlapping optional details to just
        // one, even if the single option has a smaller paper offset.
        // The secondary evidence priority favors actual native edge
        // witnesses over envelope-only approximations, then paper
        // placement with less movement and text displacement.
        int compared=0,states=0;
        var beam=new List<State>{
            new(Array.Empty<Chosen>(),Array.Empty<string>(),0,0)
        };
        foreach(var group in groups.OrderByDescending(g=>g.EvidencePriority)
            .ThenBy(g=>g.Key,StringComparer.OrdinalIgnoreCase))
        {
            var next=new List<State>();
            var candidates=group.Alternatives
                .OrderBy(a=>a.PaperCost).ThenBy(a=>a.Id)
                .Take(maxChoicesPerGroup).ToArray();
            foreach(var state in beam)
            {
                next.Add(state with {
                    Omitted=state.Omitted.Append(group.Key).ToArray()
                });
                foreach(var alternative in candidates)
                {
                    bool fit=true;
                    foreach(var previous in state.Selected)
                    {
                        compared++;
                        // Compatibility is symmetric: if either
                        // evaluation order finds an intersection,
                        // the complete arrangement is disallowed.
                        if(!compatible(previous.AlternativeId,
                               alternative.Id)||
                           !compatible(alternative.Id,
                               previous.AlternativeId))
                        {
                            fit=false;break;
                        }
                    }
                    if(!fit)continue;
                    double cost=state.Cost+alternative.PaperCost;
                    next.Add(new State(
                        state.Selected.Append(
                            new Chosen(group.Key,alternative.Id)).ToArray(),
                        state.Omitted,
                        state.Score+1000+group.EvidencePriority-
                            alternative.PaperCost,cost));
                }
            }
            states+=next.Count;
            beam=next.OrderByDescending(s=>s.Selected.Count)
                .ThenByDescending(s=>s.Score)
                .ThenBy(s=>s.Cost)
                .ThenBy(s=>string.Join("|",s.Selected.Select(x=>x.Key+":"+x.AlternativeId)),
                    StringComparer.Ordinal)
                .Take(maxStates).ToList();
        }
        var best=beam.OrderByDescending(s=>s.Selected.Count)
            .ThenByDescending(s=>s.Score)
            .ThenBy(s=>s.Cost)
            .First();
        return new Result(best.Selected,best.Omitted,
            compared,states,best.Cost);
    }
}
