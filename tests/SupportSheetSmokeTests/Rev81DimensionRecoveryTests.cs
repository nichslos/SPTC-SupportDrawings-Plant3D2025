using System;
using System.Collections.Generic;
using System.Linq;
using SPTC.SupportDrawings;

internal static class Rev81DimensionRecoveryTests
{
    private static void Require(bool condition,string why)
    {
        if(!condition)
            throw new Exception("REV81 native dimension recovery: "+why);
    }

    private static SupportOrthoDimIntersections58.Shape Horizontal(
        double firstX,double lastX,double sourceY,double lineY)
    {
        var segment=new SupportOrthoDimIntersections58.Segment(
            firstX,lineY,lastX,lineY);
        var witnessA=new SupportOrthoDimIntersections58.Segment(
            firstX,sourceY,firstX,lineY);
        var witnessB=new SupportOrthoDimIntersections58.Segment(
            lastX,sourceY,lastX,lineY);
        var label=new SupportOrthoSmartDimPlan46.Rect(
            (firstX+lastX)*.5-.45,lineY+.5,
            (firstX+lastX)*.5+.45,lineY+1.5);
        return new(segment,witnessA,witnessB,label);
    }

    internal static void Run()
    {
        // One actual paper-space witness would cross the interior of
        // an essential overall line. Never remove the essential.
        var essential=new SupportOrthoDimensionCollisionRecovery.Entry(
            "TOP","NATIVE/OVERALL",true,100,
            Horizontal(0,10,0,3));
        var optional=new SupportOrthoDimensionCollisionRecovery.Entry(
            "TOP","NATIVE/MEMBER_PROFILE",false,1,
            Horizontal(2,8,0,5));
        var clear=new SupportOrthoDimensionCollisionRecovery.Entry(
            "TOP","NATIVE/CLEAR_PROFILE",false,2,
            Horizontal(12,18,0,5));
        var result=SupportOrthoDimensionCollisionRecovery.Resolve(
            new[]{essential,optional,clear});
        Require(result.OriginalConflicts==1&&
            result.RemainingConflicts==0&&
            result.RemovedOptionalIndexes.SequenceEqual(new[]{1}),
            "remove only the conflicting optional, preserve actual essential witness");
        Require(result.Diagnostics.Any(x=>
            x.Contains("FIRST_BASELINE=",StringComparison.Ordinal)&&
            x.Contains("SECOND_WITNESSES=",StringComparison.Ordinal)&&
            x.Contains("NATIVE/OVERALL",StringComparison.Ordinal)),
            "record real paper witness coordinates, sources and pair cause");

        var bothEssential=SupportOrthoDimensionCollisionRecovery.Resolve(
            new[]{essential,optional with {Essential=true}});
        Require(bothEssential.OriginalConflicts>0&&
            bothEssential.RemainingConflicts>0&&
            bothEssential.RemovedOptionalIndexes.Count==0&&
            bothEssential.Diagnostics.Any(x=>
                x.Contains("UNRESOLVED_ESSENTIAL",StringComparison.Ordinal)),
            "never hide essential conflicts by deleting chain or overall");
        var safe=SupportOrthoDimensionCollisionRecovery.Resolve(
            new[]{essential,clear});
        Require(safe.RemainingConflicts==0&&
            safe.RemovedOptionalIndexes.Count==0,
            "do not change existing safe native dimensions");
        var twoOptional=SupportOrthoDimensionCollisionRecovery.Resolve(
            new[]{optional with {Source="LOW",EvidenceRank=1},
                  essential with {Source="HIGH",Essential=false,
                      EvidenceRank=3}});
        Require(twoOptional.RemovedOptionalIndexes.SequenceEqual(new[]{0}),
            "when only optionals collide, preserve the better verified source");
        bool wrongView=false;
        try
        {
            _=SupportOrthoDimensionCollisionRecovery.Resolve(
                new[]{essential,clear with {View="FRONT"}});
        }
        catch(ArgumentException){wrongView=true;}
        Require(wrongView,
            "never compare witness geometry from separate native cameras");

        // Legacy REV reflow must recover a crossing even when there
        // are zero optional omissions and correct overall/chain tiers.
        var sourceOnly=new SupportOrthoReferenceDimPlan47.Result(
            Array.Empty<SupportOrthoDimLinearPlan41.Dimension>(),
            Array.Empty<string>(),1,0,0,0,0,
            UnintendedAcceptedWitnessIntersections:2,
            FinalCollisionDiagnostics:new[]{
                "TOP:CAUSE=WITNESS_CROSSES_DIMENSION_LINE_INTERIOR"
            });
        var clearPlan=sourceOnly with {
            UnintendedAcceptedWitnessIntersections=0,
            FinalCollisionDiagnostics=Array.Empty<string>()
        };
        var camera=new SupportOrthoMemberCalloutPlan17.Camera(
            "TOP",0,0,100,100,0,0,1,0,0,0,.1);
        int trials=0;
        var rescued=SupportOrthoDimensionReflow.Improve(
            new[]{camera},sourceOnly,_=>{
                trials++;
                return clearPlan;
            },maxAttemptsPerView:2);
        Require(trials>0&&
            rescued.Plan.UnintendedAcceptedWitnessIntersections==0&&
            rescued.ChangedViews==1,
            "reflow corrects witness-only failures even when no other issue exists");
        Console.WriteLine(
            "REV81 native witness pair diagnostics, optional-only salvage, "+
            "essential protection and independent crossing reflow PASS");
    }
}
