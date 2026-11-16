using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV64: one native paper sheet means one registered Ortho DWG / one PDF,
/// with ALL ten preset viewports at the requested exact 1:N scale.
/// Only inspect already-registered EMPTY native seeds, never scale a
/// viewport to fit secretly or change the seed's plotted media/title block.
/// This helper is pure; Autodesk project registration is verified upstream.
/// </summary>
internal static class NativeOrthoSingleSheet64
{
    internal sealed record Seed(string Path,double WidthMm,
        double HeightMm,bool RegisteredEmpty);

    internal static bool TryExactTenFit(NativeOrthoLayout.Plan layout,
        IReadOnlyList<string> selectedViews,int denominator,
        double widthMm,double heightMm,out string reason,
        bool bypassTitleBlock=false)
    {
        reason="";
        if(layout==null||denominator<1||
           selectedViews==null||selectedViews.Count!=10||
           selectedViews.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=10)
        {
            reason="REV64 requires ten distinct selected native views and a positive exact 1:N.";
            return false;
        }
        try
        {
            var plan=NativeOrthoTenViewPlan.Build(layout,widthMm,heightMm)
                with {Denominator=denominator,Scale=1.0/denominator};
            // Force the selection-aware EXACT-scale layout, even when
            // an oversized sheet has a less conservative auto fit.
            var placement=NativeOrthoExactScalePlacement61.Build(
                plan,selectedViews,widthMm,heightMm,
                bypassTitleBlock:bypassTitleBlock);
            NativeOrthoEffectivePaper59.RequireInsideUsableArea(
                NativeOrthoEffectivePaper59.FromMedia(widthMm,heightMm,0),
                placement.Views.Select(v=>
                    new NativeOrthoEffectivePaper59.ViewFrame(
                        v.Name,v.CenterX,v.CenterY,
                        v.PaperFrameWidth,v.PaperFrameHeight)).ToArray(),
                placement.Titles.Select(t=>
                    new NativeOrthoEffectivePaper59.CaptionFrame(
                        t.Name,t.CenterX,t.TopY,t.Width,t.Height)).ToArray(),
                requireTen:true,
                bypassTitleBlock:bypassTitleBlock);
            if(placement.Views.Count!=10||placement.Titles.Count!=10)
                throw new InvalidOperationException(
                    "Native Ortho views/titles not complete.");
            return true;
        }
        catch(System.Exception e) when(e is InvalidOperationException||
            e is ArgumentException||e is OverflowException)
        {
            reason=e.Message;
            return false;
        }
    }

    internal static Seed? SmallestFitting(
        NativeOrthoLayout.Plan layout,IReadOnlyList<string> views,
        int denominator,IEnumerable<Seed> candidates,
        bool bypassTitleBlock=false)
    {
        if(candidates==null)throw new ArgumentNullException(nameof(candidates));
        return candidates.Where(x=>x.RegisteredEmpty&&
                !string.IsNullOrWhiteSpace(x.Path)&&
                TryExactTenFit(layout,views,denominator,
                    x.WidthMm,x.HeightMm,out _,
                    bypassTitleBlock:bypassTitleBlock))
            // Prefer the smallest suitable paper and make tie-breaking
            // deterministic across project sessions.
            .OrderBy(x=>x.WidthMm*x.HeightMm)
            .ThenBy(x=>x.Path,StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }
}
