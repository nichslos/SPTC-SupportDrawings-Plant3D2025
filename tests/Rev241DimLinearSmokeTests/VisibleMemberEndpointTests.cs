using SPTC.SupportDrawings;
using Camera=SPTC.SupportDrawings.SupportOrthoMemberCalloutPlan17.Camera;
using Member=SPTC.SupportDrawings.SupportOrthoAllViewDimPlan44.Member;
using Segment=SPTC.SupportDrawings.SupportOrthoDimLinearPlan41.Segment;
using Note=SPTC.SupportDrawings.SupportOrthoMemberCalloutPlan27;
using Dim=SPTC.SupportDrawings.SupportOrthoDimLinearPlan41.Dimension;
using Rect=SPTC.SupportDrawings.SupportOrthoSmartDimPlan46.Rect;

internal static class VisibleMemberEndpointTests
{
    private static void Check(bool pass,string reason)
    {
        if(!pass)throw new Exception("Visible member endpoints: "+reason);
    }

    private static (IReadOnlyList<Dim> Dims,IReadOnlyList<string> Skips) Build(bool smart,
        Camera view,Member[] members,Segment[] sources,Rect[]? blocks=null)
    {
        if(smart)
        {
            var p=SupportOrthoSmartDimPlan46.Build(new[]{view},members,1000,700,1,
                additionalKeepouts:blocks,sources:sources);
            Check(p.Placements.Count==p.Dimensions.Count,"smart dimension/rectangle pairs");
            return (p.Dimensions,p.Skipped);
        }
        var plan=SupportOrthoReferenceDimPlan47.Build(new[]{view},members,1000,700,1,
            sources,additionalKeepouts:blocks);
        return (plan.Dimensions,plan.Skipped);
    }

    private static void SharedConstructionCorner()
    {
        // H and V spacings to one datum can cross each other's extension lines
        // at the construction corner of those same two points.
        Dim Component(bool h)=>new("TOP","source","STRUCTURE",h,10,10,40,30,
            h?25:-5,h?50:20,h?"30":"20",SupportOrthoEndpointVisibility.Locator,PaperToModelMm:1);
        var horizontal=SupportOrthoDimIntersections58.Project(Component(true),new Rect(23,53,27,55));
        var vertical=SupportOrthoDimIntersections58.Project(Component(false),new Rect(-10,18,-8,22));
        Check(SupportOrthoDimIntersections58.Conflict(horizontal,vertical)==null,"shared construction corner");
        var other=SupportOrthoDimIntersections58.Project(Component(false) with {Bx=47},vertical.Text);
        Check(SupportOrthoDimIntersections58.Conflict(horizontal,other)=="WITNESS_WITNESS_INTERSECTION",
            "one common endpoint cannot excuse a crossing with another pair");
        Check(SupportOrthoDimIntersections58.Conflict(horizontal,vertical with {Text=new Rect(9,20,11,22)})!=null,
            "text crossing a witness of the same pair is still blocked");
    }

    private static void CrowdedElevation()
    {
        // Two arms point into the camera at different levels on one post.
        var view=new Camera("FRONT",260,480,34,98,0,-1,0,0,380,0,.1);
        var views=new[]{view};
        Member Steel(string key,double y1,double y2,double z1,double z2)=>
            new("NATIVE:"+key,"STRUCTURE",-25,y1,z1,25,y2,z2);
        var post=Steel("POST",-100,0,0,760);
        var upper=Steel("UPPER",0,620,660,760);
        var lower=Steel("LOWER",0,315,305,405);
        var members=new[]{post,upper,lower};
        Segment AxisOf(Member m)
        {
            bool vertical=m==post;
            double y=(m.MinY+m.MaxY)/2,z=(m.MinZ+m.MaxZ)/2;
            double len=vertical?m.MaxZ-m.MinZ:m.MaxY-m.MinY;
            return new(m.Key,m.Kind,0,vertical?y:m.MinY,vertical?m.MinZ:z,
                0,vertical?y:m.MaxY,vertical?m.MaxZ:z,len,len);
        }
        var axes=members.Select(AxisOf).ToArray();
        var titles=NativeOrthoViewTitlePlan.BuildTitles(new[]{new NativeOrthoViewTitlePlan.Frame(
            view.Name,view.X,view.Y,view.W,view.H)},1189,841,allowSubset:true);
        var labels=members.Select(m=>new Note.Member(m.Key.Substring(7),"Channel",
            0,(m.MinY+m.MaxY)/2,(m.MinZ+m.MaxZ)/2,m!=post,m==post?null:"TOS","MODEL")).ToArray();
        var notes=Note.Build(views,titles,labels,1189,841,allowSubset:true).Callouts;
        var primary=SupportOrthoReferenceDimPlan47.Build(views,members,1189,841,1,axes,notes,titles,
            includeEndpointDetails:false);
        var before=SupportOrthoReferenceDimPlan47.CompleteEndpoints(primary,views,members,axes,
            1,1189,841,notes,titles,null,retryFullSpans:false);
        var after=SupportOrthoReferenceDimPlan47.CompleteEndpoints(primary,views,members,axes,
            1,1189,841,notes,titles,null);
        var qa=SupportOrthoDrawingQualityAudit61.Review(views,members,after.Dimensions,after.Skipped,1,sources:axes);
        Check(before.Skipped.Any(s=>s.Contains(SupportOrthoEndpointVisibility.Unplaced))&&
            after.EndpointViewsReflowed==1&&qa.ExpectedEndpointLocations==2&&qa.CoveredEndpointLocations==2&&
            qa.GeometryIntersections==0,"clear another lane for the end-on arm: "+
            $"reflow={after.EndpointViewsReflowed}, coverage={qa.CoveredEndpointLocations}/{qa.ExpectedEndpointLocations}");
        foreach(var old in before.Dimensions)
            Check(after.Dimensions.Any(d=>d.Source==old.Source&&d.Measurement==old.Measurement&&
                d.Ax==old.Ax&&d.Ay==old.Ay&&d.Bx==old.Bx&&d.By==old.By),"keep both original witnesses on "+old.Source);
    }

    internal static void Run()
    {
        SharedConstructionCorner();
        CrowdedElevation();
        var top=new Camera("TOP",320,290,150,130,0,0,1,0,0,0,1,9500,-7200,300);
        var first=new Member("NATIVE:A","STRUCTURE",9486,-7224,270,9494,-7216,330);
        var second=new Member("NATIVE:B","STRUCTURE",9526,-7194,270,9534,-7186,330);
        var pins=new[]{first,second};
        Segment Pin(Member m)=>new(m.Key,m.Kind,(m.MinX+m.MaxX)/2,(m.MinY+m.MaxY)/2,m.MinZ,
            (m.MinX+m.MaxX)/2,(m.MinY+m.MaxY)/2,m.MaxZ,m.MaxZ-m.MinZ,m.MaxZ-m.MinZ);
        var axes=pins.Select(Pin).ToArray();
        foreach(bool smart in new[]{true,false})
        foreach(double twist in new[]{0d,.27})
        {
            var cam=top with {TwistRadians=twist};
            var (dims,skips)=Build(smart,cam,pins,axes);
            var qa=SupportOrthoDrawingQualityAudit61.Review(new[]{cam},pins,dims,skips,1,!smart,sources:axes);
            var requirements=SupportOrthoEndpointVisibility.Inspect(cam,pins,axes,1).Required;
            var loc=dims.Where(d=>requirements.Any(r=>r.Matches(d))).ToArray();
            Check(qa.ExpectedEndpointComponents==0&&qa.ExpectedEndpointLocations==4&&
                qa.CoveredEndpointLocations==4&&loc.Length==2&&qa.GeometryIntersections==0,
                $"two superposed pairs in plan share two nonzero station spacings, smart={smart}, twist={twist}, expected={qa.ExpectedEndpointLocations}, covered={qa.CoveredEndpointLocations}, dimensions={loc.Length}: "+
                string.Join(", ",qa.Issues)+" "+string.Join(" ",skips));
            var a=SupportOrthoMemberCalloutPlan17.Project(cam,axes[0].Ax,axes[0].Ay,axes[0].Az);
            var b=SupportOrthoMemberCalloutPlan17.Project(cam,axes[1].Ax,axes[1].Ay,axes[1].Az);
            foreach(var d in loc)
            {
                double expected=Math.Abs(d.Horizontal?a.X-b.X:a.Y-b.Y);
                Check(Math.Abs(SupportOrthoDynamicDimension492.MeasuredMillimeters(d)-expected)<1e-6&&
                    new SupportOrthoProjectedAxis(a,b).Along(d.Horizontal).ShownBy(d,d.Horizontal),
                    "exact two native witnesses and literal 1:1 placement");
            }
            var source=requirements.First(r=>r.Matches(loc[0]));
            var bad=loc[0] with {Ax=loc[0].Ax+.5,Provenance=SupportOrthoEndpointVisibility.Locator,
                Source="DETAIL:"+cam.Name+"/"+source.AxisName+"/"+source.Role};
            var changed=SupportOrthoDrawingQualityAudit61.Review(new[]{cam},pins,new[]{bad},null,1,!smart,sources:axes);
            Check(changed.CoveredEndpointLocations==0&&changed.Issues.Any(i=>i.Code=="ENDPOINT_DATUM_MISMATCH"),
                "moved witness fails the endpoint audit");
        }

        // The primary datum is valid model geometry, but its paper station is
        // behind a reserved area. The other visible post is on the clear side.
        var near=first with {Key="NATIVE:NEAR",MinX=first.MinX+15,MaxX=first.MaxX+15};
        var far=first with {Key="NATIVE:FAR",MinX=first.MinX-34,MaxX=first.MaxX-34};
        var three=new[]{first,near,far};
        var threeAxes=three.Select(Pin).ToArray();
        var refPoint=SupportOrthoMemberCalloutPlan17.Project(top,threeAxes[1].Ax,threeAxes[1].Ay,threeAxes[1].Az);
        var reserved=new[]{new Rect(refPoint.X-2,0,refPoint.X+2,700)};
        var main=SupportOrthoReferenceDimPlan47.Build(new[]{top},three,1000,700,1,threeAxes,includeEndpointDetails:false);
        var empty=main with {Dimensions=Array.Empty<Dim>(),Skipped=Array.Empty<string>()};
        var final=SupportOrthoReferenceDimPlan47.CompleteEndpoints(empty,new[]{top},three,
            threeAxes,1,1000,700,null,null,reserved);
        var target=SupportOrthoEndpointVisibility.Inspect(top,three,threeAxes,1).Required
            .Single(r=>r.Key==first.Key&&r.Horizontal);
        var placed=final.Dimensions.Single(d=>target.Matches(d));
        Check(target.Datum==near.Key&&placed.Source.EndsWith("_TO_"+far.Key)&&
            target.Identifies(placed)&&Math.Abs(SupportOrthoDynamicDimension492.MeasuredMillimeters(placed)-34)<1e-7,
            "retry an alternate selected endpoint and record its actual datum");
        var query=SupportOrthoDrawingQualityAudit61.Review(new[]{top},three,final.Dimensions,final.Skipped,1,
            referenceStyle:false,sources:threeAxes);
        Check(!query.Issues.Any(i=>i.Code=="ENDPOINT_DATUM_MISMATCH"),"QA accepts the verified alternate datum");

        var longMember=new Member("NATIVE:C","STRUCTURE",9470,-7210,290,9680,-7185,310);
        Segment run=new(longMember.Key,longMember.Kind,9480,-7204,300,9540,-7194,300,
            Math.Sqrt(3700),Math.Sqrt(3700));
        foreach(bool smart in new[]{true,false})
        {
            // Both verified ends are inside although the member's WCS envelope exceeds this view.
            var (dims,skips)=Build(smart,top,new[]{longMember},new[]{run});
            var qa=SupportOrthoDrawingQualityAudit61.Review(new[]{top},new[]{longMember},dims,skips,1,!smart,sources:new[]{run});
            Check(qa.ExpectedEndpointComponents==2&&qa.CoveredEndpointComponents==2&&qa.ExpectedOverallAxes==0&&
                dims.All(d=>!d.Source.EndsWith("/OVERALL")),"visible endpoints survive a cropped envelope");
            if(!smart)
            {
                var completeMember=longMember with {MaxX=9543};
                var required=SupportOrthoEndpointVisibility.Inspect(top,new[]{completeMember},new[]{run},1).Required;
                var omitted=empty with {Skipped=required.Select(r=>top.Name+"/"+r.Missing).ToArray()};
                var recovered=SupportOrthoReferenceDimPlan47.CompleteEndpoints(omitted,new[]{top},new[]{completeMember},
                    new[]{run},1,1000,700,null,null,null);
                Check(required.Count==2&&required.All(r=>r.FullyInLayout&&recovered.Dimensions.Any(r.Matches))&&
                    recovered.Skipped.Count==0,"final pass recovers both previously skipped full-view components");
            }
            var partial=run with {Bx=9670,SourceLength=Math.Sqrt(36200),Millimeters=Math.Sqrt(36200)};
            var members=new[]{longMember,second,first with {Key="XREF:EXTERNAL"}};
            var src=new[]{partial,axes[1],axes[0] with {Key="XREF:EXTERNAL"}};
            var visible=Build(smart,top,members,src);
            var req=SupportOrthoEndpointVisibility.Inspect(top,members,src,1);
            Check(req.Required.Any(r=>r.Key==longMember.Key&&r.Ends=="START")&&
                req.Required.All(r=>r.Datum!="XREF:EXTERNAL")&&
                req.Required.All(r=>visible.Dims.Any(r.Matches))&&
                visible.Skips.Any(s=>s.Contains("NATIVE:C:ENDPOINT_OUTSIDE_VIEW_END")),
                "one visible endpoint is located against another selected native member, smart="+smart+
                " "+string.Join(" ",visible.Skips));
            var blocked=Build(smart,top,members,src,new[]{new Rect(0,0,1000,700)});
            var failed=SupportOrthoDrawingQualityAudit61.Review(new[]{top},members,blocked.Dims,blocked.Skips,1,!smart,sources:src);
            Check(failed.CoveredEndpointLocations==0&&failed.ExpectedEndpointLocations>0&&
                failed.Issues.Any(i=>i.Code=="MISSING_MEMBER_ENDPOINT_LOCATION"&&i.Description.Contains("NATIVE:C")),
                "blocked endpoint is identified by member, view and end");
            var alone=Build(smart,top,new[]{first},new[]{axes[0]});
            Check(alone.Dims.All(d=>d.Provenance!=SupportOrthoEndpointVisibility.Locator)&&
                alone.Skips.Any(s=>s.Contains("ENDPOINT_NO_DISTINCT_VISIBLE_DATUM")),
                "an isolated end-on point never produces a fictitious length");
        }
        Console.WriteLine("Visible endpoints, end-on locations, clipped viewports and blocked placement PASS");
    }
}
