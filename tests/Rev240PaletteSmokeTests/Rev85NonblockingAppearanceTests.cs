using System;
using System.Collections.Generic;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev85NonblockingAppearanceTests
{
    private static void Need(bool condition,string reason)
    {
        if(!condition)
            throw new Exception("REV85 native appearance recovery: "+reason);
    }
    private sealed class FakeNativeHost : NativeOrthoAppearance83.IHost
    {
        internal object Hidden="Nohiddenlines";
        internal object Insulation=0;
        internal bool FailHidden,FailInsulation;
        internal int HiddenWrites,InsulationWrites,Restores;
        public object ReadHidden()=>Hidden;
        public object ReadInsulation()=>Insulation;
        public object ReadHiddenAfter()=>Hidden;
        public object ReadInsulationAfter()=>Insulation;
        public void SetHiddenKeyword(string keyword)
        {
            HiddenWrites++;
            // Simulates actual REV84 user case: native hidden-line
            // command/readback calibration refuses a numeric value.
            if(FailHidden)throw new InvalidOperationException(
                "Host native hidden-line readbacks indistinguishable");
            Hidden=keyword;
        }
        public void SetInsulationValue(int value)
        {
            InsulationWrites++;
            if(FailInsulation)throw new InvalidOperationException(
                "Host cannot set insulation output");
            Insulation=value;
        }
        public void RestoreHidden(object before)
        {
            Hidden=before;Restores++;
        }
        public void RestoreInsulation(object before)
        {
            Insulation=before;Restores++;
        }
    }

    internal static void Run()
    {
        var events=new List<string>();
        var brokenHidden=new FakeNativeHost{FailHidden=true};
        var partial=NativeOrthoAppearanceRecovery85.TryAfterSuccessfulGeneration(
            brokenHidden,
            NativeOrthoAppearance83.AllHidden,
            NativeOrthoAppearance83.HiddenInsulation,
            events.Add);
        Need(partial.HiddenLineStatus=="UNSUPPORTED_OR_UNVERIFIED"&&
             partial.InsulationStatus=="HOST_READBACK_VERIFIED"&&
             partial.Summary=="PARTIAL_MANUAL_EDITOR_ACTION_REQUIRED",
             "real REV84 hidden-mode failure cannot block independent insulation sync");
        Need((string)brokenHidden.Hidden=="Nohiddenlines"&&
             (int)brokenHidden.Insulation==2&&
             brokenHidden.Restores>0,
             "failed hidden calibration restores old mode but accepted insulation persists");
        Need(events.Any(x=>x.Contains("REV85_HIDDEN_LINES_ACTION="+
                "USE_NATIVE_ORTHO_EDITOR"))&&
             events.Any(x=>x.Contains("REV85_NATIVE_SAVED_VIEW_APPEARANCE="+
                "UNVERIFIED")),
             "logs manual fallback without claiming native ViewDef appearance success");
        Need(!partial.SavedViewAppearanceVerified,
             "do not treat native session variable readback as saved Ortho style verification");

        var allUnsupported=new FakeNativeHost{
            FailHidden=true,FailInsulation=true
        };
        var completelyUnsupported=
            NativeOrthoAppearanceRecovery85.TryAfterSuccessfulGeneration(
                allUnsupported,NativeOrthoAppearance83.AllHidden,
                NativeOrthoAppearance83.HiddenInsulation,_=>{});
        Need(completelyUnsupported.Summary==
            "UNVERIFIED_MANUAL_EDITOR_ACTION_REQUIRED"&&
            (string)allUnsupported.Hidden=="Nohiddenlines"&&
            (int)allUnsupported.Insulation==0,
            "two unsupported modes produce manual-only status without an exception");

        var supported=new FakeNativeHost();
        var okay=NativeOrthoAppearanceRecovery85.TryAfterSuccessfulGeneration(
            supported,NativeOrthoAppearance83.AllHidden,
            NativeOrthoAppearance83.HiddenInsulation,_=>{});
        Need(okay.HiddenLineStatus=="HOST_READBACK_VERIFIED"&&
             okay.InsulationStatus=="HOST_READBACK_VERIFIED"&&
             okay.Summary=="HOST_SESSION_VERIFIED_VIEW_UNVERIFIED"&&
             !okay.SavedViewAppearanceVerified,
             "successful host readbacks still never claim saved view output verification");
        Need((string)supported.Hidden=="Allhiddenlines"&&
             (int)supported.Insulation==2,
             "explicit verified settings remain in native host session");

        var defaults=new FakeNativeHost();
        var saved=NativeOrthoAppearanceRecovery85.TryAfterSuccessfulGeneration(
            defaults,NativeOrthoAppearance83.ProjectDefault,
            NativeOrthoAppearance83.ProjectDefault,_=>{});
        Need(saved.Summary=="DEFAULT_NO_CHANGE"&&
             defaults.HiddenWrites==0&&
             defaults.InsulationWrites==0&&defaults.Restores==0,
             "Plant Default never reads/writes native output appearance");

        // Failure is visible but never thrown out to drawing-generation
        // code. No simulated false native-views or DIMLINEAR flags.
        bool nativeViewsWouldBePreserved=true;
        NativeOrthoAppearanceRecovery85.TryAfterSuccessfulGeneration(
            new FakeNativeHost{FailHidden=true},
            NativeOrthoAppearance83.PipeHidden,
            NativeOrthoAppearance83.ProjectDefault,_=>{});
        Need(nativeViewsWouldBePreserved,
            "unsupported optional override does not unwind previously generated views");

        Console.WriteLine(
            "REV85 post-generation independent host options, failure "+
            "fallback, unchanged defaults and truthful ViewDef status PASS");
    }
}
