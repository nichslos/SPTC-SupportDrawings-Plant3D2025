using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(HarveyTools.PreviewAddon.PreviewExtension))]
[assembly: ExtensionApplication(typeof(HarveyTools.PreviewAddon.PreviewExtension))]

namespace HarveyTools.PreviewAddon;

public sealed class PreviewExtension : IExtensionApplication
{
    private static PaletteSet? originalPalette;
    private static Form? previewWindow;
    private static GroupBox? patchedGroup;
    private static string? originalFolder;
    private static bool isScanning;

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr window, out Rect rectangle);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    public void Initialize()
    {
        // Does not replace HTTOOLS or any existing L/R/T/M/U command handler.
        CadApp.Idle += OnIdle;
    }

    public void Terminate()
    {
        CadApp.Idle -= OnIdle;
        if (previewWindow != null && !previewWindow.IsDisposed)
            previewWindow.Close();
    }

    [CommandMethod("HTPREVIEW", CommandFlags.Session)]
    public static void ShowPreviewCommand() => ShowPreview();

    [CommandMethod("HTPREVIEWSETUP", CommandFlags.Session)]
    public static void SetupCommand() => AttachPreviewButton();

    private static void OnIdle(object? sender, EventArgs args)
    {
        if (patchedGroup != null && !patchedGroup.IsDisposed)
            return;
        if (isScanning) return;
        isScanning = true;
        try { AttachPreviewButton(); }
        catch { /* Noninvasive: never interrupt normal AutoCAD / MY TOOL behavior. */ }
        finally { isScanning = false; }
    }

    private static void AttachPreviewButton()
    {
        // The supplied production DLL already contains all working support buttons.
        // Find its live palette without replacing, patching or re-compiling that DLL.
        PaletteSet? palette = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType("HarveyTools.ToolsPalette", false);
            if (type == null) continue;
            var field = type.GetField("palette", BindingFlags.Static | BindingFlags.NonPublic);
            if (field?.GetValue(null) is PaletteSet existing)
            {
                palette = existing;
                originalFolder = Path.GetDirectoryName(assembly.Location);
                break;
            }
        }
        if (palette == null || !palette.Visible || palette.Handle == IntPtr.Zero)
            return;
        originalPalette = palette;

        GroupBox? group = null;
        EnumChildWindows(palette.Handle, (handle, _) =>
        {
            if (Control.FromHandle(handle) is GroupBox candidate
                && string.Equals(candidate.Text, "Pipe Support Steel", StringComparison.OrdinalIgnoreCase))
            {
                group = candidate;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        if (group == null || group.IsDisposed) return;

        if (group.Controls.OfType<Button>().Any(button =>
            string.Equals(button.Text, "PREVIEW SUPPORT", StringComparison.OrdinalIgnoreCase)))
        {
            patchedGroup = group;
            return;
        }

        var uButton = group.Controls.OfType<Button>()
            .FirstOrDefault(button => button.Text.Contains("U-TYPE", StringComparison.OrdinalIgnoreCase));
        if (uButton == null) return;

        var previewButton = new Button
        {
            Name = "HT_PREVIEW_SUPPORT",
            Text = "PREVIEW SUPPORT",
            Location = new Point(uButton.Left, uButton.Bottom + 12),
            Size = new Size(Math.Max(140, Math.Min(230, uButton.Width / 2)), 35),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            TabStop = true,
            UseVisualStyleBackColor = false,
            BackColor = uButton.BackColor,
            ForeColor = uButton.ForeColor,
            Font = uButton.Font,
            FlatStyle = uButton.FlatStyle
        };
        previewButton.FlatAppearance.BorderSize = Math.Max(1, uButton.FlatAppearance.BorderSize);
        previewButton.FlatAppearance.BorderColor = uButton.FlatAppearance.BorderColor;
        previewButton.Click += (_, _) => ShowPreview();
        group.Controls.Add(previewButton);
        group.Height = Math.Max(group.Height, previewButton.Bottom + 15);

        // Ensure the new row is visible in the native palette.
        if (group.Parent is ScrollableControl parent)
            parent.AutoScroll = true;
        palette.MinimumSize = new Size(Math.Max(palette.MinimumSize.Width, 410),
                                       Math.Max(palette.MinimumSize.Height, 340));
        patchedGroup = group;
    }

    private static readonly (string File, string Title)[] Images =
    {
        ("LTYPE.png", "L-TYPE"), ("UTYPE.png", "U-TYPE"),
        ("RTYPE(A).png", "R-TYPE (A)"), ("RTYPE(B).png", "R-TYPE (B)"),
        ("TTYPE(A).png", "T-TYPE (A)"), ("TTYPE(B).png", "T-TYPE (B)"),
        ("MTYPE(A).png", "M-TYPE (A)"), ("MTYPE(B).png", "M-TYPE (B)")
    };

    private static void ShowPreview()
    {
        if (previewWindow != null && !previewWindow.IsDisposed)
        {
            if (previewWindow.WindowState == FormWindowState.Minimized)
                previewWindow.WindowState = FormWindowState.Normal;
            previewWindow.Show();
            previewWindow.BringToFront();
            previewWindow.Activate();
            return;
        }

        var folder = originalFolder ?? Path.GetDirectoryName(typeof(PreviewExtension).Assembly.Location) ?? "";
        var imageDirectory = Path.Combine(folder, "IMAGES");
        if (!Directory.Exists(imageDirectory))
            imageDirectory = Path.Combine(folder, "PreviewImages");

        var form = new Form
        {
            Text = "MY TOOL - PREVIEW SUPPORT",
            ShowInTaskbar = true,
            ShowIcon = false,
            StartPosition = FormStartPosition.Manual,
            Size = new Size(820, 805),
            MinimumSize = new Size(560, 450),
            BackColor = Color.FromArgb(31, 39, 48),
            ForeColor = Color.White,
            AutoScaleMode = AutoScaleMode.Dpi
        };

        var header = new Label
        {
            Dock = DockStyle.Top,
            Height = 42,
            Padding = new Padding(15, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "MY TOOL  /  PREVIEW SUPPORT",
            BackColor = Color.FromArgb(43, 50, 62),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11, FontStyle.Bold)
        };

        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(31, 39, 48)
        };
        var table = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 4,
            Size = new Size(780, 1320),
            Location = new Point(4, 4),
            BackColor = Color.FromArgb(31, 39, 48)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        for (var r = 0; r < 4; r++)
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 330f));

        var bitmaps = new System.Collections.Generic.List<Bitmap>();
        for (int i = 0; i < Images.Length; i++)
        {
            var item = Images[i];
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(5),
                Padding = new Padding(6),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(37, 47, 58)
            };
            var image = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(33, 40, 48),
                AccessibleName = item.Title
            };
            var path = Path.Combine(imageDirectory, item.File);
            if (File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                using var opened = Image.FromStream(stream);
                var bitmap = new Bitmap(opened);
                bitmaps.Add(bitmap);
                image.Image = bitmap; // Original drawing already has the title: no duplicate captions.
            }
            else
            {
                var missing = new Label
                {
                    Dock = DockStyle.Bottom,
                    Height = 24,
                    ForeColor = Color.White,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Text = "Missing " + item.File
                };
                card.Controls.Add(missing);
            }
            card.Controls.Add(image);
            table.Controls.Add(card, i % 2, i / 2);
        }

        scroll.Controls.Add(table);
        form.Controls.Add(scroll);
        form.Controls.Add(header);
        scroll.Resize += (_, _) =>
        {
            if (!table.IsDisposed)
                table.Width = Math.Max(520, scroll.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 12);
        };
        form.FormClosed += (_, _) =>
        {
            previewWindow = null;
            foreach (var bitmap in bitmaps) bitmap.Dispose();
        };
        previewWindow = form;
        PositionNextToPalette(form);
        form.Show();
        form.BringToFront();
    }

    private static void PositionNextToPalette(Form form)
    {
        var screen = Screen.PrimaryScreen;
        if (originalPalette == null || originalPalette.Handle == IntPtr.Zero ||
            !GetWindowRect(originalPalette.Handle, out var rect))
        {
            if (screen != null)
                form.Location = new Point(screen.WorkingArea.Left + 20, screen.WorkingArea.Top + 30);
            return;
        }
        var area = Screen.FromHandle(originalPalette.Handle).WorkingArea;
        int xRight = rect.Right + 12;
        int xLeft = rect.Left - form.Width - 12;
        int x = xRight + form.Width <= area.Right ? xRight :
                xLeft >= area.Left ? xLeft :
                Math.Max(area.Left, area.Right - form.Width);
        int y = Math.Max(area.Top, Math.Min(rect.Top, area.Bottom - form.Height));
        form.Location = new Point(x, y);
    }
}