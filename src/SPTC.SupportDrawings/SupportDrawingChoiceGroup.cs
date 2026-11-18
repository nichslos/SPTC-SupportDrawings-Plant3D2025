using System;
using System.Linq;
using System.Windows.Forms;

namespace SPTC.SupportDrawings;

/// <summary>One keyboard-accessible choice per group. Stored values are independent of the displayed captions.</summary>
internal sealed class SupportDrawingChoiceGroup : FlowLayoutPanel
{
    private readonly RadioButton[] choices;
    internal event EventHandler? SelectionChanged;

    internal SupportDrawingChoiceGroup(string caption,params string[] values)
    {
        AccessibleName=caption;
        AccessibleRole=AccessibleRole.Grouping;
        AutoSize=true;
        AutoSizeMode=AutoSizeMode.GrowAndShrink;
        WrapContents=true;
        TabStop=false;
        Margin=Padding.Empty;
        choices=values.Select((value,index)=>
        {
            var choice=new RadioButton {
                Text=value.TrimEnd('.'), Tag=value, AutoSize=true,
                TabIndex=index, Margin=new Padding(0,2,16,6),
                UseVisualStyleBackColor=true
            };
            choice.CheckedChanged+=(_,_)=>{
                if(choice.Checked)SelectionChanged?.Invoke(this,EventArgs.Empty);
            };
            Controls.Add(choice);
            return choice;
        }).ToArray();
        choices[0].Checked=true;
    }

    internal int SelectedIndex
    {
        get=>Array.FindIndex(choices,c=>c.Checked);
        set=>choices[value].Checked=true;
    }

    internal string SelectedValue
    {
        get=>(string)choices[SelectedIndex].Tag!;
        set
        {
            var choice=Array.Find(choices,c=>string.Equals((string?)c.Tag,value,
                StringComparison.OrdinalIgnoreCase));
            if(choice!=null)choice.Checked=true;
        }
    }

    internal void FocusSelection()=>choices[SelectedIndex].Focus();
}
