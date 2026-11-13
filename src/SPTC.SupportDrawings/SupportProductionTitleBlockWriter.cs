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
            if(!config.OverwriteExistingAttributes&&
               !string.IsNullOrWhiteSpace(attr.TextString))
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
