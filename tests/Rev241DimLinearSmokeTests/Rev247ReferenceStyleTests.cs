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
        var plan=SupportOrthoReferenceDimPlan47.Build(
            new[]{top},new[]{first,second},594,841,1);
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
        var longPlan=SupportOrthoReferenceDimPlan47.Build(
            new[]{wide},members,594,841,1);
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
            "NATIVE:VERIFIED","STRUCTURE",
            360,-25,0,360,25,0,50,50,
            "EXPLICIT_MEMBER_ENDPOINTS");
        var oneWithPoint=SupportOrthoReferenceDimPlan47.Build(
            new[]{top},new[]{first},594,841,1,new[]{endpoint});
        Need(oneWithPoint.ChainDimensions>=2,
            "verified model endpoint can create a detail station");
        Need(!oneWithPoint.Dimensions.Any(d=>d.Source.Contains("BOLT")),
            "bolt pitches are never invented from member bounding boxes");

        var iso=top with {Name="NW",DirX=1,DirY=-1,DirZ=1};
        var subset=SupportOrthoReferenceDimPlan47.Build(
            new[]{top,iso},new[]{first,second},594,841,1);
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
