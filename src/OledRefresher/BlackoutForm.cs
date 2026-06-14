using System;
using System.Drawing;
using System.Windows.Forms;

namespace OledRefresher;

/// <summary>
/// A borderless, top-most, pure-black window sized to exactly cover one display.
/// On OLED, "all black" means the pixels are physically off, which is what rests the panel.
/// </summary>
internal sealed class BlackoutForm : Form
{
    public BlackoutForm(Rectangle bounds)
    {
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.Black;
        ShowInTaskbar = false;
        ControlBox = false;
        MinimizeBox = false;
        MaximizeBox = false;
        DoubleBuffered = true;
        KeyPreview = true;
        Text = "OLED Refresher";

        // Take the supplied pixel bounds literally; do not let per-monitor DPI rescale the window.
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        TopMost = true;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOOLWINDOW = 0x00000080; // keep the overlay out of Alt-Tab
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW;
            return cp;
        }
    }
}
