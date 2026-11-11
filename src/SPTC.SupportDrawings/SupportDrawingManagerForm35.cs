using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using WinForms = System.Windows.Forms;

namespace SPTC.SupportDrawings;

/// <summary>
/// REV22.35. All UI settings are opt-in. Defaults exactly preserve the
/// previously validated ten-view Ortho path, scale and annotations.
/// The four existing AutoCAD CommandMethod names are the only entry points.
/// </summary>
internal sealed record SupportDrawingUiOptions(
    string Operation,
    bool SupportFocus,
    bool AutoSeed,
    string SeedPath,
    string DrawingPrefix,
    int ScaleDenominator,
    IReadOnlyList<string> Views,
    bool LineNumber,
    bool Bop,
    bool Tos,
    bool Description,
    bool BatchOrtho,
    bool XrefMemberNames,
    bool AutoDimLinear)
{
    public static readonly string[] AllViews =
        {"TOP","BOTTOM","LEFT","RIGHT","FRONT","BACK","NW","NE","SW","SE"};

    public bool HasView(string name) =>
        Views.Contains(name, StringComparer.OrdinalIgnoreCase);
}

internal sealed class SupportDrawingManagerForm35 : WinForms.UserControl
{
    private readonly TabControl tabs = new();
    private readonly ComboBox cubeMode = new();
    private readonly CheckBox autoSeed = new();
    private readonly TextBox seed = new();
    private readonly TextBox prefix = new();
    private readonly ComboBox scale = new();
    private readonly NumericUpDown customScale = new();
    private readonly CheckedListBox views = new();
    private readonly CheckBox line = new();
    private readonly CheckBox bop = new();
    private readonly CheckBox tos = new();
    private readonly CheckBox description = new();
    private readonly CheckBox batch = new();
    private readonly CheckBox xrefNames = new();
    private readonly CheckBox autoDimLinear = new();
    private readonly TextBox preview = new();
    private readonly Button runButton = new();
    private readonly Button preflightButton = new();
    private readonly Label sourceLabel = new();
    private string source;
    private string orthoFolder;
    private string projectRoot;
    internal SupportDrawingUiOptions? Settings { get; private set; }
    internal event Action<SupportDrawingUiOptions>? RunRequested;
    internal bool IsRunning { get; private set; }

    internal SupportDrawingManagerForm35(string sourceDwg, string projectRoot,
        string projectOrthoFolder)
    {
        source = sourceDwg;
        orthoFolder = projectOrthoFolder;
        this.projectRoot = projectRoot;
        Name = "SPTC_SupportDrawingManager40";
        Font = new Font("Segoe UI", 9f);
        Size = new Size(805, 765);
        MinimumSize = new Size(780, 740);
        BackColor = Color.White;
        AutoScroll = true;
        // REV22.40: Hosted permanently as a modeless AutoCAD PaletteSet
        // UserControl. No WinForms DialogResult or Close() on Run.

        // The supplied SPTC Engineering logo is embedded; no image path needed.
        // A larger top band keeps the full wordmark readable at Windows DPI.
        Controls.Add(SupportBranding36.MakeLogo(18, 10, 184, 118));
        Label heading = new()
        {
            Text = "SUPPORT DRAWING MANAGER",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            Location = new Point(222, 21), Size = new Size(545, 33)
        };
        Controls.Add(heading);
        Label brandSubtitle = new()
        {
            Text = "SPTC Engineering  |  AutoCAD Plant 3D 2025",
            ForeColor = Color.DimGray,
            Location = new Point(222, 57), Size = new Size(545, 25)
        };
        Controls.Add(brandSubtitle);
        sourceLabel.Text = "Source: " + Path.GetFileName(sourceDwg) +
            "     |     Project: " + Path.GetFileName(projectRoot.TrimEnd('\\','/'));
        sourceLabel.Location = new Point(222, 94);
        sourceLabel.Size = new Size(545, 24);
        sourceLabel.AutoEllipsis = true;
        Controls.Add(sourceLabel);

        tabs.Location = new Point(18, 140);
        tabs.Size = new Size(770, 475);
        tabs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
            AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(tabs);

        TabPage orthoTab = new("10-View Ortho") { Name = "ortho", AutoScroll = true };
        tabs.TabPages.Add(orthoTab);
        tabs.TabPages.Add(MakeInfoTab("export", "Support Export",
            "Export ONE selected support as an independent DWG.\r\n" +
            "The existing SPTC_SUPPORTEXPORT selection and Save As dialog are retained."));
        tabs.TabPages.Add(MakeInfoTab("batch", "Support Batch",
            "Export one drawing for each selected support BLOCK reference.\r\n" +
            "The existing SPTC_SUPPORTBATCH list and folder picker are retained."));
        tabs.TabPages.Add(MakeInfoTab("2dbatch", "2D Batch",
            "Create 2D DWGs from selected support BLOCK references.\r\n" +
            "The existing SPTC_SUPPORT2DBATCH exporter and fallback are retained."));

        Label modeLabel = L("OrthoCube mode", 17, 17, 205);
        orthoTab.Controls.Add(modeLabel);
        cubeMode.SetBounds(18, 40, 205, 27);
        cubeMode.DropDownStyle = ComboBoxStyle.DropDownList;
        cubeMode.Items.AddRange(new object[] {"SupportFocus (recommended)", "Original"});
        cubeMode.SelectedIndex = 0;
        orthoTab.Controls.Add(cubeMode);

        Label scaleLabel = L("Paper scale", 241, 17, 170);
        orthoTab.Controls.Add(scaleLabel);
        scale.SetBounds(242, 40, 140, 27);
        scale.DropDownStyle = ComboBoxStyle.DropDownList;
        scale.Items.AddRange(new object[] {
            "Automatic", "1:10", "1:20", "1:30", "1:40", "1:50",
            "1:75", "1:100", "1:150", "1:200", "1:250", "1:500",
            SupportDrawingPaperScale39.Custom
        });
        scale.SelectedIndex = 0;
        orthoTab.Controls.Add(scale);

        // REV22.39: explicit custom paper-scale denominator (1:N).
        // The native Ortho generator and its existing sheet-fit preflight
        // receive the same validated ScaleDenominator as any preset.
        Label customLabel = L("Custom scale  1 :", 242, 79, 130);
        customScale.SetBounds(375, 76, 160, 27);
        customScale.Minimum = SupportDrawingPaperScale39.Min;
        customScale.Maximum = SupportDrawingPaperScale39.Max;
        customScale.Value = 25;
        customScale.DecimalPlaces = 0;
        customScale.Increment = 1;
        customScale.TextAlign = HorizontalAlignment.Right;
        customScale.ThousandsSeparator = false;
        customLabel.Visible = false;
        customScale.Visible = false;
        scale.SelectedIndexChanged += (_,_) =>
        {
            bool active=scale.Text==SupportDrawingPaperScale39.Custom;
            customLabel.Visible = active;
            customScale.Visible = active;
            customScale.Enabled = active;
        };
        orthoTab.Controls.Add(customLabel);
        orthoTab.Controls.Add(customScale);

        Label prefixLabel = L("Drawing prefix (optional)", 406, 17, 288);
        orthoTab.Controls.Add(prefixLabel);
        prefix.SetBounds(407, 40, 315, 27);
        prefix.PlaceholderText = "Automatic unique name";
        orthoTab.Controls.Add(prefix);

        autoSeed.SetBounds(18, 120, 355, 26);
        autoSeed.Text = "Automatically select a verified empty Ortho seed";
        autoSeed.Checked = true;
        orthoTab.Controls.Add(autoSeed);
        seed.SetBounds(18, 152, 568, 27);
        seed.PlaceholderText = "Auto: select a registered empty project Ortho DWG";
        seed.Enabled = false;
        orthoTab.Controls.Add(seed);
        Button browse = new() { Text = "Browse seed...", Location = new Point(594, 150),
            Size = new Size(127, 30), Enabled = false };
        orthoTab.Controls.Add(browse);
        autoSeed.CheckedChanged += (_,_) =>
        {
            seed.Enabled = !autoSeed.Checked;
            browse.Enabled = !autoSeed.Checked;
        };
        browse.Click += (_,_) =>
        {
            using OpenFileDialog picker = new()
            {
                Title = "Select a registered EMPTY native Plant 3D Ortho seed",
                Filter = "AutoCAD DWG (*.dwg)|*.dwg", CheckFileExists = true,
                InitialDirectory = Directory.Exists(Path.Combine(orthoFolder,"DWGs"))
                    ? Path.Combine(orthoFolder,"DWGs") : orthoFolder
            };
            if(picker.ShowDialog(this)==DialogResult.OK) seed.Text = picker.FileName;
        };

        Label vLabel = L("Native views (choose one or more)", 18, 196, 350);
        orthoTab.Controls.Add(vLabel);
        views.SetBounds(18, 222, 345, 165);
        views.CheckOnClick = true;
        views.IntegralHeight = false;
        foreach(string name in SupportDrawingUiOptions.AllViews)
            views.Items.Add(name, true);
        orthoTab.Controls.Add(views);
        Button all = new() {Text = "All 10", Location = new Point(18, 392),
            Size = new Size(83, 29)};
        Button none = new() {Text = "Clear", Location = new Point(110, 392),
            Size = new Size(83, 29)};
        all.Click += (_,_) => SetAllViews(true);
        none.Click += (_,_) => SetAllViews(false);
        orthoTab.Controls.Add(all);
        orthoTab.Controls.Add(none);
        // REV22.41: dimensions are generated only from verified native
        // StructureMember / Plant Support Start/End source geometry.
        autoDimLinear.SetBounds(18, 425, 355, 27);
        autoDimLinear.Text = "Auto DIMLINEAR (horizontal / vertical)";
        autoDimLinear.Checked = true;
        orthoTab.Controls.Add(autoDimLinear);

        Label aLabel = L("Annotations", 407, 196, 280);
        orthoTab.Controls.Add(aLabel);
        line.SetBounds(407, 222, 295, 24);
        line.Text = "Pipe Line Number Tag"; line.Checked = true;
        bop.SetBounds(407, 249, 295, 24);
        bop.Text = "Pipe BOP elevation"; bop.Checked = true;
        tos.SetBounds(407, 276, 295, 24);
        tos.Text = "Support / Structure TOS"; tos.Checked = true;
        description.SetBounds(407, 303, 295, 24);
        description.Text = "Support / Structure description"; description.Checked = true;
        orthoTab.Controls.AddRange(new Control[] {line,bop,tos,description});

        xrefNames.SetBounds(407, 336, 314, 29);
        xrefNames.Text = "Nearby XREF members: label source DWG";
        xrefNames.Checked = true;
        orthoTab.Controls.Add(xrefNames);
        batch.SetBounds(407, 370, 310, 45);
        batch.Text = "Batch Ortho: one native Ortho DWG per\r\nselected support BLOCK reference";
        batch.Checked = false;
        orthoTab.Controls.Add(batch);
        Label notice = L("Batch runs sequentially. Start with a copied project.",407,424,330);
        notice.ForeColor = Color.DarkGoldenrod;
        orthoTab.Controls.Add(notice);

        Label previewLabel = L("Preflight / configuration summary", 22, 630, 420);
        previewLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        Controls.Add(previewLabel);
        preview.SetBounds(20, 652, 600, 73);
        preview.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        preview.Multiline = true;
        preview.ReadOnly = true;
        preview.ScrollBars = ScrollBars.Vertical;
        preview.Text = "Project: " + projectRoot +
            "\r\nNo drawing writes occur until validation has passed.";
        Controls.Add(preview);

        preflightButton.Text = "Preflight";
        preflightButton.Size = new Size(110, 29);
        preflightButton.Location = new Point(637, 652);
        preflightButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        preflightButton.Click += (_,_) => ShowPreflight();
        Controls.Add(preflightButton);

        runButton.Text = "Run selected";
        runButton.Size = new Size(112, 30);
        runButton.Location = new Point(637, 689);
        runButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        runButton.Click += (_,_) => RunSelected();
        Controls.Add(runButton);
    }

    // Never clear user options on another drawing run. Refresh source context
    // only when the command is explicitly opened for that project.
    internal void UpdateProjectContext(string sourceDwg,string root,string folder)
    {
        bool differentProject=!string.Equals(projectRoot,root,
            StringComparison.OrdinalIgnoreCase);
        source=sourceDwg;
        projectRoot=root;
        orthoFolder=folder;
        sourceLabel.Text="Source: "+Path.GetFileName(sourceDwg)+
            "     |     Project: "+Path.GetFileName(root.TrimEnd('\\','/'));
        if(differentProject && !autoSeed.Checked)
        {
            // Never carry a manual seed from a different Plant project.
            autoSeed.Checked=true;
            seed.Text="";
        }
        SetStatus("Source: "+Path.GetFileName(sourceDwg)+
            " | Ready (persistent palette; options retained).");
    }

    internal void SetRunning(bool running)
    {
        IsRunning=running;
        runButton.Enabled=!running;
        preflightButton.Enabled=!running;
        tabs.Enabled=!running;
        if(running)
            SetStatus("Operation queued / in progress. Select CAD entities when prompted.");
        else
            SetStatus("Ready for another operation. Settings retained.");
    }

    internal void SetStatus(string message)
    {
        if(IsDisposed)return;
        if(InvokeRequired)
        {
            BeginInvoke(new Action<string>(SetStatus),message);
            return;
        }
        preview.Text=message;
    }

    private static Label L(string text, int x, int y, int w) =>
        new() {Text = text, Location = new Point(x,y), Size = new Size(w,23)};

    private static TabPage MakeInfoTab(string key,string name,string text)
    {
        TabPage tab = new(name) {Name = key};
        tab.Controls.Add(new Label
        {
            Text = text,
            Location = new Point(24,30),
            Size = new Size(665,105),
            Font = new Font("Segoe UI",10f),
            AutoSize = false
        });
        return tab;
    }

    private void SetAllViews(bool check)
    {
        for(int i=0;i<views.Items.Count;i++)
            views.SetItemChecked(i,check);
    }

    private void ShowPreflight()
    {
        string selected = tabs.SelectedTab?.Name ?? "ortho";
        if(selected != "ortho")
        {
            preview.Text = "Selected: " + tabs.SelectedTab?.Text +
                "\r\nThe existing native support-export command and dialog will be used.";
            return;
        }
        string[] chosen=views.CheckedItems.Cast<object>()
            .Select(v=>v.ToString() ?? "").Where(v=>v.Length>0).ToArray();
        preview.Text = "Read-only configuration preflight: " +
            Path.GetFileName(source) + "\r\n" +
            "Project Ortho folder: " + orthoFolder + "\r\n" +
            "Seed: " + (autoSeed.Checked ? "Auto (registered / empty)" : seed.Text) +
            "   |   Views: " + string.Join(",",chosen) +
            "   |   Scale: " + SupportDrawingPaperScale39.Display(
                SupportDrawingPaperScale39.Resolve(scale.Text,(int)customScale.Value)) +
            "   |   XREF member names: " + (xrefNames.Checked?"On":"Off") +
            "   |   Auto DIMLINEAR: " + (autoDimLinear.Checked?"On":"Off");
    }

    private void RunSelected()
    {
        if(IsRunning)return; // REV22.40 re-entry guard.
        string tab = tabs.SelectedTab?.Name ?? "ortho";
        string[] selected = views.CheckedItems.Cast<object>()
            .Select(v=>v.ToString() ?? "").Where(v=>v.Length>0).ToArray();
        if(tab=="ortho")
        {
            if(selected.Length==0)
            {
                MessageBox.Show(this,"Choose at least one view.",
                    "SPTC",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                return;
            }
            if(!autoSeed.Checked && !File.Exists(seed.Text.Trim()))
            {
                MessageBox.Show(this,"Choose an existing native Ortho seed DWG.",
                    "SPTC",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                return;
            }
            if(prefix.Text.Trim().Length>48 ||
                prefix.Text.IndexOfAny(Path.GetInvalidFileNameChars())>=0 ||
                prefix.Text.Contains("..",StringComparison.Ordinal) ||
                prefix.Text.EndsWith(".",StringComparison.Ordinal))
            {
                MessageBox.Show(this,"Enter a valid prefix (maximum 48 characters).",
                    "SPTC",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                return;
            }
        }
        int denominator=0;
        if(tab=="ortho")
        {
            try
            {
                denominator=SupportDrawingPaperScale39.Resolve(
                    scale.Text,(int)customScale.Value);
            }
            catch(ArgumentException ex)
            {
                MessageBox.Show(this,ex.Message,"Invalid paper scale",
                    MessageBoxButtons.OK,MessageBoxIcon.Warning);
                return;
            }
        }
        Settings = new SupportDrawingUiOptions(
            tab, cubeMode.SelectedIndex==0,
            autoSeed.Checked, seed.Text.Trim(), prefix.Text.Trim(), denominator,
            selected, line.Checked,bop.Checked,tos.Checked,
            description.Checked,batch.Checked,xrefNames.Checked,
            autoDimLinear.Checked);
        // Modeless palette remains open; dispatch in a fresh AutoCAD
        // command context rather than accessing Editor from this UI callback.
        RunRequested?.Invoke(Settings);
    }
}

/// <summary>Non-modal status window; never intercepts the AutoCAD Editor selection.</summary>
internal sealed class SupportDrawingProgressForm35 : WinForms.Form
{
    private readonly TextBox log = new();
    private readonly ProgressBar bar = new();
    internal SupportDrawingProgressForm35()
    {
        Text = "SPTC  /  Ortho generation progress";
        Icon = SupportBranding36.CreateIcon();
        Font = new Font("Segoe UI",9f);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(630,335);
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        ShowInTaskbar = false;
        Controls.Add(SupportBranding36.MakeLogo(14, 7, 110, 72));
        var title = new Label
        {
            Text = "Generating native Ortho drawing(s)",
            Location = new Point(136, 24), Size = new Size(475, 31),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold)
        };
        Controls.Add(title);
        bar.SetBounds(14,89,600,17);
        bar.Style = ProgressBarStyle.Marquee;
        Controls.Add(bar);
        log.SetBounds(14,115,600,201);
        log.ReadOnly = true;
        log.Multiline = true;
        log.ScrollBars = ScrollBars.Vertical;
        log.Anchor = AnchorStyles.Top|AnchorStyles.Bottom|
            AnchorStyles.Left|AnchorStyles.Right;
        Controls.Add(log);
        FormClosing += (_,e) =>
        {
            // Do not accidentally cancel unsupported native geometry API calls.
            if(!completed) e.Cancel=true;
        };
    }

    private bool completed;
    internal void Report(string message)
    {
        if(IsDisposed) return;
        if(InvokeRequired)
        {
            BeginInvoke(new Action<string>(Report),message);
            return;
        }
        log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " +
            message + Environment.NewLine);
    }
    internal void Complete()
    {
        completed=true;
        if(IsDisposed)return;
        bar.Style=ProgressBarStyle.Continuous;
        bar.Value=bar.Maximum;
        Report("Complete. Read the generated TXT reports for full diagnostics.");
    }
}
