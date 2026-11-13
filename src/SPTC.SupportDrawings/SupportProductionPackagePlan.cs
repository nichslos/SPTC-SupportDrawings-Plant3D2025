using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SPTC.SupportDrawings;

/// <summary>
/// Revision-independent, AutoCAD-free production drawing package planning.
/// Never infers structural fabrication dimensions, approves an unsaved DWG,
/// mutates native views, invents a PDF, or silently replaces an issue drawing.
/// An issue register is evidence of processing, not an engineering approval.
/// </summary>
internal static class SupportProductionPackagePlan
{
    internal static readonly string[] MetadataFields={
        "DrawingNumber","Project","Revision","Title",
        "SupportTag","Scale","SourceDrawing"
    };
    internal sealed record Settings(
        bool Enabled,string ProjectCode,string DrawingSeries,
        string Revision,string DrawingTitle,string TitleBlockName,
        Dictionary<string,string> AttributeTags,
        bool OverwriteExistingAttributes=false,
        bool RequirePdfForIssue=true,
        bool RequireFullDimensionCoverage=false,
        bool SkipVerifiedExisting=false,
        string SupportTagAttribute="",
        bool PublishPdf=false,
        string PdfPlotDevice="DWG To PDF.pc3");

    internal static Settings Defaults=>new(false,"","","",
        "PIPE SUPPORT DRAWING","",new Dictionary<string,string>());

    internal sealed record Evidence(
        string QaStatus,int NativeViews,int DimensionsCreated,
        int? ExpectedOveralls,int? PresentOveralls,
        int? OptionalOmissions,int? WitnessIntersections,
        string FinalStatus)
    {
        internal bool Pass =>
            QaStatus=="PASS"&&NativeViews>0&&
            ExpectedOveralls.HasValue&&PresentOveralls.HasValue&&
            ExpectedOveralls==PresentOveralls&&
            (!WitnessIntersections.HasValue||WitnessIntersections.Value==0)&&
            FinalStatus.StartsWith("TEN_NATIVE_VIEWS_AND_TITLES",StringComparison.Ordinal);
    }

    internal sealed record Entry(
        string Schema,string DrawingNumber,string SupportKey,
        string SourceDwg,string[] SourceHandles,
        long SourceWriteUtcTicks,long SourceLength,
        string DestinationDwg,string DrawingTitle,string ProjectCode,
        string Revision,string Scale,string[] Views,
        string BuildId,string QaStatus,string IssueStatus,
        string PdfStatus,string? PdfPath,string? MaterialRegister,
        int NativeViews,int DimensionsCreated,int? ExpectedOverallDimensions,
        int? PresentOverallDimensions,int? OptionalOmissions,
        int? WitnessIntersections,string? TitleBlockStatus,
        string ReportPath,string? IssueNote,string CreatedUtc,
        bool SavedDwgVerified=false,long SavedDwgLength=0,
        long VerifiedPdfBytes=0,string? PdfDeviceUsed=null);

    internal static Settings Validate(Settings input)
    {
        if(input==null)throw new ArgumentNullException(nameof(input));
        if(!input.Enabled)return Defaults;
        CheckCode(input.ProjectCode,"project code",32);
        CheckCode(input.DrawingSeries,"drawing series",20);
        CheckValue(input.Revision,"revision",24);
        CheckValue(input.DrawingTitle,"drawing title",120);
        if(input.TitleBlockName?.Length>80)
            throw new ArgumentException("Title block name exceeds 80 characters.");
        if(input.AttributeTags==null||input.AttributeTags.Count>MetadataFields.Length)
            throw new ArgumentException("Invalid title block attribute mapping.");
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var (field,tag) in input.AttributeTags)
        {
            if(!MetadataFields.Contains(field,StringComparer.Ordinal))
                throw new ArgumentException("Unsupported metadata field: "+field);
            CheckCode(tag,"attribute tag",64);
            if(!seen.Add(tag))
                throw new ArgumentException("One title block tag cannot map multiple fields.");
        }
        if(input.AttributeTags.Count>0&&string.IsNullOrWhiteSpace(input.TitleBlockName))
            throw new ArgumentException(
                "Exact title block name is required before mapping attributes.");
        if(!string.IsNullOrWhiteSpace(input.SupportTagAttribute))
            CheckCode(input.SupportTagAttribute,"support tag source attribute",64);
        if(input.PublishPdf && !SupportProductionPdfPolicy.AllowedDevice(
            input.PdfPlotDevice))
            throw new ArgumentException(
                "PDF publishing requires a supported AutoCAD PDF PC3 device.");
        return input with {
            ProjectCode=input.ProjectCode.Trim(),
            DrawingSeries=input.DrawingSeries.Trim(),
            Revision=input.Revision.Trim(),
            DrawingTitle=input.DrawingTitle.Trim(),
            TitleBlockName=(input.TitleBlockName??"").Trim(),
            SupportTagAttribute=(input.SupportTagAttribute??"").Trim(),
            AttributeTags=new Dictionary<string,string>(input.AttributeTags,
                StringComparer.OrdinalIgnoreCase)
        };
    }

    private static void CheckCode(string? value,string label,int max)
    {
        if(string.IsNullOrWhiteSpace(value)||value.Length>max||
            !value.All(c=>char.IsAsciiLetterOrDigit(c)||c=='-'||c=='_'))
            throw new ArgumentException(
                "Invalid "+label+": use letters, digits, hyphen or underscore.");
    }

    private static void CheckValue(string? value,string label,int max)
    {
        if(string.IsNullOrWhiteSpace(value)||value.Length>max||
           value.Any(c=>char.IsControl(c)&&c!=' '))
            throw new ArgumentException("Missing or invalid "+label+".");
    }

    internal static string Fingerprint(string sourceDwg,
        IEnumerable<string> sourceHandles)
    {
        if(string.IsNullOrWhiteSpace(sourceDwg)||sourceHandles==null)
            throw new ArgumentException("Verified source and handles are required.");
        string[] handles=sourceHandles.Select(s=>s.Trim().ToUpperInvariant())
            .Where(s=>s.Length>0)
            .Distinct(StringComparer.Ordinal).OrderBy(s=>s,StringComparer.Ordinal)
            .ToArray();
        if(handles.Length==0)throw new ArgumentException(
            "A package must have at least one selected support handle.");
        string normalized=Path.GetFullPath(sourceDwg)
            .TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(normalized+"|"+string.Join("|",handles))))
            .Substring(0,12);
    }

    internal static string DrawingNumber(Settings config,
        string sourceDwg,IEnumerable<string> handles,string? supportTag=null)
    {
        var s=Validate(config);
        if(!s.Enabled)throw new ArgumentException("Production package is disabled.");
        string fingerprint=Fingerprint(sourceDwg,handles);
        string tag=string.IsNullOrWhiteSpace(supportTag)?"":new string(
            supportTag.Trim().ToUpperInvariant().Where(
                c=>char.IsAsciiLetterOrDigit(c)||c=='-'||c=='_').ToArray());
        if(tag.Length>32)tag=tag.Substring(0,32);
        // Even identical PnP support tags in different source models
        // remain unique. Stable independent of run time or view order.
        return s.ProjectCode+"-"+s.DrawingSeries+"-"+
            (tag.Length>0?tag+"-":"")+fingerprint;
    }

    internal static Evidence ParseReport(string report)
    {
        if(report==null)throw new ArgumentNullException(nameof(report));
        string Get(string key)
        {
            string value="";
            foreach(string row in report.Split('\n'))
            {
                int i=row.IndexOf(key+"=",StringComparison.Ordinal);
                if(i<0||(i>0&&!char.IsWhiteSpace(row[i-1])))continue;
                value=row[(i+key.Length+1)..].Trim();
            }
            return value;
        }
        int? Int(string key)=>int.TryParse(Get(key),
            NumberStyles.Integer,CultureInfo.InvariantCulture,
            out int val)?val:null;
        return new Evidence(
            Get("REV22_61_QA_STATUS"),
            Int("STEP10_SELECTED_NATIVE_VIEWS_REGISTERED")??0,
            Int("REV22_41_DIMENSIONS_CREATED")??0,
            Int("REV22_61_EXPECTED_HV_OVERALLS"),
            Int("REV22_61_PRESENT_HV_OVERALLS"),
            Int("REV22_61_QA_OPTIONAL_OMISSIONS"),
            Int("REV22_61_QA_WITNESS_INTERSECTIONS"),
            Get("FINAL_STATUS"));
    }

    internal static Entry BuildEntry(
        Settings config,string sourceDwg,IEnumerable<string> handles,
        string destinationDwg,string reportPath,string reportText,
        string[] selectedViews,string scale,string buildId,
        string? supportTag=null,string? titleBlockStatus=null,
        string? materialCsv=null)
    {
        var valid=Validate(config);
        if(!valid.Enabled)throw new ArgumentException("Package is disabled.");
        string[] orderedHandles=handles.Where(x=>!string.IsNullOrWhiteSpace(x))
            .Select(x=>x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal)
            .ToArray();
        var stamp=new FileInfo(sourceDwg);
        var qa=ParseReport(reportText);
        bool complete=qa.Pass&&(!valid.RequireFullDimensionCoverage||
            (qa.OptionalOmissions.HasValue&&qa.OptionalOmissions.Value==0));
        string why=!qa.Pass?"NATIVE_DRAWING_QA_NOT_VERIFIED":
            valid.RequireFullDimensionCoverage&&
            qa.OptionalOmissions.GetValueOrDefault()>0?
                "OPTIONAL_DIMENSIONS_OMITTED":
            "DWG_SAVE_AND_ISSUE_REVIEW_REQUIRED";
        return new Entry(
            "SPTC-PRODUCTION-DRAWING-1",
            DrawingNumber(valid,sourceDwg,orderedHandles,supportTag),
            Fingerprint(sourceDwg,orderedHandles),
            Path.GetFullPath(sourceDwg),orderedHandles,
            stamp.Exists?stamp.LastWriteTimeUtc.Ticks:0,
            stamp.Exists?stamp.Length:0,
            Path.GetFullPath(destinationDwg),
            valid.DrawingTitle,valid.ProjectCode,valid.Revision,
            scale,selectedViews.ToArray(),buildId,
            complete?"PASS":"REVIEW",
            // Never imply an in-memory AutoCAD DWG is saved/approved.
            "DRAFT_NEEDS_SAVED_DWG_AND_QA",
            valid.RequirePdfForIssue?"PDF_REQUIRED_NOT_PUBLISHED":
                "PDF_NOT_REQUESTED",null,materialCsv,
            qa.NativeViews,qa.DimensionsCreated,
            qa.ExpectedOveralls,qa.PresentOveralls,
            qa.OptionalOmissions,qa.WitnessIntersections,
            titleBlockStatus,Path.GetFullPath(reportPath),
            why,DateTimeOffset.UtcNow.ToString("O",CultureInfo.InvariantCulture));
    }

    internal static Entry FailedEntry(Settings config,string sourceDwg,
        IEnumerable<string> handles,string issueNote)
    {
        var valid=Validate(config);
        if(!valid.Enabled)throw new ArgumentException("Package is disabled.");
        string[] selected=handles.Where(x=>!string.IsNullOrWhiteSpace(x))
            .Select(x=>x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal)
            .ToArray();
        var info=new FileInfo(sourceDwg);
        return new Entry(
            "SPTC-PRODUCTION-DRAWING-1",
            DrawingNumber(valid,sourceDwg,selected),
            Fingerprint(sourceDwg,selected),Path.GetFullPath(sourceDwg),
            selected,info.Exists?info.LastWriteTimeUtc.Ticks:0,
            info.Exists?info.Length:0,"",valid.DrawingTitle,
            valid.ProjectCode,valid.Revision,"",
            Array.Empty<string>(),"","FAILED","GENERATION_FAILED",
            "NOT_PUBLISHED",null,null,0,0,null,null,null,null,
            "NOT_ATTEMPTED","","GENERATION_FAILED:"+issueNote,
            DateTimeOffset.UtcNow.ToString("O",
                CultureInfo.InvariantCulture));
    }

    internal static string ChangeStatus(Entry previous,Entry latest)
    {
        if(previous.SupportKey!=latest.SupportKey)
            return "DIFFERENT_SUPPORT";
        if(previous.SourceWriteUtcTicks!=latest.SourceWriteUtcTicks||
           previous.SourceLength!=latest.SourceLength)
            return "SOURCE_STAMP_CHANGED_REVIEW";
        if(previous.Views.Length!=latest.Views.Length||
           !previous.Views.OrderBy(x=>x,StringComparer.Ordinal)
             .SequenceEqual(latest.Views.OrderBy(x=>x,StringComparer.Ordinal))||
           !string.Equals(previous.Scale,latest.Scale,StringComparison.Ordinal))
            return "DRAWING_CONFIGURATION_CHANGED_REVIEW";
        return "SAME_SOURCE_STAMP_NOT_GEOMETRY_VERIFIED";
    }

    internal static bool CanSkipVerifiedExisting(
        Settings s,Entry prior,string sourceDwg,
        IEnumerable<string> handles,long sourceStamp,long sourceLength,
        string[] views,string scale,bool verifiedSavedDrawing,bool pdfExists)
    {
        // Newly generated draft sidecars NEVER qualify. Only a
        // separately verified issue-ready record can be skipped.
        return Validate(s).Enabled&&s.SkipVerifiedExisting&&
            prior.IssueStatus=="ISSUE_VERIFIED"&&prior.QaStatus=="PASS"&&
            verifiedSavedDrawing&&File.Exists(prior.DestinationDwg)&&
            (!s.RequirePdfForIssue||pdfExists)&&
            prior.SourceWriteUtcTicks==sourceStamp&&
            prior.SourceLength==sourceLength&&
            prior.SupportKey==Fingerprint(sourceDwg,handles)&&
            prior.Scale==scale&&
            prior.Views.OrderBy(x=>x,StringComparer.Ordinal)
                .SequenceEqual(views.OrderBy(x=>x,StringComparer.Ordinal));
    }

    internal static string RegisterCsv(IEnumerable<Entry> entries)
    {
        var columns=new[]{
            "DrawingNumber","SupportKey","SourceDWG","SourceHandles",
            "OutputDWG","Revision","Scale","Views","QA","IssueStatus",
            "PDFStatus","PDFPath","Dimensions","ExpectedOveralls",
            "PresentOveralls","OptionalOmissions","ReportPath","MaterialRegister",
            "CreatedUTC","SavedDWGVerified","SavedDWGBytes",
            "VerifiedPDFBytes","PDFDevice"};
        var sb=new StringBuilder().AppendLine(string.Join(",",columns));
        foreach(var e in entries)
        {
            string[] fields={
                e.DrawingNumber,e.SupportKey,e.SourceDwg,
                string.Join(";",e.SourceHandles),e.DestinationDwg,
                e.Revision,e.Scale,string.Join(";",e.Views),
                e.QaStatus,e.IssueStatus,e.PdfStatus,e.PdfPath??"",
                e.DimensionsCreated.ToString(CultureInfo.InvariantCulture),
                e.ExpectedOverallDimensions?.ToString(
                    CultureInfo.InvariantCulture)??"",
                e.PresentOverallDimensions?.ToString(
                    CultureInfo.InvariantCulture)??"",
                e.OptionalOmissions?.ToString(CultureInfo.InvariantCulture)??"",
                e.ReportPath,e.MaterialRegister??"",e.CreatedUtc,
                e.SavedDwgVerified.ToString(),
                e.SavedDwgLength.ToString(CultureInfo.InvariantCulture),
                e.VerifiedPdfBytes.ToString(CultureInfo.InvariantCulture),
                e.PdfDeviceUsed??""
            };
            sb.AppendLine(string.Join(",",fields.Select(Csv)));
        }
        return sb.ToString();
    }

    private static string Csv(string value)
    {
        if(value.Length>0&&"=+-@\t\r\n".Contains(value[0]))
            value="'"+value;
        return "\""+value.Replace("\"","\"\"")+"\"";
    }

    internal static void SaveEntry(Entry entry)
    {
        string file=Path.ChangeExtension(entry.ReportPath,".production.json");
        WriteAtomic(file,JsonSerializer.Serialize(entry,
            new JsonSerializerOptions{WriteIndented=true}));
    }

    internal static void SaveRegister(string file,IEnumerable<Entry> entries)=>
        WriteAtomic(file,RegisterCsv(entries));

    internal static void WriteAtomic(string path,string value)
    {
        string full=Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        string tmp=full+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(tmp,value,new UTF8Encoding(false));
            File.Move(tmp,full,overwrite:true);
        }
        finally{if(File.Exists(tmp))File.Delete(tmp);}
    }
}
