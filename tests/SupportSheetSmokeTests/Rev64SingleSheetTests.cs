using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev64SingleSheetTests
{
    internal static void Run()
    {
        var geometry=NativeOrthoLayout.Build(
            new BoundingBox3(0,0,0,180,1234,603),1.0,0);
        string[] views={"TOP","BOTTOM","LEFT","RIGHT","FRONT",
            "BACK","NW","NE","SW","SE"};
        var basePlan=NativeOrthoTenViewPlan.Build(geometry,841,594);
        Check(basePlan.Denominator==25,
            "REV59 AUTO 1:25 baseline unchanged");
        Check(!NativeOrthoSingleSheet64.TryExactTenFit(
            geometry,views,10,841,594,out _),
            "TEST5-like exact 1:10 all 10 must never be squeezed into A1");
        Check(NativeOrthoSingleSheet64.TryExactTenFit(
            geometry,views,10,1189,841,out var reason),
            "TEST5-like exact 1:10 all 10 fit safely on one A0 "+
            "landscape native sheet: "+reason);
        Check(NativeOrthoSingleSheet64.TryExactTenFit(
            geometry,views,20,841,594,out _),
            "existing exact 1:20 all ten on A1 remains valid");
        var selected=NativeOrthoSingleSheet64.SmallestFitting(
            geometry,views,10,new[]{
                new NativeOrthoSingleSheet64.Seed(
                    "A1_REGISTERED_EMPTY",841,594,true),
                new NativeOrthoSingleSheet64.Seed(
                    "A0_NOT_REGISTERED",1189,841,false),
                new NativeOrthoSingleSheet64.Seed(
                    "A0_REGISTERED_EMPTY",1189,841,true),
                new NativeOrthoSingleSheet64.Seed(
                    "LARGE_REGISTERED_EMPTY",1400,1000,true)
            });
        Check(selected?.Path=="A0_REGISTERED_EMPTY",
            "choose smallest *registered empty* fitting paper, never unregistered A0");
        Check(NativeOrthoSingleSheet64.SmallestFitting(
            geometry,views,10,new[]{
                new NativeOrthoSingleSheet64.Seed("A1",841,594,true),
                new NativeOrthoSingleSheet64.Seed("FAKE_A0",1189,841,false)
            })==null,
            "with A1 only, refuse to generate a second page or change scale");
        Check(NativeOrthoSingleSheet64.SmallestFitting(
            geometry,views,1,new[]{
                new NativeOrthoSingleSheet64.Seed("A1",841,594,true),
                new NativeOrthoSingleSheet64.Seed("A0",1189,841,true)
            })==null,
            "do not claim all ten 1:1 fit on A0 if they physically exceed paper");
        var actual=NativeOrthoTenViewPlan.Build(geometry,1189,841)
            with{Denominator=10,Scale=.1};
        var placement=NativeOrthoExactScalePlacement61.Build(
            actual,views,1189,841);
        Check(placement.Views.Count==10&&placement.Titles.Count==10&&
            placement.Views.Select(v=>v.Name).SequenceEqual(views),
            "one native A0 sheet covers exact ten preset views once");
        Check(placement.Views.Max(v=>v.CenterX+v.PaperFrameWidth/2)<
                1189*NativeOrthoEffectivePaper59.RightTitleBlockStartRatio,
            "all ten A0 frames clear right-side title block");
        Console.WriteLine(
            "REV64 ONE SHEET: exact 1:10 all ten on validated A0, "+
            "no clipped A1 or unregistered seeds, no autoscale/split PASS");
    }
    private static void Check(bool yes,string message)
    {
        if(!yes)throw new Exception("REV64 single native sheet: "+message);
    }
}
