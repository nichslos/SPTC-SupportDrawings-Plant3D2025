using System;
using System.Globalization;
using System.IO;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using AcadApp=Autodesk.AutoCAD.ApplicationServices.Application;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV87 read-only Ortho Editor mode capture invoked from the EXISTING
/// modeless Support Drawing Manager. No new AutoCAD CommandMethod is
/// registered; all four production commands remain unchanged.
/// The operator must first choose the mode in Plant's native ribbon.
/// </summary>
internal static class NativeOrthoContextCapture87
{
    internal static string Capture(Document doc,string claimedNativeRibbon)
    {
        if(doc==null)throw new ArgumentNullException(nameof(doc));
        NativeOrthoAppearance83.ValidateHidden(claimedNativeRibbon);
        if(claimedNativeRibbon==NativeOrthoAppearance83.ProjectDefault)
            throw new ArgumentException("Select an explicit native ribbon mode.");

        string hidden=TryRead("PLANTORTHOHIDDENLINEMODE");
        string insulation=TryRead("PLANTORTHOINSULATIONMODE");
        string document=doc.Name??"(unnamed)";
        string entry=
            "REV87_CAPTURE_UTC="+DateTimeOffset.UtcNow.ToString(
                "O",CultureInfo.InvariantCulture)+Environment.NewLine+
            "REV87_ACTIVE_DRAWING="+document+Environment.NewLine+
            "REV87_NATIVE_EDITOR_CONTEXT=OPERATOR_MUST_VERIFY"+
                Environment.NewLine+
            "REV87_ACTUAL_RIBBON_LABEL_OPERATOR_SELECTED="+
                claimedNativeRibbon+Environment.NewLine+
            "REV87_NATIVE_HIDDEN_MODE_READBACK="+hidden+
                Environment.NewLine+
            "REV87_NATIVE_INSULATION_READBACK="+insulation+
                Environment.NewLine+
            "REV87_CAPTURE_POLICY=READ_ONLY NO_NATIVE_MODE_MUTATION"+
                Environment.NewLine+Environment.NewLine;

        string directory=Path.GetDirectoryName(Path.GetFullPath(document)) ??
            throw new InvalidOperationException(
                "Active document has no file folder.");
        if(!Directory.Exists(directory))
            throw new DirectoryNotFoundException(directory);
        string output=Path.Combine(directory,
            Path.GetFileNameWithoutExtension(document)+
            "_REV87_APPEARANCE_CAPTURE.txt");
        File.AppendAllText(output,entry,new UTF8Encoding(false));
        return "REV87 native mode captured ("+claimedNativeRibbon+
            "). File: "+output+
            ". Repeat the other two modes. "+
            "Saved view appearance NOT verified.";
    }

    private static string TryRead(string systemVariable)
    {
        try
        {
            object? value=AcadApp.GetSystemVariable(systemVariable);
            if(value==null)return "NULL";
            return "TYPE="+value.GetType().FullName+
                " VALUE="+(Convert.ToString(value,
                    CultureInfo.InvariantCulture)??"NULL");
        }
        catch(Exception ex)
        {
            return "UNAVAILABLE="+ex.GetType().Name+
                ":"+ex.Message.Replace('\r',' ').Replace('\n',' ');
        }
    }
}
