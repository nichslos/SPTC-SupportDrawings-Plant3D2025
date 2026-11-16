using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV67: READ-ONLY lightweight PDF trailer/xref evidence. This is not
/// a complete PDF syntax parser or substitute for Adobe Acrobat/qpdf.
/// Do not alter Native PlotEngine output, rewrite streams or suppress
/// issuance review. Verified native PDF status remains independent.
/// </summary>
internal static class SupportProductionPdfStructure67
{
    internal sealed record Result(string Status,int StartXrefOffset,
        int EofOffset,string Note);

    // REV68: append the already-computed PDF diagnostic to the
    // generated RESULT.txt AFTER native PDF verification. Does NOT
    // rewrite a PDF, alter production issue status or change title
    // blocks. Never create/overwrite an unverified report file.
    internal static bool TryAppendToExistingResult(
        string? reportPath,Result evidence,out string status)
    {
        status="NO_EXISTING_RESULT_REPORT";
        if(string.IsNullOrWhiteSpace(reportPath)||
           !string.Equals(Path.GetExtension(reportPath),".txt",
                StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            var info=new FileInfo(reportPath);
            if(!info.Exists||info.Length>32*1024*1024)
            {
                status="REPORT_MISSING_OR_OVERSIZED";
                return false;
            }
            string line=DateTime.UtcNow.ToString("O")+
                " REV67_PDF_STRUCTURE="+evidence.Status+
                " STARTXREF="+evidence.StartXrefOffset+
                " EOF="+evidence.EofOffset+
                " NOTE="+evidence.Note.Replace("\r"," ").
                    Replace("\n"," ")+"\r\n"+
                DateTime.UtcNow.ToString("O")+
                " REV67_EXTERNAL_PDF_READER_CHECK_REQUIRED=TRUE\r\n";
            File.AppendAllText(reportPath,line);
            status="APPENDED_TO_EXISTING_RESULT";
            return true;
        }
        catch(System.Exception ex) when(ex is IOException||
            ex is UnauthorizedAccessException||
            ex is ArgumentException)
        {
            status="REPORT_APPEND_FAILED_"+ex.GetType().Name;
            return false;
        }
    }

    internal static Result InspectFile(string path)
    {
        try
        {
            var info=new FileInfo(path);
            if(!info.Exists)return new("REVIEW_MISSING_FILE",-1,-1,
                "PDF file not found");
            if(info.Length<1024||info.Length>32*1024*1024)
                return new("NOT_AUDITED_SIZE",-1,-1,
                    "Basic compatibility audit supports 1KB to 32MB");
            return InspectBytes(File.ReadAllBytes(path));
        }
        catch(System.Exception ex) when(ex is IOException||
            ex is UnauthorizedAccessException)
        {
            return new("REVIEW_IO_FAILURE",-1,-1,ex.GetType().Name);
        }
    }

    internal static Result InspectBytes(byte[] bytes)
    {
        if(bytes==null||bytes.Length<24||
           Encoding.ASCII.GetString(bytes,0,5)!="%PDF-")
            return new("REVIEW_HEADER",-1,-1,"Missing PDF header");
        // The LAST xref and EOF is authoritative when incremental
        // updates exist. Offset is absolute from the PDF file start.
        string text=Encoding.Latin1.GetString(bytes);
        int eof=text.LastIndexOf("%%EOF",StringComparison.Ordinal);
        if(eof<0)return new("REVIEW_NO_EOF",-1,-1,
            "PDF EOF marker missing");
        if(text[(eof+5)..].Any(c=>!char.IsWhiteSpace(c)))
            return new("REVIEW_TRAILING_DATA",-1,eof,
                "Non-whitespace data follows final EOF marker");
        int pointer=text.LastIndexOf("startxref",eof,
            StringComparison.Ordinal);
        if(pointer<0)return new("REVIEW_MISSING_STARTXREF",-1,eof,
            "No final cross-reference pointer");
        int start=pointer+"startxref".Length;
        while(start<eof&&char.IsWhiteSpace(text[start]))start++;
        int end=start;
        while(end<eof&&char.IsAsciiDigit(text[end]))end++;
        if(end==start||
           !int.TryParse(text[start..end],out int offset)||
           offset<0||offset>=bytes.Length)
            return new("REVIEW_INVALID_XREF_OFFSET",-1,eof,
                "Final startxref offset is outside the PDF");
        string head=text.Substring(offset,Math.Min(80,bytes.Length-offset));
        bool table=head.StartsWith("xref",StringComparison.Ordinal);
        bool stream=Regex.IsMatch(head,@"^\d+\s+\d+\s+obj(?:\s|<)",
            RegexOptions.None,TimeSpan.FromMilliseconds(100));
        if(!table&&!stream)
            return new("REVIEW_XREF_TARGET",offset,eof,
                "Cross-reference location does not start with xref or an object");
        return new("BASIC_STRUCTURE_PASS",offset,eof,
            (table?"xref table":"xref stream object")+
            "; external standards-compliant PDF parser/print check still required");
    }
}
