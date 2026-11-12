using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev253OverallPriorityTests
{
    private static void Check(bool yes,string reason)
    {
        if(!yes)throw new Exception("REV22.53: "+reason);
    }

    internal static void Run()
    {
        // Model and native RIGHT viewport approximate the actual
        // TEST5/8A3+8A4 1:15 support drawing. These numbers are test
        // fixture geometry ONLY and are never used by production code.
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
        var actual=SupportOrthoReferenceDimPlan47.Build(
            new[]{right},new[]{beam,upright},594,841,1);
        var dimensions=actual.Dimensions;
        var hOverall=dimensions.Single(d=>d.Horizontal&&
            d.Source.EndsWith("/OVERALL"));
        var vOverall=dimensions.Single(d=>!d.Horizontal&&
            d.Source.EndsWith("/OVERALL"));
        Check(hOverall.Measurement=="928.28",
            "RIGHT horizontal overall measured from 928.28 WCS extent");
        Check(vOverall.Measurement=="502.787",
            "RIGHT 502.787 vertical overall is no longer skipped");
        Check(actual.PriorityOverallDimensions==2,
            "both overall axes are reserved before any optional local widths");
        Check(!actual.Skipped.Any(x=>
            x.Contains("OVERALL_NO_CLEAR_DIMENSION_LANE")),
            "tight RIGHT viewport has no missing overall dimension");
        Check(vOverall.LineX<vOverall.Ax&&
            vOverall.LineX<vOverall.Bx,
            "RIGHT vertical overall outside-left of native member");
        Check(hOverall.LineY>hOverall.Ay&&
            hOverall.LineY>hOverall.By,
            "RIGHT horizontal overall above the support");
        Check(dimensions.Count(d=>d.Source.EndsWith("/OVERALL"))==2,
            "one independent overall per real H and V axis");
        Check(actual.LocalMemberDimensions>=1 &&
            dimensions.Any(d=>d.Source.Contains("/MEMBER_")&&
                d.Measurement=="100"),
            "real 100 mm member section remains available as optional detail");
        Check(dimensions.All(
            SupportOrthoDynamicDimension492.MatchesDiagnostic),
            "no hardcoded value override; native paper witness + scale match");

        var changed=SupportOrthoReferenceDimPlan47.Build(
            new[]{right},new[]{beam,upright with {MaxZ=2256.772}},
            594,841,1);
        Check(changed.Dimensions.Any(d=>!d.Horizontal&&
            d.Source.EndsWith("/OVERALL")&&
            d.Measurement=="552.787"),
            "changing native structure height updates measured overall");
        Check(!changed.Dimensions.Any(d=>!d.Horizontal&&
            d.Source.EndsWith("/OVERALL")&&
            d.Measurement=="502.787"),
            "stale 502.787 literal cannot survive a geometry modification");

        var xref=new SupportOrthoAllViewDimPlan44.Member(
            "XREF_MEMBER:TEST_REMOTE","STRUCTURE",
            100,100,100,20000,20000,20000);
        var mixed=SupportOrthoReferenceDimPlan47.Build(
            new[]{right},new[]{beam,upright,xref},594,841,1);
        Check(mixed.Dimensions.Select(d=>d.Measurement)
            .OrderBy(x=>x).SequenceEqual(
                dimensions.Select(d=>d.Measurement).OrderBy(x=>x)),
            "XREF exclusion covers reserved overall and native local dimensions");
        Check(mixed.Skipped.Any(x=>x.Contains("XREF_OR_NONNATIVE")),
            "XREF exclusion recorded explicitly");
        Console.WriteLine(
            "REV22.53 RIGHT 502.787/928.28 overall-first + local 100 + native-only dynamic regression PASS");
    }
}
