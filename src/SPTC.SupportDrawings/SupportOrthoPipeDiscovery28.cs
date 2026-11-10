using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.28 isolated, READ-ONLY discovery of native and loaded, nested XREF pipes.
/// A true native Pipe ObjectId is required. No exploded proxies, inferred line tags,
/// inferred BOP or direct PnP database writes. All output bounds are in host WCS.
/// Prior REV22.27 source and command remain unchanged.
/// </summary>
internal static class SupportOrthoPipeDiscovery28
{
    internal sealed record Candidate(ObjectId Id, string Key, string Source,
        bool FromXref, Matrix3d ToHost, BoundingBox3 WorldBounds);

    internal sealed record Result(IReadOnlyList<Candidate> Candidates,
        int NativeCount, int XrefCount, IReadOnlyList<string> Diagnostics);

    internal static Result Scan(Database host, BoundingBox3 support, double toMm)
    {
        var found=new List<Candidate>();
        var diagnostics=new List<string>();
        int native=0,xref=0;
        if(toMm<=0 || !double.IsFinite(toMm))
            throw new ArgumentException("REV22.28 source drawing units invalid.");

        // Scan only a small support-centered envelope. The later OrthoCube
        // clips long pipes, never extending the XY footprint to route endpoints.
        double xy=(Math.Clamp(Math.Min(support.SizeX,support.SizeY)*toMm*
            0.12,50.0,160.0)+250.0)/toMm;
        double z=(Math.Clamp(support.SizeZ*toMm*0.45,
            300.0,1200.0)+250.0)/toMm;
        var region=new BoundingBox3(support.MinX-xy,support.MinY-xy,
            support.MinZ-z,support.MaxX+xy,support.MaxY+xy,
            support.MaxZ+z);
        try
        {
            using var tr=host.TransactionManager.StartTransaction();
            var bt=(BlockTable)tr.GetObject(host.BlockTableId,OpenMode.ForRead);
            var model=(BlockTableRecord)tr.GetObject(
                bt[BlockTableRecord.ModelSpace],OpenMode.ForRead);
            Walk(host,tr,model,Matrix3d.Identity,"MODEL",false,0,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                region,found,diagnostics,ref native,ref xref);
        }
        catch(System.Exception ex)
        {
            diagnostics.Add("REV22_28_SCAN_FAILED="+ex.GetType().Name+
                ":"+ex.Message);
        }
        return new Result(found.OrderBy(x=>x.Key,
            StringComparer.OrdinalIgnoreCase).ToArray(),
            native,xref,diagnostics);
    }

    private static void Walk(Database db,Transaction tr,
        BlockTableRecord record,Matrix3d toHost,string path,bool fromXref,
        int depth,HashSet<string> ancestors,BoundingBox3 region,
        List<Candidate> found,List<string> notes,ref int native,ref int xref)
    {
        if(depth>12){notes.Add("XREF_DEPTH_LIMIT="+path);return;}
        foreach(ObjectId id in record)
        {
            Entity? entity;
            try {entity=tr.GetObject(id,OpenMode.ForRead,false) as Entity;}
            catch {continue;}
            if(entity==null)continue;
            if(entity is BlockReference br)
            {
                try
                {
                    var def=tr.GetObject(br.BlockTableRecord,
                        OpenMode.ForRead,false) as BlockTableRecord;
                    if(def==null)continue;
                    var next=toHost*br.BlockTransform;
                    if(def.IsFromExternalReference || def.IsFromOverlayReference)
                    {
                        Database xdb=def.GetXrefDatabase(false);
                        if(xdb==null)
                        {
                            notes.Add("XREF_UNRESOLVED="+def.Name);
                            continue;
                        }
                        string key=xdb.Filename+"|"+def.Name;
                        if(ancestors.Contains(key))
                        {
                            notes.Add("XREF_CYCLE_SKIPPED="+key);
                            continue;
                        }
                        var nextAncestors=new HashSet<string>(ancestors,
                            StringComparer.OrdinalIgnoreCase){key};
                        using var xt=xdb.TransactionManager.StartTransaction();
                        var xb=(BlockTable)xt.GetObject(
                            xdb.BlockTableId,OpenMode.ForRead);
                        var xm=(BlockTableRecord)xt.GetObject(
                            xb[BlockTableRecord.ModelSpace],OpenMode.ForRead);
                        Walk(xdb,xt,xm,next,path+"/XREF:"+
                            br.Handle.ToString()+":"+def.Name,true,depth+1,
                            nextAncestors,region,found,notes,ref native,ref xref);
                    }
                    else
                    {
                        // Ordinary nested blocks can contain attached XREFs.
                        // Their own plain geometry is not mistaken for Plant pipes.
                        string token=db.Filename+"|BLOCK|"+def.Handle;
                        if(ancestors.Contains(token))continue;
                        var nextAncestors=new HashSet<string>(ancestors,
                            StringComparer.OrdinalIgnoreCase){token};
                        Walk(db,tr,def,next,path+"/BLOCK:"+
                            br.Handle.ToString(),fromXref,depth+1,
                            nextAncestors,region,found,notes,ref native,ref xref);
                    }
                }
                catch(System.Exception ex)
                {
                    notes.Add("XREF_REFERENCE_SKIPPED="+path+"/"+
                        br.Handle+":"+ex.GetType().Name+":"+ex.Message);
                }
                continue;
            }
            string cls=entity.GetType().Name;
            string rx="";
            try{rx=id.ObjectClass?.Name??"";}catch{}
            if(!SupportOrthoPipeLabel27.IsNativePipeType(cls) &&
                !SupportOrthoPipeLabel27.IsNativePipeType(rx))continue;
            // Native pipes from ordinary blocks are not first-class selectable
            // pipes in the host model and need a valid XREF source context.
            if(!fromXref && path!="MODEL")continue;
            try
            {
                Extents3d ext=entity.GeometricExtents;
                ext.TransformBy(toHost);
                var world=new BoundingBox3(ext.MinPoint.X,ext.MinPoint.Y,
                    ext.MinPoint.Z,ext.MaxPoint.X,ext.MaxPoint.Y,
                    ext.MaxPoint.Z);
                if(!Intersects(world,region))continue;
                string sourcePath=fromXref?path:"MODEL";
                string unique=(fromXref?"XREF:":"PIPE:")+
                    sourcePath+"/"+id.Handle;
                found.Add(new Candidate(id,unique,sourcePath,
                    fromXref,toHost,world));
                if(fromXref)xref++;else native++;
            }
            catch(System.Exception ex)
            {
                notes.Add("PIPE_EXTENTS_SKIPPED="+path+"/"+id.Handle+
                    ":"+ex.GetType().Name);
            }
        }
    }

    private static bool Intersects(BoundingBox3 a,BoundingBox3 b)
    {
        return a.MaxX>=b.MinX && a.MinX<=b.MaxX &&
            a.MaxY>=b.MinY && a.MinY<=b.MaxY &&
            a.MaxZ>=b.MinZ && a.MinZ<=b.MaxZ;
    }

    internal sealed record Labels(IReadOnlyList<SupportOrthoPlantPipe27.Pipe> Pipes,
        int Candidates,int LinkedRows,IReadOnlyList<string> Diagnostics);

    internal static Labels ReadXrefs(IEnumerable<Candidate> all,
        BoundingBox3 cube)
    {
        var pipes=new List<SupportOrthoPlantPipe27.Pipe>();
        var log=new List<string>();
        int candidates=0,linked=0;
        var valuesAccessor=new PnpAccessor(log);
        foreach(var c in all.Where(c=>c.FromXref))
        {
            if(!Intersects(c.WorldBounds,cube))continue;
            candidates++;
            try
            {
                using var tr=c.Id.Database.TransactionManager.StartTransaction();
                var ent=tr.GetObject(c.Id,OpenMode.ForRead,false) as Entity;
                if(ent==null)continue;
                // Query the actual referenced source entity's row id, not
                // the host drawing's BlockReference handle.
                var values=valuesAccessor.Read(ent,c.Id,out bool rowLinked);
                if(rowLinked)linked++;
                var parsed=SupportOrthoPipeLabel27.Evaluate(values);
                if(parsed.LineSource=="LINE_NUMBER_TAG_NOT_AVAILABLE" &&
                   parsed.BopSource=="BOP_NOT_AVAILABLE")
                {
                    log.Add("XREF_PIPE_METADATA_UNAVAILABLE="+c.Key);
                    continue;
                }
                string bop=parsed.BopText;
                string bopSource=parsed.BopSource;
                if(bopSource=="EXPLICIT_BOTTOM_OF_PIPE")
                {
                    string? raw=SupportOrthoSupportLabel24.Get(values,
                        "BottomOfPipe","Bottom of Pipe","BOP",
                        "BottomOfPipeElevation","Bottom of Pipe Elevation");
                    if(SupportOrthoPipeLabel27.TryElevation(raw,out double z) &&
                        TryWorldBop(c.ToHost,z,out double worldZ))
                    {
                        bop="BOP EL. "+
                            SupportOrthoVerticalMember18.FormatElevation(worldZ);
                        bopSource="XREF_EXPLICIT_BOTTOM_OF_PIPE_TRANSFORMED";
                    }
                    else
                    {
                        bop="BOP: N/A";
                        bopSource="XREF_BOP_TRANSFORM_UNSUPPORTED";
                        log.Add("XREF_BOP_TRANSFORM_UNSUPPORTED="+c.Key);
                    }
                }
                double x=(Math.Max(c.WorldBounds.MinX,cube.MinX)+
                    Math.Min(c.WorldBounds.MaxX,cube.MaxX))*0.5;
                double y=(Math.Max(c.WorldBounds.MinY,cube.MinY)+
                    Math.Min(c.WorldBounds.MaxY,cube.MaxY))*0.5;
                double zAnchor=(Math.Max(c.WorldBounds.MinZ,cube.MinZ)+
                    Math.Min(c.WorldBounds.MaxZ,cube.MaxZ))*0.5;
                pipes.Add(new SupportOrthoPlantPipe27.Pipe(c.Key,
                    parsed.LineTag,bop,parsed.LineSource,bopSource,
                    x,y,zAnchor,"XREF_WCS_BOUNDS_CLIPPED_TO_ORTHOCUBE"));
                log.Add("XREF_PIPE_ACCEPTED="+c.Key+" LINE_SOURCE="+
                    parsed.LineSource+" BOP_SOURCE="+bopSource);
            }
            catch(System.Exception ex)
            {
                log.Add("XREF_PIPE_READ_ERROR="+c.Key+":"+
                    ex.GetType().Name+":"+ex.Message);
            }
        }
        return new Labels(pipes,candidates,linked,log);
    }

    private static bool TryWorldBop(Matrix3d tx,double sourceZ,
        out double hostZ)
    {
        hostZ=0;
        // A world elevation is invariant across source X/Y positions only
        // for XY-plane-preserving transforms (including Z rotation and scale).
        Point3d o=Point3d.Origin.TransformBy(tx);
        Point3d x=new Point3d(1,0,0).TransformBy(tx);
        Point3d y=new Point3d(0,1,0).TransformBy(tx);
        Point3d z=new Point3d(0,0,1).TransformBy(tx);
        if(Math.Abs(o.Z-x.Z)>1e-7 || Math.Abs(o.Z-y.Z)>1e-7)
            return false;
        hostZ=o.Z+(z.Z-o.Z)*sourceZ;
        return double.IsFinite(hostZ);
    }

    private sealed class PnpAccessor
    {
        private readonly object? manager;
        private readonly MethodInfo? findRow,getProperties;
        private readonly List<string> notes;

        internal PnpAccessor(List<string> diagnostics)
        {
            notes=diagnostics;
            try
            {
                Type app=AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a=>a.GetType(
                        "Autodesk.ProcessPower.PlantInstance.PlantApplication",
                        throwOnError:false)).FirstOrDefault(t=>t!=null) ??
                    throw new InvalidOperationException("PlantApplication unavailable");
                object project=app.GetProperty("CurrentProject",
                    BindingFlags.Static|BindingFlags.Public)?.GetValue(null) ??
                    throw new InvalidOperationException("No active Plant project");
                object parts=project.GetType().GetProperty("ProjectParts")?
                    .GetValue(project) ??
                    throw new InvalidOperationException("ProjectParts unavailable");
                object piping=parts.GetType().GetProperty("Item")?
                    .GetValue(parts,new object[]{"Piping"}) ??
                    throw new InvalidOperationException("Piping part unavailable");
                manager=piping.GetType().GetProperty("DataLinksManager")?
                    .GetValue(piping);
                if(manager==null)
                    throw new InvalidOperationException("Piping DataLinksManager unavailable");
                findRow=manager.GetType().GetMethods().FirstOrDefault(m=>
                    m.Name=="FindAcPpRowId" &&
                    m.GetParameters().Length==1 &&
                    m.GetParameters()[0].ParameterType==typeof(ObjectId));
                getProperties=manager.GetType().GetMethods().FirstOrDefault(m=>
                    m.Name=="GetAllProperties" && m.GetParameters().Length==2 &&
                    m.GetParameters()[0].ParameterType==typeof(int) &&
                    m.GetParameters()[1].ParameterType==typeof(bool));
            }
            catch(System.Exception ex)
            {
                notes.Add("XREF_PNP_MANAGER_UNAVAILABLE="+ex.Message);
            }
        }

        internal Dictionary<string,string> Read(Entity ent,ObjectId id,
            out bool linked)
        {
            linked=false;
            var result=new Dictionary<string,string>(
                StringComparer.OrdinalIgnoreCase);
            if(manager!=null && findRow!=null && getProperties!=null)
            {
                try
                {
                    int row=Convert.ToInt32(findRow.Invoke(manager,
                        new object[]{id}));
                    if(row>0 &&
                       getProperties.Invoke(manager,
                           new object[]{row,true}) is IEnumerable pairs)
                    {
                        linked=true;
                        foreach(object? pair in pairs)
                        {
                            if(pair==null)continue;
                            string key=pair.GetType().GetProperty("Key")?
                                .GetValue(pair)?.ToString()??"";
                            string value=pair.GetType().GetProperty("Value")?
                                .GetValue(pair)?.ToString()??"";
                            if(!string.IsNullOrWhiteSpace(key))
                                result[key]=value;
                        }
                    }
                }
                catch(System.Exception ex)
                {
                    notes.Add("XREF_PNP_LOOKUP_FAILED="+id.Handle+":"+
                        ex.GetType().Name);
                }
            }
            // Some source XREF entities expose the computed properties even
            // when their row cannot be resolved from the host project's DLM.
            try
            {
                foreach(PropertyDescriptor prop in TypeDescriptor.GetProperties(ent))
                {
                    string key=SupportOrthoSupportLabel24.Normalize(
                        prop.DisplayName);
                    string internalName=SupportOrthoSupportLabel24.Normalize(
                        prop.Name);
                    if(!new[]{"LINENUMBERTAG","LINENUMBER","LINETAG",
                        "PIPINGLINENUMBERTAG","BOTTOMOFPIPE",
                        "BOTTOMOFPIPEELEVATION","BOP"}.Any(k=>
                        k==key || k==internalName))continue;
                    string value=prop.GetValue(ent)?.ToString()??"";
                    if(string.IsNullOrWhiteSpace(value))continue;
                    if(!result.TryGetValue(prop.Name,out string? prev) ||
                        string.IsNullOrWhiteSpace(prev))
                        result[prop.Name]=value;
                    if(!result.TryGetValue(prop.DisplayName,out prev) ||
                        string.IsNullOrWhiteSpace(prev))
                        result[prop.DisplayName]=value;
                }
            }
            catch(System.Exception ex)
            {
                notes.Add("XREF_PROPERTY_PALETTE_LOOKUP_FAILED="+
                    id.Handle+":"+ex.GetType().Name);
            }
            foreach(string key in new[]{
                "LineNumberTag","LineNumber","BottomOfPipe","BOP"})
            {
                try
                {
                    var property=ent.GetType().GetProperty(key,
                        BindingFlags.Public|BindingFlags.Instance|
                        BindingFlags.IgnoreCase);
                    string value=property?.GetValue(ent)?.ToString()??"";
                    if(!string.IsNullOrWhiteSpace(value) &&
                       (!result.TryGetValue(key,out string? previous) ||
                        string.IsNullOrWhiteSpace(previous)))
                        result[key]=value;
                }
                catch{}
            }
            return result;
        }
    }
}
