using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev282PlantSteelPlateDimensionsTests
{
    private static void Need(bool ok,string reason)
    {
        if(!ok)throw new Exception("REV82 PLANTSTEELPLATE: "+reason);
    }

    internal static void Run()
    {
        Need(SupportOrthoPlantPlatePolicy82.IsNativePlate(
            "AcPpDb3dStructurePlate","",null),
            "native Plant StructurePlate managed type is recognized");
        Need(SupportOrthoPlantPlatePolicy82.IsNativePlate(
            "ProxyEntity","AcPpDb3dStructurePlate",null),
            "native Plant structural plate RX class is recognized");
        Need(SupportOrthoPlantPlatePolicy82.IsNativePlate(
            "ProxyEntity","AcDbProxyEntity","StructurePlate"),
            "verified PnP StructurePlate class can identify a native wrapper");
        Need(!SupportOrthoPlantPlatePolicy82.IsNativePlate(
            "Solid3d","AcDb3dSolid",null)&&
             !SupportOrthoPlantPlatePolicy82.IsNativePlate(
                "Pipe","AcPpDb3dPipe","Plate")&&
             !SupportOrthoPlantPlatePolicy82.IsNativePlate(
                "BlockReference","AcDbBlockReference",null),
            "generic solids, piping objects and blocks are NOT guessed as steel plates");

        var plate=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:PLATE-01","PLATE",80,-50,5,240,60,16);
        Need(SupportOrthoPlantPlatePolicy82.IsMeasuredEnvelope(plate)&&
            SupportOrthoNativeDimensionPolicy48.AcceptMember(plate),
            "real measured host WCS plate is eligible for native projected dimensions");
        Need(!SupportOrthoNativeDimensionPolicy48.AcceptMember(
                plate with {Key="XREF_MEMBER:PLATE-01"})&&
             !SupportOrthoPlantPlatePolicy82.IsMeasuredEnvelope(
                plate with {MaxX=double.NaN}),
            "foreign XREF or nonfinite plate extents are never dimensioned");

        // These numbers are standalone synthetic smoke-test geometry
        // (NOT code defaults for plate size). Only source-derived
        // runtime extents determine paper dimensions in production.
        var top=new SupportOrthoMemberCalloutPlan17.Camera(
            "TOP",180,650,150,160,0,0,1,0,0,0,1.0/20);
        var first=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:MEMBER_A","STRUCTURE",0,-80,0,720,80,140);
        var second=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:MEMBER_B","STRUCTURE",720,-80,0,820,80,140);
        var topPlan=SupportOrthoReferenceDimPlan47.Build(
            new[]{top},new[]{first,second,plate},594,841,1);
        Need(topPlan.OverallDimensions>=2,
            "adding a native plate does not remove existing overall dimensions");
        Need(topPlan.Dimensions.Any(d=>d.Kind=="PLATE"&&
                d.Source.Contains("PLATE_",StringComparison.Ordinal)),
            "at least one safe native plate DIMLINEAR must be planned; "+
            "dims="+string.Join(",",topPlan.Dimensions.Select(d=>d.Source))+
            ", skips="+string.Join(";",topPlan.Skipped.Where(x=>
                x.Contains("PLATE",StringComparison.OrdinalIgnoreCase)||
                x.Contains("POST_AUDIT",StringComparison.OrdinalIgnoreCase)||
                x.Contains("OPTIONAL_PROFILE",StringComparison.OrdinalIgnoreCase))));
        Need(topPlan.UnintendedAcceptedWitnessIntersections==0,
            "native plate dimensions never bypass witness intersection safety");
        Need(topPlan.Dimensions.All(d=>
            d.Source.Contains("PLATE_")?
                d.Kind=="PLATE"&&
                d.Provenance==
                "NATIVE_STRUCTURE_PLATE_PROJECTED_ENVELOPE_NOT_CUT_LENGTH":
                true),
            "plate dimensions are expressly projected, never fabrication cut sizes");

        var front=new SupportOrthoMemberCalloutPlan17.Camera(
            "FRONT",180,450,150,160,0,-1,0,0,0,0,1.0/20);
        var frontPlan=SupportOrthoReferenceDimPlan47.Build(
            new[]{front},new[]{first,second,plate},594,841,1);
        Need(frontPlan.Dimensions.All(d=>
            !d.Source.Contains("PLATE_")||
                d.Provenance.Contains("PLATE_PROJECTED_ENVELOPE",
                    StringComparison.Ordinal)),
            "plate thickness/height is measured in actual saved orthographic projection");
        var xrefPlate=plate with {Key="XREF_MEMBER:FOREIGN_PLATE"};
        var xrefPlan=SupportOrthoReferenceDimPlan47.Build(
            new[]{top},new[]{first,second,xrefPlate},594,841,1);
        Need(!xrefPlan.Dimensions.Any(d=>d.Source.Contains("PLATE_"))&&
             xrefPlan.Skipped.Any(x=>x.Contains("XREF_OR_NONNATIVE")),
            "an external plate cannot inject DIMLINEAR or geometry stations");

        Console.WriteLine(
            "REV82 native PlantSteelPlate class, real projected envelope, "+
            "native-only policy and no unsafe witness crossings PASS");
    }
}
