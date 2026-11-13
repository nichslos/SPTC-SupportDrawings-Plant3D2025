using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.64: conservative coordination between native paper-space
/// dimension layouts and the already verified annotation stacks.
/// The default REV22.63 drawing is ALWAYS evaluated first. Reposition
/// a whole view's external label stack / leader dogleg lanes only when
/// that strictly adds a locally measured native dimension WITHOUT moving,
/// replacing or removing any previously accepted dimension.
///
/// Model geometry, reference view titles, camera, pipe tags, source
/// member identities and dimension measurements are never modified.
/// The solver is deliberately bounded: it runs only for orthographic
/// views with explicitly diagnosed missing optional member dimensions.
/// </summary>
public static class SupportOrthoLeaderDimensionLayout64
{
    public sealed record Result(
        IReadOnlyList<SupportOrthoMemberCalloutPlan27.Callout> Callouts,
        int Attempts,int ShiftedViews,int OriginalLocalOmissions,
        int RemainingLocalOmissions,int AddedDimensions,
        IReadOnlyList<string> Diagnostics);

    private static readonly HashSet<string> Ortho=new(
        new[]{"TOP","BOTTOM","LEFT","RIGHT","FRONT","BACK"},
        StringComparer.OrdinalIgnoreCase);

    // Two independently controlled paper-space variables, never model
    // coordinates: vertical label row shift and right-side dogleg lane.
    // The first six alternatives have the least visual displacement.
    private static readonly (double Y,double Lane)[] Trials={
        (0,4),(0,7),(8,0),(-8,0),(8,4),(-8,4),
        (16,4),(-16,4),(24,4),(-24,4),(24,7),(-24,7),
        (32,4),(-32,4)
    };

    public static Result Coordinate(
        IReadOnlyList<SupportOrthoMemberCalloutPlan17.Camera> cameras,
        IReadOnlyList<NativeOrthoViewTitlePlan.Title> titles,
        IReadOnlyList<SupportOrthoMemberCalloutPlan27.Callout> originals,
        double paperWidth,double paperHeight,
        Func<IReadOnlyList<SupportOrthoMemberCalloutPlan27.Callout>,
             SupportOrthoReferenceDimPlan47.Result> replan,
        int maxTrialsPerView=12)
    {
        if(cameras==null||titles==null||originals==null||
           replan==null||!double.IsFinite(paperWidth)||
           !double.IsFinite(paperHeight)||
           paperWidth<=0||paperHeight<=0||
           maxTrialsPerView<1||maxTrialsPerView>Trials.Length)
            throw new ArgumentException(
                "REV22.64 invalid coordinated paper-space layout inputs.");

        var untouched=originals.ToArray();
        var current=untouched;
        var originalPlan=replan(current);
        var activePlan=originalPlan;
        int originalMissing=LocalMissing(originalPlan);
        if(originalMissing==0||originalPlan.PriorityOverallDimensions==0)
            return new Result(current,0,0,originalMissing,
                originalMissing,0,Array.Empty<string>());

        int attempts=0,shiftedViews=0;
        var messages=new List<string>();
        foreach(var view in cameras.Select(c=>c.Name)
                    .Where(n=>Ortho.Contains(n)).Distinct(
                        StringComparer.OrdinalIgnoreCase))
        {
            if(!MissingView(activePlan,view))continue;
            var viewNotes=current.Where(n=>
                n.View.Equals(view,StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if(viewNotes.Length==0)continue;

            var best=activePlan;
            IReadOnlyList<SupportOrthoMemberCalloutPlan27.Callout>? bestNotes=null;
            double bestMotion=double.PositiveInfinity;
            foreach(var trial in Trials.Take(maxTrialsPerView))
            {
                var candidate=current.Select(note=>
                    note.View.Equals(view,StringComparison.OrdinalIgnoreCase)?
                        note with {LabelTop=note.LabelTop+trial.Y,
                            LaneX=note.LaneX+trial.Lane}:note).ToArray();
                if(!SafePaperLayout(candidate,cameras,titles,
                    paperWidth,paperHeight))
                    continue;
                attempts++;
                var checkedPlan=replan(candidate);
                // Absolutely preserve the entire accepted baseline
                // geometry and dynamic measurements; no new detail may
                // steal a successful 828.28/928.28 overall or chain.
                if(checkedPlan.UnintendedAcceptedWitnessIntersections!=0||
                   !Preserves(originalPlan,checkedPlan)||
                   checkedPlan.PriorityOverallDimensions!=
                       originalPlan.PriorityOverallDimensions)
                    continue;
                if(checkedPlan.Dimensions.Count<=best.Dimensions.Count||
                   LocalMissing(checkedPlan)>=LocalMissing(best))
                    continue;
                double motion=Math.Abs(trial.Y)+trial.Lane*.6;
                if(checkedPlan.Dimensions.Count>best.Dimensions.Count||
                   motion<bestMotion)
                {
                    best=checkedPlan;
                    bestNotes=candidate;
                    bestMotion=motion;
                }
            }
            if(bestNotes==null)continue;
            current=bestNotes.ToArray();
            activePlan=best;
            shiftedViews++;
            messages.Add(view+":DIMENSIONS_RECOVERED="+
                (activePlan.Dimensions.Count-originalPlan.Dimensions.Count)+
                ";LOCAL_OMISSIONS="+LocalMissing(activePlan));
        }
        return new Result(current,attempts,shiftedViews,
            originalMissing,LocalMissing(activePlan),
            Math.Max(0,activePlan.Dimensions.Count-
                originalPlan.Dimensions.Count),messages);
    }

    private static bool MissingView(SupportOrthoReferenceDimPlan47.Result plan,
        string view) =>
        plan.Skipped.Any(s=>s.StartsWith(view+"/",
            StringComparison.OrdinalIgnoreCase)&&IsLocalOmission(s));

    private static int LocalMissing(SupportOrthoReferenceDimPlan47.Result p) =>
        p.Skipped.Count(IsLocalOmission);

    private static bool IsLocalOmission(string text) =>
        text.Contains("_LOCAL_MEMBER_WIDTH_NO_CLEAR_LANE",
            StringComparison.Ordinal)||
        text.Contains("REV22_60_OPTIONAL_PROFILE_NO_COMPATIBLE_LAYOUT",
            StringComparison.Ordinal);

    private static bool Preserves(
        SupportOrthoReferenceDimPlan47.Result original,
        SupportOrthoReferenceDimPlan47.Result candidate)
    {
        if(candidate.Dimensions.Count<original.Dimensions.Count||
           candidate.OverallDimensions<original.OverallDimensions||
           candidate.ChainDimensions<original.ChainDimensions)
            return false;
        foreach(var previous in original.Dimensions)
        {
            var after=candidate.Dimensions.Where(d=>d.View==previous.View&&
                d.Source==previous.Source&&
                d.Horizontal==previous.Horizontal).ToArray();
            if(after.Length!=1)return false;
            var d=after[0];
            if(!Close(d.Ax,previous.Ax)||!Close(d.Ay,previous.Ay)||
               !Close(d.Bx,previous.Bx)||!Close(d.By,previous.By)||
               !Close(d.LineX,previous.LineX)||
               !Close(d.LineY,previous.LineY)||
               !Close(d.PaperTextX,previous.PaperTextX)||
               !Close(d.PaperTextY,previous.PaperTextY)||
               d.Measurement!=previous.Measurement||
               d.Provenance!=previous.Provenance||
               !Close(d.PaperToModelMm,previous.PaperToModelMm))
                return false;
        }
        return true;
    }

    private static bool Close(double? a,double? b) =>
        !a.HasValue&&!b.HasValue ||
        a.HasValue&&b.HasValue&&Close(a.Value,b.Value);
    private static bool Close(double a,double b) =>
        double.IsFinite(a)&&double.IsFinite(b)&&
        Math.Abs(a-b)<1e-6;

    private static bool SafePaperLayout(
        IReadOnlyList<SupportOrthoMemberCalloutPlan27.Callout> notes,
        IReadOnlyList<SupportOrthoMemberCalloutPlan17.Camera> cameras,
        IReadOnlyList<NativeOrthoViewTitlePlan.Title> titles,
        double paperW,double paperH)
    {
        var boxes=new List<SupportOrthoSmartDimPlan46.Rect>();
        foreach(var note in notes)
        {
            if(!new[]{note.AnchorX,note.AnchorY,note.LaneX,
                note.LabelX,note.LabelTop,note.LabelWidth,note.LabelHeight}
                    .All(double.IsFinite)||
               note.LabelWidth<=0||note.LabelHeight<=0||
               note.LaneX<=note.AnchorX||
               note.LaneX>=note.LabelX-1.5)
                return false;
            var text=new SupportOrthoSmartDimPlan46.Rect(
                note.LabelX-1,note.LabelTop-note.LabelHeight-1,
                note.LabelX+note.LabelWidth+1,note.LabelTop+1);
            if(text.L<2||text.R>paperW*.78-1||
               text.B<paperH*.12+1||text.T>paperH*.94-1)
                return false;
            if(boxes.Any(b=>b.Intersects(text,.20)))return false;
            boxes.Add(text);
            foreach(var view in cameras)
            {
                var frame=new SupportOrthoSmartDimPlan46.Rect(
                    view.X-view.W*.5,view.Y-view.H*.5,
                    view.X+view.W*.5,view.Y+view.H*.5);
                if(text.Intersects(frame,.20))return false;
                if(!view.Name.Equals(note.View,
                    StringComparison.OrdinalIgnoreCase))
                {
                    // Do not route a dogleg across an unrelated
                    // viewport while moving a label stack vertically.
                    var dogleg=new SupportOrthoSmartDimPlan46.Rect(
                        note.LaneX-.45,
                        Math.Min(note.AnchorY,note.LabelTop-
                            SupportOrthoMemberCalloutPlan27.TextLineHeightMm*.5)-.45,
                        note.LaneX+.45,
                        Math.Max(note.AnchorY,note.LabelTop-
                            SupportOrthoMemberCalloutPlan27.TextLineHeightMm*.5)+.45);
                    if(dogleg.Intersects(frame,.2))return false;
                }
            }
            foreach(var title in titles)
            {
                var titleArea=new SupportOrthoSmartDimPlan46.Rect(
                    title.CenterX-title.Width*.5-1,
                    title.TopY-title.Height-1,
                    title.CenterX+title.Width*.5+1,
                    title.TopY+1);
                if(text.Intersects(titleArea,.20))
                    return false;
                var landing=note.LabelTop-
                    SupportOrthoMemberCalloutPlan27.TextLineHeightMm*.5;
                var dogleg=new SupportOrthoSmartDimPlan46.Rect(
                    note.LaneX-.35,Math.Min(note.AnchorY,landing)-.35,
                    note.LaneX+.35,Math.Max(note.AnchorY,landing)+.35);
                if(dogleg.Intersects(titleArea,.20))
                    return false;
            }
        }
        return true;
    }
}
