using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.41 isolated PAPER-SPACE writer. Native RotatedDimension is the .NET
/// representation of DIMLINEAR (0° horizontal / 90° vertical). A real source
/// member length in mm is passed as the dimension text override because
/// measuring projected paper points would otherwise show the paper length.
/// The original source model, native Ortho viewport and XREF are untouched.
/// </summary>
internal static class SupportOrthoDimLinearWriter41
{
    internal static int Write(Database db,string layoutName,
        IReadOnlyList<SupportOrthoDimLinearPlan41.Dimension> dimensions,
        out List<string> diagnostics)
    {
        diagnostics=new List<string>();
        if(dimensions.Count==0)return 0;
        using Transaction tr=db.TransactionManager.StartTransaction();
        var layouts=(DBDictionary)tr.GetObject(
            db.LayoutDictionaryId,OpenMode.ForRead);
        if(!layouts.Contains(layoutName))
            throw new InvalidOperationException(
                "REV22.41 native Ortho paper layout missing.");
        var layout=(Layout)tr.GetObject(
            layouts.GetAt(layoutName),OpenMode.ForRead);
        var paper=(BlockTableRecord)tr.GetObject(
            layout.BlockTableRecordId,OpenMode.ForWrite);
        var layers=(LayerTable)tr.GetObject(
            db.LayerTableId,OpenMode.ForRead);
        ObjectId layerId;
        if(layers.Has(SupportOrthoDimLinearPlan41.Layer))
            layerId=layers[SupportOrthoDimLinearPlan41.Layer];
        else
        {
            layers.UpgradeOpen();
            using var layer=new LayerTableRecord
            {
                Name=SupportOrthoDimLinearPlan41.Layer
            };
            layerId=layers.Add(layer);
            tr.AddNewlyCreatedDBObject(layer,true);
        }

        int created=0;
        foreach(var item in dimensions)
        {
            try
            {
                var first=new Point3d(item.Ax,item.Ay,0);
                var second=new Point3d(item.Bx,item.By,0);
                var dimensionLine=new Point3d(item.LineX,item.LineY,0);
                double rotation=item.Horizontal?0:Math.PI/2;
                // AutoCAD DIMLINEAR entity: text override is the independently
                // verified *model* length, not shortened paper distance.
                using var dim=new RotatedDimension(rotation,first,second,
                    dimensionLine,item.Measurement,db.Dimstyle)
                {
                    LayerId=layerId
                };
                // REV22.45: a short 50/75/100 mm projected span at
                // 1:30 can be narrower than its text or arrowheads.
                // Let AutoCAD move those elements OUTSIDE the witness
                // lines rather than squeezing/hiding their content.
                // Only the paper dimension is changed, never the model.
                double measuredPaperSpan=item.Horizontal
                    ? Math.Abs(item.Bx-item.Ax)
                    : Math.Abs(item.By-item.Ay);
                if(measuredPaperSpan<8.0)
                {
                    dim.Dimatfit=3;
                    dim.Dimtix=false;
                    dim.Dimtofl=true;
                }
                // REV22.46: obey the collision planner's reserved text
                // position rather than letting an automatic style move
                // text back across another dimension or leader.
                if(item.PaperTextX.HasValue&&item.PaperTextY.HasValue &&
                   double.IsFinite(item.PaperTextX.Value)&&
                   double.IsFinite(item.PaperTextY.Value))
                {
                    dim.UsingDefaultTextPosition=false;
                    dim.TextPosition=new Point3d(
                        item.PaperTextX.Value,item.PaperTextY.Value,0);
                }
                paper.AppendEntity(dim);
                tr.AddNewlyCreatedDBObject(dim,true);
                created++;
            }
            catch(System.Exception ex)
            {
                diagnostics.Add("DIM_CREATE_SKIPPED="+item.View+"/"+item.Source+
                    ":"+ex.GetType().Name+":"+ex.Message);
            }
        }
        tr.Commit();
        return created;
    }
}
