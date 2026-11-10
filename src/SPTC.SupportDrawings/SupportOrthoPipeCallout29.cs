using System;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.29: only the two user-visible pipe DBText lines are compacted.
/// Full XREF provenance/handle is retained in the source diagnostics and
/// annotation identity (never printed into the pipe's drawing label).
/// </summary>
public static class SupportOrthoPipeCallout29
{
    public static string Compose(string? lineNumberTag, string? bopLabel)
    {
        string tag=OneLine(lineNumberTag,"Not available");
        string bop=OneLine(bopLabel,"BOP: N/A");
        return "LINE NO: "+tag+"\n"+bop;
    }

    private static string OneLine(string? value,string fallback)
    {
        if(string.IsNullOrWhiteSpace(value))return fallback;
        return value.Replace('\r',' ').Replace('\n',' ').Trim();
    }
}
