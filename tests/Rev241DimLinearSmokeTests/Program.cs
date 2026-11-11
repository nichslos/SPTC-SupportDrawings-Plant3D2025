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
// REV22.42: the supplied Ortho PDF repeats the SAME 828.28 mm across
// TOP/BOTTOM/LEFT/RIGHT/FRONT/BACK. Compact one verified length globally.
var repeatSource=horizontal with {Key="NATIVE:828",Ax=-414.14,Bx=414.14,
    SourceLength=828.28,Millimeters=828.28};
var top2=top with {Name="TOP",Y=700};
var bottom=top with {Name="BOTTOM",Y=700,DirZ=-1};
var front2=front with {Name="FRONT",Y=550};
var back=front with {Name="BACK",Y=550,DirY=1};
var left=front with {Name="LEFT",Y=430,DirX=-1,DirY=0};
var right=front with {Name="RIGHT",Y=430,DirX=1,DirY=0};
var compact=SupportOrthoDimLinearPlan41.Build(
    new[]{right,back,bottom,front2,left,top2},
    new[]{repeatSource},594,841);
Assert(compact.Dimensions.Count==1 &&
    compact.Dimensions[0].View=="TOP" &&
    compact.Dimensions[0].Measurement=="828.28",
    "six orthographic views share one 828.28 dimension, prioritizing TOP");
Assert(compact.Skipped.Any(x=>x.Contains("SOURCE_ALREADY_DIMENSIONED_IN_TOP")),
    "opposite-view dimensions produce explicit reduction diagnostics");

// Equal-sized independent members of the SAME category and physical
// orientation should not repeat the same size on multiple leaders.
var equalLength=repeatSource with {Key="NATIVE:829",Ay=100,By=100};
var equalPlan=SupportOrthoDimLinearPlan41.Build(
    new[]{top2,front2},new[]{repeatSource,equalLength},594,841);
Assert(equalPlan.Dimensions.Count==1 &&
    equalPlan.Skipped.Any(x=>x.Contains("EQUAL_LENGTH_ALREADY_DIMENSIONED_BY_")),
    "repeat identical size/type/direction once per sheet");

// Different lengths, orientations or Plant categories must remain distinct.
var different=repeatSource with {Key="NATIVE:900",
    Ax=-450,Bx=450,Ay=500,By=500,SourceLength=900,Millimeters=900};
var otherCategory=repeatSource with {
    Key="NATIVE:SUPP",Kind="SUPPORT",Ay=-500,By=-500
};
var dimensionVariety=SupportOrthoDimLinearPlan41.Build(
    new[]{top2,front2},
    new[]{repeatSource,different,otherCategory,vertical},594,841);
Assert(dimensionVariety.Dimensions.Count==4,
    "different source lengths/types and H/V orientation stay dimensioned");

// A source that cannot be projected in TOP may be included in FRONT.
var fallback=horizontal with {
    Key="NATIVE:FALLBACK",Ay=1100,By=1100
};
var fallbackPlan=SupportOrthoDimLinearPlan41.Build(
    new[]{top2,front2},new[]{fallback},594,841);
Assert(fallbackPlan.Dimensions.Count==1 &&
    fallbackPlan.Dimensions[0].View=="FRONT",
    "an off-view preferred TOP should permit a fallback FRONT dimension");
// REV22.43: Some valid native/XREF vertical members expose their physical
// geometry but do not publish Start/End in Plant 3D API. Their conservative
// overall Z envelope may be measured and must be labeled as HEIGHT, never
// silently passed off as a fabricated member cut length.
Assert(SupportOrthoDimLinearPlan41.AcceptOverallZHeight(
    100,100,450,1.0),"upright support 450 mm high qualifies");
Assert(SupportOrthoDimLinearPlan41.AcceptOverallZHeight(
    70,70,0.45,1000.0),"source geometry in meters is unit-safe");
Assert(!SupportOrthoDimLinearPlan41.AcceptOverallZHeight(
    500,600,450,1.0),"broad assembly is not assumed a vertical member");
Assert(!SupportOrthoDimLinearPlan41.AcceptOverallZHeight(
    50,50,70,1.0),"very short extents are not dimensioned as overall height");
Assert(!SupportOrthoDimLinearPlan41.AcceptOverallZHeight(
    50,50,double.NaN,1.0),"invalid geometry always rejected");

var uprightHeight=vertical with {
    Key="XREF_MEMBER:VERTICAL_POST",
    Provenance="GEOMETRIC_OVERALL_Z_HEIGHT"
};
var verticalOnly=SupportOrthoDimLinearPlan41.Build(
    new[]{front,top},new[]{uprightHeight},594,841);
Assert(verticalOnly.Dimensions.Count==1 &&
    verticalOnly.Dimensions[0].View=="FRONT" &&
    !verticalOnly.Dimensions[0].Horizontal &&
    verticalOnly.Dimensions[0].Measurement=="H=400" &&
    verticalOnly.Dimensions[0].Provenance=="GEOMETRIC_OVERALL_Z_HEIGHT",
    "recognized XREF vertical post gets an overall-height DIMLINEAR on FRONT");
var mixedSheet=SupportOrthoDimLinearPlan41.Build(
    new[]{front,top,bottom,back},
    new[]{horizontal,uprightHeight},594,841);
Assert(mixedSheet.Dimensions.Count==2 &&
    mixedSheet.Dimensions.Any(d=>d.View=="TOP" &&
        d.Measurement=="400" && d.Horizontal) &&
    mixedSheet.Dimensions.Any(d=>d.View=="FRONT" &&
        d.Measurement=="H=400" && !d.Horizontal),
    "compact mode must preserve one horizontal plus one vertical dimension");
var verticalTwin=uprightHeight with {
    Key="XREF_MEMBER:VERTICAL_POST_2",Ax=100, Bx=100
};
var twinPlan=SupportOrthoDimLinearPlan41.Build(
    new[]{front,back},
    new[]{uprightHeight,verticalTwin},594,841);
Assert(twinPlan.Dimensions.Count==1 &&
    twinPlan.Skipped.Any(x=>x.Contains("EQUAL_LENGTH_ALREADY_DIMENSIONED_BY_")),
    "identical vertical overall-height dimensions remain compact");
Console.WriteLine("REV22.43 vertical overall-height fallback and cross-axis compact safety PASS");

Console.WriteLine("REV22.42 compact DIMLINEAR across six views PASS");
Console.WriteLine("REV22.41 verified orthographic DIMLINEAR planner PASS");
