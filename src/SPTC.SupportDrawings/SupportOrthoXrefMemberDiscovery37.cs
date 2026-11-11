using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.37: Read-only discovery of actual native StructureMember and explicit
/// Pipe/Plant Support objects from resolved nested XREF databases.
/// XREF drawing names are taken from the actual Database.Filename (not a
/// block reference name, inferred model name or host DWG).
///
/// Never writes model, XREF, PnP or project data. This supplements the existing
/// native steel/support/pipe readers without changing their discovery rules.
/// </summary>
internal static class SupportOrthoXrefMemberDiscovery37
{
    internal sealed record Item(string Key, string Kind, string DrawingName,
        string DrawingPath, BoundingBox3 WorldBounds,
        ObjectId Id,Matrix3d ToHost);

    internal sealed record Result(IReadOnlyList<Item> Items,
        IReadOnlyList<string> Diagnostics);

    private const int MaxItems=64;

    internal static Result Scan(Database host,BoundingBox3 selected,double unitsToMm)
    {
        var members=new List<Item>();
        var diagnostics=new List<string>();
        if(!double.IsFinite(unitsToMm)||unitsToMm<=0)
            throw new ArgumentOutOfRangeException(nameof(unitsToMm));
        // Use the same support-centered envelope as native/XREF pipe discovery.
        // This does NOT include entire distant XREF models.
        double xy=(Math.Clamp(Math.Min(selected.SizeX,selected.SizeY)*
            unitsToMm*.12,50.0,160.0)+250.0)/unitsToMm;
        double z=(Math.Clamp(selected.SizeZ*unitsToMm*.45,
            300.0,1200.0)+250.0)/unitsToMm;
        var region=new BoundingBox3(
            selected.MinX-xy,selected.MinY-xy,selected.MinZ-z,
            selected.MaxX+xy,selected.MaxY+xy,selected.MaxZ+z);
        try
        {
            using Transaction tr=host.TransactionManager.StartTransaction();
            var blocks=(BlockTable)tr.GetObject(host.BlockTableId,OpenMode.ForRead);
            var model=(BlockTableRecord)tr.GetObject(
                blocks[BlockTableRecord.ModelSpace],OpenMode.ForRead);
            Walk(host,tr,model,Matrix3d.Identity,"MODEL",false,"",
                0,new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                region,members,diagnostics);
        }
        catch(System.Exception ex)
        {
            diagnostics.Add("XREF_MEMBER_SCAN_FAILED="+
                ex.GetType().Name+":"+ex.Message);
        }
        var keep=members.OrderBy(x=>DistanceSquared(x.WorldBounds,selected))
            .ThenBy(x=>x.Key,StringComparer.OrdinalIgnoreCase)
            .Take(MaxItems).ToArray();
        if(members.Count>MaxItems)
            diagnostics.Add("XREF_MEMBER_DISCOVERY_CAPPED="+
                MaxItems+" OF "+members.Count);
        diagnostics.Add("XREF_MEMBER_NEARBY_COUNT="+keep.Length);
        return new Result(keep,diagnostics);
    }

    private static void Walk(Database db,Transaction transaction,
        BlockTableRecord record,Matrix3d toHost,string path,bool fromXref,
        string sourceFile,int depth,HashSet<string> ancestors,
        BoundingBox3 region,List<Item> found,List<string> notes)
    {
        if(depth>12)
        {
            notes.Add("XREF_MEMBER_DEPTH_LIMIT="+path);
            return;
        }
        foreach(ObjectId id in record)
        {
            Entity? entity;
            try { entity=transaction.GetObject(id,OpenMode.ForRead,false) as Entity; }
            catch { continue; }
            if(entity==null)continue;
            if(entity is BlockReference br)
            {
                try
                {
                    var definition=transaction.GetObject(
                        br.BlockTableRecord,OpenMode.ForRead,false)
                        as BlockTableRecord;
                    if(definition==null)continue;
                    Matrix3d next=toHost*br.BlockTransform;
                    if(definition.IsFromExternalReference||
                       definition.IsFromOverlayReference)
                    {
                        Database xdb=definition.GetXrefDatabase(false);
                        if(xdb==null)
                        {
                            notes.Add("XREF_MEMBER_UNRESOLVED="+definition.Name);
                            continue;
                        }
                        string filename=xdb.Filename??"";
                        if(string.IsNullOrWhiteSpace(filename))
                        {
                            notes.Add("XREF_MEMBER_SOURCE_PATH_UNKNOWN="+definition.Name);
                            continue;
                        }
                        string cycle=filename+"|"+definition.Name;
                        if(ancestors.Contains(cycle))
                        {
                            notes.Add("XREF_MEMBER_CYCLE_SKIPPED="+cycle);
                            continue;
                        }
                        var nextAncestors=new HashSet<string>(ancestors,
                            StringComparer.OrdinalIgnoreCase){cycle};
                        using Transaction xt=xdb.TransactionManager.StartTransaction();
                        var xb=(BlockTable)xt.GetObject(xdb.BlockTableId,OpenMode.ForRead);
                        var xm=(BlockTableRecord)xt.GetObject(
                            xb[BlockTableRecord.ModelSpace],OpenMode.ForRead);
                        Walk(xdb,xt,xm,next,path+"/XREF:"+br.Handle+":"+
                            definition.Name,true,filename,depth+1,
                            nextAncestors,region,found,notes);
                    }
                    else
                    {
                        string token=db.Filename+"|BLOCK|"+definition.Handle;
                        if(ancestors.Contains(token))continue;
                        var nextAncestors=new HashSet<string>(ancestors,
                            StringComparer.OrdinalIgnoreCase){token};
                        Walk(db,transaction,definition,next,path+"/BLOCK:"+
                            br.Handle,fromXref,sourceFile,depth+1,
                            nextAncestors,region,found,notes);
                    }
                }
                catch(System.Exception ex)
                {
                    notes.Add("XREF_MEMBER_REFERENCE_SKIPPED="+path+"/"+
                        br.Handle+":"+ex.GetType().Name+":"+ex.Message);
                }
                continue;
            }
            if(!fromXref)continue;
            string type=entity.GetType().Name;
            string rx="";
            try {rx=id.ObjectClass?.Name??"";} catch { }
            string kind;
            if(type.Contains("StructureMember",StringComparison.OrdinalIgnoreCase)||
               rx.Contains("StructureMember",StringComparison.OrdinalIgnoreCase))
                kind="STRUCTURE";
            else if(SupportOrthoSupportLabel24.IsSupportClass(type)||
                    SupportOrthoSupportLabel24.IsSupportClass(rx)||
                    type.Contains("PipeSupport",StringComparison.OrdinalIgnoreCase)||
                    rx.Contains("PipeSupport",StringComparison.OrdinalIgnoreCase))
                kind="SUPPORT";
            else
                continue; // Never label generic solids / blocks as Plant support.
            try
            {
                Extents3d ext=entity.GeometricExtents;
                ext.TransformBy(toHost);
                var bounds=new BoundingBox3(ext.MinPoint.X,ext.MinPoint.Y,
                    ext.MinPoint.Z,ext.MaxPoint.X,ext.MaxPoint.Y,ext.MaxPoint.Z);
                if(!Intersects(bounds,region))continue;
                string drawing=Path.GetFileName(sourceFile);
                if(string.IsNullOrWhiteSpace(drawing))continue;
                string key="XREF_MEMBER:"+path+"/"+id.Handle;
                found.Add(new Item(key,kind,drawing,sourceFile,bounds,id,toHost));
            }
            catch(System.Exception ex)
            {
                notes.Add("XREF_MEMBER_EXTENTS_SKIPPED="+path+"/"+id.Handle+
                    ":"+ex.GetType().Name);
            }
        }
    }

    internal static Item[] Inside(IReadOnlyList<Item> items,BoundingBox3 cube) =>
        items.Where(x=>Intersects(x.WorldBounds,cube)).ToArray();

    internal static (double X,double Y,double Z) ClippedCenter(
        BoundingBox3 member,BoundingBox3 cube) =>
        ((Math.Max(member.MinX,cube.MinX)+Math.Min(member.MaxX,cube.MaxX))*.5,
         (Math.Max(member.MinY,cube.MinY)+Math.Min(member.MaxY,cube.MaxY))*.5,
         (Math.Max(member.MinZ,cube.MinZ)+Math.Min(member.MaxZ,cube.MaxZ))*.5);

    internal static bool Intersects(BoundingBox3 a,BoundingBox3 b) =>
        a.MaxX>=b.MinX && a.MinX<=b.MaxX &&
        a.MaxY>=b.MinY && a.MinY<=b.MaxY &&
        a.MaxZ>=b.MinZ && a.MinZ<=b.MaxZ;

    private static double DistanceSquared(BoundingBox3 a,BoundingBox3 b)
    {
        double ax=(a.MinX+a.MaxX)*.5,ay=(a.MinY+a.MaxY)*.5,
            az=(a.MinZ+a.MaxZ)*.5;
        double bx=(b.MinX+b.MaxX)*.5,by=(b.MinY+b.MaxY)*.5,
            bz=(b.MinZ+b.MaxZ)*.5;
        double x=ax-bx,y=ay-by,z=az-bz;
        return x*x+y*y+z*z;
    }
}
