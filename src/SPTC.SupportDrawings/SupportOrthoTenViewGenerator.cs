using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using WinForms = System.Windows.Forms;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.29 isolated actual viewport paper-size experiment: select support in a
/// registered 3D source; clone a verified empty native Ortho Project Manager seed;
/// register, open, initialize ten native OrthoCubes and call unsupported internal
/// GenerateOrthos for ten views using automatically computed paper-space TOP anchor.
/// BACKUP/COPIED PROJECT ONLY. Never directly patch Ortho.dcf.
/// Any Autodesk dialogs from the public API remain host-controlled.
/// </summary>
public sealed class SupportOrthoTenViewGenerator
{
    [CommandMethod("SPTC_SUPPORTORTHO10",
        CommandFlags.Modal | CommandFlags.Session)]
    public async void Run()
    {
        Document? sourceDoc=AcadApp.DocumentManager.MdiActiveDocument;
        if(sourceDoc==null)return;
        Editor ed=sourceDoc.Editor;

        if(SupportDrawingPalette40.HasPendingOrtho)
        {
            // Queued by the persistent UI: execute only after AutoCAD enters
            // the registered command's document/selection context.
            if(!SupportDrawingPalette40.TryTakeQueuedOrtho(
                sourceDoc,out SupportDrawingUiOptions? options) ||
               options==null)return;
            try
            {
                await RunQueuedOrtho(sourceDoc,options);
            }
            catch(System.Exception ex)
            {
                SafeWriteToActiveEditor("SPTC palette Ortho error: "+ex);
            }
            finally
            {
                SupportDrawingPalette40.Finish(
                    "SPTC Ortho operation finished. Review progress and TXT logs.");
            }
            return;
        }

        if(SupportDrawingPalette40.IsBusy)
        {
            ed.WriteMessage("\nSPTC: Operation already running; finish it before starting another.");
            return;
        }

        string source=sourceDoc.Database.Filename??"";
        if(string.IsNullOrWhiteSpace(source)||!File.Exists(source))
        {
            ed.WriteMessage("\nSPTC: Save a Plant 3D project-registered source DWG first.");
            return;
        }
        ProjectPart part;
        try {part=CurrentOrthoProject();}
        catch(System.Exception ex)
        {
            ed.WriteMessage("\nSPTC project preflight failed: "+ex.Message);
            return;
        }
        SupportDrawingPalette40.ShowOrFocus(
            sourceDoc,part.ProjectRoot,part.OrthoFolder);
        // REV22.40: No modal ShowDialog. Running the command again focuses
        // the existing palette and preserves all editable UI options.
    }

    private async Task RunQueuedOrtho(Document sourceDoc,
        SupportDrawingUiOptions options)
    {
        Editor ed=sourceDoc.Editor;
        string source=sourceDoc.Database.Filename??"";
        var progress=new SupportDrawingProgressForm35();
        try
        {
            if(options.BatchOrtho)
            {
                var selection=ed.GetSelection(new PromptSelectionOptions
                {
                    MessageForAdding="\nSelect each support BLOCK reference for a separate native Ortho DWG: "
                });
                if(selection.Status!=PromptStatus.OK)return;
                var ids=new List<ObjectId>();
                using(Transaction tr=sourceDoc.Database.TransactionManager.StartTransaction())
                {
                    foreach(var id in selection.Value.GetObjectIds().Distinct())
                        if(tr.GetObject(id,OpenMode.ForRead,false) is BlockReference)
                            ids.Add(id);
                }
                if(ids.Count==0)
                {
                    ed.WriteMessage("\nSPTC batch: no support block references were selected.");
                    return;
                }
                progress.Show();
                int passed=0,failed=0;
                var report=new StringBuilder();
                report.AppendLine("SPTC_REV22_35_BATCH_REPORT");
                report.AppendLine("SOURCE="+source);
                report.AppendLine("SUPPORT_COUNT="+ids.Count);
                foreach(ObjectId id in ids)
                {
                    try
                    {
                        AcadApp.DocumentManager.MdiActiveDocument=sourceDoc;
                        progress.Report("Starting support handle "+id.Handle);
                        bool ok=await GenerateOne(sourceDoc,new[]{id},options,progress);
                        if(ok)passed++; else failed++;
                        report.AppendLine("HANDLE="+id.Handle+" STATUS="+(ok?"PASS":"FAIL"));
                        progress.Report("Support "+id.Handle+": "+(ok?"PASS":"FAIL"));
                    }
                    catch(System.Exception ex)
                    {
                        failed++;
                        report.AppendLine("HANDLE="+id.Handle+" STATUS=FAIL ERROR="+ex.Message);
                        progress.Report("Support "+id.Handle+" FAILED: "+ex.Message);
                    }
                }
                report.AppendLine("PASS="+passed+" FAIL="+failed);
                try
                {
                    string output=Path.Combine(Path.GetDirectoryName(source)!,
                        Path.GetFileNameWithoutExtension(source)+"_SPTC_BATCH_"+
                        DateTime.UtcNow.ToString("yyyyMMdd_HHmmss",
                            CultureInfo.InvariantCulture)+".txt");
                    File.WriteAllText(output,report.ToString(),new UTF8Encoding(false));
                    progress.Report("Batch report: "+output);
                }
                catch(System.Exception ex)
                {
                    progress.Report("Could not write batch report: "+ex.Message);
                }
            }
            else
            {
                bool ok=await GenerateOne(sourceDoc,null,options,progress);
                progress.Report("Single Ortho result: "+(ok?"PASS":"FAIL / SEE TXT"));
            }
        }
        catch(System.Exception ex)
        {
            progress.Report("SPTC command error: "+ex);
            SafeWriteToActiveEditor("SPTC generation stopped: "+ex.Message);
        }
        finally
        {
            progress.Complete();
            if(!progress.Visible)progress.Show();
        }
    }

    private async Task<bool> GenerateOne(Document sourceDoc,
        ObjectId[]? selectedOverride,SupportDrawingUiOptions options,
        SupportDrawingProgressForm35 progress)
    {
        Editor ed=sourceDoc.Editor;
        bool succeeded=false;
        string source = Path.GetFullPath(sourceDoc.Database.Filename ?? "");
        if (!File.Exists(source))
        {
            ed.WriteMessage("\nREV22.29: Open a saved project-registered 3D model before running the test.");
            return false;
        }

        NativeOrthoProjectCheck.Result sourceCheck;
        try { sourceCheck = NativeOrthoProjectCheck.Inspect(source); }
        catch (System.Exception ex)
        {
            ed.WriteMessage("\nREV22.29: Native source check failed: " + ex.Message);
            return false;
        }
        if (!sourceCheck.SourceRegistrationVerified)
        {
            ed.WriteMessage("\nREV22.29: Source 3D drawing is not verified project-registered. No changes.");
            return false;
        }

        double units = sourceDoc.Database.Insunits.ToString() switch
        {
            "Millimeters" => 1, "Centimeters" => 10, "Meters" => 1000,
            "Inches" => 25.4, "Feet" => 304.8, _ => 0
        };
        if (units <= 0)
        {
            ed.WriteMessage("\nREV22.29: Unsupported source units; expected a physical unit like Millimeters.");
            return false;
        }

        // REV22.29: Keep Original mode available as an exact fallback.
        // SupportFocus mode calculates the XY cube only from STRUCTURAL
        // support selection, not full long-pipe GeometricExtents.
        // UI selection replaces the old command-line mode prompt.
        bool supportFocus=options.SupportFocus;
        ObjectId[] selectedIds;
        if(selectedOverride!=null)selectedIds=selectedOverride;
        else
        {
            PromptSelectionResult selection=ed.GetSelection(new PromptSelectionOptions
            {
                MessageForAdding=supportFocus
                    ? "\nSPTC: Select STRUCTURE MEMBERS and/or Plant 3D SUPPORT objects: "
                    : "\nSPTC: Select support entities for Original OrthoCube mode: "
            });
            if(selection.Status!=PromptStatus.OK)return false;
            selectedIds=selection.Value.GetObjectIds();
        }
        if(!progress.Visible)progress.Show();
        progress.Report("Support selection received: "+selectedIds.Length+" object(s)");

        var handles = new List<string>();
        var selectedSupportIds = new List<ObjectId>();
        BoundingBox3? geometry = null;
        int invalidExtents = 0;
        int misselectedPipeEntities = 0;
        using (Transaction tr = sourceDoc.Database.TransactionManager.StartTransaction())
        {
            foreach (ObjectId id in selectedIds.Distinct())
            {
                try
                {
                    if (tr.GetObject(id, OpenMode.ForRead, false) is not Entity e) continue;
                    string selectedType=e.GetType().Name;
                    if(supportFocus &&
                       selectedType.EndsWith("Pipe",StringComparison.OrdinalIgnoreCase))
                    {
                        misselectedPipeEntities++;
                        continue;
                    }
                    Extents3d b = e.GeometricExtents;
                    var current = new BoundingBox3(b.MinPoint.X, b.MinPoint.Y,
                        b.MinPoint.Z, b.MaxPoint.X, b.MaxPoint.Y, b.MaxPoint.Z);
                    geometry = geometry.HasValue ? geometry.Value.Include(current) : current;
                    handles.Add(id.Handle.ToString());
                    selectedSupportIds.Add(id);
                }
                catch { invalidExtents++; }
            }
        }
        if (!geometry.HasValue || handles.Count == 0 ||
            invalidExtents > 0 || misselectedPipeEntities > 0)
        {
            ed.WriteMessage("\nREV22.29: Select valid structural support entities only. "+
                "Invalid extents="+invalidExtents+
                " accidentally selected pipe objects="+misselectedPipeEntities);
            return false;
        }

        // REV22.29: read only the actual selected support objects.
        // The member sizes must come from declared CAD metadata, never
        // geometric bounding-box dimensions.
        // REV22.29: Plant project property DB stores native
        // Structure Member.Size independently of ordinary CAD objects.
        // Retrieve the real value before opening any new Ortho drawing.
        var pnpRead=SupportOrthoPlantProperties16.Inspect(
            sourceDoc.Database,selectedSupportIds);
        var structuralMetadata=pnpRead.Summary;
        // REV22.29: capture each real steel member separately while the
        // source drawing is active and its geometric extents are accessible.
        var sourceSteel=SupportOrthoMemberSource17.Read(
            sourceDoc.Database,selectedSupportIds,pnpRead);
        // REV22.29: check source member direction (start/end axis when
        // available) and Plant 3D Top of Steel before context switches.
        var horizontalSteel=SupportOrthoHorizontalSource19.Read(
            sourceDoc.Database,selectedSupportIds,sourceSteel);
        // REV22.29: read native Plant 3D Piping Support objects separately.
        // Their actual Long Description (Size) is used for the label.
        // Plant Support TOS is separate: exact if source field is present,
        // otherwise labeled approximate from geometric upper elevation.
        var plantSupports=SupportOrthoPlantSupport25.Read(
            sourceDoc.Database,selectedSupportIds);
        var sourceAnnotations=horizontalSteel.Members.Select(m=>
            new SupportOrthoMemberCalloutPlan27.Member(
                m.Handle,m.Size,m.X,m.Y,m.Z,m.IsHorizontal,
                m.TosText,m.TosSource))
            .Concat(plantSupports.Members.Select(s=>
                new SupportOrthoMemberCalloutPlan27.Member(
                    s.Handle,s.Description,s.X,s.Y,s.Z,true,
                    string.IsNullOrWhiteSpace(s.TosText)?null:s.TosText,
                    s.TosSource)))
            .ToArray();
        if(sourceAnnotations.Select(m=>m.Handle).Distinct(
            StringComparer.OrdinalIgnoreCase).Count()!=sourceAnnotations.Length)
            throw new InvalidOperationException(
                "REV22.29 duplicate steel/support ObjectId handles.");
        ed.WriteMessage("\nREV22.29: "+structuralMetadata.IdentityKind+
            "="+structuralMetadata.Identity+
            " ; Plant 3D structural sizes="+structuralMetadata.SizeText+
            " ; PnP members="+pnpRead.MembersWithSize);

        // REV22.29: no separate pipe selection. Include all nearby native pipes
        // and native Pipe entities from loaded XREFs, including nested instances.
        // Support selection remains explicit; only pipes are auto-discovered.
        var pipeDiscovery=SupportOrthoPipeDiscovery28.Scan(
            sourceDoc.Database,geometry.Value,units);
        var localPipeBounds=pipeDiscovery.Candidates
            .Select(p=>p.WorldBounds).ToList();
        // Optional, read-only proximity scan of loaded nested XREF
        // StructureMember / genuine Plant Support entities.
        // The XREF file name is always derived from its actual source DB.
        var xrefMembers=(options.XrefMemberNames||options.AutoDimLinear)
            ? SupportOrthoXrefMemberDiscovery37.Scan(
                sourceDoc.Database,geometry.Value,units)
            : new SupportOrthoXrefMemberDiscovery37.Result(
                Array.Empty<SupportOrthoXrefMemberDiscovery37.Item>(),
                Array.Empty<string>());
        ed.WriteMessage("\nSPTC nearby XREF STRUCTURE/SUPPORT objects: "+
            xrefMembers.Items.Count);
        foreach(string diagnostic in xrefMembers.Diagnostics)
            ed.WriteMessage("\nSPTC XREF: "+diagnostic);
        var selectedPipeIds=pipeDiscovery.Candidates
            .Where(p=>!p.FromXref).Select(p=>p.Id).Distinct().ToList();
        SupportFocusedCubePlanner.Result? focusPlan=null;
        double focusXYClearanceMm=0.0,focusPipeReachMm=0.0;
        ed.WriteMessage("\nREV22.29: Automatically discovered "+
            pipeDiscovery.NativeCount+" native pipes and "+
            pipeDiscovery.XrefCount+" XREF pipes near the selected support.");
        foreach(string note in pipeDiscovery.Diagnostics)
            ed.WriteMessage("\nREV22.29 SOURCE: "+note);
        if(pipeDiscovery.Diagnostics.Any(s=>
            s.StartsWith("REV22_29_SCAN_FAILED=",StringComparison.Ordinal)))
        {
            ed.WriteMessage("\nREV22.29: Pipe scan failed. No files changed.");
            return false;
        }

        NativeOrthoLayout.Plan layout;
        try
        {
            if(supportFocus)
            {
                var pipeOnlyPlan=SupportFocusedCubePlanner.BuildAutomatic(
                    geometry.Value,localPipeBounds,units);
                // Retain the proven "no eligible pipe" preflight exactly.
                if(localPipeBounds.Count>0 &&
                   pipeOnlyPlan.Cube.PipesAccepted==0)
                    throw new InvalidOperationException(
                        "No selected pipe intersects the automatically computed "+
                        "support cube. Reselect nearby pipes or use Original mode.");
                var focusCandidates=localPipeBounds.Concat(
                    xrefMembers.Items.Select(x=>x.WorldBounds)).ToArray();
                var autoPlan=xrefMembers.Items.Count>0
                    ? SupportFocusedCubePlanner.BuildAutomatic(
                        geometry.Value,focusCandidates,units)
                    : pipeOnlyPlan;
                focusPlan=autoPlan.Cube;
                focusXYClearanceMm=autoPlan.XYClearanceMm;
                focusPipeReachMm=autoPlan.PipeVerticalReachMm;
                 ed.WriteMessage("\nREV22.29: Calculated XY clearance="+
                    focusXYClearanceMm.ToString("0.###",CultureInfo.InvariantCulture)+
                    " mm; pipe elevation reach="+
                    focusPipeReachMm.ToString("0.###",CultureInfo.InvariantCulture)+" mm.");
                layout=NativeOrthoLayout.Build(focusPlan.FocusBounds,units);
            }
            else layout=NativeOrthoLayout.Build(geometry.Value,units);
        }
        catch (System.Exception ex)
        {
            ed.WriteMessage("\nREV22.29: Invalid support OrthoCube: " + ex.Message);
            return false;
        }
        // REV22.29: source-authenticated pipe metadata only.
        // XREF source ObjectIds and nested transforms come from loaded XREF DBs.
        var nativeRead=SupportOrthoPlantPipe27.Read(
            sourceDoc.Database,selectedPipeIds,layout.PaddedCube);
        var xrefRead=SupportOrthoPipeDiscovery28.ReadXrefs(
            pipeDiscovery.Candidates,layout.PaddedCube);
        if(pipeDiscovery.XrefCount>0 && xrefRead.Pipes.Count==0)
        {
            ed.WriteMessage("\nREV22.29: XREF pipes were found, but none "+
                "provided verifiable Line Number Tag/BOP metadata. "+
                "See diagnostics below. No Ortho drawing created.");
            foreach(string note in xrefRead.Diagnostics)
                ed.WriteMessage("\nREV22.29 XREF: "+note);
            return false;
        }
        var pipeRead=new SupportOrthoPlantPipe27.Result(
            nativeRead.Pipes.Concat(xrefRead.Pipes).ToArray(),
            nativeRead.Candidates+xrefRead.Candidates,
            nativeRead.LinkedRows+xrefRead.LinkedRows,
            pipeDiscovery.Diagnostics.Concat(nativeRead.Diagnostics)
                .Concat(xrefRead.Diagnostics).ToArray());
        var pipeAnnotations=pipeRead.Pipes.Select(p=>
            new SupportOrthoMemberCalloutPlan27.Member(
                p.Handle,"Line Number Tag: "+p.LineTag,p.X,p.Y,p.Z,
                false,null,"NOT_STEEL_TOS",p.BopLabel,p.BopSource)).ToArray();
        // Optional XREF members receive source-DWG-name labels only.
        // We do NOT guess Structure Size, Support Description, TOS or
        // associated source PnP properties from foreign XREF databases.
        var xrefNearCube=SupportOrthoXrefMemberDiscovery37.Inside(
            xrefMembers.Items,layout.PaddedCube);
        var xrefAnnotations=xrefNearCube.Select(x=>
        {
            var position=SupportOrthoXrefMemberDiscovery37.ClippedCenter(
                x.WorldBounds,layout.PaddedCube);
            return new SupportOrthoMemberCalloutPlan27.Member(
                x.Key,"XREF "+x.Kind+": "+x.DrawingName,
                position.X,position.Y,position.Z,false,null,
                "XREF_SOURCE_DWG",null,null,x.DrawingName,x.Kind);
        }).ToArray();
        sourceAnnotations=sourceAnnotations.Concat(pipeAnnotations)
            .Concat(xrefAnnotations).ToArray();
        if(sourceAnnotations.Select(m=>m.Handle).Distinct(
            StringComparer.OrdinalIgnoreCase).Count()!=sourceAnnotations.Length)
            throw new InvalidOperationException(
                "REV22.29 duplicate Pipe/Structure Member/Support handle.");

        // REV22.43: Start/End remains the source of genuine member length.
        // Recognized upright members without those fields may now carry an
        // explicitly marked OVERALL Z-height from actual entity geometry,
        // never presented as a fabricated member cut length.
        var dimSource=options.AutoDimLinear
            ? SupportOrthoDimensionSource41.Read(
                sourceDoc.Database,selectedSupportIds,xrefNearCube,units,
                horizontalSteel.Members.Select(x=>x.Handle),
                plantSupports.Members.Select(x=>x.Handle))
            : new SupportOrthoDimensionSource41.Result(
                Array.Empty<SupportOrthoDimensionSource41.Segment>(),
                Array.Empty<string>());

        // REV22.44: dedicated source list for OVERALL VIEW-PROJECTED
        // horizontal/vertical dimensions on EVERY selected orthographic view.
        // Extents are source model WCS, XREF extents transformed into host WCS.
        var everyViewMembers=options.AutoDimLinear
            ? SupportOrthoAllMemberExtent44.Read(
                sourceDoc.Database,selectedSupportIds,
                horizontalSteel.Members.Select(x=>x.Handle),
                plantSupports.Members.Select(x=>x.Handle),
                xrefNearCube,layout.PaddedCube)
            : new SupportOrthoAllMemberExtent44.Result(
                Array.Empty<SupportOrthoAllViewDimPlan44.Member>(),
                Array.Empty<string>());

        ProjectPart part;
        try { part = CurrentOrthoProject(); }
        catch (System.Exception ex)
        {
            ed.WriteMessage("\nREV22.29: Cannot resolve current Plant Ortho project: " + ex.Message);
            return false;
        }

        // REV22.35: discover only native, project-registered and EMPTY seeds.
        // Never treat a previous generated Ortho or an arbitrary DWG as a seed.
        string seed="";
        if(options.AutoSeed)
        {
            try
            {
                var candidates=Directory.EnumerateFiles(part.OrthoFolder,
                    "*.dwg",SearchOption.AllDirectories)
                    .Where(p=>NativeOrthoRegistrationNames.IsInsideProjectDirectory(
                        p,part.OrthoFolder))
                    .OrderByDescending(p=>Path.GetFileName(p).Contains(
                        "SEED",StringComparison.OrdinalIgnoreCase))
                    .ThenBy(p=>p,StringComparer.OrdinalIgnoreCase);
                foreach(string path in candidates)
                {
                    try
                    {
                        if(IsNativeOrthoValid(part.OrthoPart,path) &&
                           ViewCatalog(part.OrthoPart,path)=="EMPTY_REGISTERED_ORTHO")
                        {
                            seed=Path.GetFullPath(path);
                            break;
                        }
                    }
                    catch(System.Exception) { /* Reject this candidate, continue scan. */ }
                }
            }
            catch(System.Exception ex)
            {
                ed.WriteMessage("\nSPTC auto-seed scan error: "+ex.Message);
                return false;
            }
            if(seed.Length==0)
            {
                ed.WriteMessage("\nSPTC: No registered EMPTY native Ortho seed found in project Orthos. Create one in Project Manager.");
                return false;
            }
        }
        else
        {
            seed=Path.GetFullPath(options.SeedPath);
        }
        progress.Report("Preflight: source registered; native seed "+Path.GetFileName(seed));
        if (!NativeOrthoRegistrationNames.IsInsideProjectDirectory(seed, part.ProjectRoot) ||
            !NativeOrthoRegistrationNames.IsInsideProjectDirectory(seed, part.OrthoFolder))
        {
            ed.WriteMessage("\nREV22.29: Seed must be within the active project's registered Orthos folder.");
            return false;
        }
        if (!IsNativeOrthoValid(part.OrthoPart, seed))
        {
            ed.WriteMessage("\nREV22.29: Seed failed native OrthoDocUtils.IsValidOrthoDrawing.");
            return false;
        }
        string seedState = ViewCatalog(part.OrthoPart, seed);
        if (seedState != "EMPTY_REGISTERED_ORTHO")
        {
            ed.WriteMessage("\nREV22.29: Seed must be registered and EMPTY; found: " + seedState);
            return false;
        }

        string suffix = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss",
            CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N")[..6];
        string file = NativeOrthoRegistrationNames.NewOrthoDrawingFileName(source, handles, suffix)
            .Replace("_ORTHO_R14_", "_ORTHO_R229XREF_", StringComparison.Ordinal);
        if(!string.IsNullOrWhiteSpace(options.DrawingPrefix))
            file=options.DrawingPrefix+"_"+file; // Keep unique source/handle/time suffix.

        string dest = Path.Combine(Path.GetDirectoryName(seed)!, file);
        if (File.Exists(dest))
        {
            ed.WriteMessage("\nREV22.29: Destination DWG exists. No changes.");
            return false;
        }
        // Preflight the actual seed sheet before project writes. A requested
        // scale must be no more magnified than the proven ten-view fit.
        try
        {
            using(var previewDb=new Database(false,true))
            {
                previewDb.ReadDwgFile(seed,
                    FileOpenMode.OpenForReadAndAllShare,true,null);
                previewDb.CloseInput(true);
                string previewLayout=FindPaperLayout(previewDb);
                (double w,double h)=CurrentPaper(previewDb,previewLayout);
                var fit=NativeOrthoTenViewPlan.Build(layout,w,h);
                if(options.ScaleDenominator>0 &&
                   options.ScaleDenominator<fit.Denominator)
                {
                    ed.WriteMessage("\nSPTC preflight: Requested 1:"+
                        options.ScaleDenominator+" cannot fit the ten-view layout. "+
                        "Minimum safe denominator is "+fit.Denominator+". No drawing created.");
                    return false;
                }
                if(options.ScaleDenominator>0)
                    fit=fit with {Denominator=options.ScaleDenominator,
                        Scale=1.0/options.ScaleDenominator};
                // Perform the same exact title/frame collision calculations
                // read-only, BEFORE cloning/registering the new Ortho DWG.
                NativeOrthoTenViewAutoPlacement26.Build(fit,w,h);
            }
        }
        catch(System.Exception ex)
        {
            ed.WriteMessage("\nSPTC preflight failed before drawing registration: "+ex.Message);
            return false;
        }
        progress.Report("Preflight PASS. Output: "+Path.GetFileName(dest)+
            " | Views: "+string.Join(",",options.Views)+
            " | Scale: "+(options.ScaleDenominator>0?
                "1:"+options.ScaleDenominator:"automatic"));


        // The public SDK, not this command, decides the final viewport insertion.
        // No false claim that the proposed TOP center is passed into CreateOrthos.
        // REV22.34: The explicit SPTC_SUPPORTORTHO10 command authorizes creating
        // and registering a new, uniquely named Ortho drawing. The source and
        // registered-empty-seed validations and destination-exists guard above
        // must all pass before any project write. No Yes/No or typed revision
        // confirmation is requested.
        ed.WriteMessage("\nSPTC: Creating and registering a new 10-view Ortho DWG automatically: " + dest);

        string report = Path.Combine(Path.GetDirectoryName(dest)!,
            Path.GetFileNameWithoutExtension(dest) + "_REV22_29_RESULT.txt");
        var log = new StringBuilder(6000);
        void Log(string msg)
        {
            string line=DateTimeOffset.UtcNow.ToString("O",CultureInfo.InvariantCulture)+
                " "+msg+Environment.NewLine;
            log.Append(line);
            // Append-only rather than repeatedly rewriting a growing TXT.
            try { File.AppendAllText(report,line,new UTF8Encoding(false)); }
            catch { /* command window is still available for diagnostics */ }
            if(msg.Contains("_BEGIN",StringComparison.Ordinal) &&
               msg.StartsWith("STEP6_",StringComparison.Ordinal))
                progress.Report("Generating "+msg["STEP6_".Length..^"_BEGIN".Length]+" native view");
            if(msg.StartsWith("FINAL_STATUS=",StringComparison.Ordinal) ||
               msg.StartsWith("ERROR=",StringComparison.Ordinal) ||
               msg.StartsWith("NATIVE_EXCEPTION=",StringComparison.Ordinal))
                progress.Report(msg);
        }

        bool cloned = false, registrationAttempted = false;
        Document? targetDoc = null;
        try
        {
            Log("SPTC_REV22_ONE_COMMAND_NATIVE_TOP_TEST");
            Log("PROJECT=" + part.ProjectRoot);
            Log("SOURCE_MODEL=" + source);
            Log("SOURCE_HANDLES=" + string.Join(",", handles));
            Log("REV22_29_SUPPORT_TYPE="+structuralMetadata.IdentityKind);
            Log("REV22_29_SUPPORT_IDENTITY="+structuralMetadata.Identity);
            Log("REV22_29_SUPPORT_OBJECT_COUNT="+structuralMetadata.SelectedCount);
            Log("REV22_29_BLOCK_REFERENCE_COUNT="+structuralMetadata.BlockCount);
            Log("REV22_29_NATIVE_STRUCTURE_MEMBER_COUNT="+
                structuralMetadata.StructuralMemberCount);
            Log("REV22_29_MEMBER_SIZES="+structuralMetadata.SizeText);
            Log("REV22_29_MEMBER_SIZE_COUNT="+structuralMetadata.MemberSizes.Count);
            Log("REV22_29_MEMBER_SIZE_SOURCE_COUNT="+structuralMetadata.Evidence.Count);
            Log("REV22_29_PNP_STRUCTURE_CANDIDATES="+pnpRead.CandidateMembers);
            Log("REV22_29_PNP_LINKED_ROW_COUNT="+pnpRead.LinkedRows);
            Log("REV22_29_PNP_MEMBERS_WITH_SIZE="+pnpRead.MembersWithSize);
            Log("REV22_29_ANCHOR_SOURCE_MEMBER_COUNT="+sourceSteel.Members.Count);
            Log("REV22_29_PNP_SUPPORT_CANDIDATES="+plantSupports.CandidateCount);
            Log("REV22_29_PNP_SUPPORT_LINKED_ROWS="+plantSupports.LinkedRowCount);
            Log("REV22_29_SELECTED_PIPE_IDS="+selectedPipeIds.Count);
            Log("REV22_37_XREF_MEMBER_NAMES_ENABLED="+options.XrefMemberNames);
            Log("REV22_37_NEARBY_XREF_MEMBER_COUNT="+xrefMembers.Items.Count);
            Log("REV22_37_XREF_MEMBER_IN_CUBE_COUNT="+xrefNearCube.Length);
            foreach(var xrefMember in xrefNearCube)
                Log("REV22_37_XREF_SOURCE="+xrefMember.Key+
                    " KIND="+xrefMember.Kind+
                    " SOURCE_DWG="+xrefMember.DrawingName+
                    " SOURCE_FILE="+xrefMember.DrawingPath);
            foreach(string diagnostic in xrefMembers.Diagnostics)
                Log("REV22_37_XREF_DIAGNOSTIC="+diagnostic);
            Log("REV22_29_PIPE_CANDIDATES="+pipeRead.Candidates);
            Log("REV22_29_PIPE_PNP_LINKED_ROWS="+pipeRead.LinkedRows);
            Log("REV22_29_PIPE_ANNOTATION_SOURCES="+pipeRead.Pipes.Count);
            foreach(var pipe in pipeRead.Pipes)
                Log("REV22_29_PIPE_SOURCE="+pipe.Handle+
                    " LINE_TAG="+pipe.LineTag+
                    " LINE_SOURCE="+pipe.LineSource+
                    " "+pipe.BopLabel+
                    " BOP_SOURCE="+pipe.BopSource+
                    " ANCHOR_SOURCE="+pipe.AnchorSource+
                    " WCS="+Point(pipe.X,pipe.Y,pipe.Z));
            foreach(var diagnostic in pipeRead.Diagnostics)
                Log("REV22_29_PIPE_DIAGNOSTIC="+diagnostic);
            Log("REV22_29_PNP_SUPPORT_ACCEPTED="+plantSupports.Members.Count);
            Log("REV22_29_SUPPORT_TOS_EXPLICIT_COUNT="+
                plantSupports.Members.Count(x=>
                    x.TosSource=="SUPPORT_EXPLICIT_TOP_OF_STEEL"));
            Log("REV22_29_SUPPORT_TOS_APPROX_COUNT="+
                plantSupports.Members.Count(x=>
                    x.TosSource=="SUPPORT_GEOMETRIC_TOP_APPROX"));
            Log("REV22_29_SUPPORT_TOS_UNAVAILABLE_COUNT="+
                plantSupports.Members.Count(x=>
                    x.TosSource=="SUPPORT_TOS_UNAVAILABLE"));
            Log("REV22_29_COMBINED_ANNOTATION_SOURCE_COUNT="+
                sourceAnnotations.Length);
            foreach(var support in plantSupports.Members)
                Log("REV22_29_SUPPORT_ANNOTATION_SOURCE="+support.Handle+
                    " PNP_CLASS="+support.PnpClass+
                    " DESCRIPTION="+support.Description+
                    " SIZE="+support.Size+
                    " SPEC="+support.Spec+
                    " TOS="+SupportOrthoSupportTos25.Display(
                        new SupportOrthoSupportTos25.Observation(
                            support.TosText,support.TosSource))+
                    " TOS_SOURCE="+support.TosSource+
                    " WCS="+Point(support.X,support.Y,support.Z));
            foreach(var warning in plantSupports.Diagnostics)
                Log("REV22_29_SUPPORT_DIAGNOSTIC="+warning);
            Log("REV22_29_HORIZONTAL_SOURCE_MEMBER_COUNT="+horizontalSteel.HorizontalCount);
            Log("REV22_29_TOS_EXACT_PROPERTY_COUNT="+horizontalSteel.PropertyTosCount);
            Log("REV22_29_TOS_GEOMETRIC_APPROX_COUNT="+horizontalSteel.ApproximateTosCount);
            foreach(var member in horizontalSteel.Members)
                Log("REV22_29_MEMBER_TOS="+member.Handle+
                    " VERTICAL="+member.IsHorizontal+
                    " SIZE="+member.Size+
                    " TOS="+(member.TosText??"UNAVAILABLE")+
                    " SOURCE="+member.TosSource);
            foreach(var diagnostic in horizontalSteel.Diagnostics)
                Log("REV22_29_TOS_DIAGNOSTIC="+diagnostic);
            foreach(var member in sourceSteel.Members)
                Log("REV22_29_SOURCE_MEMBER="+member.Handle+
                    " SIZE="+member.Size+" WCS="+Point(
                        member.X,member.Y,member.Z));
            foreach(var warning in sourceSteel.Diagnostics)
                Log("REV22_29_SOURCE_MEMBER_WARNING="+warning);
            foreach(var diagnostic in pnpRead.Diagnostics)
                Log("REV22_29_PNP_DIAGNOSTIC="+diagnostic);
            foreach(var evidence in structuralMetadata.Evidence)
                Log("REV22_29_SOURCE_EVIDENCE="+evidence);
            foreach(var warning in structuralMetadata.Warnings)
                Log("REV22_29_SOURCE_WARNING="+warning);
            Log("REV22_29_CUBE_MODE="+(supportFocus?"SupportFocus":"Original"));
            Log("REV22_29_SUPPORT_RAW_MIN="+Point(
                geometry.Value.MinX,geometry.Value.MinY,geometry.Value.MinZ));
            Log("REV22_29_SUPPORT_RAW_MAX="+Point(
                geometry.Value.MaxX,geometry.Value.MaxY,geometry.Value.MaxZ));
            Log("REV22_29_SELECTED_PIPE_BOUNDS_COUNT="+localPipeBounds.Count);
            if(focusPlan!=null)
            {
                Log("REV22_29_AUTOMATIC_CLEARANCE=True");
                Log("REV22_29_AUTO_XY_CLEARANCE_MM="+focusXYClearanceMm);
                Log("REV22_29_AUTO_PIPE_REACH_MM="+focusPipeReachMm);
                Log("REV22_29_XY_CLEARANCE_MM="+focusXYClearanceMm);
                Log("REV22_29_VERTICAL_PIPE_REACH_MM="+focusPipeReachMm);
                Log("REV22_29_PIPE_BBOX_ACCEPTED="+focusPlan.PipesAccepted);
                Log("REV22_29_PIPE_BBOX_REJECTED="+focusPlan.PipesRejected);
                Log("REV22_29_PIPE_Z_CLIPPED="+focusPlan.PipesVerticalClipped);
                Log("REV22_29_FOCUS_XY_UNCHANGED_BY_PIPES=True");
                Log("REV22_29_FOCUSED_MIN="+Point(
                    focusPlan.FocusBounds.MinX,
                    focusPlan.FocusBounds.MinY,focusPlan.FocusBounds.MinZ));
                Log("REV22_29_FOCUSED_MAX="+Point(
                    focusPlan.FocusBounds.MaxX,
                    focusPlan.FocusBounds.MaxY,focusPlan.FocusBounds.MaxZ));
            }
            Log("EMPTY_NATIVE_SEED=" + seed);
            Log("NEW_CLONE=" + dest);
            Log("NATIVE_CUBE_MIN=" + Point(layout.PaddedCube.MinX,
                layout.PaddedCube.MinY,layout.PaddedCube.MinZ));
            Log("NATIVE_CUBE_MAX=" + Point(layout.PaddedCube.MaxX,
                layout.PaddedCube.MaxY,layout.PaddedCube.MaxZ));
            Log("ONE_CLICK_ANCHOR_REQUIRED=TRUE");
            Log("PUBLIC_API_ONLY=FALSE");
            Log("REV22_35_SELECTED_VIEWS="+string.Join(",",options.Views));
            Log("REV22_35_SCALE_REQUEST="+
                (options.ScaleDenominator==0?"AUTO":"1:"+options.ScaleDenominator));
            Log("REV22_35_ANNOTATION_OPTIONS=LINE:"+options.LineNumber+
                " BOP:"+options.Bop+" TOS:"+options.Tos+
                " DESCRIPTION:"+options.Description);
            Log("REV22_35_BATCH_MODE="+options.BatchOrtho);

            // REV16-proven native DWG seed clone; never clone a DWT.
            using (var db = new Database(false, true))
            {
                // REV22.29: The FileShare overload can throw eFileSharingViolation
                // when the native Project Manager Ortho seed is open in Plant 3D.
                // Autodesk recommends the FileOpenMode API for shared read access.
                // CloseInput materializes the lazy load before the SaveAs clone.
                db.ReadDwgFile(seed, FileOpenMode.OpenForReadAndAllShare, true, null);
                db.CloseInput(true);
                db.SaveAs(dest, DwgVersion.Current);
            }
            cloned = File.Exists(dest);
            if (!cloned) throw new IOException("Native seed clone did not create DWG.");
            Log("STEP1_NATIVE_SEED_CLONED=PASS");

            MethodInfo add = part.OrthoPart.GetType().GetMethod("AddPnPDrawingFile",
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] {typeof(string),typeof(string),typeof(string),
                    typeof(bool).MakeByRefType(),typeof(bool).MakeByRefType()}, null) ??
                throw new MissingMethodException("OrthoProject.AddPnPDrawingFile is unavailable.");
            object?[] addArgs = { dest, "", Path.GetFileNameWithoutExtension(dest), false, false };
            registrationAttempted = true;
            Log("STEP2_NATIVE_ADD_PNP_DRAWING_ENTERED");
            object? guid = add.Invoke(part.OrthoPart, addArgs);
            Log("STEP2_ADD_RETURN=" + guid);
            Log("STEP2_ALREADY_EXISTS=" + addArgs[3]);
            Log("STEP2_ILLEGAL_XREFS=" + addArgs[4]);
            if (!Guid.TryParse(guid?.ToString(), out _) ||
                addArgs[3] is true || addArgs[4] is true)
                throw new InvalidOperationException("Native Ortho project registration did not return a new valid drawing GUID.");
            if (!IsNativeOrthoValid(part.OrthoPart, dest))
                throw new InvalidOperationException("Native registration returned a GUID, but OrthoDocUtils rejected new DWG.");
            if (ViewCatalog(part.OrthoPart, dest) != "EMPTY_REGISTERED_ORTHO")
                throw new InvalidOperationException("New DWG is not proven empty after native registration.");
            Log("STEP2_PROJECT_REGISTERED_EMPTY_ORTHO=PASS");

            // Session command can switch AutoCAD documents. Do not close or
            // edit the source 3D model; retain it for native model linking.
            Log("STEP3_DOCUMENT_OPEN_ENTERED");
            targetDoc = AcadApp.DocumentManager.Open(dest, false);
            if (targetDoc == null)
                throw new InvalidOperationException("AutoCAD returned no document for new native Ortho DWG.");
            AcadApp.DocumentManager.MdiActiveDocument = targetDoc;
            string active = AcadApp.DocumentManager.MdiActiveDocument?.Database.Filename ?? "";
            if (!string.Equals(Path.GetFullPath(active), Path.GetFullPath(dest),
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Document switching did not activate new Ortho DWG.");
            Log("STEP3_ORTHO_DOCUMENT_ACTIVE=PASS");

            // Find an actual configured paper-space layout from the cloned seed.
            // Layout changes are managed in the command's active session context.
            string paperLayout = FindPaperLayout(targetDoc.Database);
            if (string.IsNullOrWhiteSpace(paperLayout))
                throw new InvalidOperationException("No valid paper-space layout in native Ortho DWG.");
            if (!string.Equals(LayoutManager.Current.CurrentLayout, paperLayout,
                StringComparison.OrdinalIgnoreCase))
            {
                using (DocumentLock locked = targetDoc.LockDocument())
                    LayoutManager.Current.CurrentLayout = paperLayout;
            }
            if (targetDoc.Database.TileMode)
                throw new InvalidOperationException("AutoCAD remains in model space after paper layout selection.");
            (double paperWidth, double paperHeight) = CurrentPaper(targetDoc.Database, paperLayout);
            NativeOrthoTenViewPlan.Plan candidate =
                NativeOrthoTenViewPlan.Build(layout, paperWidth, paperHeight);
            if(options.ScaleDenominator>0)
                candidate=candidate with {
                    Denominator=options.ScaleDenominator,
                    Scale=1.0/options.ScaleDenominator
                };
            Log("STEP4_SHEET_LAYOUT=" + paperLayout);
            Log("STEP4_SHEET_MM=" + Point(paperWidth,paperHeight,0));
            Log("STEP4_COMMON_SCALE=1:" + candidate.Denominator);
            foreach(var slot in candidate.Views)
                Log("PROPOSED_" + slot.Name + "_CENTER=" +
                    Point(slot.CenterX,slot.CenterY,0) +
                    "_TILE=" + Point(slot.W,slot.H,0));
            Log("PROPOSED_LAYOUT_NOT_ENFORCED_BY_NATIVE_API=True");
            Log("PUBLIC_API_HAS_NO_INSERTION_POINT_ARGUMENT=TRUE");

            // REV22.29: A Session command can activate a different DWG without
            // entering that drawing's document/command execution context.
            // REV22.29 yielded sentinel +/-1e20 Extents after InitializeCube,
            // whereas REV18 succeeded when invoked within the Ortho document.
            // Schedule all native cube + view generation in target document
            // context, preserving the exact strict extents validation.
            Log("STEP5_SESSION_CONTEXT_IS_APPLICATION=" +
                AcadApp.DocumentManager.IsApplicationContext);
            Log("STEP5_TARGET_COMMAND_CONTEXT_REQUESTED");
            await AcadApp.DocumentManager.ExecuteInCommandContextAsync(_ =>
            {
                // REV22.4: distinguish managed wrapper object identity from
                // the actual DWG identity. If a Session document switch left
                // the working DB on the source DWG, synchronize it to the
                // verified active target before creating any native OrthoCube.
                Database? originalWorkingDatabase = null;
                bool workingDbWasChanged = false;
                try
                {
                    Log("STEP5_TARGET_COMMAND_CONTEXT_ENTERED");
                    bool appContext = AcadApp.DocumentManager.IsApplicationContext;
                    bool activeMatches = ReferenceEquals(
                        AcadApp.DocumentManager.MdiActiveDocument, targetDoc);
                    originalWorkingDatabase = HostApplicationServices.WorkingDatabase;
                    string beforePath = originalWorkingDatabase?.Filename ?? "";
                    string targetPath = targetDoc.Database.Filename ?? "";
                    bool wrapperMatches = ReferenceEquals(
                        originalWorkingDatabase, targetDoc.Database);
                    bool sameDrawing = !string.IsNullOrWhiteSpace(beforePath) &&
                        !string.IsNullOrWhiteSpace(targetPath) &&
                        string.Equals(Path.GetFullPath(beforePath),
                            Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase);
                    Log("STEP5_TARGET_CONTEXT_IS_APPLICATION=" + appContext);
                    Log("STEP5_TARGET_CONTEXT_ACTIVE_DOC_MATCH=" + activeMatches);
                    Log("STEP5_WORKING_DB_BEFORE=" + beforePath);
                    Log("STEP5_EXPECTED_WORKING_DB=" + targetPath);
                    Log("STEP5_WORKING_DB_WRAPPER_EQUALS=" + wrapperMatches);
                    Log("STEP5_TARGET_CONTEXT_WORKING_DB_MATCH=" + sameDrawing);
                    if (appContext || !activeMatches)
                        throw new InvalidOperationException(
                            "Target Ortho document command context is unavailable. " +
                            "Native TOP generation stopped before touching cube geometry.");

                    if (!sameDrawing)
                    {
                        Log("STEP5_WORKING_DB_SYNC_ENTERED");
                        HostApplicationServices.WorkingDatabase = targetDoc.Database;
                        workingDbWasChanged = true;
                        Log("STEP5_WORKING_DB_SYNC_RETURNED");
                    }
                    string effective = HostApplicationServices.WorkingDatabase?.Filename ?? "";
                    bool verifiedWorking = !string.IsNullOrWhiteSpace(effective) &&
                        string.Equals(Path.GetFullPath(effective),
                            Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase);
                    Log("STEP5_WORKING_DB_EFFECTIVE=" + effective);
                    Log("STEP5_WORKING_DB_VERIFIED_TARGET=" + verifiedWorking);
                    if (!verifiedWorking)
                        throw new InvalidOperationException(
                            "AutoCAD working database still differs from native Ortho " +
                            "target after command-context synchronization. See REV22.4 log.");

            // REV22 ISOLATED EXPERIMENT: Non-public GenerateOrthos requires a
            // preexisting native paper-space Viewport ObjectId and insertion
            // Point3d. Exact signature is verified before automatic placement.
            // Unsupported calls are limited to a NEW registered Ortho clone in
            // the user-confirmed COPIED test project.
            Assembly[] loaded=AppDomain.CurrentDomain.GetAssemblies();
            Type cubeType=FindType(loaded,
                "Autodesk.ProcessPower.PnP3dOrthoDrawingsUI.PlantOrthoCube") ??
                throw new InvalidOperationException("Native PlantOrthoCube unavailable.");
            Type viewType=FindType(loaded,
                "Autodesk.ProcessPower.PnP3dOrthoDrawingsUI.PlantOrthoView") ??
                throw new InvalidOperationException("Native PlantOrthoView unavailable.");
            MethodInfo init=cubeType.GetMethod("InitializeCube",
                BindingFlags.Public|BindingFlags.Instance,null,
                new[]{typeof(Extents3d)},null) ??
                throw new MissingMethodException("Native InitializeCube(Extents3d)");
            MethodInfo makeSolid=cubeType.GetMethod("MakeCubeCopy",
                BindingFlags.Public|BindingFlags.Instance,null,Type.EmptyTypes,null) ??
                throw new MissingMethodException("Native MakeCubeCopy()");
            MethodInfo create=viewType.GetMethods(BindingFlags.Public|BindingFlags.Instance)
                .FirstOrDefault(m=>m.Name=="CreateOrthos"&&m.GetParameters().Length==13) ??
                throw new MissingMethodException("Public CreateOrthos(13 args), required for enum discovery");
            ConstructorInfo ctor=viewType.GetConstructor(new[]{typeof(bool),typeof(bool)}) ??
                throw new MissingMethodException("PlantOrthoView(bool,bool)");
            Type nativeEnum=create.GetParameters()[6].ParameterType;
            if(!nativeEnum.IsEnum)throw new InvalidOperationException("Native ViewType is not an enum.");
            string[] available=Enum.GetNames(nativeEnum);
            Log("STEP5_NATIVE_VIEW_TYPES="+string.Join(",",available));
            // REV22.29: native Edit View string parser is queried before
            // picking stored preset names for all SIX standard views.
            Type nativeUi=FindType(loaded,
                "Autodesk.ProcessPower.PnP3dOrthoDrawingsUI.UIUtils") ??
                throw new InvalidOperationException("Native UIUtils missing.");
            MethodInfo parser=nativeUi.GetMethod("GetViewTypeFromName",
                BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static,
                null,new[]{typeof(string)},null) ??
                throw new MissingMethodException("UIUtils.GetViewTypeFromName(string)");
            var resolved=new List<(NativeOrthoTenViewPlan.Slot Slot,object ViewEnum,string TypeName)>();
            foreach(var slot in candidate.Views)
            {
                if(!options.HasView(slot.Name))continue;
                string selected=slot.EnumName;
                if(slot.Name=="SIDE"&&!available.Any(x=>x.Equals(selected,StringComparison.OrdinalIgnoreCase)) &&
                    available.Any(x=>x.Equals("Side",StringComparison.OrdinalIgnoreCase)))
                    selected="Side";
                if(!available.Any(x=>x.Equals(selected,StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Native ViewType "+selected+" missing for "+slot.Name);
                // Keep proven NW/NE/SW/SE type strings; resolve axial
                // presets against *this installation's* Edit View parser.
                object nativeValue=Enum.Parse(nativeEnum,selected,true);
                string nativeString=NativeOrthoEditorPreset.Resolve(
                    slot.Name,selected=="Side"?"SideView":slot.TypeName);
                if(NativeOrthoAxialEditorPreset.IsAxial(slot.Name))
                {
                    Log("STEP5_"+slot.Name+"_AXIAL_CANDIDATES="+
                        string.Join(",",NativeOrthoAxialEditorPreset.Candidates(
                            slot.Name,slot.TypeName)));
                    nativeString=NativeOrthoAxialEditorPreset.Resolve(
                        slot.Name,slot.TypeName,
                        Convert.ToInt32(nativeValue,CultureInfo.InvariantCulture),
                        candidateLabel=>
                        {
                            try
                            {
                                object? parsedValue=parser.Invoke(
                                    null,new object[]{candidateLabel});
                                return parsedValue==null?(int?)null:
                                    Convert.ToInt32(parsedValue,CultureInfo.InvariantCulture);
                            }
                            catch(TargetInvocationException){ return null; }
                        });
                }
                if(NativeOrthoIsoViewType.IsIsometric(slot.Name)&&
                    (!NativeOrthoIsoViewType.HasExpectedQuadrant(slot.Name,
                        slot.Dx,slot.Dy,slot.Dz)||
                     !string.Equals(selected,slot.Name,StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException(
                        "REV22.29 unexpected "+slot.Name+
                        " native enum or camera quadrant, refusing Current View fallback.");
                resolved.Add((slot,nativeValue,nativeString));
                Log("STEP5_"+slot.Name+"_ENUM="+selected+
                    " OLD_TYPE_NAME="+slot.TypeName+
                    " NATIVE_TYPE_NAME="+nativeString+
                    " ISOMETRIC="+NativeOrthoIsoViewType.IsIsometric(slot.Name));
            }
            // REV22.29: verify ALL TEN strings, not just four isometrics.
            // Unknown/native Current fallback is a hard pre-generation failure.
            foreach(var spec in resolved)
            {
                if(NativeOrthoIsoViewType.IsIsometric(spec.Slot.Name) &&
                   !NativeOrthoEditorPreset.IsExactIsoPreset(
                       spec.Slot.Name,spec.TypeName))
                    throw new InvalidOperationException("REV22.29 "+
                        spec.Slot.Name+" invalid saved ISO type: "+spec.TypeName);
                object? parsed=parser.Invoke(null,new object[]{spec.TypeName});
                bool match=parsed!=null &&
                    Convert.ToInt32(parsed,CultureInfo.InvariantCulture)==
                    Convert.ToInt32(spec.ViewEnum,CultureInfo.InvariantCulture);
                Log("STEP5_"+spec.Slot.Name+"_EDITOR_PRESET="+
                    spec.TypeName+" MATCHED_NATIVE_ENUM="+match);
                if(!match)
                    throw new InvalidOperationException("REV22.29 "+
                        spec.Slot.Name+" maps to Current View in native Editor.");
            }
            if(candidate.Views.Count!=10 || resolved.Count!=options.Views.Count ||
               resolved.Count==0)
                throw new InvalidOperationException("SPTC native view selection does not match requested presets.");
            if(resolved.Select(v=>v.Slot.EnumName)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count()!=resolved.Count)
                throw new InvalidOperationException("SPTC duplicate native view-type enum detected.");
            Type[] expectedNativeSignature={
                typeof(StringCollection),typeof(string),typeof(string),
                typeof(Point3d),typeof(Point3d),nativeEnum,typeof(Point3d),
                typeof(Vector3d),typeof(Vector3d),typeof(ObjectId),
                typeof(double),typeof(double),typeof(double),typeof(double),
                typeof(double),typeof(Solid3d)
            };
            var nativeMethods=viewType.GetMethods(BindingFlags.NonPublic|BindingFlags.Instance)
                .Where(m=>m.Name=="GenerateOrthos").ToArray();
            foreach(var m in nativeMethods)
                Log("STEP5_INTERNAL_SIGNATURE="+m);
            MethodInfo? generate=nativeMethods.FirstOrDefault(m=>
                m.GetParameters().Select(p=>p.ParameterType)
                .SequenceEqual(expectedNativeSignature));
            Log("STEP5_UNSUPPORTED_GENERATE_SIGNATURE_MATCH="+(generate!=null));
            if(generate==null)
                throw new MissingMethodException(
                    "Exact internal GenerateOrthos(16 arguments) signature not found. " +
                    "This Plant 3D release is NOT safe for the REV22 experiment.");
            Log("STEP5_UNSUPPORTED_INTERNAL_API=TRUE");
            var bounds=layout.PaddedCube;
            var extents=new Extents3d(
                new Point3d(bounds.MinX,bounds.MinY,bounds.MinZ),
                new Point3d(bounds.MaxX,bounds.MaxY,bounds.MaxZ));
            Log("STEP5_CUBE_EXPECTED_MIN="+Point(bounds.MinX,bounds.MinY,bounds.MinZ));
            Log("STEP5_CUBE_EXPECTED_MAX="+Point(bounds.MaxX,bounds.MaxY,bounds.MaxZ));

            // REV22.29: NO PAPER POINT PROMPT, NO MOUSE CLICK.
            // Derive the entire two-column x five-row arrangement from
            // the native ten-view candidate, preserve camera/scale and
            // reserve all ten title bands before ANY native generation.
            var autoPlacement=NativeOrthoTenViewAutoPlacement26.Build(
                candidate,paperWidth,paperHeight);
            var group=autoPlacement.Views;
            Log("STEP5_AUTO_PLACEMENT_ENABLED=True");
            Log("STEP5_PAPER_POINT_CLICKS_REQUIRED=0");
            Log("STEP5_AUTO_TOP_SUGGESTED="+
                Point(autoPlacement.SuggestedTopX,autoPlacement.SuggestedTopY,0));
            Log("STEP5_AUTO_TOP_EFFECTIVE="+
                Point(autoPlacement.EffectiveTopX,autoPlacement.EffectiveTopY,0));
            Log("STEP5_AUTO_TOP_FINAL="+
                Point(autoPlacement.FinalTopX,autoPlacement.FinalTopY,0));
            Log("STEP5_AUTO_TITLE_SHIFT_Y_MM="+
                autoPlacement.TitleShiftY.ToString("0.###",CultureInfo.InvariantCulture));
            Log("STEP5_AUTO_GROUP_SHIFTED="+autoPlacement.WasShifted);
            targetDoc.Editor.WriteMessage(
                "\nSPTC: Automatically placing "+resolved.Count+" selected native Ortho "+
                "view(s) and titles. No insertion click required. "+
                "TOP center: ("+
                autoPlacement.FinalTopX.ToString("0.###",CultureInfo.InvariantCulture)+
                ", "+autoPlacement.FinalTopY.ToString("0.###",CultureInfo.InvariantCulture)+").");
            var planned=new List<(NativeOrthoTenViewPlan.Slot Slot,
                object ViewEnum,string TypeName,Point3d Center,double Width,double Height)>();
            foreach(var item in resolved)
            {
                var plannedPosition=group.First(g=>g.Name==item.Slot.Name);
                planned.Add((item.Slot,item.ViewEnum,item.TypeName,
                    new Point3d(plannedPosition.CenterX,plannedPosition.CenterY,0),
                    plannedPosition.PaperFrameWidth,plannedPosition.PaperFrameHeight));
                Log("STEP5_"+item.Slot.Name+"_AUTO_CENTER="+
                    Point(plannedPosition.CenterX,plannedPosition.CenterY,0)+
                    " FRAME="+Point(plannedPosition.PaperFrameWidth,
                        plannedPosition.PaperFrameHeight,0));
            }
            var priorHandles=new HashSet<string>(
                PaperViewports(targetDoc.Database,paperLayout).Select(x=>x.Handle),
                StringComparer.OrdinalIgnoreCase);
            var completed=new List<string>();
            var confirmedFrames=new List<NativeOrthoViewportPaperGuard.Frame>();
            var nativeMemberViewportIds=new Dictionary<string,ObjectId>(
                StringComparer.OrdinalIgnoreCase);
            string stamp=DateTime.UtcNow.ToString("HHmmss",CultureInfo.InvariantCulture);
            foreach(var item in planned)
            {
                string label=item.Slot.Name;
                Log("STEP6_"+label+"_BEGIN");
                object cube=Activator.CreateInstance(cubeType) ??
                    throw new InvalidOperationException(label+": failed to instantiate native cube.");
                try
                {
                    init.Invoke(cube,new object[]{extents});
                    if(cubeType.GetProperty("Extents")?.GetValue(cube) is not Extents3d got)
                        throw new InvalidOperationException(label+": no native cube extents.");
                    Log("STEP6_"+label+"_NATIVE_MIN="+
                        Point(got.MinPoint.X,got.MinPoint.Y,got.MinPoint.Z));
                    Log("STEP6_"+label+"_NATIVE_MAX="+
                        Point(got.MaxPoint.X,got.MaxPoint.Y,got.MaxPoint.Z));
                    bool matches=Near(got.MinPoint.X,bounds.MinX)&&
                        Near(got.MinPoint.Y,bounds.MinY)&&Near(got.MinPoint.Z,bounds.MinZ)&&
                        Near(got.MaxPoint.X,bounds.MaxX)&&Near(got.MaxPoint.Y,bounds.MaxY)&&
                        Near(got.MaxPoint.Z,bounds.MaxZ);
                    Log("STEP6_"+label+"_CUBE_EXTENTS_MATCHED="+matches);
                    if(!matches)throw new InvalidOperationException(
                        label+": native OrthoCube differs from selected support bounds.");
                    if(makeSolid.Invoke(cube,null) is not Solid3d solid)
                        throw new InvalidOperationException(label+": native cube did not return Solid3d.");
                    using(solid)
                    {
                        if(!ReferenceEquals(AcadApp.DocumentManager.MdiActiveDocument,targetDoc))
                            throw new InvalidOperationException(label+": expected native Ortho not active.");
                        if(LayoutManager.Current.CurrentLayout!=paperLayout)
                            throw new InvalidOperationException(label+": native paper layout changed.");
                        string before=ViewCatalog(part.OrthoPart,dest);
                        if(!(completed.Count==0?
                            before=="EMPTY_REGISTERED_ORTHO":CatalogMatches(before,completed)))
                            throw new InvalidOperationException(label+": unexpected native view catalog: "+before);
                        string viewName="SPTC_"+label+"_R22_"+stamp;
                        Log("STEP7_"+label+"_VIEW_NAME="+viewName);
                        Log("STEP7_"+label+"_VIEW_DIRECTION="+
                            Point(item.Slot.Dx,item.Slot.Dy,item.Slot.Dz));
                        Log("STEP7_"+label+"_UP_DIRECTION="+
                            Point(item.Slot.Ux,item.Slot.Uy,item.Slot.Uz));
                        Log("STEP7_"+label+"_CONSTRUCT_NATIVE_ENTERED");
                        object nativeView=ctor.Invoke(new object[]{false,false});
                        Log("STEP7_"+label+"_CONSTRUCT_NATIVE_RETURNED");
                        try
                        {
                            if(!ReferenceEquals(AcadApp.DocumentManager.MdiActiveDocument,targetDoc))
                                throw new InvalidOperationException(
                                    label+": native constructor switched active document.");
                            // The unsupported method expects an ObjectId of a
                            // native paper viewport already belonging to the
                            // active Ortho layout. NEVER pass ObjectId.Null.
                            ObjectId viewportId=CreateExperimentalViewport(
                                targetDoc.Database,paperLayout,item.Center,
                                item.Width,item.Height,
                                new Vector3d(item.Slot.Dx,item.Slot.Dy,item.Slot.Dz),
                                new Point3d(bounds.MidX,bounds.MidY,bounds.MidZ),
                                candidate.Scale,item.Slot.ModelHmm);
                            Log("STEP7_"+label+"_CREATED_VIEWPORT_ID="+viewportId);
                            if(NativeOrthoIsoViewType.IsIsometric(label))
                            {
                                Vector3d configured=ReadViewportCamera(
                                    targetDoc.Database,viewportId);
                                bool correct=NativeOrthoIsoViewType.MatchesCamera(
                                    label,configured.X,configured.Y,configured.Z,
                                    item.Slot.Dx,item.Slot.Dy,item.Slot.Dz);
                                Log("STEP7_"+label+"_ISOMETRIC_CAMERA_BEFORE="+
                                    Point(configured.X,configured.Y,configured.Z));
                                Log("STEP7_"+label+"_ISOMETRIC_CAMERA_MATCH_BEFORE="+correct);
                                if(!correct)throw new InvalidOperationException(
                                    "REV22.29 "+label+
                                    " pre-generation camera differs from requested isometric preset.");
                            }
                            Log("STEP7_"+label+"_NATIVE_TYPE_STRING="+item.TypeName);
                            object[] args={
                                new StringCollection{source},viewName,item.TypeName,
                                new Point3d(bounds.MinX,bounds.MinY,bounds.MinZ),
                                new Point3d(bounds.MaxX,bounds.MaxY,bounds.MaxZ),
                                item.ViewEnum,item.Center,
                                new Vector3d(item.Slot.Dx,item.Slot.Dy,item.Slot.Dz),
                                new Vector3d(item.Slot.Ux,item.Slot.Uy,item.Slot.Uz),
                                viewportId,item.Width,item.Height,bounds.SizeZ,
                                candidate.Scale,0.0,solid
                            };
                            // REV22.29: REV22.2 passed raw bounds.SizeX/SizeY as
                            // viewportWidth/viewportHeight. Its native call then
                            // changed a planned 76.163 x 19.950 paper viewport
                            // into a 362.680 x 95.000 viewport. The API argument
                            // names and observed equality indicate the requested
                            // paper frame dimensions belong here instead.
                            // Treat this as an EXPERIMENT, validated immediately
                            // against the ACTUAL resulting AutoCAD Viewport.
                            Log("STEP8_"+label+"_NATIVE_REQUESTED_PAPER_FRAME="+
                                Point(item.Width,item.Height,0));
                            Log("STEP8_"+label+"_PRIVATE_GENERATEORTHOS_ENTERED");
                            generate.Invoke(nativeView,args);
                            Log("STEP8_"+label+"_PRIVATE_GENERATEORTHOS_RETURNED");
                            if(NativeOrthoAxialEditorPreset.IsAxial(label))
                            {
                                // REV22.29 ISOLATED AXIAL HALF-TURN experiment:
                                // Manual Update View visibly rotates all SIX
                                // axial projections ~180 degrees. Exact twist
                                // values BEFORE/AFTER were never recorded in
                                // REV22.9. Rotate either 0->180 or 180->0, and
                                // invert BOTH ViewCenter axes in one transaction,
                                // as REV22.7 did for four validated ISO views.
                                // Never alter the NW/NE/SW/SE code branch.
                                var beforeAxial=ReadIsoViewportState(
                                    targetDoc.Database,viewportId);
                                Vector3d axialCameraBefore=ReadViewportCamera(
                                    targetDoc.Database,viewportId);
                                Vector3d axialExpectedCamera=new Vector3d(
                                    item.Slot.Dx,item.Slot.Dy,item.Slot.Dz);
                                bool beforeCameraMatch=
                                    axialCameraBefore.Length>1e-12&&
                                    axialExpectedCamera.Length>1e-12&&
                                    axialCameraBefore.GetNormal().DotProduct(
                                        axialExpectedCamera.GetNormal())>0.99999;
                                Log("STEP8_"+label+"_AXIAL_CAMERA_MATCH_BEFORE="+
                                    beforeCameraMatch);
                                Log("STEP8_"+label+"_AXIAL_TWIST_BEFORE_DEG="+
                                    (beforeAxial.TwistRadians*180/Math.PI).ToString(
                                        "0.######",CultureInfo.InvariantCulture));
                                Log("STEP8_"+label+"_AXIAL_VIEWCENTER_BEFORE="+
                                    Point(beforeAxial.CenterX,beforeAxial.CenterY,0));
                                if(!beforeCameraMatch)
                                    throw new InvalidOperationException(
                                        "REV22.29 "+label+" camera mismatch; refusing to rotate.");
                                var expectedAxial=NativeOrthoAxialHalfTurnGuard.Flip(
                                    beforeAxial);
                                SetIsoViewportPose(
                                    targetDoc.Database,viewportId,expectedAxial);
                                var afterAxial=ReadIsoViewportState(
                                    targetDoc.Database,viewportId);
                                Vector3d axialCameraAfter=ReadViewportCamera(
                                    targetDoc.Database,viewportId);
                                bool afterCameraMatch=
                                    axialCameraAfter.Length>1e-12&&
                                    axialCameraAfter.GetNormal().DotProduct(
                                        axialExpectedCamera.GetNormal())>0.99999;
                                bool axialPass=NativeOrthoAxialHalfTurnGuard.Matches(
                                    expectedAxial,afterAxial)&&afterCameraMatch;
                                Log("STEP8_"+label+"_AXIAL_TWIST_AFTER_DEG="+
                                    (afterAxial.TwistRadians*180/Math.PI).ToString(
                                        "0.######",CultureInfo.InvariantCulture));
                                Log("STEP8_"+label+"_AXIAL_VIEWCENTER_AFTER="+
                                    Point(afterAxial.CenterX,afterAxial.CenterY,0));
                                Log("STEP8_"+label+"_AXIAL_CAMERA_MATCH_AFTER="+
                                    afterCameraMatch);
                                Log("STEP8_"+label+"_AXIAL_FRAME_ROTATION_CHECK="+
                                    (axialPass?"PASS":"FAIL"));
                                if(!axialPass)
                                    throw new InvalidOperationException(
                                        "REV22.29 "+label+
                                        " half-turn/center adjustment failed readback.");
                            }
                            if(NativeOrthoIsoViewType.IsIsometric(label))
                            {
                                // REV22.29 isolated corrective test. REV22.4
                                // observed 180deg twist on NW/NE/SW/SE even
                                // though VIEW_DIRECTION and stored NW/NE/SW/SE
                                // native identifiers were correct. The WCS-Z
                                // projected UP vectors in NativeOrthoTenViewPlan
                                // require ZERO screen twist, not PI radians.
                                // Only newly generated test viewports are edited.
                                // Native Edit View / Update View may restore
                                // its own rotation; verify after regeneration.
                                // REV22.29: REV22.5 merely reset TwistAngle=0
                                // but left ViewCenter in the OLD 180deg view
                                // coordinate system. Thus it rendered geometry
                                // off-screen (empty frames) despite correct camera,
                                // roll and paper size. A native Update View changed
                                // center (347.236,-17.816) -> (-347.236,17.816).
                                // Adjust both properties atomically, ISO only.
                                var oldPose=ReadIsoViewportState(
                                    targetDoc.Database,viewportId);
                                Log("STEP8_"+label+"_ISO_TWIST_BEFORE_DEG="+
                                    (oldPose.TwistRadians*180/Math.PI).ToString(
                                        "0.######",CultureInfo.InvariantCulture));
                                Log("STEP8_"+label+"_ISO_VIEWCENTER_BEFORE="+
                                    Point(oldPose.CenterX,oldPose.CenterY,0));
                                if(!NativeOrthoIsoTwistGuard.IsProjectedWorldUp(
                                    item.Slot.Dx,item.Slot.Dy,item.Slot.Dz,
                                    item.Slot.Ux,item.Slot.Uy,item.Slot.Uz))
                                    throw new InvalidOperationException(
                                        "REV22.29 "+label+" view up differs from expected WCS +Z projection.");
                                var adjusted=NativeOrthoIsoFramingGuard.NormalizeHalfTurn(oldPose);
                                SetIsoViewportPose(targetDoc.Database,viewportId,adjusted);
                                var actualPose=ReadIsoViewportState(
                                    targetDoc.Database,viewportId);
                                Log("STEP8_"+label+"_ISO_TWIST_AFTER_DEG="+
                                    (actualPose.TwistRadians*180/Math.PI).ToString(
                                        "0.######",CultureInfo.InvariantCulture));
                                Log("STEP8_"+label+"_ISO_VIEWCENTER_AFTER="+
                                    Point(actualPose.CenterX,actualPose.CenterY,0));
                                bool frameMatch=NativeOrthoIsoFramingGuard.Matches(
                                    adjusted,actualPose);
                                Log("STEP8_"+label+"_TWIST_NORMALIZED_CHECK="+
                                    (NativeOrthoIsoTwistGuard.IsZeroTwist(
                                        actualPose.TwistRadians)?"PASS":"FAIL"));
                                Log("STEP8_"+label+"_VIEWCENTER_COMPENSATED="+
                                    adjusted.AppliedHalfTurn);
                                Log("STEP8_"+label+"_ISO_FRAME_COORDINATES_CHECK="+
                                    (frameMatch?"PASS":"FAIL"));
                                if(!frameMatch)
                                    throw new InvalidOperationException(
                                        "REV22.29 "+label+
                                        " viewport twist/center correction did not persist.");
                            }
                            var plannedFrame=new NativeOrthoViewportPaperGuard.Frame(
                                label,item.Center.X,item.Center.Y,item.Width,item.Height,
                                candidate.Scale,item.Slot.ModelHmm);
                            var actualFrame=ReadPaperViewportFrame(
                                targetDoc.Database,viewportId,label);
                            Log("STEP8_"+label+"_PLANNED_VIEWPORT="+
                                Point(plannedFrame.X,plannedFrame.Y,0)+" SIZE="+
                                Point(plannedFrame.W,plannedFrame.H,0)+
                                " SCALE="+plannedFrame.Scale);
                            Log("STEP8_"+label+"_ACTUAL_VIEWPORT="+
                                Point(actualFrame.X,actualFrame.Y,0)+" SIZE="+
                                Point(actualFrame.W,actualFrame.H,0)+
                                " SCALE="+actualFrame.Scale+" VIEW_HEIGHT="+actualFrame.ViewHeight);
                            string frameCheck=NativeOrthoViewportPaperGuard.Validate(
                                plannedFrame,actualFrame,paperWidth,paperHeight,confirmedFrames);
                            Log("STEP8_"+label+"_ACTUAL_PAPER_LAYOUT_CHECK="+frameCheck);
                            if(frameCheck!="PASS")
                                throw new InvalidOperationException("REV22.29 "+label+
                                    " failed actual paper-space post-generation guard: "+
                                    frameCheck+". Stop before creating more views; "+
                                    "retain this partially registered copied test DWG.");
                            confirmedFrames.Add(actualFrame);
                            nativeMemberViewportIds.Add(label,viewportId);
                            if(NativeOrthoAxialEditorPreset.IsAxial(label))
                            {
                                // REV22.29: read back the ACTUAL persisted
                                // IDwgViewDef.ViewType, not the requested label.
                                string storedAxial=ReadNativeStoredViewType(
                                    nativeView,viewName);
                                bool savedAxialMatch=string.Equals(
                                    storedAxial,item.TypeName,StringComparison.Ordinal);
                                Log("STEP8_"+label+"_SAVED_VIEWDEF_TYPE="+storedAxial);
                                Log("STEP8_"+label+"_SAVED_TYPE_CHECK="+
                                    (savedAxialMatch?"PASS":"FAIL"));
                                if(!savedAxialMatch)
                                    throw new InvalidOperationException(
                                        "REV22.29 "+label+" native Editor preset "+
                                        "not persisted as expected. Requested: "+
                                        item.TypeName+" actual: "+storedAxial);
                            }
                            if(NativeOrthoIsoViewType.IsIsometric(label))
                            {
                                // REV22.29: Edit View uses persisted native
                                // IDwgViewDef.ViewType, not only viewport angle.
                                string storedType=ReadNativeStoredViewType(nativeView,viewName);
                                Log("STEP8_"+label+"_SAVED_VIEWDEF_TYPE="+storedType);
                                if(storedType.StartsWith("UNAVAILABLE:",StringComparison.Ordinal))
                                    Log("STEP8_"+label+"_SAVED_TYPE_CHECK=INCONCLUSIVE");
                                else
                                {
                                    bool exact=NativeOrthoEditorPreset.IsExactIsoPreset(
                                        label,storedType);
                                    Log("STEP8_"+label+"_SAVED_TYPE_CHECK="+(exact?"PASS":"FAIL"));
                                    if(!exact)throw new InvalidOperationException(
                                        "REV22.29 "+label+
                                        " was not saved with the native Ortho Editor "+
                                        "preset identifier: "+storedType);
                                }
                                Vector3d actual=ReadViewportCamera(
                                    targetDoc.Database,viewportId);
                                bool isoCameraMatches=NativeOrthoIsoViewType.MatchesCamera(
                                    label,actual.X,actual.Y,actual.Z,
                                    item.Slot.Dx,item.Slot.Dy,item.Slot.Dz);
                                Log("STEP8_"+label+"_ISOMETRIC_CAMERA_AFTER="+
                                    Point(actual.X,actual.Y,actual.Z));
                                Log("STEP8_"+label+"_ISOMETRIC_CAMERA_MATCH_AFTER="+isoCameraMatches);
                                if(!isoCameraMatches)
                                    throw new InvalidOperationException(
                                        "REV22.29 "+label+
                                        " native generator reverted camera to Current View. "+
                                        "Do NOT trust this experimental test DWG.");
                            }
                        }
                        finally
                        {
                            if(nativeView is IDisposable dispose)dispose.Dispose();
                        }
                        completed.Add(viewName);
                        string after=ViewCatalog(part.OrthoPart,dest);
                        Log("STEP9_"+label+"_VIEW_CATALOG="+after);
                        bool registered=CatalogMatches(after,completed);
                        Log("STEP9_"+label+"_REGISTERED="+registered);
                        if(!registered)throw new InvalidOperationException(
                            label+": internal native generator returned, but the expected " +
                            "model-linked Ortho view was not registered. Stop.");
                    }
                }
                finally { if(cube is IDisposable disposeCube)disposeCube.Dispose(); }
            }
            Log("STEP10_SELECTED_NATIVE_VIEWS_REGISTERED="+completed.Count);
            Log("STEP10_REV22_GROUP_PLACED_AUTOMATICALLY=True");
            if(completed.Count==10)
                Log("STEP10_ALL_TEN_NATIVE_VIEWS_REGISTERED=True");
            var newFrames=PaperViewports(targetDoc.Database,paperLayout)
                .Where(v=>!priorHandles.Contains(v.Handle)).ToList();
            Log("STEP10_NEW_PAPER_VIEWPORT_COUNT="+newFrames.Count);
            bool overlaps=false,possibleTitleBlock=false;
            for(int i=0;i<newFrames.Count;i++)
            {
                var v=newFrames[i];
                Log("STEP10_VIEWPORT="+v.Handle+" CENTER="+Point(v.X,v.Y,0)+
                    " SIZE="+Point(v.W,v.H,0));
                if(v.X+v.W/2>paperWidth*.78||v.Y-v.H/2<paperHeight*.12)
                    possibleTitleBlock=true;
                for(int j=i+1;j<newFrames.Count;j++)
                {
                    var w=newFrames[j];
                    if(v.X-v.W/2<w.X+w.W/2&&w.X-w.W/2<v.X+v.W/2&&
                       v.Y-v.H/2<w.Y+w.H/2&&w.Y-w.H/2<v.Y+v.H/2)
                    {
                        overlaps=true;
                        Log("STEP10_OVERLAPPING_VIEWPORTS="+v.Handle+","+w.Handle);
                    }
                }
            }
            Log("STEP10_ACTUAL_VIEWPORT_OVERLAP_DETECTED="+overlaps);
            Log("STEP10_POSSIBLE_TITLEBLOCK_COLLISION="+possibleTitleBlock);
            Log("STEP10_PLACEMENT_PROBE="+
                (newFrames.Count>=options.Views.Count?"INSPECTED":"INCONCLUSIVE_VERIFY_VISUALLY"));
            Log("NATIVE_ORTHO_VIEW_COUNT="+completed.Count);
            if(completed.Count!=options.Views.Count||
               newFrames.Count!=options.Views.Count||overlaps||possibleTitleBlock)
                throw new InvalidOperationException(
                    "SPTC: Selected native view frames failed layout checks; titles not created.");
            // Generate paper-space MText independently of native view metadata,
            // OrthoCube geometry or AutoCAD model contents.
            var titles=NativeOrthoViewTitlePlan.BuildTitles(
                confirmedFrames.Select(f=>new NativeOrthoViewTitlePlan.Frame(
                    f.Name,f.X,f.Y,f.W,f.H)).ToArray(),
                paperWidth,paperHeight,allowSubset:options.Views.Count!=10);
            Log("STEP10_TITLE_LAYER="+NativeOrthoViewTitlePlan.TitleLayer);
            var titleHandles=CreateNativePaperTitles(
                targetDoc.Database,paperLayout,titles);
            Log("STEP10_VIEW_TITLE_COUNT="+titleHandles.Count);
            if(titleHandles.Count!=options.Views.Count)
                throw new InvalidOperationException(
                    "SPTC did not append every selected native paper-space title.");
            for(int k=0;k<titles.Count;k++)
            {
                Log("STEP10_TITLE_"+titles[k].Name+"="+titles[k].Text+
                    " HANDLE="+titleHandles[k]+
                    " PAPER_POSITION="+Point(titles[k].CenterX,titles[k].TopY,0));
            }
            Log("STEP10_TITLE_PLACEMENT_CHECK=PASS");
            // REV22.29: project EACH actual source structure member onto
            // the ACTUAL SAVED native viewport camera. The leader starts at
            // that member's projected 3D extents center (not assembly center).
            // Arrange MText outside the viewport in sorted non-overlapping rows.
            // No revision to native cube, camera, view registration, or titles.
            string annotationStatus;
            int annotated=0;
            // REV22.46: share the ACTUAL visible label/leader locations
            // with the dimension planner; do not reserve hidden UI labels.
            IReadOnlyList<SupportOrthoMemberCalloutPlan27.Callout>
                dimensionCollisionCallouts=
                    Array.Empty<SupportOrthoMemberCalloutPlan27.Callout>();
            if(sourceAnnotations.Length>0)
            {
                var memberCameras=ReadNativeMemberCameras(
                    targetDoc.Database,nativeMemberViewportIds);
                foreach(var camera in memberCameras)
                    Log("REV22_29_CAMERA_"+camera.Name+
                        " DIRECTION="+Point(camera.DirX,camera.DirY,camera.DirZ)+
                        " CENTER="+Point(camera.ViewCenterX,camera.ViewCenterY,0)+
                        " TWIST="+camera.TwistRadians+
                        " SCALE="+camera.Scale);
                var plan=SupportOrthoMemberCalloutPlan27.Build(
                    memberCameras,titles,sourceAnnotations,paperWidth,paperHeight,
                    allowSubset:options.Views.Count!=10);
                // REV22.29: preserve full source XREF key in the diagnostics;
                // ONLY change the actual visible two-line PIPE annotation.
                // Structural members and Support TOS retain their REV22.28 text.
                var pipeByHandle=pipeRead.Pipes.ToDictionary(p=>p.Handle,
                    StringComparer.OrdinalIgnoreCase);
                // Apply presentation-only annotation toggles after source-grounded
                // camera/callout planning. This never modifies native pipe
                // properties, support data or viewport geometry.
                var visibleCallouts=plan.Callouts.Select(note=>
                {
                    if(note.IsXrefMember)
                        return options.XrefMemberNames?note:null;
                    if(note.IsPipe)
                    {
                        if(!pipeByHandle.TryGetValue(note.Handle,out var p) ||
                           (!options.LineNumber&&!options.Bop))
                            return null;
                        // Preserve REV22.29 source sanitation; REV22.38 omits\n                        // only the display prefix on the pipe line tag.
                        string[] original=SupportOrthoPipeCallout29.Compose(
                            p.LineTag,p.BopLabel).Split('\n');
                        string pipeLabel=options.LineNumber&&options.Bop?
                            original[0]+"\n"+original[1]:
                            (options.LineNumber?original[0]:original[1]);
                        return note with {Text=pipeLabel,
                            HasTos=options.LineNumber&&options.Bop};
                    }
                    bool includeSecond=options.Tos&&note.HasTos;
                    if(!options.Description&&!includeSecond)return null;
                    string[] lines=note.Text.Replace("\r\n","\n").Split('\n');
                    string firstLine=options.Description?lines[0]:
                        (lines.Length>1?lines[1]:"TOS unavailable");
                    string label=options.Description&&includeSecond&&lines.Length>1?
                        firstLine+"\n"+lines[1]:firstLine;
                    return note with {Text=label,
                        HasTos=options.Description&&includeSecond&&lines.Length>1};
                }).Where(note=>note!=null)
                    .Cast<SupportOrthoMemberCalloutPlan27.Callout>().ToArray();
                dimensionCollisionCallouts=visibleCallouts;
                Log("REV22_38_PIPE_LABEL_FORMAT=BARE_LINE_TAG_AND_BOP");
                annotated=CreateNativePaperMemberLeaders27(
                    targetDoc.Database,paperLayout,visibleCallouts,
                    out var nativeTextWarnings);
                foreach(var warning in nativeTextWarnings)
                    Log("REV22_29_NATIVE_TEXT_SKIP="+warning);
                Log("REV22_29_NATIVE_TEXT_SKIPPED_COUNT="+
                    nativeTextWarnings.Count);
                Log("REV22_29_TEXT_MODE=SEPARATE_NON_WRAPPING_DBTEXT");
                Log("REV22_29_TEXT_HEIGHT_MAX="+
                    SupportOrthoMemberCalloutPlan27.TextHeightMm);
                Log("STEP10_INDIVIDUAL_MEMBER_LAYER="+
                    SupportOrthoMemberCalloutPlan27.Layer);
                Log("STEP10_INDIVIDUAL_SOURCE_MEMBER_COUNT="+
                    sourceAnnotations.Length);
                Log("STEP10_STRUCTURE_MEMBER_COUNT="+horizontalSteel.Members.Count);
                Log("STEP10_PIPING_SUPPORT_COUNT="+plantSupports.Members.Count);
                Log("STEP10_PIPE_ANNOTATION_SOURCE_COUNT="+pipeRead.Pipes.Count);
                Log("STEP10_INDIVIDUAL_MEMBER_ANNOTATION_COUNT="+annotated);
                var steelHandles=horizontalSteel.Members.Select(m=>m.Handle)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var supportHandles=plantSupports.Members.Select(m=>m.Handle)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                int steelTosLabels=options.Tos?
                    visibleCallouts.Count(x=>steelHandles.Contains(x.Handle)):0;
                int supportTosLabels=options.Tos?
                    visibleCallouts.Count(x=>supportHandles.Contains(x.Handle)):0;
                Log("STEP10_HORIZONTAL_TOS_LABEL_COUNT="+steelTosLabels);
                Log("STEP10_SUPPORT_TOS_LABEL_COUNT="+supportTosLabels);
                Log("STEP10_TOTAL_TOS_LABEL_COUNT="+
                    (steelTosLabels+supportTosLabels));
                Log("STEP10_TOTAL_TOS_APPROX_LABEL_COUNT="+
                    (options.Tos?plan.ApproximateTosLines:0));
                int pipeBopLabels=options.Bop?
                    visibleCallouts.Count(c=>c.IsPipe):0;
                Log("STEP10_PIPE_BOP_LABEL_COUNT="+pipeBopLabels);
                // REV22.29 uses separately measured single-line DBText
                // entities. No MText word wrap or artificial paragraph flow.
                var tosPreview=visibleCallouts.FirstOrDefault(x=>x.HasTos);
                if(tosPreview!=null)
                    Log("REV22_29_TWO_SEPARATE_DBTEXT_LINES="+
                        tosPreview.Text.Replace("\n"," | "));
                foreach(var label in visibleCallouts)
                    Log("STEP10_MEMBER_LABEL="+label.View+
                        " SOURCE="+label.Handle+
                        " SECOND_LINE_PRESENT="+label.HasTos+
                        " PIPE_BOP="+label.IsPipe+
                        " TEXT="+label.Text.Replace("\n"," | ")+
                        " ANCHOR="+Point(label.AnchorX,label.AnchorY,0)+
                        " LANE_X="+label.LaneX+
                        " LABEL="+Point(label.LabelX,label.LabelTop,0));
                foreach(var warn in plan.Skipped)
                    Log("STEP10_MEMBER_ANNOTATION_SKIP="+warn);
                foreach(var noLabels in plan.ViewWithoutLabels)
                    Log("STEP10_MEMBER_ANNOTATION_VIEW_EMPTY="+noLabels);
                int perViewExpected=sourceAnnotations.Count(m=>
                    m.XrefDrawingName==null &&
                    (m.PipeBopSource!=null
                        ? options.LineNumber||options.Bop
                        : options.Description||(options.Tos&&m.HasTos)));
                int target=options.Views.Count*perViewExpected;
                int steelTosTarget=options.Tos?
                    options.Views.Count*horizontalSteel.HorizontalCount:0;
                int supportTosTarget=options.Tos?
                    options.Views.Count*plantSupports.Members.Count:0;
                int pipeBopTarget=options.Bop?
                    options.Views.Count*pipeRead.Pipes.Count:0;
                // XREF file-name notes are best-effort; failure/overflow
                // must not turn a completely correct native annotation
                // result into PARTIAL.
                int xrefPlanned=visibleCallouts.Count(c=>c.IsXrefMember);
                int xrefWriteSkips=nativeTextWarnings.Count(w=>
                    w.Contains("/XREF_MEMBER:",StringComparison.Ordinal));
                int xrefWritten=Math.Max(0,xrefPlanned-xrefWriteSkips);
                int nativeWritten=annotated-xrefWritten;
                Log("REV22_37_XREF_FILENAME_LABELS_PLANNED="+xrefPlanned);
                Log("REV22_37_XREF_FILENAME_LABELS_WRITTEN="+xrefWritten);
                Log("REV22_37_XREF_FILENAME_LABELS_WRITE_SKIPPED="+xrefWriteSkips);
                Log("REV22_37_NATIVE_LABELS_WRITTEN="+nativeWritten);
                annotationStatus=nativeWritten==target&&
                    steelTosLabels==steelTosTarget&&
                    supportTosLabels==supportTosTarget&&
                    pipeBopLabels==pipeBopTarget?"PASS":"PARTIAL";
                Log("STEP10_MEMBER_ANNOTATION_EXPECTED="+target);
                Log("STEP10_HORIZONTAL_TOS_EXPECTED="+steelTosTarget);
                Log("STEP10_SUPPORT_TOS_EXPECTED="+supportTosTarget);
                Log("STEP10_PIPE_BOP_EXPECTED="+pipeBopTarget);
                Log("STEP10_MEMBER_ANNOTATIONS_CHECK="+annotationStatus);
            }
            else
            {
                // Selected ordinary BlockReferences with no independently
                // linked members cannot acquire fabricated member pointers.
                annotationStatus="NO_NATIVE_STEEL_OR_PLANT_SUPPORTS";
                Log("STEP10_MEMBER_ANNOTATION_EXPECTED=0");
                Log("STEP10_INDIVIDUAL_MEMBER_ANNOTATION_COUNT=0");
                Log("STEP10_MEMBER_ANNOTATIONS_CHECK="+annotationStatus);
            }
            // REV22.41 paper-space native DIMLINEAR annotations.
            // Orthogonal six-view subset only; ISO projections are never
            // given misleading rotated dimensions. A dimension is planned
            // only for a model member with actual source Start/End data.
            Log("REV22_41_AUTO_DIMLINEAR_ENABLED="+options.AutoDimLinear);
            Log("REV22_41_VERIFIED_SOURCE_SEGMENTS="+
                dimSource.Segments.Count);
            Log("REV22_43_EXPLICIT_MEMBER_LENGTH_SOURCES="+
                dimSource.Segments.Count(x=>
                    x.Provenance=="EXPLICIT_MEMBER_ENDPOINTS"));
            Log("REV22_43_OVERALL_VERTICAL_HEIGHT_SOURCES="+
                dimSource.Segments.Count(x=>
                    x.Provenance=="GEOMETRIC_OVERALL_Z_HEIGHT"));
            foreach(string diagnostic in dimSource.Diagnostics)
                Log("REV22_41_SOURCE_DIAGNOSTIC="+diagnostic);
            if(options.AutoDimLinear)
            {
                try
                {
                    var dimCameras=ReadNativeMemberCameras(
                        targetDoc.Database,nativeMemberViewportIds);
                    var dimensionPlan=SupportOrthoSmartDimPlan46.Build(
                        dimCameras,everyViewMembers.Members,
                        paperWidth,paperHeight,units,
                        dimensionCollisionCallouts,titles);
                    Log("REV22_46_DIMENSION_MODE=COLLISION_AWARE_PER_MEMBER_HV");
                    Log("REV22_46_AUTO_REPOSITIONED="+
                        dimensionPlan.Repositioned);
                    Log("REV22_46_COLLIDING_CANDIDATES_REJECTED="+
                        dimensionPlan.CollisionCandidatesRejected);
                    Log("REV22_46_DIMENSIONS_REQUESTED="+
                        dimensionPlan.RequestedAxisCount);
                    Log("REV22_46_DIMENSIONS_PLANNED="+
                        dimensionPlan.Dimensions.Count);
                    Log("REV22_46_DIMENSIONS_UNPLACED="+
                        dimensionPlan.Skipped.Count(x=>
                            x.Contains("_NO_COLLISION_FREE_DIMENSION_LANE")||
                            x.Contains("_SPAN_TOO_SMALL_ON_PAPER")||
                            x.Contains("_MEMBER_NOT_FULLY_IN_NATIVE_VIEW")));
                    foreach(var skip in dimensionPlan.Skipped)
                        Log("REV22_46_DIMENSION_SKIPPED="+skip);
                    Log("REV22_44_DIMENSION_MODE=PER_MEMBER_PER_ORTHOGRAPHIC_VIEW_H_AND_V");
                    Log("REV22_44_MEMBER_SOURCES="+
                        everyViewMembers.Members.Count);
                    foreach(string note in everyViewMembers.Diagnostics)
                        Log("REV22_44_SOURCE_DIAGNOSTIC="+note);
                    Log("REV22_44_ORTHOGRAPHIC_VIEW_COUNT="+
                        dimensionPlan.OrthographicViewCount);
                    Log("REV22_44_HV_AXES_REQUESTED="+
                        dimensionPlan.RequestedAxisCount);
                    Log("REV22_44_HV_AXES_PLANNED="+
                        dimensionPlan.Dimensions.Count);
                    Log("REV22_44_TOTAL_DIMENSIONS_SKIPPED="+
                        dimensionPlan.Skipped.Count);
                    Log("REV22_41_DIMENSION_LAYER="+
                        SupportOrthoDimLinearPlan41.Layer);
                    Log("REV22_41_ORTHOGRAPHIC_VIEWS="+
                        dimensionPlan.OrthographicViewCount);
                    Log("REV22_41_DIMENSIONS_PLANNED="+
                        dimensionPlan.Dimensions.Count);
                    // Historical REV22.42 compression is disabled. Unlike
                    // before, duplicate lengths in opposite views/members
                    // are intentionally shown independently for REV22.44.
                    Log("REV22_42_COMPACT_DIMENSION_MODE=DISABLED_BY_REV22_44");
                    Log("REV22_42_REPEAT_DIMENSIONS_SUPPRESSED=0");
                    Log("REV22_42_PREFERRED_VIEWS=ALL_SELECTED_ORTHOGRAPHIC");
                    foreach(var rejected in dimensionPlan.Skipped)
                    {
                        Log("REV22_44_DIMENSION_SKIPPED="+rejected);
                        Log("REV22_41_DIMENSION_SKIPPED="+rejected);
                    }
                    foreach(var view in dimCameras.Where(c=>
                        new[]{"TOP","BOTTOM","FRONT","BACK","LEFT","RIGHT"}
                            .Contains(c.Name,StringComparer.OrdinalIgnoreCase)))
                    {
                        int horizontal=dimensionPlan.Dimensions.Count(d=>
                            d.View==view.Name&&d.Horizontal);
                        int vertical=dimensionPlan.Dimensions.Count(d=>
                            d.View==view.Name&&!d.Horizontal);
                        Log("REV22_44_VIEW="+view.Name+
                            " RECOGNIZED_MEMBERS="+everyViewMembers.Members.Count+
                            " HORIZONTAL_DIMS="+horizontal+
                            " VERTICAL_DIMS="+vertical+
                            " REQUESTED_AXES="+
                                everyViewMembers.Members.Count*2);
                    }
                    int created=SupportOrthoDimLinearWriter41.Write(
                        targetDoc.Database,paperLayout,
                        dimensionPlan.Dimensions,out var dimensionWarnings);
                    Log("REV22_41_DIMENSIONS_CREATED="+created);
                    Log("REV22_44_DIMENSIONS_CREATED="+created);
                    Log("REV22_46_DIMENSIONS_CREATED="+created);
                    Log("REV22_46_DIMENSION_COVERAGE="+
                        (created==dimensionPlan.RequestedAxisCount?
                            "COMPLETE":"PARTIAL")+
                        " CREATED="+created+
                        " REQUESTED="+dimensionPlan.RequestedAxisCount);
                    Log("REV22_46_COLLISION_MODE=NO_FORCED_OVERLAP");
                    Log("REV22_43_OVERALL_HEIGHT_DIMENSIONS_PLANNED="+
                        dimensionPlan.Dimensions.Count(d=>
                            d.Provenance=="GEOMETRIC_OVERALL_Z_HEIGHT"));
                    foreach(var warning in dimensionWarnings)
                        Log("REV22_41_DIMENSION_WRITE_WARNING="+warning);
                    foreach(var dimension in dimensionPlan.Dimensions)
                        Log("REV22_41_DIM_SOURCE="+dimension.View+
                            " SOURCE="+dimension.Source+
                            " KIND="+dimension.Kind+
                            " ORIENTATION="+
                                (dimension.Horizontal?"HORIZONTAL":"VERTICAL")+
                            " TEXT="+dimension.Measurement+
                            " MM PROVENANCE="+dimension.Provenance+
                            " VERIFIED_ENDPOINTS="+
                                (dimension.Provenance==
                                    "EXPLICIT_MEMBER_ENDPOINTS"));
                    Log("REV22_41_DIMENSION_RESULT="+
                        (created==dimensionPlan.Dimensions.Count?
                            "PASS":"PARTIAL"));
                }
                catch(System.Exception ex)
                {
                    // Do not risk breaking verified native view registration
                    // because optional dimensions could not be generated.
                    Log("REV22_41_DIMENSION_RESULT=FAILED:"+ex.GetType().Name+
                        ":"+ex.Message);
                }
            }
            Log("REV22_41_DIMENSIONS_STATIC_ON_MANUAL_UPDATE=TRUE");
            Log("UPDATE_VIEW_MANUAL_TEST_REQUIRED=TRUE");
            Log("FINAL_STATUS=TEN_NATIVE_VIEWS_AND_TITLES_MEMBER_LEADERS_"+
                annotationStatus);
            succeeded=annotationStatus=="PASS" ||
                annotationStatus=="NO_NATIVE_STEEL_OR_PLANT_SUPPORTS";
            SafeWriteToActiveEditor(
                "SPTC: "+options.Views.Count+" native views and titles created. "+
                annotated+" per-member leader annotations; "+
                annotationStatus+". Save and verify Update View.");


                }
                catch (TargetInvocationException ex)
                {
                    Log("NATIVE_DOCUMENT_CONTEXT_EXCEPTION=" +
                        (ex.InnerException?.ToString() ?? ex.ToString()));
                    SafeWriteToActiveEditor("REV22 native document-context API error: " +
                        (ex.InnerException?.Message ?? ex.Message));
                }
                catch (System.Exception ex)
                {
                    Log("NATIVE_DOCUMENT_CONTEXT_ERROR=" + ex);
                    SafeWriteToActiveEditor("REV22 native document-context error: " +
                        ex.Message);
                }
                finally
                {
                    if (workingDbWasChanged)
                    {
                        // Re-synchronize to whichever drawing is now active.
                        // Do not restore the old source working DB while the
                        // newly registered Ortho DWG remains the active tab.
                        try
                        {
                            Document? active = AcadApp.DocumentManager.MdiActiveDocument;
                            if (active != null)
                                HostApplicationServices.WorkingDatabase = active.Database;
                            else if (originalWorkingDatabase != null)
                                HostApplicationServices.WorkingDatabase = originalWorkingDatabase;
                            Log("STEP5_WORKING_DB_FINAL_SYNC=" +
                                (HostApplicationServices.WorkingDatabase?.Filename ?? "UNKNOWN"));
                        }
                        catch (System.Exception ex)
                        {
                            Log("STEP5_WORKING_DB_FINAL_SYNC_ERROR=" + ex.Message);
                        }
                    }
                }
                return Task.CompletedTask;
            }, null);
            Log("STEP5_TARGET_COMMAND_CONTEXT_COMPLETED");
        }
        catch (TargetInvocationException ex)
        {
            Log("NATIVE_EXCEPTION=" + (ex.InnerException?.ToString() ?? ex.ToString()));
            SafeWriteToActiveEditor("REV22 native API error: " + (ex.InnerException?.Message ?? ex.Message));
        }
        catch (System.Exception ex)
        {
            Log("ERROR=" + ex);
            SafeWriteToActiveEditor("REV22 stopped: " + ex.Message);
        }
        finally
        {
            // Only a clone not offered to the native registration API is safe to
            // delete. Never silently delete an API-registered Ortho drawing.
            if (cloned && !registrationAttempted && File.Exists(dest))
            {
                try { File.Delete(dest); Log("CLEANUP=UNREGISTERED_CLONE_REMOVED"); }
                catch (System.Exception ex) { Log("CLEANUP_FAILED=" + ex.Message); }
            }
            // This is a Session command: the original source Editor may no longer
            // have an active AutoCAD command context after switching to Ortho.
            // Never let eNotApplicable from WriteMessage mask the real exception.
            SafeWriteToActiveEditor("REV22 RESULT: " + report);
        }
        return succeeded;
    }

    // REV22.29 read-only API reflection; verified from native DLL metadata:
    // GetDwgSheetFromOrthoView(PlantOrthoView,out IDwgSheetDef) and
    // GetViewDefByViewName(string,IDwgSheetDef,out IDwgViewDef).
    private static string ReadNativeStoredViewType(object orthoView,string viewName)
    {
        try
        {
            Type? nativeUi=FindType(AppDomain.CurrentDomain.GetAssemblies(),
                "Autodesk.ProcessPower.PnP3dOrthoDrawingsUI.UIUtils");
            if(nativeUi==null)return "UNAVAILABLE:UIUtils missing";
            const BindingFlags flags=BindingFlags.Static|
                BindingFlags.NonPublic|BindingFlags.Public;
            MethodInfo? getSheet=nativeUi.GetMethods(flags).FirstOrDefault(m=>
            {
                if(m.Name!="GetDwgSheetFromOrthoView"||
                    m.ReturnType!=typeof(bool))return false;
                ParameterInfo[] args=m.GetParameters();
                return args.Length==2&&
                    args[0].ParameterType.IsInstanceOfType(orthoView)&&
                    args[1].ParameterType.IsByRef;
            });
            if(getSheet==null)return "UNAVAILABLE:Native sheet method missing";
            object?[] sheetArgs={orthoView,null};
            if(getSheet.Invoke(null,sheetArgs) is not bool hasSheet||!hasSheet||
                sheetArgs[1]==null)
                return "UNAVAILABLE:Native sheet definition unavailable";
            object sheet=sheetArgs[1]!;
            MethodInfo? getDef=nativeUi.GetMethods(flags).FirstOrDefault(m=>
            {
                if(m.Name!="GetViewDefByViewName"||
                    m.ReturnType!=typeof(bool))return false;
                ParameterInfo[] args=m.GetParameters();
                return args.Length==3&&args[0].ParameterType==typeof(string)&&
                    args[1].ParameterType.IsInstanceOfType(sheet)&&
                    args[2].ParameterType.IsByRef;
            });
            if(getDef==null)return "UNAVAILABLE:Native view method missing";
            object?[] viewArgs={viewName,sheet,null};
            if(getDef.Invoke(null,viewArgs) is not bool hasView||!hasView||
                viewArgs[2]==null)
                return "UNAVAILABLE:Saved native view definition not found";
            object nativeDef=viewArgs[2]!;
            PropertyInfo? viewType=nativeDef.GetType().GetProperty(
                "ViewType",BindingFlags.Public|BindingFlags.NonPublic|
                BindingFlags.Instance);
            if(viewType==null)
                viewType=nativeDef.GetType().GetInterfaces()
                    .Select(t=>t.GetProperty("ViewType"))
                    .FirstOrDefault(p=>p!=null);
            return viewType?.GetValue(nativeDef)?.ToString() ??
                "UNAVAILABLE:Native ViewType inaccessible";
        }
        catch(System.Exception ex)
        {
            return "UNAVAILABLE:"+
                ((ex is TargetInvocationException wrapped?
                    wrapped.InnerException:ex)?.Message??ex.Message);
        }
    }

    // REV22.29: read ONLY the real viewport after Autodesk's internal
    // generation. A plan-time frame is not a reliable paper-space result.
    private static NativeOrthoViewportPaperGuard.Frame ReadPaperViewportFrame(
        Database db,ObjectId viewportId,string name)
    {
        if(viewportId.IsNull||!viewportId.IsValid)
            throw new InvalidOperationException("REV22.29 invalid generated viewport ID.");
        using Transaction tr=db.TransactionManager.StartTransaction();
        if(tr.GetObject(viewportId,OpenMode.ForRead,false) is not Viewport vp)
            throw new InvalidOperationException("REV22.29 viewport missing after generation.");
        var frame=new NativeOrthoViewportPaperGuard.Frame(name,
            vp.CenterPoint.X,vp.CenterPoint.Y,vp.Width,vp.Height,
            vp.CustomScale,vp.ViewHeight);
        tr.Commit();
        return frame;
    }

    // REV22.29: this is the only new drawing mutation. Its scope is the
    // freshly created ISO viewport in an isolated copied test Ortho DWG.
    // The Autodesk native ViewDef is not edited directly.
    // REV22.29: change only newly created isometric paper-space viewports.
    // Do not edit Plant native IDwgViewDef or Ortho.dcf.
    private static NativeOrthoIsoFramingGuard.ViewState ReadIsoViewportState(
        Database db,ObjectId viewportId)
    {
        if(viewportId.IsNull||!viewportId.IsValid)
            throw new InvalidOperationException("REV22.29 generated ISO viewport id invalid.");
        using Transaction tr=db.TransactionManager.StartTransaction();
        if(tr.GetObject(viewportId,OpenMode.ForRead,false) is not Viewport vp)
            throw new InvalidOperationException("REV22.29 generated ISO viewport missing.");
        var center=vp.ViewCenter;
        var result=new NativeOrthoIsoFramingGuard.ViewState(
            center.X,center.Y,vp.TwistAngle);
        tr.Commit();
        return result;
    }

    private static void SetIsoViewportPose(Database db,ObjectId viewportId,
        NativeOrthoIsoFramingGuard.Adjustment adjusted)
    {
        if(viewportId.IsNull||!viewportId.IsValid)
            throw new InvalidOperationException("REV22.29 generated ISO viewport id invalid.");
        using Transaction tr=db.TransactionManager.StartTransaction();
        if(tr.GetObject(viewportId,OpenMode.ForWrite,false) is not Viewport vp)
            throw new InvalidOperationException("REV22.29 generated ISO viewport missing.");
        vp.TwistAngle=adjusted.TwistRadians;
        vp.ViewCenter=new Point2d(adjusted.CenterX,adjusted.CenterY);
        tr.Commit();
    }

    private static Vector3d ReadViewportCamera(Database db,ObjectId viewportId)
    {
        if(viewportId.IsNull||!viewportId.IsValid)
            throw new InvalidOperationException("REV22.29 invalid native viewport id.");
        using Transaction tr=db.TransactionManager.StartTransaction();
        if(tr.GetObject(viewportId,OpenMode.ForRead,false) is not Viewport vp)
            throw new InvalidOperationException("REV22.29 native viewport no longer exists.");
        Vector3d dir=vp.ViewDirection;
        tr.Commit();
        return dir;
    }

    private static ObjectId CreateExperimentalViewport(
        Database db,string layoutName,Point3d paperCenter,
        double frameWidth,double frameHeight,Vector3d viewDirection,
        Point3d viewTarget,double scale,double modelHeight)
    {
        if(frameWidth<=0||frameHeight<=0||scale<=0||modelHeight<=0)
            throw new ArgumentOutOfRangeException(nameof(frameWidth));
        using Transaction tr=db.TransactionManager.StartTransaction();
        var layouts=(DBDictionary)tr.GetObject(db.LayoutDictionaryId,OpenMode.ForRead);
        if(!layouts.Contains(layoutName))
            throw new InvalidOperationException("Native paper-space layout disappeared.");
        var lay=(Layout)tr.GetObject(layouts.GetAt(layoutName),OpenMode.ForRead);
        var paper=(BlockTableRecord)tr.GetObject(lay.BlockTableRecordId,OpenMode.ForWrite);
        using var viewport=new Viewport
        {
            CenterPoint=paperCenter,
            Width=frameWidth,Height=frameHeight,
            ViewDirection=viewDirection,
            ViewTarget=viewTarget,
            ViewCenter=Point2d.Origin,
            ViewHeight=modelHeight,
            CustomScale=scale
        };
        ObjectId id=paper.AppendEntity(viewport);
        tr.AddNewlyCreatedDBObject(viewport,true);
        viewport.On=true;
        tr.Commit();
        if(id.IsNull||!id.IsValid)
            throw new InvalidOperationException("New native paper viewport has invalid ObjectId.");
        return id;
    }

    // REV22.29: the saved native viewport is the sole authority for
    // projection angle, twist, scale and display-center. We read these AFTER
    // Autodesk native generation and the verified orientation corrections.
    private static IReadOnlyList<SupportOrthoMemberCalloutPlan17.Camera>
        ReadNativeMemberCameras(Database db,
            IReadOnlyDictionary<string,ObjectId> ids)
    {
        using Transaction tr=db.TransactionManager.StartTransaction();
        var views=new List<SupportOrthoMemberCalloutPlan17.Camera>(ids.Count);
        foreach(var view in ids)
        {
            if(view.Value.IsNull||!view.Value.IsValid ||
               tr.GetObject(view.Value,OpenMode.ForRead,false) is not Viewport vp)
                throw new InvalidOperationException(
                    "REV22.29 actual native viewport unavailable: "+view.Key);
            views.Add(new SupportOrthoMemberCalloutPlan17.Camera(
                view.Key,vp.CenterPoint.X,vp.CenterPoint.Y,
                vp.Width,vp.Height,
                vp.ViewDirection.X,vp.ViewDirection.Y,vp.ViewDirection.Z,
                vp.ViewCenter.X,vp.ViewCenter.Y,
                vp.TwistAngle,vp.CustomScale));
        }
        tr.Commit();
        if(views.Count!=ids.Count||views.Count==0||views.Count>10)
            throw new InvalidOperationException(
                "SPTC labels require all selected native view cameras.");
        return views;
    }

    // REV22.29: SINGLE-LINE DBText is not capable of word wrapping.
    // One name entity; a second TOS entity for HORIZONTAL members only.
    // Actual AutoCAD font extents are measured at write-time. Shrink the
    // text height to fit the allocated rectangle, or skip it entirely.
    private static int CreateNativePaperMemberLeaders27(
        Database db,string paperLayout,
        IReadOnlyList<SupportOrthoMemberCalloutPlan27.Callout> callouts,
        out List<string> skipped)
    {
        skipped=new List<string>();
        using Transaction tr=db.TransactionManager.StartTransaction();
        var layouts=(DBDictionary)tr.GetObject(db.LayoutDictionaryId,OpenMode.ForRead);
        if(!layouts.Contains(paperLayout))
            throw new InvalidOperationException(
                "REV22.29 native Ortho paper layout missing.");
        var layout=(Layout)tr.GetObject(layouts.GetAt(paperLayout),OpenMode.ForRead);
        var paper=(BlockTableRecord)tr.GetObject(
            layout.BlockTableRecordId,OpenMode.ForWrite);
        var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
        ObjectId layerId;
        if(layers.Has(SupportOrthoMemberCalloutPlan27.Layer))
            layerId=layers[SupportOrthoMemberCalloutPlan27.Layer];
        else
        {
            layers.UpgradeOpen();
            using var layer=new LayerTableRecord
            {Name=SupportOrthoMemberCalloutPlan27.Layer};
            layerId=layers.Add(layer);
            tr.AddNewlyCreatedDBObject(layer,true);
        }
        int count=0;
        foreach(var item in callouts)
        {
            string[] lines=item.Text.Replace("\r\n","\n").Split('\n');
            if(lines.Length!=(item.HasTos?2:1)||
               lines.Any(t=>string.IsNullOrWhiteSpace(t)))
            {
                skipped.Add(item.View+"/"+item.Handle+
                    ":UNEXPECTED_PARAGRAPH_COUNT");
                continue;
            }
            using var name=new DBText
            {
                TextString=SupportOrthoMemberMetadata.Clean(lines[0]),
                Height=SupportOrthoMemberCalloutPlan27.TextHeightMm,
                Position=new Point3d(item.LabelX,item.LabelTop-2.1,0),
                LayerId=layerId,
                TextStyleId=db.Textstyle,
                WidthFactor=1.0
            };
            paper.AppendEntity(name);
            tr.AddNewlyCreatedDBObject(name,true);
            DBText? tos=null;
            try
            {
                if(lines.Length==2)
                {
                    tos=new DBText
                    {
                        TextString=SupportOrthoMemberMetadata.Clean(lines[1]),
                        Height=SupportOrthoMemberCalloutPlan27.TextHeightMm,
                        Position=new Point3d(item.LabelX,
                            item.LabelTop-
                            SupportOrthoMemberCalloutPlan27.TextLineHeightMm-2.1,0),
                        LayerId=layerId,
                        TextStyleId=db.Textstyle,
                        WidthFactor=1.0
                    };
                    paper.AppendEntity(tos);
                    tr.AddNewlyCreatedDBObject(tos,true);
                }

                const double minimumReadableTextMm=1.05;
                double fontHeight=SupportOrthoMemberCalloutPlan27.TextHeightMm;
                bool fit=false;
                string problem="NATIVE_TEXT_TOO_WIDE";
                for(int pass=0;pass<10;pass++)
                {
                    name.Height=fontHeight;
                    name.Position=new Point3d(item.LabelX,
                        item.LabelTop-fontHeight*1.18,0);
                    if(tos!=null)
                    {
                        tos.Height=fontHeight;
                        tos.Position=new Point3d(item.LabelX,
                            item.LabelTop-
                                SupportOrthoMemberCalloutPlan27.TextLineHeightMm-
                                fontHeight*1.18,0);
                    }
                    try
                    {
                        Extents3d sizeBounds=name.GeometricExtents;
                        Extents3d? tosBounds=tos?.GeometricExtents;
                        double width1=sizeBounds.MaxPoint.X-sizeBounds.MinPoint.X;
                        double width2=tosBounds.HasValue?
                            tosBounds.Value.MaxPoint.X-tosBounds.Value.MinPoint.X:0;
                        double measuredWidth=Math.Max(width1,width2);
                        double safeWidth=item.LabelWidth-1.5;
                        if(!double.IsFinite(measuredWidth)||measuredWidth<=0)
                        {
                            problem="NATIVE_FONT_EXTENTS_UNAVAILABLE";
                            break;
                        }
                        double minimumY=tosBounds.HasValue?
                            tosBounds.Value.MinPoint.Y:sizeBounds.MinPoint.Y;
                        bool heightFits=
                            sizeBounds.MaxPoint.Y<=item.LabelTop+.12 &&
                            minimumY>=item.LabelTop-item.LabelHeight+.12 &&
                            (!tosBounds.HasValue||
                                sizeBounds.MinPoint.Y>=
                                    tosBounds.Value.MaxPoint.Y+.25);
                        if(measuredWidth<=safeWidth && heightFits &&
                           sizeBounds.MinPoint.X>=item.LabelX-.12 &&
                           (!tosBounds.HasValue||
                                tosBounds.Value.MinPoint.X>=item.LabelX-.12))
                        {
                            fit=true;
                            break;
                        }
                        double next=measuredWidth>safeWidth?
                            fontHeight*(safeWidth/measuredWidth)*.95:
                            fontHeight*.91;
                        if(next<minimumReadableTextMm-1e-5)
                        {
                            problem=measuredWidth>safeWidth?
                                "NATIVE_FONT_TOO_WIDE_AT_MINIMUM_HEIGHT":
                                "NATIVE_FONT_TOO_TALL_FOR_RESERVED_ROW";
                            break;
                        }
                        fontHeight=Math.Min(fontHeight-.055,next);
                    }
                    catch(System.Exception ex)
                    {
                        problem="NATIVE_FONT_MEASUREMENT_ERROR_"+
                            ex.GetType().Name;
                        break;
                    }
                }
                if(!fit)
                {
                    tos?.Erase();
                    name.Erase();
                    skipped.Add(item.View+"/"+item.Handle+":"+problem);
                    continue;
                }

                double landingY=item.LabelTop-
                    SupportOrthoMemberCalloutPlan27.TextLineHeightMm*.5;
                var routes=new (Point3d Start,Point3d End)[]
                {
                    (new Point3d(item.AnchorX,item.AnchorY,0),
                        new Point3d(item.LaneX,item.AnchorY,0)),
                    (new Point3d(item.LaneX,item.AnchorY,0),
                        new Point3d(item.LaneX,landingY,0)),
                    (new Point3d(item.LaneX,landingY,0),
                        new Point3d(item.LabelX-1.0,landingY,0))
                };
                foreach(var route in routes)
                {
                    if(route.Start.DistanceTo(route.End)<1e-5)continue;
                    using var leader=new Line(route.Start,route.End)
                    {LayerId=layerId};
                    paper.AppendEntity(leader);
                    tr.AddNewlyCreatedDBObject(leader,true);
                }
                count++;
            }
            finally
            {
                tos?.Dispose();
            }
        }
        tr.Commit();
        return count;
    }

    // REV22.29: one concise source-grounded panel in PAPER SPACE only.
    // It does not claim per-member associative leader identities.
    private static string CreateNativePaperSupportSummary(
        Database db,string paperLayout,
        SupportOrthoSummaryPanel15.Panel panel)
    {
        using Transaction tr=db.TransactionManager.StartTransaction();
        var layouts=(DBDictionary)tr.GetObject(db.LayoutDictionaryId,OpenMode.ForRead);
        if(!layouts.Contains(paperLayout))
            throw new InvalidOperationException("REV22.29 native paper layout unavailable.");
        var layout=(Layout)tr.GetObject(layouts.GetAt(paperLayout),OpenMode.ForRead);
        var paper=(BlockTableRecord)tr.GetObject(
            layout.BlockTableRecordId,OpenMode.ForWrite);
        var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
        ObjectId layerId;
        if(layers.Has(SupportOrthoSummaryPanel15.Layer))
            layerId=layers[SupportOrthoSummaryPanel15.Layer];
        else
        {
            layers.UpgradeOpen();
            using var layer=new LayerTableRecord
            {
                Name=SupportOrthoSummaryPanel15.Layer
            };
            layerId=layers.Add(layer);
            tr.AddNewlyCreatedDBObject(layer,true);
        }
        double x0=panel.X,yTop=panel.Top;
        double x1=panel.X+panel.Width,yBottom=panel.Top-panel.Height;
        // Thin, editable paper-space border and header separator.
        var segments=new (double X0,double Y0,double X1,double Y1)[]
        {
            (x0,yTop,x1,yTop),(x1,yTop,x1,yBottom),
            (x1,yBottom,x0,yBottom),(x0,yBottom,x0,yTop),
            (x0,yTop-10,x1,yTop-10)
        };
        foreach(var seg in segments)
        {
            using var line=new Line(
                new Point3d(seg.X0,seg.Y0,0),
                new Point3d(seg.X1,seg.Y1,0))
            {
                LayerId=layerId
            };
            paper.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line,true);
        }
        using var header=new MText
        {
            Contents=SupportOrthoSummaryPanel15.Header,
            Location=new Point3d(x0+4,yTop-3,0),
            Width=panel.Width-8,
            TextHeight=SupportOrthoSummaryPanel15.HeaderTextHeightMm,
            Attachment=AttachmentPoint.TopLeft,
            LayerId=layerId,
            TextStyleId=db.Textstyle
        };
        paper.AppendEntity(header);
        tr.AddNewlyCreatedDBObject(header,true);
        using var text=new MText
        {
            Contents=SupportOrthoMemberMetadata.SafeMText(panel.IdentityLine)+
                "\\P"+SupportOrthoMemberMetadata.SafeMText(panel.SizeLine),
            Location=new Point3d(x0+4,yTop-13,0),
            Width=panel.Width-8,
            TextHeight=SupportOrthoSummaryPanel15.BodyTextHeightMm,
            Attachment=AttachmentPoint.TopLeft,
            LayerId=layerId,
            TextStyleId=db.Textstyle
        };
        ObjectId id=paper.AppendEntity(text);
        tr.AddNewlyCreatedDBObject(text,true);
        if(id.IsNull||!id.IsValid)
            throw new InvalidOperationException(
                "REV22.29 native paper summary MText could not be created.");
        tr.Commit();
        return text.Handle.ToString();
    }

    // REV22.29: ten readable titles are independent paper-space MText,
    // not part of the model source, native OrthoCube or viewport regeneration.
    private static IReadOnlyList<string> CreateNativePaperTitles(
        Database db,string paperLayout,
        IReadOnlyList<NativeOrthoViewTitlePlan.Title> titles)
    {
        if(titles==null||titles.Count<1||titles.Count>10)
            throw new ArgumentException("SPTC selected native view title count invalid.");
        using Transaction tr=db.TransactionManager.StartTransaction();
        var layouts=(DBDictionary)tr.GetObject(db.LayoutDictionaryId,OpenMode.ForRead);
        if(!layouts.Contains(paperLayout))
            throw new InvalidOperationException(
                "REV22.29 target native Ortho paper layout disappeared.");
        var layout=(Layout)tr.GetObject(
            layouts.GetAt(paperLayout),OpenMode.ForRead);
        var paper=(BlockTableRecord)tr.GetObject(
            layout.BlockTableRecordId,OpenMode.ForWrite);
        var layers=(LayerTable)tr.GetObject(db.LayerTableId,OpenMode.ForRead);
        ObjectId titleLayerId;
        if(layers.Has(NativeOrthoViewTitlePlan.TitleLayer))
            titleLayerId=layers[NativeOrthoViewTitlePlan.TitleLayer];
        else
        {
            layers.UpgradeOpen();
            using var layer=new LayerTableRecord
            {
                Name=NativeOrthoViewTitlePlan.TitleLayer
            };
            titleLayerId=layers.Add(layer);
            tr.AddNewlyCreatedDBObject(layer,true);
        }
        var handles=new List<string>(titles.Count);
        foreach(var title in titles)
        {
            using var label=new MText
            {
                Contents=title.Text,
                Location=new Point3d(title.CenterX,title.TopY,0),
                Width=title.Width,
                TextHeight=NativeOrthoViewTitlePlan.TextHeightMm,
                Attachment=AttachmentPoint.TopCenter,
                LayerId=titleLayerId,
                TextStyleId=db.Textstyle
            };
            ObjectId textId=paper.AppendEntity(label);
            tr.AddNewlyCreatedDBObject(label,true);
            if(textId.IsNull||!textId.IsValid)
                throw new InvalidOperationException(
                    "REV22.29 failed to append paper title "+title.Name);
            handles.Add(label.Handle.ToString());
        }
        tr.Commit();
        return handles;
    }

    private sealed record PaperViewport(string Handle,double X,double Y,double W,double H);
    private static List<PaperViewport> PaperViewports(Database db,string layoutName)
    {
        var items=new List<PaperViewport>();
        try
        {
            using Transaction tr=db.TransactionManager.StartTransaction();
            var layouts=(DBDictionary)tr.GetObject(db.LayoutDictionaryId,OpenMode.ForRead);
            if(!layouts.Contains(layoutName))return items;
            var lay=(Layout)tr.GetObject(layouts.GetAt(layoutName),OpenMode.ForRead);
            var ps=(BlockTableRecord)tr.GetObject(lay.BlockTableRecordId,OpenMode.ForRead);
            foreach(ObjectId id in ps)
                if(tr.GetObject(id,OpenMode.ForRead) is Viewport vp &&
                    vp.Width>0&&vp.Height>0)
                    items.Add(new PaperViewport(vp.Handle.ToString(),
                        vp.CenterPoint.X,vp.CenterPoint.Y,vp.Width,vp.Height));
        }
        catch(System.Exception)
        {
            // Optional placement probe; native view catalog is authoritative.
        }
        return items;
    }
    private static bool CatalogMatches(string status,IReadOnlyCollection<string> expected)
    {
        const string prefix="EXISTING_NATIVE_VIEWS:";
        if(!status.StartsWith(prefix,StringComparison.Ordinal))return false;
        string[] names=status[prefix.Length..].Split(',',
            StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        if(names.Length!=expected.Count)return false;
        var set=new HashSet<string>(names,StringComparer.OrdinalIgnoreCase);
        return expected.All(set.Contains);
    }

    private static void SafeWriteToActiveEditor(string message)
    {
        try
        {
            Document? active = AcadApp.DocumentManager.MdiActiveDocument;
            active?.Editor.WriteMessage("\n" + message);
        }
        catch (System.Exception)
        {
            // An AutoCAD session command may not have a document prompt context.
            // Diagnostic evidence is persisted by Log() independently.
            System.Diagnostics.Trace.WriteLine(message);
        }
    }

    private sealed record ProjectPart(string ProjectRoot, string OrthoFolder, object OrthoPart);

    private static ProjectPart CurrentOrthoProject()
    {
        var asm = AppDomain.CurrentDomain.GetAssemblies();
        Type app = FindType(asm,"Autodesk.ProcessPower.PlantInstance.PlantApplication") ??
            throw new InvalidOperationException("PlantApplication not loaded.");
        object plantProject = app.GetProperty("CurrentProject",
            BindingFlags.Public | BindingFlags.Static)?.GetValue(null) ??
            throw new InvalidOperationException("No active Plant 3D project.");
        string root = plantProject.GetType().GetProperty("ProjectFolderPath")?
            .GetValue(plantProject)?.ToString() ?? "";
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("Active project root was not found.");
        object parts = plantProject.GetType().GetProperty("ProjectParts")?
            .GetValue(plantProject) ?? throw new InvalidOperationException("ProjectParts missing.");
        object ortho = parts.GetType().GetProperty("Item")?
            .GetValue(parts, new object[] {"Ortho"}) ??
            throw new InvalidOperationException("Native Ortho project part missing.");
        string path = ortho.GetType().GetMethod("GetOrthoPath",
            BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null)?
            .Invoke(ortho,null)?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("Native Ortho directory unavailable.");
        if (!Path.IsPathRooted(path)) path = Path.Combine(root,path);
        path = Path.GetFullPath(path);
        if (!Directory.Exists(path) ||
            !NativeOrthoRegistrationNames.IsInsideProjectDirectory(path,root))
            throw new InvalidOperationException("Native Ortho directory outside active project.");
        return new ProjectPart(root,path,ortho);
    }

    private static bool IsNativeOrthoValid(object ortho, string filename)
    {
        Type util = FindType(AppDomain.CurrentDomain.GetAssemblies(),
            "Autodesk.ProcessPower.P3dProjectParts.OrthoDocUtils") ??
            throw new InvalidOperationException("OrthoDocUtils missing.");
        MethodInfo? validate = util.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m =>
            {
                if (m.Name!="IsValidOrthoDrawing" || m.ReturnType!=typeof(bool)) return false;
                ParameterInfo[] ps=m.GetParameters();
                return ps.Length==3 && ps[0].ParameterType==typeof(string) &&
                    ps[1].ParameterType.IsInstanceOfType(ortho) &&
                    ps[2].ParameterType==typeof(string).MakeByRefType();
            });
        if (validate==null) return false;
        object?[] args = {filename,ortho,""};
        return validate.Invoke(null,args) is bool yes && yes;
    }

    private static string ViewCatalog(object ortho,string filename)
    {
        try
        {
            const BindingFlags pub=BindingFlags.Public | BindingFlags.Instance;
            MethodInfo? list=ortho.GetType().GetMethod("GetPnPDrawingFiles",pub,
                null,Type.EmptyTypes,null);
            MethodInfo? names=ortho.GetType().GetMethod("GetAllViewNamesInOrthoDwgByGuid",
                pub,null,new[]
                {
                    typeof(string),typeof(Dictionary<string,string>).MakeByRefType()
                },null);
            if (list?.Invoke(ortho,null) is not IEnumerable files || names==null)
                return "NATIVE_ORTHO_VIEW_CATALOG_UNAVAILABLE";
            foreach (object? entry in files)
            {
                if (entry==null) continue;
                string? n=ReadProperty(entry,"FileName") ??
                    ReadProperty(entry,"RelativeFilePath") ?? ReadProperty(entry,"Name");
                if (!string.Equals(Path.GetFileName(n),Path.GetFileName(filename),
                    StringComparison.OrdinalIgnoreCase)) continue;
                foreach (string key in new[] {"Guid","PnPDrawingGuid","DrawingGuid"})
                {
                    string? guid=ReadProperty(entry,key);
                    if (!Guid.TryParse(guid,out _)) continue;
                    object?[] args={guid,new Dictionary<string,string>()};
                    names.Invoke(ortho,args);
                    if (args[1] is Dictionary<string,string> views)
                        return views.Count==0?"EMPTY_REGISTERED_ORTHO":
                            "EXISTING_NATIVE_VIEWS:" + string.Join(",",views.Keys);
                }
                return "REGISTERED_ORTHO_WITHOUT_GUID";
            }
            return "ORTHO_NOT_IN_PROJECT_DRAWING_LIST";
        }
        catch (System.Exception ex)
        {
            return "ORTHO_VIEW_QUERY_ERROR:" + (ex.InnerException?.Message ?? ex.Message);
        }
    }

    private static string FindPaperLayout(Database db)
    {
        using Transaction tr=db.TransactionManager.StartTransaction();
        var dict=(DBDictionary)tr.GetObject(db.LayoutDictionaryId,OpenMode.ForRead);
        string best="";double area=0;
        foreach (DBDictionaryEntry e in dict)
        {
            if (tr.GetObject(e.Value,OpenMode.ForRead) is not Layout layout ||
                string.Equals(layout.LayoutName,"Model",StringComparison.OrdinalIgnoreCase))
                continue;
            Point2d size=layout.PlotPaperSize;
            double scale=layout.PlotPaperUnits.ToString()=="Inches"?25.4:1;
            double a=size.X*size.Y*scale*scale;
            if (double.IsFinite(a) && a>area)
            {
                best=layout.LayoutName; area=a;
            }
        }
        return best;
    }

    private static (double width,double height) CurrentPaper(Database db,string layoutName)
    {
        using Transaction tr=db.TransactionManager.StartTransaction();
        var dict=(DBDictionary)tr.GetObject(db.LayoutDictionaryId,OpenMode.ForRead);
        foreach (DBDictionaryEntry entry in dict)
        {
            if (tr.GetObject(entry.Value,OpenMode.ForRead) is not Layout paper ||
                paper.LayoutName!=layoutName) continue;
            double factor=paper.PlotPaperUnits.ToString()=="Inches"?25.4:1;
            Point2d size=paper.PlotPaperSize;
            if (!(size.X>0 && size.Y>0)) break;
            return (size.X*factor,size.Y*factor);
        }
        throw new InvalidOperationException("Valid Ortho paper-space layout not found.");
    }

    private static Type? FindType(IEnumerable<Assembly> assemblies,string name)
    {
        foreach (Assembly asm in assemblies)
        {
            try { Type? t=asm.GetType(name,false);if(t!=null)return t; }
            catch { }
        }
        return null;
    }

    private static string? ReadProperty(object value,string property)
    {
        try { return value.GetType().GetProperty(property)?.GetValue(value)?.ToString(); }
        catch { return null; }
    }

    private static bool Near(double a,double b) =>
        Math.Abs(a-b)<=Math.Max(1e-4,Math.Abs(b)*1e-7);

    private static string Point(double x,double y,double z) =>
        string.Format(CultureInfo.InvariantCulture,
            "({0:0.###},{1:0.###},{2:0.###})",x,y,z);
}
