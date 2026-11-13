using System;
using System.Globalization;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.49.2: generated PAPER SPACE dimensions show measurements evaluated
/// by native AutoCAD from their witness points and a per-viewport DIMLFAC.
/// The source-units-to-millimeters factor and saved native viewport scale
/// are the only inputs. No fixed measurement text overrides.
/// </summary>
public static class SupportOrthoDynamicDimension492
{
    public static double ModelMillimetersPerPaperUnit(
        double sourceUnitsToMm,double viewportScale)
    {
        if(!double.IsFinite(sourceUnitsToMm)||sourceUnitsToMm<=0||
           !double.IsFinite(viewportScale)||viewportScale<=0)
            throw new ArgumentOutOfRangeException(
                nameof(viewportScale),
                "REV22.49.2 invalid source units or native view scale.");
        double factor=sourceUnitsToMm/viewportScale;
        if(!double.IsFinite(factor)||factor<=0)
            throw new ArgumentOutOfRangeException(
                nameof(viewportScale),
                "REV22.49.2 invalid projected paper-to-model factor.");
        return factor;
    }

    public static double MeasuredMillimeters(
        SupportOrthoDimLinearPlan41.Dimension dim)
    {
        if(!dim.PaperToModelMm.HasValue)
            throw new ArgumentException(
                "REV22.49.2 auto-measured dimension has no native scale.");
        double factor=dim.PaperToModelMm.Value;
        if(!double.IsFinite(factor)||factor<=0)
            throw new ArgumentException(
                "REV22.49.2 invalid per-dimension paper-to-model factor.");
        double paperSpan=dim.Horizontal
            ?Math.Abs(dim.Bx-dim.Ax):Math.Abs(dim.By-dim.Ay);
        double value=paperSpan*factor;
        if(!double.IsFinite(value)||value<=0)
            throw new ArgumentException(
                "REV22.49.2 dimension endpoints are invalid or degenerate.");
        return value;
    }

    /// <summary>
    /// Legacy numeric string is now for diagnostics only. Fail closed if a
    /// candidate's numeric text does not agree with its actual paper points
    /// and viewport scale (e.g. stale pre-update label or wrong camera).
    /// This prevents incorrect printed dimensions on a valid-looking DWG.
    /// </summary>
    public static bool MatchesDiagnostic(
        SupportOrthoDimLinearPlan41.Dimension dim)
    {
        if(!double.TryParse(dim.Measurement,NumberStyles.Float,
            CultureInfo.InvariantCulture,out double diagnostic)||
           !double.IsFinite(diagnostic))
            return false;
        double model=MeasuredMillimeters(dim);
        return Math.Abs(diagnostic-model)<=
            Math.Max(.001,model*1e-6);
    }
}
