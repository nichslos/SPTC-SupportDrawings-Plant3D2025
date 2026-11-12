using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.49 pure, testable refresh policy. Only explicitly registered
/// SPTC-generated Ortho drawings can be refreshed. Never automatically
/// dimension XREF geometry or modify non-SPTC paper dimensions.
/// </summary>
public static class SupportOrthoDimensionUpdatePlan49
{
    public const string ManifestKey="SPTC_DIM_REFRESH49";
    public const string OwnerApp="SPTC_DIM_OWNER49";
    public const int ManifestVersion=1;

    public sealed record NativeMember(string Key,string Kind);
    public sealed record View(string Name,string Handle,
        double X,double Y,double W,double H);
    public sealed record Manifest(
        int Version,string SourcePath,string LayoutName,
        double UnitsToMm,double PaperWidth,double PaperHeight,
        bool ReferenceDetailDimensions,
        IReadOnlyList<NativeMember> Members,
        IReadOnlyList<View> Views,
        string LastSignature);

    public sealed record Candidate(string Handle,
        double X,double Y,double W,double H);

    public static void Validate(Manifest manifest)
    {
        if(manifest.Version!=ManifestVersion||
           string.IsNullOrWhiteSpace(manifest.SourcePath)||
           string.IsNullOrWhiteSpace(manifest.LayoutName)||
           !double.IsFinite(manifest.UnitsToMm)||manifest.UnitsToMm<=0||
           !double.IsFinite(manifest.PaperWidth)||manifest.PaperWidth<=0||
           !double.IsFinite(manifest.PaperHeight)||manifest.PaperHeight<=0||
           manifest.Members==null||manifest.Members.Count==0||
           manifest.Views==null||manifest.Views.Count==0)
            throw new ArgumentException("REV22.49 invalid/unsupported saved Ortho manifest.");
        if(manifest.Members.Any(m=>
                !SupportOrthoNativeDimensionPolicy48.IsNativeKey(m.Key)||
                (m.Kind!="STRUCTURE"&&m.Kind!="SUPPORT"))||
           manifest.Members.Select(m=>m.Key).Distinct(
                StringComparer.OrdinalIgnoreCase).Count()!=manifest.Members.Count)
            throw new ArgumentException("REV22.49 XREF or duplicate member in manifest.");
        if(manifest.Views.Any(v=>string.IsNullOrWhiteSpace(v.Name)||
                string.IsNullOrWhiteSpace(v.Handle)||
                !double.IsFinite(v.X)||!double.IsFinite(v.Y)||
                !double.IsFinite(v.W)||!double.IsFinite(v.H)||
                v.W<=0||v.H<=0)||
           manifest.Views.Select(v=>v.Name).Distinct(
                StringComparer.OrdinalIgnoreCase).Count()!=manifest.Views.Count)
            throw new ArgumentException("REV22.49 invalid saved Ortho viewport references.");
    }

    /// <summary>
    /// Reuse the original native viewport handle first; Plant 3D may
    /// replace the viewport during Update View, so permit an unambiguous
    /// near-position fallback. No name-only or Current View fallback.
    /// </summary>
    public static Candidate? Resolve(
        View saved,IReadOnlyList<Candidate> actual,
        ISet<string> alreadyUsed)
    {
        var match=actual.FirstOrDefault(x=>
            !alreadyUsed.Contains(x.Handle)&&
            x.Handle.Equals(saved.Handle,StringComparison.OrdinalIgnoreCase));
        if(match!=null)return match;
        var nearby=actual.Where(x=>
            !alreadyUsed.Contains(x.Handle)&&
            Math.Abs(x.X-saved.X)<2.0&&
            Math.Abs(x.Y-saved.Y)<2.0&&
            Math.Abs(x.W-saved.W)<3.0&&
            Math.Abs(x.H-saved.H)<3.0)
            .OrderBy(x=>Math.Pow(x.X-saved.X,2)+
                Math.Pow(x.Y-saved.Y,2)+
                Math.Pow(x.W-saved.W,2)+
                Math.Pow(x.H-saved.H,2))
            .ToArray();
        // Ambiguous viewport identity is never safe for auto rewriting.
        return nearby.Length==1?nearby[0]:null;
    }

    public static string Signature(
        IReadOnlyList<SupportOrthoDimLinearPlan41.Dimension> dimensions)
    {
        var rows=dimensions.Select(x=>string.Join("|",
            x.View,x.Source,x.Measurement,x.Provenance,
            x.Horizontal?"H":"V",
            N(x.Ax),N(x.Ay),N(x.Bx),N(x.By),
            N(x.LineX),N(x.LineY),
            N(x.PaperTextX??0),N(x.PaperTextY??0)))
            .OrderBy(s=>s,StringComparer.Ordinal).ToArray();
        var bytes=SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join("\n",rows)));
        return Convert.ToHexString(bytes);
    }

    public static bool NeedsReplace(string? lastSignature,
        string currentSignature,int currentlyOwned,
        int desiredCount) =>
        !string.Equals(lastSignature,currentSignature,
            StringComparison.Ordinal)||
        currentlyOwned!=desiredCount;

    private static string N(double d)=>
        Math.Round(d,5).ToString("0.#####",CultureInfo.InvariantCulture);
}
