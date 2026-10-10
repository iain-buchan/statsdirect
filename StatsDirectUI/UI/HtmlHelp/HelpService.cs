using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatsDirect.UI.HtmlHelp;

/// <summary>Owns one help session and its presentation. Docking never creates an MDI document.</summary>
internal sealed class HelpService : IDisposable
{
    private HelpWindow window;
    private HelpView view;
    private HelpDockHost dock;
    private Form host, modalOwner;
    private Rectangle? floatingBounds;
    private bool visible, preferFloating, temporary, changing, disposed;
    internal HelpWindow Window => window;
    internal HelpView View => view;
    internal HelpDockHost DockHost => dock;
    internal bool IsDocked => visible && window == null;
    internal bool IsVisible => visible;
    internal bool IsTemporary => temporary;

    // Native message boxes disable their owner's HWND without necessarily
    // updating Control.Enabled. Check both when deciding whether help can dock.
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(IntPtr handle);
    private static bool IsInteractive(Form form) => form.Enabled && (!form.IsHandleCreated || IsWindowEnabled(form.Handle));

    internal static Form FindOwner(Form parent) => Application.OpenForms.Cast<Form>().LastOrDefault(f => f.Modal && f.Visible) ?? parent?.MdiParent ?? parent;

    internal void Show(Form parent, string topic, string directory)
    {
        if (disposed) return;
        if (host == null)
        {
            Form root = parent?.MdiParent ?? parent ?? throw new ArgumentNullException(nameof(parent));
            while (root.Owner != null) root = root.Owner;
            var catalog = new HelpCatalog(directory);
            host = root;
            view = new HelpView(catalog);
            dock = new HelpDockHost(host);
            view.ModeRequested += ToggleMode;
            view.CloseRequested += Hide;
            view.TitleChanged += UpdateTitle;
            host.FormClosed += HostClosed;
            Application.Idle += CheckModality;
        }
        Control focus = FocusedControl(host);
        visible = true;
        UpdatePresentation();
        view.ShowTopic(topic);
        // F1 must not interrupt range selection or move the active MDI tab.
        if (IsDocked && focus != null && focus.CanFocus) focus.Focus();
        else if (window != null)
        {
            if (window.WindowState == FormWindowState.Minimized) window.WindowState = FormWindowState.Normal;
            window.Activate();
        }
    }

    private async void ToggleMode() => await ToggleModeAsync();

    internal async Task ToggleModeAsync()
    {
        if (!visible || temporary || changing) return;
        changing = true;
        try
        {
            await view.PreservePositionAsync(() =>
            {
                if (disposed || !visible) return;
                preferFloating = window == null;
                changing = false;
                UpdatePresentation();
                changing = true;
                if (window != null) window.Activate();
            });
        }
        catch (Exception ex) { if (!disposed) view.ShowNotice("Could not move help: " + ex.Message); }
        finally { changing = false; }
    }

    internal void Hide()
    {
        if (view == null || disposed) return;
        visible = false;
        temporary = false;
        UnhookModal();
        ParkView();
        CloseFrame();
        dock.SetVisible(false);
        if (host.Enabled) host.ActiveMdiChild?.Activate();
    }

    private void CheckModality(object sender, EventArgs e)
    {
        if (visible && !changing && !disposed) UpdatePresentation();
    }

    private void UpdatePresentation()
    {
        if (changing || !visible || host.IsDisposed) return;
        changing = true;
        try
        {
            Form owner = FindOwner(host);
            bool modal = owner != host || !IsInteractive(host);
            temporary = modal;
            if (modal || preferFloating)
            {
                if (window == null || window.IsDisposed || window.Owner != owner || !IsInteractive(window))
                    Float(owner, modal);
                view.SetPresentation(true, !modal);
            }
            else
            {
                if (window != null || view.Parent != dock.Panel || !dock.Panel.Visible)
                {
                    UnhookModal();
                    ParkView();
                    CloseFrame();
                    dock.SetVisible(true);
                    view.SetPresentation(false);
                }
            }
        }
        finally { changing = false; }
    }

    private void Float(Form owner, bool modal)
    {
        UnhookModal();
        ParkView();
        CloseFrame();
        dock.SetVisible(false);
        window = new HelpWindow();
        window.Controls.Add(view);
        window.CloseRequested += Hide;
        UpdateTitle();
        if (modal && owner != host)
        {
            modalOwner = owner;
            modalOwner.FormClosed += ModalClosed;
        }
        window.Show(owner);
        var screen = Screen.FromControl(owner).WorkingArea;
        float scale = window.DeviceDpi / 96f;
        var size = floatingBounds?.Size ?? new Size((int)(900 * scale), (int)(780 * scale));
        size = new Size(Math.Min(size.Width, screen.Width), Math.Min(size.Height, screen.Height));
        var location = floatingBounds?.Location ?? new Point(Math.Min(host.Right, screen.Right) - size.Width, screen.Top + (screen.Height - size.Height) / 2);
        location.X = Math.Clamp(location.X, screen.Left, screen.Right - size.Width);
        location.Y = Math.Clamp(location.Y, screen.Top, screen.Bottom - size.Height);
        window.Bounds = new Rectangle(location, size);
    }

    private void ParkView()
    {
        if (view.Parent != dock.Panel) dock.Panel.Controls.Add(view);
    }

    private void CloseFrame()
    {
        if (window == null) return;
        if (!window.IsDisposed)
        {
            floatingBounds = window.WindowState == FormWindowState.Normal ? window.Bounds : window.RestoreBounds;
            window.Retiring = true;
            window.CloseRequested -= Hide;
            window.Close();
            window.Dispose();
        }
        window = null;
    }

    private void ModalClosed(object sender, FormClosedEventArgs e)
    {
        // Detach before an owned frame is disposed. Restore after ShowDialog's
        // native modal loop re-enables the workspace, not inside FormClosed.
        UnhookModal();
        ParkView();
        CloseFrame();
        if (visible && !host.IsDisposed) host.BeginInvoke(new Action(UpdatePresentation));
    }

    private void UnhookModal()
    {
        if (modalOwner != null) modalOwner.FormClosed -= ModalClosed;
        modalOwner = null;
    }
    private void UpdateTitle() { if (window != null) window.Text = view.PageTitle; }
    private void HostClosed(object sender, FormClosedEventArgs e) => Dispose();
    private static Control FocusedControl(Control control)
    {
        if (control.Focused) return control;
        foreach (Control child in control.Controls)
            if (child.ContainsFocus) return FocusedControl(child);
        return null;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Application.Idle -= CheckModality;
        UnhookModal();
        if (host != null) host.FormClosed -= HostClosed;
        if (view != null)
        {
            view.Parent?.Controls.Remove(view);
            view.Dispose();
        }
        CloseFrame();
        dock?.Dispose();
    }
}
