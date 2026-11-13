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
