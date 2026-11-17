using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV87: bounded, read-only native type/member discovery on the user's
/// actual Plant 3D installation. Never invoke unknown native getters,
/// setters, methods or commands; never guess numeric mode codes.
/// </summary>
internal static class NativeOrthoAppearanceProbe87
{
    private const BindingFlags All=BindingFlags.Public|
        BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;

    internal static bool IsRelevantMember(string name)=>
        NativeOrthoAppearanceProbe86.IsAppearanceMember(name)||
        name.Contains("Hlr",StringComparison.OrdinalIgnoreCase)||
        name.Contains("LineRemoval",StringComparison.OrdinalIgnoreCase);

    internal static bool IsRelevantType(string name)=>
        new[]{"Ortho","Hidden","Hlr","Insulat","Appearance","Output"}
            .Any(token=>name.Contains(token,StringComparison.OrdinalIgnoreCase));

    // Pure metadata, no property or field values read.
    internal static IReadOnlyList<string> Describe(Type type)
    {
        var props=type.GetProperties(All)
            .Where(p=>IsRelevantMember(p.Name))
            .Select(p=>"PROPERTY "+p.Name+" TYPE="+p.PropertyType.FullName+
                " READ="+p.CanRead+" WRITE="+p.CanWrite);
        var fields=type.GetFields(All)
            .Where(f=>IsRelevantMember(f.Name))
            .Select(f=>"FIELD "+f.Name+" TYPE="+f.FieldType.FullName+
                " STATIC="+f.IsStatic+" INIT_ONLY="+f.IsInitOnly);
        var methods=type.GetMethods(All)
            .Where(m=>IsRelevantMember(m.Name)&&!m.IsSpecialName)
            .Select(m=>"METHOD "+m.Name+" SIGNATURE="+m);
        return props.Concat(fields).Concat(methods)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s=>s,StringComparer.Ordinal).Take(60).ToArray();
    }

    internal static void Inspect(object savedViewDef,
        object nativeView,Action<string> log)
    {
        if(savedViewDef==null)throw new ArgumentNullException(nameof(savedViewDef));
        if(nativeView==null)throw new ArgumentNullException(nameof(nativeView));
        if(log==null)throw new ArgumentNullException(nameof(log));

        // First inspect the exact two objects whose metadata REV86
        // established. HlrTrans may be an HLR transform, not a mode.
        // Record its TYPE, not its value and do not change it.
        foreach(var item in new[]{
            (Scope:"SAVED_VIEWDEF",Type:savedViewDef.GetType()),
            (Scope:"PLANT_ORTHO_VIEW",Type:nativeView.GetType())})
        {
            log("REV87_APPEARANCE_SCHEMA_"+item.Scope+"="+item.Type.FullName);
            foreach(string m in Describe(item.Type))
                log("REV87_APPEARANCE_MEMBER_"+item.Scope+"="+m);
        }

        // A bounded scan of two native Autodesk assemblies helps locate
        // the Ortho Editor appearance controller without modifying it.
        int candidateTypes=0;
        int matchedMembers=0;
        foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()
            .Where(a=>
            {
                string name=a.GetName().Name??"";
                return name.Contains("PnP3dOrthoDrawingsUI",
                    StringComparison.OrdinalIgnoreCase)||
                    name.Contains("Drawings2d",StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(a=>a.GetName().Name,StringComparer.Ordinal))
        {
            Type[] types;
            try { types=assembly.GetTypes(); }
            catch(ReflectionTypeLoadException ex)
            {
                types=ex.Types.Where(t=>t!=null).Select(t=>t!).ToArray();
            }
            catch(Exception ex)
            {
                log("REV87_NATIVE_ASSEMBLY_SCAN=UNAVAILABLE:"+
                    ex.GetType().Name);
                continue;
            }

            foreach(Type type in types
                .Where(t=>IsRelevantType(t.Name))
                .OrderBy(t=>t.FullName,StringComparer.Ordinal))
            {
                candidateTypes++;
                IReadOnlyList<string> members;
                try { members=Describe(type); }
                catch(Exception) { continue; }
                if(members.Count==0)continue;
                foreach(string member in members)
                {
                    if(matchedMembers>=100)break;
                    log("REV87_NATIVE_APPEARANCE_CANDIDATE="+
                        type.FullName+" "+member);
                    matchedMembers++;
                }
                if(matchedMembers>=100)break;
            }
            if(matchedMembers>=100)break;
        }
        log("REV87_NATIVE_APPEARANCE_TYPE_SCAN=READ_ONLY "+
            "CANDIDATE_TYPES="+candidateTypes+
            " MATCHED_MEMBERS="+matchedMembers+
            " NO_NATIVE_WRITES=True");
    }
}
