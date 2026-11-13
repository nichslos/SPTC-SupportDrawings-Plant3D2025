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

    // SDK build metadata can append SourceRevisionId to an explicit
    // Git SHA, producing "sha+sha". Normalize only exact duplicates;
    // retain ordinary semantic versions and legitimate build metadata.
    public static string NormalizeVersion(string? value)
    {
        string raw=value?.Trim()??"";
        int plus=raw.IndexOf('+');
        if(plus>0&&plus<raw.Length-1&&
           string.Equals(raw[..plus],raw[(plus+1)..],
               StringComparison.OrdinalIgnoreCase))
            return raw[..plus];
        return raw;
    }

    public static string Id
    {
        get
        {
            var info=NormalizeVersion(
                Assembly.GetCustomAttribute<
                    AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion);
            return info.Length>0?info:
                Assembly.GetName().Version?.ToString()??"unknown";
        }
    }

    public static string AssemblyName =>
        Assembly.GetName().Name??"SPTC.SupportDrawings";
}
