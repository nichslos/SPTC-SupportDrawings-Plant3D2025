using System;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using AcadApp=Autodesk.AutoCAD.ApplicationServices.Application;

namespace SPTC.SupportDrawings;

/// <summary>
/// Adapts documented Plant 3D 2025 Ortho Editor output appearance
/// controls to the active native Ortho document. No fake dashed
/// geometry, manual view edits, or direct project/database writes.
/// </summary>
internal sealed class NativeOrthoAppearanceAutoCAD83 :
    NativeOrthoAppearance83.IHost
{
    private const string HiddenSystem="PLANTORTHOHIDDENLINEMODE";
    private const string InsulationSystem="PLANTORTHOINSULATIONMODE";
    private readonly Document document;
    // Learned through the installed native Plant command. Never use a
    // guessed hidden-line integer enum or 2025-specific numeric value.
    private static System.Collections.Generic.IReadOnlyDictionary<string,int>?
        nativeHiddenReadbacks84;

    internal NativeOrthoAppearanceAutoCAD83(Document document)
    {
        this.document=document??throw new ArgumentNullException(nameof(document));
    }

    public object ReadHidden() =>
        AcadApp.GetSystemVariable(HiddenSystem) ??
        throw new InvalidOperationException(
            "Plant 3D did not return the current hidden-line mode. "+
            "Choose Plant 3D Default to leave native output appearance unchanged.");

    public object ReadInsulation() =>
        AcadApp.GetSystemVariable(InsulationSystem) ??
        throw new InvalidOperationException(
            "Plant 3D did not return the current insulation mode. "+
            "Choose Plant 3D Default to leave native output appearance unchanged.");

    public object ReadHiddenAfter()=>ReadHidden();
    public object ReadInsulationAfter()=>ReadInsulation();

    public void SetHiddenKeyword(string keyword)
    {
        if(string.IsNullOrWhiteSpace(keyword))
            throw new ArgumentException("Native hidden-line command keyword missing.");
        // REV83 passed a text keyword directly to an integer-backed
        // system variable. AutoCAD could coerce it to zero (No Hidden),
        // while returning success. That explains HOST_READBACK=0
        // for the user's explicit All Hidden Lines request.
        //
        // The native Ortho command is the authority. Calibrate the
        // currently installed command/readback mapping once per session
        // without assuming 0/1/2 or any fixed numeric order.
        if(nativeHiddenReadbacks84==null)
        {
            object original=ReadHidden();
            try
            {
                nativeHiddenReadbacks84=
                    NativeOrthoHiddenCalibration84.Observe(
                        requested=>document.Editor.Command(
                            "_.PLANTORTHOHIDDENLINEMODE",requested),
                        ReadHidden);
            }
            finally
            {
                // Restore exact original native value, not a guessed
                // ordinal or the string spelling of a ribbon label.
                AcadApp.SetSystemVariable(HiddenSystem,original);
            }
        }
        // Set exclusively through the documented native command.
        // No fallback to setting a string on a numeric variable.
        document.Editor.Command(
            "_.PLANTORTHOHIDDENLINEMODE",keyword);
        NativeOrthoHiddenCalibration84.Verify(
            nativeHiddenReadbacks84,keyword,ReadHidden());
    }

    public void SetInsulationValue(int value)
    {
        // Native documented 0/1/2 values. Do NOT use the 3D model
        // PLANTINSULATIONMODE; this setting is specific to Orthos.
        AcadApp.SetSystemVariable(InsulationSystem,value);
    }

    public void RestoreHidden(object before)
    {
        try
        {
            AcadApp.SetSystemVariable(HiddenSystem,before);
        }
        catch(System.Exception restoreError)
        {
            // Only a recoverable native keyword is safe for command
            // fallback. Never assume a numeric enum mapping.
            string? keyword=before as string;
            if(keyword==null||
               !NativeOrthoAppearance83.HiddenChoices.Any(x=>
                   NativeOrthoAppearance83.HiddenKeyword(x)!=null&&
                   string.Equals(NativeOrthoAppearance83.HiddenKeyword(x),
                       keyword,StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException(
                    "Unable to restore original hidden-line state "+
                    "through native Plant API; manual Ortho Editor "+
                    "verification is required.",restoreError);
            document.Editor.Command(
                "_.PLANTORTHOHIDDENLINEMODE",keyword);
        }
    }

    public void RestoreInsulation(object before)=>
        AcadApp.SetSystemVariable(InsulationSystem,before);
}
