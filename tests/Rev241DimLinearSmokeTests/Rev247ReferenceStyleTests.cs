using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev247ReferenceStyleTests
{
    internal static void Run()
    {
        // Exact numeric structure of the supplied PDF p15:
        // 712 + 110 = 822, arranged in one near chain and one outer overall.
        var top=new SupportOrthoMemberCalloutPlan17.Camera(
            "TOP",180,650,150,160,0,0,1,0,0,0,1.0/20);
        var first=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:C125_A","STRUCTURE",0,-25,0,712,25,125);
        var second=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:C125_B","STRUCTURE",712,-25,0,822,25,125);
        // REV22.50: the 712 and 110 chain is legitimate only when
        // native endpoints confirm every station; adjacent WCS AABBs
        // are NOT enough to invent a connection or a 50 mm split.
        var firstEndpoint=new SupportOrthoDimLinearPlan41.Segment(
            "NATIVE:C125_A","STRUCTURE",
            0,-25,0,712,-25,0,712,712,
            "EXPLICIT_MEMBER_ENDPOINTS");
        var secondEndpoint=new SupportOrthoDimLinearPlan41.Segment(
            "NATIVE:C125_B","STRUCTURE",
            712,-25,0,822,-25,0,110,110,
            "EXPLICIT_MEMBER_ENDPOINTS");
        var plan=SupportOrthoReferenceDimPlan47.Build(
            new[]{top},new[]{first,second},594,841,1,
            new[]{firstEndpoint,secondEndpoint});
        var h=plan.Dimensions.Where(d=>d.Horizontal).ToArray();
        Need(h.Length==3,"p15 TOP has two chain pieces and one overall");
        Need(h.Count(d=>d.Measurement=="712")==1&&
            h.Count(d=>d.Measurement=="110")==1&&
            h.Count(d=>d.Measurement=="822")==1,
            "p15 dimension values follow real 712+110=822 datum edges");
        Need(plan.ChainDimensions==2&&plan.OverallDimensions==2,
            "grouped chain plus H/V overall; not every member H/V");
        Need(h.Where(d=>d.Source.Contains("/CHAIN_"))
            .Select(d=>d.LineY).Distinct().Count()==1,
            "chain dimension segments are collinear in one lane");
        Need(h.Single(d=>d.Source.EndsWith("/OVERALL")).LineY >
            h.First(d=>d.Source.Contains("/CHAIN_")).LineY+3,
            "overall dimension is outside the shorter detail chain");
        Need(plan.Dimensions.All(d=>d.Provenance.StartsWith(
            "REFERENCE_PROJECTED_")),
            "never claim measured paper AABB is fabricated member cut length");

        // The dimensional chain on PDF p16: 775+300+200+300+
        // 366+152+132 = 2225. Datum boundaries are the source of truth.
        var segments=new[]{775d,300,200,300,366,152,132};
        double x=0;
        var members=segments.Select((length,index)=>
        {
            double start=x;x+=length;
            return new SupportOrthoAllViewDimPlan44.Member(
                "NATIVE:SEG_"+index,"STRUCTURE",
                start,-35,0,x,35,125);
        }).ToArray();
        var wide=top with {X=210,W=280,ViewCenterX=1112.5};
        var verifiedSegments=members.Select(m=>
            new SupportOrthoDimLinearPlan41.Segment(
                m.Key,m.Kind,m.MinX,-35,0,m.MaxX,-35,0,
                m.MaxX-m.MinX,m.MaxX-m.MinX,
                "EXPLICIT_MEMBER_ENDPOINTS")).ToArray();
        var longPlan=SupportOrthoReferenceDimPlan47.Build(
            new[]{wide},members,594,841,1,verifiedSegments);
        Need(longPlan.Dimensions.Any(d=>d.Horizontal&&
            d.Source.EndsWith("/OVERALL")&&d.Measurement=="2225"),
            "p16 overall dimension 2225 follows actual projected extents");
        Need(longPlan.Dimensions.Count(d=>d.Horizontal &&
            d.Source.Contains("/CHAIN_"))>=4,
            "p16 internal station dimensions survive as a meaningful chain");

        // A single beam is dimensioned once as overall, not twice as
        // its own AABB and again as a fabricated cut-length surrogate.
        var one=SupportOrthoReferenceDimPlan47.Build(
            new[]{top},new[]{first},594,841,1);
        Need(one.ChainDimensions==0&&one.OverallDimensions==2,
            "no fictitious split or redundant per-member dimensions");

        // Explicit endpoint evidence is allowed to define a station; an
        // unavailable bolt/hole centre is NOT synthesized from its bounds.
        var endpoint=new SupportOrthoDimLinearPlan41.Segment(
            "NATIVE:C125_A","STRUCTURE",
            360,-25,0,360,25,0,50,50,
            "EXPLICIT_MEMBER_ENDPOINTS");
        var oneWithPoint=SupportOrthoReferenceDimPlan47.Build(
            new[]{top},new[]{first},594,841,1,new[]{endpoint});
        Need(oneWithPoint.ChainDimensions==0,
            "single internal endpoint without verified endcaps must not "+
            "invent a complete local dimension chain");
        Need(!oneWithPoint.Dimensions.Any(d=>d.Source.Contains("BOLT")),
            "bolt pitches are never invented from member bounding boxes");

        // REV22.48: a remote XREF, even when large, must not change
        // native-only overall or inject a projected datum station.
        var xref=new SupportOrthoAllViewDimPlan44.Member(
            "XREF_MEMBER:REMOTE","STRUCTURE",-2000,-100,0,
            3000,100,100);
        var mixed=SupportOrthoReferenceDimPlan47.Build(
            new[]{top},new[]{first,second,xref},594,841,1,
            new[]{firstEndpoint,secondEndpoint});
        Need(mixed.Dimensions.Select(d=>d.Measurement)
            .OrderBy(x=>x).SequenceEqual(
                plan.Dimensions.Select(d=>d.Measurement).OrderBy(x=>x)),
            "XREF never changes native overall or chain dimensions");
        Need(mixed.Skipped.Any(x=>x.Contains("XREF_OR_NONNATIVE")),
            "XREF exclusion recorded in reference plan");
        var onlyXref=SupportOrthoReferenceDimPlan47.Build(
            new[]{top},new[]{xref},594,841,1);
        Need(onlyXref.Dimensions.Count==0,
            "XREF-only drawing produces zero dimensions");

        // A fabricated XREF Start/End inside the native envelope still
        // cannot insert an extra station into a native chain.
        var xrefEndpoint=endpoint with {
            Key="XREF_MEMBER:VERIFIED",Ax=200,Bx=200
        };
        var injected=SupportOrthoReferenceDimPlan47.Build(
            new[]{top},new[]{first},594,841,1,
            new[]{xrefEndpoint});
        Need(injected.ChainDimensions==0,
            "XREF endpoint cannot introduce chain or bolt position");
        var detached=endpoint with {Key="NATIVE:NONMEMBER"};
        var unverified=SupportOrthoReferenceDimPlan47.Build(
            new[]{top},new[]{first},594,841,1,
            new[]{detached});
        Need(unverified.ChainDimensions==0,
            "unrelated native endpoint cannot become a datum station");
        var iso=top with {Name="NW",DirX=1,DirY=-1,DirZ=1};
        var subset=SupportOrthoReferenceDimPlan47.Build(
            new[]{top,iso},new[]{first,second},594,841,1,
            new[]{firstEndpoint,secondEndpoint});
        Need(subset.OrthographicViews==1&&
            subset.Dimensions.All(d=>d.View=="TOP"),
            "isometric foreshortening does not create false linear dimensions");
        Console.WriteLine(
            "REV22.47 PDF-reference datum-chain, overall, provenance and view regression PASS");
    }

    private static void Need(bool ok,string description)
    {
        if(!ok)throw new Exception("REV22.47: "+description);
    }
}
