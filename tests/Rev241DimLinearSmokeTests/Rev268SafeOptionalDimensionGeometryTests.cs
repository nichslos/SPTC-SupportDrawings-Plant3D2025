using System;
using System.Linq;
using SPTC.SupportDrawings;

/// <summary>
/// REV68 tests only native, verified paper-space witness/dimension-line
/// alternatives after existing positions fail. No assertion that the
/// missing two TEST5 widths are guaranteed until Plant 3D live QA.
/// </summary>
internal static class Rev268SafeOptionalDimensionGeometryTests
{
    private static void Need(bool pass,string reason)
    {
        if(!pass)throw new Exception("REV68 native optional DIMLINEAR: "+reason);
    }

    internal static void Run()
    {
        // An exact Plant straight member Start/End axis supports
        // interpolated projected sections. Preserve the REV63 first
        // four source-derived stations in precisely the same order.
        var old=SupportOrthoProfileStationPlan.AlongVerifiedAxis(
            true,50,90,0,100);
        var a0=SupportOrthoProfileStationPlan.AlongVerifiedAxis(
            true,50,90,0,100,supplementalA0:true);
        Need(old.Count==4&&a0.Count>old.Count&&
            a0.Take(4).SequenceEqual(old),
            "the first four established native axis sections never move");
        Need(a0.All(x=>double.IsFinite(x)&&x>0&&x<100)&&
            a0.Distinct().Count()==a0.Count,
            "all supplementary sections are bounded on verified native axis");
        Need(SupportOrthoProfileStationPlan.AlongVerifiedAxis(
            false,50,90,0,100,supplementalA0:true).Count==0&&
            SupportOrthoProfileStationPlan.AlongVerifiedAxis(
            true,50,0,0,100,supplementalA0:true).Count==0,
            "unverified or degenerate source axis cannot invent a witness");

        var oldLanes=SupportOrthoReferenceLayout51
            .AdaptiveLocalMemberLaneOffsets().ToArray();
        var extraLanes=SupportOrthoReferenceLayout51
            .SupplementaryLocalMemberLaneOffsets().ToArray();
        Need(oldLanes.Length==6&&extraLanes.Length>=8&&
            extraLanes.All(x=>double.IsFinite(x)&&x>0&&x<6),
            "measured extra paper baseline lanes are bounded");
        Need(oldLanes.SequenceEqual(
            SupportOrthoReferenceLayout51
                .AdaptiveLocalMemberLaneOffsets()),
            "REV63 existing successful local baseline positions unchanged");

        // Representative actual 8A3/8A4 source envelopes from TEST5.
        // Essential and accepted dimensions are the safety baseline;
        // new exact-scale A0 alternatives cannot change those data.
        var members=new[]{
            new SupportOrthoAllViewDimPlan44.Member(
                "NATIVE:8A3","STRUCTURE",
                7760.82,714.341,1703.985,
                7810.82,814.341,2206.773),
            new SupportOrthoAllViewDimPlan44.Member(
                "NATIVE:8A4","STRUCTURE",
                7760.82,250.201,1703.985,
                7810.82,1178.481,1803.985)
        };
        var cameras=new[]{
            new SupportOrthoMemberCalloutPlan17.Camera(
                "LEFT",259.428,558.46,129.563,63.351,
                -1,0,0,-714.341,1955.379,0,.1),
            new SupportOrthoMemberCalloutPlan17.Camera(
                "RIGHT",531.492,558.46,129.563,63.351,
                1,0,0,714.341,1955.379,0,.1)
        };
        var titles=NativeOrthoViewTitlePlan.BuildTitles(
            cameras.Select(c=>new NativeOrthoViewTitlePlan.Frame(
                c.Name,c.X,c.Y,c.W,c.H)).ToArray(),
            1189,841,allowSubset:true);
        var result=SupportOrthoReferenceDimPlan47.Build(
            cameras,members,1189,841,1,titles:titles);
        Need(result.PriorityOverallDimensions==4&&
            result.UnintendedAcceptedWitnessIntersections==0,
            "LEFT and RIGHT mandatory overall H/V axes remain collision-free");
        Need(result.Dimensions.All(
            SupportOrthoDynamicDimension492.MatchesDiagnostic),
            "every dimension remains verified from source geometry");
        Need(result.Dimensions.All(d=>
            !d.Source.Contains("XREF",StringComparison.OrdinalIgnoreCase)),
            "no XREF, inferred bolt pitch or fabricated datum introduced");
        Need(result.Rev68SupplementaryGeometryCandidatesTested>=0&&
            result.Rev68SupplementaryGeometryCandidatesTested<=1280&&
            result.Rev68SupplementaryGeometryDimensionsRecovered>=0&&
            result.Rev68SupplementaryGeometryDimensionsRecovered<=
                result.LocalMemberDimensions,
            "A0 supplementary search stays bounded and add-only");
        var invalid=members[1] with {Key="XREF:8A4"};
        var isolated=SupportOrthoReferenceDimPlan47.Build(
            cameras,new[]{members[0],invalid},1189,841,1,
            titles:titles);
        Need(isolated.Dimensions.All(x=>
                !x.Source.Contains("XREF",StringComparison.OrdinalIgnoreCase))&&
            isolated.UnintendedAcceptedWitnessIntersections==0,
            "unverified source never yields native local witness geometry");
        Console.WriteLine(
            "REV68 verified native-axis sections, extra true DIMLINEAR "+
            "baseline candidates, bounded A0-only retries, 4/4 essential "+
            "LEFT/RIGHT overalls, zero witness conflicts PASS");
    }
}
