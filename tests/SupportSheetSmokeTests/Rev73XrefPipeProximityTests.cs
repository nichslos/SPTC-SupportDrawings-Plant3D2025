using System;
using SPTC.SupportDrawings;

/// <summary>
/// REV73 synthetic geometry-only acceptance. No production sample path, pipe
/// handle, support dimensions or proximity constants are embedded.
/// </summary>
internal static class Rev73XrefPipeProximityTests
{
    private static void Need(bool condition,string why)
    {
        if(!condition)throw new Exception("REV73 geometry QA: "+why);
    }

    internal static void Run()
    {
        var support=new BoundingBox3(10,10,10,20,20,20);
        var scan=new BoundingBox3(0,0,0,30,30,30);
        var cube=new BoundingBox3(5,5,5,25,25,25);
        var xy=new BoundingBox3(40,12,15,50,17,19);
        var z=new BoundingBox3(15,15,40,16,16,45);
        var both=new BoundingBox3(40,15,40,45,16,45);
        var cubeXy=new BoundingBox3(26,12,12,27,16,16);
        var cubeZ=new BoundingBox3(12,12,26,16,16,27);
        var inside=new BoundingBox3(12,12,15,16,16,18);
        Need(SupportOrthoXrefProximity73.Exclusion(xy,scan,cube)==
            "SCAN_XY_EXCLUDED","XY-only scan rejection");
        Need(SupportOrthoXrefProximity73.Exclusion(z,scan,cube)==
            "SCAN_Z_EXCLUDED","Z-only scan rejection");
        Need(SupportOrthoXrefProximity73.Exclusion(both,scan,cube)==
            "SCAN_XY_AND_Z_EXCLUDED","combined scan rejection");
        Need(SupportOrthoXrefProximity73.Exclusion(cubeXy,scan,cube)==
            "ORTHOCUBE_XY_EXCLUDED","scan-in/cube-out XY distinction");
        Need(SupportOrthoXrefProximity73.Exclusion(cubeZ,scan,cube)==
            "ORTHOCUBE_Z_EXCLUDED","scan-in/cube-out Z distinction");
        Need(SupportOrthoXrefProximity73.Exclusion(inside,scan,cube)==
            "IN_SCAN_AND_ORTHOCUBE","genuine native OrthoCube inclusion");
        Need(!SupportOrthoXrefProximity73.Intersects(xy,scan)&&
             SupportOrthoXrefProximity73.Intersects(cubeXy,scan)&&
             !SupportOrthoXrefProximity73.Intersects(cubeXy,cube),
            "diagnostic intersection is geometry-based and independent of XREF names");
        Need(Math.Abs(SupportOrthoXrefProximity73.XYGapMm(xy,scan,25.4)-254)<1e-8,
            "XY distance uses actual source drawing unit scale");
        Need(Math.Abs(SupportOrthoXrefProximity73.ZGapMm(z,scan,25.4)-254)<1e-8,
            "Z separation uses actual source drawing unit scale");
        var edge=new BoundingBox3(30,10,10,33,12,13);
        Need(SupportOrthoXrefProximity73.Intersects(edge,scan),
            "boundary-contact is an intersection, not an exclusion");
        string report=SupportOrthoXrefProximity73.Describe(
            "XREF:source/handle","actual-referenced-file.dwg",xy,
            support,scan,cube,25.4);
        Need(report.Contains("PIPE_WCS=[40,12,15]-[50,17,19]",
                StringComparison.Ordinal)&&
             report.Contains("PIPE_TO_SCAN_XY_GAP_MM=254",
                StringComparison.Ordinal)&&
             report.Contains("INSIDE_SCAN=False",StringComparison.Ordinal)&&
             report.Contains("REJECT_REASON=SCAN_XY_EXCLUDED",
                StringComparison.Ordinal)&&
             report.Contains("ACTION=DIAGNOSTIC_ONLY",StringComparison.Ordinal),
            "host-WCS coordinate, measured gap and explicit reason report");
        Console.WriteLine("REV73 XREF pipe support/scan/cube XYZ gaps "+
            "and unchanged geometry eligibility PASS");
    }
}
