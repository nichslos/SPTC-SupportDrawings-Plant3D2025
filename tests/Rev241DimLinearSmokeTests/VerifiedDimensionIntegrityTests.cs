using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SPTC.SupportDrawings;

/// <summary>
/// Regression for the observed TEST5 25 generated / 23 reported
/// discrepancy. Numbers and source identity are TEST DATA ONLY;
/// production logic does not hard-code projects, member handles,
/// dimension counts, or revision numbers.
/// </summary>
internal static class VerifiedDimensionIntegrityTests
{
    private static void Need(bool condition,string message)
    {
        if(!condition)throw new Exception("Native dimension integrity: "+message);
    }

    internal static void Run()
    {
        var native=new SupportOrthoDimensionUpdatePlan49.NativeMember(
            "NATIVE:8A4","STRUCTURE");
        var view=new SupportOrthoDimensionUpdatePlan49.View(
            "LEFT","2AC",118.8,588.7,86.376,42.234);
        // 25 individually identified DIMLINEARs with independently
        // verified numeric paper-witness scaling; this is deliberately
        // not a guessed live drawing dimension list.
        var verified=Enumerable.Range(0,25).Select(i=>
            new SupportOrthoDimLinearPlan41.Dimension(
                "LEFT","DETAIL:LEFT/V/MEMBER_NATIVE:8A4_TEST_"+i,
                "STRUCTURE",false,
                100+i*.1,200,100+i*.1,220,
                90+i*.1,210,"300",
                "REFERENCE_PROJECTED_MEMBER_ENVELOPE_NOT_CUT_LENGTH",
                88+i*.1,212,15)).ToArray();
        Need(verified.All(
            SupportOrthoDynamicDimension492.MatchesDiagnostic),
            "reference verification uses real witness/scale math");
        var stamped=new SupportOrthoDimensionUpdatePlan49.Manifest(
            SupportOrthoDimensionUpdatePlan49.ManifestVersion,
            @"C:\Models\TEST5.dwg","Title Block",1,594,841,
            true,new[]{native},new[]{view},
            SupportOrthoDimensionUpdatePlan49.Signature(verified),
            638985600000000000,12345678,"POSE",verified.Length,
            LastVerifiedDimensions:verified);
        SupportOrthoDimensionUpdatePlan49.Validate(stamped);
        Need(SupportOrthoDimensionUpdatePlan49.HasTrustedVerifiedPlan(stamped),
            "signed full dimension plan available for recovery");
        Need(SupportOrthoDimensionUpdatePlan49.CanRepairVerifiedDimensions(
            stamped,stamped.SourceWriteUtcTicks,stamped.SourceFileLength,
            "POSE",23),
            "missing two plugin-owned dims must trigger safe verified restoration");
        Need(!SupportOrthoDimensionUpdatePlan49.CanSkipModelReload(
            stamped,stamped.SourceWriteUtcTicks,stamped.SourceFileLength,
            "POSE",23),
            "fast path must not incorrectly accept only 23 of 25 owned dims");
        Need(SupportOrthoDimensionUpdatePlan49.CanSkipModelReload(
            stamped,stamped.SourceWriteUtcTicks,stamped.SourceFileLength,
            "POSE",25),
            "all 25 intact dims retain the inexpensive early exit");
        Need(!SupportOrthoDimensionUpdatePlan49.CanRepairVerifiedDimensions(
            stamped,stamped.SourceWriteUtcTicks,stamped.SourceFileLength,
            "POSE",25),
            "intact plans never rewrite dimensions");
        Need(!SupportOrthoDimensionUpdatePlan49.CanRepairVerifiedDimensions(
            stamped,stamped.SourceWriteUtcTicks+1,stamped.SourceFileLength,
            "POSE",23),
            "changed source model cannot reuse previous paper witnesses");
        Need(!SupportOrthoDimensionUpdatePlan49.CanRepairVerifiedDimensions(
            stamped,stamped.SourceWriteUtcTicks,stamped.SourceFileLength,
            "POSE_CHANGED",23),
            "native viewport pose changes invalidate restoration");
        Need(!SupportOrthoDimensionUpdatePlan49.CanRepairVerifiedDimensions(
            stamped,stamped.SourceWriteUtcTicks,stamped.SourceFileLength+1,
            "POSE",23),
            "source DWG length change invalidates restoration");
        Need(!SupportOrthoDimensionUpdatePlan49.CanRepairVerifiedDimensions(
            stamped with {LastSignature="invalid"},
            stamped.SourceWriteUtcTicks,stamped.SourceFileLength,
            "POSE",23),
            "dimension signature mismatch blocks untrusted snapshot");
        Need(!SupportOrthoDimensionUpdatePlan49.CanRepairVerifiedDimensions(
            stamped with {LastOwnedDimensionCount=23},
            stamped.SourceWriteUtcTicks,stamped.SourceFileLength,
            "POSE",23),
            "a silently downgraded manifest cannot claim the saved 25 plan");
        var xref=verified.ToArray();
        xref[0]=xref[0] with {
            Source="DETAIL:LEFT/XREF_MEMBER:FORBIDDEN"};
        var malicious=stamped with {
            LastVerifiedDimensions=xref,
            LastSignature=SupportOrthoDimensionUpdatePlan49.Signature(xref)};
        Need(!SupportOrthoDimensionUpdatePlan49.HasTrustedVerifiedPlan(
            malicious),
            "XREF dimension cannot enter fast owner-only restoration");

        string json=JsonSerializer.Serialize(stamped);
        var roundtrip=JsonSerializer.Deserialize<
            SupportOrthoDimensionUpdatePlan49.Manifest>(json)!;
        Need(roundtrip.LastVerifiedDimensions?.Count==25&&
            SupportOrthoDimensionUpdatePlan49.CanRepairVerifiedDimensions(
                roundtrip,roundtrip.SourceWriteUtcTicks,
                roundtrip.SourceFileLength,"POSE",23),
            "persisted DWG JSON must preserve complete verified plan");

        var legacy=stamped with {LastVerifiedDimensions=null};
        Need(!SupportOrthoDimensionUpdatePlan49.CanRepairVerifiedDimensions(
            legacy,legacy.SourceWriteUtcTicks,legacy.SourceFileLength,
            "POSE",23),
            "legacy drawings cannot invent an unrecorded 25-dim plan");
        Need(SupportOrthoDimensionUpdatePlan49.CanSkipModelReload(
            legacy,legacy.SourceWriteUtcTicks,legacy.SourceFileLength,
            "POSE",25),
            "legacy backward-compatible fast-path behavior remains");

        Console.WriteLine(
            "Verified dimension count mismatch, source/pose safety, XREF exclusion, JSON persistence, legacy compatibility PASS");
    }
}
