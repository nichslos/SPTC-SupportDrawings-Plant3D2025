using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.DatabaseServices;

namespace SPTC.SupportDrawings;

/// <summary>
/// Read-only native PLANTSTEELPLATE/StructurePlate discovery from the
/// explicitly selected host DWG entities. Verifies Plant's actual
/// native runtime class or a matching linked PnP StructurePlate class;
/// uses real WCS GeometricExtents only for PROJECTED ENVELOPE dimensions.
/// No standard plate dimensions, generated vertices or XREF members.
/// </summary>
internal static class SupportOrthoPlantPlateSource82
{
    internal sealed record Plate(string Handle,string Description,
        SupportOrthoAllViewDimPlan44.Member Geometry,
        double X,double Y,double Z);
    internal sealed record Result(
        IReadOnlyList<Plate> Plates,
        IReadOnlyList<string> Diagnostics);

    internal static Result Read(Database database,
        IReadOnlyCollection<ObjectId> selected)
    {
        var found=new List<Plate>();
        var notes=new List<string>();
        object? dlm=null;
        MethodInfo? find=null,get=null;
        try
        {
            Type app=AppDomain.CurrentDomain.GetAssemblies()
                .Select(a=>a.GetType(
                    "Autodesk.ProcessPower.PlantInstance.PlantApplication",
                    throwOnError:false))
                .FirstOrDefault(t=>t!=null) ??
                throw new InvalidOperationException(
                    "Active PlantApplication unavailable");
            object project=app.GetProperty("CurrentProject",
                BindingFlags.Public|BindingFlags.Static)?.GetValue(null) ??
                throw new InvalidOperationException("No active Plant project");
            object parts=project.GetType().GetProperty("ProjectParts")?
                .GetValue(project) ??
                throw new InvalidOperationException("No project parts");
            object piping=parts.GetType().GetProperty("Item")?
                .GetValue(parts,new object[]{"Piping"}) ??
                throw new InvalidOperationException("Piping part unavailable");
            dlm=piping.GetType().GetProperty("DataLinksManager")?
                .GetValue(piping);
            if(dlm==null)
                throw new InvalidOperationException(
                    "Native Piping DataLinksManager unavailable");
            find=dlm.GetType().GetMethods(
                BindingFlags.Public|BindingFlags.Instance)
                .FirstOrDefault(m=>m.Name=="FindAcPpRowId"&&
                    m.GetParameters().Length==1&&
                    m.GetParameters()[0].ParameterType==typeof(ObjectId));
            get=dlm.GetType().GetMethods(
                BindingFlags.Public|BindingFlags.Instance)
                .FirstOrDefault(m=>m.Name=="GetAllProperties"&&
                    m.GetParameters().Length==2&&
                    m.GetParameters()[0].ParameterType==typeof(int)&&
                    m.GetParameters()[1].ParameterType==typeof(bool));
            if(find==null||get==null)
                notes.Add("PLATE82_NATIVE_PNP_PROPERTIES_UNAVAILABLE");
        }
        catch(System.Exception ex)
        {
            notes.Add("PLATE82_DATALINKS_UNAVAILABLE="+
                ex.GetType().Name+":"+ex.Message);
        }
        using var tr=database.TransactionManager.StartTransaction();
        foreach(ObjectId id in selected.Distinct())
        {
            if(id.IsNull||!id.IsValid||id.IsErased)continue;
            try
            {
                if(tr.GetObject(id,OpenMode.ForRead,false) is not Entity entity)
                    continue;
                string managed=entity.GetType().Name;
                string rx=id.ObjectClass?.Name??"";
                string handle=id.Handle.ToString();
                string? rowClass=null;
                string? description=null;
                int rowId=0;
                if(dlm!=null&&find!=null&&get!=null)
                {
                    try
                    {
                        rowId=Convert.ToInt32(
                            find.Invoke(dlm,new object[]{id}));
                        if(rowId>0&&get.Invoke(dlm,new object[]{
                            rowId,true}) is IEnumerable rows)
                        {
                            foreach(object? pair in rows)
                            {
                                if(pair==null)continue;
                                string propertyName=pair.GetType()
                                    .GetProperty("Key")?
                                    .GetValue(pair)?.ToString()?.Trim()??"";
                                string val=pair.GetType().GetProperty("Value")?
                                    .GetValue(pair)?.ToString()?.Trim()??"";
                                if(propertyName.Equals("ClassName",
                                    StringComparison.OrdinalIgnoreCase)||
                                   propertyName.Equals("PnPClassName",
                                    StringComparison.OrdinalIgnoreCase))
                                    rowClass=val;
                                else if(propertyName.Equals("LongDescriptionSize",
                                        StringComparison.OrdinalIgnoreCase)||
                                    propertyName.Equals("Long Description (Size)",
                                        StringComparison.OrdinalIgnoreCase))
                                    description=val;
                            }
                        }
                    }
                    catch(System.Exception ex)
                    {
                        notes.Add("PLATE82_PNP_READ_SKIPPED="+handle+":"+
                            ex.GetType().Name);
                    }
                }
                if(!SupportOrthoPlantPlatePolicy82.IsNativePlate(
                    managed,rx,rowClass))
                    continue;
                var b=entity.GeometricExtents;
                string key="NATIVE:"+handle;
                var member=new SupportOrthoAllViewDimPlan44.Member(
                    key,"PLATE",
                    b.MinPoint.X,b.MinPoint.Y,b.MinPoint.Z,
                    b.MaxPoint.X,b.MaxPoint.Y,b.MaxPoint.Z);
                if(!SupportOrthoPlantPlatePolicy82.IsMeasuredEnvelope(member))
                {
                    notes.Add("PLATE82_INVALID_NATIVE_EXTENTS="+handle);
                    continue;
                }
                string label=string.IsNullOrWhiteSpace(description)?
                    "Plant Structure Plate/Grating":description.Trim();
                var plate=new Plate(handle,label,member,
                    (member.MinX+member.MaxX)*.5,
                    (member.MinY+member.MaxY)*.5,
                    (member.MinZ+member.MaxZ)*.5);
                found.Add(plate);
                notes.Add("PLATE82_VERIFIED_NATIVE="+handle+
                    " CLASS="+(string.IsNullOrWhiteSpace(rowClass)?
                        rx:rowClass)+
                    " PNP_ROW="+(rowId>0?"PRESENT":"NOT_AVAILABLE")+
                    " GEOMETRY=ENTITY_WCS_PROJECTED_ENVELOPE_NOT_CUT_SIZE");
            }
            catch(System.Exception ex)
            {
                notes.Add("PLATE82_SOURCE_READ_SKIPPED="+id.Handle+
                    ":"+ex.GetType().Name);
            }
        }
        tr.Commit();
        notes.Add("PLATE82_NATIVE_COUNT="+found.Count);
        return new Result(found,notes);
    }
}
