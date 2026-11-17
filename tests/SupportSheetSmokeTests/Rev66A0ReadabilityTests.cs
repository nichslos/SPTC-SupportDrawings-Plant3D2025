using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev66A0ReadabilityTests
{
    private static void Need(bool yes,string reason)
    {
        if(!yes)throw new Exception("REV66 A0 typography: "+reason);
    }

    internal static void Run()
    {
        // REV59 A1 AUTO is still the untouched 1.75mm, 5.4mm two-line
        // policy. Larger paper can reserve actual extra text height.
        Need(!SupportOrthoPaperTypography66.IsLargePaper(841,594)&&
            SupportOrthoPaperTypography66.WidthCap(841,594)==114&&
            SupportOrthoPaperTypography66.TextHeight(841,594)==
                SupportOrthoMemberCalloutPlan27.TextHeightMm&&
            SupportOrthoPaperTypography66.LineAdvance(841,594)==
                SupportOrthoMemberCalloutPlan27.TextLineHeightMm&&
            SupportOrthoPaperTypography66.RowSpacing(841,594)==
                SupportOrthoMemberCalloutPlan27.RowSpacingMm&&
            SupportOrthoPaperTypography66.ExtraDimTrials(841,594)==14,
            "REV59 A1 typography, row clearance and dimension budget unchanged");
        Need(SupportOrthoPaperTypography66.IsLargePaper(1189,841)&&
            SupportOrthoPaperTypography66.WidthCap(1189,841)==160&&
            SupportOrthoPaperTypography66.TextHeight(1189,841)==2.15&&
            SupportOrthoPaperTypography66.LineAdvance(1189,841)==3.5&&
            SupportOrthoPaperTypography66.RowSpacing(1189,841)==2.2&&
            SupportOrthoPaperTypography66.ExtraDimTrials(1189,841)==20,
            "A0 reserves wider labels, taller lines and bounded safe retries");

        var envelope=NativeOrthoLayout.Build(
            new BoundingBox3(0,0,0,180,1234,603),1.0,0);
        var plan=NativeOrthoTenViewPlan.Build(envelope,1189,841)
            with {Denominator=10,Scale=.1};
        string[] presets={"TOP","BOTTOM","LEFT","RIGHT","FRONT",
            "BACK","NW","NE","SW","SE"};
        var placed=NativeOrthoExactScalePlacement61.Build(
            plan,presets,1189,841);
        Need(placed.Views.Count==10&&placed.Titles.Count==10&&
            placed.Views.Select(v=>v.Name).SequenceEqual(presets),
            "A0 1:10 stays one complete native ten-view set");
        var topFrame=placed.Views.Single(v=>v.Name=="TOP");
        var topTitle=placed.Titles.Single(t=>t.Name=="TOP");
        Need(topTitle.Width>topFrame.PaperFrameWidth&&
            topTitle.Width>=NativeOrthoViewTitlePlan.MinimumFrameWidth("TOP")+
                SupportOrthoPaperTypography66.A0CaptionExtraWidthMm,
            "A0 TOP title has independent nonwrapping width without enlarging viewport");
        NativeOrthoEffectivePaper59.RequireInsideUsableArea(
            NativeOrthoEffectivePaper59.FromMedia(1189,841,0),
            placed.Views.Select(v=>new NativeOrthoEffectivePaper59.ViewFrame(
                v.Name,v.CenterX,v.CenterY,
                v.PaperFrameWidth,v.PaperFrameHeight)).ToArray(),
            placed.Titles.Select(t=>new NativeOrthoEffectivePaper59.CaptionFrame(
                t.Name,t.CenterX,t.TopY,t.Width,t.Height)).ToArray(),
            requireTen:true);
        Need(SupportOrthoPaperTypography66.CaptionWidth(
            "TOP",28,841,594)==28,
            "A1 narrow titles retain historical width");

        var cam=new SupportOrthoMemberCalloutPlan17.Camera(
            "TOP",230,400,100,100,0,0,1,0,0,0,.1);
        var member=new SupportOrthoMemberCalloutPlan27.Member(
            "8A4","Sloping channel 100x50 web-5 fl-7.5",
            0,0,0,true,"+1803.9855 mm","GEOMETRIC_TOP_APPROX");
        SupportOrthoMemberCalloutPlan27.Result Callouts(
            double w,double h)
        {
            var titles=NativeOrthoViewTitlePlan.BuildTitles(
                new[]{new NativeOrthoViewTitlePlan.Frame(
                    "TOP",cam.X,cam.Y,cam.W,cam.H)},
                w,h,allowSubset:true);
            return SupportOrthoMemberCalloutPlan27.Build(
                new[]{cam},titles,new[]{member},w,h,allowSubset:true);
        }
        var oldCallout=Callouts(841,594);
        var largeCallout=Callouts(1189,841);
        Need(oldCallout.Callouts.Count==1&&
            Math.Abs(oldCallout.Callouts[0].LabelHeight-5.4)<1e-6&&
            Math.Abs(oldCallout.Callouts[0].LabelWidth-114)<1e-6,
            "A1 original two-line native callout placement unchanged");
        Need(largeCallout.Callouts.Count==1&&
            Math.Abs(largeCallout.Callouts[0].LabelHeight-7.0)<1e-6&&
            Math.Abs(largeCallout.Callouts[0].LabelWidth-160)<1e-6&&
            largeCallout.ViewWithoutLabels.Count==0,
            "A0 permits wider text and real extra vertical line clearance");
        Need(largeCallout.Callouts[0].AnchorX==
            oldCallout.Callouts[0].AnchorX&&
            largeCallout.Callouts[0].AnchorY==
            oldCallout.Callouts[0].AnchorY&&
            largeCallout.Callouts[0].Handle==
            oldCallout.Callouts[0].Handle,
            "larger paper typography never changes source member, camera or leader origin");

        Console.WriteLine(
            "REV66 A0 1:10: ten native views, wide title bands, "+
            "2.15mm preferred font, increased label bounds, "+
            "A1 auto baseline and origin preserved PASS");
    }
}
