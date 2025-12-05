using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    /// <summary>
    /// A theoretically abstract superclass of the concrete forms that may be displayed in StatsDirect.
    /// In reality, as the form designer can't cope with abstract superclasses, this is concrete with a whole load of "subclass should have implemented" exceptions.
    /// </summary>
    public /* abstract */ class StatsDirectForm: Form, IForm
    {
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

        private static int NEXT_ID = 1;

        protected StatsDirectForm()
        {
            id = "StatsDirectForm:" + NEXT_ID++.ToString(CultureInfo.InvariantCulture);
            // Add this in the StatsDirectForm constructor so that it's earlier in the call chain than the subclass' close, and can therefore set variables before the subclass does anything.
            FormClosing += StatsDirectForm_FormClosing;
        }

        void StatsDirectForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            DoOrSwallow(() => SdApplication.SoleInstance.NoteASubformCloseIsStarting());
        }

        private static void DoOrSwallow(Action func)
        {
#if !WATCH_EXCEPTIONS
            try
            {
#endif
            func();
#if !WATCH_EXCEPTIONS
            }
            catch (Exception ex)
            {
                SdApplication.WriteToBlackbox("Unexpected exception in spreadsheet form", ex);
            }
#endif
        }

        /// <summary>
        /// Request the form to save its contents, over existing storage if it has that or to new storage if not.
        /// </summary>
        /// <returns>true if the content was saved, false if not</returns>
        internal /* abstract */ virtual bool SaveContents() { throw new NotSupportedException(); }

        /// <summary>
        /// Request the form to save its contents to new storage.
        /// </summary>
        /// <returns>true if the content was saved, false if not</returns>
        internal /* abstract */ virtual bool SaveAsContents() { throw new NotSupportedException(); }

        /// <summary>
        /// If true, changes have been made to the form since it was last saved.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Dirty { get; protected set; }

        internal bool SafeToClose => dirtyButSafeToClose || !Dirty;

        /// <summary>
        /// Return true if the form is allowed to close, false if it is not
        /// </summary>
        /// <returns></returns>
        protected bool AllowClose()
        {
            if (!Dirty)
                return true;

            DialogResult result = SdApplication.SoleInstance.MsgboxX(Text + " has changes that have not been saved. Do you want to save these changes?", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation, "StatsDirect", false, MessageBoxDefaultButton.Button3);
            if (DialogResult.Cancel == result)
            {
                SdApplication.SoleInstance.NoteASubformCloseIsCancelled();
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
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal string Path
        {
            get => path;
            set
            {
                path = value;
                // Alert our info, as we may need to update our tab and title
                if (null != Tag)
                    ((WindowInformation)Tag).Path = path;
            }
        }

        /// <param name="isTempFile"></param>
        /// <param name="nameToDisplay">If null or isTempFile is false (the normal case), use the filename.  If non-null and isTempFile is true, use this as the name to be shown for the file.</param>
        /// <param name="filename"></param>
        public /* abstract */ virtual bool OpenFile(string filename, bool isTempFile, string nameToDisplay) { throw new NotImplementedException("Subclass should have implemented"); }

        internal virtual void ShowHelp()
        {
            // By default, show the ambient help.  Subclasses may override this.
            SdApplication.SoleInstance.ShowCurrentHelp();
        }

        public /* abstract */ virtual IList<Pane> AvailablePanes => throw new NotSupportedException();

        public /* abstract */ virtual Pane SelectedPane => throw new NotSupportedException();

        /// <summary>
        /// Helper function for subclasses who want to implement IForm.EnsureActive - they can simply call this.
        /// </summary>
        public void EnsureActive()
        {
            if (this != SdApplication.SoleInstance.ActiveMdiChild)
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

        internal /* abstract */ virtual void EditCopy() { throw new NotSupportedException(); }

        internal /* abstract */ virtual void EditCut() { throw new NotSupportedException(); }

        internal /* abstract */ virtual void EditPaste() { throw new NotSupportedException(); }

        internal /* abstract */ virtual void Print() { throw new NotSupportedException(); }

        internal WindowInformation WindowInformation => (WindowInformation)Tag;

        /// <summary>
        /// Ensure anything related to batch mode is removed from this window.  Subclasses may override as necessary but should ensure they call base.ClearBatchMode(); the default is to do nothing.
        /// </summary>
        internal virtual void ClearBatchMode()
        {
            // Do nothing
        }
    }
}
