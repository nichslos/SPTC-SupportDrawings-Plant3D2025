using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.PlottingServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SPTC.SupportDrawings;

/// <summary>
/// Opt-in native PlotEngine PDF publication of exactly one already-generated
/// registered Ortho paper layout. Runs only AFTER the native generator and
/// dimension integrity checks return to the session command context.
/// Requires an exact active target document and a successful native SaveAs.
/// Never changes the source model, viewport, title, DIMLINEAR or page setup.
/// </summary>
internal static class SupportProductionNativePdfPublisher
{
    internal static SupportProductionPackagePlan.Entry Publish(
        Document sourceDoc,SupportProductionPackagePlan.Settings config,
        SupportProductionPackagePlan.Entry entry,
        Action<string> log)
    {
        if(!config.Enabled||!config.PublishPdf)return entry;
        var pre=SupportProductionPdfPolicy.Preflight(config,entry);
        if(!pre.Allowed)
        {
            log("SPTC PDF "+entry.DrawingNumber+": "+pre.Status);
            return SupportProductionPdfPolicy.AfterFailure(entry,pre.Status);
        }
        Document? targetDoc=AcadApp.DocumentManager.MdiActiveDocument;
        string pdf=pre.ExpectedPdfPath;
        bool saved=false,plotStarted=false;
        long savedBytes=0;
        // Output has been proven absent before any publishing call.
        // Never delete or replace a file that was already present.
        try
        {
            if(targetDoc==null||ReferenceEquals(targetDoc,sourceDoc)||
               !SamePath(targetDoc.Database.Filename,entry.DestinationDwg)||
               !ReferenceEquals(AcadApp.DocumentManager.MdiActiveDocument,
                   targetDoc))
                return SupportProductionPdfPolicy.AfterFailure(
                    entry,"PDF_BLOCKED_TARGET_DOCUMENT_CHANGED");
            if(targetDoc.Database.TileMode)
                return SupportProductionPdfPolicy.AfterFailure(
                    entry,"PDF_BLOCKED_MODEL_SPACE");
            if(PlotFactory.ProcessPlotState!=ProcessPlotState.NotPlotting)
                return SupportProductionPdfPolicy.AfterFailure(
                    entry,"PDF_BLOCKED_ANOTHER_PLOT_ACTIVE");

            string? layoutName=ReadVerifiedLayout(entry.ReportPath);
            if(string.IsNullOrWhiteSpace(layoutName))
                return SupportProductionPdfPolicy.AfterFailure(
                    entry,"PDF_BLOCKED_LAYOUT_NOT_RECORDED");

            // The generator has finished its ExecuteInCommandContextAsync
            // native view/dimension writes; the queued SPTC command is
            // Session-scoped. Lock this exact output document only.
            log("SPTC PDF saving annotated native Ortho: "+
                Path.GetFileName(entry.DestinationDwg));
            using(targetDoc.LockDocument())
            {
                targetDoc.Database.SaveAs(
                    entry.DestinationDwg,DwgVersion.Current);
            }
            var persisted=new FileInfo(entry.DestinationDwg);
            persisted.Refresh();
            if(!persisted.Exists||persisted.Length<1024)
                return SupportProductionPdfPolicy.AfterFailure(
                    entry,"PDF_BLOCKED_DWG_SAVE_NOT_VERIFIED");
            savedBytes=persisted.Length;
            saved=true;

            // PDF plotting must be foreground and single-flight. The
            // original environment is restored regardless of outcome.
            object? previousBackground=null;
            bool backgroundChanged=false;
            try
            {
                previousBackground=AcadApp.GetSystemVariable("BACKGROUNDPLOT");
                AcadApp.SetSystemVariable("BACKGROUNDPLOT",0);
                backgroundChanged=true;
                PlotNativeLayout(targetDoc,layoutName,config.PdfPlotDevice,
                    pdf,()=>plotStarted=true);
            }
            finally
            {
                if(backgroundChanged)
                {
                    try{AcadApp.SetSystemVariable(
                        "BACKGROUNDPLOT",previousBackground);}
                    catch(System.Exception ex)
                    {
                        log("SPTC PDF BACKGROUNDPLOT restore warning: "+ex.Message);
                    }
                }
            }

            if(!SupportProductionPdfPolicy.VerifyPdf(pdf,out long pdfBytes))
                throw new IOException("PDF plot ended without a valid PDF header/EOF.");
            log("SPTC PDF verified: "+Path.GetFileName(pdf)+
                " bytes="+pdfBytes);
            return SupportProductionPdfPolicy.AfterPublication(
                entry,pdf,pdfBytes,savedBytes,config.PdfPlotDevice);
        }
        catch(System.Exception ex)
        {
            string code=ex is IOException?
                "PDF_FAILED_FILE_VALIDATION":"PDF_FAILED_NATIVE_PLOT";
            log("SPTC PDF "+code+": "+ex.GetType().Name+": "+ex.Message);
            // Only remove the new incomplete PDF created by this call.
            // Never delete a pre-existing issue drawing or plot.
            if(plotStarted&&File.Exists(pdf))
            {
                try{File.Delete(pdf);}
                catch(System.Exception cleanup)
                {
                    log("SPTC PDF incomplete file cleanup failed: "+
                        cleanup.Message);
                }
            }
            return SupportProductionPdfPolicy.AfterFailure(
                entry,code,saved,savedBytes);
        }
    }

    private static bool SamePath(string? a,string b)
    {
        if(string.IsNullOrWhiteSpace(a)||string.IsNullOrWhiteSpace(b))
            return false;
        return string.Equals(Path.GetFullPath(a),Path.GetFullPath(b),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadVerifiedLayout(string reportPath)
    {
        if(!File.Exists(reportPath))return null;
        const string marker="STEP4_SHEET_LAYOUT=";
        string? name=null;
        foreach(string line in File.ReadLines(reportPath))
        {
            int at=line.IndexOf(marker,StringComparison.Ordinal);
            if(at<0)continue;
            name=line[(at+marker.Length)..].Trim();
        }
        return string.IsNullOrWhiteSpace(name)||
            string.Equals(name,"Model",StringComparison.OrdinalIgnoreCase)
            ?null:name;
    }

    private static void PlotNativeLayout(
        Document targetDoc,string layoutName,string plotDevice,
        string pdfPath,Action started)
    {
        if(File.Exists(pdfPath))
            throw new IOException("Destination PDF already exists: no overwrite.");
        if(PlotFactory.ProcessPlotState!=ProcessPlotState.NotPlotting)
            throw new InvalidOperationException(
                "Another AutoCAD plot is currently running.");

        Database db=targetDoc.Database;
        using var tx=db.TransactionManager.StartTransaction();
        var dict=(DBDictionary)tx.GetObject(
            db.LayoutDictionaryId,OpenMode.ForRead);
        if(!dict.Contains(layoutName))
            throw new InvalidOperationException(
                "Recorded Ortho paper layout no longer exists.");
        var layout=(Layout)tx.GetObject(
            dict.GetAt(layoutName),OpenMode.ForRead);
        if(layout.ModelType)
            throw new InvalidOperationException(
                "Cannot publish native Ortho model space.");

        var validator=PlotSettingsValidator.Current;
        var devices=validator.GetPlotDeviceList();
        if(!devices.Cast<string>().Contains(plotDevice,
            StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Selected PDF PC3 device is not installed: "+plotDevice);

        string media=layout.CanonicalMediaName??"";
        if(media.Length==0||media.StartsWith("none",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Ortho layout has no verified plot media.");

        // Copy the native layout's page size, plot orientation, margins,
        // style and lineweight settings; never modify the actual layout.
        using var sheet=new PlotSettings(layout.ModelType);
        sheet.CopyFrom(layout);
        validator.SetPlotConfigurationName(sheet,plotDevice,media);
        validator.RefreshLists(sheet);
        var available=validator.GetCanonicalMediaNameList(sheet);
        if(!available.Cast<string>().Contains(media,
            StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "PDF plotter cannot reproduce this exact paper media: "+media);
        validator.SetCanonicalMediaName(sheet,media);
        validator.SetPlotType(sheet,
            Autodesk.AutoCAD.DatabaseServices.PlotType.Layout);

        using var plotInfo=new PlotInfo {
            Layout=layout.ObjectId,OverrideSettings=sheet
        };
        using var validate=new PlotInfoValidator {
            MediaMatchingPolicy=MatchingPolicy.MatchDisabled
        };
        validate.Validate(plotInfo);

        using var engine=PlotFactory.CreatePublishEngine();
        using var progress=new PlotProgressDialog(false,1,true);
        progress.IsVisible=false; // SPTC progress form is already visible.
        progress.OnBeginPlot();
        try
        {
            // Started is recorded before invoking the plotter, so any
            // partially-created new PDF can be cleaned if plotting fails.
            started();
            engine.BeginPlot(progress,null);
            engine.BeginDocument(plotInfo,db.Filename,null,1,true,pdfPath);
            using var page=new PlotPageInfo();
            engine.BeginPage(page,plotInfo,true,null);
            engine.BeginGenerateGraphics(null);
            engine.EndGenerateGraphics(null);
            engine.EndPage(null);
            engine.EndDocument(null);
            engine.EndPlot(null);
        }
        finally
        {
            progress.OnEndPlot();
        }
        tx.Commit();
    }
}
