using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev261DrawingQualityAuditTests
{
    private static void Need(bool pass,string reason)
    {
        if(!pass)throw new Exception("REV22.61: "+reason);
    }

    internal static void Run()
    {
        var right=new SupportOrthoMemberCalloutPlan17.Camera(
            "RIGHT",338.58,588.7,86.376,42.234,
            1,0,0,714.341,1955.379,0,1.0/15);
        var left=right with {
            Name="LEFT",X=118.8,DirX=-1,ViewCenterX=-714.341
        };
        var members=new[]{
            new SupportOrthoAllViewDimPlan44.Member(
                "NATIVE:8A4","STRUCTURE",
                7760.82,250.201,1703.985,
                7810.82,1178.481,1803.985),
            new SupportOrthoAllViewDimPlan44.Member(
                "NATIVE:8A3","STRUCTURE",
                7760.82,250.201,1703.985,
                7810.82,350.201,2206.772)
        };
        var cameras=new[]{right,left};
        var planned=SupportOrthoReferenceDimPlan47.Build(
            cameras,members,594,841,1);
        var baseline=SupportOrthoDrawingQualityAudit61.Review(
            cameras,members,planned.Dimensions,
            planned.Skipped,1,true);
        Need(baseline.Status=="PASS"&&
            baseline.ExpectedOverallAxes==4&&
            baseline.PresentOverallAxes==4&&
            baseline.MissingOverallAxes==0&&
            baseline.GeometryIntersections==0,
            "TEST5 RIGHT/LEFT native dimensions pass independent H/V audit");

        // Crowded optional dimension lanes never invalidate a good
        // overall/chain drawing, but the omission count stays visible.
        var optional=SupportOrthoDrawingQualityAudit61.Review(
            cameras,members,planned.Dimensions,
            new[]{
                "RIGHT/H:NATIVE:8A3:REV22_60_OPTIONAL_PROFILE_NO_COMPATIBLE_LAYOUT",
                "LEFT/NATIVE:8A4:V_LOCAL_MEMBER_WIDTH_NO_CLEAR_LANE"
            },1,true);
        Need(optional.Status=="PASS"&&optional.OptionalOmissions==2,
            "optional 50/100 width omissions are reported but not failures");

        var rightV=planned.Dimensions.First(d=>
            d.View=="RIGHT"&&!d.Horizontal&&
            d.Source.EndsWith("/OVERALL"));
        var missing=SupportOrthoDrawingQualityAudit61.Review(
            cameras,members,planned.Dimensions
                .Where(x=>x!=rightV).ToArray(),
            planned.Skipped,1);
        Need(missing.Status=="REVIEW"&&
            missing.MissingOverallAxes==1&&
            missing.Issues.Any(i=>
                i.Code=="MISSING_ESSENTIAL_OVERALL"&&
                i.View=="RIGHT"),
            "missing RIGHT 502.787 overall is a review issue");

        var badValue=SupportOrthoDrawingQualityAudit61.Review(
            cameras,members,planned.Dimensions.Select(d=>
                d==rightV?d with {Measurement="9999"}:d).ToArray(),
            planned.Skipped,1);
        Need(badValue.Status=="REVIEW"&&
            badValue.MeasurementMismatches>0,
            "a stale/wrong DIMLFAC diagnostic is identified");

        var duplicate=SupportOrthoDrawingQualityAudit61.Review(
            cameras,members,planned.Dimensions.Append(rightV).ToArray(),
            planned.Skipped,1);
        Need(duplicate.Status=="REVIEW"&&
            duplicate.DuplicateDimensions>0,
            "duplicate overall/source dimensions are rejected");

        var foreign=planned.Dimensions.First(d=>
            d.Source.Contains("/MEMBER_",StringComparison.Ordinal));
        var unselected=SupportOrthoDrawingQualityAudit61.Review(
            cameras,members,planned.Dimensions.Select(d=>d==foreign?
                d with {Source=d.Source.Replace(
                    "MEMBER_NATIVE:8A3","MEMBER_NATIVE:UNSELECTED")
                    .Replace("MEMBER_NATIVE:8A4",
                        "MEMBER_NATIVE:UNSELECTED")}:d).ToArray(),
            planned.Skipped,1);
        Need(unselected.Status=="REVIEW"&&
            unselected.Issues.Any(i=>
                i.Code=="MEMBER_SOURCE_NOT_SELECTED"),
            "unselected native member cannot silently provide dimensions");

        var external=SupportOrthoDrawingQualityAudit61.Review(
            cameras,members,planned.Dimensions.Select(d=>d==rightV?
                d with {Source="DETAIL:RIGHT/V/MEMBER_XREF:STRUCT"}:
                d).ToArray(),planned.Skipped,1);
        Need(external.Status=="REVIEW"&&
            external.Issues.Any(i=>
                i.Code=="XREF_OR_UNKNOWN_DIMENSION_SOURCE"),
            "XREF geometry cannot enter native dimension audit");

        var written=SupportOrthoDrawingQualityAudit61.Review(
            cameras,members,planned.Dimensions,
            planned.Skipped,1,true,
            writtenCount:planned.Dimensions.Count-1);
        Need(written.Status=="REVIEW"&&
            written.WrittenCountMismatches==1,
            "missing paper-space DIMLINEAR after writer is detected");

        var h=new SupportOrthoDimLinearPlan41.Dimension(
            "RIGHT","DETAIL:RIGHT/H/CHAIN_1","STRUCTURE",true,
            310,580,360,580,335,594,"750",
            "REFERENCE_PROJECTED_DATUM_CHAIN_NOT_BOLT_PITCH",
            335,598,15);
        var local=new SupportOrthoDimLinearPlan41.Dimension(
            "RIGHT","DETAIL:RIGHT/H/CHAIN_2","STRUCTURE",true,
            325,580,335,580,330,604,"150",
            "REFERENCE_PROJECTED_DATUM_CHAIN_NOT_BOLT_PITCH",
            348,607,15);
        var crossing=SupportOrthoDrawingQualityAudit61.Review(
            cameras,members,new[]{h,local},
            Array.Empty<string>(),1,true);
        Need(crossing.GeometryIntersections>0&&
            crossing.Issues.Any(i=>
                i.Code=="UNINTENDED_DIMENSION_INTERSECTION"),
            "real DIMLINEAR witness crossing other dimension baseline is flagged");

        // A read-only source snapshot can be absent without the
        // auditor fabricating exact steel edges.
        var memberWidth=planned.Dimensions.First(d=>
            d.Source.Contains("/MEMBER_",StringComparison.Ordinal));
        var fakeExact=SupportOrthoDrawingQualityAudit61.Review(
            cameras,members,planned.Dimensions.Select(d=>
                d==memberWidth?d with {
                    Provenance="EXACT_NATIVE_PROJECTED_PROFILE_EDGE_NOT_CUT_LENGTH"
                }:d).ToArray(),planned.Skipped,1,true,
            nativeEdges:Array.Empty<SupportOrthoNativeEdgeGeometry59.Edge>());
        Need(fakeExact.Status=="REVIEW"&&
            fakeExact.Issues.Any(i=>
                i.Code=="EXACT_EDGE_EVIDENCE_MISSING"),
            "fabricated exact-edge provenance cannot pass source QA");

        Console.WriteLine(
            "REV22.61 QA PASS/REVIEW, essential H/V coverage, optional omission, duplicate, DIMLFAC, XREF, source, crossing and writer-count regression PASS");
    }
}
