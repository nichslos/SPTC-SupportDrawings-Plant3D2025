using System;
using System.Collections.Generic;
using System.Linq;
using Camera=SPTC.SupportDrawings.SupportOrthoMemberCalloutPlan17.Camera;
using Member=SPTC.SupportDrawings.SupportOrthoAllViewDimPlan44.Member;
using Rect=SPTC.SupportDrawings.SupportOrthoSmartDimPlan46.Rect;
using Span=SPTC.SupportDrawings.SupportOrthoProjectedAxis.Span;
using Dimension=SPTC.SupportDrawings.SupportOrthoDimLinearPlan41.Dimension;

namespace SPTC.SupportDrawings;

// A level member has zero axial rise. Its underside and top are separate
// physical stations and its position needs an assembly datum in elevation.
internal static class SupportOrthoMemberElevation
{
    internal const string Unplaced=":MEMBER_ELEVATION_UNPLACED_";
    internal const string Offset="BASE_TO_UNDERSIDE_";
    internal const string OffsetProvenance="REFERENCE_PROJECTED_MEMBER_ELEVATION";
    internal const string Section="SECTION_DEPTH_";
    internal sealed record Measure(string Key,Rect Bounds,bool Horizontal,bool Depth,
        double From,double To,double[] First,double[] Last)
    {
        internal string Role=>Depth?Section:Offset;
        internal string Provenance=>Depth?"REFERENCE_PROJECTED_MEMBER_SECTION_DEPTH":
            OffsetProvenance;

        internal IEnumerable<Span> Witnesses()
        {
            // Prefer the shortest extension between the two real transverse
            // stations. Prefer a common station when both section faces reach it.
            bool shared=Depth&&First.Any(a=>Last.Any(b=>Math.Abs(a-b)<1e-7));
            return from a in First from b in Last
                   where !shared||Math.Abs(a-b)<1e-7
                   orderby Math.Abs(a-b)
                   select new Span(From,To,a,b);
        }

        internal bool ShownBy(Dimension d)
        {
            if(d.Horizontal!=Horizontal)return false;
            double a=Horizontal?d.Ax:d.Ay,b=Horizontal?d.Bx:d.By;
            if(Math.Abs(Math.Min(a,b)-From)>=1e-7||Math.Abs(Math.Max(a,b)-To)>=1e-7)return false;
            double wa=Horizontal?d.Ay:d.Ax,wb=Horizontal?d.By:d.Bx;
            if(a>b)(wa,wb)=(wb,wa);
            // A labelled local detail belongs to its own member, even when
            // another channel of the same section lies at the same level.
            foreach(var marker in new[]{"/"+Offset,"/"+Section,"/MEMBER_"})
            {
                int i=d.Source.IndexOf(marker,StringComparison.Ordinal);
                if(i>=0&&!d.Source[(i+marker.Length)..].Equals(Key,StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            if(d.Source.StartsWith("NATIVE:",StringComparison.OrdinalIgnoreCase)&&
                !d.Source.Equals(Key+(Horizontal?"/H":"/V"),StringComparison.OrdinalIgnoreCase))return false;
            if(Depth&&(d.Source.EndsWith("/MEMBER_"+Key,StringComparison.OrdinalIgnoreCase)||
                d.Source.Equals(Key+(Horizontal?"/H":"/V"),StringComparison.OrdinalIgnoreCase)))
            {
                double low=Horizontal?Bounds.B:Bounds.L,high=Horizontal?Bounds.T:Bounds.R;
                if(Math.Abs(wa-wb)<1e-7&&wa>=low-1e-7&&wa<=high+1e-7)return true;
            }
            return Witnesses().Any(w=>w.ShownBy(d,Horizontal));
        }
    }

    internal static IEnumerable<Measure> Find(Camera camera,
        IEnumerable<(Member Member,Rect Bounds)> visible,
        IReadOnlyDictionary<string,SupportOrthoProjectedAxis> axes,double unitsToMm,
        IReadOnlyList<SupportOrthoNativeEdgeGeometry59.Edge>? nativeEdges=null)
    {
        var members=visible.Where(p=>SupportOrthoNativeDimensionPolicy48.AcceptMember(p.Member)).ToArray();
        if(members.Length<2||axes.Count==0)yield break;
        // DIMLINEAR can represent a world elevation on either paper axis
        // if model Z is in the view plane and parallel to that paper axis.
        double normal=Math.Sqrt(camera.DirX*camera.DirX+camera.DirY*camera.DirY+camera.DirZ*camera.DirZ);
        if(normal==0||Math.Abs(camera.DirZ)>normal*1e-10)yield break;
        double sin=Math.Sin(camera.TwistRadians),cos=Math.Cos(camera.TwistRadians);
        if(Math.Min(Math.Abs(sin),Math.Abs(cos))>1e-9)yield break;
        bool h=Math.Abs(sin)>Math.Abs(cos);
        bool upIncreases=h?sin<0:cos>0;
        double Lower(Rect r)=>h?(upIncreases?r.L:r.R):(upIncreases?r.B:r.T);
        double Upper(Rect r)=>h?(upIncreases?r.R:r.L):(upIncreases?r.T:r.B);
        double factor=unitsToMm/camera.Scale;
        double baseZ=members.Min(m=>m.Member.MinZ),topZ=members.Max(m=>m.Member.MaxZ);
        bool Measurable(double units)=>Math.Round(units*unitsToMm,3,MidpointRounding.AwayFromZero)>0;
        var edges=nativeEdges?.Where(SupportOrthoNativeEdgeGeometry59.Valid)
            .ToLookup(e=>e.Key,StringComparer.OrdinalIgnoreCase);
        double[] Face((Member Member,Rect Bounds) m,double station)
        {
            var (member,bounds)=m;
            double low=h?bounds.B:bounds.L,high=h?bounds.T:bounds.R;
            if(edges!=null)
            {
                var actual=new HashSet<double>();
                foreach(var edge in edges[member.Key].Where(e=>e.Kind==member.Kind))
                {
                    var a=SupportOrthoMemberCalloutPlan17.Project(camera,edge.Ax,edge.Ay,edge.Az);
                    var b=SupportOrthoMemberCalloutPlan17.Project(camera,edge.Bx,edge.By,edge.Bz);
                    double? At((double X,double Y) p)
                    {
                        double t=h?p.Y:p.X;
                        return Math.Abs((h?p.X:p.Y)-station)<1e-7&&t>=low-1e-7&&t<=high+1e-7?t:null;
                    }
                    double? first=At(a),last=At(b);
                    if(first.HasValue)actual.Add(first.Value);
                    if(last.HasValue)actual.Add(last.Value);
                    if(first.HasValue&&last.HasValue)actual.Add((first.Value+last.Value)*.5);
                }
                if(actual.Count>0)return actual.OrderBy(t=>t).ToArray();
            }
            if(axes.TryGetValue(member.Key,out var axis))
            {
                var at=axis.Along(h);
                var onFace=new List<double>();
                if(Math.Abs(at.From-station)<1e-7)onFace.Add(at.FirstWitness);
                if(Math.Abs(at.To-station)<1e-7)onFace.Add(at.LastWitness);
                if(onFace.Count>0)return onFace.Distinct().ToArray();
                // For the section envelope of a level member use its verified
                // axial stations as witness locations along the measured face.
                if(!axis.HasComponent(h,factor)&&axis.HasComponent(!h,factor))
                {
                    var length=axis.Along(!h);
                    return new[]{length.From,length.To,(length.From+length.To)*.5}
                        .Select(x=>Math.Clamp(x,low,high)).Distinct().ToArray();
                }
            }
            return new[]{(low+high)*.5};
        }
        var bases=members.Where(m=>!Measurable(m.Member.MinZ-baseZ)).ToArray();
        double baseStation=Lower(members.OrderBy(m=>m.Member.MinZ).First().Bounds);
        var baseWitness=bases.Where(m=>Math.Abs(Lower(m.Bounds)-baseStation)<1e-7)
            .SelectMany(m=>Face(m,baseStation)).Distinct().ToArray();
        foreach(var m in members.OrderBy(x=>x.Member.Key,StringComparer.OrdinalIgnoreCase))
        {
            if(m.Member.Kind!="STRUCTURE"||!axes.TryGetValue(m.Member.Key,out var axis)||
                axis.HasComponent(h,factor)||!axis.HasComponent(!h,factor)||
                !Measurable(m.Member.MinZ-baseZ)||!Measurable(topZ-m.Member.MaxZ)||
                !Measurable(m.Member.MaxZ-m.Member.MinZ))continue;
            double under=Lower(m.Bounds),top=Upper(m.Bounds);
            var lower=Face(m,under);
            // A level native extrusion has the same run at both Z faces;
            // a grip on either edge still witnesses the opposite face there.
            var upper=Face(m,top);
            Measure At(bool depth,double a,double b,double[] wa,double[] wb)=>
                a<=b?new(m.Member.Key,m.Bounds,h,depth,a,b,wa,wb):
                     new(m.Member.Key,m.Bounds,h,depth,b,a,wb,wa);
            yield return At(false,baseStation,under,baseWitness,lower);
            yield return At(true,under,top,lower,upper);
        }
    }
}
