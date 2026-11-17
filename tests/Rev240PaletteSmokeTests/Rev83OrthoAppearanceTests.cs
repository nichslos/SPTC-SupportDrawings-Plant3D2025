using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SPTC.SupportDrawings;

internal static class Rev83OrthoAppearanceTests
{
    private static void Need(bool ok,string description)
    {
        if(!ok)throw new Exception("REV83 Ortho Appearance: "+description);
    }

    private sealed class FakeHost:NativeOrthoAppearance83.IHost
    {
        public object Hidden="hiddenPipingonly";
        public object Insulation=0;
        public readonly List<string> Writes=new();
        public bool FailInsulation;
        public object ReadHidden()=>Hidden;
        public object ReadInsulation()=>Insulation;
        public object ReadHiddenAfter()=>Hidden;
        public object ReadInsulationAfter()=>Insulation;
        public void SetHiddenKeyword(string keyword)
        {
            Hidden=keyword;Writes.Add("H:"+keyword);
        }
        public void SetInsulationValue(int value)
        {
            if(FailInsulation)throw new InvalidOperationException(
                "Simulated native insulation setter failure");
            Insulation=value;Writes.Add("I:"+value);
        }
        public void RestoreHidden(object before)
        {
            Hidden=before;Writes.Add("HR:"+before);
        }
        public void RestoreInsulation(object before)
        {
            Insulation=before;Writes.Add("IR:"+before);
        }
    }

    internal static void Run()
    {
        var policy=NativeOrthoAppearance83.HiddenChoices;
        var insulation=NativeOrthoAppearance83.InsulationChoices;
        Need(policy.SequenceEqual(new[]{
            "Plant 3D Default","No Hidden Lines",
            "Hidden Line Piping","All Hidden Lines"}),
            "all native hidden-line UI selections");
        Need(insulation.SequenceEqual(new[]{
            "Plant 3D Default","No Insulation",
            "Solid Line Insulation","Hidden Line Insulation"}),
            "all insulation UI selections");
        Need(NativeOrthoAppearance83.HiddenKeyword(
                NativeOrthoAppearance83.NoHidden)=="Nohiddenlines"&&
             NativeOrthoAppearance83.HiddenKeyword(
                NativeOrthoAppearance83.PipeHidden)=="hiddenPipingonly"&&
             NativeOrthoAppearance83.HiddenKeyword(
                NativeOrthoAppearance83.AllHidden)=="Allhiddenlines",
            "map to Autodesk's actual prompt keywords, not guessed enums");
        Need(NativeOrthoAppearance83.InsulationValue(
                NativeOrthoAppearance83.NoInsulation)==0&&
             NativeOrthoAppearance83.InsulationValue(
                NativeOrthoAppearance83.SolidInsulation)==1&&
             NativeOrthoAppearance83.InsulationValue(
                NativeOrthoAppearance83.HiddenInsulation)==2,
            "insulation 0/1/2 values match Autodesk native documentation");
        var fake=new FakeHost();
        using(NativeOrthoAppearance83.Begin(fake,
            NativeOrthoAppearance83.ProjectDefault,
            NativeOrthoAppearance83.ProjectDefault,_=>{}))
        {
            Need(fake.Writes.Count==0,
                "default preserves native host appearance exactly");
        }
        Need(fake.Writes.Count==0,
            "default does not write or change any Plant session value");

        var output=new List<string>();
        using(NativeOrthoAppearance83.Begin(fake,
            NativeOrthoAppearance83.AllHidden,
            NativeOrthoAppearance83.HiddenInsulation,output.Add))
        {
            Need((string)fake.Hidden=="Allhiddenlines"&&
                 (int)fake.Insulation==2,
                "explicit settings active only during native generation");
        }
        Need((string)fake.Hidden=="hiddenPipingonly"&&
             (int)fake.Insulation==0&&
             fake.Writes.SequenceEqual(new[]{
                 "H:Allhiddenlines","I:2","IR:0","HR:hiddenPipingonly"}),
            "both session/registry variables restored in reverse order");
        Need(output.Any(x=>x.Contains("INSULATION_MODE_RESTORE=PASS"))&&
             output.Any(x=>x.Contains("HIDDEN_MODE_RESTORE=PASS")),
            "session cleanup logs measured restore");

        var broken=new FakeHost{FailInsulation=true};
        bool rollback=false;
        try
        {
            using var scoped=NativeOrthoAppearance83.Begin(broken,
                NativeOrthoAppearance83.NoHidden,
                NativeOrthoAppearance83.SolidInsulation,_=>{});
        }
        catch(InvalidOperationException){rollback=true;}
        Need(rollback&&(string)broken.Hidden=="hiddenPipingonly",
            "failure applying insulation restores previous hidden-line state");
        bool invalid=false;
        try{NativeOrthoAppearance83.HiddenKeyword("UNKNOWN");}
        catch(ArgumentException){invalid=true;}
        Need(invalid,"reject an unsupported hidden-line name");

        using var ui=new SupportDrawingManagerForm35(
            @"C:\Projects\REV83\MODEL.dwg",
            @"C:\Projects\REV83",
            @"C:\Projects\REV83\Orthos");
        ui.CreateControl();
        var h=ui.Controls.Find("SPTC_HIDDEN_LINES_83",true)
            .OfType<ComboBox>().Single();
        var ins=ui.Controls.Find("SPTC_INSULATION_83",true)
            .OfType<ComboBox>().Single();
        Need(h.SelectedItem?.ToString()==
            NativeOrthoAppearance83.ProjectDefault&&
            ins.SelectedItem?.ToString()==
            NativeOrthoAppearance83.ProjectDefault,
            "REV82 visible outputs unchanged by default");
        h.SelectedItem=NativeOrthoAppearance83.AllHidden;
        ins.SelectedItem=NativeOrthoAppearance83.SolidInsulation;
        var footer=ui.Controls.OfType<Panel>()
            .Single(p=>p.Dock==DockStyle.Bottom);
        footer.Controls.OfType<Button>()
            .Single(b=>b.Text=="Preflight").PerformClick();
        var summary=footer.Controls.OfType<TextBox>()
            .Single(t=>t.Multiline&&t.ReadOnly);
        Need(summary.Text.Contains("6. HIDDEN LINES: All Hidden Lines")&&
             summary.Text.Contains("7. INSULATION: Solid Line Insulation"),
            "preflight explicitly shows actual requested native modes");
        SupportDrawingUiOptions? queued=null;
        ui.RunRequested+=options=>queued=options;
        typeof(SupportDrawingManagerForm35).GetMethod(
            "RunSelected",System.Reflection.BindingFlags.NonPublic|
                          System.Reflection.BindingFlags.Instance)!
            .Invoke(ui,null);
        Need(queued?.HiddenLineMode==NativeOrthoAppearance83.AllHidden&&
             queued?.InsulationMode==NativeOrthoAppearance83.SolidInsulation,
            "palette queued settings reach native generator intact");

        var saved=SupportDrawingUiPreferences.Defaults with {
            HiddenLineMode=NativeOrthoAppearance83.NoHidden,
            InsulationMode=NativeOrthoAppearance83.HiddenInsulation
        };
        var copy=SupportDrawingUiPreferences.Deserialize(
            SupportDrawingUiPreferences.Serialize(saved));
        Need(copy.HiddenLineMode==saved.HiddenLineMode&&
             copy.InsulationMode==saved.InsulationMode,
            "per-project native appearance choices round-trip safely");
        var oldJson=SupportDrawingUiPreferences.Serialize(
            SupportDrawingUiPreferences.Defaults)
            .Replace(",\"HiddenLineMode\":\"Plant 3D Default\"","")
            .Replace(",\"InsulationMode\":\"Plant 3D Default\"","");
        var backward=SupportDrawingUiPreferences.Deserialize(oldJson);
        Need(backward.HiddenLineMode==
                NativeOrthoAppearance83.ProjectDefault&&
             backward.InsulationMode==
                NativeOrthoAppearance83.ProjectDefault,
            "older per-project preferences silently inherit safe Plant default");
        Console.WriteLine(
            "REV83 native hidden lines and insulation UI, preflight, "+
            "queued options, scoped/rollback host state, saved settings PASS");
    }
}
