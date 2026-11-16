using System;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV66 no-guess safety policy for existing static title blocks.
/// Ordinary DBText/MText in a seed BLOCK is NOT an ATTRIB and cannot be
/// assigned metadata based only on appearance. This detector is strictly
/// read-only; it is never an authorization to edit block definitions,
/// XREFs, approved metadata, names such as 'test1', or printed stamps.
/// </summary>
internal static class SupportOrthoTitleBlockStatic66
{
    internal static bool LooksLikeObviousSeedPlaceholder(string? raw)
    {
        if(string.IsNullOrWhiteSpace(raw))return false;
        string s=raw.Trim();
        return s.Length>=3&&s.Length<=64&&
            (s.All(c=>c=='#'||c=='?')||
             string.Equals(s,"SPTC_NATIVE_SEED_01",
                 StringComparison.OrdinalIgnoreCase));
    }

    internal static string ActionFor(int editableAttributeCount,
        int staticTextCount,int placeholderCount,bool isXref)
    {
        if(isXref)return "XREF_TITLEBLOCK_REQUIRES_OWNER_TEMPLATE";
        if(editableAttributeCount>0)
            return "MAP_EXACT_ATTRIB_TAGS_IN_PRODUCTION_SETTINGS";
        if(staticTextCount>0)
            return placeholderCount>0?
                "STATIC_PLACEHOLDERS_REQUIRE_ATTDEF_ATTRIB_TEMPLATE":
                "STATIC_TEXT_REQUIRES_ATTDEF_ATTRIB_TEMPLATE";
        return "NO_EDITABLE_TITLEBLOCK_TEXT_FOUND";
    }
}
