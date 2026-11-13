using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.61: independent, read-only drawing QA for already-selected
/// native DIMLINEARs. Review the model-derived H/V overall coverage,
/// source identity, projected value, duplicate geometry and actual
/// extension-line/text intersections BEFORE writing the paper drawing.
/// A missing optional member detail is logged but does NOT invalidate
/// a sound native Ortho view or suppress other dimensions.
/// </summary>
public static class SupportOrthoDrawingQualityAudit61
{
    private static readonly HashSet<string> Ortho=new(
        new[]{"TOP","BOTTOM","LEFT","RIGHT","FRONT","BACK"},
        StringComparer.OrdinalIgnoreCase);

    public sealed record Issue(string Code,string View,string Description);
    public sealed record Result(
        string Status,int ExpectedOverallAxes,int PresentOverallAxes,
        int MissingOverallAxes,int DuplicateDimensions,
        int MeasurementMismatches,int UnverifiedSources,
        int GeometryIntersections,int OptionalOmissions,
        int WrittenCountMismatches,IReadOnlyList<Issue> Issues)
    {
        public int ReviewIssues => Issues.Count;
    }

    public static Result Review(
        IReadOnlyList<SupportOrthoMemberCalloutPlan17.Camera> cameras,
        IReadOnlyList<SupportOrthoAllViewDimPlan44.Member> members,
        IReadOnlyList<SupportOrthoDimLinearPlan41.Dimension> dimensions,
        IReadOnlyList<string>? plannerSkipped,
        double unitsToMm,bool referenceStyle=true,
        int? writtenCount=null,
        IReadOnlyList<SupportOrthoNativeEdgeGeometry59.Edge>? nativeEdges=null)
    {
        if(cameras==null||members==null||dimensions==null||
           !double.IsFinite(unitsToMm)||unitsToMm<=0||
           writtenCount.HasValue&&writtenCount<0)
            throw new ArgumentException("REV22.61 invalid QA input.");

        var issues=new List<Issue>();
        void Flag(string code,string view,string reason) =>
            issues.Add(new Issue(code,view,reason));
        int expected=0,present=0,missing=0,duplicates=0,
            incorrect=0,unverified=0,intersections=0,writeMismatch=0;
        var eligible=members.Where(
            SupportOrthoNativeDimensionPolicy48.AcceptMember).ToArray();
        var selected=eligible.Select(m=>m.Key).ToHashSet(
            StringComparer.OrdinalIgnoreCase);
        var validCameras=cameras.Where(c=>
            Ortho.Contains(c.Name)&&double.IsFinite(c.Scale)&&c.Scale>0&&
            double.IsFinite(c.W)&&double.IsFinite(c.H)&&
            c.W>0&&c.H>0).ToArray();

        if(validCameras.Length==0)
            Flag("NO_VALID_ORTHOGRAPHIC_VIEWS","SHEET",
                "No valid native orthographic camera to audit.");
        if(eligible.Length==0)
            Flag("NO_NATIVE_MEMBER_EVIDENCE","SHEET",
                "No selected host-DWG structural/support member.");

        // Use the same projected native structure envelope and
        // viewport visibility boundary as the REV22.47 planner.
        // Do not count unseen members or XREFs toward a required
        // overall dimension.
        foreach(var c in validCameras)
        {
            var visible=eligible.Where(m=>FiniteMember(m))
                .Select(m=>Project(c,m))
                .Where(r=>r.IsFinite&&
                    r.L>=c.X-c.W*.5+1.4-.05&&
                    r.R<=c.X+c.W*.5-1.4+.05&&
                    r.B>=c.Y-c.H*.5+1.4-.05&&
                    r.T<=c.Y+c.H*.5-1.4+.05)
                .ToArray();
            if(visible.Length==0)
            {
                Flag("NO_VISIBLE_VERIFIED_NATIVE_MEMBER",c.Name,
                    "No native member has a verified projection in this viewport.");
                continue;
            }
            if(!referenceStyle)continue;
            double l=visible.Min(x=>x.L),r=visible.Max(x=>x.R);
            double b=visible.Min(x=>x.B),t=visible.Max(x=>x.T);
            foreach(bool horizontal in new[]{true,false})
            {
                double paperSpan=horizontal?r-l:t-b;
                if(paperSpan<.25)continue;
                expected++;
                var overall=dimensions.Where(d=>
                    d.View.Equals(c.Name,StringComparison.OrdinalIgnoreCase)&&
                    d.Horizontal==horizontal&&
                    d.Source.EndsWith("/OVERALL",StringComparison.Ordinal))
                    .ToArray();
                if(overall.Length==0)
                {
                    missing++;
                    Flag("MISSING_ESSENTIAL_OVERALL",c.Name,
                        (horizontal?"H":"V")+
                        " native geometry requires an overall dimension.");
                    continue;
                }
                present++;
                if(overall.Length!=1)
                {
                    duplicates+=overall.Length-1;
                    Flag("DUPLICATE_ESSENTIAL_OVERALL",c.Name,
                        (horizontal?"H":"V")+" has "+
                        overall.Length.ToString(CultureInfo.InvariantCulture)+
                        " overall dimensions.");
                }
                // Audited measurement must be the current native
                // projected span, not the literal number from an
                // earlier manual reference PDF or a stored dimension.
                foreach(var d in overall)
                {
                    double factor=unitsToMm/c.Scale;
                    double expectedMm=paperSpan*factor;
                    double actual;
                    try {actual=SupportOrthoDynamicDimension492
                        .MeasuredMillimeters(d);}
                    catch(ArgumentException){continue;}
                    if(Math.Abs(actual-expectedMm)>
                        Math.Max(.02,expectedMm*.00002))
                    {
                        incorrect++;
                        Flag("OVERALL_NOT_MATCHING_NATIVE_ENVELOPE",c.Name,
                            (horizontal?"H":"V")+
                            " measured length differs from current native projection.");
                    }
                }
            }
        }

        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var examined=new List<(SupportOrthoDimLinearPlan41.Dimension D,
            SupportOrthoDimIntersections58.Shape Geometry)>();
        foreach(var d in dimensions)
        {
            if(!Ortho.Contains(d.View))
            {
                unverified++;
                Flag("UNSUPPORTED_VIEW",d.View,
                    "DIMLINEAR refers to a non-orthographic view.");
                continue;
            }
            if(!validCameras.Any(c=>c.Name.Equals(d.View,
                    StringComparison.OrdinalIgnoreCase)))
            {
                unverified++;
                Flag("MISSING_CAMERA_REFERENCE",d.View,
                    "DIMLINEAR has no matching native viewport camera.");
            }
            if(string.IsNullOrWhiteSpace(d.Source)||
               d.Source.Contains("XREF",StringComparison.OrdinalIgnoreCase))
            {
                unverified++;
                Flag("XREF_OR_UNKNOWN_DIMENSION_SOURCE",d.View,
                    "An XREF or empty source cannot create native dimensions.");
                continue;
            }
            int memberStart=d.Source.IndexOf("/MEMBER_",
                StringComparison.OrdinalIgnoreCase);
            int remainderStart=d.Source.IndexOf("/TERMINAL_REMAINDER_",
                StringComparison.OrdinalIgnoreCase);
            string? memberKey=memberStart>=0?
                d.Source[(memberStart+"/MEMBER_".Length)..]:
                remainderStart>=0?
                d.Source[(remainderStart+"/TERMINAL_REMAINDER_".Length)..]:null;
            if(memberKey!=null&&!selected.Contains(memberKey))
            {
                unverified++;
                Flag("MEMBER_SOURCE_NOT_SELECTED",d.View,
                    "Dimension references an unselected host-native member.");
            }
            if(d.Provenance==
                "EXACT_NATIVE_PROJECTED_PROFILE_EDGE_NOT_CUT_LENGTH"&&
               (memberKey==null||nativeEdges!=null&&
                !nativeEdges.Any(e=>
                    SupportOrthoNativeEdgeGeometry59.Valid(e)&&
                    e.Key.Equals(memberKey,StringComparison.OrdinalIgnoreCase))))
            {
                unverified++;
                Flag("EXACT_EDGE_EVIDENCE_MISSING",d.View,
                    "Exact-profile claim lacks matching native edge evidence.");
            }
            try
            {
                if(!SupportOrthoDynamicDimension492.MatchesDiagnostic(d))
                {
                    incorrect++;
                    Flag("DYNAMIC_MEASUREMENT_MISMATCH",d.View,
                        "DIMLINEAR endpoints, DIMLFAC and diagnostic measurement disagree.");
                }
            }
            catch(ArgumentException)
            {
                incorrect++;
                Flag("DYNAMIC_MEASUREMENT_UNVERIFIED",d.View,
                    "Invalid witness points or missing viewport scale.");
            }
            string identity=d.View+"|"+d.Source+"|"+
                (d.Horizontal?"H":"V");
            if(!seen.Add(identity))
            {
                duplicates++;
                Flag("DUPLICATE_DIMENSION_SOURCE",d.View,
                    "Repeated dimension source on the same view and axis.");
            }
            if(!new[]{d.Ax,d.Ay,d.Bx,d.By,d.LineX,d.LineY}
                    .All(double.IsFinite)||
                d.PaperTextX.HasValue&&!double.IsFinite(d.PaperTextX.Value)||
                d.PaperTextY.HasValue&&!double.IsFinite(d.PaperTextY.Value))
            {
                unverified++;
                Flag("NONFINITE_DIMENSION_GEOMETRY",d.View,
                    "Invalid dimension line, witness or label position.");
                continue;
            }
            // The reference-style planner reserves the exact paper
            // text rectangle with these same values and sizes.
            // Use this independently to catch collisions introduced
            // by a future update or different dimension selection.
            if(referenceStyle&&d.PaperTextX.HasValue&&
                d.PaperTextY.HasValue)
            {
                var geom=SupportOrthoDimIntersections58.Project(d,
                    TextBox(d));
                foreach(var prior in examined)
                {
                    if(!prior.D.View.Equals(d.View,
                            StringComparison.OrdinalIgnoreCase))
                        continue;
                    string? crossing=SupportOrthoDimIntersections58
                        .Conflict(geom,prior.Geometry);
                    if(crossing==null)continue;
                    intersections++;
                    Flag("UNINTENDED_DIMENSION_INTERSECTION",d.View,
                        crossing+" between "+d.Source+
                        " and "+prior.D.Source);
                }
                examined.Add((d,geom));
            }
        }

        if(writtenCount.HasValue&&writtenCount.Value!=dimensions.Count)
        {
            writeMismatch=1;
            Flag("WRITTEN_DIMENSION_COUNT_MISMATCH","SHEET",
                "Paper-space writer did not create the complete approved dimension plan.");
        }
        int optional=(plannerSkipped??Array.Empty<string>())
            .Count(s=>s.Contains(
                    "REV22_60_OPTIONAL_PROFILE_NO_COMPATIBLE_LAYOUT",
                    StringComparison.Ordinal)||
                s.Contains("_LOCAL_MEMBER_WIDTH_NO_CLEAR_LANE",
                    StringComparison.Ordinal)||
                s.Contains("LOCAL_CHAIN_UNVERIFIED_MEMBER_ENDPOINTS",
                    StringComparison.Ordinal)||
                s.Contains("TERMINAL_REMAINDER_NO_CLEAR_LANE",
                    StringComparison.Ordinal));

        // Optional crowded-space omissions are *not* audit failures.
        // Required H/V overall, dynamic source safety and drawing
        // integrity are. No page-wide throw for a missing optional 50.
        return new Result(issues.Count==0?"PASS":"REVIEW",
            expected,present,missing,duplicates,incorrect,
            unverified,intersections,optional,writeMismatch,issues);
    }

    private static SupportOrthoSmartDimPlan46.Rect TextBox(
        SupportOrthoDimLinearPlan41.Dimension d)
    {
        double printedWidth=Math.Max(7,d.Measurement.Length*1.85+3);
        double x=d.PaperTextX!.Value,y=d.PaperTextY!.Value;
        return d.Horizontal?
            new SupportOrthoSmartDimPlan46.Rect(
                x-printedWidth*.5-1,y-1.3,x+printedWidth*.5+1,y+1.3):
            new SupportOrthoSmartDimPlan46.Rect(
                x-1.3,y-printedWidth*.5-1,
                x+1.3,y+printedWidth*.5+1);
    }

    private static bool FiniteMember(
        SupportOrthoAllViewDimPlan44.Member m) =>
        new[]{m.MinX,m.MinY,m.MinZ,m.MaxX,m.MaxY,m.MaxZ}
            .All(double.IsFinite)&&
        m.MinX<=m.MaxX&&m.MinY<=m.MaxY&&m.MinZ<=m.MaxZ;

    private static SupportOrthoSmartDimPlan46.Rect Project(
        SupportOrthoMemberCalloutPlan17.Camera c,
        SupportOrthoAllViewDimPlan44.Member m)
    {
        double l=double.PositiveInfinity,b=double.PositiveInfinity,
            r=double.NegativeInfinity,t=double.NegativeInfinity;
        foreach(double x in new[]{m.MinX,m.MaxX})
        foreach(double y in new[]{m.MinY,m.MaxY})
        foreach(double z in new[]{m.MinZ,m.MaxZ})
        {
            var p=SupportOrthoMemberCalloutPlan17.Project(c,x,y,z);
            l=Math.Min(l,p.X);r=Math.Max(r,p.X);
            b=Math.Min(b,p.Y);t=Math.Max(t,p.Y);
        }
        return new SupportOrthoSmartDimPlan46.Rect(l,b,r,t);
    }
}
