using System;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV76 UI-only choice mapping. Existing native generator still receives
/// the same two REV74/REV75 flags; no scale, camera, clearance, seed,
/// physical sheet or project data is changed by selecting an output mode.
/// </summary>
internal static class SupportDrawingOutputMode76
{
    internal const string Physical="Single sheet — fit to paper";
    internal const string Draft="One DWG — draft overflow";
    internal const string Split="Multiple sheets — exact scale";
    internal enum Mode { Physical=0, Draft=1, Split=2 }

    internal static Mode FromIndex(int index) =>
        index switch { 0=>Mode.Physical, 1=>Mode.Draft, 2=>Mode.Split,
            _=>throw new ArgumentOutOfRangeException(nameof(index)) };

    internal static bool Eligible(Mode mode,int count,int denominator,
        bool batch) =>
        mode==Mode.Physical||
        (count==10&&denominator>0&&!batch);

    internal static bool BypassOneSheetLimit(Mode mode)=>mode==Mode.Split;
    internal static bool AllowDraftOverflow(Mode mode)=>mode==Mode.Draft;

    internal static string Explanation(Mode mode)=>mode switch
    {
        Mode.Physical =>
            "Registered paper boundaries stay enforced; no-fit drawings stop safely.",
        Mode.Draft =>
            "All ten native views remain in ONE DWG. Off-sheet drawing is DRAFT ONLY; native PDF disabled.",
        Mode.Split =>
            "Keep exact 1:N; when ten views cannot fit, produce independently validated sheets.",
        _=>throw new ArgumentOutOfRangeException(nameof(mode))
    };
}
