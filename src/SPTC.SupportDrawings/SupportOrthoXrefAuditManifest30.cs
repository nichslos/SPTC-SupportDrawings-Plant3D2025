using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// Pure, fail-closed REV22.30 reader for the original REV22.29 generation log.
/// Do not assume visible saved labels represent current XREF metadata.
/// </summary>
public static class SupportOrthoXrefAuditManifest30
{
    public sealed record Point(double X,double Y,double Z);
    public sealed record Bounds(Point Min,Point Max);
    public sealed record Source(string Key,string LineTag,string Bop,
        Point WorldAnchor);
    public sealed record Label(string View,string Key,string First,
        string Second,Point Anchor,double LaneX,Point LabelTop);
    public sealed record View(string Name,Point PaperCenter);
    public sealed record Manifest(string SourceModel,Bounds Support,Bounds Cube,
        IReadOnlyList<Source> Sources,IReadOnlyList<Label> Labels,
        IReadOnlyList<View> Views);

    public static Manifest Parse(IEnumerable<string> rawLines)
    {
        if(rawLines==null)throw new ArgumentNullException(nameof(rawLines));
        string[] lines=rawLines.ToArray();
        string source=Required(lines,"SOURCE_MODEL=");
        var support=new Bounds(ParsePoint(Required(lines,
                "REV22_29_SUPPORT_RAW_MIN=")),
            ParsePoint(Required(lines,"REV22_29_SUPPORT_RAW_MAX=")));
        var cube=new Bounds(ParsePoint(Required(lines,
                "NATIVE_CUBE_MIN=")),
            ParsePoint(Required(lines,"NATIVE_CUBE_MAX=")));
        if(!GoodBounds(support)||!GoodBounds(cube))
            throw new FormatException("REV22.30 invalid source support/OrthoCube bounds.");

        var sources=new List<Source>();
        foreach(string line in lines)
        {
            if(!Take(line,"REV22_29_PIPE_SOURCE=",out string payload))continue;
            string key=Before(payload," LINE_TAG=");
            string tag=Between(payload," LINE_TAG="," LINE_SOURCE=");
            string lineAndBop=Between(payload," LINE_SOURCE="," BOP_SOURCE=");
            int space=lineAndBop.IndexOf(' ');
            if(space<=0)throw new FormatException("REV22.30 original BOP field missing: "+key);
            string bop=lineAndBop[(space+1)..].Trim();
            string anchor=After(payload," WCS=");
            if(string.IsNullOrWhiteSpace(key)||string.IsNullOrWhiteSpace(bop))
                throw new FormatException("REV22.30 original XREF source incomplete.");
            sources.Add(new Source(key,tag,bop,ParsePoint(anchor)));
        }
        if(sources.Count==0 || sources.Select(p=>p.Key).Distinct(
            StringComparer.OrdinalIgnoreCase).Count()!=sources.Count)
            throw new FormatException("REV22.30 missing or duplicate original source pipes.");

        var labels=new List<Label>();
        foreach(string line in lines)
        {
            if(!Take(line,"STEP10_MEMBER_LABEL=",out string payload))continue;
            if(!payload.Contains(" PIPE_BOP=True ",StringComparison.OrdinalIgnoreCase))
                continue;
            string view=Before(payload," SOURCE=");
            string key=Between(payload," SOURCE="," SECOND_LINE_PRESENT=");
            string annotation=Between(payload," TEXT="," ANCHOR=");
            string[] pair=annotation.Split(new[]{" | "},StringSplitOptions.None);
            if(pair.Length!=2 || pair.Any(string.IsNullOrWhiteSpace))
                throw new FormatException("REV22.30 original pipe label not two lines.");
            var anchor=ParsePoint(Between(payload," ANCHOR="," LANE_X="));
            double lane=ParseNumber(Between(payload," LANE_X="," LABEL="));
            var label=ParsePoint(After(payload," LABEL="));
            labels.Add(new Label(view,key,pair[0],pair[1],anchor,lane,label));
        }
        var views=new List<View>();
        foreach(string line in lines)
        {
            int start=line.IndexOf("STEP8_",StringComparison.Ordinal);
            if(start<0)continue;
            string rest=line[(start+6)..];
            const string marker="_ACTUAL_VIEWPORT=";
            int sep=rest.IndexOf(marker,StringComparison.Ordinal);
            if(sep<0)continue;
            string name=rest[..sep];
            int tail=rest.IndexOf(" SIZE=",sep,StringComparison.Ordinal);
            if(tail<0)throw new FormatException("REV22.30 view frame missing.");
            string point=rest[(sep+marker.Length)..tail];
            if(!new[]{"TOP","BOTTOM","LEFT","RIGHT","FRONT","BACK",
                "NW","NE","SW","SE"}.Contains(name,StringComparer.Ordinal))
                continue;
            views.Add(new View(name,ParsePoint(point)));
        }
        if(views.Count!=10 || views.Select(v=>v.Name).Distinct(
            StringComparer.OrdinalIgnoreCase).Count()!=10)
            throw new FormatException("REV22.30 original view manifest is not ten unique views.");
        if(labels.Count!=sources.Count*10 ||
            labels.GroupBy(x=>x.Key,StringComparer.OrdinalIgnoreCase)
                .Any(x=>x.Count()!=10 || x.Select(v=>v.View).Distinct(
                    StringComparer.OrdinalIgnoreCase).Count()!=10) ||
            labels.Any(l=>!sources.Any(s=>string.Equals(s.Key,l.Key,
                StringComparison.OrdinalIgnoreCase))))
            throw new FormatException("REV22.30 original pipe label/source counts are inconsistent.");
        if(!lines.Any(l=>l.Contains("STEP10_MEMBER_ANNOTATIONS_CHECK=PASS",
            StringComparison.Ordinal)))
            throw new FormatException("REV22.30 original annotation check was not PASS.");
        return new Manifest(source,support,cube,sources,labels,views);
    }

    public static Point ParsePoint(string raw)
    {
        string value=raw.Trim();
        if(!value.StartsWith("(",StringComparison.Ordinal) ||
           !value.EndsWith(")",StringComparison.Ordinal))
            throw new FormatException("REV22.30 invalid point: "+value);
        string[] p=value[1..^1].Split(',');
        if(p.Length!=3)
            throw new FormatException("REV22.30 expected 3-coordinate point.");
        return new Point(ParseNumber(p[0]),ParseNumber(p[1]),ParseNumber(p[2]));
    }

    public static double ParseNumber(string s)
    {
        if(!double.TryParse(s.Trim(),NumberStyles.Float,
            CultureInfo.InvariantCulture,out double number) ||
            !double.IsFinite(number))
            throw new FormatException("REV22.30 invalid finite number: "+s);
        return number;
    }

    public static bool GoodBounds(Bounds b) =>
        b.Min.X<=b.Max.X && b.Min.Y<=b.Max.Y &&
        b.Min.Z<=b.Max.Z;

    private static string Required(IEnumerable<string> lines,string key)
    {
        foreach(string line in lines)
            if(Take(line,key,out string value))return value;
        throw new FormatException("REV22.30 missing original log key: "+key);
    }

    private static bool Take(string text,string key,out string value)
    {
        value="";
        int i=text.IndexOf(key,StringComparison.Ordinal);
        if(i<0)return false;
        value=text[(i+key.Length)..].Trim();
        return true;
    }

    private static string Before(string text,string end)
    {
        int i=text.IndexOf(end,StringComparison.Ordinal);
        if(i<=0)throw new FormatException("REV22.30 missing field: "+end);
        return text[..i].Trim();
    }

    private static string Between(string text,string start,string end)
    {
        int i=text.IndexOf(start,StringComparison.Ordinal);
        if(i<0)throw new FormatException("REV22.30 missing field: "+start);
        i+=start.Length;
        int j=text.IndexOf(end,i,StringComparison.Ordinal);
        if(j<0)throw new FormatException("REV22.30 missing field: "+end);
        return text[i..j].Trim();
    }

    private static string After(string text,string start)
    {
        int i=text.IndexOf(start,StringComparison.Ordinal);
        if(i<0)throw new FormatException("REV22.30 missing field: "+start);
        return text[(i+start.Length)..].Trim();
    }
}
