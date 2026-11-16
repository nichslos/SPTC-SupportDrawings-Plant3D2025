using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;

namespace SPTC.SupportDrawings;

/// <summary>
/// Opt-in, bounded title-block attribute stamping ONLY. It does not
/// create blocks, change title block geometry, modify native Ortho views,
/// overwrite unrelated fields, or infer which block is the title block.
/// Requires a user-supplied exact block name and tag-to-field mapping.
/// </summary>
internal static class SupportProductionTitleBlockWriter
{
    internal sealed record Result(string Status,int Matched,
        int Written,int AlreadyCorrect,int ExistingPreserved,
        string[] MissingTags);

    internal static Result Apply(Database db,string layoutName,
        SupportProductionPackagePlan.Settings settings,
        IReadOnlyDictionary<string,string> metadata)
    {
        var config=SupportProductionPackagePlan.Validate(settings);
        if(!config.Enabled)return new("DISABLED",0,0,0,0,
            Array.Empty<string>());
        if(string.IsNullOrWhiteSpace(config.TitleBlockName)||
           config.AttributeTags.Count==0)
            return new("NOT_CONFIGURED",0,0,0,0,
                Array.Empty<string>());

        using var tr=db.TransactionManager.StartTransaction();
        var layouts=(DBDictionary)tr.GetObject(db.LayoutDictionaryId,
            OpenMode.ForRead);
        if(!layouts.Contains(layoutName))
            return new("LAYOUT_NOT_FOUND",0,0,0,0,
                Array.Empty<string>());
        var layout=(Layout)tr.GetObject(layouts.GetAt(layoutName),
            OpenMode.ForRead);
        var paper=(BlockTableRecord)tr.GetObject(layout.BlockTableRecordId,
            OpenMode.ForRead);
        var candidates=new List<BlockReference>();
        foreach(ObjectId id in paper)
        {
            if(tr.GetObject(id,OpenMode.ForRead,false)
                is not BlockReference block)continue;
            string blockName=EffectiveBlockName(tr,block);
            if(string.Equals(blockName,config.TitleBlockName,
                StringComparison.OrdinalIgnoreCase))
                candidates.Add(block);
        }
        if(candidates.Count==0)return new(
            "TITLEBLOCK_NOT_FOUND",0,0,0,0,
            Array.Empty<string>());
        if(candidates.Count!=1)return new(
            "AMBIGUOUS_TITLEBLOCK_BLOCKS",candidates.Count,0,0,0,
            Array.Empty<string>());

        BlockReference target=candidates[0];
        var byTag=new Dictionary<string,AttributeReference>(
            StringComparer.OrdinalIgnoreCase);
        foreach(ObjectId attributeId in target.AttributeCollection)
        {
            if(tr.GetObject(attributeId,OpenMode.ForRead,false)
                is AttributeReference attr &&
               !byTag.ContainsKey(attr.Tag))
                byTag.Add(attr.Tag,attr);
        }
        int written=0,already=0,preserved=0;
        var missing=new List<string>();
        foreach(var field in config.AttributeTags.Keys.OrderBy(
            s=>s,StringComparer.Ordinal))
        {
            string tag=config.AttributeTags[field];
            if(!metadata.TryGetValue(field,out string? value)||
                string.IsNullOrWhiteSpace(value))
            {
                missing.Add(tag+"(NO_VALUE)");
                continue;
            }
            if(!byTag.TryGetValue(tag,out var attr))
            {
                missing.Add(tag);
                continue;
            }
            if(string.Equals(attr.TextString?.Trim(),value,
                StringComparison.Ordinal))
            {
                already++;
                continue;
            }
            // REV60: already mapped, unmistakable seed placeholders are
            // eligible for replacement even when broad overwrite is off.
            // Real nonempty entries remain protected.
            if(!SupportProductionTitleBlockPlaceholder60.CanReplace(
                field,attr.TextString,config.OverwriteExistingAttributes))
            {
                preserved++;
                continue;
            }
            attr.UpgradeOpen();
            attr.TextString=value;
            written++;
        }
        tr.Commit();
        string status=missing.Count>0||preserved>0?
            "PARTIAL_NEEDS_REVIEW":
            "MAPPED_ATTRIBUTES_VERIFIED_IN_MEMORY";
        return new(status,1,written,already,preserved,missing.ToArray());
    }

    /// <summary>
    /// Read-only discovery for the GENERATED native Ortho paper layout.
    /// Report exact editable block names and ATTRIBUTE tags, never
    /// attribute values. This does not guess which template block to edit.
    /// </summary>
    internal static IReadOnlyList<string> InspectCandidates(
        Database db,string layoutName)
    {
        var report=new List<string>();
        using var tr=db.TransactionManager.StartTransaction();
        var layouts=(DBDictionary)tr.GetObject(db.LayoutDictionaryId,
            OpenMode.ForRead);
        if(!layouts.Contains(layoutName))
            return new[]{"LAYOUT_NOT_FOUND"};
        var layout=(Layout)tr.GetObject(layouts.GetAt(layoutName),
            OpenMode.ForRead);
        var paper=(BlockTableRecord)tr.GetObject(layout.BlockTableRecordId,
            OpenMode.ForRead);
        foreach(ObjectId id in paper)
        {
            if(tr.GetObject(id,OpenMode.ForRead,false)
                is not BlockReference block)continue;
            string blockName=EffectiveBlockName(tr,block);
            var tags=new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(ObjectId attrId in block.AttributeCollection)
                if(tr.GetObject(attrId,OpenMode.ForRead,false)
                   is AttributeReference attr&&
                   !string.IsNullOrWhiteSpace(attr.Tag))
                    tags.Add(attr.Tag);
            // REV66: a cloned native A0 sheet may still contain
            // *static* title-block DBText/MText (test1 / ####), not
            // editable attribute references. Inventory the direct
            // definition READ ONLY, without printing metadata values
            // and without changing the original template or XREF.
            int staticTextCount=0,obviousSeedPlaceholders=0;
            int uninstantiatedAttdefs=0,nestedDefinitions=0;
            bool isXref=false;
            try
            {
                // REV67: previous detector examined only DIRECT DBText
                // in the title-block definition and missed MText
                // formatting, nested insert definitions and uninstantiated
                // ATTDEF text. Inspect nested unique definitions READ ONLY.
                // Restrict recursion depth and object count to prevent
                // cycles or unexpectedly huge DWG traversal.
                var pending=new Stack<(ObjectId Id,int Depth)>();
                var seen=new HashSet<ObjectId>();
                pending.Push((block.BlockTableRecord,0));
                int visitedEntities=0;
                while(pending.Count>0&&visitedEntities<4096)
                {
                    var next=pending.Pop();
                    if(next.Depth>5||next.Id.IsNull||
                       !seen.Add(next.Id))continue;
                    if(tr.GetObject(next.Id,OpenMode.ForRead,false)
                        is not BlockTableRecord definition)continue;
                    if(definition.IsFromExternalReference)
                    {
                        isXref=true;
                        continue;
                    }
                    if(next.Depth>0)nestedDefinitions++;
                    foreach(ObjectId entityId in definition)
                    {
                        if(++visitedEntities>4096)break;
                        DBObject entity=tr.GetObject(entityId,
                            OpenMode.ForRead,false);
                        string? literal=null;
                        if(entity is AttributeDefinition attdef)
                        {
                            uninstantiatedAttdefs++;
                            literal=attdef.TextString;
                        }
                        else if(entity is DBText text)
                        {
                            staticTextCount++;
                            literal=text.TextString;
                        }
                        else if(entity is MText mtext)
                        {
                            staticTextCount++;
                            literal=mtext.Contents;
                        }
                        else if(entity is BlockReference inner&&
                                !inner.BlockTableRecord.IsNull)
                        {
                            pending.Push((inner.BlockTableRecord,
                                next.Depth+1));
                        }
                        if(literal!=null&&SupportOrthoTitleBlockStatic66.
                            LooksLikeObviousSeedPlaceholder(literal))
                            obviousSeedPlaceholders++;
                    }
                }
            }
            catch(System.Exception)
            {
                // Report partial read-only evidence. Never fail a
                // verified native DWG/PDF to inspect static seed text.
            }
            report.Add("BLOCK="+blockName+
                ";HANDLE="+block.Handle+
                ";ATTRIB_TAGS="+
                (tags.Count==0?"NONE":string.Join(",",tags))+
                ";STATIC_TEXT_COUNT="+staticTextCount+
                ";NESTED_BLOCK_DEFINITIONS="+nestedDefinitions+
                ";UNINSTANTIATED_ATTDEF_COUNT="+uninstantiatedAttdefs+
                ";OBVIOUS_STATIC_PLACEHOLDERS="+obviousSeedPlaceholders+
                ";TEMPLATE_ACTION="+
                SupportOrthoTitleBlockStatic66.ActionFor(
                    tags.Count,staticTextCount,obviousSeedPlaceholders,
                    isXref));
        }
        return report.Count==0?
            new[]{"NO_EDITABLE_PAPER_BLOCK_REFERENCES"}:
            report.OrderByDescending(x=>x.Contains("ATTRIB_TAGS=NONE",
                    StringComparison.Ordinal)?0:1)
                .ThenBy(x=>x,StringComparer.OrdinalIgnoreCase)
                .Take(16).ToArray();
    }

    private static string EffectiveBlockName(Transaction tr,
        BlockReference block)
    {
        if(block.IsDynamicBlock&&
            !block.DynamicBlockTableRecord.IsNull)
        {
            var definition=tr.GetObject(
                block.DynamicBlockTableRecord,OpenMode.ForRead,false)
                as BlockTableRecord;
            if(definition!=null)return definition.Name;
        }
        return block.Name;
    }

    internal static Dictionary<string,string> Metadata(
        SupportProductionPackagePlan.Settings config,
        string drawingNumber,string source,string supportKey,
        string scale)
    {
        var valid=SupportProductionPackagePlan.Validate(config);
        return new Dictionary<string,string>(StringComparer.Ordinal)
        {
            ["DrawingNumber"]=drawingNumber,
            ["Project"]=valid.ProjectCode,
            ["Revision"]=valid.Revision,
            ["Title"]=valid.DrawingTitle,
            ["SupportTag"]=supportKey,
            ["Scale"]=scale,
            ["SourceDrawing"]=System.IO.Path.GetFileName(source)
        };
    }
}
