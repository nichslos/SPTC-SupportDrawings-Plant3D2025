using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// Discover the installed Plant 3D native hidden-line numeric values by
/// INVOKING its three documented command keywords, then reading the
/// corresponding live host system variable. No guessed integer values.
/// A native command failure or indistinguishable readback aborts the
/// requested override instead of silently creating "No Hidden Lines".
/// </summary>
internal static class NativeOrthoHiddenCalibration84
{
    internal static IReadOnlyDictionary<string,int> Observe(
        Action<string> invokeNativeKeyword,
        Func<object> readNativeMode)
    {
        if(invokeNativeKeyword==null)
            throw new ArgumentNullException(nameof(invokeNativeKeyword));
        if(readNativeMode==null)
            throw new ArgumentNullException(nameof(readNativeMode));
        var results=new Dictionary<string,int>(
            StringComparer.OrdinalIgnoreCase);
        foreach(string selection in new[]{
            NativeOrthoAppearance83.NoHidden,
            NativeOrthoAppearance83.PipeHidden,
            NativeOrthoAppearance83.AllHidden})
        {
            string keyword=NativeOrthoAppearance83.HiddenKeyword(selection)!;
            invokeNativeKeyword(keyword);
            results[keyword]=Convert.ToInt32(readNativeMode(),
                CultureInfo.InvariantCulture);
        }
        if(results.Values.Distinct().Count()!=results.Count)
            throw new InvalidOperationException(
                "REV84: Plant 3D native hidden-line command did not "+
                "produce three independently verifiable host values. "+
                "Refusing an unverified output appearance override.");
        return results;
    }

    internal static void Verify(
        IReadOnlyDictionary<string,int> nativeValues,
        string selectedKeyword,object actual)
    {
        if(!nativeValues.TryGetValue(selectedKeyword,out int expected)||
           expected!=Convert.ToInt32(actual,CultureInfo.InvariantCulture))
            throw new InvalidOperationException(
                "REV84: Native hidden-line readback does not match "+
                "the requested Ortho ribbon mode; refusing to "+
                "report the generated appearance as verified.");
    }
}
