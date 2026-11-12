using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcadApp=Autodesk.AutoCAD.ApplicationServices.Application;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.49 automatic paper dimension refresh AFTER successful
/// PLANTORTHOUPDATE, never during native Plant's view regeneration.
/// Refresh is restricted to Ortho DWGs created with a persistently saved
/// SPTC source/viewport manifest. No DCF edits; no model/XREF edits;
/// paper-space labels, leaders and manual dimensions are preserved.
/// </summary>
internal static class SupportOrthoDimensionRefresh49
{
    private static readonly HashSet<Document> Watching=new();
    private static readonly Queue<Document> Pending=new();
    private static bool started,idleHooked,running;

    internal static void Start()
    {
        if(started)return;
        started=true;
        AcadApp.DocumentManager.DocumentCreated+=OnCreated;
        AcadApp.DocumentManager.DocumentToBeDestroyed+=OnDestroyed;
        AcadApp.DocumentManager.DocumentActivated+=OnActivated;
        foreach(Document doc in AcadApp.DocumentManager)Attach(doc);
    }

    internal static void Stop()
    {
        if(!started)return;
        started=false;
        AcadApp.DocumentManager.DocumentCreated-=OnCreated;
        AcadApp.DocumentManager.DocumentToBeDestroyed-=OnDestroyed;
        AcadApp.DocumentManager.DocumentActivated-=OnActivated;
        if(idleHooked)AcadApp.Idle-=OnIdle;
        idleHooked=false;
        Pending.Clear();
        foreach(var doc in Watching.ToArray())Detach(doc);
    }

    private static void Attach(Document doc)
    {
        if(!Watching.Add(doc))return;
        doc.CommandEnded+=OnCommandEnded;
    }
    private static void Detach(Document doc)
    {
        if(!Watching.Remove(doc))return;
        try {doc.CommandEnded-=OnCommandEnded;}catch(System.Exception){}
    }
    private static void OnCreated(object sender,DocumentCollectionEventArgs e)=>
        Attach(e.Document);
    private static void OnDestroyed(object sender,DocumentCollectionEventArgs e)=>
        Detach(e.Document);
    private static void OnActivated(object sender,
        DocumentCollectionEventArgs e)
    {
        // Deferred update may belong to a background document. Resume
        // once that exact Ortho DWG becomes active, without guessing.
        if(Pending.Contains(e.Document))EnsureIdle();
    }

    private static void OnCommandEnded(object sender,CommandEventArgs e)
    {
        var name=e.GlobalCommandName?.TrimStart('_','.')??"";
        if(!name.Equals("PLANTORTHOUPDATE",StringComparison.OrdinalIgnoreCase)||
           sender is not Document doc||
           !Watching.Contains(doc))return;
        // AutoCAD raises CommandEnded once native Ortho processing is done.
        // Do not write from the event itself; schedule document-context work.
        if(!Pending.Contains(doc))Pending.Enqueue(doc);
        EnsureIdle();
    }

    private static void EnsureIdle()
    {
        if(idleHooked||running||Pending.Count==0)return;
        idleHooked=true;
        AcadApp.Idle+=OnIdle;
    }

    private static async void OnIdle(object? sender,EventArgs e)
    {
        AcadApp.Idle-=OnIdle;
        idleHooked=false;
        if(running||Pending.Count==0)return;
        Document doc=Pending.Dequeue();
        if(!Watching.Contains(doc)) {EnsureIdle();return;}
        if(!ReferenceEquals(AcadApp.DocumentManager.MdiActiveDocument,doc))
        {
            // A pending update belongs to another drawing; process it only
            // when that document is active, never against Current View.
            Pending.Enqueue(doc);
            // DocumentActivated would normally fire when it is restored.
            return;
        }
        running=true;
        try
        {
            await AcadApp.DocumentManager.ExecuteInCommandContextAsync(_=>
            {
                Refresh(doc);
                return Task.CompletedTask;
            },null);
        }
        catch(System.Exception ex)
        {
            Message(doc,"REV22.49 dimension update preserved original: "+
                ex.GetType().Name+": "+ex.Message);
        }
        finally
        {
            running=false;
            EnsureIdle();
        }
    }

    internal static void SaveInitial(Database target,string sourcePath,
        string layoutName,double unitsToMm,double paperW,double paperH,
        bool referenceDetail,
        IReadOnlyList<SupportOrthoAllViewDimPlan44.Member> nativeMembers,
        IReadOnlyDictionary<string,ObjectId> savedNativeViews,
        IReadOnlyList<SupportOrthoDimLinearPlan41.Dimension> dimensions)
    {
        if(dimensions.Count==0||nativeMembers.Count==0)return;
        string resolved=Path.GetFullPath(sourcePath);
        var memberRefs=nativeMembers
            .Where(SupportOrthoNativeDimensionPolicy48.AcceptMember)
            .Select(m=>new SupportOrthoDimensionUpdatePlan49.NativeMember(
                m.Key,m.Kind)).ToArray();
        var viewRefs=new List<SupportOrthoDimensionUpdatePlan49.View>();
        using(var tr=target.TransactionManager.StartTransaction())
        {
            foreach(var view in savedNativeViews)
            {
                if(tr.GetObject(view.Value,OpenMode.ForRead,false)
                    is not Viewport vp)
                    throw new InvalidOperationException(
                        "REV22.49 native viewport unavailable: "+view.Key);
                viewRefs.Add(new SupportOrthoDimensionUpdatePlan49.View(
                    view.Key,vp.Handle.ToString(),
                    vp.CenterPoint.X,vp.CenterPoint.Y,vp.Width,vp.Height));
            }
        }
        var manifest=new SupportOrthoDimensionUpdatePlan49.Manifest(
            SupportOrthoDimensionUpdatePlan49.ManifestVersion,
            resolved,layoutName,unitsToMm,paperW,paperH,
            referenceDetail,memberRefs,viewRefs,
            SupportOrthoDimensionUpdatePlan49.Signature(dimensions));
        SupportOrthoDimensionUpdatePlan49.Validate(manifest);
        Store(target,manifest);
    }

    private static void Store(Database db,
        SupportOrthoDimensionUpdatePlan49.Manifest manifest)
    {
        string json=JsonSerializer.Serialize(manifest);
        var chunks=Enumerable.Range(0,(json.Length+179)/180)
            .Select(i=>new TypedValue((int)DxfCode.Text,
                json.Substring(i*180,Math.Min(180,json.Length-i*180))))
            .ToArray();
        using var tr=db.TransactionManager.StartTransaction();
        var nod=(DBDictionary)tr.GetObject(
            db.NamedObjectsDictionaryId,OpenMode.ForWrite);
        Xrecord record;
        if(nod.Contains(SupportOrthoDimensionUpdatePlan49.ManifestKey))
            record=(Xrecord)tr.GetObject(
                nod.GetAt(SupportOrthoDimensionUpdatePlan49.ManifestKey),
                OpenMode.ForWrite);
        else
        {
            record=new Xrecord();
            nod.SetAt(SupportOrthoDimensionUpdatePlan49.ManifestKey,record);
            tr.AddNewlyCreatedDBObject(record,true);
        }
        record.Data=new ResultBuffer(chunks);
        tr.Commit();
    }

    private static SupportOrthoDimensionUpdatePlan49.Manifest? Read(
        Database db)
    {
        using var tr=db.TransactionManager.StartTransaction();
        var nod=(DBDictionary)tr.GetObject(
            db.NamedObjectsDictionaryId,OpenMode.ForRead);
        if(!nod.Contains(SupportOrthoDimensionUpdatePlan49.ManifestKey))
            return null; // older drawing: deliberately do not mutate it
        if(tr.GetObject(
            nod.GetAt(SupportOrthoDimensionUpdatePlan49.ManifestKey),
            OpenMode.ForRead,false) is not Xrecord record)
            return null;
        using var data=record.Data;
        if(data==null)return null;
        var json=string.Concat(data.AsArray()
            .Where(t=>t.TypeCode==(int)DxfCode.Text)
            .Select(t=>t.Value?.ToString()??""));
        var manifest=JsonSerializer.Deserialize<
            SupportOrthoDimensionUpdatePlan49.Manifest>(json);
        if(manifest!=null)
            SupportOrthoDimensionUpdatePlan49.Validate(manifest);
        return manifest;
    }

    private static void Refresh(Document target)
    {
        if(!File.Exists(target.Database.Filename))return;
        var original=Read(target.Database);
        if(original==null)return;
        if(!File.Exists(original.SourcePath))
        {
            Message(target,"REV22.49 source model missing; existing dimensions kept.");
            return;
        }

        using var source=new Database(false,true);
        // Use SAVED model coordinates. Never read unsaved transient data,
        // edit the source drawing, or open an XREF database for dimensioning.
        source.ReadDwgFile(original.SourcePath,
            FileOpenMode.OpenForReadAndAllShare,true,null);
        source.CloseInput(true);
        double actualUnits=source.Insunits.ToString() switch
        {
            "Millimeters"=>1,"Centimeters"=>10,"Meters"=>1000,
            "Inches"=>25.4,"Feet"=>304.8,_=>0
        };
        if(Math.Abs(actualUnits-original.UnitsToMm)>1e-7)
            throw new InvalidOperationException(
                "Source units changed; existing dimensions retained.");

        var sourceIds=new List<ObjectId>();
        var steel=new List<string>();
        var supports=new List<string>();
        BoundingBox3? bounds=null;
        using(var tr=source.TransactionManager.StartTransaction())
        {
            foreach(var saved in original.Members)
            {
                string key=saved.Key["NATIVE:".Length..];
                if(!long.TryParse(key,NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,out long number))
                    throw new InvalidOperationException(
                        "Saved native member handle is not hexadecimal: "+key);
                ObjectId id;
                try {id=source.GetObjectId(false,new Handle(number),0);}
                catch(Autodesk.AutoCAD.Runtime.Exception) {continue;}
                if(id.IsNull||!id.IsValid||id.IsErased||
                   tr.GetObject(id,OpenMode.ForRead,false) is not Entity ent)
                    continue;
                try
                {
                    Extents3d box=ent.GeometricExtents;
                    if(!double.IsFinite(box.MinPoint.X)||
                       !double.IsFinite(box.MaxPoint.X))continue;
                    var bb=new BoundingBox3(box.MinPoint.X,
                        box.MinPoint.Y,box.MinPoint.Z,box.MaxPoint.X,
                        box.MaxPoint.Y,box.MaxPoint.Z);
                    bounds=bounds.HasValue?bounds.Value.Include(bb):bb;
                    sourceIds.Add(id);
                    if(saved.Kind=="STRUCTURE")steel.Add(key);
                    else supports.Add(key);
                }
                catch(Autodesk.AutoCAD.Runtime.Exception){ /* source removed */ }
            }
        }
        if(!bounds.HasValue||sourceIds.Count==0)
            throw new InvalidOperationException(
                "No verified native member extents; kept existing dimensions.");
        double margin=Math.Max(10.0/original.UnitsToMm,
            Math.Max(bounds.Value.SizeX,
                Math.Max(bounds.Value.SizeY,bounds.Value.SizeZ))*.15);
        var cube=new BoundingBox3(bounds.Value.MinX-margin,
            bounds.Value.MinY-margin,bounds.Value.MinZ-margin,
            bounds.Value.MaxX+margin,bounds.Value.MaxY+margin,
            bounds.Value.MaxZ+margin);
        var extents=SupportOrthoAllMemberExtent44.Read(
            source,sourceIds,steel,supports,
            Array.Empty<SupportOrthoXrefMemberDiscovery37.Item>(),cube);
        var native=extents.Members
            .Where(SupportOrthoNativeDimensionPolicy48.AcceptMember)
            .ToArray();
        if(native.Length==0)
            throw new InvalidOperationException(
                "Cannot resolve Plant support geometry; old dimensions kept.");

        var sourceSegments=SupportOrthoDimensionSource41.Read(source,
            sourceIds,Array.Empty<SupportOrthoXrefMemberDiscovery37.Item>(),
            original.UnitsToMm,steel,supports);
        var segments=sourceSegments.Segments
            .Where(x=>SupportOrthoNativeDimensionPolicy48.IsNativeKey(x.Key)&&
                x.Provenance=="EXPLICIT_MEMBER_ENDPOINTS")
            .Select(s=>new SupportOrthoDimLinearPlan41.Segment(
                s.Key,s.Kind,s.Ax,s.Ay,s.Az,s.Bx,s.By,s.Bz,
                s.SourceLength,s.Millimeters,s.Provenance)).ToArray();

        var cameras=new List<SupportOrthoMemberCalloutPlan17.Camera>();
        var keepouts=new List<SupportOrthoSmartDimPlan46.Rect>();
        double paperW=original.PaperWidth,paperH=original.PaperHeight;
        using(var tr=target.Database.TransactionManager.StartTransaction())
        {
            var dict=(DBDictionary)tr.GetObject(
                target.Database.LayoutDictionaryId,OpenMode.ForRead);
            if(!dict.Contains(original.LayoutName))
                throw new InvalidOperationException(
                    "Ortho paper layout renamed; old dimensions kept.");
            var layout=(Layout)tr.GetObject(
                dict.GetAt(original.LayoutName),OpenMode.ForRead);
            var plotSize=layout.PlotPaperSize;
            if(plotSize.X>0&&plotSize.Y>0)
            {
                double factor=layout.PlotPaperUnits.ToString()=="Inches"
                    ?25.4:1;
                paperW=plotSize.X*factor;paperH=plotSize.Y*factor;
            }
            var paper=(BlockTableRecord)tr.GetObject(
                layout.BlockTableRecordId,OpenMode.ForRead);
            var allCandidates=new List<
                SupportOrthoDimensionUpdatePlan49.Candidate>();
            var viewportIds=new Dictionary<string,ObjectId>(
                StringComparer.OrdinalIgnoreCase);
            foreach(ObjectId id in paper)
            {
                var entity=tr.GetObject(id,OpenMode.ForRead,false) as Entity;
                if(entity is Viewport vp && vp.Width>0&&vp.Height>0)
                {
                    string handle=vp.Handle.ToString();
                    viewportIds[handle]=id;
                    allCandidates.Add(new(
                        handle,vp.CenterPoint.X,vp.CenterPoint.Y,
                        vp.Width,vp.Height));
                }
                if(entity==null||entity is Viewport)continue;
                bool keep=entity.Layer==
                    SupportOrthoMemberCalloutPlan27.Layer||
                    entity.Layer==NativeOrthoViewTitlePlan.TitleLayer;
                // Protect manually added dimensions, but deliberately
                // exclude already owned dimensions from the new layout.
                if(entity is Dimension)
                {
                    using var marker=entity.GetXDataForApplication(
                        SupportOrthoDimensionUpdatePlan49.OwnerApp);
                    if(marker==null)keep=true;
                }
                if(!keep)continue;
                try
                {
                    var b=entity.GeometricExtents;
                    var rect=new SupportOrthoSmartDimPlan46.Rect(
                        b.MinPoint.X-.5,b.MinPoint.Y-.5,
                        b.MaxPoint.X+.5,b.MaxPoint.Y+.5);
                    if(rect.IsFinite)keepouts.Add(rect);
                }
                catch(Autodesk.AutoCAD.Runtime.Exception){}
            }
            var used=new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach(var saved in original.Views)
            {
                var candidate=SupportOrthoDimensionUpdatePlan49.Resolve(
                    saved,allCandidates,used);
                if(candidate==null||
                   !viewportIds.TryGetValue(candidate.Handle,out var id)||
                   tr.GetObject(id,OpenMode.ForRead,false) is not Viewport vp)
                    throw new InvalidOperationException(
                        "Native Ortho viewport is missing/ambiguous: "+
                        saved.Name);
                used.Add(candidate.Handle);
                cameras.Add(new SupportOrthoMemberCalloutPlan17.Camera(
                    saved.Name,vp.CenterPoint.X,vp.CenterPoint.Y,
                    vp.Width,vp.Height,vp.ViewDirection.X,
                    vp.ViewDirection.Y,vp.ViewDirection.Z,
                    vp.ViewCenter.X,vp.ViewCenter.Y,vp.TwistAngle,
                    vp.CustomScale));
            }
        }
        IReadOnlyList<SupportOrthoDimLinearPlan41.Dimension> dimensions;
        if(original.ReferenceDetailDimensions)
            dimensions=SupportOrthoReferenceDimPlan47.Build(
                cameras,native,paperW,paperH,original.UnitsToMm,
                segments,null,null,keepouts).Dimensions;
        else
            dimensions=SupportOrthoSmartDimPlan46.Build(
                cameras,native,paperW,paperH,original.UnitsToMm,
                null,null,keepouts).Dimensions;
        if(dimensions.Count==0)
            throw new InvalidOperationException(
                "No safe regenerated dimensions; old dimensions kept.");
        string next=SupportOrthoDimensionUpdatePlan49.Signature(dimensions);
        int owned=SupportOrthoDimensionOwnedCount49(
            target.Database,original.LayoutName);
        if(!SupportOrthoDimensionUpdatePlan49.NeedsReplace(
            original.LastSignature,next,owned,dimensions.Count))
        {
            Message(target,"REV22.49 native dimensions already current ("+
                owned+"); no duplicates.");
            return;
        }
        int created=SupportOrthoDimLinearWriter41.Write(
            target.Database,original.LayoutName,dimensions,
            out var diagnostics,replaceGenerated:true);
        Store(target.Database,original with {LastSignature=next,
            PaperWidth=paperW,PaperHeight=paperH});
        Message(target,"REV22.49 native DIMLINEAR refresh="+created+
            " old owned dims replaced; XREF dimensions=0; "+
            string.Join("; ",diagnostics));
    }

    private static int SupportOrthoDimensionOwnedCount49(Database db,
        string layoutName)
    {
        using var tr=db.TransactionManager.StartTransaction();
        var dict=(DBDictionary)tr.GetObject(
            db.LayoutDictionaryId,OpenMode.ForRead);
        var layout=(Layout)tr.GetObject(
            dict.GetAt(layoutName),OpenMode.ForRead);
        var paper=(BlockTableRecord)tr.GetObject(
            layout.BlockTableRecordId,OpenMode.ForRead);
        int count=0;
        foreach(ObjectId id in paper)
        {
            if(tr.GetObject(id,OpenMode.ForRead,false)
                is not RotatedDimension dim)continue;
            using var data=dim.GetXDataForApplication(
                SupportOrthoDimensionUpdatePlan49.OwnerApp);
            if(data!=null&&dim.Layer==SupportOrthoDimLinearPlan41.Layer)
                count++;
        }
        return count;
    }

    private static void Message(Document doc,string value)
    {
        try {doc.Editor.WriteMessage("\nSPTC "+value);}
        catch(System.Exception) {System.Diagnostics.Trace.WriteLine(value);}
    }
}
