using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev259NativeSheetPlacementTests
{
    private static void Require(bool pass,string problem)
    {
        if(!pass)throw new Exception("REV59 native page placement: "+problem);
    }
    private static void MustFail(Action op,string problem)
    {
        try{op();}
        catch(InvalidOperationException){return;}
        throw new Exception("REV59 native page placement did NOT block "+problem);
    }
    internal static void Run()
    {
        // Live TEST5 evidence: PlotPaperSize was 594x841 while the
        // actual Ortho border in DWG + /Rotate=270 PDF was 841x594.
        var sheet=NativeOrthoEffectivePaper59.FromMedia(594,841,270);
        Require(sheet.Transposed&&sheet.WidthMm==841&&
            sheet.HeightMm==594,
            "270-degree native A1 must use landscape paper-space limits");
        var ninety=NativeOrthoEffectivePaper59.FromMedia(594,841,90);
        Require(ninety.WidthMm==841&&ninety.HeightMm==594,
            "90-degree media also transposes physical sheet dimensions");
        var portrait=NativeOrthoEffectivePaper59.FromMedia(594,841,0);
        Require(portrait.WidthMm==594&&portrait.HeightMm==841,
            "no rotation preserves portrait layout");
        var halfTurn=NativeOrthoEffectivePaper59.FromMedia(594,841,180);
        Require(halfTurn.WidthMm==594&&halfTurn.HeightMm==841,
            "180-degree rotation preserves original sheet bounds");

        // Actual original TOP center Y723.26 on 594-high actual drawing
        // border is off-sheet, despite looking valid on raw 841 media.
        var oldFrame=new NativeOrthoEffectivePaper59.ViewFrame(
            "TOP",118.8,723.26,28,86.376);
        MustFail(()=>NativeOrthoEffectivePaper59.RequireInsideUsableArea(
            sheet,new[]{oldFrame}),
            "original off-border TOP view from TEST5 screenshot");

        // Same 10 baseline view frame sizes, now placed in 841x594
        // effective paper coordinates. Title band 4+6 mm below views.
        string[] names={"TOP","BOTTOM","LEFT","RIGHT","FRONT",
            "BACK","NW","NE","SW","SE"};
        double[] widths={28,36,86.376,86.376,34,31,
            69.986,69.986,69.986,69.986};
        double[] heights={86.376,86.376,42.234,42.234,
            42.234,42.234,74.891,74.891,74.891,74.891};
        var frames=names.Select((name,i)=>
            new NativeOrthoEffectivePaper59.ViewFrame(name,
                sheet.WidthMm*(i%2==0?.20:.57),
                sheet.HeightMm*(new[]{.86,.70,.54,.38,.22}[i/2]),
                widths[i],heights[i])).ToArray();
        var captions=frames.Select(f=>
            new NativeOrthoEffectivePaper59.CaptionFrame(
                f.Name,f.X,f.Y-f.Height/2-4,f.Width,6)).ToArray();
        NativeOrthoEffectivePaper59.RequireInsideUsableArea(
            sheet,frames,captions,requireTen:true);
        Require(frames.All(f=>f.Y+f.Height/2<
            sheet.HeightMm*NativeOrthoEffectivePaper59.TopMarginRatio),
            "all ten native viewports must fit inside landscape top border");

        MustFail(()=>NativeOrthoEffectivePaper59.RequireInsideUsableArea(
            sheet,frames.Take(9).ToArray(),captions,requireTen:true),
            "missing tenth registered native view");
        var clippedRight=frames.ToArray();
        clippedRight[1]=clippedRight[1] with {X=sheet.WidthMm*.77};
        MustFail(()=>NativeOrthoEffectivePaper59.RequireInsideUsableArea(
            sheet,clippedRight,captions,requireTen:true),
            "right-side title-block overlap");
        var clippedTitle=captions.ToArray();
        clippedTitle[8]=clippedTitle[8] with {TopY=50};
        MustFail(()=>NativeOrthoEffectivePaper59.RequireInsideUsableArea(
            sheet,frames,clippedTitle,requireTen:true),
            "caption below bottom safe drawing border");
        var outsideLeft=frames.ToArray();
        outsideLeft[0]=outsideLeft[0] with {X=0};
        MustFail(()=>NativeOrthoEffectivePaper59.RequireInsideUsableArea(
            sheet,outsideLeft,captions,requireTen:true),
            "viewport crossing left sheet border");
        Console.WriteLine(
            "REV59 native portrait-media to effective landscape 841x594 view placement, 10 view/caption fit, original TOP clipping and right-titleblock gates PASS");
    }
}
