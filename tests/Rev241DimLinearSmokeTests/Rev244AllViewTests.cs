using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev244AllViewTests
{
    internal static void Run()
    {
        var top=Camera("TOP",0,0,1);
        var bottom=Camera("BOTTOM",0,0,-1);
        var front=Camera("FRONT",0,-1,0);
        var back=Camera("BACK",0,1,0);
        var left=Camera("LEFT",-1,0,0);
        var right=Camera("RIGHT",1,0,0);
        var m=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:STRUCTURE_1","STRUCTURE",
            -250,-200,-300,250,200,300);
        var all=SupportOrthoAllViewDimPlan44.Build(
            new[]{right,bottom,front,top,left,back},new[]{m},
            594,841,1.0);
        Require(all.OrthographicViewCount==6,
            "all six actual axial native views participate");
        Require(all.RequestedAxisCount==12,
            "one member requests both axes in six views");
        Require(all.Dimensions.Count==12,
            "every selected orthographic view draws both H and V");
        foreach(string name in new[]{"TOP","BOTTOM","FRONT","BACK","LEFT","RIGHT"})
        {
            var view=all.Dimensions.Where(x=>x.View==name).ToArray();
            Require(view.Length==2 && view.Count(x=>x.Horizontal)==1 &&
                view.Count(x=>!x.Horizontal)==1,
                name+" has separate horizontal and vertical DIMLINEAR");
            Require(view.All(x=>x.Source.StartsWith("NATIVE:STRUCTURE_1/") &&
                x.Provenance=="PROJECTED_OVERALL_VIEW_ENVELOPE"),
                "source and geometry provenance kept for "+name);
        }
        // Previous REV22.42 restricted a common length to one side only.
        // This user explicitly wants BOTH views and ALL individually.
        var m2=m with {Key="NATIVE:STRUCTURE_2",
            MinX=-190,MaxX=310,MinY=-160,MaxY=240};
        var repeated=SupportOrthoAllViewDimPlan44.Build(
            new[]{top,bottom},new[]{m,m2},594,841,1.0);
        Require(repeated.Dimensions.Any(d=>d.View=="TOP"&&
            d.Source.StartsWith("NATIVE:STRUCTURE_1/")),
            "first equal-length structure remains separately annotated");
        Require(repeated.Dimensions.Any(d=>d.View=="BOTTOM"&&
            d.Source.StartsWith("NATIVE:STRUCTURE_2/")),
            "second equal-length structure still gets its own opposite-view dimension");
        Require(repeated.Dimensions.All(x=>x.Provenance==
            "PROJECTED_OVERALL_VIEW_ENVELOPE"),
            "overall projections never masquerade as member cut length");
        // Vertical members also have a plan-view footprint. If physically
        // present, plan width/depth can be dimensioned in TOP/BOTTOM while
        // true vertical world extent is dimensioned on elevation views.
        var column=new SupportOrthoAllViewDimPlan44.Member(
            "XREF_MEMBER:POST_1","STRUCTURE",
            -120,-100,-400,120,100,400);
        var verticalSet=SupportOrthoAllViewDimPlan44.Build(
            new[]{top,bottom,front,back,left,right},new[]{column},
            594,841,1.0);
        Require(verticalSet.Dimensions.Count==12,
            "XREF vertical structure has H/V envelope dimensions in all six axial views");
        Require(verticalSet.Dimensions.Any(d=>d.View=="FRONT" &&
            !d.Horizontal && d.Measurement=="800"),
            "front elevation vertically measures overall 800 mm Z");
        var iso=Camera("NW",1,-1,1);
        var isometric=SupportOrthoAllViewDimPlan44.Build(
            new[]{iso,top},new[]{m},594,841,1.0);
        Require(isometric.OrthographicViewCount==1 &&
            isometric.Dimensions.All(x=>x.View=="TOP") &&
            isometric.Skipped.Any(x=>x.Contains("ISO_DIMLINEAR_NOT_VALID")),
            "isometric foreshortening cannot be labeled as true DIMLINEAR");
        var zeroProjection=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:THIN","STRUCTURE",0,0,-100,0,0,100);
        var thin=SupportOrthoAllViewDimPlan44.Build(
            new[]{top},new[]{zeroProjection},594,841,1.0);
        Require(thin.Dimensions.Count==0 &&
            thin.Skipped.Any(x=>x.Contains("SPAN_TOO_SMALL_ON_PAPER")),
            "do not fabricate a 0 mm or illegibly small dimension");
        var unknown=m with {Key="NATIVE:PIPE_NOT_STRUCTURAL",Kind="PIPE"};
        var excluded=SupportOrthoAllViewDimPlan44.Build(
            new[]{top},new[]{unknown},594,841,1.0);
        Require(excluded.Dimensions.Count==0,
            "non-structure/support sources are excluded from all-member dimensions");
        Console.WriteLine("REV22.44 per-member per-six-views H/V dimension tests PASS");
    }

    private static SupportOrthoMemberCalloutPlan17.Camera Camera(
        string name,double x,double y,double z) =>
        new(name,180,650,150,160,x,y,z,0,0,0,1.0/20);

    private static void Require(bool value,string why)
    {
        if(!value)throw new Exception("REV22.44: "+why);
    }
}
