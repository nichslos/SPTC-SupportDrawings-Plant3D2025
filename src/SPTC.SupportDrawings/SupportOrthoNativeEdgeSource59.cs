using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.59 read-only native edge discovery. Never modify the source
/// model: Entity.Explode produces temporary in-memory DBObjects only.
/// Geometry is accepted only from handles already verified as native
/// Plant structure/support members by the existing source pipeline.
/// A Solid3d/BREP or Plant custom object that cannot expose explicit
/// straight edges is logged as UNAVAILABLE, never converted to an
/// invented profile based on its WCS bounding box.
/// </summary>
internal static class SupportOrthoNativeEdgeSource59
{
    internal sealed record Result(
        IReadOnlyList<SupportOrthoNativeEdgeGeometry59.Edge> Edges,
        IReadOnlyList<string> Diagnostics);

    private const int MaxSelectedMembers=128;
    private const int MaxTemporaryObjectsPerMember=128;
    private const int MaxEdgesPerMember=96;

    internal static Result Read(Database host,
        IEnumerable<ObjectId> selection,
        IEnumerable<SupportOrthoAllViewDimPlan44.Member> verified)
    {
        var known=verified
            .Where(SupportOrthoNativeDimensionPolicy48.AcceptMember)
            .GroupBy(m=>m.Key,StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g=>g.Key,g=>g.First().Kind,
                StringComparer.OrdinalIgnoreCase);
        var notes=new List<string>();
        var all=new List<SupportOrthoNativeEdgeGeometry59.Edge>();
        var selected=selection.Distinct().Take(MaxSelectedMembers+1)
            .ToArray();
        if(selected.Length>MaxSelectedMembers)
        {
            notes.Add("REV22_59_NATIVE_EDGE_LIMIT_EXCEEDED; "+
                "profile edges unavailable; standard extent mode retained.");
            return new Result(all,notes);
        }
        using var tr=host.TransactionManager.StartTransaction();
        foreach(var id in selected)
        {
            try
            {
                if(id.IsNull||!id.IsValid||id.IsErased)continue;
                string key="NATIVE:"+id.Handle;
                if(!known.TryGetValue(key,out string? kind))continue;
                if(tr.GetObject(id,OpenMode.ForRead,false)
                    is not Entity entity)continue;
                Extents3d envelope=entity.GeometricExtents;
                var found=new List<SupportOrthoNativeEdgeGeometry59.Edge>();
                // If entity itself is a verifiable 3D straight edge,
                // read its actual coordinates without exploding it.
                Capture(entity,key,kind,found);
                if(found.Count==0)
                {
                    var pieces=new DBObjectCollection();
                    try
                    {
                        entity.Explode(pieces);
                        if(pieces.Count>MaxTemporaryObjectsPerMember)
                        {
                            notes.Add("REV22_59_TOO_MANY_TEMP_EDGES="+key);
                            continue;
                        }
                        foreach(DBObject piece in pieces)
                        {
                            if(piece is Entity primitive)
                                Capture(primitive,key,kind,found);
                            if(found.Count>MaxEdgesPerMember)break;
                        }
                    }
                    catch(System.Exception ex)
                    {
                        notes.Add("REV22_59_EXPLODE_UNAVAILABLE="+key+
                            ":"+ex.GetType().Name);
                    }
                    finally
                    {
                        foreach(DBObject transient in pieces)
                            transient.Dispose();
                    }
                }
                if(found.Count>MaxEdgesPerMember)
                {
                    notes.Add("REV22_59_EDGE_COUNT_LIMIT="+key);
                    continue;
                }
                // Fail closed if a generated temporary edge is outside
                // the actual native entity extents (including a small
                // numerical tolerance). No cross-member edge sampling.
                double tol=Math.Max(1e-5,
                    envelope.MinPoint.DistanceTo(envelope.MaxPoint)*.001);
                var finite=found.Where(edge=>
                    SupportOrthoNativeEdgeGeometry59.Valid(edge)&&
                    Inside(edge.Ax,edge.Ay,edge.Az,envelope,tol)&&
                    Inside(edge.Bx,edge.By,edge.Bz,envelope,tol))
                    .ToArray();
                all.AddRange(finite);
                notes.Add("REV22_59_NATIVE_EDGE_EVIDENCE="+key+
                    " VALID_STRAIGHT_EDGES="+finite.Length+
                    " SOURCE="+(finite.Length>0?
                        "ENTITY_OR_TRANSIENT_PRIMITIVES":
                        "UNAVAILABLE_BREP_OR_UNSUPPORTED_SHAPE"));
            }
            catch(System.Exception ex)
            {
                notes.Add("REV22_59_MEMBER_EDGES_UNAVAILABLE="+id.Handle+
                    ":"+ex.GetType().Name);
            }
        }
        notes.Add("REV22_59_NATIVE_EXACT_EDGE_COUNT="+all.Count);
        return new Result(all,notes);
    }

    private static bool Inside(double x,double y,double z,
        Extents3d box,double tolerance) =>
        double.IsFinite(x)&&double.IsFinite(y)&&double.IsFinite(z)&&
        x>=box.MinPoint.X-tolerance&&x<=box.MaxPoint.X+tolerance&&
        y>=box.MinPoint.Y-tolerance&&y<=box.MaxPoint.Y+tolerance&&
        z>=box.MinPoint.Z-tolerance&&z<=box.MaxPoint.Z+tolerance;

    private static void Capture(DBObject obj,string key,string kind,
        List<SupportOrthoNativeEdgeGeometry59.Edge> found)
    {
        void Add(Point3d a,Point3d b)
        {
            var edge=new SupportOrthoNativeEdgeGeometry59.Edge(
                key,kind,a.X,a.Y,a.Z,b.X,b.Y,b.Z);
            if(!SupportOrthoNativeEdgeGeometry59.Valid(edge))return;
            if(found.Any(other=>
                Same(other.Ax,edge.Ax)&&Same(other.Ay,edge.Ay)&&
                Same(other.Az,edge.Az)&&Same(other.Bx,edge.Bx)&&
                Same(other.By,edge.By)&&Same(other.Bz,edge.Bz)||
                Same(other.Ax,edge.Bx)&&Same(other.Ay,edge.By)&&
                Same(other.Az,edge.Bz)&&Same(other.Bx,edge.Ax)&&
                Same(other.By,edge.Ay)&&Same(other.Bz,edge.Az)))
                return;
            found.Add(edge);
        }
        switch(obj)
        {
            case Line line:
                Add(line.StartPoint,line.EndPoint);
                break;
            case Polyline pl:
                int count=pl.NumberOfVertices;
                if(count<2||count>MaxEdgesPerMember+1)break;
                int segmentCount=pl.Closed?count:count-1;
                for(int i=0;i<segmentCount;i++)
                {
                    if(pl.GetSegmentType(i).ToString()!="Line")
                        continue; // never chord an actual curved edge
                    Add(pl.GetPoint3dAt(i),
                        pl.GetPoint3dAt((i+1)%count));
                }
                break;
            // 3D solids, regions, 3D polylines and curved profiles
            // require explicit subentity/BREP access not evidenced by
            // this SDK path. Do not invent edges for those objects.
        }
    }

    private static bool Same(double a,double b) =>
        Math.Abs(a-b)<=1e-6;
}
