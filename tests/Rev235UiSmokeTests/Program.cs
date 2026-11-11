using SPTC.SupportDrawings;
using System;
using System.Linq;

void Check(bool ok,string message)
{
    if(!ok)throw new Exception("REV22.35 smoke FAIL: "+message);
}
var frames=new[]{
    new NativeOrthoViewTitlePlan.Frame("TOP",118.8,700,90,50),
    new NativeOrthoViewTitlePlan.Frame("NE",338.58,700,90,50)
};
var titles=NativeOrthoViewTitlePlan.BuildTitles(frames,594,841,true);
Check(titles.Count==2,"subset title count");
Check(titles[0].Text=="TOP VIEW"&&titles[1].Text=="NE ISOMETRIC VIEW",
    "native view titles");
bool rejected=false;
try{NativeOrthoViewTitlePlan.BuildTitles(frames,594,841);}
catch(ArgumentException){rejected=true;}
Check(rejected,"original ten-view strict guard preserved");
rejected=false;
try{NativeOrthoViewTitlePlan.BuildTitles(new[]{
    new NativeOrthoViewTitlePlan.Frame("UNKNOWN",118.8,700,90,50)},594,841,true);}
catch(ArgumentException){rejected=true;}
Check(rejected,"reject unsupported view label");
var cameras=new[]{
    new SupportOrthoMemberCalloutPlan17.Camera("TOP",118.8,700,90,50,
        0,0,1,0,0,0,1.0/30),
    new SupportOrthoMemberCalloutPlan17.Camera("NE",338.58,700,90,50,
        .577,.577,.577,0,0,0,1.0/30)
};
var plan=SupportOrthoMemberCalloutPlan27.Build(cameras,titles,
    Array.Empty<SupportOrthoMemberCalloutPlan27.Member>(),594,841,true);
Check(plan.Callouts.Count==0,"empty source callouts");
rejected=false;
try{SupportOrthoMemberCalloutPlan27.Build(cameras,titles,
    Array.Empty<SupportOrthoMemberCalloutPlan27.Member>(),594,841);}
catch(ArgumentException){rejected=true;}
Check(rejected,"preserve all-ten production default");
// REV22.37: a nearby member's real XREF DWG filename becomes the visible
// paper-space source label, without adding a fabricated structure size/TOS.
var xref=new SupportOrthoMemberCalloutPlan27.Member(
    "XREF_MEMBER:MODEL/XREF:1/42","SOURCE FILE",0,0,0,false,null,
    "XREF_SOURCE_DWG",null,null,"SUPPORT_100A.dwg","STRUCTURE");
var xrefPlan=SupportOrthoMemberCalloutPlan27.Build(cameras,titles,
    new[]{xref},594,841,true);
Check(xrefPlan.Callouts.Count==2,"XREF labels per selected view");
Check(xrefPlan.Callouts.All(c=>
    c.IsXrefMember&&!c.IsPipe&&!c.HasTos &&
    c.Text=="XREF STRUCTURE: SUPPORT_100A.dwg"),
    "actual source drawing filename and structural classification");

var native=new SupportOrthoMemberCalloutPlan27.Member(
    "NATIVE-1","C100",0,0,0,false,null,"NONE");
var crowd=new[]{native}.Concat(Enumerable.Range(0,60).Select(i=>
    xref with {Handle="XREF_MEMBER:"+i, XrefDrawingName=
        "SUPPORT_"+i+".dwg"})).ToArray();
var crowdedPlan=SupportOrthoMemberCalloutPlan27.Build(cameras,titles,
    crowd,594,841,true);
Check(crowdedPlan.Callouts.Count(c=>c.Handle=="NATIVE-1")==2,
    "optional XREF notes must not crowd out native member callouts");
Check(crowdedPlan.Skipped.Any(x=>x.Contains("XREF_FILENAME_NO_LABEL_SPACE")),
    "overflow XREF labels are explicitly diagnosed");
Console.WriteLine("REV22.37 XREF source-filename/priority smoke PASS");
Console.WriteLine("REV22.35 selected-native-view title/callout smoke PASS");
