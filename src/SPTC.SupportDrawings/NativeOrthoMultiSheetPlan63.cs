using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV63: fail-closed pure preflight of exact-scale *multiple* native
/// Ortho paper drawings. The previous one-sheet validator remains
/// authoritative for every candidate. No fit calculation is relaxed,
/// no source/project writes, no scale conversion, no cropped view.
/// </summary>
internal static class NativeOrthoMultiSheetPlan63
{
    internal sealed record Sheet(int Number,int Total,string[] Views)
    {
        internal string Suffix=>"S"+Number.ToString("D2")+
            "_OF_"+Total.ToString("D2");
        internal string DrawingNumberSuffix=>"-S"+Number.ToString("D2");
    }

    internal static IReadOnlyList<Sheet> Build(
        NativeOrthoTenViewPlan.Plan exactPlan,
        IReadOnlyList<string> selectedViews,double paperWidth,double paperHeight)
    {
        if(exactPlan==null||selectedViews==null||
           selectedViews.Count<2||selectedViews.Count>10||
           selectedViews.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=
               selectedViews.Count)
            throw new ArgumentException("REV63 invalid multiple-sheet selection.");

        // For an all-ten request, the first split preserves the familiar
        // orthographic drawing and its four named isometric views.
        // For other selected subsets, preserve the user's source view order.
        var six=selectedViews.Where(v=>
            new[]{"TOP","BOTTOM","LEFT","RIGHT","FRONT","BACK"}
                .Contains(v,StringComparer.OrdinalIgnoreCase)).ToArray();
        var iso=selectedViews.Where(v=>
            new[]{"NW","NE","SW","SE"}
                .Contains(v,StringComparer.OrdinalIgnoreCase)).ToArray();
        if(six.Length+iso.Length!=selectedViews.Count)
            throw new ArgumentException("REV63 unknown preset view.");
        var groups=new List<string[]>();
        if(six.Length>0)AppendFitting(exactPlan,six,paperWidth,paperHeight,groups);
        if(iso.Length>0)AppendFitting(exactPlan,iso,paperWidth,paperHeight,groups);
        if(groups.Count<2)
            throw new InvalidOperationException(
                "REV63 sheet-set planner did not need multiple paper sheets.");
        if(groups.SelectMany(x=>x).Distinct(
                StringComparer.OrdinalIgnoreCase).Count()!=selectedViews.Count||
           groups.Sum(x=>x.Length)!=selectedViews.Count)
            throw new InvalidOperationException(
                "REV63 planned sheet set does not cover every selected view once.");
        return groups.Select((views,i)=>new Sheet(i+1,groups.Count,views))
            .ToArray();
    }

    private static void AppendFitting(NativeOrthoTenViewPlan.Plan exactPlan,
        string[] views,double w,double h,List<string[]> pages)
    {
        try
        {
            // This checks title placement, safe paper borders,
            // projected frame geometry and label gutter reserve.
            NativeOrthoExactScalePlacement61.Build(exactPlan,views,w,h);
            pages.Add(views);
        }
        catch(NativeOrthoExactScalePlacement61.DoesNotFitException)
        {
            if(views.Length==1)
                throw new InvalidOperationException(
                    "Even a SINGLE "+views[0]+" native view at 1:"+
                    exactPlan.Denominator+" cannot fit the registered "+
                    "paper sheet with title-block/annotation clearances. "+
                    "Create and register a larger EMPTY Ortho seed. "+
                    "No DWG or PDF was created.");
            int half=(views.Length+1)/2;
            AppendFitting(exactPlan,views.Take(half).ToArray(),w,h,pages);
            AppendFitting(exactPlan,views.Skip(half).ToArray(),w,h,pages);
        }
    }
}
