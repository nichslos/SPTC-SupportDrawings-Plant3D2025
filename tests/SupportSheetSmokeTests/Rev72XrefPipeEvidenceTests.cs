using System;
using SPTC.SupportDrawings;

/// <summary>
/// REV72 strict XREF pipe classification and negative coverage tests.
/// These are pure tests: no known user DWG, handle, paper size, support,
/// line tag or pipeline number is needed or embedded in production.
/// Actual AutoCAD XREF traversal and native Ortho rendering still
/// require Plant 3D workstation verification.
/// </summary>
internal static class Rev72XrefPipeEvidenceTests
{
    private static void Need(bool condition,string reason)
    {
        if(!condition)throw new Exception("REV72 pipe evidence: "+reason);
    }

    internal static void Run()
    {
        bool Real(string? managed,string? rx,string? dxf,
            params string[] ancestry)=>
            SupportOrthoXrefPipeEvidence72.VerifiedType(
                managed,rx,dxf,ancestry);
        Need(Real("Pipe",null,null)&&
             Real("ProxyEntity","AcPpDb3dPipe",null)&&
             Real("CustomManagedWrapper",null,null,"AcPpDb3dPipe")&&
             Real("Entity",null,"AcPpDb3dPipe"),
            "actual RX/DXF/managed/native-base Plant pipe identity accepted");
        foreach(string falsePipe in new[]{
            "PipeSupport","AcPpDb3dPipeSupport","BlockReference",
            "ProxyEntity","Solid3d","Line","AcPpDb3dValve",
            "PipeFitting","PipeRun","PipeLineTag"})
            Need(!Real(falsePipe,null,null),
                "unverified entity must not masquerade as native pipe: "+
                falsePipe);
        var noSources=new SupportOrthoXrefPipeEvidence72.Counts(
            0,0,0,0,0,0,0,0,false);
        Need(SupportOrthoXrefPipeEvidence72.Coverage(noSources)==
            "NO_RESOLVED_XREF_SOURCE",
            "no loaded XREFs cannot verify pipe coverage");
        var unresolved=noSources with{UnresolvedReferences=2};
        Need(SupportOrthoXrefPipeEvidence72.Coverage(unresolved)==
            "XREF_NOT_LOADED",
            "unloaded or unresolved XREF sources explicitly diagnosed");
        var empty=noSources with{ResolvedReferences=3};
        Need(SupportOrthoXrefPipeEvidence72.Coverage(empty)==
            "NO_VERIFIED_NATIVE_PIPE_ENTITIES"&&
            SupportOrthoXrefPipeEvidence72.NeedsReview(empty,true),
            "loaded structural-only XREFs must not falsely certify XREF pipes");
        var outside=empty with{VerifiedPipeEntities=4,
            OutsideSearchRegion=4};
        Need(SupportOrthoXrefPipeEvidence72.Coverage(outside)==
            "VERIFIED_PIPES_OUTSIDE_SCAN_REGION",
            "pipe source outside proximity cannot be counted nearby");
        var outsideCube=outside with{NearbyPipeEntities=2};
        Need(SupportOrthoXrefPipeEvidence72.Coverage(outsideCube)==
            "VERIFIED_PIPES_OUTSIDE_ORTHOCUBE",
            "inside scan is not the same as inside native OrthoCube");
        var unannotated=outsideCube with{InCubePipeCandidates=2};
        Need(SupportOrthoXrefPipeEvidence72.Coverage(unannotated)==
            "XREF_PIPE_METADATA_NOT_VERIFIED",
            "native source with unavailable Line/BOP must not pass");
        var partial=unannotated with{AnnotatedPipeSources=1,
            MetadataUnusable=1};
        Need(SupportOrthoXrefPipeEvidence72.Coverage(partial)==
            "XREF_PIPE_ANNOTATION_COVERAGE_PARTIAL"&&
            SupportOrthoXrefPipeEvidence72.NeedsReview(partial,true),
            "partial pipe annotations remain visibly incomplete");
        var verified=partial with{AnnotatedPipeSources=2,
            MetadataUnusable=0};
        Need(SupportOrthoXrefPipeEvidence72.FullyVerified(verified)&&
             !SupportOrthoXrefPipeEvidence72.NeedsReview(verified,true),
            "fully source-authenticated pipes pass metadata coverage only");
        Need(!SupportOrthoXrefPipeEvidence72.NeedsReview(empty,false),
            "structure filename checkbox off does not imply pipe requirement");
        var failed=verified with{ScanFailed=true};
        Need(SupportOrthoXrefPipeEvidence72.Coverage(failed)==
            "SCAN_FAILED"&&
            !SupportOrthoXrefPipeEvidence72.FullyVerified(failed),
            "actual scan exceptions must fail closed, not become zero-pipe success");
        Need(SupportOrthoXrefPipeEvidence72.Diagnostic(empty,true)
            .Contains("NEEDS_REVIEW=True",StringComparison.Ordinal)&&
             SupportOrthoXrefPipeEvidence72.Explanation(empty)
            .Contains("no VERIFIED native Plant pipe",
                StringComparison.Ordinal),
            "result reports absent verified XREF source pipes explicitly");
        Console.WriteLine("REV72 dynamic XREF RX/DXF/inheritance source "+
            "classification, zero/partial/verified metadata coverage, "+
            "unloaded/remote/cube distinctions and scan-fail-closed PASS");
    }
}
