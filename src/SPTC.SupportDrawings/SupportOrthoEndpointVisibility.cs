using System;
using System.Collections.Generic;
using System.Linq;
using Camera=SPTC.SupportDrawings.SupportOrthoMemberCalloutPlan17.Camera;
using Member=SPTC.SupportDrawings.SupportOrthoAllViewDimPlan44.Member;
using Segment=SPTC.SupportDrawings.SupportOrthoDimLinearPlan41.Segment;
using Dimension=SPTC.SupportDrawings.SupportOrthoDimLinearPlan41.Dimension;
using Rect=SPTC.SupportDrawings.SupportOrthoSmartDimPlan46.Rect;
using Axis=SPTC.SupportDrawings.SupportOrthoProjectedAxis;
using Point=(double X,double Y);

namespace SPTC.SupportDrawings;

internal static class SupportOrthoEndpointVisibility
{
    internal const string Locator="PROJECTED_NATIVE_ENDPOINT_LOCATION";
    internal const string Unplaced=":ENDPOINT_LOCATION_UNPLACED_";
    internal sealed record Requirement(string Key,string Ends,string? Datum,bool Horizontal,
        Axis.Span Span,Rect Bounds,bool FullyInLayout=false)
    {
        internal string AxisName=>Horizontal?"H":"V";
        internal string Role=>Datum==null?"AXIS_"+Key:"ENDPOINT_"+Ends+"_"+Key+"_TO_"+Datum;
        internal string Missing=>Datum==null?Key+Axis.UnplacedLength+AxisName:
            Key+Unplaced+AxisName+" ENDS="+Ends+" DATUM="+Datum;
        internal IReadOnlyList<Requirement> AlternateDatums {get;init;}=Array.Empty<Requirement>();
        internal bool Matches(Dimension d)=>Span.ShownBy(d,Horizontal)||
            AlternateDatums.Any(r=>r.Span.ShownBy(d,Horizontal));
        internal bool Identifies(Dimension d)=>Span.ShownBy(d,Horizontal)&&
            d.Source.EndsWith("/"+Role,StringComparison.Ordinal)||
            AlternateDatums.Any(r=>r.Identifies(d));
    }

    internal sealed record Result(List<Requirement> Required,List<Rect> Footprints,
        List<string> Notes,HashSet<string> VisibleMembers,HashSet<string> EndOnMembers);
    private sealed record Ends(string Key,Axis Axis,Rect Bounds,bool First,bool Last);

    internal static Result Inspect(Camera camera,IReadOnlyList<Member> members,
        IReadOnlyList<Segment>? sources,double unitsToMm)
    {
        var result=new Result(new(),new(),new(),new(StringComparer.OrdinalIgnoreCase),new(StringComparer.OrdinalIgnoreCase));
        if(sources==null||sources.Count==0)return result;
        var projected=members.Where(m=>SupportOrthoNativeDimensionPolicy48.AcceptMember(m)&&
            SupportOrthoReferenceDimPlan47.ValidMember(m))
            .Select(m=>(Member:m,Bounds:SupportOrthoReferenceDimPlan47.Project(camera,m))).ToArray();
        var verified=Axis.InView(camera,sources,projected);
        var frame=new Rect(camera.X-camera.W/2,camera.Y-camera.H/2,
            camera.X+camera.W/2,camera.Y+camera.H/2);
        bool Inside(Point p)=>p.X>=frame.L-1e-7&&p.X<=frame.R+1e-7&&
            p.Y>=frame.B-1e-7&&p.Y<=frame.T+1e-7;
        double factor=unitsToMm/camera.Scale;
        var ends=new List<Ends>();
        foreach(var (member,bounds) in projected)
        {
            var clipped=new Rect(Math.Max(bounds.L,frame.L),Math.Max(bounds.B,frame.B),
                Math.Min(bounds.R,frame.R),Math.Min(bounds.T,frame.T));
            if(!clipped.IsFinite)continue;
            result.Footprints.Add(clipped);
            if(member.Kind!="STRUCTURE"||!verified.TryGetValue(member.Key,out var axis))continue;
            bool first=Inside(axis.Start),last=Inside(axis.End);
            if(!first&&!last)continue;
            ends.Add(new(member.Key,axis,clipped,first,last));
            result.VisibleMembers.Add(member.Key);
        }

        var stations=ends.SelectMany(e=>new[]{(e.Key,Point:e.Axis.Start,Visible:e.First),
            (e.Key,Point:e.Axis.End,Visible:e.Last)}).Where(s=>s.Visible)
            .Select(s=>(s.Key,s.Point)).Distinct().ToArray();
        foreach(var e in ends.OrderBy(e=>e.Key,StringComparer.OrdinalIgnoreCase))
        {
            bool h=e.Axis.HasComponent(true,factor),v=e.Axis.HasComponent(false,factor);
            if(e.First&&e.Last&&(h||v))
            {
                // The main planners already coordinate full-envelope lengths with the
                // leaders. Leave those placements to their normal near-member lanes.
                bool fullyInLayout=e.Bounds.L>=frame.L+1.35&&e.Bounds.R<=frame.R-1.35&&
                    e.Bounds.B>=frame.B+1.35&&e.Bounds.T<=frame.T-1.35;
                foreach(bool horizontal in new[]{true,false})
                    if(horizontal?h:v)result.Required.Add(new(e.Key,"BOTH",null,horizontal,
                        e.Axis.Along(horizontal),e.Bounds,fullyInLayout));
                continue;
            }

            if(e.First&&e.Last)result.EndOnMembers.Add(e.Key);
            Point point=e.First?e.Axis.Start:e.Axis.End;
            string label=e.First&&e.Last?"BOTH":e.First?"START":"END";
            if(e.First!=e.Last)result.Notes.Add(e.Key+":ENDPOINT_OUTSIDE_VIEW_"+(e.First?"END":"START"));
            // Both directions use the same native reference when possible.
            // A coincident projection is one location, never a zero-length dimension.
            var choices=stations.Where(s=>s.Key!=e.Key).Select(s=>(s.Key,s.Point,
                Axis:new Axis(point,s.Point))).Select(s=>(s.Key,s.Point,s.Axis,
                    Axes:(s.Axis.HasComponent(true,factor)?1:0)+(s.Axis.HasComponent(false,factor)?1:0)))
                .Where(s=>s.Axes>0).OrderByDescending(s=>s.Axes)
                .ThenBy(s=>s.Axis.Dx*s.Axis.Dx+s.Axis.Dy*s.Axis.Dy)
                .ThenBy(s=>s.Key,StringComparer.OrdinalIgnoreCase)
                .ThenBy(s=>s.Point.X).ThenBy(s=>s.Point.Y).ToArray();
            if(choices.Length==0)
            {
                result.Notes.Add(e.Key+":ENDPOINT_NO_DISTINCT_VISIBLE_DATUM_"+label);
                continue;
            }
            foreach(bool horizontal in new[]{true,false})
            {
                // If all candidates share an ordinate, there is no spacing to measure on it.
                var options=choices.Where(s=>s.Axis.HasComponent(horizontal,factor))
                    .Select(s=>{
                        var bounds=new Rect(Math.Min(e.Bounds.L,s.Point.X),Math.Min(e.Bounds.B,s.Point.Y),
                            Math.Max(e.Bounds.R,s.Point.X),Math.Max(e.Bounds.T,s.Point.Y));
                        return new Requirement(e.Key,label,s.Key,horizontal,s.Axis.Along(horizontal),bounds);
                    }).DistinctBy(r=>(r.Datum,r.Span)).ToArray();
                if(options.Length>0)
                    result.Required.Add(options[0] with {AlternateDatums=options.Skip(1).ToArray()});
            }
        }
        return result;
    }
}
