using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev71GeometryAnnotationTests
{
    private static void Need(bool yes,string reason)
    {
        if(!yes)throw new Exception("REV71 dynamic annotation: "+reason);
    }

    internal static void Run()
    {
        string[] names={"TOP","BOTTOM","LEFT","RIGHT","FRONT","BACK",
            "NW","NE","SW","SE"};
        // The ten native PAPER FRAMES and 3x4 packing are from the
        // 12-selected-object TEST5 diagnostic. This is a regression
        // FIXTURE, never a production rule, branch or camera preset.
        var centers=new[]{
            (495.967,488.993,139.579,143.468),
            (838.611,488.993,139.579,143.468),
            (155.268,287.013,143.468,101.907),
            (497.912,287.013,143.468,101.907),
            (838.611,287.013,139.579,101.907),
            (153.323,161.106,139.579,101.907),
            (183.606,684.107,200.144,198.760),
            (526.250,684.107,200.144,198.760),
            (868.894,684.107,200.144,198.760),
            (183.606,461.347,200.144,198.760)
        };
        var frames=names.Select((n,i)=>
            new NativeOrthoViewTitlePlan.Frame(
                n,centers[i].Item1,centers[i].Item2,
                centers[i].Item3,centers[i].Item4)).ToArray();
        var titles=NativeOrthoViewTitlePlan.BuildTitles(
            frames,1189,841,bypassTitleBlock:true);
        var cameras=frames.Select(f=>
            new SupportOrthoMemberCalloutPlan17.Camera(
                f.Name,f.X,f.Y,f.W,f.H,
                0,0,1,0,0,0,.1)).ToArray();

        var mandatory=Enumerable.Range(0,8).Select(i=>
            new SupportOrthoMemberCalloutPlan27.Member(
                "MEMBER-"+i,"Channel size from Plant metadata",
                i*35,100-i*29,0,i%2==0,
                i%2==0?"+502.7872 mm":null,
                i%2==0?"GEOMETRIC_TOP_APPROX":"NOT_HORIZONTAL"))
            .ToArray();
        var xrefs=Enumerable.Range(0,2).Select(i=>
            new SupportOrthoMemberCalloutPlan27.Member(
                "XREF-"+i,"XREF",i*40,15,0,false,null,
                "NOT_HORIZONTAL",XrefDrawingName:"SUPPORT-"+i+".dwg"))
            .ToArray();
        var all=mandatory.Concat(xrefs).ToArray();
        double required=SupportOrthoAnnotationCapacity71.RequiredHeight(
            all,1189,841);
        double expected=mandatory.Sum(m=>m.HasTos?7.0:3.5)+
            (mandatory.Length-1)*2.2;
        Need(Math.Abs(required-expected)<1e-6,
            "mandatory 2-line text and row gaps are source-derived");

        // Original globally-nearest-row method incorrectly used the
        // small TOP-to-SE Y separation although their X columns
        // were disjoint. New free bands are computed from actual 2D
        // frame/title rectangles across ALL views, not view names.
        var top=frames[0];
        double globalGap=frames.Where(f=>
                Math.Abs(f.Y-top.Y)>1e-5)
            .Min(f=>Math.Abs(f.Y-top.Y));
        double legacyHalf=Math.Min(60,globalGap/2-8);
        Need(legacyHalf*2<required,
            "reproduce the historic spurious TOP height rejection");
        Need(SupportOrthoAnnotationCapacity71.TryPreflight(
            frames.Select(f=>new NativeOrthoTenViewPlan.Placement(
                f.Name,f.X,f.Y,f.W,f.H)).ToArray(),
            titles,1189,841,true,required,out string reason),
            "real geometry allows mandatory labels on all 10 views: "+reason);
        var result=SupportOrthoMemberCalloutPlan27.Build(
            cameras,titles,all,1189,841,bypassTitleBlock:true);
        Need(result.Callouts.Count(x=>!x.IsXrefMember)==
            mandatory.Length*cameras.Length,
            "all mandatory source steel/TOS callouts recovered in crowded grid");
        Need(result.ViewWithoutLabels.Count==0&&
            !result.Skipped.Any(x=>x.Contains("NOT_ENOUGH_LABEL_HEIGHT")),
            "TOP/BOTTOM/SE and every other camera have a real free label band");
        Need(result.Callouts.Count(x=>x.HasTos&&
            !x.IsXrefMember)==4*cameras.Length,
            "all required TOS second lines recovered without font shrinking");
        foreach(var a in result.Callouts)
        {
            var rect=new SupportOrthoAnnotationCapacity71.Rect(
                a.LabelX,a.LabelTop-a.LabelHeight,
                a.LabelX+a.LabelWidth,a.LabelTop);
            Need(SupportOrthoAnnotationCapacity71.IsFree(
                    frames,titles,Array.Empty<
                        SupportOrthoAnnotationCapacity71.Rect>(),
                    rect.L,rect.R-rect.L,rect.B,rect.T),
                "every label avoids ALL viewports/titles");
        }
        var notes=result.Callouts.ToArray();
        for(int i=0;i<notes.Length;i++)
          for(int j=i+1;j<notes.Length;j++)
            Need(!SupportOrthoAnnotationCapacity71.Overlaps(
                notes[i].LabelX,
                notes[i].LabelTop-notes[i].LabelHeight,
                notes[i].LabelX+notes[i].LabelWidth,
                notes[i].LabelTop,
                notes[j].LabelX,
                notes[j].LabelTop-notes[j].LabelHeight,
                notes[j].LabelX+notes[j].LabelWidth,
                notes[j].LabelTop),
                "different view labels never intersect on sheet");
        // Explicit negative test: no label capacity must still fail
        // BEFORE a native Ortho DWG can be registered.
        Need(!SupportOrthoAnnotationCapacity71.TryPreflight(
            frames.Select(f=>new NativeOrthoTenViewPlan.Placement(
                f.Name,f.X,f.Y,f.W,f.H)).ToArray(),
            titles,1189,841,true,required*100,out _),
            "physically impossible member annotation count must fail closed");
        Console.WriteLine(
            "REV71 geometry bands: 12-object TEST5 ten-view frame fixture, "+
            "80/80 mandatory native member labels, 40/40 TOS lines, "+
            "no viewport/title/label overlaps, no hard-coded view rules PASS");
    }
}
