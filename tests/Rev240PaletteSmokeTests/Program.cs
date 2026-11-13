using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using SPTC.SupportDrawings;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var manager=new SupportDrawingManagerForm35(
            @"C:\Projects\SAMPLE\MODELA.dwg",
            @"C:\Projects\SAMPLE",
            @"C:\Projects\SAMPLE\Orthos");
        Check(manager is UserControl && !manager.IsDisposed,
            "must be persistent UserControl, not a modal Form");
        Check(manager.Controls.Find("SPTC_UI_LAYOUT_SURFACE",false)
                .Single().Controls.OfType<TabControl>().Single().TabCount==4,
            "four existing operation tabs preserved");

        int count=0;
        SupportDrawingUiOptions? last=null;
        manager.RunRequested+=settings=>
        {
            count++;
            last=settings;
        };
        MethodInfo run=typeof(SupportDrawingManagerForm35).GetMethod(
            "RunSelected",BindingFlags.Instance|BindingFlags.NonPublic)
            ??throw new Exception("Run callback missing");

        run.Invoke(manager,null);
        Check(count==1 && last?.Operation=="ortho",
            "Run dispatches settings as an event without closing UI");
        Check(!manager.IsDisposed && manager.Settings!=null,
            "palette contents persist after Run");
        Check(last!.ScaleDenominator==0,"Automatic scale preserved");
        Check(last.AutoDimLinear,
            "REV22.41 default-on Auto DIMLINEAR retained by palette");
        manager.SetRunning(true);
        run.Invoke(manager,null);
        Check(count==1 && manager.IsRunning,
            "busy guard rejects overlapping Run");
        manager.SetRunning(false);
        manager.UpdateProjectContext(
            @"C:\Projects\SAMPLE\MODELB.dwg",
            @"C:\Projects\SAMPLE",
            @"C:\Projects\SAMPLE\Orthos");
        run.Invoke(manager,null);
        Check(count==2 && manager.Settings!=null,
            "settings persist and Run can repeat with new source");

        TestScrollableDpiLayout();
        TestBatchProgressReporting();
        TestProductionPackageCheckboxSaveRoundTrip();
        var surface=manager.Controls.Find(
            "SPTC_UI_LAYOUT_SURFACE",false).Single();
        var nativeTabs=surface.Controls.OfType<TabControl>().Single();
        var configButton=nativeTabs.TabPages[0].Controls
            .OfType<Button>().SingleOrDefault(b=>
                b.Text=="Production package...");
        Check(configButton!=null,
            "optional package configuration remains inside modeless Ortho tab");
        using(var configured=new SupportProductionPackageDialog(
            @"C:\Projects\SAMPLE"))
            Check(configured is Form&&configured.AutoScaleMode==AutoScaleMode.Dpi,
                "production metadata editor is DPI aware and has no AutoCAD command side effects");
        Console.WriteLine("REV22.40 persistent modeless UI/settings and busy tests PASS");
    }

    private static void TestScrollableDpiLayout()
    {
        foreach(float scale in new[]{1f,1.25f,1.5f})
        {
            using var ui=new SupportDrawingManagerForm35(
                @"C:\Projects\TEST_DPI\SOURCE.dwg",
                @"C:\Projects\TEST_DPI",
                @"C:\Projects\TEST_DPI\Orthos");
            if(scale>1f)ui.Scale(new SizeF(scale,scale));
            var canvas=ui.Controls.Find("SPTC_UI_LAYOUT_SURFACE",
                searchAllChildren:false).Single();
            var tabs=canvas.Controls.OfType<TabControl>().Single();
            Check(ui.AutoScaleMode==AutoScaleMode.Dpi&&ui.AutoScroll,
                "manager stays DPI-aware and scrollable");
            Check(tabs.TabCount==4,
                "all four production operations visible at every DPI");
            Check(tabs.TabPages[0].AutoScroll,
                "detailed Ortho page has working scroll support");
            var run=canvas.Controls.OfType<Button>()
                .Single(x=>x.Text=="Run selected");
            var preflight=canvas.Controls.OfType<Button>()
                .Single(x=>x.Text=="Preflight");
            Check(run.Bounds.Right<=canvas.ClientSize.Width&&
                run.Bounds.Bottom<=canvas.ClientSize.Height&&
                preflight.Bounds.Right<=canvas.ClientSize.Width&&
                preflight.Bounds.Bottom<=canvas.ClientSize.Height,
                "command actions remain within the full design surface");
            var summary=canvas.Controls.OfType<TextBox>()
                .Single(x=>x.Multiline&&x.ReadOnly);
            Check(!run.Bounds.IntersectsWith(summary.Bounds)&&
                !preflight.Bounds.IntersectsWith(summary.Bounds),
                "footer actions cannot overlap the preflight summary");
            int narrowW=Math.Max(ui.MinimumSize.Width+15,
                canvas.Width/2);
            int narrowH=Math.Max(ui.MinimumSize.Height+15,
                canvas.Height/2);
            ui.ClientSize=new Size(narrowW,narrowH);
            ui.PerformLayout();
            Check(ui.HorizontalScroll.Visible&&ui.VerticalScroll.Visible,
                "narrow docked palette exposes both scroll directions");
            Check(canvas.Controls.OfType<Button>()
                .Any(b=>b.Text=="Run selected"),
                "Run action stays present when host narrows");
        }
        Console.WriteLine(
            "Production palette full design area scrolls at simulated 100/125/150% DPI PASS");
    }

    private static void TestProductionPackageCheckboxSaveRoundTrip()
    {
        string root=Path.Combine(Path.GetTempPath(),
            "SPTC_PRODUCTION_CHECKBOX_"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string file=SupportProductionPackageSettingsStore.PathForProject(root);
        try
        {
            using(var dialog=new SupportProductionPackageDialog(root))
            {
                var enabled=dialog.Controls.Find(
                    "SPTC_PRODUCTION_ENABLED",true).OfType<CheckBox>().Single();
                var pdf=dialog.Controls.Find(
                    "SPTC_PUBLISH_PDF_ENABLED",true).OfType<CheckBox>().Single();
                Check(!enabled.Checked&&!pdf.Checked,
                    "new projects start with both options off");
                // Reproduce the user's lower checkbox being checked while
                // the top production switch was previously left off.
                pdf.Checked=true;
                Check(pdf.Checked&&enabled.Checked,
                    "checking PDF automatically enables its production prerequisite");
                void SetField(string name,string value)
                {
                    var field=typeof(SupportProductionPackageDialog).GetField(
                        name,BindingFlags.Instance|BindingFlags.NonPublic)
                        ??throw new Exception("Missing settings field "+name);
                    ((TextBox)(field.GetValue(dialog)??throw new Exception(
                        "Missing text field "+name))).Text=value;
                }
                SetField("projectCode","TB26");
                SetField("series","SUP");
                SetField("revision","A");
                SetField("title","SUPPORT DRAWING");
                // Invoke the exact dialog Save button handler, not a
                // synthetic settings-only serializer test.
                typeof(SupportProductionPackageDialog).GetMethod(
                    "Save",BindingFlags.Instance|BindingFlags.NonPublic)!
                    .Invoke(dialog,null);
                Check(dialog.DialogResult==DialogResult.OK,
                    "save closes only after exact values are reloaded from disk");
            }
            Check(File.Exists(file),
                "production settings exist at the correct project-scoped path");
            Check(SupportProductionPackageSettingsStore.TryLoad(root,
                    out var fromDisk,out string diagnostic)&&
                diagnostic=="CONFIGURED_ENABLED"&&
                fromDisk.Enabled&&fromDisk.PublishPdf&&
                fromDisk.ProjectCode=="TB26",
                "both enabled checkboxes survive disk save and reload");

            using(var reopened=new SupportProductionPackageDialog(root))
            {
                var enabled=reopened.Controls.Find(
                    "SPTC_PRODUCTION_ENABLED",true).OfType<CheckBox>().Single();
                var pdf=reopened.Controls.Find(
                    "SPTC_PUBLISH_PDF_ENABLED",true).OfType<CheckBox>().Single();
                Check(enabled.Checked&&pdf.Checked,
                    "reopening dialog restores both checked states");
                enabled.Checked=false;
                Check(!pdf.Checked,
                    "explicitly disabling production also disables PDF");
                enabled.Checked=true;
                pdf.Checked=true;
                typeof(SupportProductionPackageDialog).GetMethod(
                    "Save",BindingFlags.Instance|BindingFlags.NonPublic)!
                    .Invoke(reopened,null);
            }
            using(var third=new SupportProductionPackageDialog(root))
            {
                Check(third.Controls.Find("SPTC_PRODUCTION_ENABLED",true)
                    .OfType<CheckBox>().Single().Checked&&
                  third.Controls.Find("SPTC_PUBLISH_PDF_ENABLED",true)
                    .OfType<CheckBox>().Single().Checked,
                    "second Save never resets persisted PDF checkbox");
            }
        }
        finally
        {
            if(File.Exists(file))File.Delete(file);
            Directory.Delete(root,true);
        }
        Console.WriteLine(
            "Production/PDF checkbox real Save-click handler, auto-enable master, disk roundtrip and dialog reopen PASS");
    }

    private static void TestBatchProgressReporting()
    {
        using var progress=new SupportDrawingProgressForm35();
        Check(progress.AutoScaleMode==AutoScaleMode.Dpi,
            "progress window remains DPI-aware");
        progress.SetWorkload(3);
        progress.AdvanceWorkload(true);
        progress.AdvanceWorkload(false);
        var count=progress.Controls.Find("SPTC_PROGRESS_COUNTS",
            searchAllChildren:false).OfType<Label>().Single();
        Check(count.Text.Contains("Processed 2 / 3")&&
            count.Text.Contains("Passed 1")&&
            count.Text.Contains("Failed 1"),
            "partial batch records processed, passed and failed separately");
        progress.AdvanceWorkload(true);
        progress.Complete();
        Check(count.Text.Contains("Finished 3 / 3")&&
            count.Text.Contains("Passed 2")&&
            count.Text.Contains("Failed 1"),
            "progress completes at actual processed count, not all-pass");
        Console.WriteLine(
            "Production progress correctly reports successful and failed jobs PASS");
    }

    private static void Check(bool condition,string message)
    {
        if(!condition)throw new Exception("REV22.40 UI FAIL: "+message);
    }
}
