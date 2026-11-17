using System;
using SPTC.SupportDrawings;

internal static class Rev266TitleBlockStaticTests
{
    private static void Need(bool actual,string message)
    {
        if(!actual)throw new Exception("REV66 static title block: "+message);
    }
    internal static void Run()
    {
        Need(SupportOrthoTitleBlockStatic66.LooksLikeObviousSeedPlaceholder(
            "####"),"literal hash placeholder is recognizable");
        Need(SupportOrthoTitleBlockStatic66.LooksLikeObviousSeedPlaceholder(
            "  ????  "),"literal question-mark placeholder is recognizable");
        Need(SupportOrthoTitleBlockStatic66.LooksLikeObviousSeedPlaceholder(
            "SPTC_NATIVE_SEED_01"),"documented A1 original seed marker recognized");
        Need(!SupportOrthoTitleBlockStatic66.LooksLikeObviousSeedPlaceholder(
            "test1"),"A0 test1 may be meaningful, must never be rewritten by guess");
        Need(!SupportOrthoTitleBlockStatic66.LooksLikeObviousSeedPlaceholder(
            "APPROVED-123"),"genuine drawing number remains protected");
        Need(!SupportOrthoTitleBlockStatic66.LooksLikeObviousSeedPlaceholder(
            "#-AB"),"mixed literal and placeholder is not automatically blank");
        Need(SupportOrthoTitleBlockStatic66.ActionFor(0,15,3,false)==
            "STATIC_PLACEHOLDERS_REQUIRE_ATTDEF_ATTRIB_TEMPLATE",
            "static title block with ### needs an approved ATTDEF/ATTRIB template");
        Need(SupportOrthoTitleBlockStatic66.ActionFor(0,15,0,false)==
            "STATIC_TEXT_REQUIRES_ATTDEF_ATTRIB_TEMPLATE",
            "real static text is not equivalent to editable attributes");
        Need(SupportOrthoTitleBlockStatic66.ActionFor(4,15,3,false)==
            "MAP_EXACT_ATTRIB_TAGS_IN_PRODUCTION_SETTINGS",
            "real attributes may use existing secure, mapped REV60 writer");
        Need(SupportOrthoTitleBlockStatic66.ActionFor(0,15,3,true)==
            "XREF_TITLEBLOCK_REQUIRES_OWNER_TEMPLATE",
            "XREF title block never edited or mutated by plugin");
        Console.WriteLine(
            "REV66 title block: A0 DBText/MText static placeholders "+
            "reported read-only; real metadata, XREF and existing writers protected PASS");
    }
}
