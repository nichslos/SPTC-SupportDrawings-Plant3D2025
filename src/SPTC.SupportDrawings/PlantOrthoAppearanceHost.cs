using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Autodesk.AutoCAD.DatabaseServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace SPTC.SupportDrawings;

internal sealed class PlantOrthoAppearanceHost
{
    private readonly Assembly orthoUi;

    internal PlantOrthoAppearanceHost(Assembly orthoUi) => this.orthoUi = orthoUi;

    internal static int Read(string variable) => AcadApp.GetSystemVariable(variable) switch
    {
        short value => value,
        int value => value,
        _ => throw new InvalidOperationException("Plant 3D did not return an integer for " + variable + ".")
    };

    internal static void Write(string variable, int value) =>
        AcadApp.SetSystemVariable(variable, checked((short)value));

    internal int HiddenValue(string enumName)
    {
        Type mode = DrawingType("PnPDwg2dGen", "OrthoHiddenLineMode");
        return Convert.ToInt32(Enum.Parse(mode, enumName, ignoreCase: false));
    }

    internal int CheckSavedHidden(Database database, string viewName, ObjectId viewportId, int expected)
    {
        // This is the same sheet and named IDwgView lookup used by native Edit View.
        Type dbx = DrawingType("PnPDwg2dDbx", "Dwg2dDbx");
        MethodInfo getSheet = dbx.GetMethod("GetSheet", new[] { typeof(Database) }) ??
            throw new MissingMethodException(dbx.FullName, "GetSheet(Database)");
        object sheet = getSheet.Invoke(null, new object[] { database }) ??
            throw new InvalidOperationException("Native sheet unavailable.");
        MethodInfo getView = getSheet.ReturnType.GetMethod("GetView", new[] { typeof(string) }) ??
            throw new MissingMethodException(getSheet.ReturnType.FullName, "GetView(string)");
        object view = getView.Invoke(sheet, new object[] { viewName }) ??
            throw new InvalidOperationException("Native view unavailable: " + viewName);
        Type viewType = getView.ReturnType;
        if (viewType.GetProperty("ViewportId")?.GetValue(view) is not ObjectId id || id != viewportId)
            throw new InvalidOperationException("Native viewport does not match " + viewName + ".");
        PropertyInfo mode = viewType.GetProperty("HiddenLineMode") ??
            throw new MissingMemberException(viewType.FullName, "HiddenLineMode");
        int ReadMode() => mode.GetValue(view) is int value ? value :
            throw new InvalidOperationException("Native view did not return its hidden-line mode.");
        int saved = ReadMode();
        // Plant returns -1 if the newly generated view has no mode record.
        // Use its own transactional setter only for that case.
        if (saved != -1) return saved;
        mode.SetValue(view, expected);
        return ReadMode();
    }

    private Type DrawingType(string assemblyName, string typeName)
    {
        // Load Plant's own referenced assembly in the same context as its Ortho Editor.
        AssemblyName name = orthoUi.GetReferencedAssemblies().Single(a => a.Name == assemblyName);
        AssemblyLoadContext context = AssemblyLoadContext.GetLoadContext(orthoUi)!;
        return context.LoadFromAssemblyName(name).GetType(
            "Autodesk.ProcessPower.Drawings2d." + typeName, throwOnError: true)!;
    }
}
