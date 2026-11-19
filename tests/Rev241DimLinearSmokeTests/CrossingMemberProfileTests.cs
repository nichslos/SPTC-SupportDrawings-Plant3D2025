using SPTC.SupportDrawings;
using Camera=SPTC.SupportDrawings.SupportOrthoMemberCalloutPlan17.Camera;
using Member=SPTC.SupportDrawings.SupportOrthoAllViewDimPlan44.Member;
using Axis=SPTC.SupportDrawings.SupportOrthoDimLinearPlan41.Segment;
using Notes=SPTC.SupportDrawings.SupportOrthoMemberCalloutPlan27;
using Rect=SPTC.SupportDrawings.SupportOrthoSmartDimPlan46.Rect;

internal static class CrossingMemberProfileTests
{
    private static void Check(bool condition,string message)
    {
        if(!condition)throw new Exception("Crossing member profiles: "+message);
    }

    internal static void Run()
    {
        foreach(var (section,reach,height,origin,mirror) in new[]{
            (100d,628.28,766.232,0d,1), (80d,510d,640d,1400d,-1)})
        {
            double joint=origin+mirror*section;
            double outer=joint+mirror*reach;
            double z=origin/2;
            Member Channel(string name,double y1,double y2,double low,double high)=>
                new("NATIVE:"+name,"STRUCTURE",-25,Math.Min(y1,y2),low,25,Math.Max(y1,y2),high);
            var post=Channel("Post",origin,joint,z,z+height);
            var upper=Channel("Upper",joint,outer,z+height-section,z+height);
            var lower=Channel("Lower",joint,joint+mirror*reach*.55,z+height*.4,z+height*.4+section);
            var members=new[]{upper,lower,post};
            var right=new Camera("RIGHT",519,604,105,97,1,0,0,
                (origin+outer)/2,z+height/2,0,.1);
            var left=right with {Name="LEFT",X=272,DirX=-1,ViewCenterX=-right.ViewCenterX};
            var cameras=new[]{left,right};
            var titles=NativeOrthoViewTitlePlan.BuildTitles(cameras.Select(c=>
                new NativeOrthoViewTitlePlan.Frame(c.Name,c.X,c.Y,c.W,c.H)).ToArray(),1189,841,allowSubset:true);
            double midPost=(origin+joint)/2;
            var axis=new Axis(post.Key,post.Kind,0,midPost,z,0,midPost,z+height,height,height);
            var notes=Notes.Build(cameras,titles,members.Select(m=>new Notes.Member(
                m.Key.Substring("NATIVE:".Length),"Channel",0,(m.MinY+m.MaxY)/2,(m.MinZ+m.MaxZ)/2,
                m!=post,m!=post?"Top of steel":null,"MODEL")).ToArray(),1189,841,allowSubset:true).Callouts;
            SupportOrthoReferenceDimPlan47.Result Plan(Member[] source,Rect[]? obstacles=null)=>
                SupportOrthoReferenceDimPlan47.Build(cameras,source,1189,841,1,new[]{axis},notes,titles,obstacles);
            var result=Plan(members);
            var face=SupportOrthoMemberCalloutPlan17.Project(right,0,(joint+outer)/2,upper.MinZ);
            var upperDim=result.Dimensions.Single(d=>d.View=="RIGHT"&&!d.Horizontal&&
                d.Source.EndsWith("/MEMBER_"+upper.Key,StringComparison.Ordinal));
            var postDim=result.Dimensions.Single(d=>d.View=="RIGHT"&&d.Horizontal&&
                d.Source.EndsWith("/MEMBER_"+post.Key,StringComparison.Ordinal));
            Check(Math.Abs(upperDim.Ax-face.X)<1e-7&&Math.Abs(upperDim.Bx-face.X)<1e-7&&
                Math.Abs(upperDim.Ay-face.Y)<1e-7&&Math.Abs(upperDim.By-face.Y-section*right.Scale)<1e-7,
                "the upper profile uses its projected native faces and original midpoint");
            Check(new[]{upperDim,postDim}.All(d=>
                Math.Abs(SupportOrthoDynamicDimension492.MeasuredMillimeters(d)-section)<1e-6)&&
                upperDim.Provenance=="REFERENCE_PROJECTED_MEMBER_ENVELOPE_NOT_CUT_LENGTH",
                "both perpendicular profile dimensions retain their source measurement");
            Check(result.OverallDimensions==4&&result.UnintendedAcceptedWitnessIntersections==0&&
                !result.Skipped.Any(s=>s.Contains("OPTIONAL_PROFILE_NO_COMPATIBLE_LAYOUT")),
                "both profiles fit alongside the overall dimensions");
            Check(result.Dimensions.SequenceEqual(Plan(members.Reverse().ToArray()).Dimensions),
                "source selection order does not change placement");
            if(mirror==1)
            {
                var bottom=SupportOrthoMemberCalloutPlan17.Project(right,0,midPost,z);
                double halfWidth=section*right.Scale/2;
                var blocked=Plan(members,new[]{new Rect(bottom.X-halfWidth-2,bottom.Y-10,
                    bottom.X+halfWidth+2,bottom.Y-2)});
                Check(blocked.OverallDimensions==result.OverallDimensions&&
                    blocked.Dimensions.Any(d=>d.Source==postDim.Source)&&
                    blocked.UnintendedAcceptedWitnessIntersections==0,
                    "a blocked alternate lane preserves the overalls and the upright width: "+
                    string.Join(", ",blocked.Dimensions.Where(d=>d.View=="RIGHT").Select(d=>d.Source)));
            }
        }
        Console.WriteLine("Perpendicular native member profiles, mirrored geometry and blocked lanes PASS");
    }
}
