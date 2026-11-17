using System;
using SPTC.SupportDrawings;

/// <summary>
/// REV75 purely synthetic draft-canvas checks. No project sizes,
/// pipe positions, DWG names or preferred fixed physical sheet sizes.
/// </summary>
internal static class Rev75DraftOverflowTests
{
    private static void Need(bool valid,string reason)
    {
        if(!valid)throw new Exception("REV75 draft overflow: "+reason);
    }

    internal static void Run()
    {
        var alreadyFits=NativeOrthoDraftOverflow75.Choose(
            300,250,(w,h)=>w>=300&&h>=250);
        Need(alreadyFits.Enlargements==0&&
             !alreadyFits.ExceedsActualMedia &&
             Math.Abs(alreadyFits.WidthMm-300)<1e-9 &&
             Math.Abs(alreadyFits.HeightMm-250)<1e-9,
            "normally fitting media is never artificially enlarged");
        var overflow=NativeOrthoDraftOverflow75.Choose(
            1000,700,(w,h)=>w>=1850&&h>=1100);
        Need(overflow.ExceedsActualMedia&&overflow.Enlargements>0&&
             overflow.WidthMm>=1850&&overflow.HeightMm>=1100,
            "all-ten draft canvas grows from ACTUAL paper until both dimensions fit");
        bool impossibleRejected=false;
        try{NativeOrthoDraftOverflow75.Choose(1000,700,(w,h)=>false);}
        catch(InvalidOperationException){impossibleRejected=true;}
        Need(impossibleRejected,
            "impossible virtual drafting geometry remains a failure");
        bool invalidRejected=false;
        try{NativeOrthoDraftOverflow75.Choose(double.NaN,700,(w,h)=>true);}
        catch(ArgumentOutOfRangeException){invalidRejected=true;}
        Need(invalidRejected,"invalid physical sheet is rejected");
        Console.WriteLine("REV75 draft-only virtual source-dependent "+
            "canvas, unchanged normal fit, bounded failure handling PASS");
    }
}
