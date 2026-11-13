using System;
using System.IO;
using System.Linq;
using SPTC.SupportDrawings;

/// <summary>
/// Pure production UI regression: all pre-existing generator defaults,
/// XREF/numeric/style options and per-project isolation are preserved.
/// No Plant 3D project, AutoCAD DWG, source handles or model geometry
/// are accessed by this test.
/// </summary>
internal static class ProductionUiPreferencesTests
{
    private static void Need(bool ok,string detail)
    {
        if(!ok)throw new Exception("UI preferences: "+detail);
    }

    internal static void Run()
    {
        var defaults=SupportDrawingUiPreferences.Defaults;
        Need(defaults.Views.SequenceEqual(
            SupportDrawingUiPreferences.AllViews)&&
            defaults.Views.Length==10,
            "all ten native views remain the default");
        Need(defaults.SupportFocus&&defaults.LineNumber&&
            defaults.Bop&&defaults.Tos&&defaults.Description&&
            defaults.XrefMemberNames&&defaults.AutoDimLinear&&
            defaults.ReferenceDetailDimensions&&
            !defaults.BatchOrtho&&
            defaults.ScaleSelection=="Automatic"&&
            defaults.DrawingPrefix=="",
            "working native dimension, annotation, scale, and batch defaults");
        var six=SupportDrawingUiPreferences.ViewPreset("Orthographic");
        var four=SupportDrawingUiPreferences.ViewPreset("Isometric");
        Need(six.Length==6&&four.Length==4&&
            six.Concat(four).SequenceEqual(defaults.Views)&&
            six.All(x=>!four.Contains(x)),
            "orthographic and ISO presets split the ten validated cameras");
        Need(SupportDrawingUiPreferences.ViewPreset("None").Length==0,
            "clear-all remains available for manual view selection");
        var customized=defaults with {
            ScaleSelection=SupportDrawingPaperScale39.Custom,
            CustomScale=17,
            DrawingPrefix="SUPPORT_TEST",
            Views=new[]{"SW","LEFT","TOP"},
            Tos=false,
            XrefMemberNames=false,
            BatchOrtho=true
        };
        var fromJson=SupportDrawingUiPreferences.Deserialize(
            SupportDrawingUiPreferences.Serialize(customized));
        Need(fromJson.Views.SequenceEqual(
                new[]{"TOP","LEFT","SW"})&&
            fromJson.ScaleSelection==SupportDrawingPaperScale39.Custom&&
            fromJson.CustomScale==17&&
            fromJson.DrawingPrefix=="SUPPORT_TEST"&&
            !fromJson.Tos&&!fromJson.XrefMemberNames&&
            fromJson.BatchOrtho,
            "round-trip preserves intentional options and canonicalizes selected order");

        bool invalid=false;
        try{SupportDrawingUiPreferences.Validate(
            defaults with {Views=new[]{"LEFT","LEFT"}});}
        catch(ArgumentException){invalid=true;}
        Need(invalid,"duplicate native view selection rejected");

        invalid=false;
        try{SupportDrawingUiPreferences.Validate(
            defaults with {Views=new[]{"XREF"}});}
        catch(ArgumentException){invalid=true;}
        Need(invalid,"non-native or unknown view rejected");

        invalid=false;
        try{SupportDrawingUiPreferences.Validate(
            defaults with {DrawingPrefix="..\\wrong"});}
        catch(ArgumentException){invalid=true;}
        Need(invalid,"Windows drawing filename injection rejected");

        invalid=false;
        try{SupportDrawingUiPreferences.Validate(
            defaults with {ScaleSelection="1:13"});}
        catch(ArgumentException){invalid=true;}
        Need(invalid,"saved scale must come from actual WinForms choices");

        string unique=Guid.NewGuid().ToString("N");
        string one=Path.Combine(Path.GetTempPath(),"SPTC_UI_PROJECT_"+unique);
        string another=Path.Combine(Path.GetTempPath(),"SPTC_UI_OTHER_"+unique);
        string path=SupportDrawingUiPreferences.StoragePath(one);
        string otherPath=SupportDrawingUiPreferences.StoragePath(another);
        Need(!path.Equals(otherPath,StringComparison.OrdinalIgnoreCase),
            "distinct Plant projects must never share UI preferences");
        try
        {
            Need(!SupportDrawingUiPreferences.TryLoad(one,out var unseen)&&
                 unseen.Views.Length==10,
                "first run uses unchanged default options");
            SupportDrawingUiPreferences.Save(one,customized);
            Need(SupportDrawingUiPreferences.TryLoad(one,out var loaded)&&
                loaded.Views.SequenceEqual(fromJson.Views)&&
                loaded.CustomScale==17,
                "explicit save persists validated per-project options");
            Need(!SupportDrawingUiPreferences.TryLoad(another,out _),
                "settings do not leak into other project roots");
            File.WriteAllText(path,"{invalid_json");
            Need(!SupportDrawingUiPreferences.TryLoad(one,out var safe)&&
                safe.Views.Length==10&&safe.ReferenceDetailDimensions,
                "corrupted saved preferences fail closed to working defaults");
        }
        finally
        {
            if(File.Exists(path))File.Delete(path);
        }
        Console.WriteLine(
            "Production native view presets, validated local saved settings, project isolation, corrupted-file fallback, drawing defaults PASS");
    }
}
