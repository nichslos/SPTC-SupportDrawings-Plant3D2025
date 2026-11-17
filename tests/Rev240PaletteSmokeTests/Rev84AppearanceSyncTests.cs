using System;
using System.Collections.Generic;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev84AppearanceSyncTests
{
    private static void Need(bool pass,string purpose)
    {
        if(!pass)throw new Exception("REV84 native appearance: "+purpose);
    }

    private sealed class FakeNativeHost : NativeOrthoAppearance83.IHost
    {
        internal object Hidden="Nohiddenlines";
        internal object Insulation=0;
        internal int Restores;
        public object ReadHidden()=>Hidden;
        public object ReadInsulation()=>Insulation;
        public object ReadHiddenAfter()=>Hidden;
        public object ReadInsulationAfter()=>Insulation;
        public void SetHiddenKeyword(string keyword)=>Hidden=keyword;
        public void SetInsulationValue(int value)=>Insulation=value;
        public void RestoreHidden(object before)
        {
            Hidden=before;
            Restores++;
        }
        public void RestoreInsulation(object before)
        {
            Insulation=before;
            Restores++;
        }
    }

    internal static void Run()
    {
        // These values are deliberately arbitrary simulated
        // installation readbacks. Production gets the actual values
        // from the running Plant native command, never from this test.
        var values=new Dictionary<string,int>
        {
            ["Nohiddenlines"]=19,
            ["hiddenPipingonly"]=73,
            ["Allhiddenlines"]=204
        };
        int state=-1;
        var observed=NativeOrthoHiddenCalibration84.Observe(
            keyword=>state=values[keyword],
            ()=>state);
        Need(observed.Count==3&&observed["Allhiddenlines"]==204,
            "derive three distinct native host values without assuming 0,1,2");
        NativeOrthoHiddenCalibration84.Verify(observed,
            "Allhiddenlines",204);
        bool wrong=false;
        try
        {
            NativeOrthoHiddenCalibration84.Verify(observed,
                "Allhiddenlines",19);
        }
        catch(InvalidOperationException){wrong=true;}
        Need(wrong,"fail rather than silently accept wrong native hidden mode");
        bool coerced=false;
        try
        {
            _=NativeOrthoHiddenCalibration84.Observe(
                _=>state=0,()=>state);
        }
        catch(InvalidOperationException){coerced=true;}
        Need(coerced,
            "reject the former all-modes-coerced-to-zero failure");
        var host=new FakeNativeHost();
        var events=new List<string>();
        using(var scope=NativeOrthoAppearance83.Begin(host,
            NativeOrthoAppearance83.AllHidden,
            NativeOrthoAppearance83.HiddenInsulation,events.Add))
        {
            Need((string)host.Hidden=="Allhiddenlines"&&
                 (int)host.Insulation==2,
                "explicit options are applied through native host");
            scope.KeepForNativeRibbonOnSuccess();
        }
        Need(host.Restores==0&&
             (string)host.Hidden=="Allhiddenlines"&&
             (int)host.Insulation==2&&
             events.Any(e=>e.Contains(
                 "NATIVE_RIBBON_SESSION_SYNC=PRESERVED_EXPLICIT_SELECTION",
                 StringComparison.Ordinal)),
            "successful generation retains explicit options in active native ribbon");
        var failed=new FakeNativeHost();
        using(NativeOrthoAppearance83.Begin(failed,
            NativeOrthoAppearance83.AllHidden,
            NativeOrthoAppearance83.HiddenInsulation,_=>{}))
        {
            // No success commit: failed generation or aborted session.
        }
        Need(failed.Restores==2&&
             (string)failed.Hidden=="Nohiddenlines"&&
             (int)failed.Insulation==0,
            "failed or abandoned generation restores both prior modes");
        var untouched=new FakeNativeHost();
        using(var scope=NativeOrthoAppearance83.Begin(untouched,
            NativeOrthoAppearance83.ProjectDefault,
            NativeOrthoAppearance83.ProjectDefault,_=>{}))
            scope.KeepForNativeRibbonOnSuccess();
        Need(untouched.Restores==0&&
             (string)untouched.Hidden=="Nohiddenlines"&&
             (int)untouched.Insulation==0,
            "default leaves the native host unchanged");

        Console.WriteLine(
            "REV84 hidden-line dynamic calibration, numeric mismatch "+
            "rejection, successful ribbon retention and failure rollback PASS");
    }
}
