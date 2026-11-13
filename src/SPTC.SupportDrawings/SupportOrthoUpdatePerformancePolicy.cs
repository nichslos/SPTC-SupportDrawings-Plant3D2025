using System;
using System.Diagnostics;

namespace SPTC.SupportDrawings;

/// <summary>
/// Revision-independent, pure scheduling policy for a safe native Ortho
/// post-update. Never enter the expensive document/database refresh in the
/// same command event or while native Ortho generation remains active.
/// Coalesced updates for one drawing run once after a short UI idle window.
/// </summary>
public static class SupportOrthoUpdatePerformancePolicy
{
    // A short settling window is not an artificial AutoCAD performance
    // claim. It leaves a native Ortho command time to finish redraw / UI
    // work before our synchronous AutoCAD API operations are scheduled.
    public const int QuietAfterNativeMilliseconds=450;

    public static bool Ready(
        long lastSuccessfulNativeEndTimestamp,
        long nowTimestamp,
        bool nativeUpdateActive,
        bool documentIsActive,
        int quietMilliseconds=QuietAfterNativeMilliseconds)
    {
        if(quietMilliseconds<0)
            throw new ArgumentOutOfRangeException(nameof(quietMilliseconds));
        if(nativeUpdateActive||!documentIsActive||
           lastSuccessfulNativeEndTimestamp<=0||
           nowTimestamp<lastSuccessfulNativeEndTimestamp)
            return false;
        return Stopwatch.GetElapsedTime(
            lastSuccessfulNativeEndTimestamp,nowTimestamp)
            .TotalMilliseconds>=quietMilliseconds;
    }

    public static long ElapsedMilliseconds(long from,long to) =>
        from>0&&to>=from?
            (long)Stopwatch.GetElapsedTime(from,to).TotalMilliseconds:
            -1;
}
