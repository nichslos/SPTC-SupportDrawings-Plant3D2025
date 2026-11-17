using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev63MultiSheetTests
{
    static void Need(bool condition,string message)
    {
        if(!condition)throw new Exception("REV63 exact-scale sheet set: "+message);
    }
    internal static void Run()
    {
        var shape=NativeOrthoLayout.Build(
            new BoundingBox3(0,0,0,180,1234,603),1.0,0);
        var baseline=NativeOrthoTenViewPlan.Build(shape,841,594);
        Need(baseline.Denominator==25,
            "REV59 automatic A1 layout baseline still chooses 1:25");
        string[] selected={"TOP","BOTTOM","LEFT","RIGHT","FRONT",
            "BACK","NW","NE","SW","SE"};
        var exact10=baseline with {Denominator=10,Scale=.1};
        bool singleSheetRejected=false;
        try{NativeOrthoExactScalePlacement61.Build(
            exact10,selected,841,594);}
        catch(NativeOrthoExactScalePlacement61.DoesNotFitException)
        {singleSheetRejected=true;}
        Need(singleSheetRejected,"same TEST5 All10 1:10 must reject one A1 sheet");

        var sheets=NativeOrthoMultiSheetPlan63.Build(
            exact10,selected,841,594);
        Need(sheets.Count==2&&sheets[0].Views.Length==6&&
            sheets[1].Views.Length==4,
            "All10 1:10 results in independent Ortho6 + ISO4 native drawings");
        Need(sheets[0].Suffix=="S01_OF_02"&&
            sheets[0].DrawingNumberSuffix=="-S01"&&
            sheets[1].DrawingNumberSuffix=="-S02",
            "stable distinct sheet names and drawing numbers");
        Need(sheets.SelectMany(p=>p.Views).SequenceEqual(selected),
            "exactly ten original native presets covered in same order");
        foreach(var sheet in sheets)
        {
            var safe=NativeOrthoExactScalePlacement61.Build(
                exact10,sheet.Views,841,594);
            Need(safe.Views.Count==sheet.Views.Length&&
                safe.Titles.Count==sheet.Views.Length,
                "each sheet passes the ORIGINAL independent safe frame/title fit");
        }

        // At 1:1 this TEST5 model has views taller than A1.
        // Refuse the ENTIRE set before any Ortho seed clone/registration.
        var exactOne=baseline with {Denominator=1,Scale=1};
        bool oversizeRejected=false;
        try{NativeOrthoMultiSheetPlan63.Build(
            exactOne,selected,841,594);}
        catch(InvalidOperationException e)
        {oversizeRejected=e.Message.Contains("SINGLE",StringComparison.Ordinal);}
        Need(oversizeRejected,
            "even singleton overflow must reject before partial sheet creation");

        // A larger registered plot sheet can fit the exact scale
        // without changing scale or requiring automatic partial images.
        var roomier=NativeOrthoExactScalePlacement61.Build(
            exactOne,new[]{"TOP"},3500,2000);
        Need(roomier.Views.Count==1,
            "true 1:1 remains feasible on a physically larger Ortho seed");

        Console.WriteLine(
            "REV63: All10 1:10 safely splits into exact 1:10 Ortho6 + ISO4, "+
            "unique per-sheet IDs, oversize 1:1 stops without partial sheets PASS");
    }
}
