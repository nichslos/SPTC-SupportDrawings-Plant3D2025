using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// Stable native Ortho view names independent of development revisions.
/// The timestamp is a uniqueness suffix WITHIN each new Ortho drawing;
/// native view catalog and update logic keep the exact stored names.
/// </summary>
public static class SupportOrthoViewNaming
{
    private static readonly HashSet<string> NativeTypes=new(
        new[]{"TOP","BOTTOM","LEFT","RIGHT","FRONT","BACK",
              "NW","NE","SW","SE"},
        StringComparer.OrdinalIgnoreCase);

    public static string Name(string nativeType,string utcStamp)
    {
        if(string.IsNullOrWhiteSpace(nativeType)||
           !NativeTypes.Contains(nativeType))
            throw new ArgumentException(
                "Unrecognized native Ortho view type.",nameof(nativeType));
        if(utcStamp==null||utcStamp.Length!=6||
           !utcStamp.All(c=>c>='0'&&c<='9'))
            throw new ArgumentException(
                "Expected a six-digit UTC time stamp.",nameof(utcStamp));
        return "SPTC_"+nativeType.ToUpperInvariant()+"_"+utcStamp;
    }
}
