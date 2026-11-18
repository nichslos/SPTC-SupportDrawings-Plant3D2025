using System;
using System.Linq;
using System.Windows.Forms;
using SPTC.SupportDrawings;

internal static class Rev83OrthoAppearanceTests
{
    private static void Need(bool ok,string description)
    {
        if(!ok)throw new Exception("REV83 Ortho Appearance: "+description);
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
            "queued options and saved settings PASS");
    }
}
