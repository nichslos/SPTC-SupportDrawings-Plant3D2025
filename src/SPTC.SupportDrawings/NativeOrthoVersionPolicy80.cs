using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV80 pure safety policy for Plant's PnPDrawings.DwgVersion.
/// Version values are NEVER compiled in or guessed. Only existing
/// valid native Ortho drawings in the SAME active project may
/// supply the exact property value. This class performs no writes.
/// </summary>
internal static class NativeOrthoVersionPolicy80
{
    internal sealed record Drawing(
        long RowId,string? FullPath,string? GuidText,
        string? DwgVersion,bool VerifiedNative);

    internal static string ReferenceVersion(
        IReadOnlyList<Drawing> rows,string seed)
    {
        if(rows==null||rows.Count==0)
            throw new InvalidOperationException(
                "REV80: No Ortho PnPDrawings metadata. Create a native Ortho "+
                "using Project Manager in a project copy first.");
        var verified=rows.Where(r=>r.VerifiedNative&&
            !string.IsNullOrWhiteSpace(r.FullPath)&&
            !string.IsNullOrWhiteSpace(r.DwgVersion)).ToArray();
        var seedRow=verified.Where(r=>PathSame(r.FullPath,seed)).ToArray();
        if(seedRow.Length>1)
            throw new InvalidOperationException(
                "REV80: Multiple PnPDrawings rows resolve to the selected seed.");
        string? version=seedRow.Length==1?seedRow[0].DwgVersion:null;
        if(version==null)
        {
            string[] valid=verified.Select(x=>x.DwgVersion!.Trim())
                .Distinct(StringComparer.Ordinal).ToArray();
            if(valid.Length!=1)
                throw new InvalidOperationException(
                    "REV80: No unambiguous installed-version Ortho "+
                    "reference exists. Create one empty Ortho via the "+
                    "native Project Manager wizard in a project copy.");
            version=valid[0];
        }
        version=version.Trim();
        if(!int.TryParse(version,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out int number)||number<=0)
            throw new InvalidOperationException(
                "REV80: Existing native DwgVersion is not a positive "+
                "integer. Refusing to invent a project version.");
        return version;
    }

    internal static Drawing Target(IReadOnlyList<Drawing> rows,
        string targetFullPath,Guid registeredGuid)
    {
        if(registeredGuid==Guid.Empty)
            throw new InvalidOperationException(
                "REV80: Native drawing registration returned no GUID.");
        var byGuid=rows.Where(r=>
            Guid.TryParse(r.GuidText,out Guid g)&&
            g==registeredGuid).ToArray();
        if(byGuid.Length>1)
            throw new InvalidOperationException(
                "REV80: Duplicate native project drawing GUID.");
        if(byGuid.Length==1)
        {
            Drawing match=byGuid[0];
            if(match.FullPath!=null&&
                !PathSame(match.FullPath,targetFullPath))
                throw new InvalidOperationException(
                    "REV80: Drawing GUID belongs to a different DWG path.");
            if(match.RowId<=0)
                throw new InvalidOperationException("REV80: Invalid target PnPID.");
            return match;
        }
        var byPath=rows.Where(r=>
            PathSame(r.FullPath,targetFullPath)).ToArray();
        if(byPath.Length!=1||byPath[0].RowId<=0)
            throw new InvalidOperationException(
                "REV80: Cannot identify exactly one new registered "+
                "Ortho PnPDrawings row by native GUID or exact full path.");
        if(Guid.TryParse(byPath[0].GuidText,out Guid other)&&
           other!=registeredGuid)
            throw new InvalidOperationException(
                "REV80: Target row has a mismatching registration GUID.");
        return byPath[0];
    }

    internal static bool PathSame(string? a,string? b)=>
        !string.IsNullOrWhiteSpace(a)&&!string.IsNullOrWhiteSpace(b)&&
        string.Equals(a,b,StringComparison.OrdinalIgnoreCase);

    internal static bool RequiresWrite(string? actual,string expected)
    {
        if(string.IsNullOrWhiteSpace(actual))return true;
        if(!string.Equals(actual.Trim(),expected,
            StringComparison.Ordinal))
            throw new InvalidOperationException(
                "REV80: Target Ortho row already carries a conflicting "+
                "DwgVersion. Never overwrite existing metadata.");
        return false;
    }
}
