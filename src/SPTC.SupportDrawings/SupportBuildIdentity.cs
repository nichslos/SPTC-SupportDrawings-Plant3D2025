using System;
using System.Reflection;

namespace SPTC.SupportDrawings;

/// <summary>
/// Always identifies the actually loaded DLL, never a manually edited
/// revision constant. CI injects the checked-out commit automatically;
/// local builds report their assembly informational version instead.
/// Diagnostic keys and report filenames remain stable across builds.
/// </summary>
public static class SupportBuildIdentity
{
    private static readonly Assembly Assembly=
        typeof(SupportBuildIdentity).Assembly;

    public static string Id =>
        Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion?.Trim() is {Length:>0} info
                ? info
                : Assembly.GetName().Version?.ToString()??"unknown";

    public static string AssemblyName =>
        Assembly.GetName().Name??"SPTC.SupportDrawings";
}
