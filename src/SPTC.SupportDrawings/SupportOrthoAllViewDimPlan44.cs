using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.44: sheet-wide *per orthographic view, per verified Plant member*
/// horizontal AND vertical overall projected-envelope DIMLINEAR planner.
/// Unlike REV22.42, there is no cross-view, equal-length or source dedup.
/// Each source gets a separate H/V dimension in each eligible saved native
/// orthographic viewport, if measurable and collision-free on the paper.
/// Values describe the overall view-projected bounds (not steel cut length).
/// No isometric DIMLINEAR: apparent lengths in ISO are foreshortened.
/// </summary>
public static class SupportOrthoAllViewDimPlan44
{
    public sealed record Member(string Key,string Kind,
        double MinX,double MinY,double MinZ,
        double MaxX,double MaxY,double MaxZ);

    public sealed record Result(
        IReadOnlyList<SupportOrthoDimLinearPlan41.Dimension> Dimensions,
        IReadOnlyList<string> Skipped,
        int MemberCount,int OrthographicViewCount,int RequestedAxisCount);

    private static readonly HashSet<string> Orthogonal=new(
        new[]{"TOP","BOTTOM","FRONT","BACK","LEFT","RIGHT"},
        StringComparer.OrdinalIgnoreCase);

    public static Result Build(
        IReadOnlyList<SupportOrthoMemberCalloutPlan17.Camera> cameras,
        IReadOnlyList<Member> members,double paperW,double paperH,
        double unitsToMm)
    {
        if(cameras==null||members==null ||
           !double.IsFinite(paperW)||!double.IsFinite(paperH)||
           !double.IsFinite(unitsToMm)||paperW<=0||paperH<=0||
           unitsToMm<=0)
            throw new ArgumentException("REV22.44 invalid dimension inputs.");
        var result=new List<SupportOrthoDimLinearPlan41.Dimension>();
        var skipped=new List<string>();
        int axial=0;
        foreach(var c in cameras)
        {
            if(!Orthogonal.Contains(c.Name))
            {
                skipped.Add(c.Name+":ISO_DIMLINEAR_NOT_VALID");
                continue;
            }
            axial++;
            if(!double.IsFinite(c.Scale)||c.Scale<=0||c.W<=0||c.H<=0)
            {
                skipped.Add(c.Name+":INVALID_NATIVE_CAMERA");
                continue;
            }
            double frameLeft=c.X-c.W*.5+1.4;
            double frameRight=c.X+c.W*.5-1.4;
            double frameBottom=c.Y-c.H*.5+1.4;
            double frameTop=c.Y+c.H*.5-1.4;
            // REV22.45: geometry/witness points remain inside the native
            // viewport, but the dimension line can use neighboring blank
            // paper GUTTERS outside it (critical for crowded TOP views).
            double laneLeft=Math.Max(2.0,frameLeft-34.0);
            double laneRight=Math.Min(paperW*.78,frameRight+34.0);
            double laneBottom=Math.Max(paperH*.12,frameBottom-29.0);
            double laneTop=Math.Min(paperH*.94,frameTop+29.0);
            foreach(var other in cameras)
            {
                if(ReferenceEquals(c,other))continue;
                // Prevent one view from consuming an adjacent view's space.
                if(Math.Abs(other.Y-c.Y)<(c.H+other.H)*.60)
                {
                    double split=(c.X+other.X)*.5;
                    if(other.X<c.X)laneLeft=Math.Max(laneLeft,split+3);
                    if(other.X>c.X)laneRight=Math.Min(laneRight,split-3);
                }
                if(Math.Abs(other.X-c.X)<(c.W+other.W)*.60)
                {
                    double split=(c.Y+other.Y)*.5;
                    if(other.Y<c.Y)laneBottom=Math.Max(laneBottom,split+3);
                    if(other.Y>c.Y)laneTop=Math.Min(laneTop,split-3);
                }
            }
            // Each view gets its own dimension lanes. Cross-view dimensions
            // are not deduplicated: FRONT and BACK both carry dimensions.
            var occupied=new List<(double L,double B,double R,double T)>();
            foreach(Member m in members.OrderBy(x=>x.Key,
                StringComparer.OrdinalIgnoreCase))
            {
                if(!SupportOrthoNativeDimensionPolicy48.AcceptMember(m))
                {
                    skipped.Add(c.Name+"/"+m.Key+
                        ":XREF_OR_NONNATIVE_DIMENSION_SOURCE_EXCLUDED");
                    continue;
                }
                if(!Finite(m))
                {
                    skipped.Add(c.Name+"/"+m.Key+":INVALID_SOURCE_EXTENTS");
                    continue;
                }
                var corners=WorldCorners(m);
                var pp=corners.Select(p=>
                    SupportOrthoMemberCalloutPlan17.Project(
                        c,p.X,p.Y,p.Z)).ToArray();
                double x0=pp.Min(p=>p.X),x1=pp.Max(p=>p.X);
                double y0=pp.Min(p=>p.Y),y1=pp.Max(p=>p.Y);
                if(!double.IsFinite(x0)||!double.IsFinite(x1)||
                   !double.IsFinite(y0)||!double.IsFinite(y1))
                {
                    skipped.Add(c.Name+"/"+m.Key+":PROJECTED_EXTENTS_INVALID");
                    continue;
                }
                if(x0<frameLeft-.05||x1>frameRight+.05||
                   y0<frameBottom-.05||y1>frameTop+.05)
                {
                    skipped.Add(c.Name+"/"+m.Key+
                        ":MEMBER_NOT_FULLY_IN_NATIVE_VIEW");
                    continue;
                }
                // An actual zero or unresolvably small projected span
                // cannot be dimensioned as a meaningful linear length.
                // NOTE: these represent paper-projected overall envelopes,
                // NOT fabrication/member centerline lengths.
                foreach(bool horizontal in new[]{true,false})
                {
                    double span=horizontal?x1-x0:y1-y0;
                    string axis=horizontal?"HORIZONTAL":"VERTICAL";
                    // 50 mm at 1:30 is only 1.67 mm on paper.
                    // A real narrow member must not be silently omitted.
                    if(span<.25)
                    {
                        skipped.Add(c.Name+"/"+m.Key+":"+axis+
                            "_SPAN_TOO_SMALL_ON_PAPER");
                        continue;
                    }
                    double mm=span/c.Scale*unitsToMm;
                    if(!double.IsFinite(mm)||mm<.1)
                    {
                        skipped.Add(c.Name+"/"+m.Key+":"+axis+
                            "_MODEL_PROJECTION_INVALID");
                        continue;
                    }
                    string measure=mm.ToString(
                        "0.###",CultureInfo.InvariantCulture);
                    double axisCenter=horizontal?(x0+x1)*.5:(y0+y1)*.5;
                    double midOther=horizontal?(y0+y1)*.5:(x0+x1)*.5;
                    double textFootprint=Math.Max(5.0,
                        measure.Length*1.9+3);
                    bool placed=false;
                    double lineX=0,lineY=0;
                    // Try both sides and several lanes, not a single fixed
                    // dimension line. A crowding skip is still preferable
                    // to drawing a dimension across another annotation.
                    double firstSign=midOther>=
                        (horizontal?c.Y:c.X)?1:-1;
                    foreach(double offset in new[]{
                        3.2,5.9,8.6,11.3,14.0,16.7,19.4,
                        22.1,24.8,27.5,30.2,32.9})
                    {
                        foreach(double sign in new[]{firstSign,-firstSign})
                        {
                            var position=horizontal?
                                (X:axisCenter,Y:sign>0?y1+offset:y0-offset):
                                (X:sign>0?x1+offset:x0-offset,Y:axisCenter);
                            double l,b,r,t;
                            if(horizontal)
                            {
                                l=axisCenter-Math.Max(span,textFootprint)*.5-1;
                                r=axisCenter+Math.Max(span,textFootprint)*.5+1;
                                b=position.Y-1.75;
                                t=position.Y+1.75;
                            }
                            else
                            {
                                l=position.X-1.75;
                                r=position.X+1.75;
                                b=axisCenter-
                                    Math.Max(span,textFootprint)*.5-1;
                                t=axisCenter+
                                    Math.Max(span,textFootprint)*.5+1;
                            }
                            if(l<laneLeft||r>laneRight||
                                b<laneBottom||t>laneTop||
                               l<0||r>paperW*.78||
                               b<paperH*.12||t>paperH*.94)
                                continue;
                            if(occupied.Any(o=>
                                l<o.R&&r>o.L&&b<o.T&&t>o.B))
                                continue;
                            lineX=position.X;
                            lineY=position.Y;
                            occupied.Add((l,b,r,t));
                            placed=true;
                            break;
                        }
                        if(placed)break;
                    }
                    if(!placed)
                    {
                        skipped.Add(c.Name+"/"+m.Key+":"+axis+
                            "_NO_SAFE_DIMLINE_LANE");
                        continue;
                    }
                    // Witness points are on the projected overall
                    // envelope edges. RotatedDimension is a real AutoCAD
                    // DIMLINEAR; its text override is WCS-projection in mm,
                    // not the raw paper-sheet distance in millimeters.
                    double ax=horizontal?x0:x0;
                    double ay=horizontal?y0:y0;
                    double bx=horizontal?x1:x0;
                    double by=horizontal?y0:y1;
                    result.Add(new SupportOrthoDimLinearPlan41.Dimension(
                        c.Name,m.Key+"/"+(horizontal?"H":"V"),m.Kind,
                        horizontal,ax,ay,bx,by,lineX,lineY,measure,
                        "PROJECTED_OVERALL_VIEW_ENVELOPE",null,null,
                        SupportOrthoDynamicDimension492
                            .ModelMillimetersPerPaperUnit(
                                unitsToMm,c.Scale)));
                }
            }
        }
        return new Result(result,skipped,members.Count,axial,
            members.Count*axial*2);
    }

    private static bool Finite(Member m) =>
        !string.IsNullOrWhiteSpace(m.Key)&&
        (m.Kind=="STRUCTURE"||m.Kind=="SUPPORT")&&
        double.IsFinite(m.MinX)&&double.IsFinite(m.MinY)&&
        double.IsFinite(m.MinZ)&&double.IsFinite(m.MaxX)&&
        double.IsFinite(m.MaxY)&&double.IsFinite(m.MaxZ)&&
        m.MinX<=m.MaxX&&m.MinY<=m.MaxY&&m.MinZ<=m.MaxZ;

    private static (double X,double Y,double Z)[] WorldCorners(Member m)
    {
        var p=new (double X,double Y,double Z)[8];
        int i=0;
        foreach(double x in new[]{m.MinX,m.MaxX})
            foreach(double y in new[]{m.MinY,m.MaxY})
                foreach(double z in new[]{m.MinZ,m.MaxZ})
                    p[i++]=(x,y,z);
        return p;
    }
}
