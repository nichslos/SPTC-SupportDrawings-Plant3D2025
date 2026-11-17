using System;
using System.Globalization;
using System.IO;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using AcadApp=Autodesk.AutoCAD.ApplicationServices.Application;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV87 workstation-only read-only calibration of the three native
/// Ortho Editor ribbon choices. The user FIRST changes the ribbon
/// using Autodesk's real UI; this command records the host readback.
/// It deliberately never invokes PLANTORTHOHIDDENLINEMODE, changes a
/// system variable, or modifies the drawing or project databases.
/// </summary>
public sealed class NativeOrthoContextCapture87
{
    [CommandMethod("SPTC_ORTHOMODECAPTURE87",CommandFlags.Modal)]
    public void Capture()
    {
        Document? doc=AcadApp.DocumentManager.MdiActiveDocument;
        if(doc==null)return;
        Editor ed=doc.Editor;

        var prompt=new PromptKeywordOptions(
            "\nREV87: FIRST select the actual mode in native Ortho Editor"+
            " Output Appearance, then label the ribbon mode");
        prompt.Keywords.Add("NoHidden");
        prompt.Keywords.Add("PipingOnly");
        prompt.Keywords.Add("AllHidden");
        prompt.AllowNone=false;
        PromptResult chosen=ed.GetKeywords(prompt);
        if(chosen.Status!=PromptStatus.OK)return;

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
                chosen.StringResult+Environment.NewLine+
            "REV87_NATIVE_HIDDEN_MODE_READBACK="+hidden+
                Environment.NewLine+
            "REV87_NATIVE_INSULATION_READBACK="+insulation+
                Environment.NewLine+
            "REV87_CAPTURE_POLICY=READ_ONLY NO_NATIVE_MODE_MUTATION"+
                Environment.NewLine+Environment.NewLine;

        try
        {
            string directory=Path.GetDirectoryName(Path.GetFullPath(document)) ??
                throw new InvalidOperationException(
                    "Active document has no file folder.");
            if(!Directory.Exists(directory))
                throw new DirectoryNotFoundException(directory);
            string output=Path.Combine(directory,
                Path.GetFileNameWithoutExtension(document)+
                "_REV87_APPEARANCE_CAPTURE.txt");
            File.AppendAllText(output,entry,new UTF8Encoding(false));
            ed.WriteMessage("\nREV87 native appearance capture written: "+output);
        }
        catch(Exception ex)
        {
            // Even if the file is locked, keep the observations visible.
            ed.WriteMessage("\nREV87 capture could not write sidecar: "+
                ex.GetType().Name+": "+ex.Message);
        }
        ed.WriteMessage("\n"+entry.Replace(Environment.NewLine," | "));
        ed.WriteMessage("\nREV87: Repeat with the other TWO native ribbon modes."+
            " Same readback for all three = unsupported mapping."+
            " A distinct readback alone is NOT saved-view appearance proof.");
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
