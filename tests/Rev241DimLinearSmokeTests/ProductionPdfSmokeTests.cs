using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SPTC.SupportDrawings;

/// <summary>
/// Policy-only CI regression. Native PlotEngine is compiled in the production
/// DLL, but it must be live-tested in an actual Plant 3D 2025 process.
/// Fake test bytes are never represented as a real plotted sheet.
/// </summary>
internal static class ProductionPdfSmokeTests
{
    private static void Need(bool condition,string label)
    {
        if(!condition)throw new Exception("Native PDF publish policy: "+label);
    }

    internal static void Run()
    {
        string folder=Path.Combine(Path.GetTempPath(),
            "SPTC_PDF_POLICY_"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string dwg=Path.Combine(folder,"support_ortho.dwg");
        string pdf=Path.ChangeExtension(dwg,".pdf");
        File.WriteAllBytes(dwg,new byte[3200]);
        try
        {
            var settings=new SupportProductionPackagePlan.Settings(
                true,"T26","SUP","A","SUPPORT SHEET","SHEET",
                new Dictionary<string,string>{
                    ["DrawingNumber"]="DWG_NO"},
                false,true,false,false,"",true,"DWG To PDF.pc3");
            Need(SupportProductionPdfPolicy.AllowedDevice(
                "DWG To PDF.pc3")&&
                !SupportProductionPdfPolicy.AllowedDevice(
                    "GenericPrinter.pc3"),
                "approved native PDF configurations only");
            var entry=new SupportProductionPackagePlan.Entry(
                "SPTC-PRODUCTION-DRAWING-1","T26-SUP-FOO","K1",
                Path.Combine(folder,"source.dwg"),new[]{"8A4"},1,100,
                dwg,"SUPPORT SHEET","T26","A","1:20",
                new[]{"TOP","BOTTOM","LEFT","RIGHT","FRONT","BACK",
                    "NW","NE","SW","SE"},
                "test-sha","PASS","DRAFT_NEEDS_SAVED_DWG_AND_QA",
                "PDF_REQUIRED_NOT_PUBLISHED",null,null,10,25,
                12,12,2,0,"MAPPED_ATTRIBUTES_VERIFIED_IN_MEMORY",
                Path.Combine(folder,"result.txt"),"REVIEW","2026-11-13T00:00:00Z");
            var prior=new FileInfo(dwg);
            long oldSize=prior.Length;
            DateTime oldUtc=prior.LastWriteTimeUtc;
            Need(!SupportProductionPdfPolicy.VerifyNewDwgSave(
                    dwg,oldSize,oldUtc,out _),
                "existing seed clone alone does not prove QSAVE");
            File.WriteAllBytes(dwg,new byte[3500]);
            File.SetLastWriteTimeUtc(dwg,oldUtc.AddSeconds(5));
            Need(SupportProductionPdfPolicy.VerifyNewDwgSave(
                    dwg,oldSize,oldUtc,out long savedBytes)&&
                savedBytes==3500,
                "native QSAVE evidence requires real saved DWG write");
            Need(!SupportProductionPdfPolicy.VerifyNewDwgSave(
                    Path.Combine(folder,"missing.dwg"),0,DateTime.MinValue,
                    out _),
                "missing generated DWG cannot be published");
            var ready=SupportProductionPdfPolicy.Preflight(settings,entry);
            Need(ready.Allowed&&ready.Status=="PDF_PREFLIGHT_PASS"&&
                ready.ExpectedPdfPath==pdf,"valid ten-view QA reaches plot gate");
            Need(!SupportProductionPdfPolicy.Preflight(
                settings with {PublishPdf=false},entry).Allowed,
                "PDF never publishes when explicitly disabled");
            Need(SupportProductionPdfPolicy.Preflight(
                settings,entry with {QaStatus="REVIEW"}).Status==
                "PDF_BLOCKED_QA",
                "QA partial cannot publish");
            Need(SupportProductionPdfPolicy.Preflight(
                settings,entry with {NativeViews=9}).Status==
                "PDF_BLOCKED_QA",
                "missing native view cannot publish");
            Need(SupportProductionPdfPolicy.Preflight(
                settings,entry with {PresentOverallDimensions=11}).Status==
                "PDF_BLOCKED_QA",
                "essential dimensions incomplete cannot publish");
            Need(SupportProductionPdfPolicy.Preflight(
                settings with {RequireFullDimensionCoverage=true},entry
                ).Status=="PDF_BLOCKED_QA",
                "strict all-dimensions release gate blocks optional omissions");
            Need(SupportProductionPdfPolicy.Preflight(
                settings,entry with {WitnessIntersections=1}).Status==
                "PDF_BLOCKED_QA",
                "witness intersections fail plot preflight");
            Need(SupportProductionPdfPolicy.Preflight(
                settings,entry with {TitleBlockStatus="PARTIAL_NEEDS_REVIEW"}
                ).Status=="PDF_BLOCKED_TITLEBLOCK",
                "mapped but incomplete title block must not publish");
            Need(SupportProductionPdfPolicy.Preflight(
                settings with {AttributeTags=new Dictionary<string,string>()},
                entry with {TitleBlockStatus="NOT_CONFIGURED"}
                ).Allowed,
                "titleblock mapping is optional when none configured");

            File.WriteAllText(pdf,"This is not really a PDF.");
            Need(!SupportProductionPdfPolicy.VerifyPdf(pdf,out _),
                "PDF extension alone is not publication evidence");
            Need(SupportProductionPdfPolicy.Preflight(settings,entry).Status==
                "PDF_ALREADY_EXISTS_NO_OVERWRITE",
                "never overwrite existing PDF");
            File.Delete(pdf);

            // Only exercising the byte-integrity reader, not PlotEngine.
            using(var stream=new FileStream(pdf,FileMode.CreateNew))
            {
                var bytes=Encoding.ASCII.GetBytes("%PDF-1.7\n");
                stream.Write(bytes,0,bytes.Length);
                stream.Write(new byte[2048]);
                bytes=Encoding.ASCII.GetBytes("\n%%EOF\n");
                stream.Write(bytes,0,bytes.Length);
            }
            Need(SupportProductionPdfPolicy.VerifyPdf(pdf,out long pdfBytes)&&
                pdfBytes>2048,
                "real PDF magic + EOF checks both required");
            var plotted=SupportProductionPdfPolicy.AfterPublication(
                entry,pdf,pdfBytes,3200,settings.PdfPlotDevice);
            Need(plotted.PdfStatus=="PDF_PUBLISHED_VERIFIED"&&
                plotted.SavedDwgVerified&&plotted.SavedDwgLength==3200&&
                plotted.PdfPath==pdf&&
                plotted.IssueStatus=="DRAFT_SAVED_DWG_PDF_NEEDS_REVIEW"&&
                plotted.VerifiedPdfBytes==pdfBytes,
                "physical publisher evidence recorded, still not issued");
            var failed=SupportProductionPdfPolicy.AfterFailure(
                entry,"PDF_FAILED_NATIVE_PLOT",true,3200);
            Need(failed.PdfPath==null&&failed.VerifiedPdfBytes==0&&
                failed.IssueStatus=="DRAFT_NEEDS_SAVED_DWG_AND_QA",
                "PDF plot failure never claims success or issue approval");
            Need(SupportProductionPdfPolicy.FailureCode("DWG_SAVE")==
                "PDF_FAILED_DWG_SAVE"&&
                SupportProductionPdfPolicy.FailureCode("PLOT_SETUP")==
                "PDF_FAILED_PLOT_SETUP"&&
                SupportProductionPdfPolicy.FailureCode("NATIVE_PLOT_ENGINE")==
                "PDF_FAILED_NATIVE_PLOT"&&
                SupportProductionPdfPolicy.FailureCode("PDF_FILE_VERIFY")==
                "PDF_FAILED_FILE_VALIDATION"&&
                SupportProductionPdfPolicy.FailureCode("UNRECOGNIZED")==
                "PDF_FAILED_UNKNOWN_STAGE",
                "exact DWG save, plot preparation and engine failure stages preserved");
            var failureWithCause=failed with {
                IssueNote="PDF_FAILED_DWG_SAVE STAGE=DWG_SAVE EXCEPTION=AutodeskException: eFileSharingViolation"
            };
            var diagnosticCsv=SupportProductionPackagePlan.RegisterCsv(
                new[]{failureWithCause});
            Need(diagnosticCsv.Contains("PDFDiagnostic")&&
                diagnosticCsv.Contains("STAGE=DWG_SAVE")&&
                diagnosticCsv.Contains("eFileSharingViolation"),
                "CSV register preserves exact failure stage and native exception message");
            SupportProductionPackagePlan.SaveEntry(failureWithCause);
            string diagnosticJson=System.IO.File.ReadAllText(
                System.IO.Path.ChangeExtension(
                    entry.ReportPath,".production.json"));
            Need(diagnosticJson.Contains("STAGE=DWG_SAVE")&&
                diagnosticJson.Contains("eFileSharingViolation"),
                "atomic JSON sidecar persists the same actionable publish stage");
            var register=SupportProductionPackagePlan.RegisterCsv(new[]{
                plotted,failed});
            Need(register.Contains("PDF_PUBLISHED_VERIFIED")&&
                register.Contains("PDF_FAILED_NATIVE_PLOT")&&
                register.Contains("SavedDWGVerified")&&
                register.Contains("VerifiedPDFBytes"),
                "batch CSV differentiates PDF published and failed rows");

            bool invalid=false;
            try{SupportProductionPackagePlan.Validate(
                settings with {PdfPlotDevice="OfficePrinter.pc3"});}
            catch(ArgumentException){invalid=true;}
            Need(invalid,"unsafe plot-device override rejected");
        }
        finally {Directory.Delete(folder,true);}
        Console.WriteLine(
            "Native PDF preflight, saved DWG gate, QA/title block/overall coverage, no-overwrite, PDF magic+EOF, CSV publish statuses and draft issue safety PASS");
    }
}
