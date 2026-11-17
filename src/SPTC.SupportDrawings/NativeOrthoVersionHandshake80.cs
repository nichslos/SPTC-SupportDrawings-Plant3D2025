using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV80 uses the HOST'S Plant DataLinksManager/PnPDatabase interface,
/// not a SQLite library, SQL command or a hard-coded DwgVersion.
/// Copies the version value only from a validated native Ortho drawing
/// registered in the SAME active project, and only to the single
/// freshly registered drawing from AddPnPDrawingFile.
/// </summary>
internal static class NativeOrthoVersionHandshake80
{
    private const BindingFlags Flags=
        BindingFlags.Instance|BindingFlags.Public;

    private sealed record Snapshot(object DataLinks,
        IReadOnlyList<NativeOrthoVersionPolicy80.Drawing> Rows);

    internal static string Preflight(object orthoPart,string projectRoot,
        string orthoFolder,string selectedSeed,
        Func<string,bool> isValidNative)
    {
        Snapshot snapshot=Read(orthoPart,projectRoot,orthoFolder,
            isValidNative);
        return NativeOrthoVersionPolicy80.ReferenceVersion(
            snapshot.Rows,Path.GetFullPath(selectedSeed));
    }

    internal static string EnsureNewDrawing(object orthoPart,
        string projectRoot,string orthoFolder,string seed,string dest,
        Guid registeredGuid,string referenceVersion,
        Func<string,bool> isValidNative)
    {
        Snapshot snap=Read(orthoPart,projectRoot,orthoFolder,
            isValidNative);
        string expected=NativeOrthoVersionPolicy80.ReferenceVersion(
            snap.Rows,Path.GetFullPath(seed));
        if(expected!=referenceVersion)
            throw new InvalidOperationException(
                "REV80: Reference DwgVersion changed during generation. "+
                "Aborting rather than overwriting a newer project value.");
        var target=NativeOrthoVersionPolicy80.Target(snap.Rows,
            Path.GetFullPath(dest),registeredGuid);
        if(!NativeOrthoVersionPolicy80.RequiresWrite(
            target.DwgVersion,expected))
            return "PRESENT_MATCHED_NATIVE_REFERENCE="+expected;

        // Only the Plant 3D DataLinks API may write this ONE row/property.
        // Never open, update or even inspect Ortho.dcf through SQL.
        MethodInfo set=snap.DataLinks.GetType().GetMethods(Flags)
            .SingleOrDefault(m=>
            {
                if(m.Name!="SetProperties")return false;
                ParameterInfo[] p=m.GetParameters();
                return p.Length==3&&
                    (p[0].ParameterType==typeof(int)||
                     p[0].ParameterType==typeof(long))&&
                    p[1].ParameterType==typeof(StringCollection)&&
                    p[2].ParameterType==typeof(StringCollection);
            }) ?? throw new MissingMethodException(
                "REV80: Plant DataLinksManager.SetProperties(rowId,names,values) "+
                "is unavailable in this installation. No version was written.");
        MethodInfo save=snap.DataLinks.GetType().GetMethods(Flags)
            .SingleOrDefault(m=>m.Name=="SavePnPDatabaseChanges"&&
                m.GetParameters().Length==0) ??
            throw new MissingMethodException(
                "REV80: Plant native project database commit API unavailable.");

        var names=new StringCollection{"DwgVersion"};
        var values=new StringCollection{expected};
        object rowArg=Convert.ChangeType(target.RowId,
            set.GetParameters()[0].ParameterType,
            CultureInfo.InvariantCulture);
        try
        {
            set.Invoke(snap.DataLinks,new[]{rowArg,(object)names,values});
            save.Invoke(snap.DataLinks,null);
        }
        catch(TargetInvocationException ex)
        {
            throw new InvalidOperationException(
                "REV80: Native Plant API refused DwgVersion property update; "+
                "registered drawing remains unverified. "+
                (ex.InnerException?.Message??ex.Message),ex.InnerException??ex);
        }

        Snapshot check=Read(orthoPart,projectRoot,orthoFolder,
            isValidNative);
        var verified=NativeOrthoVersionPolicy80.Target(check.Rows,
            Path.GetFullPath(dest),registeredGuid);
        if(!string.Equals(verified.DwgVersion?.Trim(),expected,
            StringComparison.Ordinal))
            throw new InvalidOperationException(
                "REV80: Saved native PnP metadata readback did not match "+
                "the project-derived version. No views were generated.");
        return "NATIVE_DATALINKS_WRITTEN_AND_READBACK_VERIFIED"+
            " VALUE_FROM_REGISTERED_SEED="+expected;
    }

    private static Snapshot Read(object orthoPart,string projectRoot,
        string orthoFolder,Func<string,bool> isValidNative)
    {
        object dlm=orthoPart.GetType().GetProperty("DataLinksManager",Flags)?
            .GetValue(orthoPart) ??
            throw new InvalidOperationException(
                "REV80: Ortho DataLinksManager was not exposed by the "+
                "running Plant 3D project. No PnP database changes attempted.");
        object database=dlm.GetType().GetMethod("GetPnPDatabase",Flags,
                null,Type.EmptyTypes,null)?.Invoke(dlm,null) ??
            throw new InvalidOperationException(
                "REV80: Plant native GetPnPDatabase API unavailable.");
        object tables=database.GetType().GetProperty("Tables",Flags)?
            .GetValue(database) ??
            throw new InvalidOperationException("REV80: PnP database tables unavailable.");
        object table=Index(tables,"PnPDrawings") ??
            throw new InvalidOperationException(
                "REV80: Plant project has no PnPDrawings table.");
        MethodInfo? select=table.GetType().GetMethods(Flags)
            .FirstOrDefault(m=>m.Name=="Select"&&
                m.GetParameters().Length==1&&
                m.GetParameters()[0].ParameterType==typeof(string));
        if(select==null)
            throw new MissingMethodException(
                "REV80: Native PnPDrawings.Select(string) unavailable.");
        IEnumerable rawRows;
        try
        {
            rawRows=(select.Invoke(table,new object[]{"PnPID > 0"}) as IEnumerable)
                ??throw new InvalidOperationException(
                    "REV80: Plant returned no PnPDrawings result.");
        }
        catch(TargetInvocationException ex)
        {
            throw new InvalidOperationException(
                "REV80: Plant PnPDrawings query failed: "+
                (ex.InnerException?.Message??ex.Message),ex.InnerException??ex);
        }
        var results=new List<NativeOrthoVersionPolicy80.Drawing>();
        foreach(object? row in rawRows)
        {
            if(row==null)continue;
            if(!long.TryParse(Field(row,"PnPID"),NumberStyles.Integer,
                CultureInfo.InvariantCulture,out long id)||id<=0)
                continue;
            string? path=ResolvePath(row,projectRoot,orthoFolder);
            // For a VERSION SOURCE, require actual project-contained
            // registered native DWG, not an untrusted arbitrary row.
            bool verified=path!=null&&File.Exists(path)&&
                UnderFolder(path,projectRoot)&&isValidNative(path);
            results.Add(new NativeOrthoVersionPolicy80.Drawing(id,
                path,FirstField(row,"PnPDrawingGUID","PnPDrawingGuid",
                    "DrawingGUID"),
                Field(row,"DwgVersion"),verified));
        }
        return new Snapshot(dlm,results);
    }

    private static object? Index(object source,string field)
    {
        PropertyInfo? property=source.GetType().GetProperties(Flags)
            .FirstOrDefault(p=>p.Name=="Item"&&
                p.GetIndexParameters().Length==1&&
                p.GetIndexParameters()[0].ParameterType==typeof(string));
        if(property==null)return null;
        try{return property.GetValue(source,new object[]{field});}
        catch(TargetInvocationException){return null;}
        catch(ArgumentException){return null;}
    }

    private static string? Field(object row,string name)
    {
        object? value=Index(row,name);
        if(value==null||value is DBNull)return null;
        string s=Convert.ToString(value,CultureInfo.InvariantCulture)??"";
        return string.IsNullOrWhiteSpace(s)?null:s.Trim();
    }

    private static string? FirstField(object row,params string[] columns)
    {
        foreach(string c in columns)
        {
            string? value=Field(row,c);
            if(!string.IsNullOrWhiteSpace(value))return value;
        }
        return null;
    }

    private static string? ResolvePath(object row,
        string root,string orthoFolder)
    {
        // These are Plant schema field names, not user/project paths.
        // PnPRelativePath and Path / Dwg Name are native Autodesk
        // PnPDrawings columns. Source type/name/version values come
        // from the active project and are NEVER hard-coded here.
        foreach(string column in new[]{"PnPRelativePath",
            "AbsoluteFilePath","FilePath","RelativeFilePath",
            "PnPUnc","DwgPath","DrawingPath","DwgFile",
            "DrawingFile","FileName"})
        {
            string? value=Field(row,column);
            if(string.IsNullOrWhiteSpace(value)||
                !value.EndsWith(".dwg",StringComparison.OrdinalIgnoreCase))
                continue;
            string[] candidates=Path.IsPathRooted(value)?
                new[]{value}:
                new[]{Path.Combine(orthoFolder,value),
                    Path.Combine(root,value)};
            foreach(string candidate in candidates)
            {
                try
                {
                    string full=Path.GetFullPath(candidate);
                    if(File.Exists(full)&&UnderFolder(full,root))
                        return full;
                }
                catch(ArgumentException){/* Invalid row path. */}
                catch(NotSupportedException){/* Invalid row path. */}
            }
        }
        // Older Ortho schemas may split drawing folder and Dwg Name.
        // Use both native fields as data; accept only an existing DWG
        // under the active project. Never match by basename alone.
        string? folderValue=Field(row,"Path");
        string? name=FirstField(row,"Dwg Name","DwgName","Name");
        if(!string.IsNullOrWhiteSpace(folderValue)&&
           !string.IsNullOrWhiteSpace(name))
        {
            string filename=name.EndsWith(".dwg",
                StringComparison.OrdinalIgnoreCase)?name:name+".dwg";
            string candidate=Path.Combine(folderValue,filename);
            string[] paths=Path.IsPathRooted(candidate)?
                new[]{candidate}:
                new[]{Path.Combine(root,candidate),
                    Path.Combine(orthoFolder,candidate)};
            foreach(string p in paths)
            {
                try
                {
                    string full=Path.GetFullPath(p);
                    if(File.Exists(full)&&UnderFolder(full,root))
                        return full;
                }
                catch(ArgumentException){}
                catch(NotSupportedException){}
            }
        }
        return null;
    }

    private static bool UnderFolder(string file,string folder)
    {
        try
        {
            string rel=Path.GetRelativePath(folder,file);
            return !Path.IsPathRooted(rel)&&rel!=".."&&
                !rel.StartsWith(".."+Path.DirectorySeparatorChar,
                    StringComparison.Ordinal)&&
                !rel.StartsWith(".."+Path.AltDirectorySeparatorChar,
                    StringComparison.Ordinal);
        }
        catch(ArgumentException){return false;}
    }
}
