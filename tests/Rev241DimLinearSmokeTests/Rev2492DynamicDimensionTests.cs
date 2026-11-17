using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev2492DynamicDimensionTests
{
    internal static void Run()
    {
        // Dimensions visible in user's 2026-11-12 PDF must never be
        // embedded as immutable text overrides; they are native measured
        // projections through actual DWG unit and viewport scale.
        var top=new SupportOrthoMemberCalloutPlan17.Camera(
            "TOP",180,650,150,160,0,0,1,0,0,0,1.0/20.0);
        double f=SupportOrthoDynamicDimension492
            .ModelMillimetersPerPaperUnit(1,top.Scale);
        Need(Math.Abs(f-20)<1e-10,
            "paper-to-mm factor derived from native viewport 1:20");
        var d=new SupportOrthoDimLinearPlan41.Dimension(
            "TOP","NATIVE:821","STRUCTURE",true,
            10,30,10+928.28/20.0,30,20,50,"928.28",
            "REFERENCE_PROJECTED_OVERALL_NOT_CUT_LENGTH",
            20,52.5,f);
        Need(Math.Abs(SupportOrthoDynamicDimension492
            .MeasuredMillimeters(d)-928.28)<1e-7,
            "PDF 928.28 comes from actual paper witness measurement");
        Need(SupportOrthoDynamicDimension492.MatchesDiagnostic(d),
            "PDF numeric diagnostic agrees with projected geometry");
        var edited=d with {Bx=d.Bx+5,Measurement="1028.28"};
        Need(Math.Abs(SupportOrthoDynamicDimension492
            .MeasuredMillimeters(edited)-1028.28)<1e-7 &&
            SupportOrthoDynamicDimension492.MatchesDiagnostic(edited),
            "moving a witness 5 paper mm changes native measure by 100 mm");
        var stale=d with {Measurement="828.28"};
        Need(!SupportOrthoDynamicDimension492.MatchesDiagnostic(stale),
            "stale 828.28 label for 928.28 witnesses must never be written");
        var shortened=d with {Bx=d.Bx-5,Measurement="828.28"};
        Need(SupportOrthoDynamicDimension492.MatchesDiagnostic(shortened),
            "real geometry rather than hard-coded text controls changed value");
        bool invalid=false;
        try {SupportOrthoDynamicDimension492
            .ModelMillimetersPerPaperUnit(1,0);}
        catch(ArgumentOutOfRangeException){invalid=true;}
        Need(invalid,"degenerate saved viewport scale rejected");

        var native=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:STRUCTURE","STRUCTURE",-100,-25,0,100,25,100);
        var plan44=SupportOrthoAllViewDimPlan44.Build(
            new[]{top},new[]{native},594,841,1);
        Need(plan44.Dimensions.Count==2 &&
            plan44.Dimensions.All(v=>v.PaperToModelMm.HasValue &&
                SupportOrthoDynamicDimension492.MatchesDiagnostic(v)),
            "old all-view dimension plan also uses scale-dependent numbers");
        var plan46=SupportOrthoSmartDimPlan46.Build(
            new[]{top},new[]{native},594,841,1);
        Need(plan46.Dimensions.Count==2 &&
            plan46.Dimensions.All(v=>v.PaperToModelMm.HasValue &&
                SupportOrthoDynamicDimension492.MatchesDiagnostic(v)),
            "smart collision layout also carries native paper conversion");
        var plan47=SupportOrthoReferenceDimPlan47.Build(
            new[]{top},new[]{native},594,841,1);
        Need(plan47.Dimensions.Count==2 &&
            plan47.Dimensions.All(v=>v.PaperToModelMm.HasValue &&
                SupportOrthoDynamicDimension492.MatchesDiagnostic(v)),
            "reference chain and overall dimensions never force static text");

        var noXref=native with {Key="XREF_MEMBER:SUPP-2/401"};
        Need(SupportOrthoReferenceDimPlan47.Build(
            new[]{top},new[]{noXref},594,841,1).Dimensions.Count==0,
            "no XREF geometry may become a dynamic native dimension");
        var sig1=SupportOrthoDimensionUpdatePlan49.Signature(new[]{d});
        var sig2=SupportOrthoDimensionUpdatePlan49.Signature(
            new[]{d with {PaperToModelMm=f+1}});
        Need(sig1!=sig2,"a changed native view scale invalidates cached dimension output");
        Console.WriteLine(
            "REV22.49.2 native AutoCAD-measured DIMLFAC, no hard-coded text and XREF exclusion PASS");
    }

    private static void Need(bool value,string message)
    {
        if(!value)throw new Exception("REV22.49.2: "+message);
    }
}
