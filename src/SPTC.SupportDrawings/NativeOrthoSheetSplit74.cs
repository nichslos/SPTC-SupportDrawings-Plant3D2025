using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV74: opt-in, deterministic partition of a native view selection after
/// the existing one-sheet exact-scale preflight has proved NO_FIT.
/// This does not move viewports, reduce scale, change native cameras or
/// relax any per-sheet physical border/title/dimension checks.
/// </summary>
internal static class NativeOrthoSheetSplit74
{
    internal sealed record Halves(
        IReadOnlyList<string> First,IReadOnlyList<string> Second);

    internal static Halves Bisect(IReadOnlyList<string> names)
    {
        if(names==null || names.Count<2 || names.Count>10 ||
           names.Any(string.IsNullOrWhiteSpace) ||
           names.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=names.Count)
            throw new ArgumentException(
                "REV74 requires two or more unique standard native Ortho view names.");
        int half=(names.Count+1)/2;
        return new Halves(names.Take(half).ToArray(),
            names.Skip(half).ToArray());
    }

    internal static bool Eligible(bool enabled,int scaleDenominator,
        int viewCount,bool batchMode) =>
        enabled && scaleDenominator>0 && viewCount==10 && !batchMode;
}
