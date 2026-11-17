using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV71: source-count and PAPER-RECTANGLE based native annotation
/// capacity. No support handles, view names, member counts or sheet
/// dimensions are hardcoded. The geometry of EVERY other native
/// viewport, EVERY view title and accepted prior callout controls the
/// available label positions. Existing per-view ordering and all
/// source/model coordinates are preserved.
/// </summary>
internal static class SupportOrthoAnnotationCapacity71
{
    internal sealed record Rect(double L,double B,double R,double T);
    internal sealed record Band(double Bottom,double Top,
        bool AllLabelsFit,double CapacityMm);
    internal sealed record Gutter(double X,double Width);

    internal static double RequiredHeight(
        IReadOnlyList<SupportOrthoMemberCalloutPlan27.Member> members,
        double paperW,double paperH)
    {
        if(members==null)throw new ArgumentNullException(nameof(members));
        var required=members.Where(m=>m.XrefDrawingName==null).ToArray();
        if(required.Length==0)return 0;
        double step=SupportOrthoPaperTypography66.LineAdvance(paperW,paperH);
        double gap=SupportOrthoPaperTypography66.RowSpacing(paperW,paperH);
        return required.Sum(m=>step*(m.HasTos||m.PipeBopSource!=null?2:1))+
            (required.Length-1)*gap;
    }

    // The right gutter is bounded by REAL horizontally-overlapping
    // viewports, not by unrelated view center positions or names.
    internal static bool TryGutter(
        NativeOrthoViewTitlePlan.Frame view,
        IReadOnlyList<NativeOrthoViewTitlePlan.Frame> frames,
        double paperW,double paperH,bool bypass,
        out Gutter gutter)
    {
        double x=view.X+view.W/2+
            SupportOrthoMemberCalloutPlan27.LabelGapFromFrameMm;
        var rightNeighbor=frames
            .Where(v=>v.X>view.X+1e-5&&
                v.Y-v.H/2<view.Y+view.H/2&&
                view.Y-view.H/2<v.Y+v.H/2)
            .OrderBy(v=>v.X-v.W/2).FirstOrDefault();
        double right=rightNeighbor!=null?
            rightNeighbor.X-rightNeighbor.W/2-5:
            (bypass?NativeOrthoTitleBlockBypass69.Right(paperW,true)-3:
                SupportOrthoPaperTypography66.IsLargePaper(paperW,paperH)?
                    paperW*.75-3:paperW*.78-3);
        double width=Math.Min(
            SupportOrthoPaperTypography66.WidthCap(paperW,paperH),
            right-x);
        gutter=new Gutter(x,width);
        return double.IsFinite(width)&&width>=54;
    }

    internal static bool Overlaps(double aL,double aB,double aR,double aT,
        double bL,double bB,double bR,double bT) =>
        aL<bR&&bL<aR&&aB<bT&&bB<aT;

    internal static bool IsFree(
        IReadOnlyList<NativeOrthoViewTitlePlan.Frame> frames,
        IReadOnlyList<NativeOrthoViewTitlePlan.Title> titles,
        IReadOnlyList<Rect> reserved,
        double x,double width,double bottom,double top)
    {
        if(!double.IsFinite(x)||!double.IsFinite(width)||
           !double.IsFinite(bottom)||!double.IsFinite(top)||
           width<=0||top<=bottom)return false;
        bool Intersects(Rect r)=>Overlaps(
            x,bottom,x+width,top,r.L,r.B,r.R,r.T);
        return !frames.Any(f=>Intersects(new Rect(
                   f.X-f.W/2,f.Y-f.H/2,
                   f.X+f.W/2,f.Y+f.H/2)))&&
               !titles.Any(t=>Intersects(new Rect(
                   t.CenterX-t.Width/2,t.TopY-t.Height,
                   t.CenterX+t.Width/2,t.TopY)))&&
               !reserved.Any(Intersects);
    }

    /// <summary>
    /// Choose a physically free vertical interval large enough for
    /// EVERY native member/pipe label. XREF filename labels are
    /// included only when their actual total fits, never ahead of
    /// mandatory labels. Choose the closest gap to the current native
    /// camera's Y, not the nearest unrelated viewport ROW CENTER.
    /// </summary>
    internal static bool TryFindBand(
        IReadOnlyList<NativeOrthoViewTitlePlan.Frame> frames,
        IReadOnlyList<NativeOrthoViewTitlePlan.Title> titles,
        IReadOnlyList<Rect> reserved,
        double x,double width,double preferredY,
        double mandatoryHeight,double allHeight,
        double paperW,double paperH,bool bypass,
        out Band band)
    {
        band=new Band(0,0,false,0);
        if(frames==null||titles==null||reserved==null||
           !double.IsFinite(mandatoryHeight)||mandatoryHeight<0||
           !double.IsFinite(allHeight)||allHeight<mandatoryHeight||
           !double.IsFinite(x)||!double.IsFinite(width)||
           !double.IsFinite(preferredY)||width<=0)
            return false;
        double padding=SupportOrthoPaperTypography66.RowSpacing(
            paperW,paperH)/2;
        double minY=NativeOrthoTitleBlockBypass69.Bottom(
                paperH,bypass)+padding;
        double maxY=NativeOrthoTitleBlockBypass69.Top(
                paperH,bypass)-padding;
        if(maxY-minY<mandatoryHeight-1e-6)return false;
        var obstacles=new List<Rect>();
        obstacles.AddRange(frames.Select(f=>new Rect(
            f.X-f.W/2,f.Y-f.H/2,f.X+f.W/2,f.Y+f.H/2)));
        obstacles.AddRange(titles.Select(t=>new Rect(
            t.CenterX-t.Width/2,t.TopY-t.Height,
            t.CenterX+t.Width/2,t.TopY)));
        obstacles.AddRange(reserved);
        var blocking=obstacles
            .Where(r=>r.L<x+width&&x<r.R&&
                r.T+padding>minY&&r.B-padding<maxY)
            .Select(r=>new Rect(r.L,
                Math.Max(minY,r.B-padding),r.R,
                Math.Min(maxY,r.T+padding)))
            .OrderBy(r=>r.B).ToArray();
        var gaps=new List<(double Bottom,double Top)>();
        double cursor=minY;
        foreach(var r in blocking)
        {
            if(r.B>cursor+1e-6)
                gaps.Add((cursor,r.B));
            cursor=Math.Max(cursor,r.T);
        }
        if(cursor<maxY-1e-6)gaps.Add((cursor,maxY));
        var best=gaps.Where(g=>g.Top-g.Bottom>=mandatoryHeight-1e-6)
            .Select(g=>new{
                g.Bottom,g.Top,
                FitsAll=g.Top-g.Bottom>=allHeight-1e-6,
                Distance=Math.Abs(Math.Clamp(preferredY,
                    g.Bottom+mandatoryHeight/2,
                    g.Top-mandatoryHeight/2)-preferredY)
            })
            .OrderByDescending(g=>g.FitsAll)
            .ThenBy(g=>g.Distance)
            .ThenByDescending(g=>g.Top-g.Bottom)
            .ThenByDescending(g=>g.Top).FirstOrDefault();
        if(best==null)return false;
        band=new Band(best.Bottom,best.Top,
            best.FitsAll,best.Top-best.Bottom);
        return true;
    }

    // Pre-register gate: candidate native viewports/titles must each
    // provide sufficient height for all mandatory source callouts.
    // Use exactly the same gutter and free-interval rules as the real
    // annotation planner. This is a geometric necessary check;
    // actual projected anchors and native glyph widths are rechecked
    // after Plant creates the views. No false PDF success on failure.
    internal static bool TryPreflight(
        IReadOnlyList<NativeOrthoTenViewPlan.Placement> placements,
        IReadOnlyList<NativeOrthoViewTitlePlan.Title> titles,
        double paperW,double paperH,bool bypass,
        double mandatoryHeight,out string reason)
    {
        reason="";
        if(mandatoryHeight<=0)return true;
        var frames=placements.Select(v=>
            new NativeOrthoViewTitlePlan.Frame(
                v.Name,v.CenterX,v.CenterY,
                v.PaperFrameWidth,v.PaperFrameHeight)).ToArray();
        var reserved=new List<Rect>();
        foreach(var f in frames)
        {
            if(!TryGutter(f,frames,paperW,paperH,bypass,
                out var g))
            {
                reason=f.Name+":NO_SAFE_TEXT_GUTTER";
                return false;
            }
            if(!TryFindBand(frames,titles,reserved,
                g.X,g.Width,f.Y,mandatoryHeight,mandatoryHeight,
                paperW,paperH,bypass,out var band))
            {
                reason=f.Name+":INSUFFICIENT_MANDATORY_LABEL_AREA"+
                    " NEED_MM="+mandatoryHeight.ToString("0.###",
                        System.Globalization.CultureInfo.InvariantCulture);
                return false;
            }
            double top=Math.Min(band.Top,
                Math.Max(band.Bottom+mandatoryHeight,
                    f.Y+mandatoryHeight/2));
            reserved.Add(new Rect(g.X,top-mandatoryHeight,
                g.X+g.Width,top));
        }
        return true;
    }
}
