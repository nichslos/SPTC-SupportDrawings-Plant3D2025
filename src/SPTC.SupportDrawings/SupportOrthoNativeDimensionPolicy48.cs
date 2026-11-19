using System;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.48 dimensioning boundary: only host DWG native Plant
/// StructureMember/Support identifiers are eligible. XREF model members
/// may remain visible and annotated but cannot contribute lengths, datum
/// stations, bounding overall dimensions, or paper-space DIMLINEAR entities.
/// </summary>
public static class SupportOrthoNativeDimensionPolicy48
{
    public static bool IsNativeKey(string? key) =>
        !string.IsNullOrWhiteSpace(key)&&
        key.StartsWith("NATIVE:",StringComparison.OrdinalIgnoreCase)&&
        !key.Contains("XREF",StringComparison.OrdinalIgnoreCase);

    public static bool AcceptMember(
        SupportOrthoAllViewDimPlan44.Member member) =>
        AcceptMember(member.Key,member.Kind);

    public static bool AcceptMember(string key,string kind) =>
        IsNativeKey(key)&&(kind=="STRUCTURE"||kind=="SUPPORT"||kind=="PLATE");

    internal const string SolidEndEdge="NATIVE_SOLID_END_EDGE";
    internal const string EndGrips="NATIVE_GRIPS_ON_VERIFIED_END_STATIONS";

    internal static bool VerifiedEndpoints(string provenance)=>
        provenance=="EXPLICIT_MEMBER_ENDPOINTS"||provenance==SolidEndEdge||provenance==EndGrips;

    public static bool AcceptSegment(
        SupportOrthoDimLinearPlan41.Segment segment) =>
        IsNativeKey(segment.Key)&&
        (segment.Kind=="STRUCTURE"||segment.Kind=="SUPPORT")&&
        VerifiedEndpoints(segment.Provenance);
}
