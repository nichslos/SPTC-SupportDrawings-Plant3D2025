using System;
using System.Drawing;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Windows;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.40 persistent modeless, dockable AutoCAD palette. A single UserControl
/// is kept alive per AutoCAD session and retains the settings between drawings.
/// Palette button callbacks never perform native Ortho, Editor selection, or
/// project mutation themselves: they only queue one of the four existing
/// registered AutoCAD commands using SendStringToExecute.
/// </summary>
internal static class SupportDrawingPalette40
{
    private static PaletteSet? palette;
    private static SupportDrawingManagerForm35? manager;
    private static Document? sourceDocument;
    private static string sourceProjectRoot = "";
    private static SupportDrawingUiOptions? pendingOrtho;
    private static string? externalCommand;
    private static Document? externalDocument;
    private static bool busy;

    internal static bool IsBusy => busy;
    internal static bool HasPendingOrtho => pendingOrtho!=null;

    internal static void ShowOrFocus(Document doc,string projectRoot,
        string orthoFolder)
    {
        string source=doc.Database.Filename??"";
        if(manager==null || manager.IsDisposed || palette==null)
        {
            manager=new SupportDrawingManagerForm35(source,projectRoot,orthoFolder)
            {
                Dock=System.Windows.Forms.DockStyle.Fill
            };
            manager.RunRequested+=QueueFromPalette;
            // Fixed floating dimensions; user may dock left or right.
            // No AutoHide: remain open while CAD selection/generation runs.
            palette=new PaletteSet("SPTC Support Drawing Manager",
                "SPTC_SUPPORTORTHO10",
                new Guid("F220F3E7-582A-476E-8613-6AD3F2519B40"))
            {
                Style=PaletteSetStyles.ShowCloseButton |
                      PaletteSetStyles.ShowPropertiesMenu |
                      PaletteSetStyles.Snappable,
                DockEnabled=DockSides.Left | DockSides.Right,
                Dock=DockSides.None,
                // Narrow docking is safe: the hosted manager retains
                // a full scrollable, DPI-aware design canvas so no
                // production option or Run/Preflight button is clipped.
                MinimumSize=new Size(390,480),
                Size=new Size(840,830)
            };
            palette.Add("SPTC Support Drawings",manager);
        }
        else
        {
            if(!busy)manager.UpdateProjectContext(source,projectRoot,orthoFolder);
        }

        sourceDocument=doc;
        sourceProjectRoot=projectRoot;
        palette.Visible=true;
    }

    private static void QueueFromPalette(SupportDrawingUiOptions options)
    {
        if(busy)
        {
            manager?.SetStatus("SPTC operation already running. Wait for completion.");
            return;
        }

        Document? doc=AcadApp.DocumentManager.MdiActiveDocument;
        string? source=doc?.Database.Filename;
        if(doc==null || string.IsNullOrWhiteSpace(source) ||
           !System.IO.File.Exists(source))
        {
            manager?.SetStatus(
                "Open and save a project-registered 3D source drawing before Run.");
            return;
        }
        // Prevent a palette that was opened for one project from running an
        // arbitrary DWG in another project with the old seed/settings.
        if(!NativeOrthoRegistrationNames.IsInsideProjectDirectory(
            source,sourceProjectRoot))
        {
            manager?.SetStatus("Active DWG belongs to a different project. "+
                "Run SPTC_SUPPORTORTHO10 again to rebind the palette.");
            return;
        }
        sourceDocument=doc;
        busy=true;
        manager?.SetRunning(true);
        string command=options.Operation switch
        {
            "ortho"=>"SPTC_SUPPORTORTHO10",
            "export"=>"SPTC_SUPPORTEXPORT",
            "batch"=>"SPTC_SUPPORTBATCH",
            "2dbatch"=>"SPTC_SUPPORT2DBATCH",
            _=>""
        };
        if(command.Length==0)
        {
            Finish("Unknown SPTC operation; nothing was started.");
            return;
        }

        try
        {
            if(options.Operation=="ortho")
                pendingOrtho=options;
            else
            {
                externalCommand=command;
                externalDocument=doc;
                doc.CommandEnded+=OnExternalCommandEnded;
                doc.CommandCancelled+=OnExternalCommandCancelled;
                doc.CommandFailed+=OnExternalCommandFailed;
            }
            // Schedule a real AutoCAD command. The palette remains modeless;
            // native object-selection and GenerateOrthos execute in the same
            // AutoCAD command context as REV22.39, never in a palette click.
            doc.SendStringToExecute(command+" ",true,false,false);
        }
        catch(System.Exception ex)
        {
            Finish("SPTC command could not be queued: "+ex.Message);
        }
    }

    internal static bool TryTakeQueuedOrtho(Document current,
        out SupportDrawingUiOptions? options)
    {
        options=null;
        if(pendingOrtho==null)return false;
        if(!ReferenceEquals(current,sourceDocument))
        {
            Finish("SPTC Ortho not started: the active drawing changed.");
            return false;
        }
        options=pendingOrtho;
        pendingOrtho=null;
        return true;
    }

    private static void OnExternalCommandEnded(object sender,CommandEventArgs e)
        => CompleteExternal(e.GlobalCommandName,"completed");

    private static void OnExternalCommandCancelled(object sender,CommandEventArgs e)
        => CompleteExternal(e.GlobalCommandName,"cancelled");

    private static void OnExternalCommandFailed(object sender,CommandEventArgs e)
        => CompleteExternal(e.GlobalCommandName,"failed");

    private static void CompleteExternal(string? actual,string outcome)
    {
        if(externalCommand==null || !string.Equals(
            actual?.TrimStart('_','.'),externalCommand,
            StringComparison.OrdinalIgnoreCase))return;
        string command=externalCommand;
        Finish(command+" "+outcome+". Review the exported drawings/logs.");
    }

    internal static void Finish(string message)
    {
        pendingOrtho=null;
        DetachExternal();
        busy=false;
        if(manager!=null && !manager.IsDisposed)
        {
            manager.SetRunning(false);
            manager.SetStatus(message);
        }
    }

    private static void DetachExternal()
    {
        if(externalDocument!=null)
        {
            try
            {
                externalDocument.CommandEnded-=OnExternalCommandEnded;
                externalDocument.CommandCancelled-=OnExternalCommandCancelled;
                externalDocument.CommandFailed-=OnExternalCommandFailed;
            }
            catch(System.Exception) { /* closing document: best effort */ }
        }
        externalDocument=null;
        externalCommand=null;
    }

    internal static void Shutdown()
    {
        DetachExternal();
        pendingOrtho=null;
        busy=false;
        if(manager!=null && !manager.IsDisposed)
        {
            manager.RunRequested-=QueueFromPalette;
        }
        try {palette?.Dispose();}catch(System.Exception){}
        palette=null;
        manager=null;
        sourceDocument=null;
        sourceProjectRoot="";
    }
}
