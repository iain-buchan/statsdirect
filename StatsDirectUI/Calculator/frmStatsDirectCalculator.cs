using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using StatsDirect.UI;
using StatsDirect.UI.Properties;

namespace StatsDirect.Calculator;

/// <summary>Compatibility host for the standalone -calculator command.</summary>
public sealed class frmStatsDirectCalculator : Form
{
    private readonly CalculatorView view;
    public frmStatsDirectCalculator()
    {
        Text = "StatsDirect Calculator";
        Name = "frmStatsDirectCalculator";
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Size = new Size(720, 580);
        MinimumSize = new Size(400, 400);
        Icon = (Icon)new ComponentResourceManager(typeof(frmStatsDirectCalculator)).GetObject("$this.Icon");
        view = new CalculatorView(() => SdApplication.SoleInstance.ShowHelp(this, "1020"));
        view.SetStandalone();
        view.CloseRequested += Close;
        Controls.Add(view);
        Load += (_, _) =>
        {
            var settings = Settings.Default;
            if (!settings.WasLoaded || settings.CalculatorWidth <= 0 || settings.CalculatorHeight <= 0) return;
            var bounds = new Rectangle(settings.CalculatorLeft, settings.CalculatorTop, settings.CalculatorWidth, settings.CalculatorHeight);
            var screen = Screen.FromRectangle(bounds).WorkingArea;
            bounds.Width = Math.Min(Math.Max(MinimumSize.Width, bounds.Width), screen.Width);
            bounds.Height = Math.Min(Math.Max(MinimumSize.Height, bounds.Height), screen.Height);
            bounds.X = Math.Clamp(bounds.X, screen.Left, screen.Right - bounds.Width);
            bounds.Y = Math.Clamp(bounds.Y, screen.Top, screen.Bottom - bounds.Height);
            Bounds = bounds;
            if (settings.CalculatorMaximized) WindowState = FormWindowState.Maximized;
        };
        Shown += (_, _) => view.FocusExpression();
        FormClosing += (_, _) =>
        {
            var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            var settings = Settings.Default;
            settings.CalculatorTop = bounds.Top;
            settings.CalculatorLeft = bounds.Left;
            settings.CalculatorWidth = bounds.Width;
            settings.CalculatorHeight = bounds.Height;
            settings.CalculatorMaximized = WindowState == FormWindowState.Maximized;
            settings.Save();
            if (view.Saved.Items.Count > 0 && MessageBox.Show(this, "Copy saved results to clipboard?", Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) view.CopySaved();
        };
    }
}
