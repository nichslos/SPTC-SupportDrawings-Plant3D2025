using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

// A straight solid can supply two physical end stations when its Plant
// insertion endpoints are missing or the member has been trimmed. A box
// alone cannot establish which of its three directions is the member axis.
internal static class SupportOrthoSolidEndStations
{
    internal readonly record struct Point(double X,double Y,double Z)
    {
        public static Point operator -(Point a,Point b)=>new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        public static Point operator +(Point a,Point b)=>new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
        public static Point operator *(Point p,double scale)=>new(p.X*scale,p.Y*scale,p.Z*scale);
        internal double Dot(Point p)=>X*p.X+Y*p.Y+Z*p.Z;
        internal double Length=>Math.Sqrt(Dot(this));
        internal bool Finite=>double.IsFinite(X)&&double.IsFinite(Y)&&double.IsFinite(Z);
    }

    internal readonly record struct Edge(Point A,Point B,bool Straight);
    internal readonly record struct Pair(Point Start,Point End);

    // A local-space or unrelated direction cannot disambiguate a square
    // solid. Use the box only to reject a declared line in another frame.
    internal static bool CrossesBounds(Pair axis,Point min,Point max,double tolerance)
    {
        if(!axis.Start.Finite||!axis.End.Finite)return false;
        double lo=0,hi=1;
        foreach(var (a,b,left,right) in new[]{
            (axis.Start.X,axis.End.X,min.X,max.X),(axis.Start.Y,axis.End.Y,min.Y,max.Y),
            (axis.Start.Z,axis.End.Z,min.Z,max.Z)})
        {
            double delta=b-a;
            if(Math.Abs(delta)<=tolerance)
            {
                if(a<left-tolerance||a>right+tolerance)return false;
                continue;
            }
            double t0=(left-tolerance-a)/delta,t1=(right+tolerance-a)/delta;
            lo=Math.Max(lo,Math.Min(t0,t1));
            hi=Math.Min(hi,Math.Max(t0,t1));
            if(lo>hi)return false;
        }
        return true;
    }

    // Match actual world-space grip points at two independently measured
    // transverse planes. The span must come from the selected member.
    internal static Pair? EndGrips(IReadOnlyList<Point> grips,Point origin,
        Point unit,double low,double high,double tolerance)
    {
        double Station(Point p)=>(p-origin).Dot(unit);
        var shift=unit*(high-low);
        Pair? match=null;
        foreach(var a in grips.Where(g=>g.Finite&&Math.Abs(Station(g)-low)<=tolerance))
        foreach(var b in grips.Where(g=>g.Finite&&Math.Abs(Station(g)-high)<=tolerance))
        {
            if(((b-a)-shift).Length>tolerance)continue;
            if(match is {} old&&((old.Start-a).Length>tolerance||
                (old.End-b).Length>tolerance))return null;
            match=new(a,b);
        }
        return match;
    }

    internal static Pair? Resolve(IReadOnlyList<Edge> edges,Pair? declared,
        IReadOnlyList<Point> grips,out string reason)
    {
        var pair=ResolveProfiles(edges,declared,grips,out reason);
        if(pair.HasValue||reason=="AMBIGUOUS_SOLID_AXIS")return pair;
        // A BRep seam can split a straight physical edge into multiple records.
        // Reconnect only straight, collinear edges at a degree-two vertex.
        // Junctions, trims, curved edges and disconnected pieces stay distinct.
        var adjacency=new Dictionary<Point,List<int>>();
        for(int i=0;i<edges.Count;i++)
            foreach(var p in new[]{edges[i].A,edges[i].B})
            {
                if(!p.Finite)return null;
                if(!adjacency.TryGetValue(p,out var ids))adjacency[p]=ids=new();
                ids.Add(i);
            }
        var used=new bool[edges.Count];
        var joined=new List<Edge>(edges.Count);
        Point Extend(Point tip,Point previous)
        {
            while(adjacency[tip].Count==2)
            {
                var ids=adjacency[tip];
                int index=used[ids[0]]?ids[1]:ids[0];
                if(used[index]||!edges[index].Straight)break;
                var next=edges[index].A==tip?edges[index].B:edges[index].A;
                var a=tip-previous;var b=next-tip;
                double la=a.Length,lb=b.Length;
                if(la==0||lb==0||(a*(1/la)-b*(1/lb)).Length>1e-9)break;
                used[index]=true;
                previous=tip;
                tip=next;
            }
            return tip;
        }
        for(int i=0;i<edges.Count;i++)
        {
            if(used[i])continue;
            used[i]=true;
            var edge=edges[i];
            joined.Add(edge.Straight?new(Extend(edge.A,edge.B),Extend(edge.B,edge.A),true):edge);
        }
        return joined.Count==edges.Count?null:ResolveProfiles(joined,declared,grips,out reason);
    }

    private static Pair? ResolveProfiles(IReadOnlyList<Edge> edges,Pair? declared,
        IReadOnlyList<Point> grips,out string reason)
    {
        reason="NO_VERIFIED_END_STATIONS";
        if(edges.Count==0||edges.Any(e=>!e.A.Finite||!e.B.Finite))return null;
        var points=edges.SelectMany(e=>new[]{e.A,e.B}).Distinct().ToArray();
        var origin=points[0];
        double scale=points.Max(p=>(p-origin).Length);
        double tol=Math.Max(scale*1e-8,1e-10);
        var directions=new List<Point>();
        var min=new Point(points.Min(p=>p.X),points.Min(p=>p.Y),points.Min(p=>p.Z));
        var max=new Point(points.Max(p=>p.X),points.Max(p=>p.Y),points.Max(p=>p.Z));
        bool guided=declared is {} d&&(d.End-d.Start).Length>tol&&CrossesBounds(d,min,max,tol);
        void AddDirection(Point vector)
        {
            double length=vector.Length;
            if(length<=tol)return;
            var u=vector*(1/length);
            if(!directions.Any(v=>(u-v).Length<1e-7||(u+v).Length<1e-7))
                directions.Add(u);
        }
        if(guided)AddDirection(declared!.Value.End-declared.Value.Start);
        else foreach(var e in edges.Where(e=>e.Straight))AddDirection(e.B-e.A);

        Pair? result=null;
        foreach(var u in directions)
        {
            double Station(Point p)=>(p-origin).Dot(u);
            double low=points.Min(Station),high=points.Max(Station),span=high-low;
            if(span<=tol||points.Any(p=>Math.Min(Math.Abs(Station(p)-low),
                    Math.Abs(Station(p)-high))>tol))continue;
            var shift=u*span;
            var rails=new List<Pair>();
            foreach(var e in edges.Where(e=>e.Straight))
            {
                var pair=Station(e.A)<=Station(e.B)?new Pair(e.A,e.B):new Pair(e.B,e.A);
                if(((pair.End-pair.Start)-shift).Length<=tol&&
                    !rails.Any(r=>(r.Start-pair.Start).Length<=tol))rails.Add(pair);
            }
            // Both entire end profiles must be tied together by actual,
            // parallel longitudinal edges, with a two-dimensional section.
            if(rails.Count<3||points.Any(p=>!rails.Any(r=>
                    (r.Start-p).Length<=tol||(r.End-p).Length<=tol)))continue;
            var baseline=rails.OrderByDescending(r=>(r.Start-rails[0].Start).Length).First().Start-rails[0].Start;
            var v=baseline*(1/baseline.Length);
            double DistanceFromLine(Point p,Point a,Point unit)
            {
                var delta=p-a;
                return (delta-unit*delta.Dot(unit)).Length;
            }
            if(!rails.Any(r=>DistanceFromLine(r.Start,rails[0].Start,v)>tol))continue;
            if(result.HasValue)
            {
                reason="AMBIGUOUS_SOLID_AXIS";
                return null;
            }
            // Object grips can identify the original alignment on both caps.
            // Only a unique, translated pair of native grips qualifies.
            if(EndGrips(grips,origin,u,low,high,tol) is {} pairOfGrips)
            {
                result=pairOfGrips;
                reason="NATIVE_GRIPS_ON_VERIFIED_END_STATIONS";
            }
            else
            {
                // Keep both witnesses on the same real longitudinal edge.
                // No centroid, silhouette diagonal or nominal length is synthesized.
                var anchor=guided?declared!.Value.Start:origin;
                result=rails.OrderBy(r=>DistanceFromLine(r.Start,anchor,u))
                    .ThenBy(r=>r.Start.X).ThenBy(r=>r.Start.Y).ThenBy(r=>r.Start.Z).First();
                reason="NATIVE_SOLID_END_EDGE";
            }
        }
        if(guided&&!result.HasValue)
            return ResolveProfiles(edges,null,grips,out reason);
        return result;
    }
}
