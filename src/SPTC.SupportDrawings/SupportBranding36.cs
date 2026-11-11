using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using WinForms = System.Windows.Forms;

namespace SPTC.SupportDrawings;

/// <summary>
/// SPTC Engineering branding sourced from the user's supplied transparent logo.
/// Embedded as compact PNG data so NETLOAD works without external image files.
/// Form instances own each decoded bitmap and icon.
/// </summary>
internal static class SupportBranding36
{
    private const string LogoPng = "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";
    private const string MarkPng = "iVBORw0KGgoAAAANSUhEUgAAAIAAAACACAMAAAD04JH5AAAAYFBMVEX0p6eaz+3iWlplZWSUkpFmt+NcKin13uBAQD+WODicU1N7wuklHBv+/v4ZGRjpTExQrN06Ojn1UlLFxcVGRkXa2tlVtemysrLv5eWhoqIrKCfxeHj4x8fO6PX1paXwhoboiLSSAAAAIHRSTlP//////////////////////////////////////////3Fe9GAAAAL6SURBVHja7drdmrIgEABgfnRd289K/AkN8/7v8tNkDRTTXIGTmaPNrXidhgF6QonnQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB+huVCCO4PIO6sj1Z4ArTs8gx2QV4A3e1fZDDkAYDG4TsAQ84BXBl/yIHjeOiAC3JfAc4BEbqT+4PLR+TiGtD2Rd/N+sfwkLkGjCmX5eY6A69Jx5hQu5AjAFdnHJm2AQcAfdJF8yIgyHYFzpqO0C5FyOGslxNByUpXFu4z0JcBG6qSCCurYSSidzXQl+ajWxHJHVnZkDyIWlpcKzgXWzKZ83GRUz4D88K3AChonu6J09j3TvIKGa/Ek+d+P8MMCM77olbyLS/9i+WF2PwSdOT45y+l5r7GizEhJK7P2wG3veOfYxPgfZgAqWdAtnt880fwMYDuBxiKcAcgPx+Rgi0JqPM8NwCm9ZrSoAqqxej+FZxOw1OCoJWnjtf4VaaFOsNSYyPik/Hphu7L2J2r576Tcv/F8hQzA4pJmrbtuhjj4x1w7S0y/dnVp4B8FcDUxX6exOyPGVgFRLLVtwtvYR0gD3v+AMNxW266vQB4t+IybbF3DOh3WK1YnMkOAHNR7RlQ+M5A4AAQoVZ9KVf/1jdUdgCcyRN/EdC0/o1hszlZzOwAnoebrv3f6tU1t7ACGM58PFkfv06sAJ5HLqYtbQtB7QD6/ts13w27t5slQCIQ37J9pElisw/Qj8f/CwDjMAxx88H+tQ6S4wC4lPHTLADoLZtEkhwHwGV4HaIsGyOAbmvTOwG4vI4RvgT5csM5FtAo43c5CE0AbhMQaoBriW0CbnOAngAlBVYAdA7AE8D1ahEw/XbECPgtwyMAhRbVrLdZB6w1U+8AagCER9bAGiCwPQvWAJntPrChBPoUhNY64bYtpVoFZRm5A1TJazUe778xNq0dgO81QKps6JpQ7gfwwoZkI2D2FU32JvT3bDD+wTjSvtNc2X2YjjST18CPWAAAAAAAAAAAAAAAAAAAAAAAAP8BWhpel+SokqIAAAAASUVORK5CYII=";

    internal static Bitmap CreateLogo()
    {
        byte[] bytes=Convert.FromBase64String(LogoPng);
        using var stream=new MemoryStream(bytes, false);
        using var image=Image.FromStream(stream, false, true);
        return new Bitmap(image);
    }

    internal static Icon CreateIcon()
    {
        byte[] bytes=Convert.FromBase64String(MarkPng);
        using var stream=new MemoryStream(bytes,false);
        using var mark=Image.FromStream(stream,false,true);
        using var square=new Bitmap(32,32);
        using(var graphics=Graphics.FromImage(square))
        {
            graphics.Clear(Color.White);
            graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(mark,new Rectangle(0,0,32,32));
        }
        IntPtr handle=square.GetHicon();
        try
        {
            using Icon temporary=Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally { DestroyIcon(handle); }
    }

    internal static WinForms.PictureBox MakeLogo(int left,int top,int width,int height)
    {
        return new WinForms.PictureBox
        {
            Location=new Point(left,top),
            Size=new Size(width,height),
            BackColor=Color.White,
            SizeMode=WinForms.PictureBoxSizeMode.Zoom,
            Image=CreateLogo(),
            TabStop=false,
            AccessibleName="SPTC Engineering logo"
        };
    }

    [DllImport("user32.dll",SetLastError=true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
