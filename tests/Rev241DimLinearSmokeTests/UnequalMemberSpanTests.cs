using SPTC.SupportDrawings;
using Camera=SPTC.SupportDrawings.SupportOrthoMemberCalloutPlan17.Camera;
using Member=SPTC.SupportDrawings.SupportOrthoAllViewDimPlan44.Member;
using Segment=SPTC.SupportDrawings.SupportOrthoDimLinearPlan41.Segment;
using Callout=SPTC.SupportDrawings.SupportOrthoMemberCalloutPlan27.Callout;

internal static class UnequalMemberSpanTests
{
    private static void Require(bool value,string message)
    {
        if(!value)throw new Exception("Unequal member spans: "+message);
    }

    internal static void Run()
    {
        // Two posts on a common base, joined by a low beam. On this
        // camera all three callout leaders pass through the right post.
        const double width=100,gap=828.28,tall=502.787;
        foreach(var (view,mirror,height,lift) in new[]{
            ("RIGHT",1,285.672,0),("LEFT",1,285.672,0),("LEFT",-1,319.125,40)})
        {
            double a=-mirror*(gap+width)/2,b=-a;
            var camera=new Camera(view,538,557,142.163,63.351,view=="RIGHT"?1:-1,0,0,
                0,tall/2,0,.1,81000,-12000,3500);
            var members=new List<Member>();
            var axes=new List<Segment>();
            var labels=new List<SupportOrthoMemberCalloutPlan27.Member>();
            void Add(string id,double ya,double za,double yb,double zb,
                double yPad,double zPad,bool horizontal=false)
            {
                double x=camera.TargetX,y=camera.TargetY,z=camera.TargetZ;
                var member=new Member("NATIVE:"+id,"STRUCTURE",x-25,
                    y+Math.Min(ya,yb)-yPad,z+Math.Min(za,zb)-zPad,x+25,
                    y+Math.Max(ya,yb)+yPad,z+Math.Max(za,zb)+zPad);
                members.Add(member);
                double length=Math.Abs(yb-ya)+Math.Abs(zb-za);
                axes.Add(new(member.Key,member.Kind,x,y+yb,z+zb,x,y+ya,z+za,length,length));
                labels.Add(new(id,"Channel",x,y+(ya+yb)/2,z+(za+zb)/2,
                    horizontal,horizontal?"Top of steel":null,"MODEL"));
            }
            Add("A",a,0,a,tall,width/2,0);
            Add("B",-mirror*gap/2,width/2,mirror*gap/2,width/2,0,width/2,true);
            Add("C",b,lift,b,lift+height,width/2,0);
            var title=new NativeOrthoViewTitlePlan.Title(view,view+" VIEW",
                camera.X,camera.Y-camera.H/2-4,camera.W,3);
            var cameras=new[]{camera};
            var titles=new[]{title};
            var notes=SupportOrthoMemberCalloutPlan27.Build(cameras,titles,labels,
                1189,841,allowSubset:true).Callouts;
            Require(notes.Count==3,"three native leaders");
            SupportOrthoReferenceDimPlan47.Result Plan(IReadOnlyList<Callout> n) =>
                SupportOrthoReferenceDimPlan47.Build(cameras,members,1189,841,1,axes,n,titles);
            if(view=="RIGHT")
            {
                var fixedLayout=Plan(notes);
                var qa=SupportOrthoDrawingQualityAudit61.Review(cameras,members,
                    fixedLayout.Dimensions,fixedLayout.Skipped,1);
                Require(qa.Issues.Any(i=>i.Code=="MISSING_MEMBER_LENGTH"&&
                    i.View==view&&i.Description.Contains("NATIVE:C")),
                    "identify the individual post when fixed leaders block it");
            }
            var coordinated=SupportOrthoLeaderDimensionLayout64.Coordinate(
                cameras,titles,notes,1189,841,Plan,20);
            var result=Plan(coordinated.Callouts);
            var dim=result.Dimensions.SingleOrDefault(d=>!d.Horizontal&&
                d.Source.EndsWith("/AXIS_NATIVE:C",StringComparison.Ordinal));
            Require(dim!=null,view+" missing shorter post height, "+
                string.Join(" ",result.Dimensions.Select(d=>d.Source+"="+d.Measurement))+" "+
                string.Join(" ",result.Skipped));
            var low=SupportOrthoMemberCalloutPlan17.Project(camera,camera.TargetX,
                camera.TargetY+b,camera.TargetZ+lift);
            var high=SupportOrthoMemberCalloutPlan17.Project(camera,camera.TargetX,
                camera.TargetY+b,camera.TargetZ+lift+height);
            Require(Math.Abs(dim!.Ax-low.X)<1e-7&&Math.Abs(dim.Ay-low.Y)<1e-7&&
                Math.Abs(dim.Bx-high.X)<1e-7&&Math.Abs(dim.By-high.Y)<1e-7&&
                Math.Abs(SupportOrthoDynamicDimension492.MeasuredMillimeters(dim)-height)<1e-7,
                view+" native witnesses at both ends of the short post");
            Require(result.OverallDimensions==2&&result.UnintendedAcceptedWitnessIntersections==0,
                view+" protected overall dimensions and no dimension crossings");
            Require((dim.LineX-camera.X)*(low.X-camera.X)>0&&Math.Abs(dim.LineX-low.X)<camera.W/3,
                view+" dimension stays near the shorter post");
            Require(SupportOrthoDrawingQualityAudit61.Review(cameras,members,
                result.Dimensions,result.Skipped,1).Status=="PASS",
                view+" final layout accounts for the structural lengths");
            if(view=="RIGHT")
            {
                foreach(var measurement in new[]{"502.787","1028.28","828.28","100"})
                    Require(result.Dimensions.Count(d=>d.Measurement==measurement)==1,
                        "preserve the existing dimensions without duplicating "+measurement);
                var rotated=camera with {W=camera.H,H=camera.W,TwistRadians=Math.PI/2,
                    ViewCenterX=-tall/2,ViewCenterY=0};
                var horizontal=SupportOrthoReferenceDimPlan47.Build(new[]{rotated},members,
                    1189,841,1,axes);
                Require(horizontal.Dimensions.Any(d=>d.Horizontal&&
                    Math.Abs(SupportOrthoDynamicDimension492.MeasuredMillimeters(d)-height)<1e-7),
                    "the same member has a horizontal projected length after a quarter turn");
                var unverified=SupportOrthoReferenceDimPlan47.Build(cameras,members,1189,841,1,
                    axes.Select(s=>s.Key=="NATIVE:C"?s with {Provenance="BOUNDS_ONLY"}:s).ToArray());
                Require(unverified.Dimensions.All(d=>!d.Source.EndsWith("/AXIS_NATIVE:C")),
                    "bounds alone cannot supply member endpoints");
            }
        }
        Console.WriteLine("Unequal native member spans and callout clearance PASS");
    }
}
