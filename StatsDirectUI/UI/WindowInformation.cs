using System;
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
        private WeakReference window;
        private TabPage tabPage;
        private string path;

        internal StatsDirectForm Window
        {
            get
            {
                if (null == window)
                    return null;
                if (!window.IsAlive)
                    return null;
                return (StatsDirectForm)window.Target;
            }
            set
            {
                window = new WeakReference(value);
            }
        }

        internal bool HasWindow => null != window && window.IsAlive;

        internal TabPage TabPage
        {
            get { return tabPage; }
            set { tabPage = value; }
        }

        internal bool IsNew => null == path;

        internal string FriendlyName
        {
            get
            {
                if (null != path)
                    return System.IO.Path.GetFileNameWithoutExtension(path);
                if (HasWindow)
                    return Window.Text;
                if (null != tabPage)
                    return tabPage.Text;
                return null;
            }
            set
            {
                if (!IsNew)
                    throw new ArgumentException("Can only set the friendly name of a new window");
                if (HasWindow)
                    Window.Text = value;
                if (null != tabPage)
                {
                    tabPage.Text = value;
                    tabPage.ToolTipText = value;
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
                string fileName = System.IO.Path.GetFileNameWithoutExtension(value);
                if (HasWindow)
                    Window.Text = fileName;
                if (null != tabPage)
                {
                    tabPage.Text = fileName;
                    tabPage.ToolTipText = value;
                }
            }
        }

        internal void EditCopy()
        {
            if (HasWindow)
                Window.EditCopy();
        }

        internal void EditCut()
        {
            if (HasWindow)
                Window.EditCut();
        }

        internal void EditPaste()
        {
            if (HasWindow)
                Window.EditPaste();
        }

        internal void Print()
        {
            if (HasWindow)
                Window.Print();
        }

        /// <summary>
        /// Returns true iff candidateFilename is the file that this window has open
        /// </summary>
        /// <param name="candidateFilename"></param>
        /// <returns></returns>
        internal bool IsFile(string candidateFilename)
        {
            if (!window.IsAlive)
                return false;

            if (null != path)
                return path.Equals(candidateFilename);
            string f = FriendlyName;
            return null != f && f.Equals(candidateFilename);
        }
    }
}
