using System;
using System.Diagnostics;
using SPTC.SupportDrawings;

/// <summary>
/// No Plant 3D dependency: a successful native PLANTORTHOUPDATE must
/// settle before SPTC enters AutoCAD document context. Tests also
/// preserve the existing strict changed-model/owned-DIMLINEAR checks.
/// </summary>
internal static class OrthoUpdatePerformanceTests
{
    private static void Need(bool value,string why)
    {
        if(!value)throw new Exception("Ortho post-update: "+why);
    }

    internal static void Run()
    {
        long start=Stopwatch.Frequency*4;
        long before=start+Stopwatch.Frequency*200/1000;
        long after=start+Stopwatch.Frequency*650/1000;
        Need(!SupportOrthoUpdatePerformancePolicy.Ready(
            start,before,false,true),
            "post-update cannot start before native quiet window");
        Need(SupportOrthoUpdatePerformancePolicy.Ready(
            start,after,false,true),
            "eligible active document processes when native is settled");
        Need(!SupportOrthoUpdatePerformancePolicy.Ready(
            start,after,true,true),
            "never enter plugin refresh while native Ortho remains active");
        Need(!SupportOrthoUpdatePerformancePolicy.Ready(
            start,after,false,false),
            "never apply changes to an inactive/background Ortho document");
        Need(!SupportOrthoUpdatePerformancePolicy.Ready(
            0,after,false,true),
            "a missing native successful completion cannot authorize refresh");
        Need(!SupportOrthoUpdatePerformancePolicy.Ready(
            start,start-1,false,true),
            "clock regression cannot bypass native quiet window");
        Need(SupportOrthoUpdatePerformancePolicy.ElapsedMilliseconds(
            start,after)>=649,
            "native and plugin elapsed times use monotonic Stopwatch ticks");
        Need(SupportOrthoUpdatePerformancePolicy.ElapsedMilliseconds(
            0,after)==-1,
            "unknown native command duration is explicitly diagnosed");
        bool threw=false;
        try{SupportOrthoUpdatePerformancePolicy.Ready(
            start,after,false,true,-1);}
        catch(ArgumentOutOfRangeException){threw=true;}
        Need(threw,"quiet window may not be negative");

        // Existing fast-path policy remains strict: source file stamp,
        // view pose and owner count must all match or a rebuild is needed.
        var member=new SupportOrthoDimensionUpdatePlan49.NativeMember(
            "NATIVE:A4","STRUCTURE");
        var view=new SupportOrthoDimensionUpdatePlan49.View(
            "LEFT","2AC",118.8,588.7,86.376,42.234);
        var manifest=new SupportOrthoDimensionUpdatePlan49.Manifest(
            SupportOrthoDimensionUpdatePlan49.ManifestVersion,
            @"C:\Models\TEST5.dwg","Title Block",1,594,841,true,
            new[]{member},new[]{view},"CURRENT",100,200,"POSE",25);
        Need(SupportOrthoDimensionUpdatePlan49.CanSkipModelReload(
            manifest,100,200,"POSE",25),
            "intact SPTC dimensions and source metadata skip source DWG reads");
        Need(!SupportOrthoDimensionUpdatePlan49.CanSkipModelReload(
            manifest,101,200,"POSE",25),
            "source model changes must never be skipped");
        Need(!SupportOrthoDimensionUpdatePlan49.CanSkipModelReload(
            manifest,100,200,"POSE",24),
            "user-deleted owned dimensions must be detected and repaired");

        Console.WriteLine(
            "Stable native Ortho quiet scheduling, incomplete/active guards, exact timing and source-integrity fast path PASS");
    }
}
