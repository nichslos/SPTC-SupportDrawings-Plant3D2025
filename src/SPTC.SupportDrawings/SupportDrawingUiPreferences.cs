using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SPTC.SupportDrawings;

/// <summary>
/// Optional, revision-independent local preferences for the modeless UI.
/// Never stores source handles, DWG identifiers, project seed paths, CAD
/// geometry, or queued commands. Preferences are isolated per project.
/// </summary>
internal static class SupportDrawingUiPreferences
{
    internal static readonly string[] AllViews =
        {"TOP","BOTTOM","LEFT","RIGHT","FRONT","BACK","NW","NE","SW","SE"};

    internal sealed record Values(
        string ScaleSelection,int CustomScale,string DrawingPrefix,
        bool SupportFocus,string[] Views,bool LineNumber,bool Bop,
        bool Tos,bool Description,bool XrefMemberNames,
        bool AutoDimLinear,bool ReferenceDetailDimensions,
        bool BatchOrtho);

    internal static Values Defaults => new(
        SupportDrawingPaperScale39.Automatic,25,"",true,
        AllViews.ToArray(),true,true,true,true,true,true,true,false);

    internal static string[] ViewPreset(string name) => name switch
    {
        "All" => AllViews.ToArray(),
        "Orthographic" => AllViews.Take(6).ToArray(),
        "Isometric" => AllViews.Skip(6).ToArray(),
        "None" => Array.Empty<string>(),
        _ => throw new ArgumentException("Unknown view preset.",nameof(name))
    };

    internal static Values Validate(Values value)
    {
        if(value==null)throw new ArgumentNullException(nameof(value));
        if(value.Views==null||value.Views.Length==0||
           value.Views.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=
               value.Views.Length||
           value.Views.Any(x=>!AllViews.Contains(x,
               StringComparer.OrdinalIgnoreCase)))
            throw new ArgumentException(
                "Select at least one known native view without duplicates.");
        if(value.CustomScale<SupportDrawingPaperScale39.Min||
           value.CustomScale>SupportDrawingPaperScale39.Max)
            throw new ArgumentException("Custom paper scale is invalid.");
        SupportDrawingPaperScale39.Resolve(
            value.ScaleSelection,value.CustomScale);
        string prefix=value.DrawingPrefix??"";
        if(prefix.Length>48||prefix.Contains("..",StringComparison.Ordinal)||
           prefix.EndsWith(".",StringComparison.Ordinal)||
           prefix.IndexOfAny("<>:\"/\\|?*".ToCharArray())>=0||
           prefix.Any(char.IsControl))
            throw new ArgumentException("Saved drawing prefix is invalid.");
        // Stable canonical order; never restore a nonstandard camera.
        return value with {
            Views=AllViews.Where(v=>value.Views.Contains(v,
                StringComparer.OrdinalIgnoreCase)).ToArray(),
            DrawingPrefix=prefix
        };
    }

    internal static string Serialize(Values value) =>
        JsonSerializer.Serialize(Validate(value));

    internal static Values Deserialize(string json)
    {
        var result=JsonSerializer.Deserialize<Values>(json);
        return Validate(result??throw new ArgumentException(
            "Saved UI preferences are empty."));
    }

    internal static string StoragePath(string projectRoot)
    {
        if(string.IsNullOrWhiteSpace(projectRoot))
            throw new ArgumentException("Project root is required.");
        string full=Path.GetFullPath(projectRoot)
            .TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar).ToUpperInvariant();
        var hash=Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(full)));
        return Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "SPTC","SupportDrawings","ProjectPreferences",
            hash+".json");
    }

    // No preferences are written automatically during drawing generation.
    // This method executes only from the explicit Save settings button.
    internal static void Save(string projectRoot,Values value)
    {
        string path=StoragePath(projectRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string tmp=path+"."+Guid.NewGuid().ToString("N",
            CultureInfo.InvariantCulture)+".tmp";
        try
        {
            File.WriteAllText(tmp,Serialize(value),new UTF8Encoding(false));
            File.Move(tmp,path,overwrite:true);
        }
        finally
        {
            if(File.Exists(tmp))File.Delete(tmp);
        }
    }

    internal static bool TryLoad(string projectRoot,out Values value)
    {
        value=Defaults;
        try
        {
            string path=StoragePath(projectRoot);
            if(!File.Exists(path))return false;
            value=Deserialize(File.ReadAllText(path,Encoding.UTF8));
            return true;
        }
        catch(Exception ex) when (ex is IOException or
            UnauthorizedAccessException or ArgumentException or
            JsonException or NotSupportedException)
        {
            // Malformed/stale files must never stop Plant 3D or a
            // production SPTC drawing. Fall back to validated defaults.
            value=Defaults;
            return false;
        }
    }
}
