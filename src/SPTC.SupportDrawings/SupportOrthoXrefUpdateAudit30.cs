using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using AcadApp=Autodesk.AutoCAD.ApplicationServices.Application;
using Manifest=SPTC.SupportDrawings.SupportOrthoXrefAuditManifest30;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.30 ONLY: a read-only, fail-closed comparison of an existing REV22.29
/// Ortho against currently LOADED source/XREF Pipe PnP metadata and transformed
/// source geometry, native saved paper viewport projections, and leader starts.
/// It never changes model/Ortho geometry, performs native Update View or repairs
/// custom annotations. Older REV22.29 generator/auditor stay unchanged.
/// </summary>
public sealed class SupportOrthoXrefUpdateAudit30
{
    private sealed record PaperText(string Text,double X,double Y);
    private sealed record PaperLine(double X1,double Y1,double X2,double Y2);
    private sealed record PaperView(double X,double Y,
        SupportOrthoMemberCalloutPlan17.Camera Camera);
    private sealed record PaperState(IReadOnlyList<PaperText> Text,
        IReadOnlyList<PaperLine> Lines,IReadOnlyList<PaperView> Views);
    private sealed record CurrentPipe(string Key,string LineTag,string Bop,
        string LineSource,string BopSource,double X,double Y,double Z);

    [CommandMethod("SPTC_ORTHO22_30_VALIDATE_XREF_UPDATE",
        CommandFlags.Modal|CommandFlags.Session)]
    public void Run()
    {
        Document? ortho=AcadApp.DocumentManager.MdiActiveDocument;
        if(ortho==null)return;
        Editor ed=ortho.Editor;
        string dwg=ortho.Database.Filename??"";
        if(string.IsNullOrWhiteSpace(dwg)||!File.Exists(dwg))
        {
            ed.WriteMessage("\nREV22.30: Open the saved REV22.29 Ortho DWG.");
            return;
        }
        string prefix=Path.Combine(Path.GetDirectoryName(dwg)??"",
            Path.GetFileNameWithoutExtension(dwg));
        string sourceLog=prefix+"_REV22_29_RESULT.txt";
        string report=prefix+"_REV22_30_XREF_UPDATE_VERIFICATION.txt";
        var fail=new List<string>();
        var unknown=new List<string>();
        var info=new List<string>();
        int pipes=0,views=0,labels=0,leaders=0;
        try
        {
            if(!File.Exists(sourceLog))
                throw new InvalidOperationException("REV22.29 result TXT not found beside Ortho DWG.");
            Manifest.Manifest original=Manifest.Parse(File.ReadAllLines(sourceLog));
            pipes=original.Sources.Count;
            labels=original.Labels.Count*2;
            views=original.Views.Count;

            // The XREF PnP API must read the actual live source model and its
            // currently loaded references, not paper-space proxies.
            Document? model=null;
            foreach(Document open in AcadApp.DocumentManager)
            {
                string filename=open.Database.Filename??"";
                if(!string.IsNullOrWhiteSpace(filename) &&
                    string.Equals(Path.GetFullPath(filename),
                        Path.GetFullPath(original.SourceModel),
                        StringComparison.OrdinalIgnoreCase))
                {
                    model=open;break;
                }
            }
            if(model==null)
            {
                unknown.Add("SOURCE_MODEL_NOT_OPEN="+original.SourceModel);
                unknown.Add("ACTION=Open the registered source DWG alongside the"+
                    " Ortho DWG, reload its XREFs, then rerun audit from Ortho.");
            }
            else
            {
                var found=new List<CurrentPipe>();
                var diagnostic=new List<string>();
                using(DocumentLock sourceLock=model.LockDocument())
                {
                    var s=original.Support;
                    var c=original.Cube;
                    var support=new BoundingBox3(s.Min.X,s.Min.Y,s.Min.Z,
                        s.Max.X,s.Max.Y,s.Max.Z);
                    var cube=new BoundingBox3(c.Min.X,c.Min.Y,c.Min.Z,
                        c.Max.X,c.Max.Y,c.Max.Z);
                    double mm=UnitToMm(model.Database.Insunits.ToString());
                    if(mm<=0)throw new InvalidOperationException(
                        "Source drawing INSUNITS must be a physical unit.");
                    var discovery=SupportOrthoPipeDiscovery28.Scan(
                        model.Database,support,mm);
                    diagnostic.AddRange(discovery.Diagnostics);
                    var xref=SupportOrthoPipeDiscovery28.ReadXrefs(
                        discovery.Candidates,cube);
                    diagnostic.AddRange(xref.Diagnostics);
                    var nativeIds=discovery.Candidates.Where(c=>!c.FromXref)
                        .Select(c=>c.Id).Distinct().ToArray();
                    var native=SupportOrthoPlantPipe27.Read(
                        model.Database,nativeIds,cube);
                    diagnostic.AddRange(native.Diagnostics);
                    found.AddRange(xref.Pipes.Concat(native.Pipes).Select(p=>
                        new CurrentPipe(p.Handle,p.LineTag,p.BopLabel,
                            p.LineSource,p.BopSource,p.X,p.Y,p.Z)));
                }
                foreach(string d in diagnostic)
                    info.Add("SOURCE_DIAGNOSTIC="+d);
                var current=found.GroupBy(p=>p.Key,
                    StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g=>g.Key,g=>g.ToArray(),
                        StringComparer.OrdinalIgnoreCase);
                var paper=ReadPaperState(ortho.Database,original.Views);
                if(paper.Views.Count!=10)
                    fail.Add("NATIVE_PAPER_VIEWPORT_COUNT_MISMATCH="+
                        paper.Views.Count+"/10");

                foreach(var saved in original.Sources)
                {
                    if(!current.TryGetValue(saved.Key,out var matches) ||
                       matches.Length!=1)
                    {
                        unknown.Add("SOURCE_PIPE_NOT_UNIQUELY_RESOLVED="+saved.Key);
                        continue;
                    }
                    var now=matches[0];
                    if(now.LineSource=="LINE_NUMBER_TAG_NOT_AVAILABLE")
                        unknown.Add("LINE_TAG_SOURCE_NOT_VERIFIABLE="+saved.Key);
                    else if(!string.Equals(now.LineTag,saved.LineTag,
                        StringComparison.Ordinal))
                        fail.Add("LINE_TAG_CHANGED="+saved.Key+
                            " BEFORE="+saved.LineTag+" NOW="+now.LineTag);

                    if(now.BopSource=="BOP_NOT_AVAILABLE" ||
                       now.BopSource=="XREF_BOP_TRANSFORM_UNSUPPORTED")
                        unknown.Add("BOP_SOURCE_NOT_VERIFIABLE="+saved.Key);
                    else if(!string.Equals(now.Bop,saved.Bop,
                        StringComparison.Ordinal))
                        fail.Add("BOP_CHANGED="+saved.Key+
                            " BEFORE="+saved.Bop+" NOW="+now.Bop);

                    double sourceMoved=Math.Sqrt(
                        Square(now.X-saved.WorldAnchor.X)+
                        Square(now.Y-saved.WorldAnchor.Y)+
                        Square(now.Z-saved.WorldAnchor.Z));
                    // Source drawing may use non-mm units; the definitive
                    // leader check below is in paper mm for each native view.
                    info.Add("XREF_SOURCE="+saved.Key+
                        " LOADED_WCS_CENTER_DELTA_SOURCE_UNITS="+
                        sourceMoved.ToString("0.####",
                            CultureInfo.InvariantCulture));
                    string[] updated=SupportOrthoPipeCallout29.Compose(
                        now.LineTag,now.Bop).Split('\n');
                    foreach(var label in original.Labels.Where(l=>
                        string.Equals(l.Key,saved.Key,
                            StringComparison.OrdinalIgnoreCase)))
                    {
                        if(!string.Equals(label.First,updated[0],
                            StringComparison.Ordinal))
                            fail.Add("STALE_VISIBLE_LINE_NUMBER="+label.View+
                                " SOURCE="+saved.Key);
                        if(!string.Equals(label.Second,updated[1],
                            StringComparison.Ordinal))
                            fail.Add("STALE_VISIBLE_BOP="+label.View+
                                " SOURCE="+saved.Key);
                        var expectedView=original.Views.First(v=>v.Name==label.View);
                        var candidates=paper.Views.Where(v=>
                            Distance(v.X,v.Y,expectedView.PaperCenter.X,
                                expectedView.PaperCenter.Y)<=0.5).ToArray();
                        if(candidates.Length!=1)
                        {
                            fail.Add("NATIVE_VIEWPORT_MISSING_OR_MOVED="+label.View);
                            continue;
                        }
                        var camera=candidates[0].Camera;
                        var projected=SupportOrthoMemberCalloutPlan17.Project(
                            camera,now.X,now.Y,now.Z);
                        if(Distance(projected.X,projected.Y,label.Anchor.X,
                            label.Anchor.Y)>0.85)
                        {
                            fail.Add("PIPE_LEADER_SOURCE_MISALIGNED="+label.View+
                                " SOURCE="+saved.Key+" REPROJECTED_PAPER_MM="+
                                Point(projected.X,projected.Y));
                        }
                        bool lineExists=paper.Lines.Any(l=>
                            Distance(l.X1,l.Y1,label.Anchor.X,label.Anchor.Y)<0.5 &&
                            Distance(l.X2,l.Y2,label.LaneX,label.Anchor.Y)<0.5);
                        if(lineExists)leaders++;
                        else fail.Add("PIPE_LEADER_SEGMENT_MISSING="+label.View+
                            " SOURCE="+saved.Key);

                        bool first=paper.Text.Any(t=>
                            string.Equals(t.Text,label.First,StringComparison.Ordinal) &&
                            Math.Abs(t.X-label.LabelTop.X)<0.55 &&
                            Math.Abs(t.Y-(label.LabelTop.Y-
                                SupportOrthoMemberCalloutPlan27.TextHeightMm*1.18))<1.65);
                        bool second=paper.Text.Any(t=>
                            string.Equals(t.Text,label.Second,StringComparison.Ordinal) &&
                            Math.Abs(t.X-label.LabelTop.X)<0.55 &&
                            Math.Abs(t.Y-(label.LabelTop.Y-
                                SupportOrthoMemberCalloutPlan27.TextLineHeightMm-
                                SupportOrthoMemberCalloutPlan27.TextHeightMm*1.18))<1.65);
                        if(!first)fail.Add("VISIBLE_LINE_NO_MISSING_OR_MOVED="+
                            label.View+" SOURCE="+saved.Key);
                        if(!second)fail.Add("VISIBLE_BOP_MISSING_OR_MOVED="+
                            label.View+" SOURCE="+saved.Key);
                    }
                }
                info.Add("SOURCE_PIPES_FOUND="+found.Count);
                info.Add("ANNOTATION_TEXT_ENTITIES_ON_PAPER="+paper.Text.Count);
                info.Add("ANNOTATION_LEADER_SEGMENTS_ON_PAPER="+
                    paper.Lines.Count);
            }
        }
        catch(System.Exception ex)
        {
            unknown.Add("AUDIT_INCOMPLETE="+ex.GetType().Name+
                ": "+ex.Message);
        }

        string state=fail.Count>0?"FAIL":
            unknown.Count>0?"NOT_VERIFIED":"PASS";
        var output=new List<string>
        {
            "REV22_30_XREF_UPDATE_VERIFICATION="+state,
            "REVISION=22.30",
            "ORTHO_DWG="+dwg,
            "ORIGINAL_REV22_29_LOG="+sourceLog,
            "RUN_AT="+DateTimeOffset.Now.ToString("O",
                CultureInfo.InvariantCulture),
            "SOURCE_PIPES_EXPECTED="+pipes,
            "PAPER_NATIVE_VIEWS_EXPECTED="+views,
            "PAPER_PIPE_DBTEXT_LINES_EXPECTED="+labels,
            "PIPE_ORIGINAL_LEADER_STARTS_FOUND="+leaders,
            "FAIL_COUNT="+fail.Count,
            "UNVERIFIED_COUNT="+unknown.Count,
            "SOURCE_XREF_LOADED_DATA_COMPARED="+(unknown.Count==0),
            "NATIVE_2D_GENERATED_GEOMETRY_FRESHNESS=NOT_VERIFIED",
            "XREF_DISK_RELOAD_STATUS=NOT_VERIFIED",
            "NATIVE_UPDATE_VIEW_EVENT=NOT_VERIFIED",
            "NOTE=PASS certifies only loaded XREF properties, visible"+
                " label text and projected leader starts against native saved"+
                " paper viewports; not an independent regenerated geometry proof",
            "DWG_WRITES=NONE"
        };
        output.AddRange(fail.Select(x=>"FAIL_DETAIL="+x));
        output.AddRange(unknown.Select(x=>"UNVERIFIED_DETAIL="+x));
        output.AddRange(info);
        try
        {
            File.WriteAllLines(report,output);
            ed.WriteMessage("\nREV22.30 XREF UPDATE VERIFICATION="+state+
                "  mismatches="+fail.Count+"  unknown="+unknown.Count);
            ed.WriteMessage("\nREV22.30 report: "+report);
        }
        catch(System.Exception ex)
        {
            ed.WriteMessage("\nREV22.30: Could not save verification report: "+
                ex.Message);
        }
        ed.WriteMessage("\nREV22.30: Native generated geometry freshness and"+
            " disk XREF reload are NOT independently verified.");
    }

    private static PaperState ReadPaperState(Database db,
        IReadOnlyList<Manifest.View> expected)
    {
        var texts=new List<PaperText>();
        var lines=new List<PaperLine>();
        var views=new List<PaperView>();
        using var tr=db.TransactionManager.StartTransaction();
        var dictionary=(DBDictionary)tr.GetObject(
            db.LayoutDictionaryId,OpenMode.ForRead);
        foreach(DBDictionaryEntry entry in dictionary)
        {
            if(tr.GetObject(entry.Value,OpenMode.ForRead)
                is not Layout layout || layout.ModelType)continue;
            if(tr.GetObject(layout.BlockTableRecordId,OpenMode.ForRead)
                is not BlockTableRecord record)continue;
            foreach(ObjectId id in record)
            {
                if(tr.GetObject(id,OpenMode.ForRead,false) is not Entity e)
                    continue;
                if(e is Viewport vp && vp.Width>0 && vp.Height>0)
                {
                    var corresponding=expected.Where(v=>Distance(
                        v.PaperCenter.X,v.PaperCenter.Y,
                        vp.CenterPoint.X,vp.CenterPoint.Y)<=0.5).ToArray();
                    if(corresponding.Length!=1)continue;
                    var c=new SupportOrthoMemberCalloutPlan17.Camera(
                        corresponding[0].Name,vp.CenterPoint.X,
                        vp.CenterPoint.Y,vp.Width,vp.Height,
                        vp.ViewDirection.X,vp.ViewDirection.Y,
                        vp.ViewDirection.Z,
                        vp.ViewCenter.X,vp.ViewCenter.Y,
                        vp.TwistAngle,vp.CustomScale);
                    views.Add(new PaperView(vp.CenterPoint.X,
                        vp.CenterPoint.Y,c));
                    continue;
                }
                if(!string.Equals(e.Layer,
                    SupportOrthoMemberCalloutPlan27.Layer,
                    StringComparison.OrdinalIgnoreCase))continue;
                if(e is DBText dbt)
                    texts.Add(new PaperText(dbt.TextString,
                        dbt.Position.X,dbt.Position.Y));
                else if(e is Line l)
                    lines.Add(new PaperLine(l.StartPoint.X,l.StartPoint.Y,
                        l.EndPoint.X,l.EndPoint.Y));
            }
        }
        return new PaperState(texts,lines,views);
    }

    private static double Distance(double a,double b,double c,double d) =>
        Math.Sqrt(Square(a-c)+Square(b-d));
    private static double Square(double v)=>v*v;
    private static string Point(double x,double y) =>
        "("+x.ToString("0.###",CultureInfo.InvariantCulture)+","+
        y.ToString("0.###",CultureInfo.InvariantCulture)+")";
    private static double UnitToMm(string name)=>name switch
    {
        "Millimeters"=>1,"Centimeters"=>10,"Meters"=>1000,
        "Inches"=>25.4,"Feet"=>304.8,_=>0
    };
}
