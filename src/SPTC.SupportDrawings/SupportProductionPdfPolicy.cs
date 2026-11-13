using System;
using System.IO;
using System.Linq;
using System.Text;

namespace SPTC.SupportDrawings;

/// <summary>
/// Pure production PDF preflight, fail-closed media and file-evidence audit.
/// Never confuses a finished plot operation or a file extension with an
/// actually verified PDF. No Autodesk or native DWG access in this class.
/// </summary>
internal static class SupportProductionPdfPolicy
{
    internal static readonly string[] ApprovedPdfDevices={
        "DWG To PDF.pc3",
        "AutoCAD PDF (General Documentation).pc3"
    };

    internal sealed record Decision(
        bool Allowed,string Status,string ExpectedPdfPath);

    internal static string FailureCode(string stage)=>stage switch
    {
        "TARGET_DOCUMENT_CHECK" or "READ_SHEET_LAYOUT" =>
            "PDF_FAILED_PREPARE",
        "DWG_SAVE" => "PDF_FAILED_DWG_SAVE",
        "DWG_VERIFY" => "PDF_FAILED_DWG_VERIFY",
        "PLOT_SETUP" => "PDF_FAILED_PLOT_SETUP",
        "NATIVE_PLOT_ENGINE" => "PDF_FAILED_NATIVE_PLOT",
        "PDF_FILE_VERIFY" => "PDF_FAILED_FILE_VALIDATION",
        _ => "PDF_FAILED_UNKNOWN_STAGE"
    };

    internal static bool AllowedDevice(string? device)=>
        device!=null&&ApprovedPdfDevices.Contains(
            device,StringComparer.OrdinalIgnoreCase);

    internal static Decision Preflight(
        SupportProductionPackagePlan.Settings config,
        SupportProductionPackagePlan.Entry entry)
    {
        string path=string.IsNullOrWhiteSpace(entry.DestinationDwg)
            ?""
            :Path.ChangeExtension(
                Path.GetFullPath(entry.DestinationDwg),".pdf");
        if(!config.Enabled||!config.PublishPdf)
            return new(false,"PDF_DISABLED",path);
        if(!AllowedDevice(config.PdfPlotDevice))
            return new(false,"PDF_DEVICE_NOT_APPROVED",path);
        if(entry.QaStatus!="PASS"||
           entry.IssueStatus!="DRAFT_NEEDS_SAVED_DWG_AND_QA"||
           entry.NativeViews<=0||
           entry.NativeViews!=entry.Views.Length||
           entry.ExpectedOverallDimensions==null||
           entry.PresentOverallDimensions!=entry.ExpectedOverallDimensions||
           entry.WitnessIntersections.GetValueOrDefault()>0||
           (config.RequireFullDimensionCoverage&&
                entry.OptionalOmissions!=0))
            return new(false,"PDF_BLOCKED_QA",path);
        if(config.AttributeTags.Count>0&&
           entry.TitleBlockStatus!="MAPPED_ATTRIBUTES_VERIFIED_IN_MEMORY")
            return new(false,"PDF_BLOCKED_TITLEBLOCK",path);
        if(path.Length==0||!File.Exists(entry.DestinationDwg)||
           new FileInfo(entry.DestinationDwg).Length<1024)
            return new(false,"PDF_BLOCKED_MISSING_DWG",path);
        if(File.Exists(path))
            return new(false,"PDF_ALREADY_EXISTS_NO_OVERWRITE",path);
        return new(true,"PDF_PREFLIGHT_PASS",path);
    }

    internal static bool VerifyPdf(string file,out long byteCount)
    {
        byteCount=0;
        if(!File.Exists(file))return false;
        try
        {
            using var stream=new FileStream(file,FileMode.Open,
                FileAccess.Read,FileShare.Read);
            if(stream.Length<1024||stream.Length>int.MaxValue)
                return false;
            byteCount=stream.Length;
            var header=new byte[8];
            if(stream.Read(header,0,header.Length)!=header.Length||
               Encoding.ASCII.GetString(header,0,5)!="%PDF-")
                return false;
            int tailLength=(int)Math.Min(stream.Length,4096L);
            var trailer=new byte[tailLength];
            stream.Seek(-tailLength,SeekOrigin.End);
            if(stream.Read(trailer,0,tailLength)!=tailLength)
                return false;
            // The latest EOF marker is required: a non-empty file or a
            // filename ending ".pdf" is insufficient proof of publication.
            return Encoding.ASCII.GetString(trailer).Contains(
                "%%EOF",StringComparison.Ordinal);
        }
        catch(IOException){return false;}
        catch(UnauthorizedAccessException){return false;}
    }

    internal static SupportProductionPackagePlan.Entry AfterPublication(
        SupportProductionPackagePlan.Entry entry,string pdf,
        long pdfBytes,long dwgBytes,string pdfDevice)
    {
        if(pdfBytes<1024||dwgBytes<1024||
           !string.Equals(Path.ChangeExtension(entry.DestinationDwg,".pdf"),
               pdf,StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                "A verified saved DWG and matching PDF are required.");
        return entry with {
            PdfStatus="PDF_PUBLISHED_VERIFIED",
            PdfPath=pdf,
            VerifiedPdfBytes=pdfBytes,
            SavedDwgVerified=true,
            SavedDwgLength=dwgBytes,
            PdfDeviceUsed=pdfDevice,
            IssueStatus="DRAFT_SAVED_DWG_PDF_NEEDS_REVIEW",
            IssueNote="PDF_VALIDATED_NOT_ISSUED"
        };
    }

    internal static SupportProductionPackagePlan.Entry AfterFailure(
        SupportProductionPackagePlan.Entry entry,string status,
        bool dwgSaved=false,long dwgBytes=0)
    {
        if(status!="PDF_DISABLED"&&
           !status.StartsWith("PDF_",StringComparison.Ordinal))
            throw new ArgumentException("Invalid PDF failure status.");
        return entry with {
            PdfStatus=status,
            PdfPath=null,
            VerifiedPdfBytes=0,
            PdfDeviceUsed=null,
            SavedDwgVerified=dwgSaved,
            SavedDwgLength=dwgSaved?dwgBytes:0,
            IssueStatus=entry.IssueStatus=="GENERATION_FAILED"?
                "GENERATION_FAILED":"DRAFT_NEEDS_SAVED_DWG_AND_QA",
            IssueNote=status
        };
    }

    internal static string ReportLine(
        SupportProductionPackagePlan.Entry entry)=>
        "DRAWING_NUMBER="+entry.DrawingNumber+
        " QA="+entry.QaStatus+
        " DWG_SAVED="+entry.SavedDwgVerified+
        " PDF="+entry.PdfStatus+
        " PDF_BYTES="+entry.VerifiedPdfBytes+
        " ISSUE="+entry.IssueStatus;
}
