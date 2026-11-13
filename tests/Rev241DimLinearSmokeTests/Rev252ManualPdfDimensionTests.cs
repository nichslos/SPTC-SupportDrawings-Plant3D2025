using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev252ManualPdfDimensionTests
{
    private static void Need(bool yes,string why)
    {
        if(!yes)throw new Exception("REV22.52: "+why);
    }

    internal static void Run()
    {
        // Synthetic native support based on the manually arranged PDF.
        // These are TEST fixture measurements, not production constants.
        // The 100 mm measurement comes from a real selected native
        // member's projected cross section, not a fabricated chain datum.
        var top=new SupportOrthoMemberCalloutPlan17.Camera(
            "TOP",180,650,150,160,0,0,1,
            464.14,251.3935,0,1.0/20.0);
        var beam=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:8A4","STRUCTURE",0,0,0,928.28,50,50);
        var crossMember=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:8A3","STRUCTURE",0,50,0,100,502.787,50);
        var plan=SupportOrthoReferenceDimPlan47.Build(
            new[]{top},new[]{beam,crossMember},594,841,1);
        Need(plan.Dimensions.Any(d=>d.Horizontal&&
            d.Measurement=="928.28"&&d.Source.EndsWith("/OVERALL")),
            "full projected assembly length remains a single overall");
        Need(plan.ChainDimensions==0,
            "touching member AABBs cannot create imaginary connection chain");
        Need(plan.Dimensions.Any(d=>d.Horizontal&&
            d.Measurement=="100"&&
            d.Source.Contains("/MEMBER_NATIV",
                StringComparison.OrdinalIgnoreCase)),
            "100 mm cross-section is placed from one actual native member");
        Need(plan.LocalMemberDimensions>=1,
            "reference layout includes native member profile dimensions");
        Need(plan.Dimensions.All(d=>
            SupportOrthoDynamicDimension492.MatchesDiagnostic(d)),
            "all overall/local dimension text measured from real paper points");

        // Ensure no values are hard-coded: changing only the member
        // geometry must make the local dimension change accordingly.
        var wider=crossMember with {MaxX=125};
        var changed=SupportOrthoReferenceDimPlan47.Build(
            new[]{top},new[]{beam,wider},594,841,1);
        Need(changed.Dimensions.Any(d=>d.Horizontal&&
            d.Measurement=="125"&&d.Source.Contains("/MEMBER_")),
            "native 125 mm member produces 125 instead of fixed 100");
        Need(!changed.Dimensions.Any(d=>d.Horizontal&&
            d.Measurement=="100"&&d.Source.Contains("/MEMBER_")),
            "previous member width never persists after geometry changes");

        // RIGHT VIEW: allow a measured vertical overall alongside its
        // two verified member/chain dimensions by searching alternate
        // paper-space lanes and dimension text tiers.
        var right=top with {Name="RIGHT",X=338.58,Y=588.7,
            W=86.376,H=44.0,Scale=1.0/15.0,
            ViewCenterX=464.14,ViewCenterY=251.3935};
        var rightPlan=SupportOrthoReferenceDimPlan47.Build(
            new[]{right},new[]{beam,crossMember},594,841,1);
        Need(rightPlan.Dimensions.Any(d=>!d.Horizontal&&
            d.Source.EndsWith("/OVERALL")),
            "RIGHT overall vertical has a usable left-side dimension lane");
        Need(rightPlan.Dimensions.Where(d=>!d.Horizontal)
            .All(d=>d.LineX<Math.Min(d.Ax,d.Bx)),
            "RIGHT vertical dimension lines stay left of native members");

        // XREF geometry must not affect overall dimension, chain or
        // the new local 100/125 mm member-section measurement.
        var xref=new SupportOrthoAllViewDimPlan44.Member(
            "XREF_MEMBER:MODEL/401","STRUCTURE",0,0,0,
            5000,5000,100);
        var mixed=SupportOrthoReferenceDimPlan47.Build(
            new[]{top},new[]{beam,crossMember,xref},594,841,1);
        Need(mixed.Dimensions.Select(d=>d.Measurement)
            .OrderBy(x=>x).SequenceEqual(
                plan.Dimensions.Select(d=>d.Measurement).OrderBy(x=>x)),
            "external XREF cannot inject any local or overall dimensions");
        Need(mixed.Skipped.Any(s=>s.Contains("XREF_OR_NONNATIVE")),
            "XREF exclusion remains auditable");

        // 50 mm profile measurement is a source-projected width, never
        // an unverified bolt pitch or chain station.
        Need(plan.Dimensions.Where(d=>d.Source.Contains("/MEMBER_"))
            .All(d=>d.Provenance==
                "REFERENCE_PROJECTED_MEMBER_ENVELOPE_NOT_CUT_LENGTH"),
            "projected section dimensions are explicitly not cut lengths");
        Console.WriteLine(
            "REV22.52 manual PDF local 100 mm, true 50 mm, RIGHT overall and native-only dimensions PASS");
    }
}
