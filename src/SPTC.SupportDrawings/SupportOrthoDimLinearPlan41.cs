using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.41 pure planning of native AutoCAD DIMLINEAR entities, using the
/// ACTUAL saved viewport camera/projection, never 3D object bounding boxes.
/// Only six orthogonal views. Sources require explicit verified endpoints.
/// Source length controls displayed millimeter dimension text; paper
/// projected endpoints control witness/dimension line placement.
/// REV22.42: compact drafting output — one representative dimension per
/// unique verified source length, physical H/V orientation and Plant member
/// category ACROSS THE ENTIRE SHEET. No repeat in opposite views or for
/// equal-size equivalent members. Source identity is retained in diagnostics.
/// Skip foreshortened/diagonal projections, crowded rectangles and duplicate
/// dimensions rather than generating misleading measurements.
/// </summary>
public static class SupportOrthoDimLinearPlan41
{
    public const string Layer="SPTC_AUTO_DIMLINEAR";
    private static readonly HashSet<string> Orthogonal=new(
        new[]{"TOP","BOTTOM","FRONT","BACK","LEFT","RIGHT"},
        StringComparer.OrdinalIgnoreCase);

    public sealed record Segment(string Key,string Kind,
        double Ax,double Ay,double Az,double Bx,double By,double Bz,
        double SourceLength,double Millimeters);
    public sealed record Dimension(
        string View,string Source,string Kind,bool Horizontal,
        double Ax,double Ay,double Bx,double By,
        double LineX,double LineY,string Measurement);
    public sealed record Result(IReadOnlyList<Dimension> Dimensions,
        IReadOnlyList<string> Skipped,int SourceCount,int OrthographicViews);

    public static Result Build(
        IReadOnlyList<SupportOrthoMemberCalloutPlan17.Camera> cameras,
        IReadOnlyList<Segment> sources,
        double paperWidth,double paperHeight)
    {
        if(cameras==null||sources==null ||
           !double.IsFinite(paperWidth)||!double.IsFinite(paperHeight) ||
           paperWidth<=0||paperHeight<=0)
            throw new ArgumentException("REV22.41 invalid dimension inputs.");
        var dims=new List<Dimension>();
        var skips=new List<string>();
        // Prefer canonical views irrespective of user-selected view order.
        // TOP is favored for horizontal members, FRONT for vertical members;
        // BOTTOM/BACK are the last-resort opposing views.
        var usedSources=new Dictionary<string,string>(
            StringComparer.OrdinalIgnoreCase);
        var usedValues=new Dictionary<string,string>(
            StringComparer.OrdinalIgnoreCase);
        foreach(var camera in cameras.Where(c=>Orthogonal.Contains(c.Name))
            .OrderBy(c=>ViewPriority(c.Name)))
        {
            if(!double.IsFinite(camera.Scale)||camera.Scale<=0)
                throw new ArgumentException("REV22.41 invalid native view scale.");
            var regions=new List<(double L,double B,double R,double T)>();
            double minX=camera.X-camera.W/2+3,maxX=camera.X+camera.W/2-3;
            double minY=camera.Y-camera.H/2+3,maxY=camera.Y+camera.H/2-3;
            foreach(var source in sources.OrderBy(x=>x.Key,
                StringComparer.OrdinalIgnoreCase))
            {
                if(!Finite(source))
                {
                    skips.Add(camera.Name+"/"+source.Key+":NONFINITE_SOURCE");
                    continue;
                }
                if(source.SourceLength<=0||source.Millimeters<1)
                {
                    skips.Add(camera.Name+"/"+source.Key+":INVALID_LENGTH");
                    continue;
                }
                double wx=source.Bx-source.Ax,wy=source.By-source.Ay,
                    wz=source.Bz-source.Az;
                bool physicalH=Math.Abs(wz)<=source.SourceLength*.005;
                bool physicalV=Math.Sqrt(wx*wx+wy*wy)<=source.SourceLength*.005;
                if(!physicalH&&!physicalV)
                {
                    skips.Add(camera.Name+"/"+source.Key+":NON_HORIZONTAL_VERTICAL_MEMBER");
                    continue;
                }
                // Millimeter values are rounded exactly as they will appear
                // in the native DIMLINEAR text (0.001 mm display precision).
                // Do not collapse H and V or different Plant object kinds.
                string measurement=source.Millimeters.ToString(
                    "0.###",CultureInfo.InvariantCulture);
                string valueKey=(physicalV?"V":"H")+"|"+
                    source.Kind+"|"+measurement;
                if(usedSources.TryGetValue(source.Key,out string? firstView))
                {
                    skips.Add(camera.Name+"/"+source.Key+
                        ":SOURCE_ALREADY_DIMENSIONED_IN_"+firstView);
                    continue;
                }
                if(usedValues.TryGetValue(valueKey,out string? representative))
                {
                    skips.Add(camera.Name+"/"+source.Key+
                        ":EQUAL_LENGTH_ALREADY_DIMENSIONED_BY_"+representative);
                    continue;
                }
                var a=SupportOrthoMemberCalloutPlan17.Project(camera,
                    source.Ax,source.Ay,source.Az);
                var b=SupportOrthoMemberCalloutPlan17.Project(camera,
                    source.Bx,source.By,source.Bz);
                if(a.X<minX||a.X>maxX||b.X<minX||b.X>maxX ||
                   a.Y<minY||a.Y>maxY||b.Y<minY||b.Y>maxY)
                {
                    skips.Add(camera.Name+"/"+source.Key+":OUTSIDE_NATIVE_VIEWPORT");
                    continue;
                }
                double dx=b.X-a.X,dy=b.Y-a.Y;
                double projected=Math.Sqrt(dx*dx+dy*dy);
                if(projected<10.0)
                {
                    skips.Add(camera.Name+"/"+source.Key+":TOO_SHORT_ON_PAPER");
                    continue;
                }
                double seenLength=projected/camera.Scale;
                if(Math.Abs(seenLength-source.SourceLength)>
                   Math.Max(.03,source.SourceLength*.015))
                {
                    skips.Add(camera.Name+"/"+source.Key+":FORESHORTENED_PROJECTION");
                    continue;
                }
                bool horizontal=Math.Abs(dy)<=Math.Abs(dx)*.015;
                bool vertical=Math.Abs(dx)<=Math.Abs(dy)*.015;
                if(!horizontal&&!vertical)
                {
                    skips.Add(camera.Name+"/"+source.Key+":NON_AXIAL_PAPER_DIRECTION");
                    continue;
                }

                // Prefer dimension lines away from the view center.
                // A second side is attempted only if the first is off-frame.
                double x=0,y=0;
                var centerX=(a.X+b.X)*.5;
                var centerY=(a.Y+b.Y)*.5;
                bool placed=false;
                foreach(double sign in new[]{
                    horizontal?(centerY>=camera.Y?1:-1):
                        (centerX>=camera.X?1:-1),
                    horizontal?(centerY>=camera.Y?-1:1):
                        (centerX>=camera.X?-1:1)})
                {
                    if(horizontal)
                    {
                        x=centerX;
                        y=centerY+sign*6.5;
                    }
                    else
                    {
                        x=centerX+sign*6.5;
                        y=centerY;
                    }
                    if(horizontal && (y<minY+3||y>maxY-3))continue;
                    if(vertical && (x<minX+3||x>maxX-3))continue;
                    double left=Math.Min(Math.Min(a.X,b.X),x)-2.8;
                    double right=Math.Max(Math.Max(a.X,b.X),x)+2.8;
                    double bottom=Math.Min(Math.Min(a.Y,b.Y),y)-2.8;
                    double top=Math.Max(Math.Max(a.Y,b.Y),y)+2.8;
                    if(left<minX||right>maxX||bottom<minY||top>maxY)
                        continue;
                    if(left<0||right>paperWidth*.78 ||
                       bottom<paperHeight*.12||top>paperHeight*.94)
                        continue;
                    if(regions.Any(o=>left<o.R&&right>o.L &&
                        bottom<o.T&&top>o.B))continue;
                    regions.Add((left,bottom,right,top));
                    placed=true;
                    break;
                }
                if(!placed)
                {
                    skips.Add(camera.Name+"/"+source.Key+":NO_NONOVERLAPPING_DIMLINE");
                    continue;
                }
                // Record only successfully placed dimensions; a member
                // skipped in TOP can still be dimensioned in another view.
                // Model lengths, member types, and source IDs remain exact.
                dims.Add(new Dimension(camera.Name,source.Key,source.Kind,
                    horizontal,a.X,a.Y,b.X,b.Y,x,y,measurement));
                usedSources[source.Key]=camera.Name;
                usedValues[valueKey]=camera.Name+"/"+source.Key;
            }
        }
        return new Result(dims,skips,sources.Count,
            cameras.Count(c=>Orthogonal.Contains(c.Name)));
    }

    private static int ViewPriority(string name) =>
        name.ToUpperInvariant() switch
        {
            "TOP"=>0,
            "FRONT"=>1,
            "LEFT"=>2,
            "RIGHT"=>3,
            "BACK"=>4,
            "BOTTOM"=>5,
            _=>int.MaxValue
        };

    private static bool Finite(Segment s) =>
        double.IsFinite(s.Ax)&&double.IsFinite(s.Ay)&&
        double.IsFinite(s.Az)&&double.IsFinite(s.Bx)&&
        double.IsFinite(s.By)&&double.IsFinite(s.Bz)&&
        double.IsFinite(s.SourceLength)&&double.IsFinite(s.Millimeters);
}
