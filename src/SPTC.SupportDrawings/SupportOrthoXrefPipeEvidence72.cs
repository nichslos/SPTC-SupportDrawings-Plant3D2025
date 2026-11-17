using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV72: source-evidence and reporting policy for real Plant 3D XREF pipes.
/// Type identities are taken from actual loaded source entities and their
/// Autodesk runtime/RX inheritance, never guessed from geometry, DWG names,
/// layer names, support handles or a user-specific list of pipe IDs.
/// A valid Ortho sheet can have zero *verified* pipe sources; report that
/// separately from general structure/annotation QA success.
/// </summary>
internal static class SupportOrthoXrefPipeEvidence72
{
    internal sealed record Counts(
        int ResolvedReferences,
        int UnresolvedReferences,
        int VerifiedPipeEntities,
        int NearbyPipeEntities,
        int InCubePipeCandidates,
        int AnnotatedPipeSources,
        int MetadataUnusable,
        int OutsideSearchRegion,
        bool ScanFailed);

    internal static bool VerifiedType(
        string? runtimeClass,string? rxClass,string? dxfClass,
        IEnumerable<string>? runtimeBaseTypes)
    {
        // Canonical Autodesk Plant pipe identity. Do not treat PipeSupport,
        // Valve, generic solids, proxies or descriptive filenames as pipes.
        return new[]{runtimeClass,rxClass,dxfClass}
            .Concat(runtimeBaseTypes??Array.Empty<string>())
            .Any(SupportOrthoPipeLabel27.IsNativePipeType);
    }

    internal static string Coverage(Counts c)
    {
        if(c.ScanFailed)return "SCAN_FAILED";
        if(c.UnresolvedReferences>0&&c.ResolvedReferences==0)
            return "XREF_NOT_LOADED";
        if(c.ResolvedReferences==0)
            return "NO_RESOLVED_XREF_SOURCE";
        if(c.VerifiedPipeEntities==0)
            return "NO_VERIFIED_NATIVE_PIPE_ENTITIES";
        if(c.NearbyPipeEntities==0)
            return "VERIFIED_PIPES_OUTSIDE_SCAN_REGION";
        if(c.InCubePipeCandidates==0)
            return "VERIFIED_PIPES_OUTSIDE_ORTHOCUBE";
        if(c.AnnotatedPipeSources==0)
            return "XREF_PIPE_METADATA_NOT_VERIFIED";
        if(c.AnnotatedPipeSources<c.InCubePipeCandidates)
            return "XREF_PIPE_ANNOTATION_COVERAGE_PARTIAL";
        return "XREF_PIPE_METADATA_COVERAGE_VERIFIED";
    }

    internal static bool FullyVerified(Counts c)=>
        Coverage(c)=="XREF_PIPE_METADATA_COVERAGE_VERIFIED";

    internal static bool NeedsReview(Counts c,bool xrefNamesEnabled)=>
        xrefNamesEnabled && c.ResolvedReferences>0 &&
        !FullyVerified(c);

    internal static string Explanation(Counts c)
    {
        return Coverage(c) switch
        {
            "SCAN_FAILED" =>
                "XREF pipe source scan failed; treat pipe coverage as unknown",
            "XREF_NOT_LOADED" =>
                "XREF references are unresolved/unloaded; reload the source DWGs",
            "NO_RESOLVED_XREF_SOURCE" =>
                "No loaded XREF source was traversed; check External References",
            "NO_VERIFIED_NATIVE_PIPE_ENTITIES" =>
                "Loaded XREFs contain no VERIFIED native Plant pipe entities; check source model and RX class/proxy diagnostics",
            "VERIFIED_PIPES_OUTSIDE_SCAN_REGION" =>
                "Source XREF pipes exist but are outside support-focused scan envelope",
            "VERIFIED_PIPES_OUTSIDE_ORTHOCUBE" =>
                "Nearby source XREF pipes exist but none intersects the native OrthoCube",
            "XREF_PIPE_METADATA_NOT_VERIFIED" =>
                "Source XREF pipe metadata could not establish Line Number Tag/BOP",
            "XREF_PIPE_ANNOTATION_COVERAGE_PARTIAL" =>
                "Some source XREF pipes lack verifiable metadata/annotations",
            _ => "Source XREF pipe identities and annotation metadata verified; actual Ortho visible geometry still needs visual confirmation"
        };
    }

    // Diagnostic data is evidence rather than an explicit requirement:
    // xref member-name toggle ON does not certify that pipe objects
    // actually exist in the referenced source drawings.
    internal static string Diagnostic(
        Counts c,bool xrefNamesEnabled)=>
        "REV72_XREF_PIPE_COVERAGE="+Coverage(c)+
        " VERIFIED_SOURCE_PIPES="+c.VerifiedPipeEntities+
        " NEARBY="+c.NearbyPipeEntities+
        " IN_ORTHOCUBE="+c.InCubePipeCandidates+
        " ANNOTATED="+c.AnnotatedPipeSources+
        " UNUSABLE_METADATA="+c.MetadataUnusable+
        " XREF_MEMBER_NAMES_ON="+xrefNamesEnabled+
        " NEEDS_REVIEW="+NeedsReview(c,xrefNamesEnabled);
}
