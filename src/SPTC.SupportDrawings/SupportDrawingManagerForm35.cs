using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
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
    bool ReferenceDetailDimensions=true,
    bool BypassOneSheetLimit=false,
    bool AllowDraftOverflow=false)
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
    private readonly Panel scrollingWorkspace = new();
    private readonly Panel stickyFooter = new();
    private bool arrangingStickyFooter;
    private bool arrangingWorkspace;
    private int lastWorkspaceWidth=-1;
    private readonly Label stickySummary=new();
    private readonly Button fixSettings=new();
    private readonly List<CollapsibleSection> uiSections = new();
    private readonly Button essentialsButton=new();
    private readonly Button allSettingsButton=new();
    private readonly ToolTip uiTips=new(){AutoPopDelay=12500,InitialDelay=550,ReshowDelay=250,ShowAlways=true};
    private readonly TabControl tabs = new();
    private sealed class CollapsibleSection
    {
        internal readonly Panel Frame=new();
        internal readonly Panel Body=new();
        internal readonly Button Header=new();
        internal int BodyHeight;
        internal bool Expanded;
        internal Control[] Fields=Array.Empty<Control>();
        internal readonly Dictionary<Control,Rectangle> WideBounds=new();
    }
    private readonly ComboBox cubeMode = new();
    private readonly CheckBox autoSeed = new();
    // REV76: one unambiguous UI choice maps to the existing two
    // generator flags. Neither the generator nor its defaults change.
    private readonly ComboBox outputMode = new();
    private readonly Label outputHint = new();
    private readonly Label selectedViewCount = new();
    private readonly Label readinessHint = new();
    private readonly Button quickDraft = new();
    private readonly Button quickPhysical = new();
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
    private readonly TextBox lastQa = new();
    private readonly ListBox generatedHistory = new();
    private readonly Button openDwgFolder = new();
    private readonly Button openDwg = new();
    private readonly Button openResult = new();
    private readonly Button copyQa = new();
    private readonly Label qaIndicator = new();
    private sealed record GeneratedDwg(string Path,string ResultPath);
    private readonly List<GeneratedDwg> outputHistory = new();
    private string? lastOutputDwg;
    private string? lastResultTxt;
    private string lastCopiedSummary = "";
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
        AutoScroll = false;
        // REV77: only the workspace scrolls. Run, Preflight and status
        // belong to the separate docked footer and remain visible.
        scrollingWorkspace.Dock=DockStyle.Fill;
        scrollingWorkspace.AutoScroll=true;
        scrollingWorkspace.BackColor=Color.White;
        stickyFooter.Dock=DockStyle.Bottom;
        stickyFooter.Height=116;
        stickyFooter.BackColor=Color.FromArgb(239,244,249);
        layoutSurface.Name = "SPTC_UI_LAYOUT_SURFACE";
        layoutSurface.SetBounds(0,0,805,630);
        layoutSurface.BackColor = Color.White;
        scrollingWorkspace.Controls.Add(layoutSurface);
        scrollingWorkspace.Resize+=(_,_)=>ResizeResponsivePalette();
        Controls.Add(scrollingWorkspace);
        Controls.Add(stickyFooter);
        stickyFooter.BringToFront();
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
        uiTips.SetToolTip(sourceLabel,
            "Active source drawing and Plant 3D project. Changes update with the modeless palette.");

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
        orthoTab.AutoScrollMinSize = new Size(755,850);
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
        // REV76: mutually exclusive output modes. The default remains
        // the original one-sheet fit; REV74/75 flags are calculated
        // from the selection, not duplicated or persisted separately.
        Label outputLabel=L("Drawing output mode",407,442,300);
        outputLabel.Font=new Font("Segoe UI",9f,FontStyle.Bold);
        orthoTab.Controls.Add(outputLabel);
        outputMode.SetBounds(407,469,315,29);
        outputMode.DropDownStyle=ComboBoxStyle.DropDownList;
        outputMode.Items.AddRange(new object[]{
            SupportDrawingOutputMode76.Physical,
            SupportDrawingOutputMode76.Draft,
            SupportDrawingOutputMode76.Split
        });
        outputMode.SelectedIndex=0;
        orthoTab.Controls.Add(outputMode);
        outputHint.SetBounds(407,502,318,50);
        outputHint.AutoEllipsis=false;
        outputHint.ForeColor=Color.DimGray;
        orthoTab.Controls.Add(outputHint);
        quickDraft.Text="Draft: All 10 Views";
        quickDraft.Name="SPTC_QUICK_DRAFT_78";
        quickDraft.SetBounds(18,144,170,30);
        quickDraft.Click+=(_,_)=>PrepareDraftViews();
        orthoTab.Controls.Add(quickDraft);
        quickPhysical.Text="Printable Sheet";
        quickPhysical.Name="SPTC_QUICK_PHYSICAL_78";
        quickPhysical.SetBounds(202,144,160,30);
        quickPhysical.Click+=(_,_)=>{
            outputMode.SelectedIndex=0;
            UpdateOutputModeUi();
            SetStatus("Printable single-sheet mode selected. "+
                "The generator will enforce the real sheet boundary.");
        };
        orthoTab.Controls.Add(quickPhysical);
        readinessHint.Name="SPTC_READINESS_HINT_78";
        readinessHint.SetBounds(18,190,700,27);
        readinessHint.ForeColor=Color.DimGray;
        orthoTab.Controls.Add(readinessHint);
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
        selectedViewCount.SetBounds(205,195,166,24);
        selectedViewCount.TextAlign=ContentAlignment.MiddleRight;
        selectedViewCount.ForeColor=Color.DimGray;
        orthoTab.Controls.Add(selectedViewCount);
        views.ItemCheck+=(_,_)=>{
            // The palette can apply saved presets before a WinForms
            // window handle exists. Update synchronously in that case;
            // ApplyPreferences refreshes again after all checks settle.
            if(IsHandleCreated)
                BeginInvoke(new Action(UpdateOutputModeUi));
        };
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
            "Project settings (local to this Windows user)",18,560,535);
        settingsCaption.Font = new Font("Segoe UI",9f,FontStyle.Bold);
        orthoTab.Controls.Add(settingsCaption);
        Button saveSettings = new() {
            Text="Save settings",Location=new Point(18,589),
            Size=new Size(130,30)};
        Button loadSettings = new() {
            Text="Load saved",Location=new Point(158,589),
            Size=new Size(120,30)};
        Button resetSettings = new() {
            Text="Defaults",Location=new Point(289,589),
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
            18,632,690);
        privacy.ForeColor=Color.DimGray;
        orthoTab.Controls.Add(privacy);

        // Issue package setup is explicitly opt-in, project-isolated,
        // and independent of the existing drawing generation command.
        // Native view geometry/dimensions remain unchanged.
        Button productionSettings=new() {
            Text="Production package...",
            Location=new Point(409,589),Size=new Size(220,30)};
        productionSettings.Click+=(_,_)=>{
            if(IsRunning)return;
            using var editor=new SupportProductionPackageDialog(projectRoot);
            if(editor.ShowDialog(this)==DialogResult.OK)
            {
                if(SupportProductionPackageSettingsStore.TryLoad(
                    projectRoot,out var confirmed,out string loadState))
                    SetStatus("Production settings verified: "+
                        "Package="+(confirmed.Enabled?"ON":"OFF")+
                        ", Native PDF="+(confirmed.PublishPdf?"ON":"OFF")+
                        " ("+loadState+").");
                else
                    SetStatus("Production settings could not be reloaded: "+
                        loadState+". Reopen Production package before running.");
            }
        };
        orthoTab.Controls.Add(productionSettings);

        // REV77: one actual-result entry per generated output. Selection
        // is always backed by a real DWG path, never by a guessed name.
        Label qaHeader=L("Session DWG history / measured QA",18,8,460);
        qaHeader.Font=new Font("Segoe UI",9f,FontStyle.Bold);
        orthoTab.Controls.Add(qaHeader);
        generatedHistory.SetBounds(18,35,704,91);
        generatedHistory.HorizontalScrollbar=true;
        generatedHistory.SelectedIndexChanged+=(_,_)=>RefreshSelectedQa();
        orthoTab.Controls.Add(generatedHistory);
        qaIndicator.SetBounds(18,135,700,24);
        qaIndicator.ForeColor=Color.DimGray;
        qaIndicator.Text="QA status: UNVERIFIED";
        orthoTab.Controls.Add(qaIndicator);
        lastQa.SetBounds(18,165,704,113);
        lastQa.Multiline=true;
        lastQa.ReadOnly=true;
        lastQa.ScrollBars=ScrollBars.Vertical;
        lastQa.Text="No completed Ortho drawing in this session.";
        orthoTab.Controls.Add(lastQa);
        openDwgFolder.Text="Open DWG folder";
        openDwgFolder.SetBounds(18,287,151,30);
        openDwgFolder.Enabled=false;
        openDwgFolder.Click+=(_,_)=>OpenGeneratedOutput(false);
        orthoTab.Controls.Add(openDwgFolder);
        openResult.Text="Open RESULT.txt";
        openResult.SetBounds(179,287,151,30);
        openResult.Enabled=false;
        openResult.Click+=(_,_)=>OpenGeneratedOutput(true);
        orthoTab.Controls.Add(openResult);
        copyQa.Text="Copy QA summary";
        copyQa.SetBounds(340,287,165,30);
        copyQa.Enabled=false;
        copyQa.Click+=(_,_)=>CopySelectedQa();
        orthoTab.Controls.Add(copyQa);
        openDwg.Text="Open DWG";
        openDwg.Name="SPTC_OPEN_DWG_79";
        openDwg.SetBounds(521,287,155,30);
        openDwg.Enabled=false;
        openDwg.Click+=(_,_)=>OpenSelectedDwg();
        orthoTab.Controls.Add(openDwg);

        outputMode.SelectedIndexChanged+=(_,_)=>UpdateOutputModeUi();
        batch.CheckedChanged+=(_,_)=>UpdateOutputModeUi();
        scale.SelectedIndexChanged+=(_,_)=>UpdateOutputModeUi();
        customScale.ValueChanged+=(_,_)=>UpdateOutputModeUi();
        tabs.SelectedIndexChanged+=(_,_)=>UpdateOutputModeUi();

        // REV77: all settings scroll in their own Ortho tab. The user
        // can collapse any section without losing its values.
        BuildCollapsibleSections(orthoTab,
            new Control[]{modeLabel,cubeMode,scaleLabel,scale,
                customLabel,customScale,prefixLabel,prefix,
                autoSeed,seed,browse},
            new Control[]{vLabel,views,selectedViewCount,all,six,four,none},
            new Control[]{outputLabel,outputMode,outputHint,batch,notice,
                quickDraft,quickPhysical,readinessHint},
            new Control[]{qaHeader,generatedHistory,qaIndicator,lastQa,
                openDwgFolder,openResult,copyQa,openDwg},
            new Control[]{autoDimLinear,referenceDimensions},
            new Control[]{aLabel,line,bop,tos,description,xrefNames},
            new Control[]{settingsCaption,saveSettings,loadSettings,
                resetSettings,productionSettings,privacy});
        InstallUiGuidance(orthoTab);

        stickySummary.Name="SPTC_FOOTER_SUMMARY_79";
        stickySummary.Font=new Font("Segoe UI",9f,FontStyle.Bold);
        stickySummary.ForeColor=Color.FromArgb(31,65,103);
        stickySummary.AutoEllipsis=true;
        stickyFooter.Controls.Add(stickySummary);
        fixSettings.Name="SPTC_FIX_SETTINGS_79";
        fixSettings.Text="Fix setting";
        fixSettings.Size=new Size(112,29);
        fixSettings.Visible=false;
        fixSettings.Click+=(_,_)=>FocusRequiredSetting();
        stickyFooter.Controls.Add(fixSettings);
        preview.Multiline=true;
        preview.ReadOnly=true;
        preview.ScrollBars=ScrollBars.Vertical;
        preview.Text="Project: "+projectRoot+
            "\r\nNo drawing writes occur until validation has passed.";
        stickyFooter.Controls.Add(preview);
        preflightButton.Text="Preflight";
        preflightButton.Size=new Size(110,29);
        preflightButton.Click+=(_,_)=>ShowPreflight();
        stickyFooter.Controls.Add(preflightButton);
        runButton.Text="Run selected";
        runButton.Size=new Size(112,30);
        runButton.Click+=(_,_)=>RunSelected();
        stickyFooter.Controls.Add(runButton);
        stickyFooter.Resize+=(_,_)=>ArrangeStickyFooter();
        // AutoScale may resize the footer before scaling its children.
        // A post-scale Layout event is also needed to restore true
        // right-docking after simulated or actual 125%/150% DPI.
        stickyFooter.Layout+=(_,_)=>ArrangeStickyFooter();
        ArrangeStickyFooter();
        // A newly opened palette restores only settings explicitly
        // saved for this Plant 3D project. No persisted queued command
        // or source geometry can be replayed.
        LoadProjectPreferences(showFeedback:false);
        UpdateOutputModeUi();
        ResizeResponsivePalette();
    }

    // REV77: section headers reflow inside the existing Ortho-tab's
    // AutoScroll viewport. Reparenting existing WinForms controls leaves
    // their handlers, input state, settings and command contracts intact.
    private void BuildCollapsibleSections(TabPage tab,
        Control[] sourceFields,Control[] nativeViews,
        Control[] outputFields,Control[] qaFields,
        Control[] dimensions,Control[] annotations,
        Control[] preferences)
    {
        // Compact each independent group without touching its values.
        outputFields[0].SetBounds(18,8,316,24);
        outputFields[1].SetBounds(18,40,333,29);
        outputFields[2].SetBounds(368,30,352,63);
        outputFields[3].SetBounds(18,91,330,45);
        outputFields[4].SetBounds(368,99,350,31);
        outputFields[5].SetBounds(18,153,166,30);
        outputFields[6].SetBounds(196,153,166,30);
        outputFields[7].SetBounds(18,198,686,26);
        dimensions[0].SetBounds(18,8,390,27);
        dimensions[1].SetBounds(18,43,415,31);
        annotations[0].SetBounds(18,8,306,24);
        for(int i=1;i<=4;i++)
            annotations[i].SetBounds(18,11+i*28,370,24);
        annotations[5].SetBounds(18,151,427,29);
        preferences[0].SetBounds(18,8,520,24);
        preferences[1].SetBounds(18,40,130,30);
        preferences[2].SetBounds(158,40,120,30);
        preferences[3].SetBounds(289,40,108,30);
        preferences[4].SetBounds(409,40,220,30);
        preferences[5].SetBounds(18,84,700,24);

        AddCollapsibleSection(tab,"1. Source, Scale & Seed",true,sourceFields);
        AddCollapsibleSection(tab,"2. Choose Native Views",true,nativeViews);
        AddCollapsibleSection(tab,"3. Drawing Output",true,outputFields);
        AddCollapsibleSection(tab,"Generation History & QA",false,qaFields);
        AddCollapsibleSection(tab,"Dimensions",false,dimensions);
        AddCollapsibleSection(tab,"Annotations & XREF",false,annotations);
        AddCollapsibleSection(tab,"Project Settings",false,preferences);
        ReflowSections(tab);
    }

    // REV78: guided discovery of essential fields and a safe, explicit
    // shortcut for the already-supported native ten-view draft flow.
    // No project coordinates, model paths or drawing dimensions are used.
    private void InstallUiGuidance(TabPage tab)
    {
        essentialsButton.Name="SPTC_SHOW_BASICS_78";
        essentialsButton.Text="Show essentials";
        essentialsButton.SetBounds(8,7,159,31);
        essentialsButton.Click+=(_,_)=>SetSectionVisibility(
            tab,showAdvanced:false);
        tab.Controls.Add(essentialsButton);

        allSettingsButton.Name="SPTC_SHOW_ALL_78";
        allSettingsButton.Text="Show all settings";
        allSettingsButton.SetBounds(181,7,164,31);
        allSettingsButton.Click+=(_,_)=>SetSectionVisibility(
            tab,showAdvanced:true);
        tab.Controls.Add(allSettingsButton);

        uiTips.SetToolTip(essentialsButton,
            "Show Source, Views and Output. Your hidden settings are preserved.");
        uiTips.SetToolTip(allSettingsButton,
            "Expand all sections to review dimensions, annotations and project settings.");
        uiTips.SetToolTip(cubeMode,
            "SupportFocus limits the native OrthoCube around the selected support.");
        uiTips.SetToolTip(scale,
            "Automatic allows standard output; Draft and Multiple Sheets require an explicit 1:N.");
        uiTips.SetToolTip(customScale,
            "Enter the exact 1:N denominator. It is not a paper dimension.");
        uiTips.SetToolTip(autoSeed,
            "Use an empty Ortho seed already registered in this Plant 3D project.");
        uiTips.SetToolTip(prefix,
            "Optional filename prefix. Leave blank to use native automatic naming.");
        uiTips.SetToolTip(outputMode,
            "Printable sheet, single-DWG Draft Overflow, or split sheets without changing scale.");
        uiTips.SetToolTip(quickDraft,
            "Select all ten views and Draft Overflow; keep your selected exact drawing scale.");
        uiTips.SetToolTip(quickPhysical,
            "Return to normal printable-sheet constraints without changing your view choices.");
        uiTips.SetToolTip(batch,
            "Run sequentially for support BLOCK references. Incompatible with Draft and Split.");
        uiTips.SetToolTip(xrefNames,
            "Annotate nearby verified XREF structural/support members with source drawing names.");
        uiTips.SetToolTip(generatedHistory,
            "Select any DWG from this project's current session to inspect its RESULT.txt.");
        uiTips.SetToolTip(copyQa,
            "Copy only the chosen DWG's measured QA summary to the clipboard.");
        uiTips.SetToolTip(preflightButton,
            "Review current selections. Physical page fit is verified during generation.");
        uiTips.SetToolTip(runButton,
            "Start the existing AutoCAD command using exactly the selected options.");
    }

    private void SetSectionVisibility(TabPage tab,bool showAdvanced)
    {
        if(IsRunning)return;
        // First three sections are the primary drawing workflow.
        // On the basics layout, retain results when outputs exist.
        for(int i=0;i<uiSections.Count;i++)
            uiSections[i].Expanded=showAdvanced||i<3||
                (i==3&&outputHistory.Count>0);
        ReflowSections(tab);
    }

    private void PrepareDraftViews()
    {
        if(IsRunning)return;
        SetViewPreset("All");
        batch.Checked=false;
        outputMode.SelectedIndex=(int)SupportDrawingOutputMode76.Mode.Draft;
        var tab=tabs.TabPages.Cast<TabPage>()
            .First(p=>p.Name=="ortho");
        uiSections[0].Expanded=true;
        uiSections[1].Expanded=true;
        uiSections[2].Expanded=true;
        ReflowSections(tab);
        UpdateOutputModeUi();
        // An explicit scale is a user decision, never silently changed.
        int denominator=SupportDrawingPaperScale39.Resolve(
            scale.Text,(int)customScale.Value);
        SetStatus(denominator>0?
            "Draft configured: All 10 / 1:"+denominator+
            ". PDF remains disabled for off-sheet drawings. Click Preflight to review.":
            "All 10 Draft selected. Choose an exact 1:N scale "+
            "before Run (Automatic is not valid for Draft Overflow).");
    }

    private void AddCollapsibleSection(TabPage tab,string name,
        bool expanded,params Control[] children)
    {
        var section=new CollapsibleSection{Expanded=expanded};
        int left=children.Min(c=>c.Left);
        int top=children.Min(c=>c.Top);
        section.BodyHeight=children.Max(c=>c.Bottom)-top+14;
        section.Frame.Width=737;
        section.Frame.BackColor=Color.White;
        section.Frame.BorderStyle=BorderStyle.FixedSingle;
        section.Header.FlatStyle=FlatStyle.Flat;
        section.Header.FlatAppearance.BorderSize=0;
        section.Header.Font=new Font("Segoe UI",9f,FontStyle.Bold);
        section.Header.TextAlign=ContentAlignment.MiddleLeft;
        section.Header.BackColor=Color.FromArgb(236,242,248);
        section.Header.SetBounds(0,0,734,33);
        section.Body.SetBounds(0,35,734,section.BodyHeight);
        section.Body.BackColor=Color.White;
        foreach(Control child in children)
        {
            int dx=child.Left-left+9;
            int dy=child.Top-top+5;
            section.Body.Controls.Add(child);
            child.Location=new Point(dx,dy);
        }
        section.Frame.Controls.Add(section.Header);
        section.Frame.Controls.Add(section.Body);
        section.Header.Click+=(_,_)=>{
            if(IsRunning)return;
            section.Expanded=!section.Expanded;
            ReflowSections(tab);
        };
        uiSections.Add(section);
        tab.Controls.Add(section.Frame);
        section.Header.Tag=name;
    }

    private void ReflowSections(TabPage tab)
    {
        tab.SuspendLayout();
        try
        {
            int y=47;
            foreach(var section in uiSections)
            {
                string name=section.Header.Tag?.ToString()??"Settings";
                section.Header.Text=(section.Expanded?"[-]  ":"[+]  ")+name;
                section.Body.Visible=section.Expanded;
                section.Frame.Height=section.Expanded?
                    35+section.BodyHeight:34;
                section.Frame.Location=new Point(8,y);
                y+=section.Frame.Height+9;
            }
            tab.AutoScrollMinSize=new Size(755,y+10);
        }
        finally{tab.ResumeLayout();}
    }

    private void ArrangeStickyFooter()
    {
        if(arrangingStickyFooter||stickyFooter.IsDisposed)return;
        int w=stickyFooter.ClientSize.Width;
        int h=stickyFooter.ClientSize.Height;
        if(w<=0||h<=0)return;
        arrangingStickyFooter=true;
        try
        {
            // Use the actual DPI-scaled button dimensions, not their
            // original 96-DPI values. No CAD/paper measurements used.
            int buttonWidth=Math.Max(runButton.Width,preflightButton.Width);
            int right=Math.Max(128,w-buttonWidth-12);
            int top=Math.Max(10,(h-runButton.Height-
                preflightButton.Height-9)/2);
            preflightButton.Location=new Point(right,top);
            runButton.Location=new Point(right,
                top+preflightButton.Height+9);
            preview.SetBounds(12,Math.Max(24,top),
                Math.Max(100,right-25),Math.Max(35,h-Math.Max(24,top)-12));
        }
        finally{arrangingStickyFooter=false;}
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
            ClearGeneratedOutput();
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
        {
            // REV77: history persists across runs within this project.
            // New output is appended only when GenerateOne actually
            // completes; an aborted run cannot fabricate QA results.
            SetStatus("Generating... previous DWGs remain in session history.");
        }
        else
            SetStatus("Ready for another operation. Settings retained.");
        UpdateOutputModeUi();
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
        UpdateOutputModeUi();
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
        // Deliberately NOT persisted: opt-in overflow/splitting resets
        // on project change, Load settings or Restore defaults.
        outputMode.SelectedIndex=0;
        // Never persist manual DWG/seed paths.
        autoSeed.Checked=true;
        seed.Text="";
        UpdateOutputModeUi();
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

    private SupportDrawingOutputMode76.Mode SelectedOutputMode() =>
        SupportDrawingOutputMode76.FromIndex(
            Math.Max(0,outputMode.SelectedIndex));

    // REV76: UI-only eligibility. An incompatible 10-view output choice
    // disables Run before AutoCAD selection, without silently substituting
    // automatic scale or changing the user's view selections.
    private void UpdateOutputModeUi()
    {
        if(IsDisposed)return;
        int count=views.CheckedItems.Count;
        selectedViewCount.Text=count+" of 10 selected";
        var mode=SelectedOutputMode();
        int denominator=0;
        try
        {
            if(scale.SelectedIndex>=0)
                denominator=SupportDrawingPaperScale39.Resolve(
                    scale.Text,(int)customScale.Value);
        }
        catch(ArgumentException){denominator=0;}
        bool valid=SupportDrawingOutputMode76.Eligible(
            mode,count,denominator,batch.Checked);
        bool isOrtho=tabs.SelectedTab?.Name=="ortho";
        string modeInfo=SupportDrawingOutputMode76.Explanation(mode);
        outputHint.Text=modeInfo+
            (!valid?"\r\nTo continue: select all 10 views, "+
                "choose a 1:N scale, and turn Batch OFF.":"");
        outputHint.ForeColor=!valid?Color.Firebrick:
            mode==SupportDrawingOutputMode76.Mode.Draft?
                Color.DarkGoldenrod:Color.DimGray;
        string scaleText=denominator>0?"1:"+denominator:"Automatic";
        string readiness;
        if(IsRunning)
            readiness="Generating... CAD source selection and progress remain in AutoCAD.";
        else if(!isOrtho)
            readiness="Ready: use the original "+tabs.SelectedTab?.Text+" workflow.";
        else if(count==0)
            readiness="Select at least one native view before running.";
        else if(!valid)
            readiness="Action required: All 10 views, exact 1:N and Batch OFF.";
        else if(mode==SupportDrawingOutputMode76.Mode.Draft)
            readiness="Ready: "+count+" views / "+scaleText+
                " / ONE DWG — DRAFT ONLY (native PDF blocked).";
        else if(mode==SupportDrawingOutputMode76.Mode.Split)
            readiness="Ready: "+count+" views / "+scaleText+
                " / separate printable sheets if needed.";
        else
            readiness="Ready: "+count+" view(s) / "+scaleText+
                " / physical paper fit enforced.";
        readinessHint.Text=readiness;
        readinessHint.ForeColor=(!valid||count==0)&&isOrtho?
            Color.Firebrick:mode==SupportDrawingOutputMode76.Mode.Draft?
                Color.DarkGoldenrod:Color.DarkGreen;
        if(runButton!=null)
            runButton.Enabled=!IsRunning&&(!isOrtho||count>0&&valid);
    }

    private void ClearGeneratedOutput()
    {
        outputHistory.Clear();
        generatedHistory.Items.Clear();
        lastOutputDwg=null;
        lastResultTxt=null;
        lastCopiedSummary="";
        openDwgFolder.Enabled=false;
        openResult.Enabled=false;
        copyQa.Enabled=false;
        qaIndicator.Text="QA status: UNVERIFIED";
        qaIndicator.ForeColor=Color.DimGray;
        lastQa.Text="No generated Ortho drawings in this project session.";
    }

    // Called only with a DWG actually emitted by GenerateOne, never with
    // a guessed output name or arbitrary report path. All history entries
    // are scoped to the current project and the current palette session.
    internal void RecordGeneratedDwg(string dwg)
    {
        if(IsDisposed)return;
        if(InvokeRequired)
        {
            BeginInvoke(new Action<string>(RecordGeneratedDwg),dwg);
            return;
        }
        if(string.IsNullOrWhiteSpace(dwg)||!File.Exists(dwg))
            return;
        string full=Path.GetFullPath(dwg);
        // Avoid reusing an output from another project if a callback
        // reaches this palette after its source project was switched.
        string relative=Path.GetRelativePath(projectRoot,full);
        if(Path.IsPathRooted(relative)||
           relative==".."||
           relative.StartsWith(".."+Path.DirectorySeparatorChar,
               StringComparison.Ordinal)||
           relative.StartsWith(".."+Path.AltDirectorySeparatorChar,
               StringComparison.Ordinal))
            return;
        string report=Path.Combine(Path.GetDirectoryName(full)??"",
            Path.GetFileNameWithoutExtension(full)+"_RESULT.txt");
        int existing=outputHistory.FindIndex(x=>
            x.Path.Equals(full,StringComparison.OrdinalIgnoreCase));
        if(existing>=0)
        {
            // A regenerated file has fresh real evidence. Re-select it.
            outputHistory.RemoveAt(existing);
            generatedHistory.Items.RemoveAt(existing);
        }
        outputHistory.Add(new GeneratedDwg(full,report));
        generatedHistory.Items.Add(
            outputHistory.Count.ToString("00")+"  "+Path.GetFileName(full));
        generatedHistory.SelectedIndex=outputHistory.Count-1;
        // Surface the actual result immediately, even if the QA section
        // was hidden in the simpler essentials-only layout.
        if(uiSections.Count>3&&!uiSections[3].Expanded)
        {
            uiSections[3].Expanded=true;
            var orthoTab=tabs.TabPages.Cast<TabPage>()
                .First(p=>p.Name=="ortho");
            ReflowSections(orthoTab);
        }
    }

    // Selection-only read; no mutation of a DWG, source file, project
    // registration, PDF sidecar or a result TXT.
    private void RefreshSelectedQa()
    {
        int index=generatedHistory.SelectedIndex;
        bool has=index>=0&&index<outputHistory.Count;
        lastOutputDwg=has?outputHistory[index].Path:null;
        lastResultTxt=has?outputHistory[index].ResultPath:null;
        openDwgFolder.Enabled=has&&Directory.Exists(
            Path.GetDirectoryName(lastOutputDwg??""));
        openResult.Enabled=has&&File.Exists(lastResultTxt);
        copyQa.Enabled=false;
        if(!has)
        {
            lastQa.Text="Select a generated DWG to review its recorded QA.";
            qaIndicator.Text="QA status: UNVERIFIED";
            qaIndicator.ForeColor=Color.DimGray;
            return;
        }
        string name=Path.GetFileName(lastOutputDwg)??"(unavailable)";
        string risk="UNVERIFIED";
        string detail="No matching RESULT.txt. Actual QA cannot be verified.";
        try
        {
            if(openResult.Enabled)
            {
                string raw=File.ReadAllText(lastResultTxt!);
                var summary=SupportDrawingQaSummary76.Read(raw);
                risk=SupportDrawingQaStatus77.Assess(raw,summary);
                detail=SupportDrawingQaSummary76.Format(summary);
            }
        }
        catch(System.Exception ex) when(ex is IOException or
            UnauthorizedAccessException)
        {
            detail="RESULT.txt could not be read: "+ex.Message;
        }
        qaIndicator.Text="QA status: "+risk;
        qaIndicator.ForeColor=risk.StartsWith("FAIL",
            StringComparison.Ordinal)?Color.Firebrick:
            risk.StartsWith("PASS",StringComparison.Ordinal)?
                Color.DarkGreen:risk=="REVIEW"?
                    Color.DarkGoldenrod:Color.DimGray;
        lastCopiedSummary=name+"\r\n"+qaIndicator.Text+"\r\n"+detail;
        lastQa.Text=lastCopiedSummary;
        copyQa.Enabled=openResult.Enabled;
    }

    private void CopySelectedQa()
    {
        if(!copyQa.Enabled||lastCopiedSummary.Length==0)return;
        try
        {
            Clipboard.SetText(lastCopiedSummary);
            SetStatus("Copied the selected DWG's report-derived QA summary.");
        }
        catch(System.Exception ex) when(ex is
            System.Runtime.InteropServices.ExternalException or
            System.Threading.ThreadStateException)
        {
            SetStatus("Clipboard unavailable: "+ex.Message);
        }
    }

    private void OpenGeneratedOutput(bool reportFile)
    {
        string? path=reportFile?lastResultTxt:
            Path.GetDirectoryName(lastOutputDwg??"");
        if(string.IsNullOrWhiteSpace(path)||
           (reportFile?!File.Exists(path):!Directory.Exists(path)))
        {
            SetStatus("Generated output is unavailable. Check the Ortho folder.");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo {
                FileName=path,UseShellExecute=true
            });
        }
        catch(System.Exception ex) when (ex is System.ComponentModel.Win32Exception or
            IOException or UnauthorizedAccessException)
        {
            SetStatus("Could not open output: "+ex.Message);
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
        var selectedMode=SelectedOutputMode();
        int d=SupportDrawingPaperScale39.Resolve(
            scale.Text,(int)customScale.Value);
        bool eligible=SupportDrawingOutputMode76.Eligible(
            selectedMode,chosen.Length,d,batch.Checked);
        preview.Text =
            "READ-ONLY CONFIGURATION PREFLIGHT\r\n"+
            "Source: "+Path.GetFileName(source)+"\r\n"+
            "1. VIEWS: "+chosen.Length+" / 10 | "+
                string.Join(", ",chosen)+"\r\n"+
            "2. SCALE: "+SupportDrawingPaperScale39.Display(d)+"\r\n"+
            "3. OUTPUT: "+outputMode.Text+"\r\n"+
            "4. SEED: "+(autoSeed.Checked?
                "Automatic registered empty Ortho seed":seed.Text)+"\r\n"+
            "5. ANNOTATIONS: Line="+line.Checked+
                " | BOP="+bop.Checked+" | TOS="+tos.Checked+
                " | Description="+description.Checked+
                " | XREF Names="+xrefNames.Checked+"\r\n"+
            "6. DIMENSIONS: "+(autoDimLinear.Checked?
                (referenceDimensions.Checked?
                    "Native H/V chain + overall":"Native H/V all members"):"OFF")+
                " | Batch: "+(batch.Checked?"ON":"OFF")+"\r\n"+
            "PREFIX: "+(prefix.Text.Length==0?
                "Automatic":prefix.Text)+"\r\n"+
            (eligible?
                "READY FOR NATIVE VALIDATION. ":
                "CHECK SETTINGS: Draft/Split need All 10, 1:N, Batch OFF. ")+
            SupportDrawingOutputMode76.Explanation(selectedMode)+
            "\r\nNote: this checks UI settings only. Actual paper fit, "+
            "source geometry and QA are verified during generation.";
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
        // REV76: same existing REV74/75 constraints, now applying to
        // either non-default output mode. No automatic silent downgrade.
        var chosenOutput=SelectedOutputMode();
        if(tab=="ortho"&&!SupportDrawingOutputMode76.Eligible(
            chosenOutput,selected.Length,denominator,batch.Checked))
        {
            MessageBox.Show(this,
                "This output mode requires All 10, an explicit 1:N scale, "+
                "and Batch Ortho OFF. Choose Single Sheet or correct the "+
                "selection before running.",
                "REV76 output-mode preflight",
                MessageBoxButtons.OK,MessageBoxIcon.Warning);
            return;
        }
        Settings = new SupportDrawingUiOptions(
            tab, cubeMode.SelectedIndex==0,
            autoSeed.Checked, seed.Text.Trim(), prefix.Text.Trim(), denominator,
            selected, line.Checked,bop.Checked,tos.Checked,
            description.Checked,batch.Checked,xrefNames.Checked,
            autoDimLinear.Checked,referenceDimensions.Checked,
            SupportDrawingOutputMode76.BypassOneSheetLimit(chosenOutput),
            SupportDrawingOutputMode76.AllowDraftOverflow(chosenOutput));
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
        AutoScaleMode = AutoScaleMode.Dpi;
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
        countLabel.Name="SPTC_PROGRESS_COUNTS";
        countLabel.SetBounds(136,59,467,24);
        countLabel.Text="Preparing support drawings...";
        countLabel.ForeColor=Color.DimGray;
        countLabel.Anchor=AnchorStyles.Top|AnchorStyles.Left|
            AnchorStyles.Right;
        countLabel.AutoEllipsis=true;
        Controls.Add(countLabel);
        bar.SetBounds(14,89,600,17);
        bar.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
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
