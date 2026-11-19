using SPTC.SupportDrawings;
using Camera=SPTC.SupportDrawings.SupportOrthoMemberCalloutPlan17.Camera;
using Member=SPTC.SupportDrawings.SupportOrthoAllViewDimPlan44.Member;
using Segment=SPTC.SupportDrawings.SupportOrthoDimLinearPlan41.Segment;
using Ends=SPTC.SupportDrawings.SupportOrthoSolidEndStations;
using Point=SPTC.SupportDrawings.SupportOrthoSolidEndStations.Point;
using Pair=SPTC.SupportDrawings.SupportOrthoSolidEndStations.Pair;
using Edge=SPTC.SupportDrawings.SupportOrthoSolidEndStations.Edge;

internal static class MemberEndpointCoverageTests
{
    private static void Check(bool ok,string message)
    {
        if(!ok)throw new Exception("Member endpoint coverage: "+message);
    }

    private static void SolidGeometry()
    {
        var origin=new Point(53424.635,-16773.298,8837.621);
        var channel=new[]{(0,0),(40,0),(40,6),(7,6),(7,65),(40,65),(40,72),(0,72)};
        var box=new[]{(0,0),(40,0),(40,72),(0,72)};
        foreach(var profile in new[]{channel,box})
        foreach(double angle in new[]{0d,.73,-1.28})
        foreach(double factor in new[]{1d,1/25.4})
        {
            // Orthonormal frame with independent in-plane and vertical rotations.
            var u=new Point(Math.Cos(angle)*.8,Math.Sin(angle)*.8,.6);
            var v=new Point(-Math.Sin(angle),Math.Cos(angle),0);
            var w=new Point(-Math.Cos(angle)*.6,-Math.Sin(angle)*.6,.8);
            Point At(double x,double y,double z)=>origin+(u*x+v*y+w*z)*factor;
            var edges=new List<Edge>();
            foreach(int i in Enumerable.Range(0,profile.Length))
            {
                var (a,b)=profile[i];
                var (c,d)=profile[(i+1)%profile.Length];
                edges.Add(new(At(0,a,b),At(0,c,d),true));
                edges.Add(new(At(312,a,b),At(312,c,d),true));
                edges.Add(new(At(312,a,b),At(0,a,b),true));
            }
            var axis=new Pair(At(399,5,16),At(-45,5,16));
            var grips=new[]{At(0,5,16),At(156,5,16),At(312,5,16)};
            var resolved=Ends.Resolve(edges,axis,grips,out var why);
            Check(resolved.HasValue&&why==SupportOrthoNativeDimensionPolicy48.EndGrips&&
                (resolved.Value.Start-grips[2]).Length<1e-7&&
                (resolved.Value.End-grips[0]).Length<1e-7,"trimmed, reversed axis from physical grip ends");
            var solid=Ends.Resolve(edges,axis,Array.Empty<Point>(),out why);
            Check(solid.HasValue&&why==SupportOrthoNativeDimensionPolicy48.SolidEndEdge&&
                edges.Any(e=>e.Straight&&e.A==solid.Value.Start&&e.B==solid.Value.End)&&
                Math.Abs((solid.Value.End-solid.Value.Start).Length/factor-312)<1e-6,
                "one real longitudinal rail with both physical ends");
            var unguided=Ends.Resolve(edges,null,Array.Empty<Point>(),out why);
            Check(profile==channel?unguided.HasValue:unguided==null&&why=="AMBIGUOUS_SOLID_AXIS",
                "distinguish a unique channel extrusion from a box with three plausible axes");
            var foreign=new Pair(axis.Start+origin,axis.End+origin);
            var notLocal=Ends.Resolve(edges,foreign,Array.Empty<Point>(),out why);
            Check(profile==channel?notLocal.HasValue:notLocal==null&&why=="AMBIGUOUS_SOLID_AXIS",
                "a parallel line in another coordinate frame cannot select a box edge");
            var incomplete=edges.Append(new Edge(At(142,4,9),At(167,9,14),true)).ToArray();
            Check(Ends.Resolve(incomplete,axis,grips,out _)==null,"unmatched intermediate edges");
        }
    }

    internal static void Run()
    {
        SolidGeometry();
        const double x=45000,y=-16789,z=872;
        var cam=new Camera("TOP",240,255,218,187,0,0,1,0,0,0,.125,x,y,z);
        Member MemberAt(string key,double at)=>new("NATIVE:"+key,"STRUCTURE",
            x-140,y+at-7,z-7,x+140,y+at+7,z+7);
        Segment Axis(Member member,double at)=>new(member.Key,member.Kind,
            x+120,y+at,z,x-120,y+at,z,240,240);
        var members=new[]{MemberAt("A",-100),MemberAt("B",100)};
        var axes=new[]{Axis(members[0],-100),Axis(members[1],100) with {
            Provenance=SupportOrthoNativeDimensionPolicy48.SolidEndEdge}};
        foreach(bool smart in new[]{false,true})
        foreach(double twist in new[]{0d,Math.PI/2,.35})
        {
            var views=new[]{cam with {TwistRadians=twist}};
            var plan=smart?SupportOrthoSmartDimPlan46.Build(views,members,800,600,1,sources:axes).Dimensions:
                SupportOrthoReferenceDimPlan47.Build(views,members,800,600,1,axes).Dimensions;
            var qa=SupportOrthoDrawingQualityAudit61.Review(views,members,plan,null,1,!smart,sources:axes);
            Check(qa.ExpectedEndpointComponents==(twist==.35?4:2)&&
                qa.ExpectedEndpointComponents==qa.CoveredEndpointComponents,
                "separate equal-length members in both planners at twist "+twist+" smart="+smart+" "+
                string.Join(" ",qa.Issues));
            // An overall with the right value on a different member's witness
            // points cannot cover both members.
            var first=plan.First(d=>d.Provenance==SupportOrthoProjectedAxis.Provenance);
            var other=first with {Source="DETAIL:TOP/H/OVERALL",Ay=first.Ay+5,By=first.By+5,
                Ax=first.Ax+5,Bx=first.Bx+5};
            var incomplete=SupportOrthoDrawingQualityAudit61.Review(views,members,new[]{other},null,1,
                false,sources:axes);
            Check(incomplete.CoveredEndpointComponents==0&&incomplete.Issues.Any(i=>
                i.Code=="MISSING_MEMBER_LENGTH"),"independent per-member witness audit");
        }
        var same=members[1] with {MinY=members[0].MinY,MaxY=members[0].MaxY,MinZ=z+15,MaxZ=z+25};
        var coincident=new[]{axes[0],axes[0] with {Key=same.Key,Az=z+20,Bz=z+20}};
        var stacked=new[]{members[0],same};
        var shared=SupportOrthoSmartDimPlan46.Build(new[]{cam},stacked,800,600,1,sources:coincident);
        var shareAudit=SupportOrthoDrawingQualityAudit61.Review(new[]{cam},stacked,shared.Dimensions,
            shared.Skipped,1,false,sources:coincident);
        Check(shared.Dimensions.Count(d=>d.Provenance==SupportOrthoProjectedAxis.Provenance)==1&&
            shareAudit.CoveredEndpointComponents==2,"one pair covers both truly coincident projections");
        var depthError=new[]{axes[0] with {Az=z+500,Bz=z+500}};
        var rejected=SupportOrthoDrawingQualityAudit61.Review(new[]{cam},new[]{members[0]},
            shared.Dimensions,null,1,false,sources:depthError);
        Check(rejected.Issues.Any(i=>i.Code=="MEMBER_ENDPOINTS_UNVERIFIED"),
            "top view alone cannot validate endpoints in another elevation");
        var tiny=new SupportOrthoProjectedAxis((20,30),(45,30.025));
        Check(tiny.HasComponent(false,8)&&
            !new SupportOrthoProjectedAxis((20,30),(45,30+1e-12)).HasComponent(false,8),
            "a shallow, printable component is distinguishable from projection noise");
        var up=MemberAt("C",0) with {MinZ=z-100,MaxZ=z+100};
        var upright=new Segment(up.Key,up.Kind,x,y,z-100,x,y,z+100,200,200);
        var endOn=SupportOrthoDrawingQualityAudit61.Review(new[]{cam},new[]{up},
            Array.Empty<SupportOrthoDimLinearPlan41.Dimension>(),null,1,false,sources:new[]{upright});
        Check(endOn.ExpectedEndpointComponents==0&&endOn.EndOnViews.Single()=="TOP/NATIVE:C"&&
            endOn.Issues.Any(i=>i.Code=="MEMBER_END_ON_IN_ALL_SELECTED_VIEWS"),"coincident end view");
        var missing=SupportOrthoDrawingQualityAudit61.Review(new[]{cam},members,
            Array.Empty<SupportOrthoDimLinearPlan41.Dimension>(),null,1,false,
            sources:new[]{axes[0],axes[1] with {Key="XREF:OTHER"}});
        Check(missing.Issues.Any(i=>i.Code=="MEMBER_ENDPOINTS_UNVERIFIED"&&i.Description=="NATIVE:B"),
            "unresolved second member and XREF exclusion");
        var saved=new SupportOrthoDimensionUpdatePlan49.Manifest(1,"drawing","layout",1,800,600,true,
            new[]{new SupportOrthoDimensionUpdatePlan49.NativeMember("NATIVE:A","STRUCTURE")},
            new[]{new SupportOrthoDimensionUpdatePlan49.View("TOP","A1",100,100,90,90)},"");
        Check(SupportOrthoDimensionUpdatePlan49.NeedsEndpointUpgrade(saved)&&
            !SupportOrthoDimensionUpdatePlan49.NeedsEndpointUpgrade(saved with {
                NativeEndpointSchema=SupportOrthoDimensionUpdatePlan49.EndpointSchema}),"old snapshot migration");
        Console.WriteLine("Physical member endpoints, equal members at different stations, both planners and independent coverage PASS");
    }
}
