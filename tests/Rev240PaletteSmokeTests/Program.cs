using System;
using System.Drawing;
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
        Check(manager.Controls.OfType<TabControl>().Single().TabCount==4,
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
