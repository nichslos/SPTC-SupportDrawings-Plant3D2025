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
    bool AutoDimLinear,
    bool ReferenceDetailDimensions=true)
{
    public static readonly string[] AllViews =
        {"TOP","BOTTOM","LEFT","RIGHT","FRONT","BACK","NW","NE","SW","SE"};

    public bool HasView(string name) =>
        Views.Contains(name, StringComparer.OrdinalIgnoreCase);
}

internal sealed class SupportDrawingManagerForm35 : WinForms.UserControl
{
    // A fixed reference canvas inside a scrollable, DPI-aware host
    // prevents clipped Run/Preflight controls when an AutoCAD palette is
    // docked narrow or Windows display scaling is 125%/150%.
    // Geometry and commands are untouched; only WinForms layout changes.
    private readonly Panel layoutSurface = new();
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
    private readonly CheckBox referenceDimensions = new();
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
        MinimumSize = new Size(360, 420);
        BackColor = Color.White;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScroll = true;
        // The scrollable reference canvas retains the established 805px
        // layout, including all four operation tabs and footer controls.
        // Docking below its width exposes horizontal/vertical scrollbars
        // rather than overlapping columns or hiding action buttons.
        layoutSurface.Name = "SPTC_UI_LAYOUT_SURFACE";
        layoutSurface.SetBounds(0,0,805,765);
        layoutSurface.BackColor = Color.White;
        Controls.Add(layoutSurface);
        // REV22.40: Hosted permanently as a modeless AutoCAD PaletteSet
        // UserControl. No WinForms DialogResult or Close() on Run.

        // The supplied SPTC Engineering logo is embedded; no image path needed.
        // A larger top band keeps the full wordmark readable at Windows DPI.
        layoutSurface.Controls.Add(SupportBranding36.MakeLogo(18, 10, 184, 118));
        Label heading = new()
        {
            Text = "SUPPORT DRAWING MANAGER",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            Location = new Point(222, 21), Size = new Size(545, 33)
        };
        layoutSurface.Controls.Add(heading);
        Label brandSubtitle = new()
        {
            Text = "SPTC Engineering  |  AutoCAD Plant 3D 2025",
            ForeColor = Color.DimGray,
            Location = new Point(222, 57), Size = new Size(545, 25)
        };
        layoutSurface.Controls.Add(brandSubtitle);
        sourceLabel.Text = "Source: " + Path.GetFileName(sourceDwg) +
            "     |     Project: " + Path.GetFileName(projectRoot.TrimEnd('\\','/'));
        sourceLabel.Location = new Point(222, 94);
        sourceLabel.Size = new Size(545, 24);
        sourceLabel.AutoEllipsis = true;
        layoutSurface.Controls.Add(sourceLabel);

        tabs.Location = new Point(18, 140);
        tabs.Size = new Size(770, 475);
        tabs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
            AnchorStyles.Left | AnchorStyles.Right;
        layoutSurface.Controls.Add(tabs);

        TabPage orthoTab = new("10-View Ortho") {
            Name = "ortho", AutoScroll = true
        };
        // The settings area extends below the original tab viewport.
        // Explicit scroll extent makes the save/load/default controls
        // reachable at narrow palette sizes and higher Windows DPI.
        orthoTab.AutoScrollMinSize = new Size(735,600);
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
        // Presets are UI-only: the native camera selection contract
        // remains the unchanged 10-element checked list. Individual
        // checkboxes remain editable after choosing any preset.
        Button all = new() {Text = "All 10", Location = new Point(18, 392),
            Size = new Size(83, 29)};
        Button six = new() {Text = "Ortho 6", Location = new Point(110, 392),
            Size = new Size(87, 29)};
        Button four = new() {Text = "ISO 4", Location = new Point(206, 392),
            Size = new Size(83, 29)};
        Button none = new() {Text = "Clear", Location = new Point(299, 392),
            Size = new Size(83, 29)};
        all.Click += (_,_) => SetViewPreset("All");
        six.Click += (_,_) => SetViewPreset("Orthographic");
        four.Click += (_,_) => SetViewPreset("Isometric");
        none.Click += (_,_) => SetViewPreset("None");
        orthoTab.Controls.AddRange(new Control[] {all,six,four,none});
        // REV22.41: dimensions are generated only from verified native
        // StructureMember / Plant Support Start/End source geometry.
        autoDimLinear.SetBounds(18, 425, 355, 27);
        autoDimLinear.Text = "Auto H/V dimensions: native members only";
        autoDimLinear.Checked = true;
        orthoTab.Controls.Add(autoDimLinear);
        // REV22.47 defaults to the supplied pipe-support drawing examples:
        // grouped chain datums plus one outer overall per measured direction.
        // The REV22.46 per-member H/V mode is still available by unchecking.
        referenceDimensions.SetBounds(18, 456, 377, 30);
        referenceDimensions.Text = "Reference detail: chain + overall (PDF style)";
        referenceDimensions.Checked = true;
        orthoTab.Controls.Add(referenceDimensions);
        autoDimLinear.CheckedChanged += (_,_) =>
            referenceDimensions.Enabled=autoDimLinear.Checked;

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

        // Explicit local, per-project preferences: safe from accidental
        // project-switch propagation and no write occurs on Run.
        Label settingsCaption = L(
            "Project settings (local to this Windows user)",18,496,535);
        settingsCaption.Font = new Font("Segoe UI",9f,FontStyle.Bold);
        orthoTab.Controls.Add(settingsCaption);
        Button saveSettings = new() {
            Text="Save settings",Location=new Point(18,525),
            Size=new Size(130,30)};
        Button loadSettings = new() {
            Text="Load saved",Location=new Point(158,525),
            Size=new Size(120,30)};
        Button resetSettings = new() {
            Text="Defaults",Location=new Point(289,525),
            Size=new Size(108,30)};
        saveSettings.Click+=(_,_)=>SaveProjectPreferences();
        loadSettings.Click+=(_,_)=>LoadProjectPreferences(showFeedback:true);
        resetSettings.Click+=(_,_)=>{
            ApplyPreferences(SupportDrawingUiPreferences.Defaults);
            SetStatus("Default drawing settings restored (not saved).");
        };
        orthoTab.Controls.AddRange(new Control[]{
            saveSettings,loadSettings,resetSettings});
        Label privacy = L(
            "Manual Ortho seed paths are never saved. Existing export dialogs are unchanged.",
            18,563,690);
        privacy.ForeColor=Color.DimGray;
        orthoTab.Controls.Add(privacy);

        Label previewLabel = L("Preflight / configuration summary", 22, 630, 420);
        previewLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        layoutSurface.Controls.Add(previewLabel);
        preview.SetBounds(20, 652, 600, 73);
        preview.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        preview.Multiline = true;
        preview.ReadOnly = true;
        preview.ScrollBars = ScrollBars.Vertical;
        preview.Text = "Project: " + projectRoot +
            "\r\nNo drawing writes occur until validation has passed.";
        layoutSurface.Controls.Add(preview);

        preflightButton.Text = "Preflight";
        preflightButton.Size = new Size(110, 29);
        preflightButton.Location = new Point(637, 652);
        preflightButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        preflightButton.Click += (_,_) => ShowPreflight();
        layoutSurface.Controls.Add(preflightButton);

        runButton.Text = "Run selected";
        runButton.Size = new Size(112, 30);
        runButton.Location = new Point(637, 689);
        runButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        runButton.Click += (_,_) => RunSelected();
        layoutSurface.Controls.Add(runButton);
        // A newly opened palette restores only settings explicitly
        // saved for this Plant 3D project. No persisted queued command
        // or source geometry can be replayed.
        LoadProjectPreferences(showFeedback:false);
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
        if(differentProject)
        {
            // An existing modeless palette must never reuse another
            // project's prefix, manual seed or batch selection.
            ApplyPreferences(SupportDrawingUiPreferences.Defaults);
            LoadProjectPreferences(showFeedback:false);
        }
        SetStatus("Source: "+Path.GetFileName(sourceDwg)+
            " | Ready (persistent palette; project settings retained).");
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

    private void SetViewPreset(string preset)
    {
        var chosen=SupportDrawingUiPreferences.ViewPreset(preset);
        for(int i=0;i<views.Items.Count;i++)
            views.SetItemChecked(i,chosen.Contains(
                views.Items[i]?.ToString()??"",
                StringComparer.OrdinalIgnoreCase));
        SetStatus("View preset: "+preset+" ("+chosen.Length+
            " selected). Individual views remain editable.");
    }

    private void ApplyPreferences(SupportDrawingUiPreferences.Values saved)
    {
        var v=SupportDrawingUiPreferences.Validate(saved);
        tabs.SelectedIndex=0;
        cubeMode.SelectedIndex=v.SupportFocus?0:1;
        scale.SelectedItem=v.ScaleSelection;
        customScale.Value=v.CustomScale;
        prefix.Text=v.DrawingPrefix;
        for(int i=0;i<views.Items.Count;i++)
            views.SetItemChecked(i,v.Views.Contains(
                views.Items[i]?.ToString()??"",
                StringComparer.OrdinalIgnoreCase));
        line.Checked=v.LineNumber;
        bop.Checked=v.Bop;
        tos.Checked=v.Tos;
        description.Checked=v.Description;
        xrefNames.Checked=v.XrefMemberNames;
        autoDimLinear.Checked=v.AutoDimLinear;
        referenceDimensions.Checked=v.ReferenceDetailDimensions;
        batch.Checked=v.BatchOrtho;
        // Never persist manual DWG/seed paths.
        autoSeed.Checked=true;
        seed.Text="";
    }

    private SupportDrawingUiPreferences.Values CapturePreferences()
    {
        var chosen=views.CheckedItems.Cast<object>().Select(v=>
            v?.ToString()??"").ToArray();
        return SupportDrawingUiPreferences.Validate(new(
            scale.Text,(int)customScale.Value,prefix.Text.Trim(),
            cubeMode.SelectedIndex==0,chosen,line.Checked,bop.Checked,
            tos.Checked,description.Checked,xrefNames.Checked,
            autoDimLinear.Checked,referenceDimensions.Checked,
            batch.Checked));
    }

    private void SaveProjectPreferences()
    {
        try
        {
            SupportDrawingUiPreferences.Save(
                projectRoot,CapturePreferences());
            SetStatus("Project settings saved locally. "+
                "Manual seed path and commands were not saved.");
        }
        catch(System.Exception ex) when (ex is ArgumentException or
            IOException or UnauthorizedAccessException or
            System.Security.SecurityException)
        {
            MessageBox.Show(this,ex.Message,"Cannot save UI settings",
                MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
    }

    private void LoadProjectPreferences(bool showFeedback)
    {
        try
        {
            bool found=SupportDrawingUiPreferences.TryLoad(
                projectRoot,out var saved);
            ApplyPreferences(found?saved:
                SupportDrawingUiPreferences.Defaults);
            if(showFeedback)
                SetStatus(found?"Project settings loaded.":
                    "No valid saved settings for this project. Defaults restored.");
        }
        catch(System.Exception ex)
        {
            // Malformed files or environment restrictions may never
            // prevent native Ortho generation.
            ApplyPreferences(SupportDrawingUiPreferences.Defaults);
            if(showFeedback)SetStatus("UI defaults restored: "+ex.Message);
        }
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
            "   |   Views ("+chosen.Length+"): " + string.Join(",",chosen) +
            "   |   Scale: " + SupportDrawingPaperScale39.Display(
                SupportDrawingPaperScale39.Resolve(scale.Text,(int)customScale.Value)) +
            "   |   XREF member names: " + (xrefNames.Checked?"On":"Off") +
            "   |   Auto DIMLINEAR: " + (autoDimLinear.Checked?"On":"Off") +
            "   |   Detail style: " +
            (referenceDimensions.Checked?"Chain + overall":"All member H/V")+
            "   |   Batch Ortho: "+(batch.Checked?"Sequential":"Off")+
            "   |   Annotations: Line="+line.Checked+
            " BOP="+bop.Checked+" TOS="+tos.Checked+
            " Description="+description.Checked+
            "   |   Prefix: "+(prefix.Text.Length==0?
                "Automatic unique name":prefix.Text);
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
            autoDimLinear.Checked,referenceDimensions.Checked);
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
    private readonly Label countLabel = new();
    private int totalJobs,finishedJobs,failedJobs;
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
        countLabel.SetBounds(136,59,467,24);
        countLabel.Text="Preparing support drawings...";
        countLabel.ForeColor=Color.DimGray;
        Controls.Add(countLabel);
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

    internal void SetWorkload(int jobs)
    {
        if(IsDisposed)return;
        if(InvokeRequired)
        {
            BeginInvoke(new Action<int>(SetWorkload),jobs);
            return;
        }
        totalJobs=Math.Max(0,jobs);
        finishedJobs=0;
        failedJobs=0;
        bar.Style=ProgressBarStyle.Continuous;
        bar.Minimum=0;
        bar.Maximum=Math.Max(1,totalJobs);
        bar.Value=0;
        countLabel.Text=totalJobs>0?
            "Completed 0 / "+totalJobs+" support drawing(s)":
            "Preparing support drawing(s)...";
    }

    internal void AdvanceWorkload(bool succeeded)
    {
        if(IsDisposed)return;
        if(InvokeRequired)
        {
            BeginInvoke(new Action<bool>(AdvanceWorkload),succeeded);
            return;
        }
        finishedJobs=Math.Min(totalJobs,finishedJobs+1);
        if(!succeeded)failedJobs++;
        bar.Value=Math.Min(bar.Maximum,finishedJobs);
        countLabel.Text="Processed "+finishedJobs+" / "+totalJobs+
            "  |  Passed "+(finishedJobs-failedJobs)+
            "  |  Failed "+failedJobs;
    }

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
        bar.Value=Math.Min(bar.Maximum,finishedJobs);
        countLabel.Text=totalJobs==0?
            "Completed. See the report for details.":
            "Finished "+finishedJobs+" / "+totalJobs+
            "  |  Passed "+(finishedJobs-failedJobs)+
            "  |  Failed "+failedJobs;
        Report("Completed processing. Read generated TXT reports for full diagnostics.");
    }
}
