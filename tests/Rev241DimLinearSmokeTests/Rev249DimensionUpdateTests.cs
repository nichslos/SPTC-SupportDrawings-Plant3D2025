using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev249DimensionUpdateTests
{
    internal static void Run()
    {
        var native=new SupportOrthoDimensionUpdatePlan49.NativeMember(
            "NATIVE:521","STRUCTURE");
        var target=new SupportOrthoDimensionUpdatePlan49.View(
            "TOP","2AC",118.8,723.26,63.732,48.134);
        var manifest=new SupportOrthoDimensionUpdatePlan49.Manifest(
            SupportOrthoDimensionUpdatePlan49.ManifestVersion,
            @"C:\PlantProject\Drawings\MODEL.dwg","Layout1",1,594,841,
            true,new[]{native},new[]{target},"");
        SupportOrthoDimensionUpdatePlan49.Validate(manifest);
        bool rejected=false;
        try {SupportOrthoDimensionUpdatePlan49.Validate(
            manifest with {Members=new[]{native with {
                Key="XREF_MEMBER:SUPP-1/401"}}});}
        catch(ArgumentException){rejected=true;}
        Check(rejected,"XREF source cannot enter persistent auto-update manifest");
        rejected=false;
        try {SupportOrthoDimensionUpdatePlan49.Validate(
            manifest with {Members=new[]{native,native}});}
        catch(ArgumentException){rejected=true;}
        Check(rejected,"duplicate native reference cannot be stored");
        var candidates=new[]{
            new SupportOrthoDimensionUpdatePlan49.Candidate(
                "2AC",118.8,723.26,63.732,48.134),
            new SupportOrthoDimensionUpdatePlan49.Candidate(
                "300",338.58,723.26,63.732,48.134)};
        var used=new System.Collections.Generic.HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var exact=SupportOrthoDimensionUpdatePlan49.Resolve(
            target,candidates,used);
        Check(exact?.Handle=="2AC","exact viewport handle is preferred");
        var replacement=new[]{
            new SupportOrthoDimensionUpdatePlan49.Candidate(
                "900",118.9,723.28,64.0,48.1)};
        var near=SupportOrthoDimensionUpdatePlan49.Resolve(
            target,replacement,used);
        Check(near?.Handle=="900",
            "native Update View can regenerate viewport ID with same frame");
        var ambiguous=new[]{
            replacement[0],
            replacement[0] with {Handle="901",X=119.0}};
        Check(SupportOrthoDimensionUpdatePlan49.Resolve(
            target,ambiguous,used)==null,
            "ambiguous replacement viewport aborts rather than using Current View");
        Check(SupportOrthoDimensionUpdatePlan49.Resolve(
            target,new[]{replacement[0]},new System.Collections.Generic.HashSet<string>(
                StringComparer.OrdinalIgnoreCase){"900"})==null,
            "a physical viewport cannot be matched to multiple logical views");

        var d=new SupportOrthoDimLinearPlan41.Dimension(
            "TOP","DETAIL:TOP/H/OVERALL","STRUCTURE",true,
            20,30,90,30,55,100,"1400",
            "REFERENCE_PROJECTED_OVERALL_NOT_CUT_LENGTH",55,102.5);
        var e=d with {View="FRONT",Source="DETAIL:FRONT/H/OVERALL"};
        var s1=SupportOrthoDimensionUpdatePlan49.Signature(new[]{d,e});
        var s2=SupportOrthoDimensionUpdatePlan49.Signature(new[]{e,d});
        Check(s1==s2,"unchanged plan is order-invariant");
        Check(!SupportOrthoDimensionUpdatePlan49.NeedsReplace(s1,s2,2,2),
            "repeat update does not create duplicate dimensions");
        Check(SupportOrthoDimensionUpdatePlan49.NeedsReplace(s1,s2,1,2),
            "one deleted SPTC-owned dimension triggers repair");
        Check(SupportOrthoDimensionUpdatePlan49.NeedsReplace(
            s1,SupportOrthoDimensionUpdatePlan49.Signature(
                new[]{d with {Measurement="1500"},e}),2,2),
            "model dimension change triggers atomic replacement");
        Check(!SupportOrthoNativeDimensionPolicy48.IsNativeKey(
            "XREF_MEMBER:SUPP-1/401"),
            "native-only rule retained for every auto-update");
        // REV22.49.3: same saved model/view layout and intact SPTC-owned
        // dimensions bypass expensive ReadDwgFile and geometry scanning.
        var camera=new SupportOrthoMemberCalloutPlan17.Camera(
            "TOP",118.8,723.26,63.732,48.134,
            0,0,1,7956.295,3484.858,0,1.0/30.0);
        string pose=SupportOrthoDimensionUpdatePlan49.ViewPoseSignature(
            new[]{camera},594,841);
        var cached=manifest with {
            SourceWriteUtcTicks=638985600000000000,
            SourceFileLength=12345678,
            LastViewPoseSignature=pose,
            LastOwnedDimensionCount=2};
        Check(SupportOrthoDimensionUpdatePlan49.CanSkipModelReload(
            cached,cached.SourceWriteUtcTicks,cached.SourceFileLength,
            pose,2),
            "unchanged source+camera+owned dims skips full model file read");
        Check(!SupportOrthoDimensionUpdatePlan49.CanSkipModelReload(
            manifest,cached.SourceWriteUtcTicks,cached.SourceFileLength,
            pose,2),
            "older 22.49 metadata without fast fields refreshes once");
        Check(!SupportOrthoDimensionUpdatePlan49.CanSkipModelReload(
            cached,cached.SourceWriteUtcTicks+1,cached.SourceFileLength,
            pose,2),
            "source model modification always requires refresh");
        Check(!SupportOrthoDimensionUpdatePlan49.CanSkipModelReload(
            cached,cached.SourceWriteUtcTicks,cached.SourceFileLength+1,
            pose,2),
            "source file size change also requires refresh");
        Check(!SupportOrthoDimensionUpdatePlan49.CanSkipModelReload(
            cached,cached.SourceWriteUtcTicks,cached.SourceFileLength,
            pose,1),
            "missing user-erased SPTC dimension gets repaired");
        Check(!SupportOrthoDimensionUpdatePlan49.CanSkipModelReload(
            cached,cached.SourceWriteUtcTicks,cached.SourceFileLength,
            SupportOrthoDimensionUpdatePlan49.ViewPoseSignature(
                new[]{camera with {Scale=1.0/40.0}},594,841),2),
            "native Ortho scale change invalidates fast-path state");
        Check(!SupportOrthoDimensionUpdatePlan49.CanSkipModelReload(
            cached,cached.SourceWriteUtcTicks,cached.SourceFileLength,
            SupportOrthoDimensionUpdatePlan49.ViewPoseSignature(
                new[]{camera with {ViewCenterX=7911.8}},594,841),2),
            "native view pan or target change invalidates fast-path");
        Check(!SupportOrthoDimensionUpdatePlan49.CanSkipModelReload(
            cached,cached.SourceWriteUtcTicks,cached.SourceFileLength,
            SupportOrthoDimensionUpdatePlan49.ViewPoseSignature(
                new[]{camera},594,800),2),
            "native drawing paper layout size change invalidates fast-path");
        Check(SupportOrthoDimensionUpdatePlan49.ViewPoseSignature(
            new[]{camera},594,841)==pose,
            "native viewport pose fingerprint deterministic");

        // REV22.50: when the model file is unchanged but the native
        // viewport moves/re-scales, reuse validated member geometry
        // instead of re-opening a multi-GB network Plant 3D DWG.
        var nativeBounds=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:521","STRUCTURE",
            0,0,0,828.28,50,100);
        var realEnds=new SupportOrthoDimLinearPlan41.Segment(
            "NATIVE:521","STRUCTURE",
            0,25,0,828.28,25,0,828.28,828.28,
            "EXPLICIT_MEMBER_ENDPOINTS");
        var snapshot=cached with {
            NativeExtentSnapshot=new[]{nativeBounds},
            NativeEndpointSnapshot=new[]{realEnds}};
        SupportOrthoDimensionUpdatePlan49.Validate(snapshot);
        Check(SupportOrthoDimensionUpdatePlan49.CanReuseGeometrySnapshot(
            snapshot,cached.SourceWriteUtcTicks,cached.SourceFileLength),
            "same source model can reproject native endpoints without DWG reload");
        Check(!SupportOrthoDimensionUpdatePlan49.CanReuseGeometrySnapshot(
            snapshot,cached.SourceWriteUtcTicks+1,cached.SourceFileLength),
            "new model file invalidates all prior native geometry snapshots");
        Check(!SupportOrthoDimensionUpdatePlan49.CanReuseGeometrySnapshot(
            snapshot,cached.SourceWriteUtcTicks,cached.SourceFileLength+1),
            "changed source file length also invalidates geometry snapshot");
        Check(!SupportOrthoDimensionUpdatePlan49.CanReuseGeometrySnapshot(
            cached,cached.SourceWriteUtcTicks,cached.SourceFileLength),
            "old REV22.49 manifests without geometry must reload source");
        string stored=System.Text.Json.JsonSerializer.Serialize(snapshot);
        var loaded=System.Text.Json.JsonSerializer.Deserialize<
            SupportOrthoDimensionUpdatePlan49.Manifest>(stored)!;
        Check(SupportOrthoDimensionUpdatePlan49.CanReuseGeometrySnapshot(
            loaded,cached.SourceWriteUtcTicks,cached.SourceFileLength)&&
            loaded.NativeEndpointSnapshot!.Count==1,
            "native-only geometry cache survives DWG manifest JSON roundtrip");
        bool rejectXrefCache=false;
        try
        {
            SupportOrthoDimensionUpdatePlan49.Validate(snapshot with {
                NativeEndpointSnapshot=new[]{realEnds with {
                    Key="XREF_MEMBER:SUPP-1/401"}}
            });
        }
        catch(ArgumentException) { rejectXrefCache=true; }
        Check(rejectXrefCache,"XREF endpoint cannot enter geometry cache");
        bool rejectIncomplete=false;
        try
        {
            SupportOrthoDimensionUpdatePlan49.Validate(snapshot with {
                NativeExtentSnapshot=new[]{
                    nativeBounds with {Key="NATIVE:NOT_SELECTED"}}
            });
        }
        catch(ArgumentException) { rejectIncomplete=true; }
        Check(rejectIncomplete,
            "unrelated native member cannot inject cached geometry");
        Console.WriteLine(
            "REV22.50 source-stamped native geometry snapshot and XREF safety PASS");

        Console.WriteLine(
            "REV22.49.3 model/viewport change detection and fast-path regression PASS");
        Console.WriteLine(
            "REV22.49 manifest, native-only update, view identity and duplicate suppression PASS");
    }

    private static void Check(bool yes,string message)
    {
        if(!yes)throw new Exception("REV22.49: "+message);
    }
}
