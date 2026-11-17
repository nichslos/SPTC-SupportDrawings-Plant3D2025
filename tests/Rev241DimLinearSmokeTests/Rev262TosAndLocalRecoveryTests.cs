using System;
using System.Collections.Generic;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev262TosAndLocalRecoveryTests
{
    private static void Need(bool ok,string why)
    {
        if(!ok)throw new Exception("REV22.62: "+why);
    }

    private static SupportOrthoMemberCalloutPlan27.Callout C(
        string view,string handle,string text,bool second,
        bool pipe=false) =>
        new(view,handle,text,second,10,10,12,20,30,55,
            second?5.4:2.7,IsPipe:pipe);

    internal static void Run()
    {
        var source=new[]{
            new SupportOrthoMemberCalloutPlan27.Member(
                "955","Sloping channel 100x50",7785,764,3406,
                false,null,"NOT_HORIZONTAL"),
            new SupportOrthoMemberCalloutPlan27.Member(
                "956","Sloping channel 100x50",7785,764,3707,
                true,"+3757.8164 mm","GEOMETRIC_TOP_APPROX"),
            new SupportOrthoMemberCalloutPlan27.Member(
                "957","100A-TEST-001",7785,489,3814,
                false,null,"NOT_STEEL",
                "BOP EL. +3757.8164 mm","EXPLICIT_BOTTOM_OF_PIPE")
        };
        string[] selected={"TOP","BOTTOM","LEFT","RIGHT",
            "FRONT","BACK","NW","NE","SW","SE"};
        var displayed=selected.SelectMany(view=>new[]{
            C(view,"955","[955] Sloping channel 100x50",false),
            C(view,"956",
                "[956] Sloping channel 100x50\nTOS ~ +3757.8164 mm",
                true),
            C(view,"957",
                "100A-TEST-001\nBOP EL. +3757.8164 mm",
                true,true)
        }).ToArray();
        var group=new[]{"955","956"};
        var result=SupportOrthoTosAudit62.Count(
            source,displayed,group,selected.Length,true);
        Need(result.Expected==10&&result.Visible==10,
            "TEST5 955 description-only not counted as TOS; 956 TOS = 10");
        Need(SupportOrthoTosAudit62.Count(
            source,displayed,group,10,false)==new
                SupportOrthoTosAudit62.Counts(0,0),
            "turning TOS off yields 0/0 without hiding descriptions");
        var tosOnly=selected.Select(view=>C(view,"956",
            "TOS ~ +3757.8164 mm",false)).ToArray();
        var only=SupportOrthoTosAudit62.Count(
            source,tosOnly,group,10,true);
        Need(only.Expected==10&&only.Visible==10,
            "TOS-only display remains countable when Description off");
        var missing=SupportOrthoTosAudit62.Count(
            source,displayed.Where(x=>!(x.Handle=="956"&&
                x.View=="LEFT")).ToArray(),group,10,true);
        Need(missing.Expected==10&&missing.Visible==9,
            "real missing TOS annotation must still yield PARTIAL");
        var extraDescriptions=displayed.Append(C(
            "TOP","955","[955] second description only",true)).ToArray();
        Need(SupportOrthoTosAudit62.Count(
            source,extraDescriptions,group,10,true).Visible==10,
            "HasTos flag alone cannot fabricate a TOS text");
        Need(SupportOrthoTosAudit62.Count(
            source,displayed,new[]{"955"},10,true)==new
                SupportOrthoTosAudit62.Counts(0,0),
            "non-horizontal member without TOS has no expected label");
        var xrefSource=source.Append(
            new SupportOrthoMemberCalloutPlan27.Member(
                "XREF-1","Ref steel",7785,764,3757,
                true,"+3757 mm","EXPLICIT",
                XrefDrawingName:"REF-STEEL.dwg")).ToArray();
        Need(SupportOrthoTosAudit62.Count(
            xrefSource,displayed,new[]{"955","956","XREF-1"},
            10,true).Expected==10,
            "XREF structure cannot change native structural TOS count");

        var legacy=SupportOrthoReferenceLayout51
            .LocalMemberLaneOffsets().First();
        var compact=SupportOrthoReferenceLayout51
            .CompactLocalMemberLaneOffsets().ToArray();
        Need(compact.Length>=6&&
            compact.All(x=>x>0&&x<legacy),
            "safe compact paper lanes stay inward of old local lane");
        var chain=SupportOrthoReferenceLayout51
            .LocalChainLaneOffsets().Take(5).ToArray();
        Need(Math.Abs(chain[0]-
            SupportOrthoReferenceLayout51.ChainOffset())<1e-8&&
            chain.Skip(1).All(x=>x>chain[0]),
            "existing successful chain's first lane is unchanged");

        // Real TEST5 structure envelope from the 2026-11-13 log:
        // host member 955 spans roughly the lower vertical post,
        // member 956 spans the upper horizontal channel. Dimensions
        // are always calculated from these supplied WCS extents, not
        // from hardcoded paper dimension values.
        var members=new[]{
            new SupportOrthoAllViewDimPlan44.Member(
                "NATIVE:955","STRUCTURE",
                7760.82,714.341,3155.029,
                7810.82,814.341,3657.816),
            new SupportOrthoAllViewDimPlan44.Member(
                "NATIVE:956","STRUCTURE",
                7760.82,350.201,3657.816,
                7810.82,1178.481,3757.816)
        };
        var cameras=new[]{
            new SupportOrthoMemberCalloutPlan17.Camera(
                "TOP",118.8,723.26,28,77.976,
                0,0,1,7785.82,764.341,0,1.0/15),
            new SupportOrthoMemberCalloutPlan17.Camera(
                "BOTTOM",338.58,723.26,36,77.976,
                0,0,-1,7785.82,-764.341,
                Math.PI,1.0/15),
            new SupportOrthoMemberCalloutPlan17.Camera(
                "LEFT",118.8,588.7,77.976,60.235,
                -1,0,0,-764.341,3513.573,0,1.0/15),
            new SupportOrthoMemberCalloutPlan17.Camera(
                "RIGHT",338.58,588.7,77.976,60.235,
                1,0,0,764.341,3513.573,0,1.0/15)
        };
        var plan=SupportOrthoReferenceDimPlan47.Build(
            cameras,members,594,841,1);
        Need(plan.PriorityOverallDimensions==8,
            "REV22.62 protects both H/V model-derived overall measurements in all four TEST5 views");
        Need(plan.UnintendedAcceptedWitnessIntersections==0,
            "compacted local dimensions still obey REV22.58 intersection policy");
        Need(plan.Dimensions.All(
            SupportOrthoDynamicDimension492.MatchesDiagnostic),
            "no hardcoded dimensions or stale DIMLFAC after recovery");
        Need(plan.CompactLocalWidthDimensionsRecovered>=0&&
            plan.IntermediateChainLaneGroupsRecovered>=0,
            "diagnostic counts remain nonnegative");
        Console.WriteLine(
            "REV22.62 TOS actual 10/10 (nonTOS 955 excluded), TOS-only, XREF isolation, compact safe lanes, TEST5 H/V protected overall PASS");
    }
}
