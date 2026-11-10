using System;
using System.Collections.Generic;
using SPTC.SupportDrawings;
using M=SPTC.SupportDrawings.SupportOrthoXrefAuditManifest30;

var log=new List<string>{
    "2026-11-10 SOURCE_MODEL=C:\\Example\\Models\\PIPE.dwg",
    "2026-11-10 REV22_29_SUPPORT_RAW_MIN=(0,0,0)",
    "2026-11-10 REV22_29_SUPPORT_RAW_MAX=(500,500,1800)",
    "2026-11-10 NATIVE_CUBE_MIN=(-200,-200,-200)",
    "2026-11-10 NATIVE_CUBE_MAX=(700,700,2000)",
    "2026-11-10 REV22_29_PIPE_SOURCE=XREF:MODEL/XREF:73D:TEST6/409 LINE_TAG=TEST-001 LINE_SOURCE=EXPLICIT_LINE_NUMBER_TAG BOP EL. +3899.9543 mm BOP_SOURCE=XREF_EXPLICIT_BOTTOM_OF_PIPE_TRANSFORMED ANCHOR_SOURCE=XREF_WCS_BOUNDS_CLIPPED_TO_ORTHOCUBE WCS=(100,200,300)",
    "2026-11-10 STEP10_MEMBER_ANNOTATIONS_CHECK=PASS"
};
var views=new[]{"TOP","BOTTOM","LEFT","RIGHT","FRONT","BACK","NW","NE","SW","SE"};
foreach(string view in views){
    log.Add("2026-11-10 STEP8_"+view+"_ACTUAL_VIEWPORT=(118.8,723.26,0) SIZE=(27.369,42.084,0) SCALE=0.03333");
    log.Add("2026-11-10 STEP10_MEMBER_LABEL="+view+
        " SOURCE=XREF:MODEL/XREF:73D:TEST6/409 SECOND_LINE_PRESENT=True PIPE_BOP=True TEXT=LINE NO: TEST-001 | BOP EL. +3899.9543 mm"+
        " ANCHOR=(105.12,400.22,0) LANE_X=115 LABEL=(117,405,0)");
}
M.Manifest parsed=M.Parse(log);
if(parsed.Sources.Count!=1||parsed.Labels.Count!=10||
   parsed.Views.Count!=10||parsed.Sources[0].Bop!="BOP EL. +3899.9543 mm"||
   parsed.Sources[0].LineTag!="TEST-001"||parsed.Labels[0].Key!=parsed.Sources[0].Key)
    throw new Exception("REV22.30 valid manifest extraction failed.");
var duplicate=new List<string>(log){log[^2]};
bool failed=false;
try{M.Parse(duplicate);}catch(FormatException){failed=true;}
if(!failed)throw new Exception("REV22.30 duplicate labels must fail closed.");
var missing=new List<string>(log);
missing.RemoveAt(missing.FindIndex(x=>x.Contains("STEP10_MEMBER_ANNOTATIONS_CHECK")));
failed=false;
try{M.Parse(missing);}catch(FormatException){failed=true;}
if(!failed)throw new Exception("REV22.30 incomplete original annotation check must fail closed.");
Console.WriteLine("REV22.30 XREF manifest parsing tests PASS");
