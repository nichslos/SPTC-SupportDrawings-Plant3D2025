using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AcadApp=Autodesk.AutoCAD.ApplicationServices.Application;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.29 read-only audit to run AFTER the user manually runs native
/// PLANTORTHOUPDATE / Update View and BEFORE accepting the updated drawing.
/// Compares all generated pipe label DBText against the original layout
/// manifest in the adjacent REV22_29_RESULT.txt. Never changes the DWG.
/// Verifies original label persistence at original paper positions ONLY;
/// it cannot certify updated source properties, XREF geometry, or leaders.
/// </summary>
public sealed class SupportOrthoUpdateAudit29
{
    private sealed record Expected(string View,string Text,double X,double Y);
    private sealed record Actual(string Text,double X,double Y,string Layout);

    [CommandMethod("SPTC_ORTHO22_29_AUDIT_UPDATE",CommandFlags.Modal)]
    public void Run()
    {
        Document? doc=AcadApp.DocumentManager.MdiActiveDocument;
        if(doc==null)return;
        Editor ed=doc.Editor;
        string dwg=doc.Database.Filename??"";
        if(string.IsNullOrWhiteSpace(dwg) || !File.Exists(dwg))
        {
            ed.WriteMessage("\nREV22.29: Open the generated, saved Ortho DWG.");
            return;
        }
        string folder=Path.GetDirectoryName(dwg)??"";
        string prefix=Path.Combine(folder,Path.GetFileNameWithoutExtension(dwg));
        string manifest=prefix+"_REV22_29_RESULT.txt";
        if(!File.Exists(manifest))
        {
            ed.WriteMessage("\nREV22.29 UPDATE AUDIT: Original REV22_29_RESULT.txt"+
                " not found beside the Ortho drawing. No unverifiable PASS.");
            return;
        }

        var findings=new List<string>();
        var expected=new List<Expected>();
        int badManifest=0;
        foreach(string line in File.ReadLines(manifest))
        {
            const string key="STEP10_MEMBER_LABEL=";
            int start=line.IndexOf(key,StringComparison.Ordinal);
            if(start<0 || line.IndexOf(" PIPE_BOP=True ",
                start,StringComparison.OrdinalIgnoreCase)<0)continue;
            string payload=line[(start+key.Length)..];
            int source=payload.IndexOf(" SOURCE=",StringComparison.Ordinal);
            int text=payload.IndexOf(" TEXT=",StringComparison.Ordinal);
            int anchor=payload.IndexOf(" ANCHOR=",StringComparison.Ordinal);
            int label=payload.LastIndexOf(" LABEL=(",StringComparison.Ordinal);
            if(source<=0 || text<0 || anchor<=text || label<anchor)
            {
                badManifest++;continue;
            }
            string view=payload[..source].Trim();
            string[] pair=payload[(text+6)..anchor]
                .Split(new[]{" | "},StringSplitOptions.None);
            int close=payload.IndexOf(')',label+8);
            if(pair.Length!=2 || close<0 ||
                !TryPoint(payload[(label+8)..close],out double x,out double y))
            {
                badManifest++;continue;
            }
            expected.Add(new Expected(view,pair[0],x,y));
            expected.Add(new Expected(view,pair[1],x,y));
        }
        if(expected.Count==0 || badManifest!=0 ||
            expected.Count%2!=0)
        {
            ed.WriteMessage("\nREV22.29 UPDATE AUDIT: Missing or malformed "+
                "pipe label manifest. No PASS. Entries="+expected.Count+
                " malformed="+badManifest);
            return;
        }
        // Compare manifest to DWG paper-space only. Update View can change
        // native model geometry without regenerating these custom DBText labels.
        var actual=new List<Actual>();
        using(var tr=doc.Database.TransactionManager.StartTransaction())
        {
            var dict=(DBDictionary)tr.GetObject(
                doc.Database.LayoutDictionaryId,OpenMode.ForRead);
            foreach(DBDictionaryEntry item in dict)
            {
                var layout=tr.GetObject(item.Value,OpenMode.ForRead) as Layout;
                if(layout==null || layout.ModelType)continue;
                var paper=tr.GetObject(layout.BlockTableRecordId,
                    OpenMode.ForRead) as BlockTableRecord;
                if(paper==null)continue;
                foreach(ObjectId id in paper)
                {
                    try
                    {
                        if(tr.GetObject(id,OpenMode.ForRead,false)
                            is DBText t && string.Equals(t.Layer,
                            SupportOrthoMemberCalloutPlan27.Layer,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            actual.Add(new Actual(t.TextString,
                                t.Position.X,t.Position.Y,layout.LayoutName));
                        }
                    }
                    catch(System.Exception){}
                }
            }
        }
        var used=new HashSet<int>();
        int matched=0;
        foreach(var expect in expected)
        {
            int index=-1;
            for(int i=0;i<actual.Count;i++)
            {
                if(used.Contains(i) ||
                    !string.Equals(actual[i].Text,expect.Text,
                        StringComparison.Ordinal))continue;
                // Original DBText is placed below the top of its label row.
                // There are two rows separated by TextLineHeightMm.
                bool second=expect.Text.StartsWith("BOP",StringComparison.OrdinalIgnoreCase);
                double centerY=expect.Y-
                    (second?SupportOrthoMemberCalloutPlan27.TextLineHeightMm:0)-
                    SupportOrthoMemberCalloutPlan27.TextHeightMm*1.18;
                if(Math.Abs(actual[i].X-expect.X)>1.0 ||
                    Math.Abs(actual[i].Y-centerY)>1.65)continue;
                index=i;break;
            }
            if(index<0)
                findings.Add("MISSING_OR_MOVED_VIEW="+expect.View+
                    " TEXT="+expect.Text);
            else
            {
                used.Add(index);
                matched++;
            }
        }
        bool pass=matched==expected.Count && findings.Count==0;
        string state=pass?"PASS":"FAIL";
        var report=new List<string>
        {
            "REV22_29_UPDATE_AUDIT="+state,
            "ORTHO_DWG="+dwg,
            "REFERENCE_LOG="+manifest,
            "RUN_AT="+DateTimeOffset.Now.ToString("O",
                CultureInfo.InvariantCulture),
            "PIPE_TEXT_LINES_EXPECTED="+expected.Count,
            "PIPE_TEXT_LINES_PRESENT_IN_ORIGINAL_POSITIONS="+matched,
            "PIPE_TEXT_LINES_MISSING_OR_MOVED="+findings.Count,
            "VISIBLE_PAPER_CALLOUT_TEXT_ENTITIES="+actual.Count,
            "XREF_PROPERTY_FRESHNESS=NOT_VERIFIED",
            "NATIVE_VIEW_GEOMETRY_ALIGNMENT=NOT_VERIFIED",
            "NOTE=Read-only persistence audit after manual Update View;"+
                " rerunning Update View may leave custom DBText visible but stale"
        };
        report.AddRange(findings);
        string file=prefix+"_REV22_29_UPDATE_AUDIT.txt";
        try {File.WriteAllLines(file,report);}
        catch(System.Exception ex)
        {
            ed.WriteMessage("\nREV22.29: Audit output cannot be saved: "+ex.Message);
        }
        ed.WriteMessage("\nREV22.29 Update View annotation persistence: "+
            state+" ("+matched+"/"+expected.Count+
            " text lines in original paper positions).");
        ed.WriteMessage("\nXREF metadata freshness and native view/leader"+
            " alignment are NOT verified by this audit.");
        ed.WriteMessage("\nAudit TXT: "+file);
    }

    private static bool TryPoint(string xyz,out double x,out double y)
    {
        x=y=0;
        string[] p=xyz.Split(',');
        return p.Length>=2 &&
            double.TryParse(p[0],NumberStyles.Float,
                CultureInfo.InvariantCulture,out x) &&
            double.TryParse(p[1],NumberStyles.Float,
                CultureInfo.InvariantCulture,out y);
    }
}
