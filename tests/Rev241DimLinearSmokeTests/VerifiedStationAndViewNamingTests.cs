using System;
using System.Collections.Generic;
using System.Linq;
using SPTC.SupportDrawings;

/// <summary>
/// Tests the geometry-derived section station recovery contract and
/// revision-independent view and loaded-build identity. TEST5 coordinates
/// are fixtures only, never compiled into the production algorithms.
/// </summary>
internal static class VerifiedStationAndViewNamingTests
{
    private static void Need(bool ok,string reason)
    {
        if(!ok)throw new Exception("Verified stations and names: "+reason);
    }

    internal static void Run()
    {
        var left=new SupportOrthoMemberCalloutPlan17.Camera(
            "LEFT",118.8,588.7,86.376,42.234,
            -1,0,0,-714.341,1955.379,0,1.0/15);
        var start=SupportOrthoMemberCalloutPlan17.Project(
            left,7785.82,300.201,1753.985);
        var end=SupportOrthoMemberCalloutPlan17.Project(
            left,7785.82,1128.481,1753.985);
        double midpoint=(start.X+end.X)*.5;
        double delta=end.X-start.X;
        var sourceStations=SupportOrthoProfileStationPlan
            .AlongVerifiedAxis(true,midpoint,delta,
                Math.Min(start.X,end.X)-3.33,
                Math.Max(start.X,end.X)+3.33);
        Need(sourceStations.Count>=2&&
            sourceStations.All(x=>double.IsFinite(x)&&
                x>Math.Min(start.X,end.X)-3.33&&
                x<Math.Max(start.X,end.X)+3.33&&
                Math.Abs(x-midpoint)>.35),
            "candidate profile stations interpolate only verified native member axis");
        Need(SupportOrthoProfileStationPlan.AlongVerifiedAxis(
                false,midpoint,delta,100,160).Count==0&&
             SupportOrthoProfileStationPlan.AlongVerifiedAxis(
                true,double.NaN,delta,100,160).Count==0&&
             SupportOrthoProfileStationPlan.AlongVerifiedAxis(
                true,midpoint,0,100,160).Count==0,
            "never fabricate cross-section stations for missing or degenerate axis evidence");

        string name=SupportOrthoViewNaming.Name("left","021623");
        Need(name=="SPTC_LEFT_021623"&&!name.Contains("_R22_",
            StringComparison.OrdinalIgnoreCase) &&
            SupportOrthoViewNaming.Name("NW","021623")=="SPTC_NW_021623",
            "new native view names contain no development revision");
        bool invalidRejected=false;
        try{SupportOrthoViewNaming.Name("CUSTOM","021623");}
        catch(ArgumentException){invalidRejected=true;}
        Need(invalidRejected,"unrecognized camera cannot produce a registered native view name");

        Need(SupportBuildIdentity.NormalizeVersion(
                "0d9a4774+0d9a4774")=="0d9a4774"&&
             SupportBuildIdentity.NormalizeVersion(
                "1.2.3+sha")=="1.2.3+sha"&&
             !string.IsNullOrWhiteSpace(SupportBuildIdentity.Id),
            "real build ID normalizes duplicated Git SHA without changing other metadata");

        var overall=SupportOrthoReferenceLayout51
            .OverallLaneOffsets().ToArray();
        var chains=SupportOrthoReferenceLayout51
            .LocalChainLaneOffsets().ToArray();
        Need(overall.Length>=10&&chains.Length>=6&&
            SupportOrthoReferenceDimPlan47
                .PreferLaneRank(chains,4).First()==chains[4],
            "reflow exposes more paper-space lane combinations without source-size constants");
        Console.WriteLine(
            "Verified source-axis station fallback, safe source absence, stable view names and Git build identity PASS");
    }
}
