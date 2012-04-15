using SpreadsheetGear.Windows.Forms;

namespace StatsDirect.UI
{
    public class SDWorkbookView : WorkbookView
    {
        /** No longer required - keeping the class in case there's anything else we need to override!
        protected override void OnMouseDown(System.Windows.Forms.MouseEventArgs e)
        {
            if (SDApplication.SoleInstance.SelectingData && e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                // NoteEndOfSelection clears both selectingData and inputtingData.  However, that's safe here, as we only get here if we're SelectingData.
                SDApplication.SoleInstance.NoteEndOfSelection(true);
            }
            else
            {
                base.OnMouseDown(e);
            }
        }
         */
    }
}
