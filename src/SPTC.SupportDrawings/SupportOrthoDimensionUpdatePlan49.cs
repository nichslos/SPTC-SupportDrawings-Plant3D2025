using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.49 pure, testable refresh policy. Only explicitly registered
/// SPTC-generated Ortho drawings can be refreshed. Never automatically
/// dimension XREF geometry or modify non-SPTC paper dimensions.
/// </summary>
public static class SupportOrthoDimensionUpdatePlan49
{
    public const string ManifestKey="SPTC_DIM_REFRESH49";
    public const string OwnerApp="SPTC_DIM_OWNER49";
    public const int ManifestVersion=1;

    public sealed record NativeMember(string Key,string Kind);
    public sealed record View(string Name,string Handle,
        double X,double Y,double W,double H);
    public sealed record Manifest(
        int Version,string SourcePath,string LayoutName,
        double UnitsToMm,double PaperWidth,double PaperHeight,
        bool ReferenceDetailDimensions,
        IReadOnlyList<NativeMember> Members,
        IReadOnlyList<View> Views,
        string LastSignature,
        long SourceWriteUtcTicks=0,
        long SourceFileLength=-1,
        string LastViewPoseSignature="",
        int LastOwnedDimensionCount=-1,
        IReadOnlyList<SupportOrthoAllViewDimPlan44.Member>? NativeExtentSnapshot=null,
        IReadOnlyList<SupportOrthoDimLinearPlan41.Segment>? NativeEndpointSnapshot=null,
        IReadOnlyList<SupportOrthoNativeEdgeGeometry59.Edge>? NativeEdgeSnapshot=null,
        IReadOnlyList<SupportOrthoDimLinearPlan41.Dimension>? LastVerifiedDimensions=null);

    public sealed record Candidate(string Handle,
        double X,double Y,double W,double H);

    public static void Validate(Manifest manifest)
    {
        if(manifest.Version!=ManifestVersion||
           string.IsNullOrWhiteSpace(manifest.SourcePath)||
           string.IsNullOrWhiteSpace(manifest.LayoutName)||
           !double.IsFinite(manifest.UnitsToMm)||manifest.UnitsToMm<=0||
           !double.IsFinite(manifest.PaperWidth)||manifest.PaperWidth<=0||
           !double.IsFinite(manifest.PaperHeight)||manifest.PaperHeight<=0||
           manifest.Members==null||manifest.Members.Count==0||
           manifest.Views==null||manifest.Views.Count==0)
            throw new ArgumentException("REV22.49 invalid/unsupported saved Ortho manifest.");
        if(manifest.Members.Any(m=>
                !SupportOrthoNativeDimensionPolicy48.IsNativeKey(m.Key)||
                (m.Kind!="STRUCTURE"&&m.Kind!="SUPPORT"))||
           manifest.Members.Select(m=>m.Key).Distinct(
                StringComparer.OrdinalIgnoreCase).Count()!=manifest.Members.Count)
            throw new ArgumentException("REV22.49 XREF or duplicate member in manifest.");
        if(manifest.Views.Any(v=>string.IsNullOrWhiteSpace(v.Name)||
                string.IsNullOrWhiteSpace(v.Handle)||
                !double.IsFinite(v.X)||!double.IsFinite(v.Y)||
                !double.IsFinite(v.W)||!double.IsFinite(v.H)||
                v.W<=0||v.H<=0)||
           manifest.Views.Select(v=>v.Name).Distinct(
                StringComparer.OrdinalIgnoreCase).Count()!=manifest.Views.Count)
            throw new ArgumentException("REV22.49 invalid saved Ortho viewport references.");
        // REV22.50 cached snapshots must represent exactly the verified
        // native members in this drawing. Missing/partial snapshots are
        // deliberately ineligible for fast geometry reuse.
        if(manifest.NativeExtentSnapshot!=null&&
           manifest.NativeExtentSnapshot.Count>0)
        {
            if(manifest.NativeExtentSnapshot.Count!=manifest.Members.Count||
               manifest.NativeExtentSnapshot.Any(m=>
                   !SupportOrthoNativeDimensionPolicy48.AcceptMember(m)||
                   !new[]{m.MinX,m.MinY,m.MinZ,m.MaxX,m.MaxY,m.MaxZ}
                       .All(double.IsFinite)||
                   m.MinX>m.MaxX||m.MinY>m.MaxY||m.MinZ>m.MaxZ||
                   !manifest.Members.Any(original=>
                       original.Key.Equals(m.Key,
                           StringComparison.OrdinalIgnoreCase)&&
                       original.Kind==m.Kind))||
               manifest.NativeExtentSnapshot.Select(m=>m.Key)
                   .Distinct(StringComparer.OrdinalIgnoreCase).Count()!=
                   manifest.Members.Count)
                throw new ArgumentException(
                    "REV22.50 cached native member extents are invalid.");
        }
        if(manifest.NativeEndpointSnapshot!=null&&
           manifest.NativeEndpointSnapshot.Any(seg=>
               !SupportOrthoNativeDimensionPolicy48.AcceptSegment(seg)||
               !manifest.Members.Any(m=>
                   m.Key.Equals(seg.Key,
                       StringComparison.OrdinalIgnoreCase)&&
                   m.Kind==seg.Kind)||
               !new[]{seg.Ax,seg.Ay,seg.Az,seg.Bx,seg.By,seg.Bz,
                   seg.SourceLength,seg.Millimeters}.All(double.IsFinite)||
               seg.SourceLength<=0||seg.Millimeters<=0))
            throw new ArgumentException(
                "REV22.50 cached native endpoint segment invalid.");
        // REV22.59: source-stamped straight-edge evidence is small and
        // host-native only. Reject any XREF/invented geometry or edge
        // that does not belong to one of the explicitly selected
        // source model members. Legacy manifests without edges remain
        // backward compatible and use projected-envelope dimensions.
        if(manifest.NativeEdgeSnapshot!=null)
        {
            if(manifest.NativeEdgeSnapshot.Count>512||
               manifest.NativeEdgeSnapshot.Any(edge=>
                   !SupportOrthoNativeEdgeGeometry59.Valid(edge)||
                   !manifest.Members.Any(member=>
                       member.Key.Equals(edge.Key,
                           StringComparison.OrdinalIgnoreCase)&&
                       member.Kind==edge.Kind)))
                throw new ArgumentException(
                    "REV22.59 saved native edge evidence invalid or XREF.");
            if((manifest.NativeExtentSnapshot==null||
                manifest.NativeExtentSnapshot.Count==0)&&
                manifest.NativeEdgeSnapshot.Count>0)
                throw new ArgumentException(
                    "REV22.59 exact edges require selected native extents.");
        }
    }

    /// <summary>
    /// A saved plan is usable for fast repair only if it is a complete,
    /// independently signed record of the verified source dimensions.
    /// Legacy manifests without such a record continue through the
    /// original authoritative source/viewport verification path.
    /// </summary>
    public static bool HasTrustedVerifiedPlan(Manifest saved)
    {
        if(saved.LastVerifiedDimensions==null||
           saved.LastVerifiedDimensions.Count==0||
           saved.LastVerifiedDimensions.Count>512||
           saved.LastOwnedDimensionCount!=
               saved.LastVerifiedDimensions.Count||
           string.IsNullOrWhiteSpace(saved.LastSignature)||
           !string.Equals(saved.LastSignature,
               Signature(saved.LastVerifiedDimensions),
               StringComparison.Ordinal))
            return false;
        var viewNames=new HashSet<string>(
            saved.Views.Select(x=>x.Name),StringComparer.OrdinalIgnoreCase);
        return saved.LastVerifiedDimensions.All(d=>
            !string.IsNullOrWhiteSpace(d.View)&&
            viewNames.Contains(d.View)&&
            !string.IsNullOrWhiteSpace(d.Source)&&
            !d.Source.Contains("XREF",StringComparison.OrdinalIgnoreCase)&&
            !string.IsNullOrWhiteSpace(d.Measurement)&&
            !string.IsNullOrWhiteSpace(d.Provenance)&&
            new[]{d.Ax,d.Ay,d.Bx,d.By,d.LineX,d.LineY}
                .All(double.IsFinite)&&
            (!d.PaperTextX.HasValue||
                double.IsFinite(d.PaperTextX.Value))&&
            (!d.PaperTextY.HasValue||
                double.IsFinite(d.PaperTextY.Value))&&
            (!d.PaperToModelMm.HasValue||
                (double.IsFinite(d.PaperToModelMm.Value)&&
                 d.PaperToModelMm.Value>0&&
                 SupportOrthoDynamicDimension492.MatchesDiagnostic(d))));
    }

    /// <summary>
    /// An unchanged verified source DWG and identical native viewport
    /// poses authorize restoring missing SPTC-owned dimensions from
    /// the previously validated plan. A model/pan/scale change or a
    /// plan without trusted provenance must use full regeneration.
    /// </summary>
    public static bool CanRepairVerifiedDimensions(
        Manifest saved,long sourceWriteUtcTicks,long sourceLength,
        string currentPose,int currentOwned)
    {
        Validate(saved);
        return HasTrustedVerifiedPlan(saved)&&
            saved.SourceWriteUtcTicks>0&&
            saved.SourceWriteUtcTicks==sourceWriteUtcTicks&&
            saved.SourceFileLength>=0&&
            saved.SourceFileLength==sourceLength&&
            !string.IsNullOrWhiteSpace(saved.LastViewPoseSignature)&&
            string.Equals(saved.LastViewPoseSignature,currentPose,
                StringComparison.Ordinal)&&
            currentOwned>=0&&currentOwned<
                saved.LastVerifiedDimensions!.Count;
    }

    /// <summary>
    /// Reuse the original native viewport handle first; Plant 3D may
    /// replace the viewport during Update View, so permit an unambiguous
    /// near-position fallback. No name-only or Current View fallback.
    /// </summary>
    public static Candidate? Resolve(
        View saved,IReadOnlyList<Candidate> actual,
        ISet<string> alreadyUsed)
    {
        var match=actual.FirstOrDefault(x=>
            !alreadyUsed.Contains(x.Handle)&&
            x.Handle.Equals(saved.Handle,StringComparison.OrdinalIgnoreCase));
        if(match!=null)return match;
        var nearby=actual.Where(x=>
            !alreadyUsed.Contains(x.Handle)&&
            Math.Abs(x.X-saved.X)<2.0&&
            Math.Abs(x.Y-saved.Y)<2.0&&
            Math.Abs(x.W-saved.W)<3.0&&
            Math.Abs(x.H-saved.H)<3.0)
            .OrderBy(x=>Math.Pow(x.X-saved.X,2)+
                Math.Pow(x.Y-saved.Y,2)+
                Math.Pow(x.W-saved.W,2)+
                Math.Pow(x.H-saved.H,2))
            .ToArray();
        // Ambiguous viewport identity is never safe for auto rewriting.
        return nearby.Length==1?nearby[0]:null;
    }

    public static string Signature(
        IReadOnlyList<SupportOrthoDimLinearPlan41.Dimension> dimensions)
    {
        var rows=dimensions.Select(x=>string.Join("|",
            x.View,x.Source,x.Measurement,x.Provenance,
            x.Horizontal?"H":"V",
            N(x.Ax),N(x.Ay),N(x.Bx),N(x.By),
            N(x.LineX),N(x.LineY),
            N(x.PaperTextX??0),N(x.PaperTextY??0),
            N(x.PaperToModelMm??0)))
            .OrderBy(s=>s,StringComparer.Ordinal).ToArray();
        var bytes=SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join("\n",rows)));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// A cheap, read-only early exit for the usual no-change
    /// PLANTORTHOUPDATE. Never trust only the file timestamp: the native
    /// view pose and owned DIMLINEAR count must also match the last saved
    /// state. Old REV22.49 manifests with no cache fields refresh once.
    /// </summary>
    /// <summary>
    /// If the native MODEL DWG has not changed but Plant Ortho modifies a
    /// viewport scale/camera, reuse the exact saved native geometry.
    /// This avoids repeating ReadDwgFile/GeometricExtents/reflection just
    /// because a paper-space view was updated. Unknown or legacy cached
    /// content always falls back to a full verified source-model read.
    /// </summary>
    public static bool CanReuseGeometrySnapshot(
        Manifest manifest,long currentSourceWriteUtcTicks,
        long currentSourceFileLength)
    {
        Validate(manifest);
        return manifest.SourceWriteUtcTicks>0&&
            manifest.SourceWriteUtcTicks==currentSourceWriteUtcTicks&&
            manifest.SourceFileLength>=0&&
            manifest.SourceFileLength==currentSourceFileLength&&
            manifest.NativeExtentSnapshot!=null&&
            manifest.NativeExtentSnapshot.Count==manifest.Members.Count;
    }

    public static bool CanSkipModelReload(
        Manifest saved,long currentSourceWriteUtcTicks,
        long currentSourceFileLength,string currentViewPose,
        int currentOwnedDimensionCount)
    {
        Validate(saved);
        return saved.SourceWriteUtcTicks>0&&
            saved.SourceFileLength>=0&&
            saved.LastOwnedDimensionCount>=0&&
            (saved.LastVerifiedDimensions==null||
                HasTrustedVerifiedPlan(saved))&&
            !string.IsNullOrWhiteSpace(saved.LastViewPoseSignature)&&
            saved.SourceWriteUtcTicks==currentSourceWriteUtcTicks&&
            saved.SourceFileLength==currentSourceFileLength&&
            string.Equals(saved.LastViewPoseSignature,currentViewPose,
                StringComparison.Ordinal)&&
            saved.LastOwnedDimensionCount==currentOwnedDimensionCount;
    }

    public static string ViewPoseSignature(
        IReadOnlyList<SupportOrthoMemberCalloutPlan17.Camera> cameras,
        double paperW,double paperH)
    {
        if(cameras==null||cameras.Count==0||
           !double.IsFinite(paperW)||paperW<=0||
           !double.IsFinite(paperH)||paperH<=0)
            throw new ArgumentException("REV22.49.3 invalid native view poses.");
        var rows=cameras.Select(c=>string.Join("|",
            c.Name.ToUpperInvariant(),N(c.X),N(c.Y),N(c.W),N(c.H),
            N(c.DirX),N(c.DirY),N(c.DirZ),
            N(c.ViewCenterX),N(c.ViewCenterY),
            N(c.TwistRadians),N(c.Scale)))
            .OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        var bytes=SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join("\n",rows)+"|PAPER|"+N(paperW)+"|"+N(paperH)));
        return Convert.ToHexString(bytes);
    }

    public static bool NeedsReplace(string? lastSignature,
        string currentSignature,int currentlyOwned,
        int desiredCount) =>
        !string.Equals(lastSignature,currentSignature,
            StringComparison.Ordinal)||
        currentlyOwned!=desiredCount;

    private static string N(double d)=>
        Math.Round(d,5).ToString("0.#####",CultureInfo.InvariantCulture);
}
