using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    /// <summary>
    /// Holds general information about a window - its handle, its tab strip and so on.
    /// This is typically placed as the Tag of any UI object concerned with the window,
    /// so that it is possible to find all of our window information in one place.
    /// </summary>
    public sealed class WindowInformation
    {
        private WeakReference? window;
        private string? path;

        internal StatsDirectForm? Window
        {
            get
            {
                if (window is null)
                    return null;
                if (!window.IsAlive)
                    return null;
                return (StatsDirectForm?)window.Target;
            }
            set => window = new WeakReference(value);
        }

        [MemberNotNullWhen(true, nameof(Window))]
        internal bool HasWindow => window is not null && window.IsAlive;

        internal TabPage? TabPage { get; set; }

        internal bool IsNew => path is null;

        internal string? FriendlyName
        {
            get
            {
                if (path is not null)
                    return System.IO.Path.GetFileNameWithoutExtension(path);
                if (Window is not null)
                    return Window.Text;
                if (TabPage is not null)
                    return TabPage.Text;
                return null;
            }
            set
            {
                if (!IsNew)
                    throw new ArgumentException("Can only set the friendly name of a new window");
                if (Window is not null)
                    Window.Text = value;
                if (TabPage is not null)
                {
                    TabPage.Text = value;
                    TabPage.ToolTipText = value;
                }
            }
        }

        /// <summary>
        /// The full path to the file that is presently loaded in this window
        /// </summary>
        internal string Path
        {
            set
            {
                path = value;
                string? fileName = System.IO.Path.GetFileNameWithoutExtension(value);
                if (Window is not null)
                    Window.Text = fileName;
                if (TabPage is not null)
                {
                    TabPage.Text = fileName;
                    TabPage.ToolTipText = value;
                }
            }
        }

        internal void EditCopy() => Window?.EditCopy();

        internal void EditCut() => Window?.EditCut();

        internal void EditPaste() => Window?.EditPaste();

        internal void Print() => Window?.Print();

        /// <summary>
        /// Returns true iff candidateFilename is the file that this window has open
        /// </summary>
        /// <param name="candidateFilename"></param>
        /// <returns></returns>
        internal bool IsFile(string candidateFilename)
        {
            if (window is null || !window.IsAlive)
                return false;

            if (path is not null)
                return path.Equals(candidateFilename);
            string? f = FriendlyName;
            return f is not null && f.Equals(candidateFilename);
        }
    }
}
