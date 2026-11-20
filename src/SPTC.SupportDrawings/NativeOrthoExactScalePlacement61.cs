using System;
using System.Collections.Generic;
using System.Globalization;
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
        internal double RequiredExpansion {get;}
        internal DoesNotFitException(string message,double requiredExpansion=1)
            :base(message) => RequiredExpansion=requiredExpansion;
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
        double paperWidth,double paperHeight,
        bool bypassTitleBlock=false,
        double mandatoryAnnotationHeightMm=0) =>
        Build(plan,selectedViews,NativeOrthoEffectivePaper59.FromMedia(
            paperWidth,paperHeight,0),bypassTitleBlock,mandatoryAnnotationHeightMm);

    internal static NativeOrthoTenViewAutoPlacement26.Result Build(
        NativeOrthoTenViewPlan.Plan plan,IReadOnlyList<string> selectedViews,
        NativeOrthoEffectivePaper59.Sheet sheet,bool bypassTitleBlock=false,
        double mandatoryAnnotationHeightMm=0)
    {
        double paperWidth=sheet.WidthMm,paperHeight=sheet.HeightMm;
        if(bypassTitleBlock&&!NativeOrthoTitleBlockBypass69.Eligible(
            paperWidth,paperHeight,plan?.Denominator??0,
            selectedViews?.Count??0))
            throw new ArgumentException(
                "REV70 bypass requires explicit 1:N All10 on valid registered Ortho paper media.");
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
        double right=NativeOrthoTitleBlockBypass69.Right(
            paperWidth,bypassTitleBlock);
        double bottom=NativeOrthoTitleBlockBypass69.Bottom(
            paperHeight,bypassTitleBlock);
        double top=NativeOrthoTitleBlockBypass69.Top(
            paperHeight,bypassTitleBlock);
        double availableWidth=right-left;
        double availableHeight=top-bottom;
        const double columnGap=6;
        const double rowGap=5;
        // REV65 extends, but DOES NOT relax, the REV61 physical fit
        // check. Preserve the original order as the FIRST candidate,
        // then try bounded, deterministic groups of similar heights
        // and widths to reduce wasted row/column space. Camera names,
        // projection directions, scale and final result order do not
        // change. The native title/viewport guards validate every
        // candidate identically.
        int[] original=Enumerable.Range(0,views.Length).ToArray();
        var orders=new (string Label,int[] Indices)[]
        {
            ("PRESET",original),
            ("HEIGHT_DESC",original.OrderByDescending(i=>views[i].Height)
                .ThenByDescending(i=>views[i].Width).ToArray()),
            ("WIDTH_DESC",original.OrderByDescending(i=>views[i].Width)
                .ThenByDescending(i=>views[i].Height).ToArray()),
            ("AREA_DESC",original.OrderByDescending(i=>
                views[i].Width*views[i].Height).ToArray()),
            ("HEIGHT_ASC",original.OrderBy(i=>views[i].Height)
                .ThenBy(i=>views[i].Width).ToArray()),
            ("WIDTH_ASC",original.OrderBy(i=>views[i].Width)
                .ThenBy(i=>views[i].Height).ToArray())
        };
        var uniqueOrders=new HashSet<string>(StringComparer.Ordinal);
        string nearest="";
        double bestShortfall=double.PositiveInfinity;
        double requiredExpansion=double.PositiveInfinity;
        string firstTitleCollision="";
        // Use exactly the existing grid-column preferences to preserve
        // the tested 2-column layout wherever it already passes.
        var columnOptions=new[]{2,1,3,4}
            .Where(x=>x<=views.Length).Distinct().ToArray();
        foreach(int cols in columnOptions)
        foreach(var order in orders)
        {
            // A distinct layout ordering is needed for the same cols;
            // the same ordering at DIFFERENT column counts is different.
            string key=cols+":"+string.Join(",",order.Indices);
            if(!uniqueOrders.Add(key))continue;
            int rows=(views.Length+cols-1)/cols;
            var colWidths=new double[cols];
            var rowHeights=new double[rows];
            for(int i=0;i<views.Length;i++)
            {
                var item=views[order.Indices[i]];
                int c=i%cols,r=i/cols;
                colWidths[c]=Math.Max(colWidths[c],
                    item.Width+AnnotationReserveMm);
                rowHeights[r]=Math.Max(rowHeights[r],
                    item.Height+CaptionAndRowReserveMm);
            }
            double totalWidth=colWidths.Sum()+columnGap*(cols-1);
            double totalHeight=rowHeights.Sum()+rowGap*(rows-1);
            requiredExpansion=Math.Min(requiredExpansion,Math.Max(
                (totalWidth+2)/availableWidth,(totalHeight+2)/availableHeight));
            double shortageW=Math.Max(0,totalWidth-(availableWidth-2));
            double shortageH=Math.Max(0,totalHeight-(availableHeight-2));
            double normalShortfall=shortageW/Math.Max(1,availableWidth)+
                shortageH/Math.Max(1,availableHeight);
            if(normalShortfall<bestShortfall)
            {
                bestShortfall=normalShortfall;
                nearest=cols+"x"+rows+" "+order.Label+
                    " required "+totalWidth.ToString("0.#",CultureInfo.InvariantCulture)+
                    " x "+totalHeight.ToString("0.#",CultureInfo.InvariantCulture)+
                    " mm; available "+
                    (availableWidth-2).ToString("0.#",CultureInfo.InvariantCulture)+
                    " x "+(availableHeight-2).ToString("0.#",
                        CultureInfo.InvariantCulture)+
                    " mm; shortage "+
                    shortageW.ToString("0.#",CultureInfo.InvariantCulture)+
                    " x "+shortageH.ToString("0.#",CultureInfo.InvariantCulture)+" mm";
            }
            if(shortageW>0||shortageH>0)continue;

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
                int sourceIndex=order.Indices[i];
                var item=views[sourceIndex];
                int c=i%cols,r=i/cols;
                double cx=colLefts[c]+item.Width/2;
                double cy=rowTops[r]-item.Height/2;
                // Preserve original preset order for downstream native
                // registration even when the paper positions are reflowed.
                frames[sourceIndex]=new NativeOrthoTenViewPlan.Placement(
                    item.Name,cx,cy,item.Width,item.Height);
            }
            try
            {
                var titles=NativeOrthoViewTitlePlan.BuildTitles(
                    frames.Select(v=>new NativeOrthoViewTitlePlan.Frame(
                        v.Name,v.CenterX,v.CenterY,
                        v.PaperFrameWidth,v.PaperFrameHeight)).ToArray(),
                    paperWidth,paperHeight,
                    allowSubset:views.Length!=10,
                    bypassTitleBlock:bypassTitleBlock);
                NativeOrthoEffectivePaper59.RequireInsideUsableArea(
                    sheet,frames.Select(v=>
                        new NativeOrthoEffectivePaper59.ViewFrame(
                            v.Name,v.CenterX,v.CenterY,
                            v.PaperFrameWidth,v.PaperFrameHeight)).ToArray(),
                    titles.Select(t=>
                        new NativeOrthoEffectivePaper59.CaptionFrame(
                            t.Name,t.CenterX,t.TopY,t.Width,t.Height)).ToArray(),
                    requireTen:views.Length==10,
                    bypassTitleBlock:bypassTitleBlock);
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
                // REV71: views and native titles fitting on a registered
                // paper sheet does not guarantee that every mandatory
                // member/pipe annotation fits. Check real free paper
                // rectangles BEFORE returning the best candidate; try
                // the next exact-scale grid on insufficient capacity.
                if(mandatoryAnnotationHeightMm>0&&
                   !SupportOrthoAnnotationCapacity71.TryPreflight(
                       frames.Select(v=>new NativeOrthoViewTitlePlan.Frame(
                           v.Name,v.CenterX,v.CenterY,
                           v.PaperFrameWidth,v.PaperFrameHeight)).ToArray(),
                       titles,paperWidth,paperHeight,
                       bypassTitleBlock,mandatoryAnnotationHeightMm,
                       out string capacityReason))
                {
                    if(firstTitleCollision.Length==0)
                        firstTitleCollision=cols+"x"+rows+" "+order.Label+
                            ": "+capacityReason;
                    continue;
                }
                var first=frames[0];
                return new NativeOrthoTenViewAutoPlacement26.Result(
                    first.CenterX,first.CenterY,
                    first.CenterX,first.CenterY,
                    first.CenterX,first.CenterY,
                    0,false,frames,titles);
            }
            catch(InvalidOperationException ex)
            {
                // An otherwise physically fitting layout can still fail
                // the existing caption, sheet border and overlap checks.
                // Keep the exact reason to distinguish geometry shortage
                // from title/collision rejection in REV65 diagnostics.
                if(firstTitleCollision.Length==0)
                    firstTitleCollision=cols+"x"+rows+" "+order.Label+
                        ": "+ex.Message;
            }
        }
        throw new DoesNotFitException(
            "Requested 1:"+plan.Denominator+" cannot fit "+
            views.Length+" selected native view(s) on this paper sheet "+
            (bypassTitleBlock?
                "even after bypassing the reserved title-block area, "+
                "inside the physical page margins and annotation clearances. ":
                "with the title block and annotation clearances. ")+
            "Choose fewer views or a larger registered empty Ortho seed "+
            "with suitable plot media. The plugin never changes 1:"+
            plan.Denominator+" into a different scale or draws off-sheet. "+
            "REV65 closest layout: "+nearest+
            (firstTitleCollision.Length>0?
                "; physical size fits but native title/frame validation "+
                "failed: "+firstTitleCollision:""),requiredExpansion);
    }
}
