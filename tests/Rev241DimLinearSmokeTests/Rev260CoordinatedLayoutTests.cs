using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev260CoordinatedLayoutTests
{
    private static void Need(bool success,string message)
    {
        if(!success)throw new Exception("REV22.60: "+message);
    }
    private static SupportOrthoCoordinatedLayout60.Alternative O(
        int id,double cost) =>new(id,cost);

    internal static void Run()
    {
        SupportOrthoCoordinatedLayout60.Result solver(
            System.Collections.Generic.IReadOnlyList<
                SupportOrthoCoordinatedLayout60.Group> groups,
            Func<int,int,bool> compatible) =>
            SupportOrthoCoordinatedLayout60.Solve(groups,compatible);
        var candidates=new[]{
            new SupportOrthoCoordinatedLayout60.Group(
                "H:NATIVE:BEAM",40,new[]{O(1,1),O(2,5)}),
            new SupportOrthoCoordinatedLayout60.Group(
                "V:NATIVE:POST",60,new[]{O(3,1)})
        };
        // Old REV22.59 placed H alternative #1 immediately,
        // thereby blocking V #3; the whole-view solution selects
        // H #2 and V #3 without moving the overall or chain.
        bool Compatible(int a,int b) =>
            !(a==1&&b==3||a==3&&b==1);
        var joint=solver(candidates,Compatible);
        Need(joint.Chosen.Count==2 &&
            joint.Chosen.Any(c=>c.AlternativeId==2) &&
            joint.Chosen.Any(c=>c.AlternativeId==3) &&
            !joint.Chosen.Any(c=>c.AlternativeId==1),
            "global optimization repositions H to preserve both H and V");
        Need(joint.Omitted.Count==0&&
            joint.CandidateComparisons>0,
            "all optional widths fit without intersection");
        var swapped=solver(candidates.Reverse().ToArray(),Compatible);
        Need(joint.Chosen.Select(x=>x.Key+":"+x.AlternativeId)
            .OrderBy(x=>x).SequenceEqual(
                swapped.Chosen.Select(x=>x.Key+":"+x.AlternativeId)
                    .OrderBy(x=>x)),
            "coordinated placement is independent of source collection order");

        var impossible=solver(new[]{
            new SupportOrthoCoordinatedLayout60.Group(
                "H:NATIVE:EXACT",60,new[]{O(10,1)}),
            new SupportOrthoCoordinatedLayout60.Group(
                "V:NATIVE:ENVELOPE",20,new[]{O(11,1)})
        },(a,b)=>false);
        Need(impossible.Chosen.Count==1&&
            impossible.Chosen[0].AlternativeId==10&&
            impossible.Omitted.SequenceEqual(new[]{"V:NATIVE:ENVELOPE"}),
            "when placement cannot fit, preserve stronger verified edge and omit optional envelope");
        var one=solver(new[]{
            new SupportOrthoCoordinatedLayout60.Group(
                "H:NATIVE:MEMBER",60,new[]{O(20,7),O(21,3)})
        },(_,__)=>true);
        Need(one.Chosen.Single().AlternativeId==21,
            "lowest-cost paper lane wins when safe");
        bool threw=false;
        try
        {
            solver(new[]{
                new SupportOrthoCoordinatedLayout60.Group(
                    "H:NATIVE:A",20,new[]{O(1,double.NaN)})
            },(_,__)=>true);
        }catch(ArgumentException){threw=true;}
        Need(threw,"nonfinite paper placement cannot enter optimizer");
        threw=false;
        try
        {
            solver(new[]{
                new SupportOrthoCoordinatedLayout60.Group(
                    "H:NATIVE:A",20,new[]{O(1,1)}),
                new SupportOrthoCoordinatedLayout60.Group(
                    "V:NATIVE:B",20,new[]{O(1,2)})
            },(_,__)=>true);
        }catch(ArgumentException){threw=true;}
        Need(threw,"duplicate alternative identities cannot corrupt a joint layout");

        // TEST5 native support, actual 1:15 paper cameras. This
        // integration check preserves dynamic overall/terminal dims,
        // and verifies accepted optional dimensions never intersect.
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
        var drawing=SupportOrthoReferenceDimPlan47.Build(
            new[]{right,left},members,594,841,1);
        Need(drawing.CoordinatedProfileGroups>=2,
            "local H/V groups are staged across each full native view");
        Need(drawing.CoordinatedProfilesPlaced>=1,
            "coordinated optimizer retains at least one useful native member detail");
        Need(drawing.CoordinatedProfileGroups==
            drawing.CoordinatedProfilesPlaced+
            drawing.CoordinatedProfileGroupsOmitted,
            "every eligible group is either placed safely or reported as omitted");
        Need(drawing.UnintendedAcceptedWitnessIntersections==0,
            "whole-view accepted dimensions have no unplanned witness crossings");
        Need(drawing.PriorityOverallDimensions==4,
            "both horizontal and vertical overall dims remain reserved in both views");
        Need(drawing.Dimensions.Any(d=>d.View=="RIGHT"&&
            d.Horizontal&&d.Source.EndsWith("/OVERALL")&&
            d.Measurement=="928.28")&&
            drawing.Dimensions.Any(d=>d.View=="RIGHT"&&
                !d.Horizontal&&d.Source.EndsWith("/OVERALL")&&
                d.Measurement=="502.787"),
            "true model-derived 928.28 and 502.787 overall retained");
        Need(drawing.Dimensions.All(
            SupportOrthoDynamicDimension492.MatchesDiagnostic),
            "global arrangement changes only paper positions, never hardcodes values");

        var external=new SupportOrthoAllViewDimPlan44.Member(
            "XREF_MEMBER:UNRELATED","STRUCTURE",
            0,0,0,20000,20000,20000);
        var mixed=SupportOrthoReferenceDimPlan47.Build(
            new[]{right,left},members.Append(external).ToArray(),
            594,841,1);
        Need(drawing.Dimensions.Select(x=>x.Source+":"+x.Measurement)
            .OrderBy(x=>x).SequenceEqual(
                mixed.Dimensions.Select(x=>x.Source+":"+x.Measurement)
                    .OrderBy(x=>x)),
            "XREF source never changes coordinated native dimensions");
        Console.WriteLine(
            "REV22.60 all-view local H/V optimization, protected overall, deterministic beam, safe omissions and XREF exclusion PASS");
    }
}
