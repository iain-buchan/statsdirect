using System;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    /// <summary>
    /// Holder for a dialog form to be displayed, and code to be run when that dialog closes. Used when the dialog is queued due to other dialogs being ahead of it.
    /// </summary>
    internal class DialogAndAction
    {
        public Form Form { get; }
        public Action<Form, DialogResult> PostCloseAction { get; }

        public DialogAndAction(Form form, Action<Form, DialogResult> postCloseAction)
        {
            Form = form;
            PostCloseAction = postCloseAction;
        }
    }
}