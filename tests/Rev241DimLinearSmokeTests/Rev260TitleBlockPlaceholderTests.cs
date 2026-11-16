using System;
using SPTC.SupportDrawings;

internal static class Rev260TitleBlockPlaceholderTests
{
    private static void Need(bool yes,string detail)
    {
        if(!yes)throw new Exception("REV60 title block: "+detail);
    }

    internal static void Run()
    {
        Need(SupportProductionTitleBlockPlaceholder60.CanReplace(
            "Revision","",false),"blank configured attribute is writable");
        Need(SupportProductionTitleBlockPlaceholder60.CanReplace(
            "DrawingNumber","####",false),"hash drawing number template is writable");
        Need(SupportProductionTitleBlockPlaceholder60.CanReplace(
            "Revision","????",false),"question mark revision placeholder is writable");
        Need(SupportProductionTitleBlockPlaceholder60.CanReplace(
            "DrawingNumber","SPTC_NATIVE_SEED_01",false),
            "only explicitly mapped seed drawing number is replaceable");
        Need(!SupportProductionTitleBlockPlaceholder60.CanReplace(
            "Title","SPTC_NATIVE_SEED_01",false),
            "seed string in an unrelated field is preserved");
        Need(!SupportProductionTitleBlockPlaceholder60.CanReplace(
            "Revision","A",false),"real existing revision is preserved");
        Need(!SupportProductionTitleBlockPlaceholder60.CanReplace(
            "DrawingNumber","APPROVED-123",false),
            "previously populated drawing number is preserved");
        Need(!SupportProductionTitleBlockPlaceholder60.CanReplace(
            "Project","##-REAL",false),
            "mixed literal and placeholders are not assumed empty");
        Need(SupportProductionTitleBlockPlaceholder60.CanReplace(
            "Revision","A",true),"explicit overwrite remains available");
        Console.WriteLine(
            "REV60 opt-in mapped ATTRIB placeholder replacement and existing-value protection PASS");
    }
}
