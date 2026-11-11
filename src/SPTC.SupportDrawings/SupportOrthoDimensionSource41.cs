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
/// created ONLY for actual StructureMember / Plant Support entity types with
/// explicit Start/End positions. AABB extents are never promoted to member
/// endpoints or interpreted as an engineering cut length.
/// </summary>
internal static class SupportOrthoDimensionSource41
{
    internal sealed record Segment(string Key,string Kind,
        double Ax,double Ay,double Az,double Bx,double By,double Bz,
        double SourceLength,double Millimeters);

    internal sealed record Result(IReadOnlyList<Segment> Segments,
        IReadOnlyList<string> Diagnostics);

    internal static Result Read(Database host,
        IEnumerable<ObjectId> native,
        IEnumerable<SupportOrthoXrefMemberDiscovery37.Item> xref,
        double unitsToMm)
    {
        if(!double.IsFinite(unitsToMm)||unitsToMm<=0)
            throw new ArgumentOutOfRangeException(nameof(unitsToMm));
        var segments=new List<Segment>();
        var diagnostics=new List<string>();
        foreach(var id in native.Distinct())
            TryRead(id,Matrix3d.Identity,"NATIVE:"+id.Handle,"",
                unitsToMm,segments,diagnostics);
        foreach(var member in xref)
            TryRead(member.Id,member.ToHost,member.Key,member.Kind,
                unitsToMm,segments,diagnostics);

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
            if(kind.Length==0 || (expectedKind.Length>0 &&
                !kind.Equals(expectedKind,StringComparison.Ordinal)))
                return;

            var a=ExplicitPoint(entity,true);
            var b=ExplicitPoint(entity,false);
            if(a==null||b==null)
            {
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
