using System;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// Output Appearance choices from the Plant 3D Ortho Editor.
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

    // Resolve values by name in the native rendering enum.
    internal static string? HiddenEnumName(string selection)=>
        ValidateHidden(selection) switch
        {
            ProjectDefault=>null,
            NoHidden=>"NoHiddenLines",
            PipeHidden=>"HiddenLinePiping",
            AllHidden=>"AllHiddenLines",
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
}
