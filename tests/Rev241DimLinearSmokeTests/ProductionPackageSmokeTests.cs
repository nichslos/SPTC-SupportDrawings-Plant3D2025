using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SPTC.SupportDrawings;

internal static class ProductionPackageSmokeTests
{
    private static void Need(bool ok,string message)
    {
        if(!ok)throw new Exception("Production package: "+message);
    }

    internal static void Run()
    {
        string token=Guid.NewGuid().ToString("N");
        string baseDir=Path.Combine(Path.GetTempPath(),
            "SPTC_PRODUCTION_TEST_"+token);
        Directory.CreateDirectory(baseDir);
        string model=Path.Combine(baseDir,"SOURCE.dwg");
        string output=Path.Combine(baseDir,"ORTHO.dwg");
        string report=Path.Combine(baseDir,"ORTHO_RESULT.txt");
        File.WriteAllBytes(model,new byte[]{1,2,3,4});
        var settings=new SupportProductionPackagePlan.Settings(
            true,"TB26","SUP","A","PIPE SUPPORT DRAWING","SHEET_TITLE",
            new Dictionary<string,string>{
                ["DrawingNumber"]="DRAWING_NO",
                ["Revision"]="REV",
                ["Scale"]="SCALE"},
            false,true,false,false,"SUPPORT_TAG");

        var views=new[]{
            "TOP","BOTTOM","LEFT","RIGHT","FRONT","BACK",
            "NW","NE","SW","SE"};
        var handles=new[]{"8A4","8A3"};
        string log=string.Join("\n",new[]{
            "2026-11-13T05:00:00Z STEP10_SELECTED_NATIVE_VIEWS_REGISTERED=10",
            "2026-11-13T05:00:01Z REV22_61_EXPECTED_HV_OVERALLS=12",
            "2026-11-13T05:00:02Z REV22_61_PRESENT_HV_OVERALLS=12",
            "2026-11-13T05:00:03Z REV22_61_QA_OPTIONAL_OMISSIONS=2",
            "2026-11-13T05:00:04Z REV22_61_QA_WITNESS_INTERSECTIONS=0",
            "2026-11-13T05:00:05Z REV22_41_DIMENSIONS_CREATED=25",
            "2026-11-13T05:00:06Z REV22_61_QA_STATUS=PASS",
            "2026-11-13T05:00:07Z FINAL_STATUS=TEN_NATIVE_VIEWS_AND_TITLES_MEMBER_LEADERS_PASS"});
        File.WriteAllText(report,log);
        try
        {
            var read=SupportProductionPackagePlan.ParseReport(log);
            Need(read.Pass&&read.DimensionsCreated==25&&
                read.ExpectedOveralls==12&&read.PresentOveralls==12&&
                read.OptionalOmissions==2,
                "verified TEST5 25/27 and 12/12 metadata parsed");
            Need(SupportProductionPackagePlan.Fingerprint(model,handles)==
                SupportProductionPackagePlan.Fingerprint(
                    model,new[]{"8A3","8A4","8A3"}),
                "drawing identity deterministic independent of selection order");
            var entry=SupportProductionPackagePlan.BuildEntry(
                settings,model,handles,output,report,log,views,
                "1:15","TEST_SHA","SP-0001","NOT_CONFIGURED");
            Need(entry.DrawingNumber.StartsWith("TB26-SUP-SP-0001-",
                StringComparison.Ordinal)&&
                entry.SourceHandles.SequenceEqual(new[]{"8A3","8A4"})&&
                entry.QaStatus=="PASS"&&
                entry.IssueStatus=="DRAFT_NEEDS_SAVED_DWG_AND_QA"&&
                entry.PdfStatus=="PDF_REQUIRED_NOT_PUBLISHED"&&
                entry.PdfPath==null,
                "unique code, native handles, QA PASS and unissued PDF remain honest");
            var strict=SupportProductionPackagePlan.BuildEntry(
                settings with {RequireFullDimensionCoverage=true},
                model,handles,output,report,log,views,"1:15",
                "TEST_SHA");
            Need(strict.QaStatus=="REVIEW",
                "optional omitted details block strict issue release");
            var wrong=SupportProductionPackagePlan.BuildEntry(
                settings,model,handles,output,report,
                log.Replace("PRESENT_HV_OVERALLS=12",
                    "PRESENT_HV_OVERALLS=11"),
                views,"1:15","TEST_SHA");
            Need(wrong.QaStatus=="REVIEW",
                "missing essential overall dimension cannot be marked PASS");
            var x=SupportProductionPackagePlan.FailedEntry(
                settings,model,handles,"NATIVE_MODEL_PRECHECK_FAILED");
            Need(x.QaStatus=="FAILED"&&
                x.IssueStatus=="GENERATION_FAILED"&&
                x.DestinationDwg=="",
                "failures never receive fictitious drawing paths");
            string csv=SupportProductionPackagePlan.RegisterCsv(
                new[]{entry,x});
            Need(csv.Contains("DrawingNumber")&&
                csv.Contains(entry.DrawingNumber)&&
                csv.Contains("GENERATION_FAILED"),
                "single register includes pass and failed rows");
            var formula=entry with {DrawingTitle="=BAD",
                DestinationDwg="=HYPERLINK(\"bad\")"};
            Need(SupportProductionPackagePlan.RegisterCsv(
                new[]{formula}).Contains("\"'=HYPERLINK("),
                "CAD-derived CSV text cannot execute spreadsheet formulas");
            SupportProductionPackagePlan.SaveEntry(entry);
            string sidecar=Path.ChangeExtension(report,".production.json");
            Need(File.Exists(sidecar)&&
                JsonSerializer.Deserialize<SupportProductionPackagePlan.Entry>(
                    File.ReadAllText(sidecar))?.DrawingNumber==entry.DrawingNumber,
                "per-drawing JSON metadata persisted atomically");
            string register=Path.Combine(baseDir,"register.csv");
            SupportProductionPackagePlan.SaveRegister(register,new[]{entry,x});
            Need(File.Exists(register)&&
                File.ReadAllText(register).Contains("GENERATION_FAILED"),
                "run register written without touching DWG");

            var newer=entry with {SourceWriteUtcTicks=entry.SourceWriteUtcTicks+1};
            Need(SupportProductionPackagePlan.ChangeStatus(entry,newer)==
                "SOURCE_STAMP_CHANGED_REVIEW",
                "changed source stamp triggers review, not auto regeneration");
            var reconfigured=entry with {Scale="1:20"};
            Need(SupportProductionPackagePlan.ChangeStatus(entry,reconfigured)==
                "DRAWING_CONFIGURATION_CHANGED_REVIEW",
                "changed paper scale triggers review");
            Need(!SupportProductionPackagePlan.CanSkipVerifiedExisting(
                settings with {SkipVerifiedExisting=true},entry,
                model,handles,entry.SourceWriteUtcTicks,
                entry.SourceLength,views,"1:15",true,true),
                "new draft MUST NOT be skipped as a verified issue drawing");
            File.WriteAllText(output,"test-only DWG sentinel");
            Need(SupportProductionPackagePlan.CanSkipVerifiedExisting(
                settings with {SkipVerifiedExisting=true},
                entry with {IssueStatus="ISSUE_VERIFIED"},
                model,handles,entry.SourceWriteUtcTicks,
                entry.SourceLength,views,"1:15",true,true),
                "separately verified issue may be skipped only when all guards match");
            Need(!SupportProductionPackagePlan.CanSkipVerifiedExisting(
                settings with {SkipVerifiedExisting=true},
                entry with {IssueStatus="ISSUE_VERIFIED"},
                model,handles,entry.SourceWriteUtcTicks,
                entry.SourceLength,views,"1:15",true,false),
                "missing required PDF disables verified issue skip");
            var cfgFile=SupportProductionPackageSettingsStore.PathForProject(
                baseDir);
            try
            {
                SupportProductionPackageSettingsStore.Save(baseDir,settings);
                Need(SupportProductionPackageSettingsStore.TryLoad(
                    baseDir,out var restored,out var status)&&
                    restored.Enabled&&status=="CONFIGURED_ENABLED"&&
                    restored.ProjectCode=="TB26",
                    "opt-in production settings persist per project");
                File.WriteAllText(cfgFile,"invalid_json");
                Need(!SupportProductionPackageSettingsStore.TryLoad(
                    baseDir,out var disabled,out _)&&!disabled.Enabled,
                    "damaged settings fail closed and never start production package");
            }
            finally{if(File.Exists(cfgFile))File.Delete(cfgFile);}

            bool invalid=false;
            try{SupportProductionPackagePlan.Validate(
                settings with {ProjectCode="../BAD"});}
            catch(ArgumentException){invalid=true;}
            Need(invalid,"unsafe project code rejected");
            invalid=false;
            try{SupportProductionPackagePlan.Validate(
                settings with {AttributeTags=new Dictionary<string,string>{
                    ["DrawingNumber"]="REV",["Revision"]="REV"}});}
            catch(ArgumentException){invalid=true;}
            Need(invalid,"one title block field cannot overwrite another mapped attribute");
        }
        finally{Directory.Delete(baseDir,recursive:true);}
        Console.WriteLine(
            "Production numbering, QA, draft registration, source change guards, CSV injection defense, opt-in settings and safe issue skip PASS");
    }
}
