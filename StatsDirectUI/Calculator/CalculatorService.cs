using System;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using StatsDirect.UI.ToolWindows;

namespace StatsDirect.Calculator;

internal sealed class CalculatorService : IDisposable
{
    internal CalculatorView View { get; private set; }
    internal ToolPaneSession Session { get; private set; }
    private bool disposed;

    internal void Show(Form host, Action help)
    {
        if (disposed) return;
        if (Session == null)
        {
            View = new CalculatorView(help);
            float scale = host.DeviceDpi / 96f;
            int dockHeight = (int)Math.Ceiling(View.MeasureCompactHeight(host.ClientSize.Width) / scale);
            int floatingHeight = (int)Math.Ceiling(View.MeasureCompactHeight((int)(720 * scale)) / scale) + 40;
            // About three input lines at the default Arial size. Users can
            // enlarge either presentation without changing the expression.
            Session = new ToolPaneSession(host, View, "Calculator", DockStyle.Bottom, dockHeight, new Size(720, floatingHeight),
                View.SetPresentation, View.FocusExpression, View.ShowNotice,
                minimumDockLength: 190, minimumWindowSize: new Size(360, 220));
            View.ModeRequested += async () => await Session.ToggleModeAsync();
            View.CloseRequested += Session.Hide;
        }
        Session.Show(true);
    }

    // Recognize saved Tools configurations, including paths with spaces, while
    // leaving third-party calculators and launches of other installations alone.
    internal static bool IsBuiltInCommand(string command, string applicationPath)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;
        var match = Regex.Match(command, "^\\s*(?:\"(?<exe>[^\"]+)\"|(?<exe>.+?))\\s+-calculator\\s*$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;
        try
        {
            string exe = match.Groups["exe"].Value.Replace("%STATSDIRECT%", Path.GetDirectoryName(applicationPath), StringComparison.OrdinalIgnoreCase);
            if (string.Equals(exe, "StatsDirect.exe", StringComparison.OrdinalIgnoreCase)) return true;
            return string.Equals(Path.GetFullPath(exe), Path.GetFullPath(applicationPath), StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }
    }
    public void Dispose() { disposed = true; Session?.Dispose(); }
}
