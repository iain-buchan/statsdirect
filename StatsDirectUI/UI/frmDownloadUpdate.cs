using System;
using System.Drawing;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    internal sealed class frmDownloadUpdate : Form
    {
        private readonly Label status;
        private readonly ProgressBar progress;
        private readonly Button cancel;
        private readonly CancellationTokenSource cancellation = new();
        private readonly Func<IProgress<UpdateDownloadProgress>, CancellationToken, Task<UpdateInstaller>> download;
        private bool downloading;

        internal frmDownloadUpdate() : this(DownloadAsync) { }

        internal frmDownloadUpdate(Func<IProgress<UpdateDownloadProgress>, CancellationToken, Task<UpdateInstaller>> download)
        {
            SuspendLayout();
            this.download = download;
            Text = "Update StatsDirect";
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(480, 180);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 4
            };
            layout.SuspendLayout();
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.Controls.Add(new Label
            {
                Text = "Downloading the StatsDirect installer. You will be asked to save any unsaved work before StatsDirect closes.",
                Dock = DockStyle.Fill, AutoSize = true
            });
            status = new Label { Text = "Connecting to statsdirect.com…", Dock = DockStyle.Fill, AutoSize = true };
            progress = new ProgressBar { Dock = DockStyle.Top, Style = ProgressBarStyle.Marquee, Height = 22 };
            cancel = new Button { Text = "Cancel", Anchor = AnchorStyles.Right, AutoSize = true };
            cancel.Click += (_, _) => CancelDownload();
            CancelButton = cancel;
            layout.Controls.Add(status);
            layout.Controls.Add(progress);
            layout.Controls.Add(cancel);
            Controls.Add(layout);
            layout.ResumeLayout(false);
            layout.PerformLayout();
            ResumeLayout(false);
        }

        internal UpdateInstaller Installer { get; private set; }

        private static async Task<UpdateInstaller> DownloadAsync(IProgress<UpdateDownloadProgress> progress, CancellationToken token)
        {
            using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            // ResponseHeadersRead does not apply HttpClient.Timeout to the body; cover the whole transfer.
            timeout.CancelAfter(TimeSpan.FromMinutes(30));
            return await UpdateInstaller.DownloadAsync(client, progress, timeout.Token);
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            downloading = true;
            try
            {
                Installer = await download(new Progress<UpdateDownloadProgress>(ShowProgress), cancellation.Token);
                // A cancel click can be queued just as the final download continuation arrives.
                if (cancellation.IsCancellationRequested)
                {
                    Installer.Dispose();
                    Installer = null;
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception ex)
            {
                Utilities.DiagnosticLog.Write("Installer download failed: " + ex);
                string reason = ex is OperationCanceledException ? "The download timed out." : ex.Message;
                MessageBox.Show(this, "The update could not be downloaded. StatsDirect will stay open.\r\n\r\n" +
                    reason + "\r\n\r\nPlease try again, or download the installer from https://www.statsdirect.com.",
                    "StatsDirect update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                downloading = false;
            }
            DialogResult = Installer == null ? DialogResult.Cancel : DialogResult.OK;
        }

        private void ShowProgress(UpdateDownloadProgress value)
        {
            if (IsDisposed || cancellation.IsCancellationRequested)
                return;
            double megabytes = value.BytesReceived / 1048576.0;
            if (value.TotalBytes is > 0)
            {
                progress.Style = ProgressBarStyle.Continuous;
                progress.Value = (int)Math.Clamp(100.0 * value.BytesReceived / value.TotalBytes.Value, 0, 100);
                status.Text = $"Downloaded {megabytes:F1} MB of {value.TotalBytes.Value / 1048576.0:F1} MB";
            }
            else
                status.Text = $"Downloaded {megabytes:F1} MB";
        }

        private void CancelDownload()
        {
            cancel.Enabled = false;
            status.Text = "Cancelling download…";
            cancellation.Cancel();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (downloading)
            {
                e.Cancel = true;
                CancelDownload();
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                cancellation.Dispose();
            base.Dispose(disposing);
        }
    }
}
