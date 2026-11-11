using System;
using System.Linq;
using SPTC.SupportDrawings;

static void Assert(bool condition,string message)
{
    if(!condition)throw new Exception("REV22.41 DIMLINEAR test: "+message);
}
var top=new SupportOrthoMemberCalloutPlan17.Camera("TOP",
    120,700,100,80,0,0,1,0,0,0,1.0/20);
var front=new SupportOrthoMemberCalloutPlan17.Camera("FRONT",
    120,550,100,80,0,-1,0,0,0,0,1.0/20);
var iso=front with {Name="NW"};
var horizontal=new SupportOrthoDimLinearPlan41.Segment(
    "NATIVE:1","STRUCTURE",-200,0,0,200,0,0,400,400);
var vertical=new SupportOrthoDimLinearPlan41.Segment(
    "NATIVE:2","SUPPORT",0,0,-200,0,0,200,400,400);
var topResult=SupportOrthoDimLinearPlan41.Build(new[]{top},
    new[]{horizontal},594,841);
Assert(topResult.Dimensions.Count==1 &&
    topResult.Dimensions[0].Horizontal &&
    topResult.Dimensions[0].Measurement=="400",
    "true horizontal source produces DIMLINEAR 0-degree in TOP");
var frontResult=SupportOrthoDimLinearPlan41.Build(new[]{front},
    new[]{vertical},594,841);
Assert(frontResult.Dimensions.Count==1 &&
    !frontResult.Dimensions[0].Horizontal &&
    frontResult.Dimensions[0].Measurement=="400",
    "true vertical source produces DIMLINEAR 90-degree in FRONT");
Assert(SupportOrthoDimLinearPlan41.Build(new[]{iso},
    new[]{horizontal,vertical},594,841).Dimensions.Count==0,
    "never auto-dimension NW/NE/SW/SE isometric views");
Assert(SupportOrthoDimLinearPlan41.Build(new[]{top},
    new[]{vertical},594,841).Dimensions.Count==0,
    "vertical source viewed from TOP is foreshortened and skipped");
var other=horizontal with {Key="XREF_MEMBER:MODEL/123",Kind="SUPPORT"};
var duplicate=SupportOrthoDimLinearPlan41.Build(new[]{top},
    new[]{horizontal,other},594,841);
Assert(duplicate.Dimensions.Count==1 &&
    duplicate.Skipped.Any(s=>s.Contains("NO_NONOVERLAPPING_DIMLINE")),
    "paper-space overlaps skipped for identical native/XREF members");
var outOfFrame=horizontal with {
    Key="NATIVE:REMOTE",Ax=100000,Bx=100400
};
Assert(SupportOrthoDimLinearPlan41.Build(new[]{top},
    new[]{outOfFrame},594,841).Dimensions.Count==0,
    "do not place dimensions outside the actual native viewport");
var diagonal=horizontal with {
    Key="NATIVE:DIAGONAL",Bz=400,SourceLength=Math.Sqrt(320000),
    Millimeters=Math.Sqrt(320000)
};
Assert(SupportOrthoDimLinearPlan41.Build(new[]{top},
    new[]{diagonal},594,841).Dimensions.Count==0,
    "do not dimension 3D diagonal projections");
Assert(SupportOrthoDimLinearPlan41.Build(new[]{top},
    new[]{other},594,841).Dimensions.Count==1,
    "same verified endpoint rules apply to XREF support");
Console.WriteLine("REV22.41 verified orthographic DIMLINEAR planner PASS");
