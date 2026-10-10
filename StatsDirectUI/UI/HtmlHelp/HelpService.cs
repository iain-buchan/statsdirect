using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using StatsDirect.UI.ToolWindows;

namespace StatsDirect.UI.HtmlHelp;

internal sealed class HelpService : IDisposable
{
    private ToolPaneSession session;
    private HelpView view;
    private bool disposed;
    internal ToolWindow Window => session?.Window;
    internal HelpView View => view;
    internal ToolDockHost DockHost => session?.DockHost;
    internal bool IsDocked => session?.IsDocked == true;
    internal bool IsVisible => session?.IsVisible == true;
    internal bool IsTemporary => session?.IsTemporary == true;
    internal static Form FindOwner(Form parent) => ToolPaneSession.FindOwner(parent);

    internal void Show(Form parent, string topic, string directory)
    {
        if (disposed) return;
        if (session == null)
        {
            Form root = parent?.MdiParent ?? parent ?? throw new ArgumentNullException(nameof(parent));
            while (root.Owner != null) root = root.Owner;
            view = new HelpView(new HelpCatalog(directory));
            session = new ToolPaneSession(root, view, "Help", DockStyle.Right, 460, new Size(900, 780),
                view.SetPresentation, null, view.ShowNotice, view.PreservePositionAsync);
            view.ModeRequested += async () => await session.ToggleModeAsync();
            view.CloseRequested += session.Hide;
            view.TitleChanged += () => session.SetTitle(view.PageTitle);
        }
        session.Show(false);
        view.ShowTopic(topic);
    }
    internal Task ToggleModeAsync() => session?.ToggleModeAsync() ?? Task.CompletedTask;
    internal void Hide() => session?.Hide();
    public void Dispose() { disposed = true; session?.Dispose(); }
}
