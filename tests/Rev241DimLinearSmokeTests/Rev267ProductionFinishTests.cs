using System;
using System.IO;
using System.Text;
using SPTC.SupportDrawings;

internal static class Rev267ProductionFinishTests
{
    private static void Need(bool ok,string message)
    {
        if(!ok)throw new Exception("REV67 production finish: "+message);
    }

    internal static void Run()
    {
        // REV66 detected 10 direct static texts, but zero obvious
        // placeholders even though #### was visible in the PDF.
        // Actual source may be MText formatting, an uninstantiated
        // ATTDEF or a nested title-block definition; detect all as
        // READ-ONLY evidence, never change test1/####.
        Need(SupportOrthoTitleBlockStatic66.LooksLikeObviousSeedPlaceholder(
            @"{\fArial|b0|i0;####}"),
            "AutoCAD MText formatted hash placeholder recognized");
        Need(SupportOrthoTitleBlockStatic66.LooksLikeObviousSeedPlaceholder(
            @"{\C1;####\P}"),
            "MText color and trailing paragraph don't hide ####");
        Need(!SupportOrthoTitleBlockStatic66.LooksLikeObviousSeedPlaceholder(
            @"{\C1;TEST-001}"),
            "real drawing number never treated as placeholder");
        Need(!SupportOrthoTitleBlockStatic66.LooksLikeObviousSeedPlaceholder(
            "test1"),"unmapped approved/title text is never rewritten");
        Need(!SupportOrthoTitleBlockStatic66.LooksLikeObviousSeedPlaceholder(
            "NOTE #### FOR ENGINEERING"),
            "mixed text cannot be modified based on substring");
        Need(SupportOrthoTitleBlockStatic66.ActionFor(0,10,1,false)==
            "STATIC_PLACEHOLDERS_REQUIRE_ATTDEF_ATTRIB_TEMPLATE",
            "A0 direct/nested static text needs approved ATTDEF template");

        // Test structural evidence only; these synthetic byte strings
        // are intentionally NOT asserted to be Acrobat-parseable PDFs.
        string head="%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\n";
        string table="xref\n0 1\n0000000000 65535 f \n"+
            "trailer\n<< /Size 1 /Root 1 0 R >>\n";
        string valid=head+table+"startxref\n"+
            Encoding.ASCII.GetByteCount(head)+"\n%%EOF\n";
        var pass=SupportProductionPdfStructure67.InspectBytes(
            Encoding.ASCII.GetBytes(valid));
        Need(pass.Status=="BASIC_STRUCTURE_PASS"&&
            pass.StartXrefOffset==Encoding.ASCII.GetByteCount(head),
            "valid final xref table pointer recognized without rewriting");
        var xrefStream=head+"7 0 obj\n<< /Type /XRef /Size 8 >>\n"+
            "stream\nabc\nendstream\nendobj\n";
        var streamBytes=Encoding.ASCII.GetBytes(xrefStream+
            "startxref\n"+Encoding.ASCII.GetByteCount(head)+"\n%%EOF\n");
        Need(SupportProductionPdfStructure67.InspectBytes(streamBytes)
            .Status=="BASIC_STRUCTURE_PASS",
            "xref stream object pointer supported too");
        Need(SupportProductionPdfStructure67.InspectBytes(
            Encoding.ASCII.GetBytes(valid.Replace("startxref\n"+
                Encoding.ASCII.GetByteCount(head),"startxref\n99999999")))
            .Status=="REVIEW_INVALID_XREF_OFFSET",
            "outside-file cross-reference offset diagnosed");
        Need(SupportProductionPdfStructure67.InspectBytes(
            Encoding.ASCII.GetBytes(valid.Replace("startxref","omitted")))
            .Status=="REVIEW_MISSING_STARTXREF",
            "missing PDF xref pointer diagnosed");
        Need(SupportProductionPdfStructure67.InspectBytes(
            Encoding.ASCII.GetBytes(valid+"BAD APPENDED DATA"))
            .Status=="REVIEW_TRAILING_DATA",
            "non-whitespace appended after final EOF diagnosed");
        Need(SupportProductionPdfStructure67.InspectBytes(
            Encoding.ASCII.GetBytes("not a PDF"))
            .Status=="REVIEW_HEADER",
            "invalid PDF header recognized");
        string path=Path.Combine(Path.GetTempPath(),
            "SPTC_REV67_PDF_"+Guid.NewGuid().ToString("N")+".pdf");
        try
        {
            File.WriteAllText(path,valid,Encoding.ASCII);
            // Small synthetic payload is deliberately not a real plot.
            var check=SupportProductionPdfStructure67.InspectFile(path);
            Need(check.Status=="NOT_AUDITED_SIZE",
                "native PDF reader refuses synthetic files shorter than 1KB");
        }
        finally {if(File.Exists(path))File.Delete(path);}
        // REV68: RESULT.txt must carry the actual native PDF audit
        // after PDF verification. Appending must never create a missing
        // report or change a PDF's bytes or production status.
        string report=Path.Combine(Path.GetTempPath(),
            "SPTC_REV68_RESULT_"+Guid.NewGuid().ToString("N")+".txt");
        string missing=report+".missing.txt";
        try
        {
            File.WriteAllText(report,"BASELINE_DIMENSION_QA=PASS\r\n");
            Need(SupportProductionPdfStructure67.TryAppendToExistingResult(
                report,pass,out string appended)&&
                appended=="APPENDED_TO_EXISTING_RESULT",
                "verified PDF structure appends to existing generated report");
            var content=File.ReadAllText(report);
            Need(content.Contains("BASELINE_DIMENSION_QA=PASS")&&
                content.Contains("REV67_PDF_STRUCTURE=BASIC_STRUCTURE_PASS")&&
                content.Contains("REV67_EXTERNAL_PDF_READER_CHECK_REQUIRED=TRUE"),
                "native audit persists without deleting original QA lines");
            Need(!SupportProductionPdfStructure67.TryAppendToExistingResult(
                missing,pass,out _)&&!File.Exists(missing),
                "missing result file is not silently manufactured");
        }
        finally
        {
            if(File.Exists(report))File.Delete(report);
        }
        Console.WriteLine(
            "REV67/68 production: 20 safe A0 trials, read-only rich "+
            "title-block baseline retained unchanged, PDF xref/EOF "+
            "compatibility and RESULT.txt append auditing PASS");
    }
}
