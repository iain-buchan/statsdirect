using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    /// <summary>
    /// A theoretically abstract superclass of the concrete forms that may be displayed in StatsDirect.
    /// In reality, this is concrete solely because the VS2008 designer can't handle abstract classes in the hierarchy.
    /// </summary>
    public /* abstract */ class StatsDirectForm: Form, IForm
    {
        /// <summary>
        /// If true, changes have been made to the form since it was last saved.
        /// </summary>
        protected bool dirty;
        /// <summary>
        /// The path from which the form was loaded, or null if unknown.
        /// </summary>
        protected string path;
        /// <summary>
        /// True if there have been changes made, but the user has said that they're safe to discard
        /// </summary>
        protected bool dirtyButSafeToClose;

        /// <summary>
        /// An internal identity for this form that is unique and persistent for the lifetime of the application.
        /// </summary>
        private readonly string id;

        private static int nextId = 1;

        protected StatsDirectForm()
        {
            id = "StatsDirectForm:" + (nextId++).ToString();
        }

        internal virtual bool SaveContents()
        {
            throw new NotImplementedException();
        }

        internal virtual bool SaveAsContents()
        {
            throw new NotImplementedException();
        }

        internal bool Dirty
        {
            get { return dirty; }
        }

        internal bool SafeToClose
        {
            get { return dirtyButSafeToClose || !dirty; }
        }

        /// <summary>
        /// Return true if the form is allowed to close, false if it is not
        /// </summary>
        /// <returns></returns>
        protected bool AllowClose()
        {
            if (!dirty)
                return true;

            DialogResult result = MessageBox.Show(Text + " has changes that have not been saved. Do you want to save these changes?", "StatsDirect", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button3);
            if (DialogResult.Cancel == result)
            {
                return false;
            }
            if (DialogResult.No == result)
            {
                dirtyButSafeToClose = true;
                return true;
            }
            return SaveContents();
        }

        /// <summary>
        /// Note that, despite being asked to close, the application is not closing.
        /// Amend any state that may have been touched during the close operation.
        /// </summary>
        internal void NoteNonClosure()
        {
            dirtyButSafeToClose = false;
        }

        /// <summary>
        /// The full path to the file shown in this form, if any
        /// </summary>
        internal string Path
        {
            get { return path; }
            set
            {
                path = value;
                // Alert our info, as we may need to update our tab and title
                if (null != Tag)
                {
                    ((WindowInformation)Tag).Path = path;
                }
            }
        }

        public /* abstract */ virtual bool OpenFile(string Filename)
        {
            throw new NotImplementedException("Subclass should have overridden this");
        }

        internal virtual void ShowHelp()
        {
            // By default, show the ambient help.  Subclasses may override this.
            SDApplication.SoleInstance.ShowCurrentHelp();
        }

        public virtual bool ImplementsIReport
        {
            get { return false; }
        }

        public virtual bool ImplementsIGrid
        {
            get { return false; }
        }

        public virtual bool ImplementsIScriptWindow
        {
            get { return false; }
        }

        public virtual /* abstract */ IList<Pane> AvailablePanes
        {
            get { throw new NotImplementedException(); }
        }

        public virtual /* abstract */ Pane SelectedPane
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Helper function for subclasses who want to implement IForm.EnsureActive - they can simply call this.
        /// </summary>
        public void EnsureActive()
        {
            if (this != SDApplication.SoleInstance.MainWindow.ActiveMdiChild)
                Activate();
        }

        public string Id()
        {
            return id;
        }

        public virtual bool SelectPane(Pane pane)
        {
            throw new NotImplementedException();
        }

        internal /* abstract */ virtual void EditCopy() {}

        internal /* abstract */ virtual void EditCut() {}

        internal /* abstract */ virtual void EditPaste() {}

        internal /* abstract */ virtual void Print() {}

        internal WindowInformation WindowInformation
        {
            get { return (WindowInformation)Tag; }
        }

        /// <summary>
        /// Ensure anything related to batch mode is removed from this window.  Subclasses may override as necessary but should ensure they call base.ClearBatchMode(); the default is to do nothing.
        /// </summary>
        internal virtual void ClearBatchMode()
        {
            // Do nothing
        }
    }
}
