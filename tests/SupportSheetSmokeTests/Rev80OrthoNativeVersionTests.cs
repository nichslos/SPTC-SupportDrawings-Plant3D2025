using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using SPTC.SupportDrawings;

internal static class Rev80OrthoNativeVersionTests
{
    private static void Need(bool success,string message)
    {
        if(!success)throw new Exception("REV80 native Ortho: "+message);
    }

    private sealed class FakeRow
    {
        internal readonly Dictionary<string,object?> Items=new(
            StringComparer.OrdinalIgnoreCase);
        public object? this[string name]
        {
            get => Items.TryGetValue(name,out object? v)?v:null;
            set => Items[name]=value;
        }
    }
    private sealed class FakeTable
    {
        public readonly List<FakeRow> Rows=new();
        public List<FakeRow> Select(string filter)
        {
            Need(filter.Contains("PnPID",StringComparison.Ordinal),
                "query scoped to PnP row key");
            return Rows;
        }
    }
    private sealed class FakeTables
    {
        public FakeTable PnPDrawings=new();
        public FakeTable this[string name] =>
            name=="PnPDrawings"?PnPDrawings:throw new ArgumentException(name);
    }
    private sealed class FakeDb
    {
        public readonly FakeTables Tables=new();
    }
    private sealed class FakeDlm
    {
        internal readonly FakeDb Db=new();
        internal int Writes,Saves;
        public FakeDb GetPnPDatabase()=>Db;
        public void SetProperties(long rowId,StringCollection names,
            StringCollection values)
        {
            Need(names.Count==1&&names[0]=="DwgVersion"&&values.Count==1,
                "update only one native DwgVersion property");
            FakeRow row=Db.Tables.PnPDrawings.Rows.Single(r=>
                Convert.ToInt64(r["PnPID"],CultureInfo.InvariantCulture)==rowId);
            row[names[0]!]=values[0];
            Writes++;
        }
        public void SavePnPDatabaseChanges(){Saves++;}
    }
    private sealed class FakeOrtho
    {
        public FakeDlm DataLinksManager {get;}=new();
    }
    private static FakeRow Row(long id,string path,Guid guid,
        string? version) 
    {
        var r=new FakeRow();
        r["PnPID"]=id;
        // Use the actual Autodesk Ortho schema's PnPRelativePath
        // to check that a registered native drawing can be resolved.
        r["PnPRelativePath"]=Path.GetFileName(path);
        r["PnPDrawingGUID"]=guid.ToString("D");
        r["DwgVersion"]=version;
        return r;
    }

    internal static void Run()
    {
        string root=Path.Combine(Path.GetTempPath(),
            "SPTC_REV80_METADATA_"+Guid.NewGuid().ToString("N"));
        string ortho=Path.Combine(root,"Orthos");
        Directory.CreateDirectory(ortho);
        try
        {
            string seed=Path.Combine(ortho,"wizard_ortho.dwg");
            string generated=Path.Combine(ortho,"new_ortho.dwg");
            File.WriteAllText(seed,"native seed fixture");
            File.WriteAllText(generated,"new DWG fixture");
            var part=new FakeOrtho();
            Guid wizardGuid=Guid.NewGuid(),newGuid=Guid.NewGuid();
            part.DataLinksManager.Db.Tables.PnPDrawings.Rows.Add(
                Row(4001,seed,wizardGuid,"13"));
            part.DataLinksManager.Db.Tables.PnPDrawings.Rows.Add(
                Row(4002,generated,newGuid,null));
            string actual=NativeOrthoVersionHandshake80.Preflight(
                part,root,ortho,seed,_=>true);
            Need(actual=="13",
                "version is derived from a real project drawing, not hard-coded");
            string result=NativeOrthoVersionHandshake80.EnsureNewDrawing(
                part,root,ortho,seed,generated,newGuid,actual,_=>true);
            Need(result.Contains("READBACK_VERIFIED",
                StringComparison.Ordinal)&&
                 part.DataLinksManager.Writes==1&&
                 part.DataLinksManager.Saves==1&&
                 part.DataLinksManager.Db.Tables.PnPDrawings.Rows[1]
                    ["DwgVersion"]?.ToString()=="13",
                 "only one verified native API property write and save");
            _=NativeOrthoVersionHandshake80.EnsureNewDrawing(
                part,root,ortho,seed,generated,newGuid,actual,_=>true);
            Need(part.DataLinksManager.Writes==1,
                "matching existing version is never rewritten");
            bool badGuid=false;
            try
            {
                _=NativeOrthoVersionHandshake80.EnsureNewDrawing(
                    part,root,ortho,seed,generated,Guid.NewGuid(),actual,_=>true);
            }
            catch(InvalidOperationException){badGuid=true;}
            Need(badGuid,"different native registration GUID fails closed");
            part.DataLinksManager.Db.Tables.PnPDrawings.Rows[0]
                ["DwgVersion"]=null;
            bool noReference=false;
            try
            {
                _=NativeOrthoVersionHandshake80.Preflight(
                    part,root,ortho,seed,_=>true);
            }
            catch(InvalidOperationException){noReference=true;}
            Need(noReference,
                "no registered native version refuses new drawing generation");

            string testSeed=Path.Combine(ortho,"alternate_native.dwg");
            File.WriteAllText(testSeed,"other native seed");
            var pure=new List<NativeOrthoVersionPolicy80.Drawing>
            {
                new(1,seed,null,"13",true),
                new(2,testSeed,null,"42",true),
                new(3,generated,newGuid.ToString(),null,false)
            };
            Need(NativeOrthoVersionPolicy80.ReferenceVersion(
                pure,seed)=="13",
                "seed version wins, even with different legacy project drawings");
            bool ambiguity=false;
            try
            {
                _=NativeOrthoVersionPolicy80.ReferenceVersion(
                    pure,testSeed+"-none");
            }
            catch(InvalidOperationException){ambiguity=true;}
            Need(ambiguity,
                "conflicting candidate versions without a seed fail closed");
            Need(NativeOrthoVersionPolicy80.RequiresWrite(null,"13"),
                "missing version requires native initialization");
            Console.WriteLine(
                "REV80 native Ortho version reference, exact GUID matching, "+
                "readback, non-hardcoded metadata and fail-closed rules PASS");
        }
        finally
        {
            try{Directory.Delete(root,recursive:true);}
            catch(IOException){/* test-only output cleanup */}
        }
    }
}
