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
        IsNativeKey(member.Key)&&
        (member.Kind=="STRUCTURE"||member.Kind=="SUPPORT");

    public static bool AcceptSegment(
        SupportOrthoDimLinearPlan41.Segment segment) =>
        IsNativeKey(segment.Key)&&
        (segment.Kind=="STRUCTURE"||segment.Kind=="SUPPORT")&&
        segment.Provenance=="EXPLICIT_MEMBER_ENDPOINTS";
}
