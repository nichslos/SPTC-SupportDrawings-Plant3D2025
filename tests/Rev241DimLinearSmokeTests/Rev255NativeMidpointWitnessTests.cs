using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev255NativeMidpointWitnessTests
{
    private static void Need(bool ok,string why)
    {
        if(!ok)throw new Exception("REV22.55: "+why);
    }

    internal static void Run()
    {
        // TEST5 geometry (native 8A3/8A4, RIGHT view, 1:15).
        // Both members deliberately have Start/End axis midpoints
        // OFFSET from their WCS bounding-box midpoints. This catches
        // the prior bug that anchored every local 100 mm dimension
        // to a bounding-box outside face rather than the real axis.
        var right=new SupportOrthoMemberCalloutPlan17.Camera(
            "RIGHT",338.58,588.7,86.376,42.234,
            1,0,0,714.341,1955.379,0,1.0/15);
        var beam=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:8A4","STRUCTURE",
            7760.82,250.201,1703.985,
            7810.82,1178.481,1803.985);
        var upright=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:8A3","STRUCTURE",
            7760.82,250.201,1703.985,
            7810.82,350.201,2206.772);
        var beamEnds=new SupportOrthoDimLinearPlan41.Segment(
            "NATIVE:8A4","STRUCTURE",
            7785.82,350.201,1753.985,
            7785.82,1140.481,1753.985,790.28,790.28,
            "EXPLICIT_MEMBER_ENDPOINTS");
        var uprightEnds=new SupportOrthoDimLinearPlan41.Segment(
            "NATIVE:8A3","STRUCTURE",
            7785.82,300.201,1720.985,
            7785.82,300.201,2185.772,464.787,464.787,
            "EXPLICIT_MEMBER_ENDPOINTS");
        var plan=SupportOrthoReferenceDimPlan47.Build(
            new[]{right},new[]{beam,upright},594,841,1,
            new[]{beamEnds,uprightEnds});
        var crossSectionV=plan.Dimensions.Single(d=>
            d.View=="RIGHT"&&!d.Horizontal&&
            d.Source.Contains("/MEMBER_NATIVE:8A4"));
        var crossSectionH=plan.Dimensions.Single(d=>
            d.View=="RIGHT"&&d.Horizontal&&
            d.Source.Contains("/MEMBER_NATIVE:8A3"));
        var actualBeamMid=SupportOrthoMemberCalloutPlan17.Project(
            right,
            (beamEnds.Ax+beamEnds.Bx)*.5,
            (beamEnds.Ay+beamEnds.By)*.5,
            (beamEnds.Az+beamEnds.Bz)*.5);
        var actualPostMid=SupportOrthoMemberCalloutPlan17.Project(
            right,
            (uprightEnds.Ax+uprightEnds.Bx)*.5,
            (uprightEnds.Ay+uprightEnds.By)*.5,
            (uprightEnds.Az+uprightEnds.Bz)*.5);
        Need(Math.Abs(crossSectionV.Ax-actualBeamMid.X)<1e-5&&
             Math.Abs(crossSectionV.Bx-actualBeamMid.X)<1e-5,
            "beam width witnesses must originate from native axis midpoint");
        Need(Math.Abs(crossSectionH.Ay-actualPostMid.Y)<1e-5&&
             Math.Abs(crossSectionH.By-actualPostMid.Y)<1e-5,
            "upright width witnesses must originate from native axis midpoint");
        Need(crossSectionV.Measurement=="100"&&
             crossSectionH.Measurement=="100",
            "cross-section values are still dynamically measured 100 mm");
        Need(plan.NativeMidpointMemberDimensions>=2,
            "selected native Start/End midpoint anchors were used");
        Need(plan.BoundsMidpointMemberDimensions==0,
            "verified native axes must never silently fall back to AABB faces");
        Need(plan.Dimensions.All(
            SupportOrthoDynamicDimension492.MatchesDiagnostic),
            "midpoint adjustments preserve native DIMLINEAR dynamic measurements");
        var overall=plan.Dimensions.Where(d=>
            d.Source.EndsWith("/OVERALL")).ToArray();
        Need(overall.Any(d=>d.Horizontal&&d.Measurement=="928.28")&&
             overall.Any(d=>!d.Horizontal&&d.Measurement=="502.787"),
            "the overall dimensions remain unchanged by midpoint alignment");
        // Move only the real native beam axis endpoints; the midpoint
        // anchor should move accordingly WITHOUT changing its width.
        var movedBeam=beamEnds with {
            Ay=beamEnds.Ay+40,
            By=beamEnds.By+40
        };
        var shifted=SupportOrthoReferenceDimPlan47.Build(
            new[]{right},new[]{beam,upright},594,841,1,
            new[]{movedBeam,uprightEnds});
        var shiftedLocal=shifted.Dimensions.Single(d=>
            !d.Horizontal&&d.Source.Contains("/MEMBER_NATIVE:8A4"));
        var expectedMovedMid=SupportOrthoMemberCalloutPlan17.Project(
            right,(movedBeam.Ax+movedBeam.Bx)*.5,
            (movedBeam.Ay+movedBeam.By)*.5,
            (movedBeam.Az+movedBeam.Bz)*.5);
        Need(Math.Abs(shiftedLocal.Ax-expectedMovedMid.X)<1e-5 &&
             Math.Abs(shiftedLocal.Ax-crossSectionV.Ax)>.5,
            "midpoint follows changed source geometry instead of stale placement");
        Need(shiftedLocal.Measurement=="100",
            "changing the member axis location cannot hardcode width");
        // Without explicit native endpoints, retain only a clearly
        // diagnosed projected-envelope midpoint estimate. No invented
        // member cut length, connection station or bolt pitch.
        var legacy=SupportOrthoReferenceDimPlan47.Build(
            new[]{right},new[]{beam,upright},594,841,1);
        Need(legacy.BoundsMidpointMemberDimensions>=1 &&
             legacy.NativeMidpointMemberDimensions==0,
            "legacy geometry logs approximate envelope midpoint instead");
        var beamOnly=legacy.Dimensions.Single(d=>
            !d.Horizontal&&d.Source.Contains("/MEMBER_NATIVE:8A4"));
        double midX=(SupportOrthoMemberCalloutPlan17.Project(
            right,0,beam.MinY,0).X+
            SupportOrthoMemberCalloutPlan17.Project(
            right,0,beam.MaxY,0).X)*.5;
        Need(Math.Abs(beamOnly.Ax-midX)<1e-5,
            "no native axis: fallback is member-centered, never a boundary face");
        var xref=new SupportOrthoAllViewDimPlan44.Member(
            "XREF_MEMBER:REMOTE","STRUCTURE",
            0,0,0,20000,20000,20000);
        var mixed=SupportOrthoReferenceDimPlan47.Build(
            new[]{right},new[]{beam,upright,xref},594,841,1,
            new[]{beamEnds,uprightEnds,new SupportOrthoDimLinearPlan41.Segment(
                "XREF_MEMBER:REMOTE","STRUCTURE",
                0,0,0,10,10,10,17.321,17.321,
                "EXPLICIT_MEMBER_ENDPOINTS")});
        Need(mixed.Dimensions.Select(d=>d.Measurement)
            .OrderBy(x=>x).SequenceEqual(
                plan.Dimensions.Select(d=>d.Measurement).OrderBy(x=>x)),
            "XREF members and endpoints must remain excluded");
        Console.WriteLine(
            "REV22.55 native member axis midpoint witnesses / accurate 100 widths / preserved overall and XREF exclusion PASS");
    }
}
