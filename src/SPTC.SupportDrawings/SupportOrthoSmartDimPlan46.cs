using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.46: collision-aware PAPER SPACE dimension planner.
/// Reads the saved native view cameras and recognized member world extents.
/// It does NOT modify model geometry or claim fabrication/cut lengths.
/// Dimension lines, text, witnesses, existing leaders, titles and neighboring
/// viewports share one deterministic paper-space collision model.
/// </summary>
public static class SupportOrthoSmartDimPlan46
{
    public readonly record struct Rect(double L,double B,double R,double T)
    {
        public bool Intersects(Rect b,double gap=0) =>
            L<b.R+gap&&R>b.L-gap&&B<b.T+gap&&T>b.B-gap;
        public bool IsFinite => double.IsFinite(L)&&double.IsFinite(B)&&
            double.IsFinite(R)&&double.IsFinite(T)&&L<=R&&B<=T;
    }

    public sealed record Placement(string View,string Source,
        Rect DimensionLine,Rect Text,Rect Witness1,Rect Witness2);
    public sealed record Result(
        IReadOnlyList<SupportOrthoDimLinearPlan41.Dimension> Dimensions,
        IReadOnlyList<string> Skipped,
        IReadOnlyList<Placement> Placements,
        int MemberCount,int OrthographicViewCount,int RequestedAxisCount,
        int Repositioned,int CollisionCandidatesRejected);

    private sealed record Envelope(SupportOrthoAllViewDimPlan44.Member Member,
        Rect Bounds);
    private sealed record Request(Envelope Source,bool Horizontal,double Span,
        double Mm,string Label);

    private static readonly HashSet<string> Ortho=new(
        new[]{"TOP","BOTTOM","LEFT","RIGHT","FRONT","BACK"},
        StringComparer.OrdinalIgnoreCase);

    public static Result Build(
        IReadOnlyList<SupportOrthoMemberCalloutPlan17.Camera> cameras,
        IReadOnlyList<SupportOrthoAllViewDimPlan44.Member> members,
        double paperW,double paperH,double unitsToMm,
        IReadOnlyList<SupportOrthoMemberCalloutPlan27.Callout>? callouts=null,
        IReadOnlyList<NativeOrthoViewTitlePlan.Title>? titles=null,
        IReadOnlyList<Rect>? additionalKeepouts=null)
    {
        if(cameras==null||members==null||
           !double.IsFinite(paperW)||!double.IsFinite(paperH)||
           !double.IsFinite(unitsToMm)||paperW<=0||paperH<=0||
           unitsToMm<=0)
            throw new ArgumentException("REV22.46 invalid layout inputs.");

        var dims=new List<SupportOrthoDimLinearPlan41.Dimension>();
        var skips=new List<string>();
        var placements=new List<Placement>();
        int repositioned=0,rejected=0,viewCount=0;
        double paperRight=paperW*.78,paperBottom=paperH*.12,
            paperTop=paperH*.94;
        var paper=new Rect(2,paperBottom+1.5,paperRight-2,paperTop-1.5);

        foreach(var camera in cameras)
        {
            if(!Ortho.Contains(camera.Name))
            {
                skips.Add(camera.Name+":ISO_DIMLINEAR_NOT_VALID");
                continue;
            }
            viewCount++;
            if(!double.IsFinite(camera.Scale)||camera.Scale<=0||
               !double.IsFinite(camera.W)||!double.IsFinite(camera.H)||
               camera.W<=0||camera.H<=0)
            {
                skips.Add(camera.Name+":INVALID_NATIVE_CAMERA");
                continue;
            }
            var frame=new Rect(camera.X-camera.W*.5,
                camera.Y-camera.H*.5,camera.X+camera.W*.5,
                camera.Y+camera.H*.5);
            var body=new Rect(frame.L+1.4,frame.B+1.4,
                frame.R-1.4,frame.T-1.4);
            var fixedZones=BuildFixedZones(camera,cameras,
                callouts??Array.Empty<SupportOrthoMemberCalloutPlan27.Callout>(),
                titles??Array.Empty<NativeOrthoViewTitlePlan.Title>());
            if(additionalKeepouts!=null)
                fixedZones.AddRange(additionalKeepouts.Where(x=>x.IsFinite));

            var envelopes=new List<Envelope>();
            foreach(var member in members.OrderBy(m=>m.Key,
                         StringComparer.OrdinalIgnoreCase))
            {
                if(!SupportOrthoNativeDimensionPolicy48.AcceptMember(member))
                {
                    skips.Add(camera.Name+"/"+member.Key+
                        ":XREF_OR_NONNATIVE_DIMENSION_SOURCE_EXCLUDED");
                    continue;
                }
                if(!Valid(member))
                {
                    skips.Add(camera.Name+"/"+member.Key+
                        ":INVALID_SOURCE_EXTENTS");
                    continue;
                }
                var rect=ProjectBounds(camera,member);
                if(!rect.IsFinite)
                {
                    skips.Add(camera.Name+"/"+member.Key+
                        ":PROJECTED_EXTENTS_INVALID");
                    continue;
                }
                // Witness geometry must belong to the target native view.
                if(rect.L<body.L-.05||rect.R>body.R+.05||
                   rect.B<body.B-.05||rect.T>body.T+.05)
                {
                    skips.Add(camera.Name+"/"+member.Key+
                        ":MEMBER_NOT_FULLY_IN_NATIVE_VIEW");
                    continue;
                }
                envelopes.Add(new Envelope(member,rect));
            }

            var requests=new List<Request>();
            foreach(var envelope in envelopes)
            foreach(bool horizontal in new[]{true,false})
            {
                double span=horizontal?envelope.Bounds.R-envelope.Bounds.L:
                    envelope.Bounds.T-envelope.Bounds.B;
                string axis=horizontal?"HORIZONTAL":"VERTICAL";
                if(!double.IsFinite(span)||span<.25)
                {
                    skips.Add(camera.Name+"/"+envelope.Member.Key+
                        ":"+axis+"_SPAN_TOO_SMALL_ON_PAPER");
                    continue;
                }
                double mm=span/camera.Scale*unitsToMm;
                if(!double.IsFinite(mm)||mm<.1)
                {
                    skips.Add(camera.Name+"/"+envelope.Member.Key+
                        ":"+axis+"_MODEL_PROJECTION_INVALID");
                    continue;
                }
                requests.Add(new Request(envelope,horizontal,span,mm,
                    mm.ToString("0.###",CultureInfo.InvariantCulture)));
            }

            // Short/local dimensions are placed before long overall ones.
            // This creates progressively outward dimension stacks, but does
            // not change their exact source-projected model measurements.
            // Witness lines may meet each other at shared source corners.
            // Reject text/line crossings, but do not treat legitimate
            // witness-to-witness junctions as text clashes.
            var occupiedCore=new List<Rect>();
            var occupiedWitnesses=new List<Rect>();
            foreach(var req in requests.OrderBy(x=>x.Span)
                       .ThenBy(x=>x.Source.Member.Key,
                           StringComparer.OrdinalIgnoreCase)
                       .ThenBy(x=>x.Horizontal?0:1))
            {
                var b=req.Source.Bounds;
                bool horizontal=req.Horizontal;
                double center=horizontal?(b.L+b.R)*.5:(b.B+b.T)*.5;
                double perpendicular=horizontal?(b.B+b.T)*.5:(b.L+b.R)*.5;
                double refCenter=horizontal?camera.Y:camera.X;
                double preferred=perpendicular>=refCenter?1:-1;
                // Minimum 3.2 mm clearance to the source member. When
                // congested, search successive measured 3.2 mm lanes.
                var offsets=new[]{3.2,6.4,9.6,12.8,16.0,19.2,22.4,
                    25.6,28.8,32.0,35.2,38.4,41.6,44.8};
                bool placed=false;
                int attempt=0;
                foreach(double offset in offsets)
                {
                    foreach(double sign in new[]{preferred,-preferred})
                    {
                        attempt++;
                        double lineX=horizontal?center:
                            (sign>0?b.R+offset:b.L-offset);
                        double lineY=horizontal?
                            (sign>0?b.T+offset:b.B-offset):center;
                        // Text is explicitly placed above the dimension
                        // line; small dimensions have text outside the
                        // extension lines instead of crowding the arrows.
                        // Width is a conservative paper-glyph reservation.
                        double textLength=Math.Max(8.5,
                            req.Label.Length*2.15+5.0);
                        double textX=horizontal?
                            (req.Span<textLength+4?
                                b.R+2.5+textLength*.5:center):
                            lineX+2.5;
                        double textY=horizontal?lineY+2.5:
                            (req.Span<textLength+4?
                                b.T+2.5+textLength*.5:center);

                        var dimLine=horizontal?
                            new Rect(b.L-.65,lineY-.65,b.R+.65,lineY+.65):
                            new Rect(lineX-.65,b.B-.65,lineX+.65,b.T+.65);
                        var textZone=horizontal?
                            new Rect(textX-textLength*.5-.9,textY-1.5,
                                textX+textLength*.5+.9,textY+1.5):
                            new Rect(textX-1.5,textY-textLength*.5-.9,
                                textX+1.5,textY+textLength*.5+.9);
                        var witness1=horizontal?
                            Thin(b.L,b.B,lineX,lineY,true):
                            Thin(b.L,b.B,lineX,lineY,false);
                        var witness2=horizontal?
                            Thin(b.R,b.B,lineX,lineY,true):
                            Thin(b.L,b.T,lineX,lineY,false);
                        // For horizontal the witness line is vertical and
                        // for vertical the witness line is horizontal.
                        var pieces=new[]{dimLine,textZone,witness1,witness2};
                        if(pieces.Any(p=>!Inside(p,paper))||
                           pieces.Any(p=>fixedZones.Any(f=>p.Intersects(f,.5)))||
                           // Dimension line/text must not cover a *different*
                           // member's projected silhouette. Witnesses are
                           // allowed to touch their source member.
                           envelopes.Any(e=>e!=req.Source &&
                               (dimLine.Intersects(e.Bounds,.4)||
                                textZone.Intersects(e.Bounds,.4)))||
                           new[]{dimLine,textZone}.Any(p=>
                               occupiedCore.Any(o=>p.Intersects(o,.45))||
                               occupiedWitnesses.Any(o=>p.Intersects(o,.45)))||
                           new[]{witness1,witness2}.Any(p=>
                               occupiedCore.Any(o=>p.Intersects(o,.45))))
                        {
                            rejected++;
                            continue;
                        }
                        var measure=new SupportOrthoDimLinearPlan41.Dimension(
                            camera.Name,
                            req.Source.Member.Key+(horizontal?"/H":"/V"),
                            req.Source.Member.Kind,horizontal,
                            b.L,b.B,horizontal?b.R:b.L,
                            horizontal?b.B:b.T,lineX,lineY,req.Label,
                            "PROJECTED_OVERALL_VIEW_ENVELOPE",
                            textX,textY,
                            SupportOrthoDynamicDimension492
                                .ModelMillimetersPerPaperUnit(
                                    unitsToMm,camera.Scale));
                        dims.Add(measure);
                        placements.Add(new Placement(camera.Name,
                            measure.Source,dimLine,textZone,witness1,
                            witness2));
                        occupiedCore.Add(dimLine);
                        occupiedCore.Add(textZone);
                        occupiedWitnesses.Add(witness1);
                        occupiedWitnesses.Add(witness2);
                        if(attempt>1)repositioned++;
                        placed=true;
                        break;
                    }
                    if(placed)break;
                }
                if(!placed)
                    skips.Add(camera.Name+"/"+req.Source.Member.Key+
                        ":"+(horizontal?"HORIZONTAL":"VERTICAL")+
                        "_NO_COLLISION_FREE_DIMENSION_LANE");
            }
        }
        return new Result(dims,skips,placements,members.Count,viewCount,
            members.Count*viewCount*2,repositioned,rejected);
    }

    private static Rect Thin(double fromX,double fromY,
        double toX,double toY,bool vertical) =>
        vertical
            ?new Rect(fromX-.30,Math.Min(fromY,toY)-.25,
                fromX+.30,Math.Max(fromY,toY)+.25)
            :new Rect(Math.Min(fromX,toX)-.25,fromY-.30,
                Math.Max(fromX,toX)+.25,fromY+.30);

    private static List<Rect> BuildFixedZones(
        SupportOrthoMemberCalloutPlan17.Camera current,
        IReadOnlyList<SupportOrthoMemberCalloutPlan17.Camera> cameras,
        IReadOnlyList<SupportOrthoMemberCalloutPlan27.Callout> callouts,
        IReadOnlyList<NativeOrthoViewTitlePlan.Title> titles)
    {
        var fixedZones=new List<Rect>();
        foreach(var c in cameras)
        {
            if(c.Name.Equals(current.Name,StringComparison.OrdinalIgnoreCase))
                continue;
            // No dimension component may invade another native viewport,
            // including the four ISO views that are not dimensioned.
            fixedZones.Add(new Rect(c.X-c.W*.5-1,c.Y-c.H*.5-1,
                c.X+c.W*.5+1,c.Y+c.H*.5+1));
        }
        foreach(var t in titles)
            fixedZones.Add(new Rect(t.CenterX-t.Width*.5-.8,
                t.TopY-t.Height-.8,t.CenterX+t.Width*.5+.8,
                t.TopY+.8));
        foreach(var c in callouts)
        {
            var labelLines=c.Text.Replace("\r\n","\n").Split('\n');
            double width=Math.Min(c.LabelWidth,
                labelLines.Max(l=>l.Length)*
                    SupportOrthoMemberCalloutPlan27.TextHeightMm*1.05+4);
            fixedZones.Add(new Rect(c.LabelX-.6,
                c.LabelTop-c.LabelHeight-.5,
                c.LabelX+width+.6,c.LabelTop+.5));
            double endY=c.LabelTop-
                SupportOrthoMemberCalloutPlan27.TextLineHeightMm*.5;
            fixedZones.Add(ThinLine(c.AnchorX,c.AnchorY,
                c.LaneX,c.AnchorY));
            fixedZones.Add(ThinLine(c.LaneX,c.AnchorY,
                c.LaneX,endY));
            fixedZones.Add(ThinLine(c.LaneX,endY,
                c.LabelX-1,endY));
        }
        return fixedZones;
    }

    private static Rect ThinLine(double x0,double y0,double x1,double y1) =>
        new(Math.Min(x0,x1)-.3,Math.Min(y0,y1)-.3,
            Math.Max(x0,x1)+.3,Math.Max(y0,y1)+.3);

    private static bool Inside(Rect b,Rect p) =>
        b.IsFinite&&b.L>=p.L&&b.R<=p.R&&b.B>=p.B&&b.T<=p.T;

    private static bool Valid(SupportOrthoAllViewDimPlan44.Member m)=>
        !string.IsNullOrWhiteSpace(m.Key)&&
        (m.Kind=="STRUCTURE"||m.Kind=="SUPPORT")&&
        double.IsFinite(m.MinX)&&double.IsFinite(m.MinY)&&
        double.IsFinite(m.MinZ)&&double.IsFinite(m.MaxX)&&
        double.IsFinite(m.MaxY)&&double.IsFinite(m.MaxZ)&&
        m.MinX<=m.MaxX&&m.MinY<=m.MaxY&&m.MinZ<=m.MaxZ;

    private static Rect ProjectBounds(
        SupportOrthoMemberCalloutPlan17.Camera c,
        SupportOrthoAllViewDimPlan44.Member m)
    {
        double left=double.PositiveInfinity,right=double.NegativeInfinity;
        double bottom=double.PositiveInfinity,top=double.NegativeInfinity;
        foreach(double x in new[]{m.MinX,m.MaxX})
        foreach(double y in new[]{m.MinY,m.MaxY})
        foreach(double z in new[]{m.MinZ,m.MaxZ})
        {
            var p=SupportOrthoMemberCalloutPlan17.Project(c,x,y,z);
            left=Math.Min(left,p.X);right=Math.Max(right,p.X);
            bottom=Math.Min(bottom,p.Y);top=Math.Max(top,p.Y);
        }
        return new Rect(left,bottom,right,top);
    }
}
