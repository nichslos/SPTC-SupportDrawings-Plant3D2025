using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev2494MemberWitnessTests
{
    internal static void Run()
    {
        // User's 2026-11-12 drawing: 928.28 mm overall,
        // 828.28 mm preceding member, and short 50 mm stations.
        // The long beam ends low but the upright reaches much higher.
        // A dimension placed ABOVE the assembly must extend from the
        // ACTUAL source beam top, not from the upright's global top.
        var camera=new SupportOrthoMemberCalloutPlan17.Camera(
            "TOP",180,650,150,160,0,0,1,
            464.14,251.395,0,1.0/20.0);
        var beam=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:BEAM","STRUCTURE",
            0,0,0,828.28,50,20);
        var post1=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:POST_A","STRUCTURE",
            828.28,0,0,878.28,502.79,20);
        var post2=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:POST_B","STRUCTURE",
            878.28,0,0,928.28,502.79,20);
        // Independent native Start/End evidence is required. The source
        // member envelopes alone are not proof of 50 mm connections.
        var verified=new[]{
            new SupportOrthoDimLinearPlan41.Segment(
                "NATIVE:BEAM","STRUCTURE",
                0,50,0,828.28,50,0,828.28,828.28,
                "EXPLICIT_MEMBER_ENDPOINTS"),
            new SupportOrthoDimLinearPlan41.Segment(
                "NATIVE:POST_A","STRUCTURE",
                828.28,502.79,0,878.28,502.79,0,50,50,
                "EXPLICIT_MEMBER_ENDPOINTS"),
            new SupportOrthoDimLinearPlan41.Segment(
                "NATIVE:POST_B","STRUCTURE",
                878.28,502.79,0,928.28,502.79,0,50,50,
                "EXPLICIT_MEMBER_ENDPOINTS")
        };
        var actual=SupportOrthoReferenceDimPlan47.Build(
            new[]{camera},new[]{beam,post1,post2},594,841,1,
            verified);
        var horizontal=actual.Dimensions.Where(d=>d.Horizontal)
            .ToArray();
        Need(horizontal.Any(d=>d.Measurement=="928.28"),
            "overall length remains 928.28 from actual native geometry");
        Need(horizontal.Any(d=>d.Measurement=="828.28"),
            "long beam chain remains 828.28 from native geometry");
        var fifty=horizontal.Where(d=>d.Measurement=="50")
            .OrderBy(d=>d.Ax).ToArray();
        Need(fifty.Length==2,
            "two truly verified 50 mm native endpoint intervals are preserved");
        var beamTip=SupportOrthoMemberCalloutPlan17.Project(
            camera,0,50,20);
        var highPost=SupportOrthoMemberCalloutPlan17.Project(
            camera,928.28,502.79,20);
        var overall=horizontal.Single(d=>d.Source.EndsWith("/OVERALL"));
        Need(Math.Abs(overall.Ay-beamTip.Y)<.001,
            "overall LEFT witness extends to beam end face (not global upright top)");
        Need(Math.Abs(overall.By-highPost.Y)<.001,
            "overall RIGHT witness extends to upright native end face");
        var chain=horizontal.Single(d=>d.Measurement=="828.28");
        Need(Math.Abs(chain.Ay-beamTip.Y)<.001 &&
            Math.Abs(chain.By-highPost.Y)<.001,
            "inner 828.28 chain witnesses start at their actual source members");
        Need(fifty.All(d=>d.PaperTextX.HasValue &&
            (d.PaperTextX.Value<d.Ax || d.PaperTextX.Value>d.Bx)),
            "short 50 mm text sits outside its arrows/witnesses");
        Need(Math.Abs(fifty[0].PaperTextX!.Value-
            fifty[1].PaperTextX!.Value)>7,
            "two adjacent 50 mm labels are separated in paper-space");
        Need(actual.Dimensions.All(d=>
            SupportOrthoDynamicDimension492.MatchesDiagnostic(d)),
            "witness corrections preserve dynamically measured mm lengths");

        // Unverified local chains must be suppressed even when geometry
        // rectangles touch at exactly 50 mm. Their overall is still
        // measured, but no 50 mm station is fabricated.
        var boundsOnly=SupportOrthoReferenceDimPlan47.Build(
            new[]{camera},new[]{beam,post1,post2},594,841,1);
        Need(boundsOnly.ChainDimensions==0 &&
            !boundsOnly.Dimensions.Any(d=>d.Measurement=="50"&&
                d.Source.Contains("/CHAIN_",StringComparison.Ordinal)),
            "AABB contacts cannot fabricate a 50 mm connection chain");
        // REV22.52 allows a *different* drafting fact: the measurable
        // projected 50 mm outline of a selected native member. This
        // explicitly is NOT a 50 mm connection/cut length.
        Need(boundsOnly.Dimensions.Where(d=>d.Measurement=="50")
            .All(d=>d.Provenance==
                "REFERENCE_PROJECTED_MEMBER_ENVELOPE_NOT_CUT_LENGTH"||
                d.Source.EndsWith("/OVERALL",StringComparison.Ordinal)),
            "AABB-only 50 mm may describe member width/overall, never pitch");

        // A single 100 mm post is NOT evidence for a fictitious
        // mid-point splitting it into two 50 mm dimensions.
        var wholePost=post1 with {
            Key="NATIVE:POST_SINGLE",MaxX=928.28};
        var noMidpoint=SupportOrthoReferenceDimPlan47.Build(
            new[]{camera},new[]{beam,wholePost},594,841,1);
        Need(noMidpoint.Dimensions.Where(d=>d.Horizontal)
            .All(d=>d.Measurement!="50"),
            "never synthesize a 50 mm partition without physical member datum");

        // All-member H/V fallback must also pick the nearest real face:
        // for a dimension above the member, the witness originates at T,
        // not the opposite B face.
        var smart=SupportOrthoSmartDimPlan46.Build(
            new[]{camera},new[]{beam},594,841,1);
        var hb=smart.Dimensions.Single(d=>d.Horizontal);
        var vb=smart.Dimensions.Single(d=>!d.Horizontal);
        var boundsTop=SupportOrthoMemberCalloutPlan17.Project(
            camera,0,50,0).Y;
        var boundsBottom=SupportOrthoMemberCalloutPlan17.Project(
            camera,0,0,0).Y;
        Need(Math.Abs(hb.Ay-hb.By)<1e-7,
            "smart H witness endpoints share the selected actual member face");
        Need(Math.Abs(hb.Ay-boundsTop)<1e-7 ||
            Math.Abs(hb.Ay-boundsBottom)<1e-7,
            "smart H witnesses use native member top/bottom boundary");
        Need(Math.Abs(vb.Ax-vb.Bx)<1e-7,
            "smart V witnesses use one real native left/right member edge");
        Console.WriteLine(
            "REV22.49.4 native face endpoints and 50 mm chain text regression PASS");
    }
    private static void Need(bool success,string reason)
    {
        if(!success)throw new Exception("REV22.49.4: "+reason);
    }
}
