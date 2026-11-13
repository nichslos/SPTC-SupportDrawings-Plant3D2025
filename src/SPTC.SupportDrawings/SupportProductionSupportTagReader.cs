using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;

namespace SPTC.SupportDrawings;

/// <summary>
/// Read-only, explicitly configured support-tag extraction. A tag is used
/// only when selected native BLOCK references agree on its actual value.
/// Never guesses foreign XREF attributes, source names or member labels.
/// </summary>
internal static class SupportProductionSupportTagReader
{
    internal static string? Read(Database source,ObjectId[] selected,
        string attributeTag)
    {
        if(string.IsNullOrWhiteSpace(attributeTag))return null;
        var values=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var tx=source.TransactionManager.StartTransaction();
        foreach(ObjectId id in selected.Distinct())
        {
            if(tx.GetObject(id,OpenMode.ForRead,false)
                is not BlockReference block)continue;
            foreach(ObjectId aid in block.AttributeCollection)
            {
                if(tx.GetObject(aid,OpenMode.ForRead,false)
                    is not AttributeReference attr)continue;
                if(string.Equals(attr.Tag,attributeTag,
                    StringComparison.OrdinalIgnoreCase)&&
                   !string.IsNullOrWhiteSpace(attr.TextString))
                    values.Add(attr.TextString.Trim());
            }
        }
        return values.Count==1?values.Single():null;
    }
}
