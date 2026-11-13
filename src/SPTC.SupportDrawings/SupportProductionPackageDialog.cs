using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using WinForms=System.Windows.Forms;

namespace SPTC.SupportDrawings;

/// <summary>
/// Project-specific opt-in production metadata editor. WinForms only:
/// does not touch AutoCAD Editor, native views, or a DWG in this dialog.
/// </summary>
internal sealed class SupportProductionPackageDialog : WinForms.Form
{
    private readonly string projectRoot;
    private readonly CheckBox enabled=new();
    private readonly TextBox projectCode=new();
    private readonly TextBox series=new();
    private readonly TextBox revision=new();
    private readonly TextBox title=new();
    private readonly TextBox titleBlock=new();
    private readonly TextBox supportTagAttribute=new();
    private readonly TextBox mapping=new();
    private readonly CheckBox overwrite=new();
    private readonly CheckBox requirePdf=new();
    private readonly CheckBox fullDimensions=new();
    private readonly CheckBox skipExisting=new();
    private readonly CheckBox publishPdf=new();
    private readonly ComboBox pdfDevice=new();

    internal SupportProductionPackageDialog(string projectRoot)
    {
        this.projectRoot=projectRoot;
        Text="SPTC Production Package — project settings";
        Font=new Font("Segoe UI",9f);
        AutoScaleMode=AutoScaleMode.Dpi;
        StartPosition=FormStartPosition.CenterParent;
        ClientSize=new Size(720,760);
        MinimumSize=new Size(650,640);
        AutoScroll=true;
        FormBorderStyle=FormBorderStyle.Sizable;
        ShowInTaskbar=false;

        var panel=new Panel{
            AutoScroll=true,Dock=DockStyle.Fill,
            AutoScrollMinSize=new Size(670,782)};
        Controls.Add(panel);

        void LabelAt(string caption,int y) => panel.Controls.Add(new Label {
            Text=caption,Location=new Point(20,y),Size=new Size(235,25)
        });
        void TextAt(TextBox control,int y) {
            control.SetBounds(270,y,415,27);
            control.Anchor=AnchorStyles.Top|AnchorStyles.Left|
                AnchorStyles.Right;
            panel.Controls.Add(control);
        }
        enabled.Text="Enable production package metadata and draft register";
        enabled.SetBounds(20,15,630,30);
        panel.Controls.Add(enabled);
        LabelAt("Project code",56);TextAt(projectCode,53);
        LabelAt("Drawing series",94);TextAt(series,91);
        LabelAt("Drawing revision",132);TextAt(revision,129);
        LabelAt("Drawing title",170);TextAt(title,167);
        LabelAt("Exact title block name (optional)",208);TextAt(titleBlock,205);
        LabelAt("Support tag source attribute (optional)",246);
        TextAt(supportTagAttribute,243);

        panel.Controls.Add(new Label {
            Text="Existing title block attribute tags (optional). One FIELD=TAG per line.",
            Location=new Point(20,282),Size=new Size(650,24)
        });
        mapping.SetBounds(20,309,665,120);
        mapping.Multiline=true;
        mapping.ScrollBars=ScrollBars.Vertical;
        mapping.Font=new Font("Consolas",9f);
        mapping.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
        panel.Controls.Add(mapping);
        panel.Controls.Add(new Label {
            Text="Allowed fields: DrawingNumber, Project, Revision, Title, SupportTag, Scale, SourceDrawing",
            ForeColor=Color.DimGray,
            Location=new Point(20,434),Size=new Size(660,22)
        });
        overwrite.Text="Overwrite nonempty, mapped title block attributes";
        overwrite.SetBounds(20,466,635,25);
        panel.Controls.Add(overwrite);
        requirePdf.Text="Require verified PDF for future issue-ready status";
        requirePdf.SetBounds(20,494,645,25);
        panel.Controls.Add(requirePdf);
        fullDimensions.Text="Require all optional detail dimensions for release";
        fullDimensions.SetBounds(20,522,615,25);
        panel.Controls.Add(fullDimensions);
        skipExisting.Text="Allow skipping separately verified, unchanged issued drawings";
        skipExisting.SetBounds(20,550,645,25);
        panel.Controls.Add(skipExisting);
        publishPdf.Text="Auto-save generated Ortho DWG and publish native PDF (opt-in)";
        publishPdf.SetBounds(20,580,650,27);
        panel.Controls.Add(publishPdf);
        panel.Controls.Add(new Label {
            Text="PDF plotter:",Location=new Point(20,618),Size=new Size(240,24)
        });
        pdfDevice.SetBounds(270,613,415,29);
        pdfDevice.DropDownStyle=ComboBoxStyle.DropDownList;
        pdfDevice.Items.AddRange(
            SupportProductionPdfPolicy.ApprovedPdfDevices.Cast<object>().ToArray());
        pdfDevice.SelectedIndex=0;
        panel.Controls.Add(pdfDevice);
        panel.Controls.Add(new Label {
            Text="Publishing is gated by native QA and exact sheet media. Draft PDF is never automatically issued.",
            ForeColor=Color.DarkGoldenrod,
            Location=new Point(20,660),Size=new Size(655,38)
        });
        var save=new Button{
            Text="Save configuration",Location=new Point(378,712),
            Size=new Size(170,34),Anchor=AnchorStyles.Top|AnchorStyles.Right
        };
        var cancel=new Button{
            Text="Cancel",Location=new Point(558,712),
            Size=new Size(127,34),DialogResult=WinForms.DialogResult.Cancel,
            Anchor=AnchorStyles.Top|AnchorStyles.Right
        };
        panel.Controls.Add(save);
        panel.Controls.Add(cancel);
        AcceptButton=save;
        CancelButton=cancel;

        SupportProductionPackageSettingsStore.TryLoad(
            projectRoot,out var current,out _);
        Apply(current);
        save.Click+=(_,_)=>Save();
    }

    private void Apply(SupportProductionPackagePlan.Settings config)
    {
        enabled.Checked=config.Enabled;
        projectCode.Text=config.ProjectCode;
        series.Text=config.DrawingSeries;
        revision.Text=config.Revision;
        title.Text=config.DrawingTitle;
        titleBlock.Text=config.TitleBlockName;
        supportTagAttribute.Text=config.SupportTagAttribute;
        mapping.Lines=config.AttributeTags.OrderBy(k=>k.Key,
            StringComparer.Ordinal).Select(x=>x.Key+"="+x.Value).ToArray();
        overwrite.Checked=config.OverwriteExistingAttributes;
        requirePdf.Checked=config.RequirePdfForIssue;
        fullDimensions.Checked=config.RequireFullDimensionCoverage;
        skipExisting.Checked=config.SkipVerifiedExisting;
        publishPdf.Checked=config.PublishPdf;
        pdfDevice.SelectedItem=
            SupportProductionPdfPolicy.AllowedDevice(config.PdfPlotDevice)
            ?config.PdfPlotDevice
            :SupportProductionPdfPolicy.ApprovedPdfDevices[0];
    }

    private void Save()
    {
        try
        {
            var tags=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(string line in mapping.Lines.Select(x=>x.Trim())
                .Where(x=>x.Length>0))
            {
                int equals=line.IndexOf('=');
                if(equals<1||equals==line.Length-1||
                   line.IndexOf('=',equals+1)>=0)
                    throw new ArgumentException(
                        "Enter title block tags as FIELD=TAG, one per line.");
                string field=line[..equals].Trim();
                string tag=line[(equals+1)..].Trim();
                if(!tags.TryAdd(field,tag))
                    throw new ArgumentException("Duplicate metadata field: "+field);
            }
            var settings=new SupportProductionPackagePlan.Settings(
                enabled.Checked,projectCode.Text,series.Text,
                revision.Text,title.Text,titleBlock.Text,tags,
                overwrite.Checked,requirePdf.Checked,
                fullDimensions.Checked,skipExisting.Checked,
                supportTagAttribute.Text,publishPdf.Checked,pdfDevice.Text);
            SupportProductionPackageSettingsStore.Save(projectRoot,settings);
            DialogResult=WinForms.DialogResult.OK;
            Close();
        }
        catch(Exception ex) when (ex is ArgumentException or
            System.IO.IOException or UnauthorizedAccessException or
            System.Security.SecurityException)
        {
            MessageBox.Show(this,ex.Message,"Invalid production package settings",
                MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
    }
}
