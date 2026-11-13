using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace SPTC.SupportDrawings;

/// <summary>
/// Explicit opt-in, project-local-on-this-PC production settings.
/// Kept outside Plant Project Manager and outside the native DWG.
/// </summary>
internal static class SupportProductionPackageSettingsStore
{
    internal static string PathForProject(string projectRoot)
    {
        string ui=SupportDrawingUiPreferences.StoragePath(projectRoot);
        return System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(ui)!,
            System.IO.Path.GetFileNameWithoutExtension(ui)+".production.json");
    }

    internal static void Save(string projectRoot,
        SupportProductionPackagePlan.Settings config)
    {
        var valid=SupportProductionPackagePlan.Validate(config);
        SupportProductionPackagePlan.WriteAtomic(PathForProject(projectRoot),
            JsonSerializer.Serialize(valid,
                new JsonSerializerOptions {WriteIndented=true}));
    }

    // A successful write is not enough: confirm the exact project-scoped
    // checkbox values survive a real disk read before closing the dialog.
    // This detects wrong paths, truncated or stale settings and prevents
    // silently returning to CONFIGURED_DISABLED after the user checks a box.
    internal static SupportProductionPackagePlan.Settings SaveAndVerify(
        string projectRoot,SupportProductionPackagePlan.Settings config)
    {
        var expected=SupportProductionPackagePlan.Validate(config);
        Save(projectRoot,expected);
        if(!TryLoad(projectRoot,out var actual,out string reason)||
           actual.Enabled!=expected.Enabled||
           actual.PublishPdf!=expected.PublishPdf||
           !string.Equals(actual.PdfPlotDevice,expected.PdfPlotDevice,
               StringComparison.OrdinalIgnoreCase)||
           !string.Equals(actual.ProjectCode,expected.ProjectCode,
               StringComparison.Ordinal)||
           !string.Equals(actual.DrawingSeries,expected.DrawingSeries,
               StringComparison.Ordinal)||
           !string.Equals(actual.Revision,expected.Revision,
               StringComparison.Ordinal))
            throw new IOException("Production package settings could not be "+
                "verified after saving ("+reason+"). No settings were confirmed.");
        return actual;
    }

    internal static bool TryLoad(string projectRoot,
        out SupportProductionPackagePlan.Settings config,out string diagnostic)
    {
        config=SupportProductionPackagePlan.Defaults;
        diagnostic="NOT_CONFIGURED";
        try
        {
            string file=PathForProject(projectRoot);
            if(!File.Exists(file))return false;
            var raw=JsonSerializer.Deserialize<
                SupportProductionPackagePlan.Settings>(
                    File.ReadAllText(file,Encoding.UTF8));
            if(raw==null)throw new ArgumentException(
                "Empty production package configuration.");
            config=SupportProductionPackagePlan.Validate(raw);
            diagnostic=config.Enabled?"CONFIGURED_ENABLED":"CONFIGURED_DISABLED";
            return true;
        }
        catch(Exception ex) when (ex is IOException or ArgumentException or
            JsonException or UnauthorizedAccessException or
            System.Security.SecurityException or NotSupportedException)
        {
            config=SupportProductionPackagePlan.Defaults;
            diagnostic="INVALID_SETTINGS_DISABLED:"+ex.GetType().Name;
            return false;
        }
    }
}
