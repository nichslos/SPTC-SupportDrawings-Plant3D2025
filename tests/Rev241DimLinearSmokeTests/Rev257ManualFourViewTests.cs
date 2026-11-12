using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev257ManualFourViewTests
{
    private static void Check(bool success,string reason)
    {
        if(!success)throw new Exception("REV22.57: "+reason);
    }

    private static SupportOrthoAllViewDimPlan44.Member[] Test5() =>
        new[]{
            new SupportOrthoAllViewDimPlan44.Member(
                "NATIVE:8A4","STRUCTURE",
                7760.82,250.201,1703.985,
                7810.82,1178.481,1803.985),
            new SupportOrthoAllViewDimPlan44.Member(
                "NATIVE:8A3","STRUCTURE",
                7760.82,250.201,1703.985,
                7810.82,350.201,2206.772)
        };

    internal static void Run()
    {
        // The user supplied MANUAL.pdf and the REV22.56 generated PDF.
        // These fixture values are not used by production code.
        var top=new SupportOrthoMemberCalloutPlan17.Camera(
            "TOP",118.8,723.26,28,86.376,
            0,0,1,7785.82,714.341,0,1.0/15);
        var bottom=new SupportOrthoMemberCalloutPlan17.Camera(
            "BOTTOM",338.58,723.26,36,86.376,
            0,0,-1,7785.82,-714.341,Math.PI,1.0/15);
        var left=new SupportOrthoMemberCalloutPlan17.Camera(
            "LEFT",118.8,588.7,86.376,42.234,
            -1,0,0,-714.341,1955.379,0,1.0/15);
        var right=new SupportOrthoMemberCalloutPlan17.Camera(
            "RIGHT",338.58,588.7,86.376,42.234,
            1,0,0,714.341,1955.379,0,1.0/15);
        var cameras=new[]{top,bottom,left,right};
        var plan=SupportOrthoReferenceDimPlan47.Build(
            cameras,Test5(),594,841,1);

        foreach(var view in cameras)
        {
            var dims=plan.Dimensions.Where(d=>d.View==view.Name).ToArray();
            var horizontalOverall=dims.Single(d=>d.Horizontal&&
                d.Source.EndsWith("/OVERALL"));
            var verticalOverall=dims.Single(d=>!d.Horizontal&&
                d.Source.EndsWith("/OVERALL"));
            Check(dims.Where(d=>d.Horizontal&&
                !d.Source.EndsWith("/OVERALL"))
                    .All(d=>d.LineY<horizontalOverall.LineY-.54),
                view.Name+" horizontal overall must be OUTERMOST");
            Check(dims.Where(d=>!d.Horizontal&&
                !d.Source.EndsWith("/OVERALL"))
                    .All(d=>d.LineX>verticalOverall.LineX+.54),
                view.Name+" vertical overall must be OUTERMOST");
            Check(dims.All(SupportOrthoDynamicDimension492
                .MatchesDiagnostic),
                view.Name+" dimensions remain dynamic, never text override");
        }
        foreach(string view in new[]{"TOP","BOTTOM"})
        {
            var dims=plan.Dimensions.Where(d=>d.View==view).ToArray();
            Check(!dims.Any(d=>!d.Horizontal&&
                d.Source.Contains("/CHAIN_")&&d.Measurement=="50"),
                view+" duplicate vertical 50 mm centerline chain suppressed");
            Check(dims.Any(d=>d.Horizontal&&
                d.Measurement=="50"),
                view+" real 50 mm transverse native profile preserved");
            Check(dims.Any(d=>!d.Horizontal&&
                d.Measurement=="928.28"),
                view+" true native overall 928.28 retained");
        }
        foreach(string view in new[]{"LEFT","RIGHT"})
        {
            var dims=plan.Dimensions.Where(d=>d.View==view).ToArray();
            Check(!dims.Any(d=>d.Horizontal&&
                d.Source.Contains("/CHAIN_")&&d.Measurement=="50"),
                view+" redundant horizontal 50 mm centerline chain suppressed");
            Check(dims.Any(d=>d.Horizontal&&d.Measurement=="928.28"),
                view+" original overall 928.28 retained");
            Check(dims.Any(d=>!d.Horizontal&&d.Measurement=="502.787"),
                view+" native overall height retained");
            Check(dims.Any(d=>!d.Horizontal&&
                d.Source.Contains("/MEMBER_")&&d.Measurement=="100"),
                view+" physical end section 100 remains");
        }
        Check(plan.Dimensions.Any(d=>d.View=="LEFT"&&
            !d.Horizontal&&d.Source.Contains(
                "/TERMINAL_REMAINDER_")&&d.Measurement=="402.787"),
            "LEFT view contains 402.787 intermediate height");
        Check(!plan.Dimensions.Any(d=>d.View=="RIGHT"&&
            !d.Horizontal&&d.Source.Contains(
                "/TERMINAL_REMAINDER_")),
            "RIGHT view omits redundant 402.787 intermediate height");
        Check(plan.PriorityOverallDimensions==8,
            "all four native views reserve both axis overalls");

        Check(SupportOrthoManualDetailStyle57
            .ObscuredByTerminalMember(0,50,0,928.28,new[]{
                new SupportOrthoReferenceDatum56.Span(
                    "NATIVE:END",0,100)
            }),
            "50 chain inside real 100 terminal section is redundant");
        Check(!SupportOrthoManualDetailStyle57
            .ObscuredByTerminalMember(100,150,0,928.28,new[]{
                new SupportOrthoReferenceDatum56.Span(
                    "NATIVE:END",0,100)
            }),
            "50 interval outside terminal section is not suppressed");
        Check(!SupportOrthoManualDetailStyle57
            .ObscuredByTerminalMember(0,100,0,928.28,new[]{
                new SupportOrthoReferenceDatum56.Span(
                    "NATIVE:END",0,100)
            }),
            "real native 100 section width is never suppressed");
        Check(!SupportOrthoManualDetailStyle57
            .ObscuredByTerminalMember(0,50,0,928.28,new[]{
                new SupportOrthoReferenceDatum56.Span(
                    "XREF_MEMBER:EXT",0,100)
            }),
            "XREF end profile cannot suppress native chain measurements");

        var external=new SupportOrthoAllViewDimPlan44.Member(
            "XREF_MEMBER:REMOTE","STRUCTURE",
            0,0,0,20000,20000,20000);
        var mixed=SupportOrthoReferenceDimPlan47.Build(
            cameras,Test5().Append(external).ToArray(),594,841,1);
        Check(mixed.Dimensions.Select(d=>d.Measurement)
            .OrderBy(x=>x).SequenceEqual(plan.Dimensions
                .Select(d=>d.Measurement).OrderBy(x=>x)),
            "native-only dimension values unchanged by XREF structure");
        Console.WriteLine(
            "REV22.57 manual TOP/BOTTOM/LEFT/RIGHT selection, outermost overall and no duplicate 50 mm chain PASS");
    }
}
