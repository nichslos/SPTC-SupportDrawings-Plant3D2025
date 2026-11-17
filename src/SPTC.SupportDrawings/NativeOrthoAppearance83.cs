using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// Autodesk Ortho Editor Output Appearance modes. The strings are
/// AutoCAD native command keywords and the insulation numeric values
/// are Autodesk documented registry-variable values, NOT model or
/// project-specific data. "Plant 3D Default" makes no host changes.
/// </summary>
internal static class NativeOrthoAppearance83
{
    internal const string ProjectDefault="Plant 3D Default";
    internal const string NoHidden="No Hidden Lines";
    internal const string PipeHidden="Hidden Line Piping";
    internal const string AllHidden="All Hidden Lines";
    internal const string NoInsulation="No Insulation";
    internal const string SolidInsulation="Solid Line Insulation";
    internal const string HiddenInsulation="Hidden Line Insulation";

    internal static readonly string[] HiddenChoices={
        ProjectDefault,NoHidden,PipeHidden,AllHidden};
    internal static readonly string[] InsulationChoices={
        ProjectDefault,NoInsulation,SolidInsulation,HiddenInsulation};

    internal static string ValidateHidden(string? selected)=>
        HiddenChoices.Contains(selected,StringComparer.Ordinal)?
            selected!:throw new ArgumentException("Invalid native hidden-line mode.");
    internal static string ValidateInsulation(string? selected)=>
        InsulationChoices.Contains(selected,StringComparer.Ordinal)?
            selected!:throw new ArgumentException("Invalid native insulation mode.");

    // Native PLANTORTHOHIDDENLINEMODE prompt keywords as published
    // by Autodesk. Never guess undocumented integer enum values.
    internal static string? HiddenKeyword(string selection)=>
        ValidateHidden(selection) switch
        {
            ProjectDefault=>null,
            NoHidden=>"Nohiddenlines",
            PipeHidden=>"hiddenPipingonly",
            AllHidden=>"Allhiddenlines",
            _=>throw new ArgumentOutOfRangeException(nameof(selection))
        };

    // Autodesk's PLANTORTHOINSULATIONMODE: 0 hides insulation,
    // 1 hides pipe lines behind insulation, 2 shows pipe lines.
    internal static int? InsulationValue(string selection)=>
        ValidateInsulation(selection) switch
        {
            ProjectDefault=>null,
            NoInsulation=>0,
            SolidInsulation=>1,
            HiddenInsulation=>2,
            _=>throw new ArgumentOutOfRangeException(nameof(selection))
        };

    internal interface IHost
    {
        object ReadHidden();
        object ReadInsulation();
        void SetHiddenKeyword(string keyword);
        void SetInsulationValue(int value);
        void RestoreHidden(object before);
        void RestoreInsulation(object before);
        object ReadHiddenAfter();
        object ReadInsulationAfter();
    }

    internal sealed class Scope : IDisposable
    {
        private readonly IHost host;
        private readonly object? oldHidden,oldInsulation;
        private readonly bool changeHidden,changeInsulation;
        private readonly Action<string> report;
        private bool disposed;
        private bool preserveForNativeRibbon84;
        // Explicit selection is an opt-in to make the native Ortho
        // Editor ribbon match the SPTC setting after successful output.
        // Never retain on failure: disposal restores the old session.
        internal void KeepForNativeRibbonOnSuccess()
        {
            if(disposed)throw new ObjectDisposedException(nameof(Scope));
            preserveForNativeRibbon84=true;
        }
        internal Scope(IHost host,object? oldHidden,object? oldInsulation,
            bool changeHidden,bool changeInsulation,Action<string> report)
        {
            this.host=host;this.oldHidden=oldHidden;
            this.oldInsulation=oldInsulation;
            this.changeHidden=changeHidden;
            this.changeInsulation=changeInsulation;
            this.report=report;
        }
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
            if(preserveForNativeRibbon84)
            {
                report("NATIVE_RIBBON_SESSION_SYNC=PRESERVED_EXPLICIT_SELECTION"+
                    " OTHER_VIEWS_MAY_SHARE_MODEL_APPEARANCE");
                return;
            }
            Exception? error=null;
            if(changeInsulation&&oldInsulation!=null)
            {
                try{host.RestoreInsulation(oldInsulation);
                    report("INSULATION_MODE_RESTORE=PASS");}
                catch(Exception ex){error=ex;}
            }
            if(changeHidden&&oldHidden!=null)
            {
                try{host.RestoreHidden(oldHidden);
                    report("HIDDEN_MODE_RESTORE=PASS");}
                catch(Exception ex){error??=ex;}
            }
            if(error!=null)
                throw new InvalidOperationException(
                    "SPTC native Ortho appearance could not restore the "+
                    "previous Plant 3D session setting. Check the native "+
                    "Ortho Editor appearance ribbon before continuing.",error);
        }
    }

    internal static Scope Begin(IHost host,string selectedHidden,
        string selectedInsulation,Action<string> report)
    {
        if(host==null)throw new ArgumentNullException(nameof(host));
        report??= _=>{};
        string? keyword=HiddenKeyword(selectedHidden);
        int? insulation=InsulationValue(selectedInsulation);
        object? oldHidden=null,oldInsulation=null;
        // Capture both native states BEFORE changing either. If an
        // installed Plant 3D version cannot expose the state, refuse
        // to mutate unknown user preferences.
        if(keyword!=null)oldHidden=host.ReadHidden();
        if(insulation.HasValue)oldInsulation=host.ReadInsulation();
        if(keyword==null&&!insulation.HasValue)
            return new Scope(host,null,null,false,false,report);
        var scope=new Scope(host,oldHidden,oldInsulation,
            keyword!=null,insulation.HasValue,report);
        try
        {
            if(keyword!=null)
            {
                host.SetHiddenKeyword(keyword);
                object observed=host.ReadHiddenAfter();
                if(observed is string seen &&
                   HiddenChoices.Any(x=>HiddenKeyword(x)!=null&&
                       string.Equals(HiddenKeyword(x),seen,
                           StringComparison.OrdinalIgnoreCase))&&
                   !string.Equals(seen,keyword,
                       StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "Plant 3D reported a different hidden-line mode "+
                        "after setting the native command keyword.");
                report("HIDDEN_LINE_REQUEST="+selectedHidden+
                    " NATIVE_KEYWORD="+keyword+
                    " HOST_READBACK="+observed+
                    " HOST_MODE_VERIFICATION=ADAPTER_REQUIRED"+
                    " VIEW_OUTPUT_VERIFICATION=PENDING");
            }
            if(insulation.HasValue)
            {
                host.SetInsulationValue(insulation.Value);
                object observed=host.ReadInsulationAfter();
                if(Convert.ToInt32(observed,
                    System.Globalization.CultureInfo.InvariantCulture)!=
                    insulation.Value)
                    throw new InvalidOperationException(
                        "Plant 3D insulation mode readback differs from "+
                        "the requested native setting.");
                report("INSULATION_REQUEST="+selectedInsulation+
                    " NATIVE_VALUE="+insulation.Value+
                    " READBACK=PASS");
            }
            return scope;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }
}
