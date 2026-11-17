using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV86: read-only discovery of Plant 3D's actual native output-appearance
/// storage. REV85 proved that a session system-variable readback is not a
/// verified saved IDwgViewDef appearance setting. This diagnostic never
/// writes host variables, view definitions, PnP/Ortho databases or geometry.
/// It cannot make an unsupported appearance choice appear to succeed.
/// </summary>
internal static class NativeOrthoAppearanceProbe86
{
    private const BindingFlags All=BindingFlags.Public|
        BindingFlags.NonPublic|BindingFlags.Instance;
    private const BindingFlags Static=BindingFlags.Public|
        BindingFlags.NonPublic|BindingFlags.Static;

    internal static bool IsAppearanceMember(string name)=>
        new[]{"hidden","insulat","appear","display","render","occlus","linemode",
            "style"}.Any(s=>name.Contains(s,StringComparison.OrdinalIgnoreCase));

    // Pure metadata summary: safe to unit-test without AutoCAD installed.
    internal static IReadOnlyList<string> Describe(Type type)
    {
        var properties=type.GetProperties(All)
            .Where(p=>IsAppearanceMember(p.Name))
            .OrderBy(p=>p.Name,StringComparer.Ordinal)
            .Select(p=>p.Name+" TYPE="+p.PropertyType.FullName+
                " CAN_READ="+p.CanRead+" CAN_WRITE="+p.CanWrite);
        var fields=type.GetFields(All)
            .Where(f=>IsAppearanceMember(f.Name))
            .OrderBy(f=>f.Name,StringComparer.Ordinal)
            .Select(f=>f.Name+" TYPE="+f.FieldType.FullName+
                " FIELD_READONLY="+f.IsInitOnly);
        var methods=type.GetMethods(All)
            .Where(m=>IsAppearanceMember(m.Name)&&!m.IsSpecialName)
            .OrderBy(m=>m.Name,StringComparer.Ordinal)
            .Select(m=>m.Name+" METHOD="+m.ToString());
        return properties.Concat(fields).Concat(methods).Distinct().Take(40).ToArray();
    }

    internal static void Inspect(object nativeView,string viewName,
        Action<string> report)
    {
        if(nativeView==null)throw new ArgumentNullException(nameof(nativeView));
        if(report==null)throw new ArgumentNullException(nameof(report));
        if(string.IsNullOrWhiteSpace(viewName))
            throw new ArgumentException("Native view name missing.",nameof(viewName));

        try
        {
            ReportType(nativeView,"PLANT_ORTHO_VIEW",report);
            Type? utils=AppDomain.CurrentDomain.GetAssemblies()
                .Select(a=>a.GetType(
                    "Autodesk.ProcessPower.PnP3dOrthoDrawingsUI.UIUtils",
                    throwOnError:false))
                .FirstOrDefault(t=>t!=null);
            if(utils==null)
            {
                report("REV86_VIEWDEF_PROBE=UNAVAILABLE:UIUtils not loaded");
                return;
            }

            MethodInfo? sheetMethod=utils.GetMethods(Static).FirstOrDefault(m=>
            {
                if(m.Name!="GetDwgSheetFromOrthoView"||
                   m.ReturnType!=typeof(bool))return false;
                ParameterInfo[] p=m.GetParameters();
                return p.Length==2&&
                    p[0].ParameterType.IsInstanceOfType(nativeView)&&
                    p[1].ParameterType.IsByRef;
            });
            if(sheetMethod==null)
            {
                report("REV86_VIEWDEF_PROBE=UNAVAILABLE:native sheet reader missing");
                return;
            }
            object?[] sheetArgs={nativeView,null};
            if(sheetMethod.Invoke(null,sheetArgs) is not bool foundSheet||
               !foundSheet||sheetArgs[1]==null)
            {
                report("REV86_VIEWDEF_PROBE=UNAVAILABLE:native sheet absent");
                return;
            }
            object sheet=sheetArgs[1]!;
            MethodInfo? viewMethod=utils.GetMethods(Static).FirstOrDefault(m=>
            {
                if(m.Name!="GetViewDefByViewName"||
                   m.ReturnType!=typeof(bool))return false;
                ParameterInfo[] p=m.GetParameters();
                return p.Length==3&&
                    p[0].ParameterType==typeof(string)&&
                    p[1].ParameterType.IsInstanceOfType(sheet)&&
                    p[2].ParameterType.IsByRef;
            });
            if(viewMethod==null)
            {
                report("REV86_VIEWDEF_PROBE=UNAVAILABLE:native view reader missing");
                return;
            }
            object?[] viewArgs={viewName,sheet,null};
            if(viewMethod.Invoke(null,viewArgs) is not bool foundView||
               !foundView||viewArgs[2]==null)
            {
                report("REV86_VIEWDEF_PROBE=UNAVAILABLE:saved native ViewDef absent");
                return;
            }
            ReportType(sheet,"NATIVE_SHEET",report);
            ReportType(viewArgs[2]!,"SAVED_VIEWDEF",report);
            report("REV86_VIEWDEF_PROBE=READ_ONLY_COMPLETED"+
                " NO_HOST_OR_SAVED_VIEW_MUTATION=True");
        }
        catch(Exception e)
        {
            report("REV86_VIEWDEF_PROBE=UNAVAILABLE:"+
                e.GetType().Name+":"+e.Message.Replace('\r',' ')
                    .Replace('\n',' '));
        }
    }

    private static void ReportType(object target,string scope,
        Action<string> report)
    {
        Type t=target.GetType();
        report("REV86_APPEARANCE_SCHEMA_"+scope+"="+t.FullName);
        var members=Describe(t);
        if(members.Count==0)
            report("REV86_APPEARANCE_MEMBERS_"+scope+"=NONE_MATCHED");
        foreach(string member in members)
            report("REV86_APPEARANCE_MEMBER_"+scope+"="+member);
        if(scope=="SAVED_VIEWDEF")
        {
            // Log names only. Unknown getters can be side-effectful,
            // and native ordinal meanings must never be guessed.
            string all=string.Join(",",
                t.GetProperties(All).Select(p=>p.Name)
                    .Distinct().OrderBy(n=>n,StringComparer.Ordinal).Take(80));
            report("REV86_SAVED_VIEWDEF_PROPERTY_NAMES="+all);
        }
    }
}
