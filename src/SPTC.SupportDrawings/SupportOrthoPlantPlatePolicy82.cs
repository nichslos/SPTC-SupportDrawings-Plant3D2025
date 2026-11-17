using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// Source identity and dimension-scope policy for Autodesk Plant 3D's
/// native StructurePlate (the PLANTSTEELPLATE plate/grating command).
/// A generic AutoCAD solid, block, material or piping component whose
/// name merely contains "plate" is NOT sufficient evidence.
/// </summary>
public static class SupportOrthoPlantPlatePolicy82
{
    public static bool IsNativePlate(string? managedType,
        string? runtimeClass,string? pnpClass)
    {
        static string Canonical(string? s)=>
            new string((s??"").Where(char.IsLetterOrDigit).ToArray())
                .ToUpperInvariant();
        string pnp=Canonical(pnpClass);
        if(pnp=="STRUCTUREPLATE")return true;
        string managed=Canonical(managedType);
        string rx=Canonical(runtimeClass);
        return managed=="STRUCTUREPLATE"||
            managed=="ACPPDB3DSTRUCTUREPLATE"||
            rx=="ACPPDB3DSTRUCTUREPLATE"||
            rx=="STRUCTUREPLATE";
    }

    public static bool IsMeasuredEnvelope(
        SupportOrthoAllViewDimPlan44.Member plate)
    {
        if(!SupportOrthoNativeDimensionPolicy48.IsNativeKey(plate.Key)||
           plate.Kind!="PLATE")return false;
        double[] v={plate.MinX,plate.MinY,plate.MinZ,
            plate.MaxX,plate.MaxY,plate.MaxZ};
        return v.All(double.IsFinite)&&
            plate.MinX<=plate.MaxX&&
            plate.MinY<=plate.MaxY&&
            plate.MinZ<=plate.MaxZ&&
            (plate.MaxX>plate.MinX||
             plate.MaxY>plate.MinY||
             plate.MaxZ>plate.MinZ);
    }
}
