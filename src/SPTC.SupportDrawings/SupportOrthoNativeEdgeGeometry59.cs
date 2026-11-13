using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.59 read-only source evidence: an actual straight edge supplied
/// by a vetted HOST-DWG native StructureMember or Plant Support.
/// A projected bounding box never becomes an 'exact' profile edge.
/// AutoCAD solid/BREP edges and unexposed Plant custom objects are
/// deliberately not inferred from their GeometricExtents.
/// </summary>
public static class SupportOrthoNativeEdgeGeometry59
{
    public sealed record Edge(
        string Key,string Kind,
        double Ax,double Ay,double Az,
        double Bx,double By,double Bz,
        string Provenance="NATIVE_EXPLICIT_STRAIGHT_EDGE");

    public sealed record Witness(
        double From,double To,double Transverse,
        string SourceKey,string Provenance);

    public static bool Valid(Edge edge)
    {
        if(edge==null||
           !SupportOrthoNativeDimensionPolicy48.IsNativeKey(edge.Key)||
           (edge.Kind!="STRUCTURE"&&edge.Kind!="SUPPORT")||
           edge.Provenance!="NATIVE_EXPLICIT_STRAIGHT_EDGE")return false;
        var a=new[]{edge.Ax,edge.Ay,edge.Az,
            edge.Bx,edge.By,edge.Bz};
        if(!a.All(double.IsFinite))return false;
        double squared=Math.Pow(edge.Ax-edge.Bx,2)+
            Math.Pow(edge.Ay-edge.By,2)+
            Math.Pow(edge.Az-edge.Bz,2);
        return double.IsFinite(squared)&&squared>1e-12;
    }

    /// <summary>
    /// An actual straight source edge must span BOTH projected profile
    /// extremities at one common transverse location. Reject edges
    /// merely touching a bounding rectangle at one corner, and reject
    /// sloping/oblique edges for an H/V projected profile WIDTH.
    /// Coordinates and tolerance are in paper millimetres.
    /// </summary>
    public static Witness? Resolve(
        SupportOrthoMemberCalloutPlan17.Camera camera,
        string nativeMemberKey,bool horizontal,
        SupportOrthoSmartDimPlan46.Rect memberBounds,
        IReadOnlyList<Edge>? available)
    {
        if(!SupportOrthoNativeDimensionPolicy48.IsNativeKey(
                nativeMemberKey)||
           !memberBounds.IsFinite||available==null)return null;
        double lo=horizontal?memberBounds.L:memberBounds.B;
        double hi=horizontal?memberBounds.R:memberBounds.T;
        if(hi-lo<=.20)return null;
        const double tolerance=.22; // paper mm, not a model dimension
        const double planeTolerance=.12;
        var candidates=new List<Witness>();
        foreach(var edge in available)
        {
            if(!Valid(edge)||
               !edge.Key.Equals(nativeMemberKey,
                   StringComparison.OrdinalIgnoreCase))continue;
            var a=SupportOrthoMemberCalloutPlan17.Project(camera,
                edge.Ax,edge.Ay,edge.Az);
            var b=SupportOrthoMemberCalloutPlan17.Project(camera,
                edge.Bx,edge.By,edge.Bz);
            if(!new[]{a.X,a.Y,b.X,b.Y}.All(double.IsFinite))
                continue;
            double from=horizontal?Math.Min(a.X,b.X):
                Math.Min(a.Y,b.Y);
            double to=horizontal?Math.Max(a.X,b.X):
                Math.Max(a.Y,b.Y);
            double orthA=horizontal?a.Y:a.X;
            double orthB=horizontal?b.Y:b.X;
            if(Math.Abs(from-lo)>tolerance||
               Math.Abs(to-hi)>tolerance||
               Math.Abs(orthA-orthB)>planeTolerance)
                continue;
            double transverse=(orthA+orthB)*.5;
            double bmin=horizontal?memberBounds.B:memberBounds.L;
            double bmax=horizontal?memberBounds.T:memberBounds.R;
            if(transverse<bmin-tolerance||
               transverse>bmax+tolerance)
                continue;
            // Actual world endpoints create this paper witness, not
            // a fabricated projection from unrelated AABB corners.
            candidates.Add(new Witness(from,to,transverse,
                nativeMemberKey,edge.Provenance));
        }
        if(candidates.Count==0)return null;
        // Favor a real section edge closest to the MEMBER's
        // projected centerline (stable even if a flange is sloped).
        double mid=horizontal?
            (memberBounds.B+memberBounds.T)*.5:
            (memberBounds.L+memberBounds.R)*.5;
        return candidates.OrderBy(x=>Math.Abs(x.Transverse-mid))
            .ThenBy(x=>x.Transverse).First();
    }
}
