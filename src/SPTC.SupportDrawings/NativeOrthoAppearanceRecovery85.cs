using System;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV85: native hidden/insulation appearance is a NON-BLOCKING,
/// post-generation host-session request. It must never interrupt
/// GenerateOrthos or be mistaken for verified saved-view styling.
/// Each requested mode is attempted independently: unsupported
/// hidden-line calibration cannot prevent a valid insulation mode.
/// </summary>
internal static class NativeOrthoAppearanceRecovery85
{
    internal sealed record Result(
        string HiddenLineStatus,
        string InsulationStatus,
        string Summary,
        bool SavedViewAppearanceVerified);

    internal static Result TryAfterSuccessfulGeneration(
        NativeOrthoAppearance83.IHost host,
        string hidden,
        string insulation,
        Action<string> report)
    {
        if(host==null)throw new ArgumentNullException(nameof(host));
        report??= _=>{};
        // Bad values must never be passed to the native host. The
        // existing queued-options preflight validates these as well.
        NativeOrthoAppearance83.ValidateHidden(hidden);
        NativeOrthoAppearance83.ValidateInsulation(insulation);

        string hiddenStatus=TryOne(host,hidden,
            NativeOrthoAppearance83.ProjectDefault,report,
            "HIDDEN_LINES");
        string insulationStatus=TryOne(host,
            NativeOrthoAppearance83.ProjectDefault,insulation,report,
            "INSULATION");

        bool anyExplicit=hidden!=NativeOrthoAppearance83.ProjectDefault||
            insulation!=NativeOrthoAppearance83.ProjectDefault;
        bool hiddenOk=hiddenStatus=="DEFAULT_UNCHANGED"||
            hiddenStatus=="HOST_READBACK_VERIFIED";
        bool insulationOk=insulationStatus=="DEFAULT_UNCHANGED"||
            insulationStatus=="HOST_READBACK_VERIFIED";
        string overall=!anyExplicit?"DEFAULT_NO_CHANGE":
            hiddenOk&&insulationOk?"HOST_SESSION_VERIFIED_VIEW_UNVERIFIED":
            hiddenOk||insulationOk?"PARTIAL_MANUAL_EDITOR_ACTION_REQUIRED":
                "UNVERIFIED_MANUAL_EDITOR_ACTION_REQUIRED";
        // Neither system variables nor command execution prove a
        // specific saved native ViewDef's render mode. Never return
        // SAVED_VIEW_PASS based solely on the ribbon state.
        report("REV85_POST_GENERATION_APPEARANCE="+overall);
        report("REV85_NATIVE_SAVED_VIEW_APPEARANCE=UNVERIFIED"+
            " EDIT_VIEW_VISUAL_CHECK_REQUIRED="+anyExplicit);
        return new Result(hiddenStatus,insulationStatus,overall,false);
    }

    private static string TryOne(NativeOrthoAppearance83.IHost host,
        string hidden,string insulation,Action<string> report,
        string channel)
    {
        if(hidden==NativeOrthoAppearance83.ProjectDefault&&
           insulation==NativeOrthoAppearance83.ProjectDefault)
        {
            report("REV85_"+channel+"=DEFAULT_UNCHANGED");
            return "DEFAULT_UNCHANGED";
        }
        try
        {
            // Independent acquisition, native application, readback
            // and exception-safe restore. A failed host call does
            // not alter native Ortho generation's success state.
            using var scope=NativeOrthoAppearance83.Begin(
                host,hidden,insulation,
                message=>report("REV85_"+channel+"_DETAIL="+message));
            scope.KeepForNativeRibbonOnSuccess();
            report("REV85_"+channel+"=HOST_READBACK_VERIFIED"+
                " SAVED_VIEW_APPEARANCE=UNVERIFIED");
            return "HOST_READBACK_VERIFIED";
        }
        catch(Exception ex)
        {
            report("REV85_"+channel+"=UNSUPPORTED_OR_UNVERIFIED"+
                " ERROR="+ex.GetType().Name+":"+
                ex.Message.Replace('\r',' ').Replace('\n',' '));
            report("REV85_"+channel+"_ACTION=USE_NATIVE_ORTHO_EDITOR"+
                " OUTPUT_APPEARANCE_AFTER_GENERATION");
            return "UNSUPPORTED_OR_UNVERIFIED";
        }
    }
}
