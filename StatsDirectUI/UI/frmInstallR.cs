using StatsDirect.R;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatsDirect.UI;

public partial class frmInstallR : Form
{
    private readonly Label status = new() { AutoSize = true, MaximumSize = new Size(520, 0), Margin = new Padding(0, 12, 0, 10) };
    private readonly ProgressBar progress = new() { Width = 510, Height = 18, Visible = false };
    private readonly Button download = new() { AutoSize = true, Text = "Download and Install R" };
    private readonly Button check = new() { AutoSize = true, Text = "Check installation" };
    private readonly Button cancel = new() { AutoSize = true, Text = "Not now" };
    private CancellationTokenSource operation;
    private bool installing, checking, closing;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Func<CancellationToken, Task<bool>> ready;
    private readonly Func<IProgress<RDownloadProgress>, CancellationToken, Task> prepareTest;
    private readonly Func<CancellationToken, Task> launchTest;
    public bool UserThinksRIsInstalled { get; private set; }

    public frmInstallR() : this(RInstallation.ReadyAsync) { }
    internal frmInstallR(Func<CancellationToken, Task<bool>> ready,
        Func<IProgress<RDownloadProgress>, CancellationToken, Task> prepareTest = null,
        Func<CancellationToken, Task> launchTest = null)
    {
        if ((prepareTest == null) != (launchTest == null)) throw new ArgumentException("Both setup test actions must be supplied together.");
        this.ready = ready; this.prepareTest = prepareTest; this.launchTest = launchTest;
        Text = "Set up R"; StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Arial", 10); AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(558, 270);
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        layout.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(520, 0), Text = "R is needed for this action. Download the official Windows installer from CRAN, then complete its setup. Your worksheet and script will stay open; StatsDirect will resume this action when R is ready." });
        status.Text = "R " + RInstallation.Version + " · official CRAN download · verified before opening";
        layout.Controls.Add(status); layout.Controls.Add(progress);
        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 12, 0, 0) };
        buttons.Controls.AddRange([download, check, cancel]); layout.Controls.Add(buttons);
        var website = new LinkLabel { AutoSize = true, Text = "Official R website / other installation options", Margin = new Padding(0, 8, 0, 0) };
        website.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo("https://cran.r-project.org/bin/windows/base/") { UseShellExecute = true });
        layout.Controls.Add(website); Controls.Add(layout);
        download.Click += async (_, _) => await DownloadAsync();
        check.Click += async (_, _) => await CheckAsync();
        cancel.Click += (_, _) => { if (operation != null) operation.Cancel(); else Close(); };
        Activated += async (_, _) => { if (installing && !checking) await CheckAsync(); };
        FormClosing += (_, _) => { closing = true; lifetime.Cancel(); operation?.Cancel(); };
        FormClosed += (_, _) => lifetime.Dispose();
        AcceptButton = download; CancelButton = cancel;
    }
    private async Task CheckAsync()
    {
        if (checking || IsDisposed || closing) return;
        checking = true; check.Enabled = false;
        try
        {
            bool installed = await ready(lifetime.Token);
            if (closing || IsDisposed) return;
            if (installed)
            { UserThinksRIsInstalled = true; DialogResult = DialogResult.OK; Close(); }
            else if (!IsDisposed) status.Text = "R is not ready yet. Finish its installer, then choose Check installation. Your work is kept open.";
        }
        catch (Exception ex) { if (!IsDisposed && !closing) status.Text = "Could not check R: " + ex.Message; }
        finally { checking = false; if (!IsDisposed && !closing) check.Enabled = true; }
    }
    private async Task DownloadAsync()
    {
        if (operation != null || closing) return;
        using var tokenSource = new CancellationTokenSource(); operation = tokenSource;
        download.Enabled = check.Enabled = false; cancel.Text = "Cancel download"; progress.Visible = true;
        progress.Style = ProgressBarStyle.Marquee; status.Text = "Downloading R from CRAN…";
        try
        {
            using var package = new RInstallation();
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(15) };
            var updates = new Progress<RDownloadProgress>(p =>
            {
                if (IsDisposed || closing || tokenSource.IsCancellationRequested) return;
                if (p.Total is > 0) { progress.Style = ProgressBarStyle.Continuous; progress.Value = (int)Math.Min(100, p.Received * 100 / p.Total.Value); }
                status.Text = $"Downloading R: {p.Received / 1048576d:0.0} MB";
            });
            if (prepareTest != null) await prepareTest(updates, tokenSource.Token);
            else await package.DownloadAsync(client, updates, tokenSource.Token);
            tokenSource.Token.ThrowIfCancellationRequested();
            installing = true;
            // Hiding a modal WinForms dialog ends ShowDialog and loses the pending
            // action. Minimise it while the native installer owns the foreground.
            WindowState = FormWindowState.Minimized;
            if (launchTest != null) await launchTest(tokenSource.Token);
            else { using var process = package.Start(); if (process != null) await process.WaitForExitAsync(tokenSource.Token); }
            if (!IsDisposed && !closing) { WindowState = FormWindowState.Normal; await CheckAsync(); }
        }
        catch (OperationCanceledException) { if (!IsDisposed) status.Text = "Download cancelled. Your work is kept open; choose Retry when ready."; }
        catch (Exception ex) { if (!IsDisposed) status.Text = "R setup could not finish: " + ex.Message; }
        finally
        {
            operation = null;
            if (!IsDisposed && !closing && DialogResult != DialogResult.OK)
            {
                WindowState = FormWindowState.Normal;
                download.Text = "Retry download"; download.Enabled = check.Enabled = true;
                cancel.Text = "Not now"; progress.Visible = false;
            }
        }
    }
    // Retired designer event bindings; the live form is constructed above.
    private void lnkInstallR_LinkClicked(object s, LinkLabelLinkClickedEventArgs e) => download.PerformClick();
    private void lnkMoreInformation_LinkClicked(object s, LinkLabelLinkClickedEventArgs e) => Process.Start(new ProcessStartInfo("https://www.r-project.org/") { UseShellExecute = true });
    private void cmdDoNotInstall_Click(object s, EventArgs e) => Close();
    private async void cmdInstalled_Click(object s, EventArgs e) => await CheckAsync();
}
