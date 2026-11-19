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
    bool AllowDraftOverflow=false,
    string HiddenLineMode=NativeOrthoAppearance83.ProjectDefault,
    string InsulationMode=NativeOrthoAppearance83.ProjectDefault)
{
    public static readonly string[] AllViews =
        {"TOP","BOTTOM","LEFT","RIGHT","FRONT","BACK","NW","NE","SW","SE"};

    public bool HasView(string name) =>
        Views.Contains(name, StringComparer.OrdinalIgnoreCase);
}

internal sealed class SupportDrawingManagerForm35 : WinForms.UserControl
{
    private readonly Panel layoutSurface = new();
    private readonly Panel scrollingWorkspace = new();
    private readonly Panel stickyFooter = new();
    private bool arrangingStickyFooter;
    private readonly Label stickySummary=new();
    private readonly Button fixSettings=new();
    private readonly ToolTip uiTips=new(){AutoPopDelay=12500,InitialDelay=550,ReshowDelay=250,ShowAlways=true};
    private readonly TabControl tabs=new(){Name="SPTC_OPERATIONS",Multiline=true};
    private readonly TabControl settingsPages=new(){Name="SPTC_ORTHO_SETTINGS",Dock=DockStyle.Fill,Multiline=true};
    private readonly SupportDrawingChoiceGroup cubeMode=new("OrthoCube mode","Support focus","Original OrthoCube");
    private readonly CheckBox autoSeed = new();
    private readonly SupportDrawingChoiceGroup outputMode=new("Drawing output",
        SupportDrawingOutputMode76.Physical,SupportDrawingOutputMode76.Draft,SupportDrawingOutputMode76.Split);
    private readonly Label outputHint = new();
    private readonly SupportDrawingChoiceGroup hiddenLineMode=new("Hidden lines",NativeOrthoAppearance83.HiddenChoices);
    private readonly SupportDrawingChoiceGroup insulationMode=new("Insulation",NativeOrthoAppearance83.InsulationChoices);
    private readonly Label selectedViewCount = new();
    private readonly Label readinessHint = new();
    private readonly Button quickDraft = new();
    private readonly Button quickPhysical = new();
    private readonly TextBox seed = new();
    private readonly TextBox prefix = new();
    private readonly SupportDrawingChoiceGroup scale=new("Paper scale",SupportDrawingUiPreferences.ScaleChoices);
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
        scrollingWorkspace.AutoScroll = true;
        scrollingWorkspace.BackColor=Color.White;
        stickyFooter.Dock=DockStyle.Bottom;
        stickyFooter.Height=116;
        stickyFooter.BackColor=Color.FromArgb(239,244,249);
        layoutSurface.Name = "SPTC_UI_LAYOUT_SURFACE";
        layoutSurface.Dock=DockStyle.Fill;
        layoutSurface.BackColor=Color.White;
        scrollingWorkspace.Controls.Add(layoutSurface);
        Controls.Add(scrollingWorkspace);
        Controls.Add(stickyFooter);

        var header=new TableLayoutPanel {Dock=DockStyle.Top,Height=76,ColumnCount=2,RowCount=2,
            Padding=new Padding(8,4,8,4)};
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,110));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        header.RowStyles.Add(new RowStyle(SizeType.Percent,55));
        header.RowStyles.Add(new RowStyle(SizeType.Percent,45));
        var logo=SupportBranding36.MakeLogo(0,0,106,64);
        logo.Dock=DockStyle.Fill;
        header.Controls.Add(logo,0,0);
        header.SetRowSpan(logo,2);
        var heading=new Label {Text="Support Drawing Manager",Dock=DockStyle.Fill,AutoEllipsis=true,
            Font=new Font(Font.FontFamily,12f,FontStyle.Bold),TextAlign=ContentAlignment.BottomLeft};
        header.Controls.Add(heading,1,0);
        sourceLabel.Text="Source: "+Path.GetFileName(sourceDwg)+"     |     Project: "+
            Path.GetFileName(projectRoot.TrimEnd('\\','/'));
        sourceLabel.Dock=DockStyle.Fill;
        sourceLabel.AutoEllipsis=true;
        sourceLabel.ForeColor=SystemColors.GrayText;
        header.Controls.Add(sourceLabel,1,1);
        tabs.Dock=DockStyle.Fill;
        layoutSurface.Controls.Add(tabs);
        layoutSurface.Controls.Add(header);

        TabPage orthoTab=new("10-View Ortho"){Name="ortho",Padding=new Padding(4)};
        orthoTab.Controls.Add(settingsPages);
        tabs.TabPages.Add(orthoTab);
        tabs.TabPages.Add(MakeInfoTab("batch", "Support Batch",
            "Create one DWG per support block. Click Run selected to choose blocks and an output folder."));

        cubeMode.Name="SPTC_CUBE_MODE";
        scale.Name="SPTC_PAPER_SCALE";
        outputMode.Name="SPTC_OUTPUT_MODE";
        hiddenLineMode.Name="SPTC_HIDDEN_LINES_83";
        insulationMode.Name="SPTC_INSULATION_83";

        var setup=AddSettingsPage("setup","Setup");
        AddRows(setup,Caption("OrthoCube mode"),cubeMode,Caption("Paper scale"),scale);
        var customLabel=Caption("Custom scale denominator (1:N)");
        customScale.AccessibleName="Custom scale denominator";
        customScale.Minimum=SupportDrawingPaperScale39.Min;
        customScale.Maximum=SupportDrawingPaperScale39.Max;
        customScale.Value=25;
        customScale.DecimalPlaces=0;
        customScale.MaximumSize=new Size(180,0);
        customScale.TextAlign=HorizontalAlignment.Right;
        AddRows(setup,customLabel,customScale);
        void ShowCustomScale()
        {
            bool custom=scale.SelectedValue==SupportDrawingPaperScale39.Custom;
            customLabel.Visible=custom;
            customScale.Visible=custom;
            customScale.Enabled=custom;
        }
        scale.SelectionChanged+=(_,_)=>{ShowCustomScale();UpdateOutputModeUi();};
        ShowCustomScale();
        prefix.AccessibleName="Drawing prefix";
        prefix.PlaceholderText="Automatic unique name";
        AddRows(setup,Caption("Drawing prefix (optional)"),prefix);
        autoSeed.Text="Automatically select a verified empty Ortho seed";
        autoSeed.Checked=true;
        seed.AccessibleName="Ortho seed drawing";
        seed.PlaceholderText="Select a registered empty Ortho DWG";
        var browse=new Button {Text="Browse seed..."};
        var seedActions=ActionRow(browse);
        AddRows(setup,Caption("Ortho seed"),autoSeed,seed,seedActions);
        void ShowManualSeed()
        {
            seed.Visible=!autoSeed.Checked;
            seedActions.Visible=!autoSeed.Checked;
        }
        autoSeed.CheckedChanged+=(_,_)=>{ShowManualSeed();UpdateOutputModeUi();};
        ShowManualSeed();
        browse.Click+=(_,_)=>{
            using OpenFileDialog picker=new() {
                Title="Select a registered EMPTY native Plant 3D Ortho seed",
                Filter="AutoCAD DWG (*.dwg)|*.dwg", CheckFileExists=true,
                InitialDirectory=Directory.Exists(Path.Combine(orthoFolder,"DWGs"))?
                    Path.Combine(orthoFolder,"DWGs"):orthoFolder
            };
            if(picker.ShowDialog(this)==DialogResult.OK)seed.Text=picker.FileName;
        };

        var viewPage=AddSettingsPage("views","Views");
        views.AccessibleName="Native views";
        views.CheckOnClick=true;
        views.IntegralHeight=false;
        foreach(string name in SupportDrawingUiOptions.AllViews)views.Items.Add(name,true);
        views.Height=views.ItemHeight*views.Items.Count+6;
        views.ItemCheck+=(_,_)=>{
            if(IsHandleCreated)BeginInvoke(new Action(UpdateOutputModeUi));
        };
        Button Preset(string caption,string preset)
        {
            var button=new Button {Text=caption};
            button.Click+=(_,_)=>SetViewPreset(preset);
            return button;
        }
        AddRows(viewPage,Caption("Native views"),ActionRow(Preset("All 10","All"),
            Preset("Ortho 6","Orthographic"),Preset("ISO 4","Isometric"),Preset("Clear","None")),
            views,selectedViewCount);

        var output=AddSettingsPage("output","Output");
        batch.Text="Batch Ortho for selected support blocks";
        quickDraft.Name="SPTC_QUICK_DRAFT_78";
        quickDraft.Text="Draft all 10 views";
        quickDraft.Click+=(_,_)=>PrepareDraftViews();
        quickPhysical.Name="SPTC_QUICK_PHYSICAL_78";
        quickPhysical.Text="Printable sheet";
        quickPhysical.Click+=(_,_)=>{
            outputMode.SelectedIndex=0;
            UpdateOutputModeUi();
            SetStatus("Printable single-sheet mode selected.");
        };
        readinessHint.Name="SPTC_READINESS_HINT_78";
        AddRows(output,Caption("Drawing output"),outputMode,outputHint,batch,
            Caption("Quick setup"),ActionRow(quickPhysical,quickDraft),readinessHint);

        var appearance=AddSettingsPage("appearance","Appearance");
        AddRows(appearance,Caption("Hidden lines"),hiddenLineMode,Caption("Insulation"),insulationMode);
        var details=AddSettingsPage("details","Details");
        autoDimLinear.Text="Auto H/V dimensions: native members only";
        autoDimLinear.Checked = true;
        referenceDimensions.Text="Reference detail: chain + overall (PDF style)";
        referenceDimensions.Checked = true;
        autoDimLinear.CheckedChanged+=(_,_)=>referenceDimensions.Enabled=autoDimLinear.Checked;
        line.Text="Pipe line number";line.Checked=true;
        bop.Text="Bottom of pipe (BOP)";bop.Checked=true;
        tos.Text="Top of steel (TOS)";tos.Checked=true;
        description.Text="Support and structure description";description.Checked=true;
        xrefNames.Text="Nearby XREF members: label source DWG";
        xrefNames.Checked = true;
        AddRows(details,Caption("Dimensions"),autoDimLinear,referenceDimensions,
            Caption("Annotations"),line,bop,tos,description,xrefNames);

        var project=AddSettingsPage("project","Project");
        var saveSettings=new Button {Text="Save settings"};
        var loadSettings=new Button {Text="Load saved"};
        var resetSettings=new Button {Text="Defaults"};
        saveSettings.Click+=(_,_)=>SaveProjectPreferences();
        loadSettings.Click+=(_,_)=>LoadProjectPreferences(showFeedback:true);
        resetSettings.Click+=(_,_)=>{
            ApplyPreferences(SupportDrawingUiPreferences.Defaults);
            SetStatus("Default drawing settings restored (not saved).");
        };
        var productionSettings=new Button {Text="Production package..."};
        productionSettings.Click+=(_,_)=>{
            if(IsRunning)return;
            using var editor=new SupportProductionPackageDialog(projectRoot);
            if(editor.ShowDialog(this)==DialogResult.OK)
            {
                if(SupportProductionPackageSettingsStore.TryLoad(projectRoot,out var confirmed,out string loadState))
                    SetStatus("Production settings verified: Package="+(confirmed.Enabled?"ON":"OFF")+
                        ", Native PDF="+(confirmed.PublishPdf?"ON":"OFF")+" ("+loadState+").");
                else SetStatus("Production settings could not be reloaded: "+loadState+".");
            }
        };
        AddRows(project,Caption("Drawing preferences for this project"),ActionRow(saveSettings,loadSettings,resetSettings),
            Caption("Publishing and drawing register"),ActionRow(productionSettings));

        var results=AddSettingsPage("results","Results");
        generatedHistory.Name="SPTC_DWG_HISTORY";
        generatedHistory.AccessibleName="Generated drawings";
        generatedHistory.Height=100;
        generatedHistory.HorizontalScrollbar=true;
        generatedHistory.SelectedIndexChanged+=(_,_)=>RefreshSelectedQa();
        generatedHistory.DoubleClick+=(_,_)=>OpenSelectedDwg();
        qaIndicator.Text="QA status: UNVERIFIED";
        lastQa.AccessibleName="Drawing QA summary";
        lastQa.Height=115;
        lastQa.Multiline=true;
        lastQa.ReadOnly=true;
        lastQa.ScrollBars=ScrollBars.Vertical;
        lastQa.Text="No completed Ortho drawing in this session.";
        openDwgFolder.Text="Open DWG folder";
        openDwgFolder.Click+=(_,_)=>OpenGeneratedOutput(false);
        openResult.Text="Open RESULT.txt";
        openResult.Click+=(_,_)=>OpenGeneratedOutput(true);
        copyQa.Text="Copy QA summary";
        copyQa.Click+=(_,_)=>CopySelectedQa();
        openDwg.Text="Open DWG";
        openDwg.Name="SPTC_OPEN_DWG_79";
        openDwg.Click+=(_,_)=>OpenSelectedDwg();
        openDwg.Enabled=openDwgFolder.Enabled=openResult.Enabled=copyQa.Enabled=false;
        AddRows(results,Caption("Generated drawings"),generatedHistory,qaIndicator,lastQa,
            ActionRow(openDwg,openDwgFolder,openResult,copyQa));

        outputMode.SelectionChanged+=(_,_)=>UpdateOutputModeUi();
        batch.CheckedChanged+=(_,_)=>UpdateOutputModeUi();
        customScale.ValueChanged+=(_,_)=>UpdateOutputModeUi();
        tabs.SelectedIndexChanged+=(_,_)=>UpdateOutputModeUi();
        seed.TextChanged+=(_,_)=>UpdateOutputModeUi();
        prefix.TextChanged+=(_,_)=>UpdateOutputModeUi();
        InstallUiGuidance();

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
    }

    private TableLayoutPanel AddSettingsPage(string key,string caption)
    {
        var orthoTab=new TabPage(caption){Name=key,AutoScroll=true,UseVisualStyleBackColor=true};
        var rows=new TableLayoutPanel {
            Dock=DockStyle.Top, AutoSize=true, AutoSizeMode=AutoSizeMode.GrowAndShrink,
            ColumnCount=1, RowCount=0, Padding=new Padding(12,8,12,8),
            BackColor=SystemColors.Window, GrowStyle=TableLayoutPanelGrowStyle.AddRows
        };
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        rows.SizeChanged+=(_,_)=>{
            var extent=new Size(0,rows.Height);
            if(orthoTab.AutoScrollMinSize!=extent)orthoTab.AutoScrollMinSize=extent;
        };
        orthoTab.Controls.Add(rows);
        settingsPages.TabPages.Add(orthoTab);
        return rows;
    }

    private static void AddRows(TableLayoutPanel table,params Control[] fields)
    {
        table.SuspendLayout();
        foreach(var field in fields)
        {
            int row=table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            field.Dock=DockStyle.Top;
            field.TabIndex=row;
            field.Margin=new Padding(0,3,0,6);
            if(field is Label label)label.AutoSize=true;
            if(field is CheckBox check)check.AutoSize=true;
            table.Controls.Add(field,0,row);
        }
        table.ResumeLayout(true);
    }

    private Label Caption(string text)=>new() {
        Text=text,AutoSize=true,Font=new Font(Font,FontStyle.Bold),ForeColor=SystemColors.ControlText
    };

    private static FlowLayoutPanel ActionRow(params Button[] buttons)
    {
        var row=new FlowLayoutPanel {
            AutoSize=true, AutoSizeMode=AutoSizeMode.GrowAndShrink,
            WrapContents=true, Margin=Padding.Empty, TabStop=false
        };
        foreach(var button in buttons)
        {
            button.AutoSize=true;
            button.AutoSizeMode=AutoSizeMode.GrowAndShrink;
            button.Padding=new Padding(6,2,6,2);
            button.Margin=new Padding(0,0,7,4);
            row.Controls.Add(button);
        }
        return row;
    }

    private void InstallUiGuidance()
    {
        void Tip(Control control,string description)
        {
            uiTips.SetToolTip(control,description);
            if(control is SupportDrawingChoiceGroup group)
                foreach(Control choice in group.Controls)uiTips.SetToolTip(choice,description);
        }
        uiTips.SetToolTip(sourceLabel,sourceLabel.Text);
        Tip(cubeMode,
            "SupportFocus limits the native OrthoCube around the selected support.");
        Tip(scale,
            "Automatic allows standard output; Draft and Multiple Sheets require an explicit 1:N.");
        Tip(customScale,
            "Enter the exact 1:N denominator. It is not a paper dimension.");
        Tip(autoSeed,
            "Use an empty Ortho seed already registered in this Plant 3D project.");
        Tip(prefix,
            "Optional filename prefix. Leave blank to use native automatic naming.");
        Tip(outputMode,
            "Printable sheet, single-DWG Draft Overflow, or split sheets without changing scale.");
        Tip(hiddenLineMode,
            "Sets the hidden-line mode as Plant generates each selected view. Edit View reads the mode saved with that view.");
        Tip(insulationMode,
            "Sets the native Ortho insulation mode for generation. Plant shares this setting across views. Visible insulation requires insulated model parts.");
        Tip(quickDraft,
            "Select all ten views and Draft Overflow; keep your selected exact drawing scale.");
        Tip(quickPhysical,
            "Return to normal printable-sheet constraints without changing your view choices.");
        Tip(batch,
            "Run sequentially for support BLOCK references. Incompatible with Draft and Split.");
        Tip(xrefNames,
            "Annotate nearby verified XREF structural/support members with source drawing names.");
        Tip(generatedHistory,
            "Select any DWG from this project's current session to inspect its RESULT.txt.");
        Tip(copyQa,
            "Copy only the chosen DWG's measured QA summary to the clipboard.");
        Tip(preflightButton,
            "Review current selections. Physical page fit is verified during generation.");
        Tip(runButton,
            "Start the existing AutoCAD command using exactly the selected options.");
    }

    private void PrepareDraftViews()
    {
        if(IsRunning)return;
        SetViewPreset("All");
        batch.Checked=false;
        outputMode.SelectedIndex=(int)SupportDrawingOutputMode76.Mode.Draft;
        UpdateOutputModeUi();
        // An explicit scale is a user decision, never silently changed.
        int denominator=SupportDrawingPaperScale39.Resolve(
            scale.SelectedValue,(int)customScale.Value);
        SetStatus(denominator>0?
            "Draft configured: All 10 / 1:"+denominator+
            ". PDF remains disabled for off-sheet drawings. Click Preflight to review.":
            "All 10 Draft selected. Choose an exact 1:N scale "+
            "before Run (Automatic is not valid for Draft Overflow).");
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
            // The fixed footer always reserves a separate right column
            // for actions, including at 125/150% DPI.
            int buttonWidth=Math.Max(fixSettings.Width,
                Math.Max(runButton.Width,preflightButton.Width));
            int right=Math.Max(125,w-buttonWidth-12);
            int gap=Math.Max(5,h/30);
            int total=runButton.Height+preflightButton.Height+gap;
            if(fixSettings.Visible)total+=fixSettings.Height+gap;
            int top=Math.Max(5,(h-total)/2);
            if(fixSettings.Visible)
            {
                fixSettings.Location=new Point(right,top);
                top+=fixSettings.Height+gap;
            }
            preflightButton.Location=new Point(right,top);
            runButton.Location=new Point(right,
                top+preflightButton.Height+gap);
            int textWidth=Math.Max(95,right-24);
            stickySummary.SetBounds(12,4,textWidth,
                Math.Max(20,stickySummary.Height));
            int textTop=Math.Max(29,stickySummary.Bottom+4);
            preview.SetBounds(12,textTop,textWidth,
                Math.Max(35,h-textTop-8));
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
        uiTips.SetToolTip(sourceLabel,sourceLabel.Text);
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

    private static TabPage MakeInfoTab(string key,string name,string text)
    {
        TabPage tab = new(name) {Name = key, UseVisualStyleBackColor = true};
        tab.Controls.Add(new Label
        {
            Text = text,
            Dock = DockStyle.Fill, Padding = new Padding(12),
            Font = new Font("Segoe UI",10f)
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
        scale.SelectedValue=v.ScaleSelection;
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
        hiddenLineMode.SelectedValue=v.HiddenLineMode;
        insulationMode.SelectedValue=v.InsulationMode;
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
            scale.SelectedValue,(int)customScale.Value,prefix.Text.Trim(),
            cubeMode.SelectedIndex==0,chosen,line.Checked,bop.Checked,
            tos.Checked,description.Checked,xrefNames.Checked,
            autoDimLinear.Checked,referenceDimensions.Checked,
            batch.Checked,hiddenLineMode.SelectedValue,insulationMode.SelectedValue));
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
                    scale.SelectedValue,(int)customScale.Value);
        }
        catch(ArgumentException){denominator=0;}
        bool isOrtho=tabs.SelectedTab?.Name=="ortho";
        Control? issue=RequiredSetting(count,denominator,mode,isOrtho);
        bool eligible=SupportDrawingOutputMode76.Eligible(
            mode,count,denominator,batch.Checked);
        bool valid=eligible&&issue==null;
        string modeInfo=SupportDrawingOutputMode76.Explanation(mode);
        outputHint.Text=modeInfo+
            (!valid?"\r\nTo continue: "+RequiredAction(issue):"");
        outputHint.ForeColor=!valid?Color.Firebrick:
            mode==SupportDrawingOutputMode76.Mode.Draft?
                Color.DarkGoldenrod:Color.DimGray;
        string scaleText=denominator>0?"1:"+denominator:"Automatic";
        string modeName=mode switch
        {
            SupportDrawingOutputMode76.Mode.Draft=>"Draft DWG · PDF BLOCKED",
            SupportDrawingOutputMode76.Mode.Split=>"Multiple sheets · Printable",
            _=>"Single sheet · Paper fit"
        };
        stickySummary.Text=isOrtho
            ? count+"/10 views  ·  "+scaleText+"  ·  "+modeName
            : tabs.SelectedTab?.Text;
        stickySummary.ForeColor=issue!=null?Color.Firebrick:
            isOrtho&&mode==SupportDrawingOutputMode76.Mode.Draft?
                Color.DarkGoldenrod:Color.FromArgb(31,65,103);
        uiTips.SetToolTip(stickySummary,stickySummary.Text+
            (issue!=null?"  — "+RequiredAction(issue):""));
        string readiness;
        if(IsRunning)
            readiness="Generating... CAD source selection and progress remain in AutoCAD.";
        else if(!isOrtho)
            readiness="Ready: use the original "+tabs.SelectedTab?.Text+" workflow.";
        else if(issue!=null)
            readiness="Action required: "+RequiredAction(issue);
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
        readinessHint.ForeColor=issue!=null&&isOrtho?
            Color.Firebrick:mode==SupportDrawingOutputMode76.Mode.Draft?
                Color.DarkGoldenrod:Color.DarkGreen;
        fixSettings.Visible=!IsRunning&&isOrtho&&issue!=null;
        fixSettings.Enabled=fixSettings.Visible;
        runButton.Enabled=!IsRunning&&(!isOrtho||valid);
        ArrangeStickyFooter();
    }

    // REV79 explicit validation navigation, based solely on selected
    // WinForms options, never native model/Ortho checks.
    private Control? RequiredSetting(int count,int denominator,
        SupportDrawingOutputMode76.Mode mode,bool isOrtho)
    {
        if(!isOrtho)return null;
        if(count==0||mode!=SupportDrawingOutputMode76.Mode.Physical&&
            count!=10)return views;
        if(mode!=SupportDrawingOutputMode76.Mode.Physical&&
           denominator<=0)return scale;
        if(mode!=SupportDrawingOutputMode76.Mode.Physical&&
           batch.Checked)return batch;
        if(!autoSeed.Checked&&!File.Exists(seed.Text.Trim()))return seed;
        if(!ValidDrawingPrefix())return prefix;
        return null;
    }

    private bool ValidDrawingPrefix() =>
        prefix.Text.Trim().Length<=48&&
        prefix.Text.IndexOfAny(Path.GetInvalidFileNameChars())<0&&
        !prefix.Text.Contains("..",StringComparison.Ordinal)&&
        !prefix.Text.EndsWith(".",StringComparison.Ordinal);

    private string RequiredAction(Control? field)
    {
        if(field==views)return "Select the required native views.";
        if(field==scale)return "Choose an exact 1:N scale (Automatic cannot be used).";
        if(field==batch)return "Turn Batch Ortho OFF for Draft or Split.";
        if(field==seed)return "Select an existing registered EMPTY native Ortho seed.";
        if(field==prefix)return "Correct the drawing prefix (48 characters max).";
        return "Ready for native validation.";
    }

    private void FocusRequiredSetting()
    {
        if(IsRunning||tabs.SelectedTab?.Name!="ortho")return;
        int denominator=0;
        try{denominator=SupportDrawingPaperScale39.Resolve(
            scale.SelectedValue,(int)customScale.Value);}
        catch(ArgumentException){/* exact scale validated before Run */}
        Control? target=RequiredSetting(views.CheckedItems.Count,
            denominator,SelectedOutputMode(),true);
        if(target==null)return;
        for(Control? parent=target.Parent;parent!=null;parent=parent.Parent)
            if(parent is TabPage page)
            {
                settingsPages.SelectedTab=page;
                page.ScrollControlIntoView(target);
                break;
            }
        if(target is SupportDrawingChoiceGroup choices)choices.FocusSelection();
        else target.Focus();
        SetStatus("Fix setting: "+RequiredAction(target));
    }

    private void ClearGeneratedOutput()
    {
        outputHistory.Clear();
        settingsPages.TabPages["results"]!.Text="Results";
        generatedHistory.Items.Clear();
        lastOutputDwg=null;
        lastResultTxt=null;
        lastCopiedSummary="";
        openDwgFolder.Enabled=false;
        openDwg.Enabled=false;
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
        string kind="REPORT PENDING";
        try
        {
            if(File.Exists(report))
            {
                var measured=SupportDrawingQaSummary76.Read(
                    File.ReadAllText(report));
                kind=measured.DraftOnly?"DRAFT · NO PDF":"NATIVE SHEET";
            }
        }
        catch(System.Exception ex) when(ex is IOException or
            UnauthorizedAccessException)
        {
            kind="QA UNAVAILABLE";
        }
        string savedAt=File.GetLastWriteTime(full).ToString(
            "MM-dd HH:mm",System.Globalization.CultureInfo.InvariantCulture);
        generatedHistory.Items.Add(savedAt+"  |  "+
            Path.GetFileName(full)+"  |  "+kind);
        generatedHistory.SelectedIndex=outputHistory.Count-1;
        settingsPages.TabPages["results"]!.Text="Results ("+outputHistory.Count+")";
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
        openDwg.Enabled=has&&File.Exists(lastOutputDwg);
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

    // This is explicitly Windows Shell file association; it does not
    // inject AutoCAD commands or mutate any DWG/project from the palette.
    private void OpenSelectedDwg()
    {
        string? selected=lastOutputDwg;
        if(string.IsNullOrWhiteSpace(selected)||!File.Exists(selected))
        {
            SetStatus("Selected DWG was moved or deleted. Choose another result.");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo {
                FileName=selected,UseShellExecute=true
            });
        }
        catch(System.Exception ex) when(ex is System.ComponentModel.Win32Exception or
            IOException or UnauthorizedAccessException)
        {
            SetStatus("Windows could not open the selected DWG: "+ex.Message);
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
        if(tabs.SelectedTab?.Name=="batch")
        {
            preview.Text="Support Batch\r\nRun selected starts SPTC_SUPPORTBATCH. "+
                "Select support blocks, then choose which drawings to export and where to save them.";
            return;
        }
        string[] chosen=views.CheckedItems.Cast<object>()
            .Select(v=>v.ToString() ?? "").Where(v=>v.Length>0).ToArray();
        var selectedMode=SelectedOutputMode();
        int d=SupportDrawingPaperScale39.Resolve(
            scale.SelectedValue,(int)customScale.Value);
        bool eligible=SupportDrawingOutputMode76.Eligible(
            selectedMode,chosen.Length,d,batch.Checked);
        preview.Text =
            "READ-ONLY CONFIGURATION PREFLIGHT\r\n"+
            "Source: "+Path.GetFileName(source)+"\r\n"+
            "1. VIEWS: "+chosen.Length+" / 10 | "+
                string.Join(", ",chosen)+"\r\n"+
            "2. SCALE: "+SupportDrawingPaperScale39.Display(d)+"\r\n"+
            "3. OUTPUT: "+outputMode.SelectedValue+"\r\n"+
            "4. SEED: "+(autoSeed.Checked?
                "Automatic registered empty Ortho seed":seed.Text)+"\r\n"+
            "5. ANNOTATIONS: Line="+line.Checked+
                " | BOP="+bop.Checked+" | TOS="+tos.Checked+
                " | Description="+description.Checked+
                " | XREF Names="+xrefNames.Checked+"\r\n"+
            "6. HIDDEN LINES: "+hiddenLineMode.SelectedValue+"\r\n"+
            "7. INSULATION: "+insulationMode.SelectedValue+"\r\n"+
            "   Each view uses the selected appearance during generation. " +
            "Plant shares the Ortho insulation setting across views.\r\n" +
            "8. DIMENSIONS: "+(autoDimLinear.Checked?
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
                    scale.SelectedValue,(int)customScale.Value);
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
            SupportDrawingOutputMode76.AllowDraftOverflow(chosenOutput),
            NativeOrthoAppearance83.ValidateHidden(hiddenLineMode.SelectedValue),
            NativeOrthoAppearance83.ValidateInsulation(insulationMode.SelectedValue));
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
