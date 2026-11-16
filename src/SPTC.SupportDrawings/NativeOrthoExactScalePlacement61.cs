using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV61: an explicit paper scale means EXACT 1:N, never automatic 1:N.
/// Repack ONLY when the explicit denominator is smaller than the automatic
/// 10-view fit. A bounded grid may fit a subset or all ten views without
/// altering the source cube, native camera, text sizes or PDF output.
/// If actual model projections do not fit even one safe paper-space grid,
/// fail BEFORE native drawing registration; physical space cannot be faked.
/// </summary>
internal static class NativeOrthoExactScalePlacement61
{
    internal sealed class DoesNotFitException:InvalidOperationException
    {
        internal DoesNotFitException(string message):base(message) {}
    }

    internal const double ModelFrameMargin=1.05;
    internal const double AnnotationReserveMm=
        SupportOrthoMemberCalloutPlan27.LabelGapFromFrameMm+114+5;
    internal const double CaptionAndRowReserveMm=
        NativeOrthoViewTitlePlan.GapBelowFrameMm+
        NativeOrthoViewTitlePlan.CaptionBandHeightMm+9;

    internal static bool NeedsAdaptiveLayout(int requestedDenominator,
        int automaticDenominator) =>
        requestedDenominator>0&&
        requestedDenominator<automaticDenominator;

    internal static NativeOrthoTenViewAutoPlacement26.Result Build(
        NativeOrthoTenViewPlan.Plan plan,
        IReadOnlyList<string> selectedViews,
        double paperWidth,double paperHeight)
    {
        if(plan==null||plan.Views==null||plan.Views.Count!=10||
           !double.IsFinite(plan.Scale)||plan.Scale<=0||
           !double.IsFinite(paperWidth)||!double.IsFinite(paperHeight)||
           paperWidth<=0||paperHeight<=0||
           selectedViews==null||selectedViews.Count<1||
           selectedViews.Count>10)
            throw new ArgumentException("REV61 invalid exact scale sheet inputs.");
        var names=new HashSet<string>(selectedViews,
            StringComparer.OrdinalIgnoreCase);
        if(names.Count!=selectedViews.Count||
           names.Except(plan.Views.Select(x=>x.Name),
               StringComparer.OrdinalIgnoreCase).Any())
            throw new ArgumentException("REV61 invalid or duplicate view selections.");

        var views=plan.Views.Where(v=>names.Contains(v.Name))
            .Select(v=>new {
                v.Name,
                Width=Math.Max(
                    NativeOrthoViewTitlePlan.MinimumFrameWidth(v.Name),
                    v.ModelWmm*plan.Scale*ModelFrameMargin),
                Height=Math.Max(5,v.ModelHmm*plan.Scale*ModelFrameMargin)
            }).ToArray();
        if(views.Any(v=>!double.IsFinite(v.Width)||
           !double.IsFinite(v.Height)||v.Width<=0||v.Height<=0))
            throw new ArgumentException("REV61 invalid scaled view geometry.");

        double left=paperWidth*
            NativeOrthoEffectivePaper59.LeftMarginRatio;
        double right=paperWidth*
            NativeOrthoEffectivePaper59.RightTitleBlockStartRatio;
        double bottom=paperHeight*
            NativeOrthoEffectivePaper59.BottomMarginRatio;
        double top=paperHeight*
            NativeOrthoEffectivePaper59.TopMarginRatio;
        double availableWidth=right-left;
        double availableHeight=top-bottom;
        const double columnGap=6;
        const double rowGap=5;
        // Respect the established 2-column layout preference; use
        // other grid counts only to accommodate a requested exact scale.
        var columnOptions=new[]{2,1,3,4}
            .Where(x=>x<=views.Length).Distinct().ToArray();
        foreach(int cols in columnOptions)
        {
            int rows=(views.Length+cols-1)/cols;
            var colWidths=new double[cols];
            var rowHeights=new double[rows];
            for(int i=0;i<views.Length;i++)
            {
                int c=i%cols,r=i/cols;
                colWidths[c]=Math.Max(colWidths[c],
                    views[i].Width+AnnotationReserveMm);
                rowHeights[r]=Math.Max(rowHeights[r],
                    views[i].Height+CaptionAndRowReserveMm);
            }
            double totalWidth=colWidths.Sum()+columnGap*(cols-1);
            double totalHeight=rowHeights.Sum()+rowGap*(rows-1);
            if(totalWidth>availableWidth-2||
               totalHeight>availableHeight-2)
                continue;

            double startX=left+(availableWidth-totalWidth)/2;
            double startY=top-(availableHeight-totalHeight)/2;
            var colLefts=new double[cols];
            var rowTops=new double[rows];
            double x=startX;
            for(int c=0;c<cols;c++)
            {
                colLefts[c]=x;
                x+=colWidths[c]+columnGap;
            }
            double y=startY;
            for(int r=0;r<rows;r++)
            {
                rowTops[r]=y;
                y-=rowHeights[r]+rowGap;
            }
            var frames=new NativeOrthoTenViewPlan.Placement[views.Length];
            for(int i=0;i<views.Length;i++)
            {
                int c=i%cols,r=i/cols;
                double cx=colLefts[c]+views[i].Width/2;
                double cy=rowTops[r]-views[i].Height/2;
                frames[i]=new NativeOrthoTenViewPlan.Placement(
                    views[i].Name,cx,cy,views[i].Width,views[i].Height);
            }
            try
            {
                var titles=NativeOrthoViewTitlePlan.BuildTitles(
                    frames.Select(v=>new NativeOrthoViewTitlePlan.Frame(
                        v.Name,v.CenterX,v.CenterY,
                        v.PaperFrameWidth,v.PaperFrameHeight)).ToArray(),
                    paperWidth,paperHeight,
                    allowSubset:views.Length!=10);
                var paper=NativeOrthoEffectivePaper59.FromMedia(
                    paperWidth,paperHeight,0);
                NativeOrthoEffectivePaper59.RequireInsideUsableArea(
                    paper,frames.Select(v=>
                        new NativeOrthoEffectivePaper59.ViewFrame(
                            v.Name,v.CenterX,v.CenterY,
                            v.PaperFrameWidth,v.PaperFrameHeight)).ToArray(),
                    titles.Select(t=>
                        new NativeOrthoEffectivePaper59.CaptionFrame(
                            t.Name,t.CenterX,t.TopY,t.Width,t.Height)).ToArray(),
                    requireTen:views.Length==10);
                bool overlap=false;
                for(int i=0;i<frames.Length;i++)
                    for(int j=i+1;j<frames.Length;j++)
                    {
                        var a=frames[i];var b=frames[j];
                        if(Math.Abs(a.CenterX-b.CenterX)<
                            (a.PaperFrameWidth+b.PaperFrameWidth)/2 &&
                           Math.Abs(a.CenterY-b.CenterY)<
                            (a.PaperFrameHeight+b.PaperFrameHeight)/2)
                            overlap=true;
                    }
                if(overlap)continue;
                var first=frames[0];
                return new NativeOrthoTenViewAutoPlacement26.Result(
                    first.CenterX,first.CenterY,
                    first.CenterX,first.CenterY,
                    first.CenterX,first.CenterY,
                    0,false,frames,titles);
            }
            catch(InvalidOperationException)
            {
                // Another bounded grid might fit without collisions.
            }
        }
        throw new DoesNotFitException(
            "Requested 1:"+plan.Denominator+" cannot fit "+
            views.Length+" selected native view(s) on this paper sheet "+
            "with the title block and annotation clearances. "+
            "Choose fewer views or a larger registered empty Ortho seed "+
            "with suitable plot media. The plugin never changes 1:"+
            plan.Denominator+" into a different scale or draws off-sheet.");
    }
}
