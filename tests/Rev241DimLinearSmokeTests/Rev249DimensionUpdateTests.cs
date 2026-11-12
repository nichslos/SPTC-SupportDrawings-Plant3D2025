using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev249DimensionUpdateTests
{
    internal static void Run()
    {
        var native=new SupportOrthoDimensionUpdatePlan49.NativeMember(
            "NATIVE:521","STRUCTURE");
        var target=new SupportOrthoDimensionUpdatePlan49.View(
            "TOP","2AC",118.8,723.26,63.732,48.134);
        var manifest=new SupportOrthoDimensionUpdatePlan49.Manifest(
            SupportOrthoDimensionUpdatePlan49.ManifestVersion,
            @"C:\PlantProject\Drawings\MODEL.dwg","Layout1",1,594,841,
            true,new[]{native},new[]{target},"");
        SupportOrthoDimensionUpdatePlan49.Validate(manifest);
        bool rejected=false;
        try {SupportOrthoDimensionUpdatePlan49.Validate(
            manifest with {Members=new[]{native with {
                Key="XREF_MEMBER:SUPP-1/401"}}});}
        catch(ArgumentException){rejected=true;}
        Check(rejected,"XREF source cannot enter persistent auto-update manifest");
        rejected=false;
        try {SupportOrthoDimensionUpdatePlan49.Validate(
            manifest with {Members=new[]{native,native}});}
        catch(ArgumentException){rejected=true;}
        Check(rejected,"duplicate native reference cannot be stored");
        var candidates=new[]{
            new SupportOrthoDimensionUpdatePlan49.Candidate(
                "2AC",118.8,723.26,63.732,48.134),
            new SupportOrthoDimensionUpdatePlan49.Candidate(
                "300",338.58,723.26,63.732,48.134)};
        var used=new System.Collections.Generic.HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var exact=SupportOrthoDimensionUpdatePlan49.Resolve(
            target,candidates,used);
        Check(exact?.Handle=="2AC","exact viewport handle is preferred");
        var replacement=new[]{
            new SupportOrthoDimensionUpdatePlan49.Candidate(
                "900",118.9,723.28,64.0,48.1)};
        var near=SupportOrthoDimensionUpdatePlan49.Resolve(
            target,replacement,used);
        Check(near?.Handle=="900",
            "native Update View can regenerate viewport ID with same frame");
        var ambiguous=new[]{
            replacement[0],
            replacement[0] with {Handle="901",X=119.0}};
        Check(SupportOrthoDimensionUpdatePlan49.Resolve(
            target,ambiguous,used)==null,
            "ambiguous replacement viewport aborts rather than using Current View");
        Check(SupportOrthoDimensionUpdatePlan49.Resolve(
            target,new[]{replacement[0]},new System.Collections.Generic.HashSet<string>(
                StringComparer.OrdinalIgnoreCase){"900"})==null,
            "a physical viewport cannot be matched to multiple logical views");

        var d=new SupportOrthoDimLinearPlan41.Dimension(
            "TOP","DETAIL:TOP/H/OVERALL","STRUCTURE",true,
            20,30,90,30,55,100,"1400",
            "REFERENCE_PROJECTED_OVERALL_NOT_CUT_LENGTH",55,102.5);
        var e=d with {View="FRONT",Source="DETAIL:FRONT/H/OVERALL"};
        var s1=SupportOrthoDimensionUpdatePlan49.Signature(new[]{d,e});
        var s2=SupportOrthoDimensionUpdatePlan49.Signature(new[]{e,d});
        Check(s1==s2,"unchanged plan is order-invariant");
        Check(!SupportOrthoDimensionUpdatePlan49.NeedsReplace(s1,s2,2,2),
            "repeat update does not create duplicate dimensions");
        Check(SupportOrthoDimensionUpdatePlan49.NeedsReplace(s1,s2,1,2),
            "one deleted SPTC-owned dimension triggers repair");
        Check(SupportOrthoDimensionUpdatePlan49.NeedsReplace(
            s1,SupportOrthoDimensionUpdatePlan49.Signature(
                new[]{d with {Measurement="1500"},e}),2,2),
            "model dimension change triggers atomic replacement");
        Check(!SupportOrthoNativeDimensionPolicy48.IsNativeKey(
            "XREF_MEMBER:SUPP-1/401"),
            "native-only rule retained for every auto-update");
        Console.WriteLine(
            "REV22.49 manifest, native-only update, view identity and duplicate suppression PASS");
    }

    private static void Check(bool yes,string message)
    {
        if(!yes)throw new Exception("REV22.49: "+message);
    }
}
