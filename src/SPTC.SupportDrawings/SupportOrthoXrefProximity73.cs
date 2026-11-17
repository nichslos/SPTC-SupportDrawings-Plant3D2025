using System;
using System.Globalization;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV73 read-only geometry diagnostics. All boxes are measured in the
/// source project's HOST WCS after nested XREF transforms. Distances are
/// reported in millimetres but NEVER used to change selection or OrthoCubes.
/// No fixed user DWG, coordinates, proximity values or support dimensions.
/// </summary>
internal static class SupportOrthoXrefProximity73
{
    private static bool Overlap(double a0,double a1,double b0,double b1) =>
        a1>=b0 && a0<=b1;

    private static double Gap(double a0,double a1,double b0,double b1) =>
        Math.Max(0.0,Math.Max(b0-a1,a0-b1));

    private static bool XY(BoundingBox3 a,BoundingBox3 b) =>
        Overlap(a.MinX,a.MaxX,b.MinX,b.MaxX) &&
        Overlap(a.MinY,a.MaxY,b.MinY,b.MaxY);

    private static bool Z(BoundingBox3 a,BoundingBox3 b) =>
        Overlap(a.MinZ,a.MaxZ,b.MinZ,b.MaxZ);

    internal static bool Intersects(BoundingBox3 a,BoundingBox3 b) =>
        XY(a,b) && Z(a,b);

    internal static string Exclusion(BoundingBox3 pipe,
        BoundingBox3 scan,BoundingBox3 cube)
    {
        if(!XY(pipe,scan)&&!Z(pipe,scan))
            return "SCAN_XY_AND_Z_EXCLUDED";
        if(!XY(pipe,scan))return "SCAN_XY_EXCLUDED";
        if(!Z(pipe,scan))return "SCAN_Z_EXCLUDED";
        if(!XY(pipe,cube)&&!Z(pipe,cube))
            return "ORTHOCUBE_XY_AND_Z_EXCLUDED";
        if(!XY(pipe,cube))return "ORTHOCUBE_XY_EXCLUDED";
        if(!Z(pipe,cube))return "ORTHOCUBE_Z_EXCLUDED";
        return "IN_SCAN_AND_ORTHOCUBE";
    }

    private static string Number(double value) =>
        value.ToString("0.###",CultureInfo.InvariantCulture);

    private static string Bounds(BoundingBox3 b) =>
        "["+Number(b.MinX)+","+Number(b.MinY)+","+Number(b.MinZ)+
        "]-["+Number(b.MaxX)+","+Number(b.MaxY)+","+Number(b.MaxZ)+"]";

    internal static double XYGapMm(BoundingBox3 a,BoundingBox3 b,double toMm)
    {
        double x=Gap(a.MinX,a.MaxX,b.MinX,b.MaxX);
        double y=Gap(a.MinY,a.MaxY,b.MinY,b.MaxY);
        return Math.Sqrt(x*x+y*y)*toMm;
    }

    internal static double ZGapMm(BoundingBox3 a,BoundingBox3 b,double toMm) =>
        Gap(a.MinZ,a.MaxZ,b.MinZ,b.MaxZ)*toMm;

    internal static string Describe(string key,string source,
        BoundingBox3 pipe,BoundingBox3 support,BoundingBox3 scan,
        BoundingBox3 cube,double toMm)
    {
        if(!double.IsFinite(toMm)||toMm<=0)
            throw new ArgumentOutOfRangeException(nameof(toMm));
        return "REV73_XREF_PIPE_GEOMETRY="+key+
            " SOURCE_FILE="+source+
            " COORDINATE_FRAME=HOST_WCS SOURCE=TRANSFORMED_NATIVE_GEOMETRIC_EXTENTS"+
            " SOURCE_UNITS_TO_MM="+Number(toMm)+
            " PIPE_WCS="+Bounds(pipe)+
            " SUPPORT_WCS="+Bounds(support)+
            " SCAN_WCS="+Bounds(scan)+
            " ORTHOCUBE_WCS="+Bounds(cube)+
            " PIPE_TO_SUPPORT_XY_GAP_MM="+Number(XYGapMm(pipe,support,toMm))+
            " PIPE_TO_SUPPORT_Z_GAP_MM="+Number(ZGapMm(pipe,support,toMm))+
            " PIPE_TO_SCAN_XY_GAP_MM="+Number(XYGapMm(pipe,scan,toMm))+
            " PIPE_TO_SCAN_Z_GAP_MM="+Number(ZGapMm(pipe,scan,toMm))+
            " PIPE_TO_ORTHOCUBE_XY_GAP_MM="+Number(XYGapMm(pipe,cube,toMm))+
            " PIPE_TO_ORTHOCUBE_Z_GAP_MM="+Number(ZGapMm(pipe,cube,toMm))+
            " INSIDE_SCAN="+Intersects(pipe,scan)+
            " INSIDE_ORTHOCUBE="+Intersects(pipe,cube)+
            " REJECT_REASON="+Exclusion(pipe,scan,cube)+
            " ACTION=DIAGNOSTIC_ONLY";
    }
}
