using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.47: reference-style pipe-support DETAIL dimensions, following the
/// supplied 25-page drafting examples (particularly sheets 15-16).
/// Draw a readable chain of verified projected member-edge/endpoint stations
/// near the support and a separate OUTER overall dimension. Do NOT dimension
/// each individual member's WCS bounding rectangle in every direction.
/// Existing geometry is read-only; measurements are projected millimetres,
/// never fabrication cut lengths, bolt pitches or invented hole centres.
/// </summary>
public static class SupportOrthoReferenceDimPlan47
{
    public sealed record Result(
        IReadOnlyList<SupportOrthoDimLinearPlan41.Dimension> Dimensions,
        IReadOnlyList<string> Skipped,
        int OrthographicViews,int ChainDimensions,int OverallDimensions,
        int CollisionCandidatesRejected,int RequestedDimensions,
        int LocalMemberDimensions=0,
        int PriorityOverallDimensions=0,
        int NativeMidpointMemberDimensions=0,
        int BoundsMidpointMemberDimensions=0,
        int TerminalMemberRemainderDimensions=0,
        int RedundantChainsSuppressed=0,
        int HierarchyCrossingsRejected=0,
        int ManualViewDetailsSuppressed=0);

    private sealed record Station(double At,int Weight);
    private sealed record Request(bool Horizontal,double From,double To,
        bool Overall,int Index,string Measurement,
        double PaperToModelMm,
        SupportOrthoSmartDimPlan46.Rect? LocalBounds=null,
        string SourceKey="",
        double? MidpointWitness=null,
        bool TerminalRemainder=false);
    private sealed record Placed(bool Horizontal,
        SupportOrthoSmartDimPlan46.Rect Line,
        SupportOrthoSmartDimPlan46.Rect Text);
    // REV22.54: identify the real source of a blocked lane rather than
    // conflating nearby view labels, leaders, page boundary and members.
    private sealed record Obstacle(
        SupportOrthoSmartDimPlan46.Rect Area,string Type);

    private static readonly HashSet<string> Ortho=new(
        new[]{"TOP","BOTTOM","FRONT","BACK","LEFT","RIGHT"},
        StringComparer.OrdinalIgnoreCase);

    public static Result Build(
        IReadOnlyList<SupportOrthoMemberCalloutPlan17.Camera> cameras,
        IReadOnlyList<SupportOrthoAllViewDimPlan44.Member> members,
        double paperW,double paperH,double unitsToMm,
        IReadOnlyList<SupportOrthoDimLinearPlan41.Segment>? sources=null,
        IReadOnlyList<SupportOrthoMemberCalloutPlan27.Callout>? callouts=null,
        IReadOnlyList<NativeOrthoViewTitlePlan.Title>? titles=null,
        IReadOnlyList<SupportOrthoSmartDimPlan46.Rect>? additionalKeepouts=null)
    {
        if(cameras==null||members==null||!double.IsFinite(paperW)||
           !double.IsFinite(paperH)||!double.IsFinite(unitsToMm)||
           paperW<=0||paperH<=0||unitsToMm<=0)
            throw new ArgumentException("REV22.47 invalid drafting inputs.");
        var dimensions=new List<SupportOrthoDimLinearPlan41.Dimension>();
        var skipped=new List<string>();
        int views=0,chain=0,overall=0,localCount=0,
            priorityOverallCount=0,midpointNative=0,
            midpointBounds=0,terminalRemainders=0,
            redundantChains=0,hierarchyRejected=0,
            manualViewSuppressed=0,rejected=0,requested=0;
        var paper=new SupportOrthoSmartDimPlan46.Rect(
            2,paperH*.12+2,paperW*.78-2,paperH*.94-2);

        foreach(var camera in cameras)
        {
            if(!Ortho.Contains(camera.Name))
            {
                skipped.Add(camera.Name+":ISO_REFERENCE_DIMENSIONS_NOT_VALID");
                continue;
            }
            views++;
            if(!double.IsFinite(camera.Scale)||camera.Scale<=0||
               camera.W<=0||camera.H<=0)
            {
                skipped.Add(camera.Name+":INVALID_CAMERA");
                continue;
            }
            var frame=new SupportOrthoSmartDimPlan46.Rect(
                camera.X-camera.W*.5,camera.Y-camera.H*.5,
                camera.X+camera.W*.5,camera.Y+camera.H*.5);
            var safe=new SupportOrthoSmartDimPlan46.Rect(
                frame.L+1.4,frame.B+1.4,
                frame.R-1.4,frame.T-1.4);
            var projected=new List<SupportOrthoSmartDimPlan46.Rect>();
            var projectedMembers=new List<(
                SupportOrthoAllViewDimPlan44.Member Member,
                SupportOrthoSmartDimPlan46.Rect Bounds)>();
            // REV22.48: only HOST-DWG native Plant members contribute to
            // overall lengths or connected-edge dimension chain stations.
            var visibleNative=new Dictionary<string,
                SupportOrthoSmartDimPlan46.Rect>(
                StringComparer.OrdinalIgnoreCase);
            foreach(var member in members)
            {
                if(!SupportOrthoNativeDimensionPolicy48.AcceptMember(member))
                {
                    skipped.Add(camera.Name+"/"+member.Key+
                        ":XREF_OR_NONNATIVE_DIMENSION_SOURCE_EXCLUDED");
                    continue;
                }
                if(!ValidMember(member))
                {
                    skipped.Add(camera.Name+"/"+member.Key+
                        ":UNVERIFIED_MEMBER_EXTENTS");
                    continue;
                }
                var bounds=Project(camera,member);
                if(bounds.L<safe.L-.05||bounds.R>safe.R+.05||
                   bounds.B<safe.B-.05||bounds.T>safe.T+.05)
                {
                    skipped.Add(camera.Name+"/"+member.Key+
                        ":OUTSIDE_NATIVE_VIEW");
                    continue;
                }
                projected.Add(bounds);
                projectedMembers.Add((member,bounds));
                visibleNative[member.Key]=bounds;
            }
            if(projected.Count==0)
            {
                skipped.Add(camera.Name+":NO_VERIFIED_VISIBLE_MEMBER");
                continue;
            }
            double minX=projected.Min(b=>b.L),maxX=projected.Max(b=>b.R);
            double minY=projected.Min(b=>b.B),maxY=projected.Max(b=>b.T);
            var assembly=new SupportOrthoSmartDimPlan46.Rect(
                minX,minY,maxX,maxY);
            var blocks=FixedZones(camera,cameras,callouts,titles);
            if(additionalKeepouts!=null)
                blocks.AddRange(additionalKeepouts.Where(x=>x.IsFinite)
                    .Select(x=>new Obstacle(x,"RESERVED_PAPER_AREA")));
            var placed=new List<Placed>();

            // REV22.53: ONE projection pass for actual, vetted native
            // Plant Start/End points. Cache these paper coordinates for
            // both axis chains and overall witness selection. The
            // projected AABB alone never authorizes connection stations.
            var verifiedWitnessPoints=new List<(double X,double Y)>();
            // REV22.55: a selected Plant member's Start/End line
            // identifies its TRUE axis midpoint. Keep that midpoint
            // separately by native handle: global bounding boxes alone
            // cannot locate a sloping member's centerline.
            var nativeAxisMidpoints=new Dictionary<string,(
                double X,double Y,double Dx,double Dy)>(
                StringComparer.OrdinalIgnoreCase);
            if(sources!=null)
                foreach(var source in sources)
                {
                    if(!SupportOrthoNativeDimensionPolicy48.AcceptSegment(source)||
                       !visibleNative.TryGetValue(source.Key,
                            out var sourceBounds))
                        continue;
                    foreach(var point in new[]{
                        (source.Ax,source.Ay,source.Az),
                        (source.Bx,source.By,source.Bz)})
                    {
                        var p=SupportOrthoMemberCalloutPlan17.Project(
                            camera,point.Item1,point.Item2,point.Item3);
                        if(!double.IsFinite(p.X)||!double.IsFinite(p.Y)||
                           p.X<sourceBounds.L-.25||
                           p.X>sourceBounds.R+.25||
                           p.Y<sourceBounds.B-.25||
                           p.Y>sourceBounds.T+.25)
                            continue;
                        verifiedWitnessPoints.Add((p.X,p.Y));
                    }
                    if(!source.Provenance.Equals(
                        "EXPLICIT_MEMBER_ENDPOINTS",
                        StringComparison.OrdinalIgnoreCase))
                        continue;
                    var a=SupportOrthoMemberCalloutPlan17.Project(camera,
                        source.Ax,source.Ay,source.Az);
                    var b=SupportOrthoMemberCalloutPlan17.Project(camera,
                        source.Bx,source.By,source.Bz);
                    var middle=SupportOrthoMemberCalloutPlan17.Project(
                        camera,(source.Ax+source.Bx)*.5,
                        (source.Ay+source.By)*.5,
                        (source.Az+source.Bz)*.5);
                    if(!new[]{middle.X,middle.Y,a.X,a.Y,b.X,b.Y}
                            .All(double.IsFinite)||
                       middle.X<sourceBounds.L-.20||
                       middle.X>sourceBounds.R+.20||
                       middle.Y<sourceBounds.B-.20||
                       middle.Y>sourceBounds.T+.20)
                        continue;
                    nativeAxisMidpoints[source.Key]=(
                        middle.X,middle.Y,b.X-a.X,b.Y-a.Y);
                }

            // Reserve the two OUTER overall dimensions before chain
            // details and member widths. REV22.52 placed H local widths
            // before RIGHT/V overall, letting optional local details
            // consume the only left-side overall lane. RIGHT/V is now
            // protected first. Both axes still run through the same
            // geometry and collision checks: no fabricated dimensions.
            var reservedOverallAxes=new HashSet<bool>();
            var outerOverallLines=new Dictionary<bool,double>();
            // Use the same absolute paper coordinates for every
            // dimension: differences in native member bounding boxes
            // must never reverse inner detail and outer overall lanes.
            bool InnerThanReservedOverall(Placed candidate)
            {
                if(!outerOverallLines.TryGetValue(
                    candidate.Horizontal,out double outerLine))
                    return true;
                double at=candidate.Horizontal?
                    (candidate.Line.B+candidate.Line.T)*.5:
                    (candidate.Line.L+candidate.Line.R)*.5;
                return SupportOrthoManualDetailStyle57.InsideOverall(
                    candidate.Horizontal,at,outerLine);
            }
            foreach(bool horizontal in new[]{false,true})
            {
                double from=horizontal?minX:minY;
                double to=horizontal?maxX:maxY;
                if(to-from<.25)continue;
                var request=MakeRequest(horizontal,from,to,true,0,
                    camera.Scale,unitsToMm);
                bool found=false;
                var rejectionCounts=new SortedDictionary<string,int>(
                    StringComparer.Ordinal);
                void CountRejected(string cause)
                {
                    rejected++;
                    rejectionCounts[cause]=
                        rejectionCounts.TryGetValue(cause,out int count)?
                            count+1:1;
                }
                foreach(double sign in PreferredSigns(camera.Name,horizontal))
                {
                    if(found)break;
                    // A second 4.2 mm empty lane is NOT required when
                    // no chain occupies it. On RIGHT the LEFT VIEW
                    // annotation gutter ends just before this band,
                    // so insisting on 11.8 mm pushed the vertical
                    // dimension text into the LEFT annotation labels.
                    foreach(double offset in SupportOrthoReferenceLayout51
                        .OverallLaneOffsets())
                    {
                        if(found)break;
                        for(int tier=0;tier<20;tier++)
                        {
                            var candidate=Candidate(camera,assembly,
                                projected,verifiedWitnessPoints,
                                request,sign,offset,tier);
                            string? cause=candidate==null?
                                "NATIVE_WITNESS_UNRESOLVED":
                                RejectionReason(candidate.Value.P,
                                    blocks,paper,placed,projected);
                            if(cause!=null)
                            {
                                CountRejected(cause);
                                continue;
                            }
                            dimensions.Add(candidate!.Value.D);
                            placed.Add(candidate.Value.P);
                            reservedOverallAxes.Add(horizontal);
                            outerOverallLines[horizontal]=horizontal?
                                (candidate.Value.P.Line.B+
                                 candidate.Value.P.Line.T)*.5:
                                (candidate.Value.P.Line.L+
                                 candidate.Value.P.Line.R)*.5;
                            priorityOverallCount++;
                            overall++;
                            found=true;
                            break;
                        }
                    }
                }
                // Record a bounded diagnostic (per cause, not a flood
                // of hundreds of candidate lines) for real Plant DWGs.
                // This survives in the output TXT via existing
                // REV22_47_DIMENSION_SKIP= log forwarding.
                if(camera.Name.Equals("RIGHT",
                        StringComparison.OrdinalIgnoreCase)&&!horizontal)
                {
                    string summary=string.Join(",",
                        rejectionCounts.Select(x=>x.Key+":"+x.Value));
                    skipped.Add("RIGHT/V:REV22_54_OVERALL_DIAGNOSTICS="+
                        (found?"PLACED":"UNPLACED")+
                        ";CAUSES="+(summary.Length==0?"NONE":summary));
                }
            }

            foreach(bool horizontal in new[]{true,false})
            {
                double min=horizontal?minX:minY;
                double max=horizontal?maxX:maxY;
                if(max-min<.25)
                {
                    skipped.Add(camera.Name+(horizontal?"/H":"/V")+
                        ":OVERALL_SPAN_TOO_SMALL");
                    continue;
                }
                // Verified endpoints only; the bounding-box side
                // is never sufficient evidence for an internal chain.
                var stationCandidates=verifiedWitnessPoints
                    .Select(p=>horizontal?p.X:p.Y)
                    .Where(at=>at>min+.35&&at<max-.35)
                    .Select(at=>new Station(at,4)).ToList();
                var stations=SelectStations(stationCandidates,min,max);
                // REV22.56: match the user-adjusted six-view reference.
                // An L-shaped native support may have a member that
                // spans the complete projected axis plus one short
                // terminal member profile. Use the actual end face of
                // that short member as the intermediate datum, so the
                // main chain is OVERALL minus the measured terminal
                // profile width. Do NOT retain a redundant 50 mm
                // centerline gap if the physical end section is 100 mm.
                // Two abutting *independent* members do not qualify;
                // they retain the strict explicit Start/End policy.
                var projectedNativeSpans=projectedMembers.Select(m=>
                    new SupportOrthoReferenceDatum56.Span(
                        m.Member.Key,
                        horizontal?m.Bounds.L:m.Bounds.B,
                        horizontal?m.Bounds.R:m.Bounds.T)).ToArray();
                var terminalDatum=SupportOrthoReferenceDatum56.Choose(
                    projectedNativeSpans,min,max);
                // A conventional non-terminal chain segment is shown
                // only when BOTH station points have native endpoints.
                bool VerifiedAt(double at) =>
                    verifiedWitnessPoints.Any(p=>
                        Math.Abs((horizontal?p.X:p.Y)-at)<=.20);
                var steps=new List<Request>();
                if(terminalDatum.HasValue)
                {
                    if(SupportOrthoManualDetailStyle57.ShowTerminalRemainder(
                            camera.Name,horizontal))
                    {
                        var datum=terminalDatum.Value;
                        steps.Add(MakeRequest(horizontal,
                            datum.RemainingFrom,datum.RemainingTo,
                            false,0,camera.Scale,unitsToMm) with {
                                SourceKey=datum.SourceKey,
                                TerminalRemainder=true
                            });
                    }
                    else
                    {
                        // Manual RIGHT view: display overall and the
                        // measured end section; omit optional V detail.
                        manualViewSuppressed++;
                    }
                }
                else if(stations.Count>2)
                {
                    for(int i=0;i<stations.Count-1;i++)
                    {
                        double start=stations[i],end=stations[i+1];
                        if(end-start<.50)continue;
                        if(SupportOrthoManualDetailStyle57
                            .ObscuredByTerminalMember(start,end,min,max,
                                projectedNativeSpans))
                        {
                            redundantChains++;
                            continue;
                        }
                        if(!VerifiedAt(start)||!VerifiedAt(end))
                        {
                            skipped.Add(camera.Name+"/"+
                                (horizontal?"H":"V")+
                                ":LOCAL_CHAIN_UNVERIFIED_MEMBER_ENDPOINTS");
                            continue;
                        }
                        steps.Add(MakeRequest(horizontal,start,end,false,
                            i,camera.Scale,unitsToMm));
                    }
                }
                var big=MakeRequest(horizontal,min,max,true,0,
                    camera.Scale,unitsToMm);
                // Keep all consecutive chain measurements in ONE lane where
                // possible, and place overall beyond it. This follows the
                // 712+110 below 822 and 2225 multi-station examples.
                double chainOffset=0;
                int chainPlaced=0;
                if(steps.Count>0)
                {
                    bool chainGroupPlaced=false;
                    foreach(double sign in PreferredSigns(camera.Name,horizontal))
                    {
                    if(chainGroupPlaced)break;
                    foreach(double offset in SupportOrthoReferenceLayout51.LaneOffsets())
                    {
                        var local=new List<(
                            SupportOrthoDimLinearPlan41.Dimension D,
                            Placed P)>();
                        bool clear=true;
                        foreach(var step in steps)
                        {
                            // Keep one straight chain line, but search
                            // independent staggered rows for dimension
                            // text of each short/adjacent station interval.
                            // A 200 mm text can otherwise overlap the
                            // following 300 mm text even on a clear lane.
                            (SupportOrthoDimLinearPlan41.Dimension D,
                                Placed P)? candidate=null;
                            for(int tier=0;tier<8;tier++)
                            {
                                var attempt=Candidate(camera,assembly,
                                    projected,verifiedWitnessPoints,
                                    step,sign,offset,tier);
                                if(attempt!=null&&
                                   !InnerThanReservedOverall(attempt.Value.P))
                                {
                                    hierarchyRejected++;
                                    continue;
                                }
                                if(attempt!=null&&
                                   Fits(attempt.Value.P,blocks,paper,
                                       placed.Concat(local.Select(x=>x.P)),
                                       projected))
                                {
                                    candidate=attempt;
                                    break;
                                }
                                rejected++;
                            }
                            if(candidate==null)
                            {
                                clear=false;
                                break;
                            }
                            local.Add(candidate.Value);
                        }
                        if(!clear)continue;
                        foreach(var item in local)
                        {
                            dimensions.Add(item.D);
                            placed.Add(item.P);
                            if(item.D.Source.Contains(
                                "/TERMINAL_REMAINDER_",
                                StringComparison.Ordinal))
                                terminalRemainders++;
                            else chain++;
                            chainPlaced++;
                        }
                        chainOffset=offset;
                        chainGroupPlaced=true;
                        break;
                    }
                    }
                }
                if(steps.Count>0&&chainPlaced!=steps.Count)
                {
                    skipped.Add(camera.Name+"/"+(horizontal?"H":"V")+
                        (terminalDatum.HasValue?
                            ":TERMINAL_REMAINDER_NO_CLEAR_LANE":
                            ":CHAIN_NOT_FITTED_TO_ONE_LANE"));
                    // Suppress the unsafe partial chain, but preserve
                    // a correctly measured overall dimension.
                }
                requested+=steps.Count;
                requested++;
                bool overallPlaced=reservedOverallAxes.Contains(horizontal);
                if(!overallPlaced)
                foreach(double sign in PreferredSigns(camera.Name,horizontal))
                {
                if(overallPlaced)break;
                foreach(double offset in SupportOrthoReferenceLayout51.LaneOffsets())
                {
                    double extra=steps.Count>1&&chainPlaced==steps.Count
                        ?Math.Max(0,chainOffset+
                            SupportOrthoReferenceLayout51.OverallSeparation()
                            -offset):0;
                    // REV22.52: a previously occupied text row is NOT a
                    // reason to abandon the vertical overall dimension.
                    // Search additional text tiers independently of the
                    // dimension line lane. This fixes RIGHT/V overall
                    // omissions on tight portrait support details.
                    for(int textTier=0;textTier<10;textTier++)
                    {
                        var candidate=Candidate(camera,assembly,
                            projected,verifiedWitnessPoints,
                            big,sign,offset+extra,textTier);
                        if(candidate==null||
                           !Fits(candidate.Value.P,blocks,paper,placed,
                                projected))
                        {
                            rejected++;
                            continue;
                        }
                        dimensions.Add(candidate.Value.D);
                        placed.Add(candidate.Value.P);
                        overall++;
                        overallPlaced=true;
                        break;
                    }
                    if(overallPlaced)break;
                }
                }
                if(!overallPlaced)
                    skipped.Add(camera.Name+"/"+(horizontal?"H":"V")+
                        ":OVERALL_NO_CLEAR_DIMENSION_LANE");

                // REV22.52: selected native structure profiles may have a
                // measured cross-section width (e.g. 100 mm) that is not
                // a chain interval between explicit Plant endpoints.
                // Permit a distinctly verified projected MEMBER extent,
                // never an assumed connection station, hole pitch or
                // fabricated 50/100 value. Limit to two different local
                // widths per view direction to avoid duplicate clutter.
                var localCandidates=projectedMembers
                    .Select(m=>new {
                        m.Member,
                        m.Bounds,
                        From=horizontal?m.Bounds.L:m.Bounds.B,
                        To=horizontal?m.Bounds.R:m.Bounds.T
                    })
                    .Where(m=>m.To-m.From>=.25&&
                        m.To-m.From<(max-min)*.45&&
                        m.To-m.From<=Math.Max(16.0,(max-min)*.28))
                    .OrderBy(m=>m.To-m.From)
                    .ThenBy(m=>m.Member.Key,StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var measuredLocalSpans=new List<double>();
                foreach(var member in localCandidates)
                {
                    double length=member.To-member.From;
                    // Identical parallel projected member widths on the
                    // same view need only one measurement.
                    if(measuredLocalSpans.Any(x=>Math.Abs(x-length)<.12))
                        continue;
                    if(measuredLocalSpans.Count>=2)break;
                    // An existing verified chain interval at exactly the
                    // same stations already conveys this member width.
                    bool chainAlreadyShows=dimensions.Any(d=>
                        d.View==camera.Name&&d.Horizontal==horizontal&&
                        d.Source.Contains("/CHAIN_",StringComparison.Ordinal)&&
                        Math.Abs((horizontal?d.Ax:d.Ay)-member.From)<.12&&
                        Math.Abs((horizontal?d.Bx:d.By)-member.To)<.12);
                    if(chainAlreadyShows)continue;
                    if(!SupportOrthoManualDetailStyle57.PreferLocalMember(
                        member.Member.Key,member.From,member.To,
                        terminalDatum))
                    {
                        manualViewSuppressed++;
                        continue;
                    }
                    // Dimension the true member CROSS-SECTION at the
                    // projected midpoint of its own longitudinal axis.
                    // This is different from using a global assembly
                    // center or the outer member face as witness origin.
                    // If the native Start/End axis cannot be verified,
                    // the projected member midpoint is an explicitly
                    // diagnosed envelope approximation, never a
                    // fabricated Plant centerline or connection point.
                    bool verifiedMidpoint=nativeAxisMidpoints.TryGetValue(
                        member.Member.Key,out var axis);
                    // Projected member axis must be transverse to the
                    // measured width (the long direction of the member).
                    // If it runs along the measured direction instead,
                    // the axis midpoint does not prove that width.
                    if(verifiedMidpoint)
                    {
                        double longChange=horizontal?
                            Math.Abs(axis.Dy):Math.Abs(axis.Dx);
                        double widthChange=horizontal?
                            Math.Abs(axis.Dx):Math.Abs(axis.Dy);
                        verifiedMidpoint=longChange>.4&&
                            longChange>widthChange*2;
                    }
                    double witness=verifiedMidpoint?
                        (horizontal?axis.Y:axis.X):
                        horizontal?
                            (member.Bounds.B+member.Bounds.T)*.5:
                            (member.Bounds.L+member.Bounds.R)*.5;
                    var localReq=MakeRequest(horizontal,member.From,
                        member.To,false,0,camera.Scale,unitsToMm)
                        with {LocalBounds=member.Bounds,
                            SourceKey=member.Member.Key,
                            MidpointWitness=witness};
                    requested++;
                    bool fitted=false;
                    foreach(double sign in PreferredSigns(camera.Name,horizontal))
                    {
                        if(fitted)break;
                        foreach(double offset in
                            SupportOrthoReferenceLayout51.LaneOffsets())
                        {
                            if(fitted)break;
                            for(int tier=0;tier<8;tier++)
                            {
                                var candidate=Candidate(camera,assembly,
                                    projected,verifiedWitnessPoints,
                                    localReq,sign,offset,tier);
                                if(candidate!=null&&
                                   !InnerThanReservedOverall(candidate.Value.P))
                                {
                                    hierarchyRejected++;
                                    continue;
                                }
                                if(candidate==null||
                                   !Fits(candidate.Value.P,blocks,paper,
                                       placed,projected))
                                {
                                    rejected++;
                                    continue;
                                }
                                // Local dimension line may be adjacent to
                                // its own member, but never cut across an
                                // unrelated structure member.
                                if(projected.Any(b=>b!=member.Bounds&&
                                    candidate.Value.P.Line.Intersects(b,.20))||
                                   placed.Any(prior=>
                                        prior.Horizontal==horizontal &&
                                        candidate.Value.P.Line.Intersects(
                                            prior.Line,.35)))
                                {
                                    rejected++;
                                    continue;
                                }
                                dimensions.Add(candidate.Value.D);
                                placed.Add(candidate.Value.P);
                                measuredLocalSpans.Add(length);
                                localCount++;
                                if(verifiedMidpoint)midpointNative++;
                                else midpointBounds++;
                                fitted=true;
                                break;
                            }
                        }
                    }
                    if(!fitted)
                        skipped.Add(camera.Name+"/"+member.Member.Key+
                            ":"+(horizontal?"H":"V")+
                            "_LOCAL_MEMBER_WIDTH_NO_CLEAR_LANE");
                }
            }
        }
        return new Result(dimensions,skipped,views,chain,overall,rejected,
            requested,localCount,priorityOverallCount,
            midpointNative,midpointBounds,terminalRemainders,
            redundantChains,hierarchyRejected,manualViewSuppressed);
    }

    private static Request MakeRequest(bool horizontal,double from,
        double to,bool overall,int index,double scale,double units)
    {
        double mm=(to-from)/scale*units;
        return new Request(horizontal,from,to,overall,index,
            mm.ToString("0.###",CultureInfo.InvariantCulture),
            SupportOrthoDynamicDimension492.ModelMillimetersPerPaperUnit(
                units,scale));
    }

    private static List<double> SelectStations(
        IReadOnlyList<Station> proposed,double min,double max)
    {
        var sorted=proposed.Where(x=>double.IsFinite(x.At)&&
            x.At>min+.35&&x.At<max-.35)
            .OrderBy(x=>x.At).ToArray();
        var clustered=new List<Station>();
        foreach(var item in sorted)
        {
            if(clustered.Count>0&&
               Math.Abs(item.At-clustered[^1].At)<.12)
            {
                var last=clustered[^1];
                clustered[^1]=new Station(
                    (last.At*last.Weight+item.At*item.Weight)/
                    (last.Weight+item.Weight),
                    last.Weight+item.Weight);
            }
            else clustered.Add(item);
        }
        // Only connected opposing native edges or verified endpoints
        // become stations. Never infer bolt/hole centres from an AABB.
        var chosen=clustered.Where(x=>x.Weight>=3)
            .OrderByDescending(x=>x.Weight)
            .ThenBy(x=>x.At).Take(7)
            .OrderBy(x=>x.At).Select(x=>x.At);
        return new[]{min}.Concat(chosen).Concat(new[]{max}).ToList();
    }

    private static IEnumerable<double> PreferredSigns(
        string view,bool horizontal)
    {
        // REV22.51: never shift vertical dimensions into the right-side
        // annotation column, or horizontal dimensions below view titles.
        yield return SupportOrthoReferenceLayout51.PreferredSide(
            view,horizontal);
    }

    // Resolve the transverse start location from a REAL native
    // member edge at this station or an explicit native endpoint.
    // Overall envelope geometry is used only to select the lane,
    // NEVER to make both extension lines share the same false datum.
    private static double? WitnessAt(
        bool horizontal,double station,double sign,
        IReadOnlyList<SupportOrthoSmartDimPlan46.Rect> memberBounds,
        IReadOnlyList<(double X,double Y)> explicitEndpoints,
        bool preferExplicit=true)
    {
        const double paperTolerance=.20;
        // Real member Start/End coordinates take precedence over the
        // axis-aligned envelope, whose face may not be an actual vertex
        // of a sloping structure member.
        var exact=explicitEndpoints.Where(pt=>
                Math.Abs((horizontal?pt.X:pt.Y)-station)
                    <=paperTolerance)
            .Select(pt=>horizontal?pt.Y:pt.X).ToArray();
        if(preferExplicit&&exact.Length>0)
            return sign>0?exact.Max():exact.Min();
        var boundsFaces=new List<double>();
        foreach(var bounds in memberBounds)
        {
            double start=horizontal?bounds.L:bounds.B;
            double end=horizontal?bounds.R:bounds.T;
            if(Math.Abs(station-start)>paperTolerance&&
               Math.Abs(station-end)>paperTolerance)
                continue;
            boundsFaces.Add(horizontal?
                (sign>0?bounds.T:bounds.B):
                (sign>0?bounds.R:bounds.L));
        }
        if(boundsFaces.Count>0)
            return sign>0?boundsFaces.Max():boundsFaces.Min();
        // The actual native endpoint is used for chain stations and
        // as a fail-closed fallback for a rare envelope face that
        // cannot be resolved. It never fabricates a member cross
        // section, connection coordinate or bolt pitch.
        return exact.Length>0?
            (sign>0?exact.Max():exact.Min()):null;
    }

    private static (
        SupportOrthoDimLinearPlan41.Dimension D,Placed P)?
        Candidate(
        SupportOrthoMemberCalloutPlan17.Camera c,
        SupportOrthoSmartDimPlan46.Rect box,
        IReadOnlyList<SupportOrthoSmartDimPlan46.Rect> memberBounds,
        IReadOnlyList<(double X,double Y)> explicitEndpoints,
        Request request,double sign,double offset,int textTier=0)
    {
        var layoutBox=request.LocalBounds??box;
        // Local member cross-section dimensions must terminate on that
        // particular native member's projected faces, not on the
        // unrelated outer envelope of the whole support assembly.
        double? startWitness=request.LocalBounds.HasValue?
            request.MidpointWitness??(request.Horizontal?
                (layoutBox.B+layoutBox.T)*.5:
                (layoutBox.L+layoutBox.R)*.5):
            WitnessAt(request.Horizontal,request.From,sign,
                memberBounds,explicitEndpoints,
                !request.Overall&&!request.TerminalRemainder);
        double? endWitness=request.LocalBounds.HasValue?
            startWitness:WitnessAt(request.Horizontal,request.To,sign,
                memberBounds,explicitEndpoints,
                !request.Overall&&!request.TerminalRemainder);
        if(!startWitness.HasValue||!endWitness.HasValue)
            return null;
        double line=request.Horizontal?
            (sign>0?layoutBox.T+offset:layoutBox.B-offset):
            (sign>0?layoutBox.R+offset:layoutBox.L-offset);
        double center=(request.From+request.To)*.5;
        double printedWidth=Math.Max(7,request.Measurement.Length*1.85+3);
        double textAt=center;
        bool shortSpan=request.To-request.From<printedWidth+2;
        if(request.Overall&&!request.Horizontal&&!shortSpan)
        {
            // Shifting text normal to a vertical dimension merely
            // drives it into neighboring LEFT VIEW labels. Explore
            // physically valid positions ALONG the same dimension
            // line instead; the two measured datum/witness endpoints
            // and the DIMLINEAR measurement remain unchanged.
            double[] fractions={.50,.32,.68,.23,.77};
            double margin=printedWidth*.5+1.5;
            if(request.To-request.From>2*margin)
                textAt=Math.Clamp(
                    request.From+(request.To-request.From)*
                        fractions[textTier%fractions.Length],
                    request.From+margin,request.To-margin);
        }
        if(shortSpan)
        {
            // A 50 mm dimension at 1:30 is only 1.67 mm on paper;
            // force readable text OUTSIDE its witnesses. Alternate
            // the preferred side for consecutive 50 mm chain links
            // so identical labels don't stack on one another.
            bool putBefore=SupportOrthoReferenceLayout51
                .ShortTextBefore(request.Index,textTier);
            textAt=putBefore?
                request.From-printedWidth*.5-2.0:
                request.To+printedWidth*.5+2.0;
        }
        // Search opposite text sides first, then more distant tiers.
        // The dimension line itself stays collinear with the chain.
        double textClearance=request.Overall&&!request.Horizontal?
            2.7+(textTier/5)*1.5:
            2.7+(textTier/2)*3.5;
        double tx=request.Horizontal?textAt:line+
            (sign>0?textClearance:-textClearance);
        double ty=request.Horizontal?line+
            (sign>0?textClearance:-textClearance):textAt;
        var dimRect=request.Horizontal
            ?new SupportOrthoSmartDimPlan46.Rect(
                request.From-.4,line-.4,request.To+.4,line+.4)
            :new SupportOrthoSmartDimPlan46.Rect(
                line-.4,request.From-.4,line+.4,request.To+.4);
        var textRect=request.Horizontal
            ?new SupportOrthoSmartDimPlan46.Rect(
                tx-printedWidth*.5-1,ty-1.3,
                tx+printedWidth*.5+1,ty+1.3)
            :new SupportOrthoSmartDimPlan46.Rect(
                tx-1.3,ty-printedWidth*.5-1,
                tx+1.3,ty+printedWidth*.5+1);
        string axis=request.Horizontal?"H":"V";
        string source="DETAIL:"+c.Name+"/"+axis+"/"+
            (request.Overall?"OVERALL":
                request.TerminalRemainder?
                    "TERMINAL_REMAINDER_"+request.SourceKey:
                request.LocalBounds.HasValue?
                    "MEMBER_"+request.SourceKey:
                    "CHAIN_"+request.Index);
        string provenance=request.Overall?
            "REFERENCE_PROJECTED_OVERALL_NOT_CUT_LENGTH":
            request.TerminalRemainder?
                "REFERENCE_TERMINAL_NATIVE_MEMBER_REMAINDER_NOT_CUT_LENGTH":
            request.LocalBounds.HasValue?
                "REFERENCE_PROJECTED_MEMBER_ENVELOPE_NOT_CUT_LENGTH":
                "REFERENCE_PROJECTED_DATUM_CHAIN_NOT_BOLT_PITCH";
        // Each A/B dimension extension line now starts at its own
        // matching source member endpoint, rather than at an unrelated
        // edge of the whole multi-member assembly.
        double aX=request.Horizontal?request.From:startWitness.Value;
        double aY=request.Horizontal?startWitness.Value:request.From;
        double bX=request.Horizontal?request.To:endWitness.Value;
        double bY=request.Horizontal?endWitness.Value:request.To;
        var dim=new SupportOrthoDimLinearPlan41.Dimension(
            c.Name,source,"STRUCTURE",request.Horizontal,
            aX,aY,bX,bY,request.Horizontal?center:line,
            request.Horizontal?line:center,request.Measurement,
            provenance,tx,ty,request.PaperToModelMm);
        return (dim,new Placed(request.Horizontal,dimRect,textRect));
    }

    private static bool Fits(Placed p,
        IReadOnlyList<Obstacle> fixedAreas,
        SupportOrthoSmartDimPlan46.Rect paper,
        IEnumerable<Placed> existing,
        IReadOnlyList<SupportOrthoSmartDimPlan46.Rect> sourceMembers) =>
        RejectionReason(p,fixedAreas,paper,existing,sourceMembers)==null;

    private static string? RejectionReason(Placed p,
        IReadOnlyList<Obstacle> fixedAreas,
        SupportOrthoSmartDimPlan46.Rect paper,
        IEnumerable<Placed> existing,
        IReadOnlyList<SupportOrthoSmartDimPlan46.Rect> sourceMembers)
    {
        if(!Inside(p.Line,paper))return "DIM_LINE_OUTSIDE_SHEET";
        if(!Inside(p.Text,paper))return "DIM_TEXT_OUTSIDE_SHEET";
        // Never allow a dimension label to cover a native structure.
        if(sourceMembers.Any(m=>p.Text.Intersects(m,.12)))
            return "TEXT_OVER_NATIVE_MEMBER";
        foreach(var obstacle in fixedAreas)
        {
            if(p.Line.Intersects(obstacle.Area,.45))
                return "LINE_OVER_"+obstacle.Type;
            if(p.Text.Intersects(obstacle.Area,.45))
                return "TEXT_OVER_"+obstacle.Type;
        }
        foreach(var prev in existing)
        {
            // REV22.54: repeated short 50/75/100 mm labels must not
            // stack in the same paper-space text column even if they
            // happen to be on different dimension tiers. Apply this
            // to ALL short dynamically measured spans, not to a
            // hard-coded 50 mm model length.
            if(p.Horizontal==prev.Horizontal)
            {
                double span=p.Horizontal?p.Line.R-p.Line.L:
                    p.Line.T-p.Line.B;
                double earlier=prev.Horizontal?
                    prev.Line.R-prev.Line.L:
                    prev.Line.T-prev.Line.B;
                double at=p.Horizontal?
                    (p.Text.L+p.Text.R)*.5:
                    (p.Text.B+p.Text.T)*.5;
                double priorAt=prev.Horizontal?
                    (prev.Text.L+prev.Text.R)*.5:
                    (prev.Text.B+prev.Text.T)*.5;
                if(span<8&&earlier<8&&Math.Abs(at-priorAt)<7)
                    return "SHORT_DIMENSION_LABELS_TOO_CLOSE";
            }
            if(p.Text.Intersects(prev.Text,.45))
                return "TEXT_OVER_DIMENSION_TEXT";
            if(p.Text.Intersects(prev.Line,.4))
                return "TEXT_OVER_DIMENSION_LINE";
            if(p.Line.Intersects(prev.Text,.4))
                return "LINE_OVER_DIMENSION_TEXT";
            if(p.Horizontal!=prev.Horizontal&&
               p.Line.Intersects(prev.Line,.3))
                return "CROSS_AXIS_DIMENSION_LINES";
        }
        return null;
    }

    private static bool Inside(SupportOrthoSmartDimPlan46.Rect a,
        SupportOrthoSmartDimPlan46.Rect b)=>
        a.IsFinite&&a.L>=b.L&&a.R<=b.R&&a.B>=b.B&&a.T<=b.T;

    private static List<Obstacle> FixedZones(
        SupportOrthoMemberCalloutPlan17.Camera current,
        IReadOnlyList<SupportOrthoMemberCalloutPlan17.Camera> cameras,
        IReadOnlyList<SupportOrthoMemberCalloutPlan27.Callout>? callouts,
        IReadOnlyList<NativeOrthoViewTitlePlan.Title>? titles)
    {
        var zones=new List<Obstacle>();
        foreach(var c in cameras)
        {
            if(c.Name.Equals(current.Name,StringComparison.OrdinalIgnoreCase))
                continue;
            zones.Add(new Obstacle(new SupportOrthoSmartDimPlan46.Rect(
                c.X-c.W*.5-1,c.Y-c.H*.5-1,
                c.X+c.W*.5+1,c.Y+c.H*.5+1),
                "NEIGHBOR_VIEW_"+c.Name));
        }
        foreach(var title in titles??Array.Empty<NativeOrthoViewTitlePlan.Title>())
            zones.Add(new Obstacle(new SupportOrthoSmartDimPlan46.Rect(
                title.CenterX-title.Width*.5-1,
                title.TopY-title.Height-1,
                title.CenterX+title.Width*.5+1,title.TopY+1),
                "VIEW_TITLE_"+title.Name));
        foreach(var c in callouts??Array.Empty<SupportOrthoMemberCalloutPlan27.Callout>())
        {
            string kind=c.View.Equals(current.Name,
                StringComparison.OrdinalIgnoreCase)?
                "CURRENT_VIEW":"NEIGHBOR_VIEW_"+c.View;
            // Printed annotation text is protected more strictly than
            // a leader line; report which of these caused a reject.
            zones.Add(new Obstacle(new SupportOrthoSmartDimPlan46.Rect(
                c.LabelX-1,c.LabelTop-c.LabelHeight-1,
                c.LabelX+c.LabelWidth+1,c.LabelTop+1),
                "ANNOTATION_TEXT_"+kind));
            double land=c.LabelTop-
                SupportOrthoMemberCalloutPlan27.TextLineHeightMm*.5;
            zones.Add(new Obstacle(
                LineZone(c.AnchorX,c.AnchorY,c.LaneX,c.AnchorY),
                "LEADER_"+kind));
            zones.Add(new Obstacle(
                LineZone(c.LaneX,c.AnchorY,c.LaneX,land),
                "LEADER_"+kind));
            zones.Add(new Obstacle(
                LineZone(c.LaneX,land,c.LabelX-1,land),
                "LEADER_"+kind));
        }
        return zones;
    }

    private static SupportOrthoSmartDimPlan46.Rect LineZone(
        double ax,double ay,double bx,double by)=>
        new(Math.Min(ax,bx)-.35,Math.Min(ay,by)-.35,
            Math.Max(ax,bx)+.35,Math.Max(ay,by)+.35);

    private static bool ValidMember(
        SupportOrthoAllViewDimPlan44.Member m)=>
        !string.IsNullOrWhiteSpace(m.Key)&&
        (m.Kind=="STRUCTURE"||m.Kind=="SUPPORT")&&
        new[]{m.MinX,m.MinY,m.MinZ,m.MaxX,m.MaxY,m.MaxZ}
            .All(double.IsFinite)&&
        m.MinX<=m.MaxX&&m.MinY<=m.MaxY&&m.MinZ<=m.MaxZ;

    private static SupportOrthoSmartDimPlan46.Rect Project(
        SupportOrthoMemberCalloutPlan17.Camera c,
        SupportOrthoAllViewDimPlan44.Member m)
    {
        double minX=double.PositiveInfinity,minY=double.PositiveInfinity;
        double maxX=double.NegativeInfinity,maxY=double.NegativeInfinity;
        foreach(double x in new[]{m.MinX,m.MaxX})
        foreach(double y in new[]{m.MinY,m.MaxY})
        foreach(double z in new[]{m.MinZ,m.MaxZ})
        {
            var p=SupportOrthoMemberCalloutPlan17.Project(c,x,y,z);
            minX=Math.Min(minX,p.X);maxX=Math.Max(maxX,p.X);
            minY=Math.Min(minY,p.Y);maxY=Math.Max(maxY,p.Y);
        }
        return new SupportOrthoSmartDimPlan46.Rect(minX,minY,maxX,maxY);
    }
}
