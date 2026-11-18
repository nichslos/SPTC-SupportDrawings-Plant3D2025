using System;
using System.Linq;
using SPTC.SupportDrawings;
using Camera=SPTC.SupportDrawings.SupportOrthoMemberCalloutPlan17.Camera;
using Member=SPTC.SupportDrawings.SupportOrthoAllViewDimPlan44.Member;
using Segment=SPTC.SupportDrawings.SupportOrthoDimLinearPlan41.Segment;
using Dimension=SPTC.SupportDrawings.SupportOrthoDimLinearPlan41.Dimension;

internal static class Rev89MemberProjectionTests
{
    private static void Check(bool ok,string message)
    {
        if(!ok)throw new Exception("REV89: "+message);
    }

    private static bool Near(double a,double b) => Math.Abs(a-b)<1e-7;

    private static bool Anchored(Dimension d,(double X,double Y) a,(double X,double Y) b)
    {
        if((d.Horizontal?a.X:a.Y)>(d.Horizontal?b.X:b.Y))(a,b)=(b,a);
        return Near(d.Ax,a.X)&&Near(d.Ay,a.Y)&&Near(d.Bx,b.X)&&Near(d.By,b.Y);
    }

    internal static void Run()
    {
        const double x=431208.5,y=-2843961.25,z=37214.75;
        var member=new Member("NATIVE:27","STRUCTURE",
            x-155,y-115,z-85,x+155,y+115,z+85);
        double length=Math.Sqrt(260*260+180*180+120*120);
        var brace=new Segment(member.Key,member.Kind,x-130,y+90,z-60,
            x+130,y-90,z+60,length,length);
        // Independently specified display coordinates of the start relative to
        // the target for the six principal cameras. End is its reflection.
        var views=new[]{
            ("TOP",0,0,1,-130,90),("BOTTOM",0,0,-1,130,90),
            ("FRONT",0,-1,0,-130,-60),("BACK",0,1,0,130,-60),
            ("RIGHT",1,0,0,90,-60),("LEFT",-1,0,0,-90,-60)};
        foreach(var (name,nx,ny,nz,u,v) in views)
        foreach(double twist in new[]{0d,.39,-.64,Math.PI/2})
        {
            var cam=new Camera(name,230,295,180,145,nx,ny,nz,
                17,-23,twist,.12,x,y,z);
            (double X,double Y) Expected(double du,double dv) =>
                (cam.X+cam.Scale*(du*Math.Cos(twist)-dv*Math.Sin(twist)-17),
                 cam.Y+cam.Scale*(du*Math.Sin(twist)+dv*Math.Cos(twist)+23));
            var a=Expected(u,v);
            var b=Expected(-u,-v);
            var p=SupportOrthoMemberCalloutPlan17.Project(cam,brace.Ax,brace.Ay,brace.Az);
            Check(Near(p.X,a.X)&&Near(p.Y,a.Y),name+" target, pan and twist");
            // Reverse the real member endpoints to exercise negative directions.
            var reversed=brace with {Ax=brace.Bx,Ay=brace.By,Az=brace.Bz,
                Bx=brace.Ax,By=brace.Ay,Bz=brace.Az,Millimeters=length*25.4};
            var smart=SupportOrthoSmartDimPlan46.Build(new[]{cam},new[]{member},
                700,600,25.4,sources:new[]{reversed});
            Check(smart.Dimensions.Count==2,name+" two visible member projections");
            foreach(var dim in smart.Dimensions)
            {
                double mm=Math.Abs(dim.Horizontal?b.X-a.X:b.Y-a.Y)*25.4/cam.Scale;
                Check(Anchored(dim,a,b)&&dim.Provenance==SupportOrthoProjectedAxis.Provenance&&
                    Near(SupportOrthoDynamicDimension492.MeasuredMillimeters(dim),mm)&&
                    SupportOrthoDynamicDimension492.MatchesDiagnostic(dim),name+" endpoint witnesses and units");
            }
            var detail=SupportOrthoReferenceDimPlan47.Build(new[]{cam},new[]{member},
                700,600,1,new[]{brace});
            Check(detail.OverallDimensions==2&&detail.UnintendedAcceptedWitnessIntersections==0,
                name+" safe assembly dimensions");
            foreach(bool horizontal in new[]{true,false})
                Check(detail.Dimensions.Any(d=>d.Horizontal==horizontal&&Anchored(d,a,b)),
                    name+" reference detail at both real endpoints under twist "+twist);
            Check(detail.Dimensions.All(SupportOrthoDynamicDimension492.MatchesDiagnostic),
                name+" dynamic dimension text");
        }

        var top=new Camera("TOP",230,295,180,145,0,0,1,0,0,0,.12,x,y,z);
        var adjusted=brace with {Ax=x+40,Ay=y-75,Bx=x-110,By=y-25,Bz=z-60};
        double adjustedLength=Math.Sqrt(150*150+50*50);
        adjusted=adjusted with {SourceLength=adjustedLength,Millimeters=adjustedLength};
        var changed=SupportOrthoSmartDimPlan46.Build(new[]{top},new[]{member},
            700,600,1,sources:new[]{adjusted});
        Check(changed.Dimensions.Count==2&&changed.Dimensions.All(d=>
            Anchored(d,(top.X+40*.12,top.Y-75*.12),(top.X-110*.12,top.Y-25*.12))&&
            Near(SupportOrthoDynamicDimension492.MeasuredMillimeters(d),d.Horizontal?150:50)),
            "rotating and resizing a member inside the same envelope changes both dimensions");
        var baseline=SupportOrthoDimensionUpdatePlan49.ViewPoseSignature(new[]{top},700,600);
        foreach(var moved in new[]{top with {TargetX=x+2},top with {TargetY=y-3},
            top with {TargetZ=z+4},top with {TwistRadians=.3}})
            Check(baseline!=SupportOrthoDimensionUpdatePlan49.ViewPoseSignature(
                new[]{moved},700,600),"changed viewport invalidates the saved dimension plan");
        var bad=brace with {Bx=member.MaxX+5000};
        var fallback=SupportOrthoSmartDimPlan46.Build(new[]{top},new[]{member},
            700,600,1,sources:new[]{bad,brace with {Key="XREF_MEMBER:27"}});
        Check(fallback.Dimensions.Count==2&&fallback.Dimensions.All(d=>
            d.Provenance=="PROJECTED_OVERALL_VIEW_ENVELOPE"),"partial and external axes excluded");

        // A slender skewed brace at a beam end used to create a false small
        // profile and terminal remainder from the brace's entire X envelope.
        var beam=new Member("NATIVE:31","STRUCTURE",x-155,y-115,z-10,x+1045,y-90,z+10);
        var assembly=SupportOrthoReferenceDimPlan47.Build(new[]{top with {ViewCenterX=445}},new[]{beam,member},
            700,600,1,new[]{brace});
        Check(assembly.Dimensions.Any(d=>d.Horizontal&&d.Source.EndsWith("/OVERALL")&&
            Near(SupportOrthoDynamicDimension492.MeasuredMillimeters(d),1200))&&
            assembly.Dimensions.All(d=>!d.Source.Contains("MEMBER_"+member.Key)&&
            !d.Source.Contains("TERMINAL_REMAINDER_"+member.Key)),
            "oblique brace cannot be classified as a terminal profile width");
        var registered=new SupportOrthoDimensionUpdatePlan49.Manifest(
            SupportOrthoDimensionUpdatePlan49.ManifestVersion,"support.dwg","Sheet",1,700,600,true,
            new[]{new SupportOrthoDimensionUpdatePlan49.NativeMember(member.Key,member.Kind),
                new SupportOrthoDimensionUpdatePlan49.NativeMember("NATIVE:32","PLATE")},
            new[]{new SupportOrthoDimensionUpdatePlan49.View("TOP","45",230,295,180,145)},"");
        SupportOrthoDimensionUpdatePlan49.Validate(registered);
        Console.WriteLine("REV89 translated structural members, six cameras, signed twists, reversed endpoints, model units and refresh PASS");
    }
}
