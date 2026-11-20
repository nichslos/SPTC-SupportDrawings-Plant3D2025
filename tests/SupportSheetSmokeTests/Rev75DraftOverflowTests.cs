using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev75DraftOverflowTests
{
    private static void Need(bool valid,string reason)
    {
        if(!valid)throw new Exception("One DWG draft overflow: "+reason);
    }

    internal static void Run()
    {
        var physical=NativeOrthoEffectivePaper59.FromMedia(841,1189,270);
        var members=new SupportOrthoMemberCalloutPlan27.Member[] {
            new("A","Channel",10,12,20,true,"TOS 20","MODEL"),
            new("B","Angle",5,8,15,false,null,"MODEL")
        };
        var small=NativeOrthoLayout.Build(new BoundingBox3(0,0,0,15,30,25),1,0);
        var names=NativeOrthoTenViewPlan.Build(small,physical.WidthMm,physical.HeightMm)
            .Views.Select(v=>v.Name).ToArray();
        var fitting=NativeOrthoDraftOverflow75.Choose(physical,small,members,names,1);
        Need(fitting==physical,"a fitting 1:1 draft keeps the original rotated sheet");

        foreach(var box in new[]{new BoundingBox3(0,0,0,180,994,919),
            new BoundingBox3(0,0,0,180,1234,603)})
        {
            var geometry=NativeOrthoLayout.Build(box,1,0);
            Need(!NativeOrthoSingleSheet64.TryExactTenFit(geometry,names,1,
                physical.WidthMm,physical.HeightMm,out _),"these views exceed the physical sheet");
            var canvas=NativeOrthoDraftOverflow75.Choose(physical,geometry,members,names,1);
            Need(Math.Max(canvas.WidthMm,canvas.HeightMm)>5000&&
                canvas.MediaWidthMm==physical.MediaWidthMm&&
                canvas.MediaHeightMm==physical.MediaHeightMm&&
                canvas.RotationDegrees==physical.RotationDegrees,
                "off-sheet drafting keeps the registered medium and plot rotation");

            // Repeat the drawing generator's final placement with the chosen canvas.
            var plan=NativeOrthoTenViewPlan.Build(geometry,canvas.WidthMm,canvas.HeightMm)
                with {Denominator=1,Scale=1};
            double labelHeight=SupportOrthoAnnotationCapacity71.RequiredHeight(
                members,canvas.WidthMm,canvas.HeightMm);
            var result=NativeOrthoExactScalePlacement61.Build(plan,names,canvas,
                mandatoryAnnotationHeightMm:labelHeight);
            Need(result.Views.Select(v=>v.Name).SequenceEqual(names)&&
                result.Titles.Select(t=>t.Name).SequenceEqual(names),
                "one layout contains a frame and title for every view");
            foreach(var frame in result.Views)
            {
                var v=plan.Views.Single(s=>s.Name==frame.Name);
                double width=Math.Max(NativeOrthoViewTitlePlan.MinimumFrameWidth(v.Name),
                    v.ModelWmm*NativeOrthoExactScalePlacement61.ModelFrameMargin);
                double height=Math.Max(5,v.ModelHmm*NativeOrthoExactScalePlacement61.ModelFrameMargin);
                Need(Math.Abs(frame.PaperFrameWidth-width)<1e-8&&
                    Math.Abs(frame.PaperFrameHeight-height)<1e-8,v.Name+" retains its full-size projection");
            }
            bool invalidMediaRejected=false;
            try{NativeOrthoEffectivePaper59.FromMedia(canvas.WidthMm,canvas.HeightMm,0);}
            catch(ArgumentException){invalidMediaRejected=true;}
            Need(invalidMediaRejected,"the draft canvas cannot be registered as physical plot media");
        }
        bool invalidScaleRejected=false;
        try{NativeOrthoDraftOverflow75.Choose(physical,small,members,names,0);}
        catch(ArgumentException){invalidScaleRejected=true;}
        Need(invalidScaleRejected,"draft overflow requires an explicit scale");
        Console.WriteLine("One DWG 1:1, fitting and off-sheet ten-view layouts with labels PASS");
    }
}
