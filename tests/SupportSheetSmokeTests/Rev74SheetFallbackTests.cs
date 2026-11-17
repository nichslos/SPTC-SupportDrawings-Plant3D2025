using System;
using System.Collections.Generic;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev74SheetFallbackTests
{
    private static void Need(bool yes,string description)
    {
        if(!yes)throw new Exception("REV74 sheet fallback: "+description);
    }

    internal static void Run()
    {
        string[] native={
            "TOP","BOTTOM","LEFT","RIGHT","FRONT","BACK",
            "NW","NE","SW","SE"
        };
        Need(!NativeOrthoSheetSplit74.Eligible(false,10,10,false),
            "default off; original exact one-sheet contract retained");
        Need(NativeOrthoSheetSplit74.Eligible(true,10,10,false),
            "opt-in exact 1:10 ten-view retry accepted");
        Need(!NativeOrthoSheetSplit74.Eligible(true,0,10,false)&&
             !NativeOrthoSheetSplit74.Eligible(true,10,6,false)&&
             !NativeOrthoSheetSplit74.Eligible(true,10,10,true),
            "automatic scale, partial views and batch retain old behavior");
        var halves=NativeOrthoSheetSplit74.Bisect(native);
        Need(halves.First.Count==5&&halves.Second.Count==5&&
             halves.First.Concat(halves.Second).SequenceEqual(native),
            "balanced partition preserves selected order, camera names, and all views");
        List<string> leaves=new();
        void Visit(IReadOnlyList<string> names)
        {
            if(names.Count==1){leaves.Add(names[0]);return;}
            var split=NativeOrthoSheetSplit74.Bisect(names);
            Need(split.First.Count>0&&split.Second.Count>0&&
                 split.First.Count+split.Second.Count==names.Count,
                "recursive subdivision always terminates without loss");
            Visit(split.First);
            Visit(split.Second);
        }
        Visit(native);
        Need(leaves.SequenceEqual(native)&&
             leaves.Distinct(StringComparer.Ordinal).Count()==10,
            "all requested native views eventually retained, never duplicated");
        bool rejectsDuplicate=false;
        try { NativeOrthoSheetSplit74.Bisect(new[]{"TOP","TOP"}); }
        catch(ArgumentException) { rejectsDuplicate=true; }
        Need(rejectsDuplicate,"ambiguous duplicate native view cannot be partitioned");
        Console.WriteLine("REV74 opt-in exact-scale, default one-sheet, "+
            "ordered balanced multi-sheet partitions, no-loss recursion PASS");
    }
}
