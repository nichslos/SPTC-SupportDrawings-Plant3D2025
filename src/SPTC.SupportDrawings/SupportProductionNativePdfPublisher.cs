using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    internal static async Task<SupportProductionPackagePlan.Entry> PublishAsync(
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
        string stage="TARGET_DOCUMENT_CHECK";
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

            stage="READ_SHEET_LAYOUT";
            string? layoutName=ReadVerifiedLayout(entry.ReportPath);
            if(string.IsNullOrWhiteSpace(layoutName))
                return SupportProductionPdfPolicy.AfterFailure(
                    entry,"PDF_BLOCKED_LAYOUT_NOT_RECORDED");

            // The target DWG is already a named/open AutoCAD document.
            // A SECOND Database.SaveAs to that same open filename raised
            // eFilerError on the live TEST5 run. Do not call SaveAs on the
            // active document. Native QSAVE in its own document command
            // context is the supported same-name save operation.
            //
            // This entire method is called from a Session command, so
            // Editor.CommandAsync requires ExecuteInCommandContextAsync.
            // Do not hold a DocumentLock around QSAVE: that command owns
            // its current document lock. Fail closed if AutoCAD switches
            // drawings before we can save.
            stage="DWG_SAVE";
            var beforeSave=new FileInfo(entry.DestinationDwg);
            long beforeBytes=beforeSave.Exists?beforeSave.Length:0;
            DateTime beforeTimestamp=beforeSave.Exists?
                beforeSave.LastWriteTimeUtc:DateTime.MinValue;
            log("SPTC_PDF_PUBLISH_STAGE="+stage+
                " METHOD=NATIVE_QSAVE DOC="+
                Path.GetFileName(entry.DestinationDwg));
            System.Exception? quickSaveError=null;
            var manager=AcadApp.DocumentManager;
            await manager.ExecuteInCommandContextAsync(async _=>
            {
                // Protect AutoCAD from an exception escaping the native
                // command-context callback; rethrow in the caller instead.
                try
                {
                    if(manager.IsApplicationContext||
                       !ReferenceEquals(manager.MdiActiveDocument,targetDoc)||
                       !SamePath(targetDoc.Database.Filename,
                           entry.DestinationDwg))
                        throw new InvalidOperationException(
                            "Ortho DWG is no longer the current command document.");
                    await targetDoc.Editor.CommandAsync("_.QSAVE");
                }
                catch(System.Exception ex){quickSaveError=ex;}
            },null);
            if(quickSaveError!=null)
                throw new InvalidOperationException(
                    "Native QSAVE could not save the current Ortho drawing: "+
                    quickSaveError.Message,quickSaveError);
            if(!ReferenceEquals(manager.MdiActiveDocument,targetDoc)||
               !SamePath(targetDoc.Database.Filename,entry.DestinationDwg))
                throw new InvalidOperationException(
                    "Active Ortho drawing changed during QSAVE.");
            stage="DWG_VERIFY";
            var persisted=new FileInfo(entry.DestinationDwg);
            persisted.Refresh();
            if(!persisted.Exists||persisted.Length<1024||
               (persisted.LastWriteTimeUtc<=beforeTimestamp&&
                persisted.Length==beforeBytes))
                return SupportProductionPdfPolicy.AfterFailure(
                    entry,"PDF_BLOCKED_DWG_SAVE_NOT_VERIFIED");
            savedBytes=persisted.Length;
            saved=true;
            log("SPTC_PDF_PUBLISH_DWG_QSAVE_VERIFIED=TRUE BYTES="+
                savedBytes+" UPDATED_UTC="+
                persisted.LastWriteTimeUtc.ToString("O"));

            // PDF plotting must be foreground and single-flight. The
            // original environment is restored regardless of outcome.
            stage="PLOT_SETUP";
            log("SPTC_PDF_PUBLISH_STAGE="+stage+
                " PDF_DEVICE="+config.PdfPlotDevice);
            object? previousBackground=null;
            bool backgroundChanged=false;
            try
            {
                previousBackground=AcadApp.GetSystemVariable("BACKGROUNDPLOT");
                AcadApp.SetSystemVariable("BACKGROUNDPLOT",0);
                backgroundChanged=true;
                PlotNativeLayout(targetDoc,layoutName,config.PdfPlotDevice,
                    pdf,()=>{
                        plotStarted=true;
                        stage="NATIVE_PLOT_ENGINE";
                    },log);
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

            stage="PDF_FILE_VERIFY";
            if(!SupportProductionPdfPolicy.VerifyPdf(pdf,out long pdfBytes))
                throw new IOException("PDF plot ended without a valid PDF header/EOF.");
            log("SPTC PDF verified: "+Path.GetFileName(pdf)+
                " bytes="+pdfBytes);
            return SupportProductionPdfPolicy.AfterPublication(
                entry,pdf,pdfBytes,savedBytes,config.PdfPlotDevice);
        }
        catch(System.Exception ex)
        {
            string code=SupportProductionPdfPolicy.FailureCode(stage);
            string details=(ex.GetType().Name+": "+ex.Message)
                .Replace("\r"," ").Replace("\n"," ").Trim();
            if(details.Length>500)details=details.Substring(0,500);
            log("SPTC_PDF_PUBLISH_ERROR_STAGE="+stage+
                " CODE="+code+" EXCEPTION="+details);
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
            var failed=SupportProductionPdfPolicy.AfterFailure(
                entry,code,saved,savedBytes);
            return failed with {
                IssueNote=code+" STAGE="+stage+" EXCEPTION="+details
            };
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
        string pdfPath,Action started,Action<string> log)
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

        log("SPTC_PDF_PUBLISH_STAGE=PLOT_CONFIG_READ_LAYOUT");
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
        log("SPTC_PDF_PUBLISH_STAGE=PLOT_CONFIG_EXACT_MEDIA MEDIA="+media);
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

        log("SPTC_PDF_PUBLISH_STAGE=PLOT_ENGINE_CREATE");
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
