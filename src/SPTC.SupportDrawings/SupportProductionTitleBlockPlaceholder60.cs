using System;
using System.Linq;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV60: Only a positively configured ATTRIBUTE tag is eligible for
/// production title-block stamping. Blank or unmistakable template
/// placeholders may be replaced without the broad overwrite option;
/// real nonempty entries must remain untouched.
/// </summary>
internal static class SupportProductionTitleBlockPlaceholder60
{
    internal static bool CanReplace(string field,string? existing,
        bool overwriteExisting)
    {
        if(overwriteExisting||string.IsNullOrWhiteSpace(existing))return true;
        string value=existing.Trim();
        // An explicit field-to-ATTRIBUTE mapping is required upstream.
        // Repeated #/? are empty seed placeholders, not issue metadata.
        if(value.Length>=3&&value.All(c=>c=='#'||c=='?'))
            return true;
        // Only in the explicitly mapped DRAWING NUMBER field. Never
        // rewrite this text when it appears as ordinary DBText or MText.
        return string.Equals(field,"DrawingNumber",
            StringComparison.Ordinal)&&
            string.Equals(value,"SPTC_NATIVE_SEED_01",
                StringComparison.OrdinalIgnoreCase);
    }
}
