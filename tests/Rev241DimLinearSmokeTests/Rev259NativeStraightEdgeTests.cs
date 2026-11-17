using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev259NativeStraightEdgeTests
{
    private static void Check(bool pass,string reason)
    {
        if(!pass)throw new Exception("REV22.59: "+reason);
    }

    internal static void Run()
    {
        var camera=new SupportOrthoMemberCalloutPlan17.Camera(
            "TOP",180,650,150,160,0,0,1,
            464.14,251.395,0,1.0/20.0);
        var native=new SupportOrthoNativeEdgeGeometry59.Edge(
            "NATIVE:POST","STRUCTURE",
            0,400,0,100,400,0);
        var pa=SupportOrthoMemberCalloutPlan17.Project(
            camera,native.Ax,native.Ay,native.Az);
        var pb=SupportOrthoMemberCalloutPlan17.Project(
            camera,native.Bx,native.By,native.Bz);
        var region=new SupportOrthoSmartDimPlan46.Rect(
            Math.Min(pa.X,pb.X),Math.Min(pa.Y,pb.Y)-12,
            Math.Max(pa.X,pb.X),Math.Max(pa.Y,pb.Y)+12);
        var exact=SupportOrthoNativeEdgeGeometry59.Resolve(
            camera,"NATIVE:POST",true,region,new[]{native});
        Check(exact!=null&&
            exact.Provenance=="NATIVE_EXPLICIT_STRAIGHT_EDGE",
            "native 3D edge spanning projected profile is verified");
        Check(Math.Abs(exact!.Transverse-pa.Y)<1e-7&&
            Math.Abs(exact.To-exact.From-5.0)<1e-6,
            "projected edge witness and 100 mm / 1:20 width follow source");
        var changed=native with {Bx=75};
        var p75=SupportOrthoMemberCalloutPlan17.Project(
            camera,changed.Bx,changed.By,changed.Bz);
        var bounds75=region with {
            L=Math.Min(pa.X,p75.X),R=Math.Max(pa.X,p75.X)};
        var resized=SupportOrthoNativeEdgeGeometry59.Resolve(
            camera,"NATIVE:POST",true,bounds75,new[]{changed});
        Check(resized!=null&&
            Math.Abs(resized.To-resized.From-3.75)<1e-6,
            "changing native geometry changes actual projected profile width");
        Check(SupportOrthoNativeEdgeGeometry59.Resolve(
            camera,"NATIVE:POST",true,region,new[]{changed})==null,
            "old 100 mm bounds cannot be assigned a 75 mm physical edge");

        var oblique=native with {By=460};
        Check(SupportOrthoNativeEdgeGeometry59.Resolve(
            camera,"NATIVE:POST",true,region,new[]{oblique})==null,
            "sloped edge never masquerades as perpendicular profile datum");
        Check(SupportOrthoNativeEdgeGeometry59.Resolve(
            camera,"NATIVE:POST",true,region,new[]{native with {
                Key="NATIVE:OTHER"}})==null,
            "another member's physical edge cannot be copied onto this member");
        Check(!SupportOrthoNativeEdgeGeometry59.Valid(
            native with {Key="XREF_MEMBER:REF"} ),
            "external member edge is excluded at geometry validation");
        Check(!SupportOrthoNativeEdgeGeometry59.Valid(
            native with {Provenance="GEOMETRIC_BOUNDING_BOX"} ),
            "WCS bounding-box edges cannot claim native edge evidence");
        Check(!SupportOrthoNativeEdgeGeometry59.Valid(
            native with {Bx=double.NaN}),
            "nonfinite native geometry cannot enter dimensioning");

        // Planner: 928.28 mm overall and actual 100 mm terminal
        // member PROFILE, with edge witness offset well away from
        // bounding-box midpoint to prove geometry is controlling.
        var beam=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:BEAM","STRUCTURE",0,0,0,928.28,50,20);
        var post=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:POST","STRUCTURE",0,0,0,100,502.787,20);
        var without=SupportOrthoReferenceDimPlan47.Build(
            new[]{camera},new[]{beam,post},594,841,1);
        var with=SupportOrthoReferenceDimPlan47.Build(
            new[]{camera},new[]{beam,post},594,841,1,
            nativeEdges:new[]{native});
        var local=with.Dimensions.FirstOrDefault(x=>
            x.Horizontal&&x.Source.Contains("/MEMBER_NATIVE:POST"));
        Check(local!=null &&
            local.Provenance==
                "EXACT_NATIVE_PROJECTED_PROFILE_EDGE_NOT_CUT_LENGTH",
            "planner recognizes actual native straight edge as local witness");
        Check(Math.Abs(local!.Ay-pa.Y)<1e-7&&
            Math.Abs(local.By-pb.Y)<1e-7,
            "both local dimension extension lines start at the exact source edge");
        Check(local.Measurement=="100"&&
            SupportOrthoDynamicDimension492.MatchesDiagnostic(local),
            "verified geometry retains dynamic AutoCAD dimension value");
        Check(with.ExactNativeEdgeProfileDimensions>=1,
            "new exact-edge count is reported");
        Check(without.ExactNativeEdgeProfileDimensions==0,
            "legacy read without exact edges stays projected-envelope mode");
        Check(with.Dimensions.Any(d=>d.Source.EndsWith("/OVERALL")),
            "native overall dimensions preserved, unchanged by local geometry");

        var extent=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:POST","STRUCTURE",0,0,0,100,502.787,20);
        var manifest=new SupportOrthoDimensionUpdatePlan49.Manifest(
            SupportOrthoDimensionUpdatePlan49.ManifestVersion,
            "C:/SOURCE.dwg","Layout1",1,594,841,true,
            new[]{new SupportOrthoDimensionUpdatePlan49.NativeMember(
                "NATIVE:POST","STRUCTURE")},
            new[]{new SupportOrthoDimensionUpdatePlan49.View(
                "TOP","2AC",180,650,150,160)},"SOURCE_SIGNATURE",
            638985600000000000,1234567,"POSE",0,
            new[]{extent},null,new[]{native});
        SupportOrthoDimensionUpdatePlan49.Validate(manifest);
        string json=System.Text.Json.JsonSerializer.Serialize(manifest);
        var roundtrip=System.Text.Json.JsonSerializer.Deserialize<
            SupportOrthoDimensionUpdatePlan49.Manifest>(json)!;
        Check(roundtrip.NativeEdgeSnapshot?.Count==1&&
            SupportOrthoDimensionUpdatePlan49.CanReuseGeometrySnapshot(
                roundtrip,manifest.SourceWriteUtcTicks,
                manifest.SourceFileLength),
            "native edge evidence is retained in source-stamped Ortho cache");
        bool bad=false;
        try
        {
            SupportOrthoDimensionUpdatePlan49.Validate(
                manifest with {NativeEdgeSnapshot=new[]{native with {
                    Key="XREF_MEMBER:OTHER"}}});
        }
        catch(ArgumentException){bad=true;}
        Check(bad,"malicious XREF edge in Ortho manifest is rejected");
        bad=false;
        try
        {
            SupportOrthoDimensionUpdatePlan49.Validate(
                manifest with {NativeEdgeSnapshot=new[]{native with {
                    Key="NATIVE:UNSELECTED"}}});
        }
        catch(ArgumentException){bad=true;}
        Check(bad,"unselected native edge in cache is rejected");
        Check(!SupportOrthoDimensionUpdatePlan49.CanReuseGeometrySnapshot(
            roundtrip,manifest.SourceWriteUtcTicks+1,
            manifest.SourceFileLength),
            "changed source invalidates exact geometry cache");

        Console.WriteLine(
            "REV22.59 verified straight native edge, oblique/XREF rejection, 100/75 live geometry and fast Ortho cache PASS");
    }
}
