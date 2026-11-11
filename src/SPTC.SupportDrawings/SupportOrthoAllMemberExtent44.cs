using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.44: independently gather all known source StructureMembers and
/// Plant Supports, including nearby resolved nested-XREF objects, for
/// view-by-view projected overall dimensions.
/// This is intentionally distinct from REV22.41's explicit true
/// member-length and REV22.43's vertical overall-height extraction.
/// Never call these world AABB sizes member cut lengths.
/// </summary>
internal static class SupportOrthoAllMemberExtent44
{
    internal sealed record Result(
        IReadOnlyList<SupportOrthoAllViewDimPlan44.Member> Members,
        IReadOnlyList<string> Diagnostics);

    internal static Result Read(Database host,
        IEnumerable<ObjectId> nativeSelection,
        IEnumerable<string> verifiedSteel,
        IEnumerable<string> verifiedSupport,
        IEnumerable<SupportOrthoXrefMemberDiscovery37.Item> loadedXrefs,
        BoundingBox3 targetCube)
    {
        var members=new List<SupportOrthoAllViewDimPlan44.Member>();
        var notes=new List<string>();
        var known=new Dictionary<string,string>(
            StringComparer.OrdinalIgnoreCase);
        foreach(string handle in verifiedSteel)
            known[handle]="STRUCTURE";
        foreach(string handle in verifiedSupport)
            known[handle]="SUPPORT";
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using(Transaction tr=host.TransactionManager.StartTransaction())
        {
            foreach(ObjectId id in nativeSelection.Distinct())
            {
                try
                {
                    if(id.IsNull||!id.IsValid||id.IsErased)continue;
                    string handle=id.Handle.ToString();
                    string key="NATIVE:"+handle;
                    if(!seen.Add(key))continue;
                    Entity? entity=tr.GetObject(id,OpenMode.ForRead,false)
                        as Entity;
                    if(entity==null)continue;
                    string type=entity.GetType().Name;
                    string rx="";
                    try {rx=id.ObjectClass?.Name??"";}catch{}
                    string kind=type.Contains("StructureMember",
                        StringComparison.OrdinalIgnoreCase) ||
                        rx.Contains("StructureMember",
                            StringComparison.OrdinalIgnoreCase)
                            ? "STRUCTURE"
                            : (SupportOrthoSupportLabel24.IsSupportClass(type)||
                               SupportOrthoSupportLabel24.IsSupportClass(rx)||
                               type.Contains("PipeSupport",
                                   StringComparison.OrdinalIgnoreCase)||
                               rx.Contains("PipeSupport",
                                   StringComparison.OrdinalIgnoreCase)
                                ? "SUPPORT" : "");
                    if(kind.Length==0)known.TryGetValue(handle,out kind);
                    if(string.IsNullOrWhiteSpace(kind))
                    {
                        notes.Add("DIM44_UNVERIFIED_NATIVE_SKIPPED="+key);
                        continue;
                    }
                    Extents3d extent=entity.GeometricExtents;
                    var bounds=FromExtents(extent);
                    if(!InsideAndFinite(bounds,targetCube,key,notes))
                        continue;
                    members.Add(ToMember(key,kind,bounds));
                }
                catch(System.Exception ex)
                {
                    notes.Add("DIM44_NATIVE_EXTENTS_SKIPPED="+id.Handle+
                        ":"+ex.GetType().Name);
                }
            }
        }
        foreach(var xref in loadedXrefs)
        {
            string key=xref.Key;
            if(!seen.Add(key))continue;
            // These bounds are already transformed to source-host WCS by
            // the loaded XREF scanner. Never use XREF-local bounds here.
            if(xref.Kind!="STRUCTURE"&&xref.Kind!="SUPPORT")continue;
            if(!InsideAndFinite(xref.WorldBounds,targetCube,key,notes))
                continue;
            members.Add(ToMember(key,xref.Kind,xref.WorldBounds));
        }
        notes.Add("DIM44_RECOGNIZED_MEMBERS="+members.Count);
        notes.Add("DIM44_NATIVE_COUNT="+
            members.Count(x=>x.Key.StartsWith("NATIVE:",
                StringComparison.Ordinal)));
        notes.Add("DIM44_XREF_COUNT="+
            members.Count(x=>x.Key.StartsWith("XREF_MEMBER:",
                StringComparison.Ordinal)));
        return new Result(members,notes);
    }

    private static BoundingBox3 FromExtents(Extents3d x) =>
        new(x.MinPoint.X,x.MinPoint.Y,x.MinPoint.Z,
            x.MaxPoint.X,x.MaxPoint.Y,x.MaxPoint.Z);

    private static SupportOrthoAllViewDimPlan44.Member ToMember(
        string key,string kind,BoundingBox3 b) =>
        new(key,kind,b.MinX,b.MinY,b.MinZ,b.MaxX,b.MaxY,b.MaxZ);

    private static bool InsideAndFinite(BoundingBox3 b,BoundingBox3 cube,
        string key,List<string> notes)
    {
        bool valid=double.IsFinite(b.MinX)&&double.IsFinite(b.MinY)&&
            double.IsFinite(b.MinZ)&&double.IsFinite(b.MaxX)&&
            double.IsFinite(b.MaxY)&&double.IsFinite(b.MaxZ)&&
            b.MinX<=b.MaxX&&b.MinY<=b.MaxY&&b.MinZ<=b.MaxZ;
        if(!valid)
        {
            notes.Add("DIM44_BAD_GEOMETRY="+key);
            return false;
        }
        if(!SupportOrthoXrefMemberDiscovery37.Intersects(b,cube))
        {
            notes.Add("DIM44_OUTSIDE_ORTHOCUBE="+key);
            return false;
        }
        return true;
    }
}
