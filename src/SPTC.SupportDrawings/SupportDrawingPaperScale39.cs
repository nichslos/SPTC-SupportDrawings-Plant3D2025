using System;
using System.Globalization;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.39 paper-scale value validation for the WinForms selector.
/// Denominator 0 means the unchanged automatic fit; positive values are
/// passed unchanged to the existing native Ortho scale and sheet preflight.
/// No project, viewport, or AutoCAD API dependencies.
/// </summary>
public static class SupportDrawingPaperScale39
{
    public const string Automatic="Automatic";
    public const string Custom="Custom 1:N...";
    public const int Min=1;
    public const int Max=1000000;

    public static int Resolve(string? selection,int customDenominator)
    {
        if(string.Equals(selection,Automatic,StringComparison.Ordinal))
            return 0;
        if(string.Equals(selection,Custom,StringComparison.Ordinal))
            return Valid(customDenominator);
        if(selection!=null &&
           selection.StartsWith("1:",StringComparison.Ordinal) &&
           int.TryParse(selection.AsSpan(2),NumberStyles.None,
               CultureInfo.InvariantCulture,out int preset))
            return Valid(preset);
        throw new ArgumentException(
            "Select Automatic, a standard 1:N preset, or Custom 1:N.",
            nameof(selection));
    }

    public static string Display(int denominator)
    {
        if(denominator==0)return Automatic;
        return "1:"+Valid(denominator).ToString(CultureInfo.InvariantCulture);
    }

    private static int Valid(int denominator)
    {
        if(denominator<Min||denominator>Max)
            throw new ArgumentOutOfRangeException(nameof(denominator),
                "Scale denominator must be from 1 through 1000000.");
        return denominator;
    }
}
