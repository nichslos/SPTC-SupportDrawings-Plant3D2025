using SPTC.SupportDrawings;
using Camera=SPTC.SupportDrawings.SupportOrthoMemberCalloutPlan17.Camera;
using Member=SPTC.SupportDrawings.SupportOrthoAllViewDimPlan44.Member;
using Segment=SPTC.SupportDrawings.SupportOrthoDimLinearPlan41.Segment;
using Dim=SPTC.SupportDrawings.SupportOrthoDimLinearPlan41.Dimension;

internal static class IntermediateMemberElevationTests
{
    private static void Check(bool ok,string reason)
    {
        if(!ok)throw new Exception("Intermediate member elevation: "+reason);
    }

    private static void TwoArmsOnOnePost()
    {
        // The horizontal length of an intermediate arm can be fully
        // dimensioned while its height is absent. Reserve both local levels
        // even when the upper arm and leaders compete for the same bay.
        var upright=new Member("NATIVE:UPRIGHT","STRUCTURE",-25,0,0,25,100,760);
        var high=new Member("NATIVE:HIGH","STRUCTURE",-25,100,660,25,720,760);
        var low=new Member("NATIVE:LOW","STRUCTURE",-25,100,302,25,404,402);
        var members=new[]{upright,high,low};
        var axes=new[]{new Segment(upright.Key,upright.Kind,0,50,0,0,50,760,760,760),
            new Segment(high.Key,high.Kind,0,100,710,0,720,710,620,620),
            new Segment(low.Key,low.Kind,0,100,352,0,404,352,304,304)};
        var views=new[]{new Camera("LEFT",220,304,105,97,-1,0,0,0,0,0,.1,0,360,380),
            new Camera("RIGHT",460,304,105,97,1,0,0,0,0,0,.1,0,360,380)};
        var titles=NativeOrthoViewTitlePlan.BuildTitles(views.Select(v=>
            new NativeOrthoViewTitlePlan.Frame(v.Name,v.X,v.Y,v.W,v.H)).ToArray(),1000,640,allowSubset:true);
        var labels=members.Select(m=>new SupportOrthoMemberCalloutPlan27.Member(m.Key,"Channel",
            0,(m.MinY+m.MaxY)/2,(m.MinZ+m.MaxZ)/2,m!=upright,m==upright?null:"TOS","MODEL")).ToArray();
        var notes=SupportOrthoMemberCalloutPlan27.Build(views,titles,labels,1000,640,allowSubset:true).Callouts;
        SupportOrthoReferenceDimPlan47.Result Plan(IReadOnlyList<SupportOrthoMemberCalloutPlan27.Callout> n,
            IReadOnlyDictionary<string,SupportOrthoReferenceDimPlan47.PlacementPreference>? preferences=null)=>
            SupportOrthoReferenceDimPlan47.Build(views,members,1000,640,1,axes,n,titles,lanePreferences:preferences);
        var coordinated=SupportOrthoLeaderDimensionLayout64.Coordinate(views,titles,notes,1000,640,n=>Plan(n));
        var result=SupportOrthoDimensionReflow.Improve(views,Plan(coordinated.Callouts),
            p=>Plan(coordinated.Callouts,p)).Plan;
        var qa=SupportOrthoDrawingQualityAudit61.Review(views,members,result.Dimensions,result.Skipped,1,sources:axes);
        Check(qa.ExpectedMemberElevations==4&&qa.CoveredMemberElevations==4&&qa.ExpectedEndpointComponents==6&&
            qa.CoveredEndpointComponents==6&&qa.PresentOverallAxes==4&&qa.GeometryIntersections==0,
            "two channels sharing a post with dimensions and callouts on both elevations: "+string.Join(", ",qa.Issues));
    }

    private static void NativeFaceWitnesses()
    {
        var cam=new Camera("FRONT",220,230,180,170,0,-1,0,0,0,0,.1);
        var post=new Member("NATIVE:P","STRUCTURE",-25,-15,-180,25,15,390);
        var arm=new Member("NATIVE:A","STRUCTURE",25,-16,38,270,18,96);
        var axes=new[]{new Segment(post.Key,post.Kind,0,0,-180,0,0,390,570,570),
            new Segment(arm.Key,arm.Kind,40,0,61,245,0,61,205,205)};
        var edges=new[]{
            new SupportOrthoNativeEdgeGeometry59.Edge(arm.Key,arm.Kind,31,-4,38,94,-4,38),
            new SupportOrthoNativeEdgeGeometry59.Edge(arm.Key,arm.Kind,172,9,96,252,9,96),
            new SupportOrthoNativeEdgeGeometry59.Edge(post.Key,post.Kind,-25,0,-180,25,0,-180)};
        var visible=new[]{(post,new SupportOrthoSmartDimPlan46.Rect(217.5,212,222.5,269)),
            (arm,new SupportOrthoSmartDimPlan46.Rect(222.5,233.8,247,239.6))};
        var projected=SupportOrthoProjectedAxis.InView(cam,axes,visible);
        var section=SupportOrthoMemberElevation.Find(cam,visible,projected,1,edges).Single(m=>m.Depth);
        var witness=section.Witnesses().First();
        Check(Math.Abs(witness.FirstWitness-229.4)<1e-7&&Math.Abs(witness.LastWitness-237.2)<1e-7,
            "use actual disjoint flange edges on a rolled or trimmed section");
        var blocked=new[]{new SupportOrthoSmartDimPlan46.Rect(0,0,900,650)};
        var plan=SupportOrthoReferenceDimPlan47.Build(new[]{cam},new[]{post,arm},900,650,1,axes,
            additionalKeepouts:blocked,nativeEdges:edges);
        var audit=SupportOrthoDrawingQualityAudit61.Review(new[]{cam},new[]{post,arm},plan.Dimensions,
            plan.Skipped,1,nativeEdges:edges,sources:axes);
        Check(audit.CoveredMemberElevations==0&&audit.ExpectedMemberElevations==2&&
            plan.Skipped.Count(s=>s.Contains(SupportOrthoMemberElevation.Unplaced))==2,
            "a blocked view reports the missing elevation and depth separately");
    }

    internal static void Run()
    {
        // Geometry is translated off the WCS origin. Rotate the assembly
        // in plan, mirror the view, and reverse the native endpoint order.
        const double x=19023,y=-6438,z=2501,depth=84;
        foreach(double bearing in new[]{0d,.54})
        foreach(double raised in new[]{0d,37.8})
        {
            double dx=Math.Cos(bearing),dy=Math.Sin(bearing);
            double bottom=z+190.75+raised,top=bottom+depth;
            var post=new Member("NATIVE:POST","STRUCTURE",x-32,y-32,z,x+32,y+32,z+720);
            var arm=new Member("NATIVE:ARM","STRUCTURE",x+32,y-14,bottom,
                x+32+310*dx,y+14+310*dy,top);
            var members=new[]{post,arm,post with {Key="XREF:OTHER",MinZ=z-440}};
            var axes=new[]{new Segment(post.Key,post.Kind,x,y,z,x,y,z+720,720,720),
                new Segment(arm.Key,arm.Kind,x+32+310*dx,y+310*dy,bottom+depth/2,
                    x+32,y,bottom+depth/2,310,310)};
            foreach(double twist in new[]{0d,Math.PI/2,Math.PI})
            foreach(int side in new[]{1,-1})
            {
                var camera=new Camera(side>0?"FRONT":"BACK",260,245,205,208,
                    side*dy,-side*dx,0,0,0,twist,.15,x,y,z+350);
                var views=new[]{camera};
                foreach(bool smart in new[]{false,true})
                {
                    SupportOrthoReferenceDimPlan47.Result Reference(
                        IReadOnlyDictionary<string,SupportOrthoReferenceDimPlan47.PlacementPreference>? lanes=null)=>
                        SupportOrthoReferenceDimPlan47.Build(views,members,900,650,1,axes,lanePreferences:lanes);
                    var dims=smart?SupportOrthoSmartDimPlan46.Build(views,members,900,650,1,sources:axes).Dimensions:
                        SupportOrthoDimensionReflow.Improve(views,Reference(),p=>Reference(p)).Plan.Dimensions;
                    var qa=SupportOrthoDrawingQualityAudit61.Review(views,members,dims,null,1,!smart,sources:axes);
                    string context=$"bearing={bearing} shift={raised} side={side} twist={twist} smart={smart}";
                    Check(qa.ExpectedMemberElevations==2&&qa.CoveredMemberElevations==2,context+" "+string.Join(", ",qa.Issues));
                    Check(qa.ExpectedEndpointComponents==qa.CoveredEndpointComponents,context+" axial coverage "+string.Join(", ",qa.Issues));
                    bool horizontal=twist==Math.PI/2;
                    var offset=dims.Single(d=>d.Source.EndsWith("/BASE_TO_UNDERSIDE_"+arm.Key));
                    double Measured(Dim d)=>SupportOrthoDynamicDimension492.MeasuredMillimeters(d);
                    Check(offset.Horizontal==horizontal&&Math.Abs(Measured(offset)-(bottom-z))<1e-6&&
                        dims.Any(d=>d.Horizontal==horizontal&&Math.Abs(Measured(d)-depth)<1e-6),context+" dimensions");
                    var wrong=offset with {Source=offset.Source.Replace(arm.Key,post.Key)};
                    var partial=SupportOrthoDrawingQualityAudit61.Review(views,members,new[]{wrong},null,1,!smart,sources:axes);
                    Check(partial.CoveredMemberElevations==0&&partial.Issues.Any(i=>i.Code=="MISSING_MEMBER_ELEVATION")
                        &&partial.Issues.Any(i=>i.Code=="MISSING_MEMBER_SECTION_DEPTH"),context+" source and face audit");
                }
            }
            // Axial rise remains the REV94 requirement when a member is
            // inclined. An end-on or plan view cannot show true level offsets.
            var front=new Camera("FRONT",260,245,205,208,0,-1,0,0,0,0,.15,x,y,z+350);
            foreach(var view in new[]{front with {Name="TOP",DirY=0,DirZ=1},front with {TwistRadians=.32}})
            {
                var qa=SupportOrthoDrawingQualityAudit61.Review(new[]{view},members,Array.Empty<Dim>(),null,1,false,sources:axes);
                Check(qa.ExpectedMemberElevations==0,"true elevation must be measurable on the dimension axis");
            }
            var inclined=new[]{axes[0],axes[1] with {Bz=axes[1].Bz+15}};
            var sloped=SupportOrthoDrawingQualityAudit61.Review(new[]{front},members,Array.Empty<Dim>(),null,1,false,sources:inclined);
            Check(sloped.ExpectedMemberElevations==0&&sloped.ExpectedEndpointComponents>=3,"retain sloping endpoint spans");
        }
        TwoArmsOnOnePost();
        NativeFaceWitnesses();
        Console.WriteLine("Intermediate member base-to-underside and section depth in both planners PASS");
    }
}
