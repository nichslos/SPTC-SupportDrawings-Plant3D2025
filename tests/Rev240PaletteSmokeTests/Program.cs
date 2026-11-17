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
        Check(manager.Controls.Find("SPTC_UI_LAYOUT_SURFACE",true)
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
        TestRev77SessionHistoryAndCollapse();
        TestRev78GuidedSetup();
        TestRev79ResponsiveUx();
        Rev83OrthoAppearanceTests.Run();
        Rev84AppearanceSyncTests.Run();
        Rev85NonblockingAppearanceTests.Run();
        TestBatchProgressReporting();
        TestProductionPackageCheckboxSaveRoundTrip();
        var surface=manager.Controls.Find(
            "SPTC_UI_LAYOUT_SURFACE",true).Single();
        var nativeTabs=surface.Controls.OfType<TabControl>().Single();
        var configButton=AllControls(nativeTabs.TabPages[0])
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

    private static System.Collections.Generic.IEnumerable<Control>
        AllControls(Control parent)
    {
        foreach(Control child in parent.Controls)
        {
            yield return child;
            foreach(Control descendant in AllControls(child))
                yield return descendant;
        }
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
                searchAllChildren:true).Single();
            var tabs=canvas.Controls.OfType<TabControl>().Single();
            var workspace=ui.Controls.OfType<Panel>()
                .Single(p=>p.Dock==DockStyle.Fill);
            var footer=ui.Controls.OfType<Panel>()
                .Single(p=>p.Dock==DockStyle.Bottom);
            ui.CreateControl();
            ui.PerformLayout();
            footer.PerformLayout();
            Check(ui.AutoScaleMode==AutoScaleMode.Dpi&&workspace.AutoScroll,
                "only the workspace scrolls; footer stays docked");
            Check(tabs.TabCount==4&&tabs.TabPages[0].AutoScroll,
                "all four production tabs and inner Ortho scrolling retained");
            var run=footer.Controls.OfType<Button>()
                .Single(x=>x.Text=="Run selected");
            var preflight=footer.Controls.OfType<Button>()
                .Single(x=>x.Text=="Preflight");
            var summary=footer.Controls.OfType<TextBox>()
                .Single(x=>x.Multiline&&x.ReadOnly);
            Check(run.Bounds.Right<=footer.ClientSize.Width&&
                run.Bounds.Bottom<=footer.ClientSize.Height&&
                preflight.Bounds.Right<=footer.ClientSize.Width&&
                preflight.Bounds.Bottom<=footer.ClientSize.Height,
                "sticky commands remain fully visible in footer"+
                $" (scale={scale}, footer={footer.ClientSize}, "+
                $"run={run.Bounds}, preflight={preflight.Bounds})");
            Check(!run.Bounds.IntersectsWith(summary.Bounds)&&
                !preflight.Bounds.IntersectsWith(summary.Bounds),
                "sticky actions cannot obscure the preflight text");
            Check(AllControls(tabs.TabPages[0])
                .OfType<Button>()
                .Count(b=>b.Text.StartsWith("[-]")||b.Text.StartsWith("[+]"))==7,
                "all seven collapsible sections preserved");
            int narrowW=Math.Max(ui.MinimumSize.Width+15,
                canvas.Width/2);
            int narrowH=Math.Max(ui.MinimumSize.Height+15,
                canvas.Height/2);
            ui.ClientSize=new Size(narrowW,narrowH);
            ui.PerformLayout();
            Check(workspace.AutoScroll&&tabs.TabPages[0].AutoScroll,
                "narrow palette retains working independent scroll areas");
            Check(run.Parent==footer&&preflight.Parent==footer,
                "Run and Preflight never scroll out of the palette");
        }
        Console.WriteLine(
            "REV77 sticky footer and seven collapsible sections at 100/125/150% DPI PASS");
    }

    private static void TestRev77SessionHistoryAndCollapse()
    {
        string root=Path.Combine(Path.GetTempPath(),
            "SPTC_REV77_UI_"+Guid.NewGuid().ToString("N"));
        string ortho=Path.Combine(root,"Orthos");
        string folder=Path.Combine(ortho,"DWGs");
        Directory.CreateDirectory(folder);
        try
        {
            using var ui=new SupportDrawingManagerForm35(
                Path.Combine(root,"MODEL.dwg"),root,ortho);
            var hist=AllControls(ui).OfType<ListBox>()
                .Single(b=>b is not CheckedListBox);
            var qa=AllControls(ui).OfType<TextBox>()
                .Single(b=>b.Multiline&&b.ReadOnly&&
                    b.Text.Contains("No completed",StringComparison.Ordinal));
            string first=Path.Combine(folder,"FIRST.dwg");
            string second=Path.Combine(folder,"SECOND.dwg");
            File.WriteAllText(first,"test artifact");
            File.WriteAllText(second,"test artifact");
            File.WriteAllText(Path.ChangeExtension(first,null)+"_RESULT.txt",
                "REV75_DRAFT_OVERFLOW_ACTIVE=True\n"+
                "NATIVE_ORTHO_VIEW_COUNT=10\n"+
                "REV22_41_DIMENSIONS_CREATED=33\n"+
                "STEP10_ACTUAL_VIEWPORT_OVERLAP_DETECTED=False\n"+
                "FINAL_STATUS=TEN_NATIVE_VIEWS_AND_TITLES_MEMBER_LEADERS_PASS_DRAFT_ONLY\n");
            ui.RecordGeneratedDwg(first);
            var openDwg=AllControls(ui).OfType<Button>()
                .Single(b=>b.Name=="SPTC_OPEN_DWG_79");
            Check(hist.Items.Count==1&&hist.SelectedIndex==0&&
                  qa.Text.Contains("Dimensions: 33",StringComparison.Ordinal)&&
                  openDwg.Enabled&&
                  (hist.Items[0]?.ToString()??"").Contains("DRAFT",
                      StringComparison.Ordinal),
                "real DWG, timestamp/output label, and matching QA navigation available");
            ui.SetRunning(true);
            Check(hist.Items.Count==1,
                "starting next command preserves project session history");
            ui.SetRunning(false);
            ui.RecordGeneratedDwg(second);
            Check(hist.Items.Count==2,
                "multiple real DWGs appended, even if later report missing");
            hist.SelectedIndex=0;
            Check(qa.Text.Contains("OFF-SHEET DRAFT",
                     StringComparison.Ordinal),
                "history selection restores original DWG-specific QA");
            var tab=AllControls(ui).OfType<TabControl>().Single()
                .TabPages[0];
            var dim=AllControls(tab).OfType<Button>()
                .Single(b=>b.Text.Contains("Dimensions",
                    StringComparison.Ordinal));
            Check(dim.Text.StartsWith("[+]",StringComparison.Ordinal),
                "dimensions start collapsed without discarding controls");
            ui.CreateControl();
            ui.PerformLayout();
            tab.ScrollControlIntoView(dim);
            dim.PerformClick();
            Check(dim.Text.StartsWith("[-]",StringComparison.Ordinal),
                "collapsible section expands when clicked after scrolling"+
                $" (Visible={dim.Visible}, CanSelect={dim.CanSelect})");
            dim.PerformClick();
            Check(dim.Text.StartsWith("[+]",StringComparison.Ordinal),
                "collapsible section closes again");
            string next=Path.Combine(Path.GetTempPath(),
                "SPTC_REV77_OTHER_"+Guid.NewGuid().ToString("N"));
            ui.UpdateProjectContext(Path.Combine(next,"MODEL.dwg"),
                next,Path.Combine(next,"Orthos"));
            Check(hist.Items.Count==0,
                "switching projects clears all previous project history");
            Console.WriteLine(
                "REV77 history, real QA selection, retention, collapse and project isolation PASS");
        }
        finally
        {
            try{Directory.Delete(root,recursive:true);}
            catch(IOException){/* no native DWG files held by this test */}
        }
    }

    private static void TestRev78GuidedSetup()
    {
        using var ui=new SupportDrawingManagerForm35(
            @"C:\Projects\REV78\MODEL.dwg",
            @"C:\Projects\REV78",
            @"C:\Projects\REV78\Orthos");
        ui.CreateControl();
        ui.PerformLayout();
        var draft=AllControls(ui).OfType<Button>()
            .Single(b=>b.Name=="SPTC_QUICK_DRAFT_78");
        var normal=AllControls(ui).OfType<Button>()
            .Single(b=>b.Name=="SPTC_QUICK_PHYSICAL_78");
        var basics=AllControls(ui).OfType<Button>()
            .Single(b=>b.Name=="SPTC_SHOW_BASICS_78");
        var all=AllControls(ui).OfType<Button>()
            .Single(b=>b.Name=="SPTC_SHOW_ALL_78");
        var readiness=AllControls(ui).OfType<Label>()
            .Single(x=>x.Name=="SPTC_READINESS_HINT_78");
        var checks=AllControls(ui).OfType<CheckedListBox>().Single();
        var output=AllControls(ui).OfType<ComboBox>()
            .Single(x=>x.Items.Contains(SupportDrawingOutputMode76.Draft));
        var scale=AllControls(ui).OfType<ComboBox>()
            .Single(x=>x.Items.Contains("1:20"));
        Check(output.SelectedIndex==0&&checks.CheckedItems.Count==10,
            "safe physical mode and ten native view defaults are preserved");
        scale.SelectedItem="1:20";
        AllControls(ui).OfType<Button>()
            .Single(b=>b.Text=="Ortho 6").PerformClick();
        Check(checks.CheckedItems.Count==6,
            "native view subset still works before guided shortcut");
        draft.PerformClick();
        Check(checks.CheckedItems.Count==10&&
              output.SelectedIndex==1&&scale.Text=="1:20",
            "guided draft checks all ten without silently overriding 1:20");
        Check(readiness.Text.Contains("DRAFT ONLY",
            StringComparison.Ordinal)&&
            readiness.Text.Contains("native PDF blocked",
                StringComparison.Ordinal),
            "draft readiness shows an unambiguous no-PDF warning");
        SupportDrawingUiOptions? queued=null;
        ui.RunRequested+=options=>queued=options;
        typeof(SupportDrawingManagerForm35).GetMethod(
            "RunSelected",BindingFlags.Instance|BindingFlags.NonPublic)!
            .Invoke(ui,null);
        Check(queued?.AllowDraftOverflow==true&&
              queued.BypassOneSheetLimit==false&&
              queued.ScaleDenominator==20&&queued.Views.Count==10,
            "guided draft reaches the original native options unchanged");
        scale.SelectedItem="Automatic";
        var footer=AllControls(ui).OfType<Panel>()
            .Single(x=>x.Dock==DockStyle.Bottom);
        var run=footer.Controls.OfType<Button>()
            .Single(x=>x.Text=="Run selected");
        Check(!run.Enabled&&readiness.Text.Contains("Action required",
            StringComparison.Ordinal),
            "automatic scale is clearly blocked for draft rather than silently changed");
        scale.SelectedItem="1:20";
        all.PerformClick();
        Check(AllControls(ui).OfType<Button>()
                .Count(b=>b.Text.StartsWith("[-]"))==7,
            "Show all reveals all seven existing setting groups");
        basics.PerformClick();
        Check(AllControls(ui).OfType<Button>()
                .Count(b=>b.Text.StartsWith("[-]"))==3,
            "Show essentials reduces clutter without resetting values");
        normal.PerformClick();
        Check(output.SelectedIndex==0&&scale.Text=="1:20"&&
              checks.CheckedItems.Count==10&&run.Enabled,
            "printable shortcut returns to safe physical mode without changing scale/views");
        footer.Controls.OfType<Button>()
            .Single(b=>b.Text=="Preflight").PerformClick();
        var summary=footer.Controls.OfType<TextBox>()
            .Single(x=>x.Multiline&&x.ReadOnly);
        Check(summary.Text.Contains("READ-ONLY CONFIGURATION PREFLIGHT")&&
              summary.Text.Contains("1. VIEWS:")&&
              summary.Text.Contains("3. OUTPUT:"),
            "preflight uses a numbered, readable UI-only setup summary");
        Console.WriteLine(
            "REV78 guided modes, no silent scale changes, clear warnings, basics/all, preflight PASS");
    }

    private static void TestRev79ResponsiveUx()
    {
        using var ui=new SupportDrawingManagerForm35(
            @"C:\Projects\REV79\MODEL.dwg",
            @"C:\Projects\REV79",
            @"C:\Projects\REV79\Orthos");
        ui.CreateControl();
        ui.PerformLayout();
        var workspace=ui.Controls.OfType<Panel>()
            .Single(p=>p.Dock==DockStyle.Fill);
        var footer=ui.Controls.OfType<Panel>()
            .Single(p=>p.Dock==DockStyle.Bottom);
        var canvas=ui.Controls.Find("SPTC_UI_LAYOUT_SURFACE",true).Single();
        var tabs=canvas.Controls.OfType<TabControl>().Single();
        var page=tabs.TabPages[0];
        var cube=AllControls(page).OfType<ComboBox>()
            .Single(x=>x.Items.Contains("SupportFocus (recommended)"));
        var paper=AllControls(page).OfType<ComboBox>()
            .Single(x=>x.Items.Contains("1:20"));
        var output=AllControls(page).OfType<ComboBox>()
            .Single(x=>x.Items.Contains(SupportDrawingOutputMode76.Draft));
        var selected=AllControls(page).OfType<CheckedListBox>().Single();
        var sticky=footer.Controls.OfType<Label>()
            .Single(x=>x.Name=="SPTC_FOOTER_SUMMARY_79");
        var fix=footer.Controls.OfType<Button>()
            .Single(x=>x.Name=="SPTC_FIX_SETTINGS_79");
        var run=footer.Controls.OfType<Button>()
            .Single(x=>x.Text=="Run selected");

        ui.ClientSize=new Size(410,650);
        ui.PerformLayout();
        workspace.PerformLayout();
        page.PerformLayout();
        Check(canvas.Width<=workspace.ClientSize.Width+5,
            "narrow palette shrinks the reference canvas instead of horizontal 805px overflow");
        Check(cube.Width>=240&&paper.Width>=240&&
              paper.Top>cube.Top&&
              page.AutoScrollMinSize.Width<=tabs.Width,
            "native source and scale controls stack in one usable column");
        Check(sticky.Text.Contains("10/10 views")&&
              sticky.Text.Contains("Automatic"),
            "sticky summary always reflects current views and paper scale");

        paper.SelectedItem="1:10";
        output.SelectedIndex=(int)SupportDrawingOutputMode76.Mode.Draft;
        Check(sticky.Text.Contains("1:10")&&
              sticky.Text.Contains("PDF BLOCKED")&&run.Enabled,
            "live summary explicitly warns of offsheet draft PDF block");

        var header=AllControls(page).OfType<Button>()
            .Single(b=>b.Text.Contains("Source, Scale & Seed",
                StringComparison.Ordinal));
        Check(header.Text.Contains("1:10")&&
              AllControls(page).OfType<Button>().Any(b=>
                  b.Text.Contains("10/10 views",StringComparison.Ordinal)),
            "collapsed and expanded section headers contain live setting summaries");
        page.ScrollControlIntoView(header);
        header.PerformClick();
        Check(header.Text.StartsWith("[+]",StringComparison.Ordinal),
            "source section can collapse without clearing data");
        paper.SelectedItem="Automatic";
        Check(!run.Enabled&&
              sticky.Text.Contains("Automatic")&&
              fix.Text=="Fix setting",
            "invalid scale disables Run while keeping fix navigation available");
        var focus=typeof(SupportDrawingManagerForm35).GetMethod(
            "FocusRequiredSetting",BindingFlags.Instance|BindingFlags.NonPublic)!;
        focus.Invoke(ui,null);
        Check(header.Text.StartsWith("[-]",StringComparison.Ordinal)&&
              paper.SelectedItem?.ToString()=="Automatic",
            "Fix setting opens source-scale section without changing user scale");
        paper.SelectedItem="1:10";
        ui.ClientSize=new Size(805,765);
        ui.PerformLayout();
        workspace.PerformLayout();
        Check(canvas.Width>=740&&
              cube.Width<240&&run.Enabled&&selected.CheckedItems.Count==10,
            "undocking restores original two-column source controls and view choices");
        Console.WriteLine(
            "REV79 responsive narrow/wide, live summary, section details and Fix Setting PASS");
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
