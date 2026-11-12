using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev256ManualTerminalDatumTests
{
    private static void Need(bool yes,string why)
    {
        if(!yes)throw new Exception("REV22.56: "+why);
    }

    private static SupportOrthoReferenceDatum56.Span S(
        string id,double start,double end) =>
        new("NATIVE:"+id,start,end);

    internal static void Run()
    {
        // Reference MANUAL.pdf, TEST5/8A3+8A4: a full projected
        // member and a local member section touching one end.
        // All numerical lengths here are TEST FIXTURE geometry only.
        var length=SupportOrthoReferenceDatum56.Choose(
            new[]{S("LONG",0,928.28),S("POST",0,100)},0,928.28);
        Need(length.HasValue&&
            Math.Abs(length.Value.LocalWidth-100)<1e-8&&
            Math.Abs(length.Value.RemainingLength-828.28)<1e-8,
            "manual overall 928.28 resolves as native terminal 100 plus 828.28 remainder");

        var height=SupportOrthoReferenceDatum56.Choose(
            new[]{S("FULL_HEIGHT",0,502.787),
                S("HORIZONTAL_MEMBER",0,100)},
            0,502.787);
        Need(height.HasValue&&
            Math.Abs(height.Value.RemainingLength-402.787)<1e-8,
            "manual 502.787 uses 402.787 + true member-section 100, not 452.787 + 50");

        var reversed=SupportOrthoReferenceDatum56.Choose(
            new[]{S("LONG",0,928.28),
                S("POST",828.28,928.28)},0,928.28);
        Need(reversed.HasValue&&!reversed.Value.AtStart&&
            Math.Abs(reversed.Value.RemainingLength-828.28)<1e-8,
            "reversed orthographic view has its end section on the other side");

        // Neither literal model 50 nor 100 is a rule.
        var changed=SupportOrthoReferenceDatum56.Choose(
            new[]{S("LONG",0,928.28),S("POST",0,125)},0,928.28);
        Need(changed.HasValue&&
            Math.Abs(changed.Value.RemainingLength-803.28)<1e-8&&
            Math.Abs(changed.Value.LocalWidth-125)<1e-8,
            "changing native geometry produces 125 + 803.28 automatically");
        Need(!SupportOrthoReferenceDatum56.Choose(new[]{
            S("ONE",0,712),S("TWO",712,822)},0,822).HasValue,
            "two adjoining independent beams do not form a fictional L-profile datum");
        Need(!SupportOrthoReferenceDatum56.Choose(new[]{
            S("FULL",0,822),new SupportOrthoReferenceDatum56.Span(
                "XREF_MEMBER:REMOTE",0,110)},0,822).HasValue,
            "a XREF profile cannot authorize new native dimension datums");
        Need(!SupportOrthoReferenceDatum56.Choose(new[]{
            S("FULL",0,928.28),S("A",0,100),
            S("B",828.28,928.28)},0,928.28).HasValue,
            "two equally sized opposite-end members are not resolved by guessing");

        // Native RIGHT view from the original TEST5 geometry.
        var right=new SupportOrthoMemberCalloutPlan17.Camera(
            "RIGHT",338.58,588.7,86.376,42.234,
            1,0,0,714.341,1955.379,0,1.0/15.0);
        var beam=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:8A4","STRUCTURE",
            7760.82,250.201,1703.985,
            7810.82,1178.481,1803.985);
        var upright=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:8A3","STRUCTURE",
            7760.82,250.201,1703.985,
            7810.82,350.201,2206.772);
        var rightPlan=SupportOrthoReferenceDimPlan47.Build(
            new[]{right},new[]{beam,upright},594,841,1);
        Need(rightPlan.Dimensions.Any(d=>d.View=="RIGHT"&&
            d.Horizontal&&d.Source.Contains(
                "/TERMINAL_REMAINDER_")&&d.Measurement=="828.28"),
            "RIGHT 828.28 uses the native terminal member's projected end face");
        Need(!rightPlan.Dimensions.Any(d=>
            d.Source.Contains("/CHAIN_")&&
            (d.Measurement=="50"||d.Measurement=="452.787")),
            "redundant 50 mm short chain is absent from manual-style RIGHT");
        Need(rightPlan.Dimensions.Any(d=>d.Source.EndsWith("/OVERALL")&&
            !d.Horizontal&&d.Measurement=="502.787"),
            "RIGHT measured vertical overall remains present");
        Need(rightPlan.Dimensions.Any(d=>d.Source.Contains("/MEMBER_")&&
            d.Horizontal&&d.Measurement=="100"),
            "RIGHT physical terminal member profile is still dimensioned");
        Need(!rightPlan.Dimensions.Any(d=>!d.Horizontal&&
            d.Source.Contains("/TERMINAL_REMAINDER_")),
            "manual RIGHT omits optional vertical remainder but preserves overall and 100 mm");
        Need(rightPlan.Dimensions.All(
            SupportOrthoDynamicDimension492.MatchesDiagnostic),
            "all dimensions retain dynamically measured source and paper-scale math");

        // LEFT projection reverses the same Y-based assembly.
        var left=right with {
            Name="LEFT",X=118.8,DirX=-1,
            ViewCenterX=-714.341
        };
        var leftPlan=SupportOrthoReferenceDimPlan47.Build(
            new[]{left},new[]{beam,upright},594,841,1);
        Need(leftPlan.Dimensions.Any(d=>
            d.Source.Contains("/TERMINAL_REMAINDER_")&&
            d.Horizontal&&d.Measurement=="828.28"),
            "LEFT also uses 100+828.28 native terminal profile hierarchy");
        Need(leftPlan.Dimensions.Any(d=>
            d.Source.Contains("/TERMINAL_REMAINDER_")&&
            !d.Horizontal&&d.Measurement=="402.787"),
            "LEFT vertical reference uses 100+402.787 rather than 50+452.787");
        Need(leftPlan.Dimensions.All(
            SupportOrthoDynamicDimension492.MatchesDiagnostic),
            "LEFT dynamic measurements remain valid after layout matching");

        // A different member size changes all relevant dimensions,
        // proving the plugin never writes PDF example lengths.
        var widened=upright with {MaxY=375.201};
        var modified=SupportOrthoReferenceDimPlan47.Build(
            new[]{right},new[]{beam,widened},594,841,1);
        Need(modified.Dimensions.Any(d=>d.Horizontal&&
            d.Source.Contains("/TERMINAL_REMAINDER_")&&
            d.Measurement=="803.28"),
            "changing terminal profile to 125 changes remainder to 803.28");
        Need(modified.Dimensions.All(
            SupportOrthoDynamicDimension492.MatchesDiagnostic),
            "changed member size is dynamically measured");

        var xref=new SupportOrthoAllViewDimPlan44.Member(
            "XREF_MEMBER:REMOTE","STRUCTURE",
            0,0,0,20000,20000,20000);
        var mixed=SupportOrthoReferenceDimPlan47.Build(
            new[]{right},new[]{beam,upright,xref},594,841,1);
        Need(mixed.Dimensions.Select(d=>d.Measurement)
            .OrderBy(x=>x).SequenceEqual(rightPlan.Dimensions
                .Select(d=>d.Measurement).OrderBy(x=>x)),
            "XREF geometry cannot change the native-only reference dimensions");

        Console.WriteLine(
            "REV22.56 manual PDF end datum 100+828.28, 100+402.787, orientation and no 50mm ghost chain PASS");
    }
}
