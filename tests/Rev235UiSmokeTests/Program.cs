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
Console.WriteLine("REV22.35 selected-native-view title/callout smoke PASS");
