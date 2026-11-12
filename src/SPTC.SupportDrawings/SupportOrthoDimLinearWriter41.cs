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
        out List<string> diagnostics,bool replaceGenerated=false)
    {
        diagnostics=new List<string>();
        // A failed/incomplete refresh must retain the last known drawing.
        // Callers supply a validated, nonempty native-only plan.
        if(replaceGenerated&&dimensions.Count==0)
            throw new InvalidOperationException(
                "REV22.49 zero dimensions; existing sheet retained.");
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

        var app=(RegAppTable)tr.GetObject(
            db.RegAppTableId,OpenMode.ForRead);
        if(!app.Has(SupportOrthoDimensionUpdatePlan49.OwnerApp))
        {
            app.UpgradeOpen();
            using var registered=new RegAppTableRecord{
                Name=SupportOrthoDimensionUpdatePlan49.OwnerApp};
            app.Add(registered);
            tr.AddNewlyCreatedDBObject(registered,true);
        }
        var oldOwned=new List<ObjectId>();
        if(replaceGenerated)
        {
            foreach(ObjectId candidateId in paper)
            {
                if(tr.GetObject(candidateId,OpenMode.ForRead,false)
                    is not RotatedDimension candidate)continue;
                using ResultBuffer? marker=candidate.GetXDataForApplication(
                    SupportOrthoDimensionUpdatePlan49.OwnerApp);
                if(marker!=null&&candidate.Layer==
                    SupportOrthoDimLinearPlan41.Layer)
                    oldOwned.Add(candidateId);
            }
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
                // REV22.49.2: for dimensions derived from the native
                // viewport projection, NEVER hard-code the numeric text.
                // The measurement comes from the DIMLINEAR witness points
                // and that viewport's individually assigned DIMLFAC.
                // Retain old explicit-endpoint handling only for historical
                // plans which do not carry a paper-to-model factor.
                bool dynamicMeasurement=item.PaperToModelMm.HasValue;
                if(dynamicMeasurement&&
                   !SupportOrthoDynamicDimension492.MatchesDiagnostic(item))
                    throw new InvalidOperationException(
                        "REV22.49.2 stale/incorrect dimension measurement "+
                        "does not match paper witnesses and native scale: "+
                        item.View+"/"+item.Source);
                using var dim=new RotatedDimension(rotation,first,second,
                    dimensionLine,dynamicMeasurement?string.Empty:
                        item.Measurement,db.Dimstyle)
                {
                    LayerId=layerId
                };
                if(dynamicMeasurement)
                {
                    dim.Dimlfac=item.PaperToModelMm!.Value;
                    // A blank AutoCAD dimension override means "measure",
                    // NOT the displayed numeric label from the planner.
                    dim.DimensionText=string.Empty;
                }
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
                // REV22.47: match the technical reference's compact
                // fabrication-detail DIMLINEAR text/arrow/extension style,
                // without changing the project-wide DIMSTYLE definition.
                // These are paper-space sizes in millimetres.
                if(item.Provenance.StartsWith("REFERENCE_PROJECTED_",
                    StringComparison.Ordinal))
                {
                    dim.Dimtxt=2.0;
                    dim.Dimasz=1.5;
                    dim.Dimexo=.8;
                    dim.Dimexe=1.2;
                    dim.Dimgap=.7;
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
                // XDATA identifies only plugin-owned dimensions. User-created
                // DIMLINEAR entities, even on the same layer, are untouched.
                dim.XData=new ResultBuffer(
                    new TypedValue((int)DxfCode.ExtendedDataRegAppName,
                        SupportOrthoDimensionUpdatePlan49.OwnerApp),
                    new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                        item.View));
                paper.AppendEntity(dim);
                tr.AddNewlyCreatedDBObject(dim,true);
                created++;
            }
            catch(System.Exception ex)
            {
                if(replaceGenerated)
                    throw new InvalidOperationException(
                        "REV22.49 dimension replacement aborted; old "+
                        "dimensions retained: "+item.View+"/"+item.Source,ex);
                diagnostics.Add("DIM_CREATE_SKIPPED="+item.View+"/"+item.Source+
                    ":"+ex.GetType().Name+":"+ex.Message);
            }
        }
        if(replaceGenerated)
        {
            if(created!=dimensions.Count)
                throw new InvalidOperationException(
                    "REV22.49 incomplete rebuild; existing DIMLINEAR retained.");
            // Erase AFTER every replacement is successfully staged. AutoCAD
            // Transaction rolls back both operations if any step fails.
            foreach(ObjectId prior in oldOwned)
                if(tr.GetObject(prior,OpenMode.ForWrite,false)
                    is Entity previous&&!previous.IsErased)
                    previous.Erase();
            diagnostics.Add("REV22_49_OLD_OWNED_DIMENSIONS_REMOVED="+
                oldOwned.Count);
        }
        tr.Commit();
        return created;
    }
}
