using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// Revision-independent final audit of ACTUALLY selected native paper-space
/// DIMLINEAR line/text/witness geometry. Essential overall and verified chain
/// dimensions are immutable. When the legacy coordinated placement admits
/// a conflict, omit only the least-supported optional native profile detail
/// needed to make the entire remaining view pairwise safe.
///
/// No model coordinate, dimension length, material profile, view number,
/// paper size or project path is encoded in this policy.
/// </summary>
public static class SupportOrthoDimensionCollisionRecovery
{
    public sealed record Entry(
        string View,string Source,bool Essential,int EvidenceRank,
        SupportOrthoDimIntersections58.Shape Geometry);

    public sealed record Result(
        IReadOnlyList<int> RemovedOptionalIndexes,
        IReadOnlyList<string> Diagnostics,
        int OriginalConflicts,int RemainingConflicts);

    private sealed record Pair(int First,int Second,string Cause);

    public static Result Resolve(IReadOnlyList<Entry> selected)
    {
        if(selected==null)throw new ArgumentNullException(nameof(selected));
        if(selected.Any(e=>string.IsNullOrWhiteSpace(e.View)||
            string.IsNullOrWhiteSpace(e.Source)||e.EvidenceRank<0))
            throw new ArgumentException(
                "Invalid source identity or native geometry evidence.");
        if(selected.Select(e=>e.View).Distinct(
            StringComparer.OrdinalIgnoreCase).Take(2).Count()>1)
            throw new ArgumentException(
                "Audit one native Ortho view at a time.");

        var present=new SortedSet<int>(Enumerable.Range(0,selected.Count));
        var initial=Conflicts(selected,present);
        var diagnostics=new List<string>();
        foreach(var pair in initial)
            diagnostics.Add(PairDiagnostic(selected,pair));

        // Deterministic, minimal-change greedy vertex cover restricted to
        // OPTIONAL details. Do not move source witness stations or silently
        // replace any verified measurement. No essential is ever removed.
        var removed=new List<int>();
        while(true)
        {
            var conflicts=Conflicts(selected,present);
            if(conflicts.Count==0)break;
            var candidates=present
                .Where(i=>!selected[i].Essential)
                .Select(i=>new {
                    Index=i,
                    Incident=conflicts.Count(p=>
                        p.First==i||p.Second==i),
                    Evidence=selected[i].EvidenceRank
                })
                .Where(x=>x.Incident>0)
                .OrderByDescending(x=>x.Incident)
                .ThenBy(x=>x.Evidence)
                .ThenBy(x=>x.Index)
                .ToArray();
            if(candidates.Length==0)
            {
                diagnostics.Add("UNRESOLVED_ESSENTIAL_DIMENSION_CONFLICTS="+
                    conflicts.Count.ToString(CultureInfo.InvariantCulture));
                break;
            }
            int victim=candidates[0].Index;
            removed.Add(victim);
            present.Remove(victim);
            diagnostics.Add("OPTIONAL_DETAIL_OMITTED="+
                selected[victim].View+"/"+selected[victim].Source+
                ":PAIRS_RESOLVED="+candidates[0].Incident.ToString(
                    CultureInfo.InvariantCulture));
        }
        int remaining=Conflicts(selected,present).Count;
        diagnostics.Add("POST_AUDIT_REMAINING="+
            remaining.ToString(CultureInfo.InvariantCulture));
        return new Result(removed,diagnostics,initial.Count,remaining);
    }

    private static List<Pair> Conflicts(
        IReadOnlyList<Entry> selected,SortedSet<int> present)
    {
        var pairs=new List<Pair>();
        int[] keys=present.ToArray();
        for(int later=1;later<keys.Length;later++)
        for(int earlier=0;earlier<later;earlier++)
        {
            int a=keys[earlier],b=keys[later];
            string? reason=SupportOrthoDimIntersections58.Conflict(
                selected[b].Geometry,selected[a].Geometry);
            if(reason!=null)pairs.Add(new Pair(a,b,reason));
        }
        return pairs;
    }

    private static string PairDiagnostic(
        IReadOnlyList<Entry> entries,Pair conflict)
    {
        Entry a=entries[conflict.First],b=entries[conflict.Second];
        static string Point(double x,double y)=>
            x.ToString("0.###",CultureInfo.InvariantCulture)+","+
            y.ToString("0.###",CultureInfo.InvariantCulture);
        static string Line(SupportOrthoDimIntersections58.Segment l)=>
            Point(l.Ax,l.Ay)+"->"+Point(l.Bx,l.By);
        return a.View+":CAUSE="+conflict.Cause+
            ":FIRST="+a.Source+":SECOND="+b.Source+
            ":ESSENTIAL="+a.Essential+"/"+b.Essential+
            ":FIRST_BASELINE="+Line(a.Geometry.Baseline)+
            ":SECOND_BASELINE="+Line(b.Geometry.Baseline)+
            ":FIRST_WITNESSES="+Line(a.Geometry.WitnessA)+";"+
                Line(a.Geometry.WitnessB)+
            ":SECOND_WITNESSES="+Line(b.Geometry.WitnessA)+";"+
                Line(b.Geometry.WitnessB);
    }
}
