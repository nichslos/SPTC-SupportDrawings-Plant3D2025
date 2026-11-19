using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.BoundaryRepresentation;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Endpoints=SPTC.SupportDrawings.SupportOrthoSolidEndStations;

namespace SPTC.SupportDrawings;

internal static class SupportOrthoPlantSolidEndpoints
{
    private static Endpoints.Point Point(Point3d p)=>new(p.X,p.Y,p.Z);

    internal static Endpoints.Pair? Read(Entity member,Point3d? start,Point3d? end,
        out string reason)
    {
        // Some Plant wrappers expose their steel as a single transient 3D solid.
        // Traverse only the selected native entity, never another database object.
        var pieces=new DBObjectCollection();
        try
        {
            var gripPoints=new Point3dCollection();
            var modes=new IntegerCollection();
            var ids=new IntegerCollection();
            try {member.GetGripPoints(gripPoints,modes,ids);}
            catch(Autodesk.AutoCAD.Runtime.Exception) {gripPoints.Clear();}
            Endpoints.Pair? declared=start.HasValue&&end.HasValue
                ?new(Point(start.Value),Point(end.Value)):null;
            var bounds=member.GeometricExtents;
            double tol=Math.Max(1e-10,bounds.MinPoint.DistanceTo(bounds.MaxPoint)*1e-8);
            var grips=gripPoints.Cast<Point3d>().Where(p=>
                p.X>=bounds.MinPoint.X-tol&&p.X<=bounds.MaxPoint.X+tol&&
                p.Y>=bounds.MinPoint.Y-tol&&p.Y<=bounds.MaxPoint.Y+tol&&
                p.Z>=bounds.MinPoint.Z-tol&&p.Z<=bounds.MaxPoint.Z+tol).Select(Point).ToArray();
            if(start is {} a&&end is {} b&&declared is {} axis&&grips.Length>=2&&
                Endpoints.CrossesBounds(axis,Point(bounds.MinPoint),Point(bounds.MaxPoint),tol)&&
                TryAlignedGrips(member,a,b,grips,tol) is {} nativeGrips)
            {
                reason=SupportOrthoNativeDimensionPolicy48.EndGrips;
                return nativeGrips;
            }
            List<Endpoints.Edge> edges;
            try {edges=ReadEdges(member);}
            catch(System.Exception) {edges=new();}
            if(edges.Count==0)
            {
                member.Explode(pieces);
                var entities=pieces.OfType<Entity>().ToArray();
                if(entities.Length!=1||entities[0] is not Solid3d solid)
                {
                    reason="NO_SINGLE_NATIVE_SOLID";
                    return null;
                }
                edges=ReadEdges(solid);
            }
            return Endpoints.Resolve(edges,declared,grips,out reason);
        }
        catch(System.Exception ex)
        {
            reason="NATIVE_SOLID_UNAVAILABLE_"+ex.GetType().Name;
            return null;
        }
        finally
        {
            foreach(DBObject piece in pieces)piece.Dispose();
            pieces.Dispose();
        }
    }

    private static Endpoints.Pair? TryAlignedGrips(Entity member,Point3d a,Point3d b,
        Endpoints.Point[] grips,double tolerance)
    {
        var direction=b-a;
        if(!double.IsFinite(direction.Length)||direction.Length<=tolerance)return null;
        try
        {
            var u=direction.GetNormal();
            var v=u.GetPerpendicularVector().GetNormal();
            var worldToMember=Matrix3d.AlignCoordinateSystem(a,u,v,u.CrossProduct(v),
                Point3d.Origin,Vector3d.XAxis,Vector3d.YAxis,Vector3d.ZAxis);
            // Recompute geometric extents on the actual transformed entity.
            // Transforming the corners of its WCS box would enlarge oblique spans.
            using var copy=member.GetTransformedCopy(worldToMember);
            var bounds=copy.GeometricExtents;
            if(bounds.MaxPoint.X-bounds.MinPoint.X<=tolerance)return null;
            return Endpoints.EndGrips(grips,Point(a),new(u.X,u.Y,u.Z),
                bounds.MinPoint.X,bounds.MaxPoint.X,tolerance);
        }
        catch(System.Exception) {return null;}
    }

    private static List<Endpoints.Edge> ReadEdges(Entity entity)
    {
        var edges=new List<Endpoints.Edge>();
        using var brep=new Brep(entity);
        foreach(var edge in brep.Edges)
        using(edge)
        using(var a=edge.Vertex1)
        using(var b=edge.Vertex2)
        using(var curve=edge.Curve)
        {
            bool straight=curve is LinearEntity3d||
                curve is ExternalCurve3d external&&(external.IsLineSegment||external.IsLine);
            edges.Add(new(Point(a.Point),Point(b.Point),straight));
        }
        return edges;
    }

}
