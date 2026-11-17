using System;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev251PdfReferenceLayoutTests
{
    internal static void Run()
    {
        // All six orthographic views in the user's manually positioned
        // PDF place H dimensions ABOVE the member and V dimensions LEFT.
        // Annotation leaders, BOP and TOS occupy the right-hand side;
        // view titles occupy the area below the member.
        foreach(string view in new[]{
            "TOP","BOTTOM","LEFT","RIGHT","FRONT","BACK"})
        {
            Check(SupportOrthoReferenceLayout51.UsesReferenceStyle(view),
                view+" must use the reference drafting convention");
            Check(SupportOrthoReferenceLayout51.PreferredSide(view,true)==1,
                view+" horizontal dims should be above the structure");
            Check(SupportOrthoReferenceLayout51.PreferredSide(view,false)==-1,
                view+" vertical dims should be to the left");
        }
        Check(!SupportOrthoReferenceLayout51.UsesReferenceStyle("NW"),
            "isometric projections must not gain false H/V dimensions");
        var lanes=SupportOrthoReferenceLayout51.LaneOffsets().Take(6).ToArray();
        Check(lanes.Length==6&&lanes[0]>0&&
            lanes.Zip(lanes.Skip(1)).All(x=>x.Second>x.First),
            "outer overall dimension lines must stack farther from member");
        Check(SupportOrthoReferenceLayout51
            .OverallSeparation()>SupportOrthoReferenceLayout51
            .ChainOffset()*.9,
            "overall dimension has clearance from local chain");
        Check(!SupportOrthoReferenceLayout51.ShortTextBefore(0,0)&&
            SupportOrthoReferenceLayout51.ShortTextBefore(0,1),
            "50 mm labels prefer right of their short witness span, then reverse");
        // Geometry is NOT a prescribed number in the layout policy.
        // Width is measured from actual native projected endpoints, even
        // if it changes from 50 to 75 mm on a different channel.
        var top=new SupportOrthoMemberCalloutPlan17.Camera(
            "TOP",180,650,150,160,0,0,1,
            25,250,0,1.0/20.0);
        var fifty=new SupportOrthoAllViewDimPlan44.Member(
            "NATIVE:CHANNEL_WIDTH","STRUCTURE",
            0,0,0,50,500,100);
        var result=SupportOrthoReferenceDimPlan47.Build(
            new[]{top},new[]{fifty},594,841,1);
        var h=result.Dimensions.Single(d=>d.Horizontal);
        var v=result.Dimensions.Single(d=>!d.Horizontal);
        Check(h.Measurement=="50"&&
            SupportOrthoDynamicDimension492.MatchesDiagnostic(h),
            "a true 50 mm projection is measured, never inserted as static text");
        Check(h.LineY>h.Ay&&h.LineY>h.By,
            "short horizontal dimension is placed above native member");
        Check(v.LineX<v.Ax&&v.LineX<v.Bx,
            "overall vertical dimension is placed to left of member");
        Check(h.PaperTextX.HasValue&&h.PaperTextX.Value>h.Bx,
            "50 mm text is positioned outside arrows toward the right");
        var seventyFive=fifty with {MaxX=75};
        var changed=SupportOrthoReferenceDimPlan47.Build(
            new[]{top},new[]{seventyFive},594,841,1);
        Check(changed.Dimensions.Any(d=>d.Horizontal&&
            d.Measurement=="75"),
            "modified structural geometry produces 75, not hard-coded 50");
        Check(changed.Dimensions.All(d=>
            SupportOrthoDynamicDimension492.MatchesDiagnostic(d)),
            "all generated dimensions use native source measurement scale");
        Console.WriteLine(
            "REV22.51 PDF reference six-view above/left lanes and short text PASS");
    }
    private static void Check(bool yes,string label)
    {
        if(!yes)throw new Exception("REV22.51: "+label);
    }
}
