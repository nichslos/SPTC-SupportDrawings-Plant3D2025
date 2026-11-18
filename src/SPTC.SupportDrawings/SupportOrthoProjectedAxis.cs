using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

// Paper-space projection of the two declared endpoints of a selected native member.
internal readonly record struct SupportOrthoProjectedAxis(
    (double X,double Y) Start,(double X,double Y) End)
{
    internal const string Provenance="PROJECTED_NATIVE_ENDPOINT_SPAN_NOT_CUT_LENGTH";
    internal double Dx => End.X-Start.X;
    internal double Dy => End.Y-Start.Y;
    internal double X => Start.X+Dx/2;
    internal double Y => Start.Y+Dy/2;

    // Ignore only floating-point noise from projecting an axial view.
    internal static double ProjectionTolerance(double span) => 1e-9*Math.Max(1,span);

    internal bool IsOblique =>
        Math.Min(Math.Abs(Dx),Math.Abs(Dy)) >
        ProjectionTolerance(Math.Max(Math.Abs(Dx),Math.Abs(Dy)));

    internal readonly record struct Span(
        double From,double To,double FirstWitness,double LastWitness);

    internal Span Along(bool horizontal)
    {
        var (a,wa)=horizontal?Start:(Start.Y,Start.X);
        var (b,wb)=horizontal?End:(End.Y,End.X);
        return a<=b?new(a,b,wa,wb):new(b,a,wb,wa);
    }

    internal static Dictionary<string,SupportOrthoProjectedAxis> InView(
        SupportOrthoMemberCalloutPlan17.Camera camera,
        IReadOnlyList<SupportOrthoDimLinearPlan41.Segment>? sources,
        IEnumerable<(SupportOrthoAllViewDimPlan44.Member Member,
            SupportOrthoSmartDimPlan46.Rect Bounds)> visible)
    {
        var result=new Dictionary<string,SupportOrthoProjectedAxis>(
            StringComparer.OrdinalIgnoreCase);
        if(sources==null||sources.Count==0)return result;
        var members=visible.ToDictionary(x=>x.Member.Key,
            StringComparer.OrdinalIgnoreCase);
        foreach(var source in sources)
        {
            if(!SupportOrthoNativeDimensionPolicy48.AcceptSegment(source)||
                !members.TryGetValue(source.Key,out var owner)||
                source.Kind!=owner.Member.Kind)continue;
            var a=SupportOrthoMemberCalloutPlan17.Project(camera,
                source.Ax,source.Ay,source.Az);
            var b=SupportOrthoMemberCalloutPlan17.Project(camera,
                source.Bx,source.By,source.Bz);
            bool Inside((double X,double Y) pt) =>
                pt.X>=owner.Bounds.L-.25&&pt.X<=owner.Bounds.R+.25&&
                pt.Y>=owner.Bounds.B-.25&&pt.Y<=owner.Bounds.T+.25;
            if(Inside(a)&&Inside(b))result[source.Key]=new(a,b);
        }
        return result;
    }
}
