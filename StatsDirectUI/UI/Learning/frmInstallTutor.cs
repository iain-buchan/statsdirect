using System;
using System.Drawing;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatsDirect.UI.Learning;

internal sealed class frmInstallTutor : Form
{
    private readonly Label status = new() { AutoSize = true, MaximumSize = new Size(490, 0), Margin = new Padding(0, 14, 0, 12) };
    private readonly ProgressBar progress = new() { Width = 490, Height = 20 };
    private readonly Button retry = new() { Text = "Retry", AutoSize = true, Visible = false };
    private readonly Button cancel = new() { Text = "Cancel", AutoSize = true };
    private readonly Func<IProgress<TutorInstallProgress>, CancellationToken, Task> install;
    private CancellationTokenSource operation;
    private bool closeRequested;

    internal frmInstallTutor(TutorRuntimeInstallation runtime) : this(async (updates, token) =>
    {
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(15) };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(20));
        try { await Task.Run(() => runtime.InstallAsync(client, updates, timeout.Token), timeout.Token); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException("The download took too long. Check your connection and retry."); }
    }) { }

    internal frmInstallTutor(Func<IProgress<TutorInstallProgress>, CancellationToken, Task> install)
    {
        SuspendLayout();
        this.install = install;
        Text = "Set up ChatGPT"; Font = new Font("Arial", 10);
        StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = MaximizeBox = false; ClientSize = new Size(530, 245);
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        layout.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(490, 0), Text = "Setting up ChatGPT for first use. This downloads about 123 MB from OpenAI's official GitHub release and installs it for your Windows account. Your work stays open. Sign-in follows setup." });
        layout.Controls.Add(status); layout.Controls.Add(progress);
        var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 14, 0, 0) };
        buttons.Controls.AddRange([retry, cancel]); layout.Controls.Add(buttons); Controls.Add(layout);
        Shown += async (_, _) => await InstallAsync();
        retry.Click += async (_, _) => await InstallAsync();
        cancel.Click += (_, _) => { if (operation != null) operation.Cancel(); else Close(); };
        FormClosing += (_, e) => { if (operation != null) { e.Cancel = true; closeRequested = true; operation.Cancel(); } };
        CancelButton = cancel; AcceptButton = retry;
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        ResumeLayout(true);
    }
    private async Task InstallAsync()
    {
        if (operation != null || IsDisposed) return;
        using var source = new CancellationTokenSource(); operation = source;
        retry.Visible = false; cancel.Text = "Cancel"; progress.Visible = true; progress.Style = ProgressBarStyle.Marquee;
        status.Text = "Preparing ChatGPT component…";
        bool succeeded = false;
        try
        {
            var updates = new Progress<TutorInstallProgress>(p =>
            {
                if (IsDisposed || operation != source || source.IsCancellationRequested) return;
                progress.Style = p.Total is > 0 ? ProgressBarStyle.Continuous : ProgressBarStyle.Marquee;
                if (p.Total is > 0) progress.Value = (int)Math.Clamp(p.Received * 100 / p.Total.Value, 0, 100);
                status.Text = p.Total is > 0 ? $"{p.Stage}: {p.Received / 1000000d:0.0} of {p.Total / 1000000d:0.0} MB" : p.Stage;
            });
            await install(updates, source.Token);
            source.Token.ThrowIfCancellationRequested(); succeeded = true;
        }
        catch (OperationCanceledException) { status.Text = "Setup cancelled. Your work is kept open. Choose Retry when ready."; }
        catch (Exception ex) { status.Text = "ChatGPT setup could not finish: " + ex.Message; }
        finally
        {
            operation = null;
            if (succeeded) { DialogResult = DialogResult.OK; Close(); }
            else if (closeRequested) { DialogResult = DialogResult.Cancel; Close(); }
            else { retry.Visible = true; cancel.Text = "Not now"; progress.Visible = false; }
        }
    }
}
