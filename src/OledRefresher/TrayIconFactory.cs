using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace OledRefresher;

/// <summary>
/// Builds the tray icon at runtime so the project ships no binary assets.
/// Draws a small dark "monitor with a refresh arc" glyph.
/// </summary>
internal static class TrayIconFactory
{
    public static Icon Create()
    {
        using var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var accent = Color.FromArgb(46, 196, 182);
            using var body = new SolidBrush(Color.FromArgb(18, 18, 20));
            using var pen = new Pen(accent, 2f);

            var screen = new Rectangle(3, 4, 26, 18);
            g.FillRectangle(body, screen);
            g.DrawRectangle(pen, screen);

            using var standBrush = new SolidBrush(accent);
            g.FillRectangle(standBrush, 14, 22, 4, 4);
            g.FillRectangle(standBrush, 9, 26, 14, 3);

            using var arcPen = new Pen(accent, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(arcPen, 10, 7, 12, 12, 40, 280);
        }

        // GetHicon allocates an unmanaged icon handle; clone into a managed Icon we own,
        // then destroy the original handle so nothing leaks.
        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }
}
