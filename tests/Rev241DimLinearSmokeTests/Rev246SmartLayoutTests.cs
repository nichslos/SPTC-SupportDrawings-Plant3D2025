using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev246SmartLayoutTests
{
    internal static void Run()
    {
        var top=new SupportOrthoMemberCalloutPlan17.Camera(
            "TOP",180,650,150,160,0,0,1,0,0,0,1.0/20);
        var member=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:STRUCTURE_1","STRUCTURE",
            -200,-150,-100,200,150,100);
        var baseline=SupportOrthoSmartDimPlan46.Build(
            new[]{top},new[]{member},594,841,1.0);
        Check(baseline.Dimensions.Count==2,
            "single TOP structure gets H and V without collisions");
        Check(baseline.Dimensions.All(x=>
            x.PaperTextX.HasValue&&x.PaperTextY.HasValue&&
            x.Provenance=="PROJECTED_OVERALL_VIEW_ENVELOPE"),
            "explicit text positioning and overall-envelope provenance");
        Check(baseline.Dimensions.Any(x=>x.Horizontal&&
            x.Measurement=="400")&&
            baseline.Dimensions.Any(x=>!x.Horizontal&&
            x.Measurement=="300"),
            "H/V values remain exact source-projected millimeters");

        // A real leader corridor crossing the planner's first H lane
        // must force an alternative side/stack, not an overlap.
        var label=new SupportOrthoMemberCalloutPlan27.Callout(
            "TOP","PIPE_1","Pipe label",false,
            168,660.7,186,196,664,38,2.7,true,false);
        var withLeader=SupportOrthoSmartDimPlan46.Build(
            new[]{top},new[]{member},594,841,1.0,
            new[]{label});
        Check(withLeader.CollisionCandidatesRejected>0,
            "leader/text obstacle rejects unsafe placements");
        Check(withLeader.Repositioned>0,
            "automatic dimensions reposition to a clear lane");
        Check(withLeader.Dimensions.Count>=1,
            "collision handling preserves feasible dimensions");

        // Short 50 mm XREF footprints remain measurable at 1:30
        // while moving text outside the extension lines.
        var tinyCamera=top with {
            X=118.8,Y=723.26,W=63.732,H=48.134,
            ViewCenterX=7956.295,ViewCenterY=3484.858,Scale=1.0/30
        };
        var narrow=new SupportOrthoAllViewDimPlan44.Member(
            "XREF_MEMBER:SUPP-1/401","STRUCTURE",
            7931.295,3459.858,0,7981.295,3509.858,500);
        var shortResult=SupportOrthoSmartDimPlan46.Build(
            new[]{tinyCamera},new[]{narrow},594,841,1.0);
        Check(shortResult.Dimensions.Count==2&&
            shortResult.Dimensions.All(x=>x.Measurement=="50"),
            "both axes of 50 mm TOP footprint survive at 1:30");

        // Multiple objects and axes share an actual collision registry:
        // the reserved text envelopes of all accepted dims are disjoint.
        var shifted=member with {Key="NATIVE:STRUCTURE_2",
            MinX=member.MinX+450,MaxX=member.MaxX+450};
        var grouped=SupportOrthoSmartDimPlan46.Build(
            new[]{top},new[]{member,shifted},594,841,1.0);
        for(int i=0;i<grouped.Placements.Count;i++)
            for(int j=i+1;j<grouped.Placements.Count;j++)
            {
                var a=grouped.Placements[i];
                var b=grouped.Placements[j];
                Check(!a.Text.Intersects(b.Text,.45)&&
                    !a.DimensionLine.Intersects(b.Text,.45)&&
                    !a.Text.Intersects(b.DimensionLine,.45),
                    "accepted dimensions never share text/line rectangles");
            }

        var front=top with {Name="FRONT",X=350,Y=480,
            DirY=-1,DirZ=0};
        var pair=SupportOrthoSmartDimPlan46.Build(
            new[]{top,front},new[]{member},594,841,1.0);
        Check(pair.OrthographicViewCount==2&&
            pair.Dimensions.Any(x=>x.View=="TOP")&&
            pair.Dimensions.Any(x=>x.View=="FRONT"),
            "each orthographic viewport independently dimensioned");
        var iso=top with {Name="NW",DirX=1,DirY=-1,DirZ=1};
        var orthoOnly=SupportOrthoSmartDimPlan46.Build(
            new[]{top,iso},new[]{member},594,841,1.0);
        Check(orthoOnly.Dimensions.All(x=>x.View=="TOP")&&
            orthoOnly.Skipped.Any(x=>
                x.Contains("ISO_DIMLINEAR_NOT_VALID")),
            "never fabricate dimensions in foreshortened isometrics");

        // A fully blocked sheet must SKIP rather than force overlapping
        // dimensions. This synthetic label is a reserved 130 mm corridor.
        var obstruction=new SupportOrthoMemberCalloutPlan27.Callout(
            "TOP","A","A",false,90,595,95,96,720,
            400,145);
        var blocked=SupportOrthoSmartDimPlan46.Build(
            new[]{top},new[]{member},594,841,1.0,
            new[]{obstruction});
        Check(blocked.Dimensions.Count<baseline.Dimensions.Count&&
            blocked.Skipped.Any(x=>
                x.Contains("NO_COLLISION_FREE_DIMENSION_LANE")),
            "unresolvable text clash explicitly skips the dimension");

        Console.WriteLine(
            "REV22.46 collision-aware dimension, leader, short-span and reporting PASS");
    }

    private static void Check(bool yes,string label)
    {
        if(!yes)throw new Exception("REV22.46: "+label);
    }
}
