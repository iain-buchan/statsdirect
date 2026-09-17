using StatsDirect.Utilities;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    /// <summary>
    /// User interface shortcuts for operation scripts, covering the few things an operation needs that ITemplateHost does not offer.
    /// </summary>
    /// <remarks>
    /// Operation scripts are compiled into their own assembly at run time, so this class and everything a script calls on it must be public.
    /// Used by SearchAndReplace.xml and SearchAndReplaceAdvanced.xml; run StatsDirect -sanity-check after changing a signature here.
    /// </remarks>
    public static class OperationHacks
    {
        /// <summary>
        /// Send keystrokes to the most recently used grid, as if the user had typed them there.
        /// </summary>
        /// <param name="keys">The keystrokes, in System.Windows.Forms.SendKeys format - for example, "^h" for Ctrl+H</param>
        /// <exception cref="TemplateOperationCancelledException">If that grid is no longer open.  The menu only offers grid operations while a grid is active, but the recent operations list does not check.</exception>
        public static void SendKeys(string keys)
        {
            WindowInformation activeGrid = SdApplication.SoleInstance.ActiveGrid;
            if (null == activeGrid || !activeGrid.HasWindow || activeGrid.Window is not frmSpreadsheetGear grid)
                throw new TemplateOperationCancelledException("Please open or select a data workbook first.", "StatsDirect");
            grid.SendKeysToGrid(keys);
        }

        /// <summary>
        /// Show an informational message and wait for the user to dismiss it.
        /// </summary>
        /// <param name="message">The message to show</param>
        public static void ShowInformation(string message)
        {
            SdApplication.SoleInstance.MsgboxX(message, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
