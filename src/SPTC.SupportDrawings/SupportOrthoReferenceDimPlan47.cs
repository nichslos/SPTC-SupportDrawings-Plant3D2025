using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.47: reference-style pipe-support DETAIL dimensions, following the
/// supplied 25-page drafting examples (particularly sheets 15-16).
/// Draw a readable chain of verified projected member-edge/endpoint stations
/// near the support and a separate OUTER overall dimension. Do NOT dimension
/// each individual member's WCS bounding rectangle in every direction.
/// Existing geometry is read-only; measurements are projected millimetres,
/// never fabrication cut lengths, bolt pitches or invented hole centres.
/// </summary>
public static class SupportOrthoReferenceDimPlan47
{
    public sealed record Result(
        IReadOnlyList<SupportOrthoDimLinearPlan41.Dimension> Dimensions,
        IReadOnlyList<string> Skipped,
        int OrthographicViews,int ChainDimensions,int OverallDimensions,
        int CollisionCandidatesRejected,int RequestedDimensions);

    private sealed record Station(double At,int Weight);
    private sealed record Request(bool Horizontal,double From,double To,
        bool Overall,int Index,string Measurement);
    private sealed record Placed(bool Horizontal,
        SupportOrthoSmartDimPlan46.Rect Line,
        SupportOrthoSmartDimPlan46.Rect Text);

    private static readonly HashSet<string> Ortho=new(
        new[]{"TOP","BOTTOM","FRONT","BACK","LEFT","RIGHT"},
        StringComparer.OrdinalIgnoreCase);

    public static Result Build(
        IReadOnlyList<SupportOrthoMemberCalloutPlan17.Camera> cameras,
        IReadOnlyList<SupportOrthoAllViewDimPlan44.Member> members,
        double paperW,double paperH,double unitsToMm,
        IReadOnlyList<SupportOrthoDimLinearPlan41.Segment>? sources=null,
        IReadOnlyList<SupportOrthoMemberCalloutPlan27.Callout>? callouts=null,
        IReadOnlyList<NativeOrthoViewTitlePlan.Title>? titles=null)
    {
        if(cameras==null||members==null||!double.IsFinite(paperW)||
           !double.IsFinite(paperH)||!double.IsFinite(unitsToMm)||
           paperW<=0||paperH<=0||unitsToMm<=0)
            throw new ArgumentException("REV22.47 invalid drafting inputs.");
        var dimensions=new List<SupportOrthoDimLinearPlan41.Dimension>();
        var skipped=new List<string>();
        int views=0,chain=0,overall=0,rejected=0,requested=0;
        var paper=new SupportOrthoSmartDimPlan46.Rect(
            2,paperH*.12+2,paperW*.78-2,paperH*.94-2);

        foreach(var camera in cameras)
        {
            if(!Ortho.Contains(camera.Name))
            {
                skipped.Add(camera.Name+":ISO_REFERENCE_DIMENSIONS_NOT_VALID");
                continue;
            }
            views++;
            if(!double.IsFinite(camera.Scale)||camera.Scale<=0||
               camera.W<=0||camera.H<=0)
            {
                skipped.Add(camera.Name+":INVALID_CAMERA");
                continue;
            }
            var frame=new SupportOrthoSmartDimPlan46.Rect(
                camera.X-camera.W*.5,camera.Y-camera.H*.5,
                camera.X+camera.W*.5,camera.Y+camera.H*.5);
            var safe=new SupportOrthoSmartDimPlan46.Rect(
                frame.L+1.4,frame.B+1.4,
                frame.R-1.4,frame.T-1.4);
            var projected=new List<SupportOrthoSmartDimPlan46.Rect>();
            foreach(var member in members)
            {
                if(!ValidMember(member))
                {
                    skipped.Add(camera.Name+"/"+member.Key+
                        ":UNVERIFIED_MEMBER_EXTENTS");
                    continue;
                }
                var bounds=Project(camera,member);
                if(bounds.L<safe.L-.05||bounds.R>safe.R+.05||
                   bounds.B<safe.B-.05||bounds.T>safe.T+.05)
                {
                    skipped.Add(camera.Name+"/"+member.Key+
                        ":OUTSIDE_NATIVE_VIEW");
                    continue;
                }
                projected.Add(bounds);
            }
            if(projected.Count==0)
            {
                skipped.Add(camera.Name+":NO_VERIFIED_VISIBLE_MEMBER");
                continue;
            }
            double minX=projected.Min(b=>b.L),maxX=projected.Max(b=>b.R);
            double minY=projected.Min(b=>b.B),maxY=projected.Max(b=>b.T);
            var assembly=new SupportOrthoSmartDimPlan46.Rect(
                minX,minY,maxX,maxY);
            var blocks=FixedZones(camera,cameras,callouts,titles);
            var placed=new List<Placed>();
            foreach(bool horizontal in new[]{true,false})
            {
                double min=horizontal?minX:minY;
                double max=horizontal?maxX:maxY;
                if(max-min<.25)
                {
                    skipped.Add(camera.Name+(horizontal?"/H":"/V")+
                        ":OVERALL_SPAN_TOO_SMALL");
                    continue;
                }
                // Primary fabrication datums: real member edges and any
                // independently verified Plant Start/End positions.
                var stationCandidates=new List<Station>();
                foreach(var b in projected)
                {
                    stationCandidates.Add(new Station(horizontal?b.L:b.B,1));
                    stationCandidates.Add(new Station(horizontal?b.R:b.T,1));
                    // AABB midpoints are not verified hole/bolt/pipe
                    // centre marks. Reference-style chain stations must
                    // come from shared real edges or explicit endpoints.
                }
                if(sources!=null)
                    foreach(var source in sources)
                    {
                        if(source.Provenance!="EXPLICIT_MEMBER_ENDPOINTS")
                            continue;
                        foreach(var point in new[]{
                            (source.Ax,source.Ay,source.Az),
                            (source.Bx,source.By,source.Bz)})
                        {
                            var p=SupportOrthoMemberCalloutPlan17.Project(
                                camera,point.Item1,point.Item2,point.Item3);
                            double at=horizontal?p.X:p.Y;
                            if(at>min+.35&&at<max-.35)
                                stationCandidates.Add(new Station(at,4));
                        }
                    }
                var stations=SelectStations(stationCandidates,min,max);
                var steps=new List<Request>();
                if(stations.Count>2)
                {
                    for(int i=0;i<stations.Count-1;i++)
                    {
                        double start=stations[i],end=stations[i+1];
                        // Ref details dimension meaningful stations only.
                        // Do not fabricate a 0 mm or unprintable chain link.
                        if(end-start<.50)continue;
                        steps.Add(MakeRequest(horizontal,start,end,false,
                            i,camera.Scale,unitsToMm));
                    }
                }
                var big=MakeRequest(horizontal,min,max,true,0,
                    camera.Scale,unitsToMm);
                // Keep all consecutive chain measurements in ONE lane where
                // possible, and place overall beyond it. This follows the
                // 712+110 below 822 and 2225 multi-station examples.
                double chainOffset=0;
                int chainPlaced=0;
                if(steps.Count>1)
                {
                    bool chainGroupPlaced=false;
                    foreach(double sign in PreferredSigns(horizontal))
                    {
                    if(chainGroupPlaced)break;
                    foreach(double offset in LaneOffsets())
                    {
                        var local=new List<(
                            SupportOrthoDimLinearPlan41.Dimension D,
                            Placed P)>();
                        bool clear=true;
                        foreach(var step in steps)
                        {
                            var candidate=Candidate(camera,assembly,
                                step,sign,offset);
                            if(candidate==null||
                               !Fits(candidate.Value.P,blocks,paper,
                                    placed.Concat(local.Select(x=>x.P))))
                            {
                                rejected++;
                                clear=false;
                                break;
                            }
                            local.Add(candidate.Value);
                        }
                        if(!clear)continue;
                        foreach(var item in local)
                        {
                            dimensions.Add(item.D);
                            placed.Add(item.P);
                            chain++;
                            chainPlaced++;
                        }
                        chainOffset=offset;
                        chainGroupPlaced=true;
                        break;
                    }
                    }
                }
                if(steps.Count>1&&chainPlaced!=steps.Count)
                {
                    skipped.Add(camera.Name+"/"+(horizontal?"H":"V")+
                        ":CHAIN_NOT_FITTED_TO_ONE_LANE");
                    // Suppress the unsafe partial chain, but preserve
                    // a correctly measured overall dimension.
                }
                requested+=steps.Count>1?steps.Count:0;
                requested++;
                bool overallPlaced=false;
                foreach(double sign in PreferredSigns(horizontal))
                {
                if(overallPlaced)break;
                foreach(double offset in LaneOffsets())
                {
                    double extra=steps.Count>1&&chainPlaced==steps.Count
                        ?Math.Max(0,chainOffset+5-offset):0;
                    var candidate=Candidate(camera,assembly,big,sign,
                        offset+extra);
                    if(candidate==null||
                       !Fits(candidate.Value.P,blocks,paper,placed))
                    {
                        rejected++;
                        continue;
                    }
                    dimensions.Add(candidate.Value.D);
                    placed.Add(candidate.Value.P);
                    overall++;
                    overallPlaced=true;
                    break;
                }
                }
                if(!overallPlaced)
                    skipped.Add(camera.Name+"/"+(horizontal?"H":"V")+
                        ":OVERALL_NO_CLEAR_DIMENSION_LANE");
            }
        }
        return new Result(dimensions,skipped,views,chain,overall,rejected,
            requested);
    }

    private static Request MakeRequest(bool horizontal,double from,
        double to,bool overall,int index,double scale,double units)
    {
        double mm=(to-from)/scale*units;
        return new Request(horizontal,from,to,overall,index,
            mm.ToString("0.###",CultureInfo.InvariantCulture));
    }

    private static List<double> SelectStations(
        IReadOnlyList<Station> proposed,double min,double max)
    {
        var sorted=proposed.Where(x=>double.IsFinite(x.At)&&
            x.At>min+.35&&x.At<max-.35)
            .OrderBy(x=>x.At).ToArray();
        var clustered=new List<Station>();
        foreach(var item in sorted)
        {
            if(clustered.Count>0&&
               Math.Abs(item.At-clustered[^1].At)<.12)
            {
                var last=clustered[^1];
                clustered[^1]=new Station(
                    (last.At*last.Weight+item.At*item.Weight)/
                    (last.Weight+item.Weight),
                    last.Weight+item.Weight);
            }
            else clustered.Add(item);
        }
        // A lone boundary of a wide member is not enough evidence for an
        // attachment station. Require two coincident verified member
        // boundaries or an explicit Plant Start/End point. Never invent
        // bolt/hole centres by averaging bounding-box edges.
        var chosen=clustered.Where(x=>x.Weight>=2)
            .OrderByDescending(x=>x.Weight)
            .ThenBy(x=>x.At).Take(7)
            .OrderBy(x=>x.At).Select(x=>x.At);
        return new[]{min}.Concat(chosen).Concat(new[]{max}).ToList();
    }

    private static IEnumerable<double> PreferredSigns(bool horizontal) =>
        horizontal?new[]{1.0,-1.0}:new[]{-1.0,1.0};

    private static IEnumerable<double> LaneOffsets() =>
        new[]{4.5,7.5,10.5,13.5,16.5,19.5,22.5,
            25.5,28.5,31.5,34.5,37.5,40.5};

    private static (
        SupportOrthoDimLinearPlan41.Dimension D,Placed P)?
        Candidate(
        SupportOrthoMemberCalloutPlan17.Camera c,
        SupportOrthoSmartDimPlan46.Rect box,Request request,
        double sign,double offset)
    {
        double line= request.Horizontal?
            (sign>0?box.T+offset:box.B-offset):
            (sign>0?box.R+offset:box.L-offset);
        double center=(request.From+request.To)*.5;
        double printedWidth=Math.Max(7,request.Measurement.Length*1.85+3);
        double textAt=center;
        if(request.To-request.From<printedWidth+2)
            textAt=request.To+printedWidth*.5+1.5;
        double tx=request.Horizontal?textAt:line+
            (sign>0?2.7:-2.7);
        double ty=request.Horizontal?line+
            (sign>0?2.7:-2.7):textAt;
        var dimRect=request.Horizontal
            ?new SupportOrthoSmartDimPlan46.Rect(
                request.From-.4,line-.4,request.To+.4,line+.4)
            :new SupportOrthoSmartDimPlan46.Rect(
                line-.4,request.From-.4,line+.4,request.To+.4);
        var textRect=request.Horizontal
            ?new SupportOrthoSmartDimPlan46.Rect(
                tx-printedWidth*.5-1,ty-1.3,
                tx+printedWidth*.5+1,ty+1.3)
            :new SupportOrthoSmartDimPlan46.Rect(
                tx-1.3,ty-printedWidth*.5-1,
                tx+1.3,ty+printedWidth*.5+1);
        string axis=request.Horizontal?"H":"V";
        string source="DETAIL:"+c.Name+"/"+axis+"/"+
            (request.Overall?"OVERALL":
                "CHAIN_"+request.Index);
        string provenance=request.Overall
            ?"REFERENCE_PROJECTED_OVERALL_NOT_CUT_LENGTH"
            :"REFERENCE_PROJECTED_DATUM_CHAIN_NOT_BOLT_PITCH";
        double aX=request.Horizontal?request.From:box.L;
        double aY=request.Horizontal?
            (sign>0?box.T:box.B):request.From;
        double bX=request.Horizontal?request.To:
            (sign>0?box.R:box.L);
        double bY=request.Horizontal?
            (sign>0?box.T:box.B):request.To;
        aX=request.Horizontal?request.From:(sign>0?box.R:box.L);
        var dim=new SupportOrthoDimLinearPlan41.Dimension(
            c.Name,source,"STRUCTURE",request.Horizontal,
            aX,aY,bX,bY,request.Horizontal?center:line,
            request.Horizontal?line:center,request.Measurement,
            provenance,tx,ty);
        return (dim,new Placed(request.Horizontal,dimRect,textRect));
    }

    private static bool Fits(Placed p,
        IReadOnlyList<SupportOrthoSmartDimPlan46.Rect> fixedAreas,
        SupportOrthoSmartDimPlan46.Rect paper,
        IEnumerable<Placed> existing)
    {
        if(!Inside(p.Line,paper)||!Inside(p.Text,paper))return false;
        if(fixedAreas.Any(b=>p.Line.Intersects(b,.45)||
                             p.Text.Intersects(b,.45)))return false;
        foreach(var prev in existing)
        {
            if(p.Text.Intersects(prev.Text,.45)||
               p.Text.Intersects(prev.Line,.4)||
               p.Line.Intersects(prev.Text,.4))
                return false;
            // Chain dimensions are permitted to meet exactly at a station;
            // orthogonal lines may not cut through one another.
            if(p.Horizontal!=prev.Horizontal&&
               p.Line.Intersects(prev.Line,.3))
                return false;
        }
        return true;
    }

    private static bool Inside(SupportOrthoSmartDimPlan46.Rect a,
        SupportOrthoSmartDimPlan46.Rect b)=>
        a.IsFinite&&a.L>=b.L&&a.R<=b.R&&a.B>=b.B&&a.T<=b.T;

    private static List<SupportOrthoSmartDimPlan46.Rect> FixedZones(
        SupportOrthoMemberCalloutPlan17.Camera current,
        IReadOnlyList<SupportOrthoMemberCalloutPlan17.Camera> cameras,
        IReadOnlyList<SupportOrthoMemberCalloutPlan27.Callout>? callouts,
        IReadOnlyList<NativeOrthoViewTitlePlan.Title>? titles)
    {
        var zones=new List<SupportOrthoSmartDimPlan46.Rect>();
        foreach(var c in cameras)
        {
            if(c.Name.Equals(current.Name,StringComparison.OrdinalIgnoreCase))
                continue;
            zones.Add(new SupportOrthoSmartDimPlan46.Rect(
                c.X-c.W*.5-1,c.Y-c.H*.5-1,
                c.X+c.W*.5+1,c.Y+c.H*.5+1));
        }
        foreach(var t in titles??Array.Empty<NativeOrthoViewTitlePlan.Title>())
            zones.Add(new SupportOrthoSmartDimPlan46.Rect(
                t.CenterX-t.Width*.5-1,t.TopY-t.Height-1,
                t.CenterX+t.Width*.5+1,t.TopY+1));
        foreach(var c in callouts??Array.Empty<SupportOrthoMemberCalloutPlan27.Callout>())
        {
            // Preserve actual visible annotation text and leader corridors.
            zones.Add(new SupportOrthoSmartDimPlan46.Rect(
                c.LabelX-1,c.LabelTop-c.LabelHeight-1,
                c.LabelX+c.LabelWidth+1,c.LabelTop+1));
            double land=c.LabelTop-
                SupportOrthoMemberCalloutPlan27.TextLineHeightMm*.5;
            zones.Add(LineZone(c.AnchorX,c.AnchorY,c.LaneX,c.AnchorY));
            zones.Add(LineZone(c.LaneX,c.AnchorY,c.LaneX,land));
            zones.Add(LineZone(c.LaneX,land,c.LabelX-1,land));
        }
        return zones;
    }

    private static SupportOrthoSmartDimPlan46.Rect LineZone(
        double ax,double ay,double bx,double by)=>
        new(Math.Min(ax,bx)-.35,Math.Min(ay,by)-.35,
            Math.Max(ax,bx)+.35,Math.Max(ay,by)+.35);

    private static bool ValidMember(
        SupportOrthoAllViewDimPlan44.Member m)=>
        !string.IsNullOrWhiteSpace(m.Key)&&
        (m.Kind=="STRUCTURE"||m.Kind=="SUPPORT")&&
        new[]{m.MinX,m.MinY,m.MinZ,m.MaxX,m.MaxY,m.MaxZ}
            .All(double.IsFinite)&&
        m.MinX<=m.MaxX&&m.MinY<=m.MaxY&&m.MinZ<=m.MaxZ;

    private static SupportOrthoSmartDimPlan46.Rect Project(
        SupportOrthoMemberCalloutPlan17.Camera c,
        SupportOrthoAllViewDimPlan44.Member m)
    {
        double minX=double.PositiveInfinity,minY=double.PositiveInfinity;
        double maxX=double.NegativeInfinity,maxY=double.NegativeInfinity;
        foreach(double x in new[]{m.MinX,m.MaxX})
        foreach(double y in new[]{m.MinY,m.MaxY})
        foreach(double z in new[]{m.MinZ,m.MaxZ})
        {
            var p=SupportOrthoMemberCalloutPlan17.Project(c,x,y,z);
            minX=Math.Min(minX,p.X);maxX=Math.Max(maxX,p.X);
            minY=Math.Min(minY,p.Y);maxY=Math.Max(maxY,p.Y);
        }
        return new SupportOrthoSmartDimPlan46.Rect(minX,minY,maxX,maxY);
    }
}
