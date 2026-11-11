using System;
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

        Console.WriteLine("REV22.40 persistent modeless UI/settings and busy tests PASS");
    }

    private static void Check(bool condition,string message)
    {
        if(!condition)throw new Exception("REV22.40 UI FAIL: "+message);
    }
}
