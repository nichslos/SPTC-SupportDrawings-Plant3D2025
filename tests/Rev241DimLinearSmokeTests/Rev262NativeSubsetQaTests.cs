using System;
using System.Linq;
using SPTC.SupportDrawings;

/// <summary>
/// REV62: reproduces the 2026-11-16 TEST5 1:10 Ortho6 TOP/BOTTOM
/// empty-annotation and ISO4 false QA REVIEW results. Pure geometric
/// tests; native AutoCAD PDF / Update View still need real-machine QA.
/// </summary>
internal static class Rev262NativeSubsetQaTests
{
    private static void Need(bool ok,string reason)
    {
        if(!ok)throw new Exception("REV62 native subset QA: "+reason);
    }

    internal static void Run()
    {
        // Exact REV61 planned camera frame coordinates in TEST5 Ortho 6
        // 022315_ee8d. The NEXT ROW does NOT overlap TOP/BOTTOM,
        // although Math.Max(view heights) incorrectly said it did.
        var cameras=new[]{
            new SupportOrthoMemberCalloutPlan17.Camera(
                "TOP",72.927,411.671,28,129.563,
                0,0,1,0,0,0,.1),
            new SupportOrthoMemberCalloutPlan17.Camera(
                "BOTTOM",348.99,411.671,36,129.563,
                0,0,-1,0,0,0,.1),
            new SupportOrthoMemberCalloutPlan17.Camera(
                "LEFT",123.708,291.214,129.563,63.351,
                -1,0,0,0,0,0,.1),
            new SupportOrthoMemberCalloutPlan17.Camera(
                "RIGHT",395.772,291.214,129.563,63.351,
                1,0,0,0,0,0,.1),
            new SupportOrthoMemberCalloutPlan17.Camera(
                "FRONT",75.927,203.863,34,63.351,
                0,-1,0,0,0,0,.1),
            new SupportOrthoMemberCalloutPlan17.Camera(
                "BACK",346.49,203.863,31,63.351,
                0,1,0,0,0,0,.1)
        };
        var titles=NativeOrthoViewTitlePlan.BuildTitles(
            cameras.Select(c=>new NativeOrthoViewTitlePlan.Frame(
                c.Name,c.X,c.Y,c.W,c.H)).ToArray(),
            841,594,allowSubset:true);
        var sources=new[]{
            new SupportOrthoMemberCalloutPlan27.Member(
                "8A3","Sloping channel 100x50 web-5 fl-7.5",
                0,0,0,false,null,"NOT_HORIZONTAL"),
            new SupportOrthoMemberCalloutPlan27.Member(
                "8A4","Sloping channel 100x50 web-5 fl-7.5",
                0,0,0,true,"+1803.9855 mm","GEOMETRIC_TOP_APPROX"),
            new SupportOrthoMemberCalloutPlan27.Member(
                "8AA","100A-TEST-001",0,0,0,false,null,"PIPE",
                PipeBopText:"BOP EL. +1803.9855 mm",
                PipeBopSource:"EXPLICIT_BOTTOM_OF_PIPE"),
            new SupportOrthoMemberCalloutPlan27.Member(
                "8AB","100A-TEST-002",0,0,0,false,null,"PIPE",
                PipeBopText:"BOP EL. +1803.9855 mm",
                PipeBopSource:"EXPLICIT_BOTTOM_OF_PIPE"),
            new SupportOrthoMemberCalloutPlan27.Member(
                "8AC","100A-TEST-003",0,0,0,false,null,"PIPE",
                PipeBopText:"BOP EL. +1803.9855 mm",
                PipeBopSource:"EXPLICIT_BOTTOM_OF_PIPE")
        };
        var placed=SupportOrthoMemberCalloutPlan27.Build(
            cameras,titles,sources,841,594,allowSubset:true);
        Need(placed.Callouts.Count==30&&
            !placed.ViewWithoutLabels.Any(),
            "all six TEST5 1:10 views retain five labels, including TOP/BOTTOM");
        Need(placed.Callouts.Count(x=>x.View=="TOP")==5&&
            placed.Callouts.Count(x=>x.View=="BOTTOM")==5&&
            !placed.Skipped.Any(x=>x.Contains("NO_SAFE_TEXT_GUTTER",
                StringComparison.Ordinal)),
            "adjacent LOWER row cannot steal TOP/BOTTOM right label gutter");
        Need(placed.Callouts.All(n=>
            n.LabelX+n.LabelWidth<841*.75),
            "labels must not enter REV59 right-side title-block area");

        var iso=new[]{"NW","NE","SW","SE"}
            .Select((name,i)=>new SupportOrthoMemberCalloutPlan17.Camera(
                name,136+(i%2)*247.48,392.488-(i/2)*136.336,
                104.979,112.336,
                i%2==0?-0.577:0.577,i/2==0?0.577:-0.577,
                0.577,0,0,0,.1)).ToArray();
        var native=new[]{
            new SupportOrthoAllViewDimPlan44.Member(
                "NATIVE:8A3","STRUCTURE",0,0,0,50,100,502),
            new SupportOrthoAllViewDimPlan44.Member(
                "NATIVE:8A4","STRUCTURE",0,0,0,50,928,100)
        };
        var qa=SupportOrthoDrawingQualityAudit61.Review(
            iso,native,Array.Empty<SupportOrthoDimLinearPlan41.Dimension>(),
            Array.Empty<string>(),1,writtenCount:0);
        Need(qa.Status=="PASS"&&qa.ExpectedOverallAxes==0&&
            qa.PresentOverallAxes==0&&qa.ReviewIssues==0&&
            qa.WrittenCountMismatches==0,
            "ISO-only native drawing has no required H/V dimensions, not REVIEW");
        var invalid=iso.ToArray();
        invalid[0]=invalid[0] with {Scale=0};
        var invalidQa=SupportOrthoDrawingQualityAudit61.Review(
            invalid,native,Array.Empty<SupportOrthoDimLinearPlan41.Dimension>(),
            Array.Empty<string>(),1,writtenCount:0);
        Need(invalidQa.Status=="REVIEW"&&
            invalidQa.Issues.Any(i=>i.Code=="NO_VALID_ORTHOGRAPHIC_VIEWS"),
            "invalid native ISO camera must not bypass QA");
        var missingSource=SupportOrthoDrawingQualityAudit61.Review(
            iso,Array.Empty<SupportOrthoAllViewDimPlan44.Member>(),
            Array.Empty<SupportOrthoDimLinearPlan41.Dimension>(),
            Array.Empty<string>(),1,writtenCount:0);
        Need(missingSource.Status=="REVIEW"&&
            missingSource.Issues.Any(i=>i.Code=="NO_NATIVE_MEMBER_EVIDENCE"),
            "ISO-only QA still requires vetted native source members");
        var badDim=new SupportOrthoDimLinearPlan41.Dimension(
            "NW","DETAIL:NW/H/OVERALL","STRUCTURE",true,
            310,580,360,580,335,594,"750",
            "REFERENCE_PROJECTED_DATUM_CHAIN_NOT_BOLT_PITCH",
            335,598,10);
        var isoDim=SupportOrthoDrawingQualityAudit61.Review(
            iso,native,new[]{badDim},Array.Empty<string>(),1,
            writtenCount:1);
        Need(isoDim.Status=="REVIEW"&&
            isoDim.Issues.Any(i=>i.Code=="UNSUPPORTED_VIEW"),
            "ISO-only sheet must not accept fabricated isometric DIMLINEAR");
        Need(SupportOrthoDrawingQualityAudit61.Review(
            Array.Empty<SupportOrthoMemberCalloutPlan17.Camera>(),
            native,Array.Empty<SupportOrthoDimLinearPlan41.Dimension>(),
            Array.Empty<string>(),1).Status=="REVIEW",
            "empty native camera sheet must remain REVIEW");
        Console.WriteLine(
            "REV62 1:10 Ortho6 TOP/BOTTOM annotations + ISO4 non-applicable H/V QA, "+
            "invalid camera/member evidence protections PASS");
    }
}
