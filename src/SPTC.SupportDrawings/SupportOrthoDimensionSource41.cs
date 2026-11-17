using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.41 READ-ONLY verified linear endpoint extraction. Dimensions are
/// uses declared Start/End positions for member LENGTH dimensions. REV22.43
/// adds a conservative overall-Z-height dimension for recognized vertical
/// Plant support/structure entities lacking explicit endpoints. AABB height
/// is explicitly identified as OVERALL_HEIGHT, NEVER as cut/member length.
/// </summary>
internal static class SupportOrthoDimensionSource41
{
    internal sealed record Segment(string Key,string Kind,
        double Ax,double Ay,double Az,double Bx,double By,double Bz,
        double SourceLength,double Millimeters,
        string Provenance="EXPLICIT_MEMBER_ENDPOINTS");

    internal sealed record Result(IReadOnlyList<Segment> Segments,
        IReadOnlyList<string> Diagnostics);

    internal static Result Read(Database host,
        IEnumerable<ObjectId> native,
        IEnumerable<SupportOrthoXrefMemberDiscovery37.Item> xref,
        double unitsToMm,
        IEnumerable<string>? verifiedStructureHandles=null,
        IEnumerable<string>? verifiedSupportHandles=null)
    {
        if(!double.IsFinite(unitsToMm)||unitsToMm<=0)
            throw new ArgumentOutOfRangeException(nameof(unitsToMm));
        var segments=new List<Segment>();
        var diagnostics=new List<string>();
        // Native member/support identity may have been verified by the
        // earlier PnP-aware source readers even if ObjectClass exposes an
        // undecorated Plant wrapper type. Only those vetted handle lists
        // can authorize a fallback classification.
        var known=new Dictionary<string,string>(
            StringComparer.OrdinalIgnoreCase);
        foreach(string handle in verifiedStructureHandles??Array.Empty<string>())
            known[handle]="STRUCTURE";
        foreach(string handle in verifiedSupportHandles??Array.Empty<string>())
            known[handle]="SUPPORT";
        foreach(var id in native.Distinct())
        {
            known.TryGetValue(id.Handle.ToString(),out string? expectedKind);
            TryRead(id,Matrix3d.Identity,"NATIVE:"+id.Handle,
                expectedKind??"",unitsToMm,segments,diagnostics);
        }
        // REV22.48: XREF dimensions are prohibited in ALL modes.
        // Keep the XREF input parameter for API compatibility with
        // older callers, but never extract external Start/End or height
        // dimensions. XREF label discovery remains a separate subsystem.
        int externalCount=xref?.Count()??0;
        if(externalCount>0)
            diagnostics.Add("DIM48_XREF_SOURCE_SEGMENTS_EXCLUDED="+
                externalCount);

        return new Result(segments,diagnostics);
    }

    private static void TryRead(ObjectId id,Matrix3d toHost,
        string key,string expectedKind,double mm,
        List<Segment> segments,List<string> notes)
    {
        try
        {
            if(id.IsNull||!id.IsValid||id.IsErased)
            {
                notes.Add("DIM_SOURCE_INVALID_ID="+key);
                return;
            }
            using var tr=id.Database.TransactionManager.StartTransaction();
            var entity=tr.GetObject(id,OpenMode.ForRead,false) as Entity;
            if(entity==null)return;
            string type=entity.GetType().Name;
            string rx=id.ObjectClass?.Name??"";
            string kind=
                type.Contains("StructureMember",StringComparison.OrdinalIgnoreCase)||
                rx.Contains("StructureMember",StringComparison.OrdinalIgnoreCase)
                    ? "STRUCTURE"
                    : (SupportOrthoSupportLabel24.IsSupportClass(type)||
                       SupportOrthoSupportLabel24.IsSupportClass(rx)||
                       type.Contains("PipeSupport",StringComparison.OrdinalIgnoreCase)||
                       rx.Contains("PipeSupport",StringComparison.OrdinalIgnoreCase)
                        ? "SUPPORT" : "");
            // The PnP-aware readers have already accepted known native
            // objects. Never guess an unknown object from its geometry alone.
            if(kind.Length==0 && expectedKind.Length>0)
                kind=expectedKind;
            if(kind.Length==0 || (expectedKind.Length>0 &&
                !kind.Equals(expectedKind,StringComparison.Ordinal)))
                return;

            var a=ExplicitPoint(entity,true);
            var b=ExplicitPoint(entity,false);
            if(a==null||b==null)
            {
                // REV22.43: This is a dimension of the ENTIRE measured
                // geometric Z envelope, not an assumed fabrication length.
                // Only known Plant structure/support with clear vertical
                // dominance and usable geometry qualify.
                if(TryVerticalOverallHeight(entity,toHost,key,kind,mm,
                    segments,notes))return;
                notes.Add("DIM_ENDPOINTS_NOT_DECLARED="+key+" KIND="+kind);
                return;
            }
            Point3d start=a.Value.TransformBy(toHost);
            Point3d end=b.Value.TransformBy(toHost);
            if(!Finite(start)||!Finite(end))
            {
                notes.Add("DIM_ENDPOINTS_NONFINITE="+key);
                return;
            }
            double length=start.DistanceTo(end);
            if(!double.IsFinite(length)||length*mm<1.0)
            {
                notes.Add("DIM_LENGTH_INVALID_OR_TOO_SHORT="+key);
                return;
            }
            // Source extents verification rejects component positions that
            // belong to a different coordinate system or transform.
            Extents3d ext=entity.GeometricExtents;
            ext.TransformBy(toHost);
            double tolerance=Math.Max(length*.03,1.0/mm);
            if(!Inside(start,ext,tolerance)||!Inside(end,ext,tolerance))
            {
                notes.Add("DIM_ENDPOINTS_OUTSIDE_NATIVE_EXTENTS="+key);
                return;
            }
            segments.Add(new Segment(key,kind,start.X,start.Y,start.Z,
                end.X,end.Y,end.Z,length,length*mm));
        }
        catch(System.Exception ex)
        {
            notes.Add("DIM_SOURCE_READ_SKIPPED="+key+":"+ex.GetType().Name);
        }
    }

    private static bool TryVerticalOverallHeight(Entity entity,
        Matrix3d toHost,string key,string kind,double unitsToMm,
        List<Segment> segments,List<string> notes)
    {
        try
        {
            Extents3d ext=entity.GeometricExtents;
            ext.TransformBy(toHost);
            double x=ext.MaxPoint.X-ext.MinPoint.X;
            double y=ext.MaxPoint.Y-ext.MinPoint.Y;
            double z=ext.MaxPoint.Z-ext.MinPoint.Z;
            if(!double.IsFinite(x)||!double.IsFinite(y)||
               !double.IsFinite(z)||x<0||y<0||z<=0)
            {
                notes.Add("DIM_OVERALL_HEIGHT_BAD_EXTENTS="+key);
                return false;
            }
            // Keep interpretation explicitly conservative. A wide/sloping
            // assembly may have a real Z envelope, but should not be
            // labeled as a single upright component without evidence.
            // This is an *overall height* dimension, not member LENGTH.
            if(!SupportOrthoDimLinearPlan41.AcceptOverallZHeight(
                x,y,z,unitsToMm))
            {
                notes.Add("DIM_OVERALL_HEIGHT_NOT_VERTICAL_DOMINANT="+key);
                return false;
            }
            double cx=(ext.MinPoint.X+ext.MaxPoint.X)*.5;
            double cy=(ext.MinPoint.Y+ext.MaxPoint.Y)*.5;
            var bottom=new Point3d(cx,cy,ext.MinPoint.Z);
            var top=new Point3d(cx,cy,ext.MaxPoint.Z);
            if(!Finite(bottom)||!Finite(top))return false;
            segments.Add(new Segment(key,kind,bottom.X,bottom.Y,bottom.Z,
                top.X,top.Y,top.Z,z,z*unitsToMm,
                "GEOMETRIC_OVERALL_Z_HEIGHT"));
            notes.Add("DIM_OVERALL_HEIGHT_VERIFIED="+key+
                " KIND="+kind+" HEIGHT_MM="+
                (z*unitsToMm).ToString("0.###",
                    System.Globalization.CultureInfo.InvariantCulture)+
                " SOURCE=ENTITY_WCS_EXTENTS_NOT_CUT_LENGTH");
            return true;
        }
        catch(System.Exception ex)
        {
            notes.Add("DIM_OVERALL_HEIGHT_FAILED="+key+":"+
                ex.GetType().Name);
            return false;
        }
    }

    private static bool Finite(Point3d p) =>
        double.IsFinite(p.X)&&double.IsFinite(p.Y)&&double.IsFinite(p.Z);

    private static bool Inside(Point3d p,Extents3d b,double t) =>
        p.X>=b.MinPoint.X-t && p.X<=b.MaxPoint.X+t &&
        p.Y>=b.MinPoint.Y-t && p.Y<=b.MaxPoint.Y+t &&
        p.Z>=b.MinPoint.Z-t && p.Z<=b.MaxPoint.Z+t;

    private static Point3d? ExplicitPoint(object obj,bool start)
    {
        string[] names=start
            ? new[]{"StartPoint","StartPosition","PositionStart","Start"}
            : new[]{"EndPoint","EndPosition","PositionEnd","End"};
        foreach(string name in names)
        {
            try
            {
                PropertyInfo? prop=obj.GetType().GetProperty(name,
                    BindingFlags.Public|BindingFlags.Instance|
                    BindingFlags.IgnoreCase);
                if(AsPoint(prop?.GetValue(obj)) is Point3d point)
                    return point;
            }
            catch { }
        }
        try
        {
            foreach(PropertyDescriptor pd in TypeDescriptor.GetProperties(obj))
            {
                if(!names.Any(x=>Normalize(x)==Normalize(pd.Name)||
                        Normalize(x)==Normalize(pd.DisplayName)))continue;
                if(AsPoint(pd.GetValue(obj)) is Point3d point)return point;
            }
        }
        catch { }

        string which=start?"Start":"End";
        if(TryNumber(obj,which+"X",out double x) &&
           TryNumber(obj,which+"Y",out double y) &&
           TryNumber(obj,which+"Z",out double z))
            return new Point3d(x,y,z);
        return null;
    }

    private static Point3d? AsPoint(object? value) => value switch
    {
        Point3d p=>p,
        Vector3d p=>new Point3d(p.X,p.Y,p.Z),
        _=>null
    };

    private static bool TryNumber(object obj,string name,out double number)
    {
        number=0;
        try
        {
            PropertyInfo? prop=obj.GetType().GetProperty(name,
                BindingFlags.Public|BindingFlags.Instance|
                BindingFlags.IgnoreCase);
            object? value=prop?.GetValue(obj);
            if(value!=null &&
               double.TryParse(Convert.ToString(value,
                   System.Globalization.CultureInfo.InvariantCulture),
                   System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out number))
                return double.IsFinite(number);
            foreach(PropertyDescriptor pd in TypeDescriptor.GetProperties(obj))
            {
                if(Normalize(pd.Name)!=Normalize(name) &&
                   Normalize(pd.DisplayName)!=Normalize(name))continue;
                value=pd.GetValue(obj);
                if(value!=null &&
                   double.TryParse(Convert.ToString(value,
                       System.Globalization.CultureInfo.InvariantCulture),
                       System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture,
                       out number))
                    return double.IsFinite(number);
            }
        }
        catch { }
        return false;
    }

    private static string Normalize(string text) =>
        new(text.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant)
            .ToArray());
}
