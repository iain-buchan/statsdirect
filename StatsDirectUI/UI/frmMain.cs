// If ALLOW_OPTIONAL_UNMANAGED_CODE is defined, the application is free to use unmanaged code to get around annoyances.
// Current uses:
// - Removes flicker when swapping between maximised MDI children using tabs
#define ALLOW_OPTIONAL_UNMANAGED_CODE

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using SpreadsheetGear.Advanced.Cells;
using StatsDirect.Builtins;
using StatsDirect.Configuration;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.UI.Properties;
using StatsDirect.Utilities;
using System.Security.Permissions;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.IO;
using SpreadsheetGear.Windows.Forms;
using SpreadsheetGear;
using Color = System.Drawing.Color;
using SystemColors = System.Drawing.SystemColors;
using StatsDirect.R;
using StatsDirect.Charting;
using InvalidDataException = StatsDirect.Templates.InvalidDataException;

namespace StatsDirect.UI
{
    public partial class frmMain : Form, IToolStripHost
    {
        private static readonly char[] BAR = { '|' };
        private static readonly char[] EQUALS = { '=' };
        private const string USER_INPUT_TABLE_NAME = "table";

        /// <summary>
        /// The minimum amount of other decoration that must be preserved above and below the operations panel.  Forces large panels to scroll.
        /// </summary>
        private const int HEIGHT_BREATHING_SPACE = 100;

        /// <summary>
        /// The name of the parameter to be passed around a result set that contains a list of operations that have contributed to the list.
        /// </summary>
        private const string OPERATION_MEMORY_NAME = "statsdirect-operation-list";
        /// <summary>
        /// Maximum length of the recent operations list
        /// </summary>
        private const int MAX_RECENT_OPERATIONS = 10;
        /// <summary>
        /// The presumed mean width in pixels of a character in a text box, for use when controlling the box's max length.
        /// </summary>
        private const int CHARWIDTH = 8;
        /// <summary>
        /// True if a child window is presently being activated via a tab click - in which case we don't try to set the active tab when the window notification comes in
        /// </summary>
        private bool activatingViaTab /* = false */;
        /// <summary>
        /// True if a child window is presently being activated via a window click - in which case we don't try to set the active window when the tab notification comes in
        /// </summary>
        private bool activatingViaWindow;
        /// <summary>
        /// A holder where we are activating a window via a tab.
        /// </summary>
        Form mostRecentlySelectedWindow;

        /// <summary>
        /// If true, a grid selection is in progress
        /// </summary>
        private bool selectingData /* = false */;
        /// <summary>
        /// If true, non-grid data entry is inprogress using the top bar
        /// </summary>
        private bool inputtingData /* = false */;
        private bool okPressed /* = false */;
        private bool cancelPressed /* = false */;

        /// <summary>
        /// The index of the tab that was most recently right-clicked.
        /// Used as a way of knowing which tab was clicked after a right-click menu is used.
        /// </summary>
        private int lastClickedTabIndex = -1;

        /// <summary>
        /// A way of keeping starting parameters between operations, where follow-on operations are in use.
        /// </summary>
        private ParameterBag knownParameters;

        /// <summary>
        /// A way of passing the ambient parameters into the visibility checks.
        /// This should be null except during a FillCombinedParameters call.
        /// </summary>
        private ParameterBag fillCombinedParametersContext;

        private List<ToolStripMenuItem> recentFileEntries;

        private object lastSeenMenuItemTag;

        PanelType currentPanelType;
        readonly Stack<PanelType> panelTypeStack;

        private Operation mostRecentOperation;
        private bool settingUpSubOperations /* = false */;
        private bool settingUpRecentOperations /* = false */;

        internal bool InsideSubformClose { get; set; }
        /// <summary>
        /// Keep a count of panel pops during a close operation, so that (if necessary) we can fix up the panels after a cancel.
        /// </summary>
        private int pendingPanelPops /* = 0 */;

        // Record the running scale factor used, for sizing controls we add dynamically where they don't do it themselves
        private SizeF currentScaleFactor = new SizeF(1f, 1f);

        /// <summary>
        /// Outside the debugger, the runtime cannot propagate exception through native code - the native handler gets them and fails.
        /// In two key places, exceptions are "punted" through the native code of a DoEvents loop.
        /// 1) SelectCells;
        /// 2) FillCombinedPanel.
        /// 
        /// Look for users of this variable for the gory details.
        /// Ideally the entire template system would be rebuilt to not steal the flow of control, at which point the system could be turned inside-out and there would be no need for this code (it would also work better in, say, an asp.net environment).
        /// </summary>
        private Exception puntedException;

        private enum PanelType
        {
            /// <summary>
            /// Tabs and toolstrip
            /// </summary>
            Default,
            /// <summary>
            /// Frame selection
            /// </summary>
            Selection,
            /// <summary>
            /// Data entry and follow-on operation selection
            /// </summary>
            Operations,
            /// <summary>
            /// Progress bar, label and cancel button
            /// </summary>
            Progress,
            /// <summary>
            /// Simulacrum of a Windows message box in the top dialog area
            /// </summary>
            ModalMessage
        };

        public frmMain()
        {
            InitializeComponent();
            tipBatch.SetToolTip(chkBatchMode, "Run this function again automatically");
            panelTypeStack = new Stack<PanelType>();
            ShowPanel(PanelType.Default, false);
            UpdateFileList();
            UpdateToolsMenu();
            cboActiveReport.Items.Add(new ComboFormAdapter(null));
            cboActiveReport.SelectedIndex = 0;
            cboRecentOperations.SelectedIndex = 0;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void AddTemplates()
        {
            SDMenuItem rootItem = LoadMenuItems(Path.Combine(SDConfiguration.InstallationDirectory, Settings.Default.MenuFileName));
            foreach (SDMenuItem sdMenuItem in rootItem.SubItems)
            {
                ToolStripItem menuItem = MakeMenuItem(sdMenuItem);
                menuItem.MergeAction = MergeAction.Replace;
                mnuMain.Items.Insert(mnuMain.Items.Count - 3, menuItem);
            }

            SDMenuItem userDefinedItem = UserMenuItems();
            if (null != userDefinedItem)
            {
                ToolStripItem userDefinedMenuItem = MakeMenuItem(userDefinedItem);
                userDefinedMenuItem.MergeAction = MergeAction.Replace;
                mnuMain.Items.Insert(mnuMain.Items.Count - 3, userDefinedMenuItem);
            }

            // #844: Templates can be loaded with windows open (for example from the Excel add-in).  Make sure that menus are set up for the active window if there is one.
            if (ActiveMdiChild is StatsDirectForm)
                SdApplication.SoleInstance.NoteFormActivated((WindowInformation)ActiveMdiChild.Tag);
            else
                SetMenuVisibility(false);
        }

        internal void SetMenuVisibility(bool isGridVisible)
        {
            foreach (ToolStripItem item in mnuMain.Items)
            {
                SetMenuVisibility(item, isGridVisible);
            }
        }

        private static bool SetMenuVisibility(ToolStripItem item, bool isGridVisible)
        {
            // Do the sub-items first; this gives us a chance to detect the case of all sub-items being disabled.
            bool atLeastOneSub = false;
            bool atLeastOneSubVisible = false;
            if (item.GetType() == typeof(ToolStripMenuItem))
            {
                foreach (ToolStripItem subItem in ((ToolStripMenuItem)item).DropDownItems)
                {
                    atLeastOneSub = true;
                    atLeastOneSubVisible |= SetMenuVisibility(subItem, isGridVisible);
                }
            }

            bool enabledViaGrid = true;
            object tagObject = ToTagObject(item);
            if (null != tagObject)
            {
                if (tagObject is Dictionary<string, string>)
                {
                    Dictionary<string, string> tagDictionary = (Dictionary<string, string>)tagObject;
                    string operationName;
                    if (tagDictionary.TryGetValue("operation", out operationName))
                    {
                        Operation operation = TemplateFactory.Operations[operationName]; // TODO: User operations
                        enabledViaGrid = isGridVisible || !operation.RequiresGrid;
                    }
                }
            }

            item.Enabled = enabledViaGrid && (atLeastOneSubVisible || !atLeastOneSub);
            return item.Enabled;
        }

        private static object ToTagObject(ToolStripItem item)
        {
            if (null == item)
                return null;

            object o = item.Tag;

            if (null == o)
                return null;

            // Check for one of our specially formatted key-value strings
            if (o is string)
            {
                string s = (string)o;
                if (s.StartsWith("#{") && s.EndsWith("}"))
                    return ToTagObject(s);
            }

            return o;
        }

        private static object ToTagObject(string s)
        {
            Dictionary<string, string> output = new Dictionary<string, string>();
            string trimmedS = s.Substring(2, s.Length - 3);
            string[] pairs = trimmedS.Split(BAR);
            foreach (string pairString in pairs)
            {
                string[] keyValue = pairString.Split(EQUALS);
                if (keyValue.Length == 2)
                    output[keyValue[0]] = keyValue[1];
            }
            return output;
        }

        private ToolStripItem MakeMenuItem(SDMenuItem sdMenuItem)
        {
            if ("-".Equals(sdMenuItem.Label))
            {
                return new ToolStripSeparator { Size = new Size(167, 6) };
            }
            ToolStripMenuItem menuItem = new ToolStripMenuItem
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Size = new Size(167, 22),
                Text = sdMenuItem.Label
            };
            // 167,22 is merely a convenient magic size that came from the VS2005 designer; it may not be "right", but it works.
            if (!string.IsNullOrEmpty(sdMenuItem.Tooltip))
                menuItem.ToolTipText = sdMenuItem.Tooltip;
            Dictionary<string, string> tags = new Dictionary<string, string>();
            if (null != sdMenuItem.Operation && TemplateFactory.Operations.ContainsKey(sdMenuItem.Operation))
            {
                tags.Add("operation", sdMenuItem.Operation);
                // menuItem.BackColor = Color.PaleGreen;
            }
            if (null != sdMenuItem.Help)
            {
                if (null != sdMenuItem.Help.ChmId)
                {
                    // Prevent string injection into tags
                    int chmId = int.Parse(sdMenuItem.Help.ChmId);
                    tags.Add("help", chmId.ToString());
                }
            }
            menuItem.Tag = ToTagString(tags);
            menuItem.Click += OperationMenuHandler;
            menuItem.MouseEnter += menuItem_MouseEnter;
            menuItem.MouseLeave += menuItem_MouseLeave;
            if (null != sdMenuItem.SubItems)
            {
                foreach (SDMenuItem subMenuItem in sdMenuItem.SubItems)
                    menuItem.DropDownItems.Add(MakeMenuItem(subMenuItem));
            }
            return menuItem;
        }

        private static string ToTagString(Dictionary<string, string> tags)
        {
            if (null == tags || tags.Count == 0)
                return null;
            StringBuilder sb = new StringBuilder();
            sb.Append("#{");
            bool first = true;
            foreach (KeyValuePair<string, string> pair in tags)
            {
                if (first)
                    first = false;
                else
                    sb.Append('|');
                sb.Append(pair.Key);
                sb.Append('=');
                sb.Append(pair.Value);
            }
            sb.Append('}');
            return sb.ToString();
        }

        void menuItem_MouseLeave(object sender, EventArgs e)
        {
            lastSeenMenuItemTag = null;
        }

        void menuItem_MouseEnter(object sender, EventArgs e)
        {
            lastSeenMenuItemTag = ToTagObject((ToolStripItem)sender);
        }

        public SDMenuItem LoadMenuItems(string pathName)
        {
            XmlSerializer s = new XmlSerializer(typeof(SDMenuItem));
            using (TextReader r = new StreamReader(pathName))
            {
                return (SDMenuItem)s.Deserialize(r);
            }
        }

        public SDMenuItem UserMenuItems()
        {
            List<SDMenuItem> items = new List<SDMenuItem>();

            // Not in use for 3.0 release.  TODO: Enable
            /**
            foreach (Operation operation in TemplateFactory.UserOperations)
            {
                SDMenuItem item = new SDMenuItem();
                item.Operation = operation.Names[0];
                if (operation.FriendlyNames.Count > 0)
                    item.Label = operation.FriendlyNames[0];
                else
                    item.Label = operation.Names[0];
                items.Add(item);
            }
             */
            if (0 == items.Count)
                return null;
            return new SDMenuItem { Label = "&User-defined", SubItems = items.ToArray() };
        }

        private void OperationMenuHandler(object sender, EventArgs e)
        {
#if !WATCH_EXCEPTIONS
            try
            {
#endif
                ToolStripMenuItem menuItem = (ToolStripMenuItem)sender;
                if (null != menuItem.Tag)
                    Debug.Print((string)menuItem.Tag);
                object tagObject = ToTagObject(menuItem);
                if (!(tagObject is Dictionary<string, string>))
                    return;
                Dictionary<string, string> tags = (Dictionary<string, string>)tagObject;
                string operationName;
                if (!tags.TryGetValue("operation", out operationName))
                    return;
                Operation operation;
                if (!TemplateFactory.Operations.TryGetValue(operationName, out operation))
                    return;
                DoOperationWithPossibleBatching(operation);
#if !WATCH_EXCEPTIONS
            }
            catch (Exception ex)
            {
                if (InOperation)
                    PuntThroughEventLoop(ex);
                else
                    throw;
            }
#endif
        }

        /// <remarks>Almost, but not quite, a clone of DoOperation</remarks>
        private void DoOperationWithPossibleBatching(Operation operation)
        {
            // As we invoke an operation that the user might batch, we know that we're not currently batching.
            ClearBatchMode();
            try
            {
                do
                {
                    bool succeeded = DoOperationOnceOrUntilCancelled(operation, null);
                    if (!succeeded)
                        break;
                    if (chkBatchMode.Checked)
                    {
                        // Ensure the next loop doesn't start with the data that's currently highlighted.
                        if (null != SdApplication.SoleInstance
                            && null != SdApplication.SoleInstance.ActiveGrid
                            && SdApplication.SoleInstance.ActiveGrid.HasWindow)
                        {
                            ((IGrid)SdApplication.SoleInstance.ActiveGrid.Window).ClearSelection();
                        }
                    }
                    // If we're batching, then we must be doing something with grid input.  So, if we're going round again, ensure our grid is visible.
                    if (chkBatchMode.Checked)
                    {
                        if (SdApplication.SoleInstance.ActiveGrid != null)
                            SdApplication.SoleInstance.ActiveGrid.Window.Activate();
                    }
                } while (chkBatchMode.Checked);
            }
            catch (CancelCurrentOperationAndDoException ex)
            {
                if (InOperation)
                {
                    PuntThroughEventLoop(ex);
                }
                else
                {
                    throw;
                }
            }
#if !WATCH_EXCEPTIONS
            catch (Exception ex)
            {
                SdApplication.SoleInstance.EraseAnyOutstandingParameters();
                SdApplication.SoleInstance.FriendlyError("Error while running operation", ex, false);
            }
#endif
        }

        private void ClearBatchMode()
        {
            chkBatchMode.Checked = false;
            // Ensure there are no remembered batch details
            SdApplication.SoleInstance.ClearBatchMode();
        }

        public void DoOperation(string operationName)
        {
            try
            {
                Operation operation = TemplateFactory.Operations[operationName];
                SdApplication.SoleInstance.MainWindow.DoOperationOnceOrUntilCancelled(operation, null);
            }
            catch (CancelCurrentOperationAndDoException ex)
            {
                if (InOperation)
                {
                    PuntThroughEventLoop(ex);
                }
                else
                {
                    throw;
                }
            }
#if !WATCH_EXCEPTIONS
            catch (Exception ex)
            {
                SdApplication.SoleInstance.EraseAnyOutstandingParameters();
                SdApplication.SoleInstance.FriendlyError("Error while running operation", ex, false);
            }
#endif
        }

        /// <summary>
        /// Create and add a new grid window
        /// </summary>
        internal StatsDirectForm CreateGrid()
        {
            return CreateGrid(null);
        }

        /// <summary>
        /// Create and add a new grid window
        /// </summary>
        internal StatsDirectForm CreateGrid(string unsavedName)
        {
            // Make and add the child window
            using (new WaitCursor())
            {
                frmSpreadsheetGear child = new frmSpreadsheetGear();
                child.SetUnsavedName(unsavedName ?? child.Text + " " + SdApplication.SoleInstance.GetGridNumber());
                SetUpForm(child);
                return child;
            }
        }

        internal StatsDirectForm FindOrOpenGrid(string filename)
        {
            foreach (WindowInformation wi in SdApplication.SoleInstance.Windows)
            {
                if (wi.IsFile(filename))
                    return wi.Window;
            }
            // If we get here, no existing grid has the file open - we'll have to reopen it if we can.

            // Check that the grid was, in fact, a file.  If it doesn't contain a directory separator, it wasn't - it was therefore almost certainly never saved and we can't recover it.
            if (filename.IndexOf(Path.DirectorySeparatorChar) < 0)
                return null;
            return CreateGrid(filename, true, null);
        }

        internal StatsDirectForm CreateGrid(string filename, bool isTempFile, string nameToDisplay)
        {
            using (new WaitCursor())
            {
                StatsDirectForm newGrid = CreateGrid(nameToDisplay);
                bool opened = false;
                try
                {
                    opened = newGrid.OpenFile(filename, isTempFile, nameToDisplay);
                    if (!isTempFile)
                        SdApplication.SoleInstance.NoteRecentFile(filename, opened);
                }
                catch (IOException ex)
                {
                    SdApplication.SoleInstance.FriendlyError("Couldn't open spreadsheet", ex, true);
                    SdApplication.SoleInstance.NoteRecentFile(filename, false);
                }
                if (!opened)
                {
                    newGrid.Close();
                    SdApplication.SoleInstance.NoteFormClosing(newGrid, new FormClosingEventArgs(CloseReason.None, false));
                }
                return opened ? newGrid : null;
            }
        }

        /// <summary>
        /// Create and add a new report window
        /// </summary>
        internal StatsDirectForm CreateReport()
        {
            // Make and add the child window
            using (new WaitCursor())
            {
                StatsDirectForm child = new frmReportRichEdit();
                string childName = child.Text + " " + SdApplication.SoleInstance.GetReportNumber();
                child.Text = childName;
                SetUpForm(child);
                return child;
            }
        }

        internal StatsDirectForm CreateReport(string filename, bool isTempFile)
        {
            StatsDirectForm newReport = CreateReport();
            bool opened = false;
            try
            {
                opened = newReport.OpenFile(filename, isTempFile, null);
                if (!isTempFile)
                    SdApplication.SoleInstance.NoteRecentFile(filename, opened);
            }
            catch (IOException ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't open report", ex, true);
                SdApplication.SoleInstance.NoteRecentFile(filename, false);
            }
            if (!opened)
            {
                newReport.Close();
                SdApplication.SoleInstance.NoteFormClosing(newReport, new FormClosingEventArgs(CloseReason.None, false));
            }
            return opened ? newReport : null;
        }

        /// <summary>
        /// Create and add a new script window
        /// </summary>
        internal StatsDirectForm CreateScriptWindow()
        {
            // Make and add the child window
            using (new WaitCursor())
            {
                frmScript child = new frmScript();
                string childName = child.Text + " " + SdApplication.SoleInstance.GetScriptWindowNumber();
                child.Text = childName;
                SetUpForm(child);
                return child;
            }
        }

        internal StatsDirectForm CreateScriptWindow(string filename, bool isTempFile)
        {
            StatsDirectForm newScriptWindow = CreateScriptWindow();
            bool opened = false;
            try
            {
                opened = newScriptWindow.OpenFile(filename, isTempFile, null);
                if (!isTempFile)
                    SdApplication.SoleInstance.NoteRecentFile(filename, opened);
            }
            catch (IOException ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't open script", ex, true);
                SdApplication.SoleInstance.NoteRecentFile(filename, false);
            }
            if (!opened)
            {
                newScriptWindow.Close();
                SdApplication.SoleInstance.NoteFormClosing(newScriptWindow, new FormClosingEventArgs(CloseReason.None, false));
            }
            return opened ? newScriptWindow : null;
        }

        /// <summary>
        /// Create and add a new grid window
        /// </summary>
        private void SetUpForm(StatsDirectForm child)
        {
            using (new WaitCursor())
            {
                // Make and add the child window
                child.MdiParent = this;

                // Make and add the corresponding tab(page)
                TabPage tabPage = new TabPage(child.Text);
                tabWindows.TabPages.Add(tabPage);
                tabWindows.SelectedTab = tabPage;

                // Add the report to the drop-down reports list
                if (child is IReport)
                {
                    ComboFormAdapter cfa = new ComboFormAdapter(child);
                    cboActiveReport.Items.Add(cfa);
                    cboActiveReport.SelectedItem = cfa;
                }

                // Store the information about the window
                WindowInformation info = new WindowInformation { TabPage = tabPage, Window = child };
                child.Tag = info;
                tabPage.Tag = info;
                SdApplication.SoleInstance.AddWindow(info);

                // Update the display
                closeToolStripMenuItem.Enabled = (tabWindows.TabPages.Count > 0);
                child.WindowState = FormWindowState.Maximized;
                child.Show();
                // Work around an unpleasant glitch in the framework that stops maximised windows showing their icons when first shown.
                // From http://www.xtremedotnettalk.com/showthread.php?t=94923
                if (mnuMain.Items[0].GetType().Name == "SystemMenuItem")
                {
                    // int t = mnuMain.Height;
                    mnuMain.Items[0].Image = child.Icon.ToBitmap();
                    // mnuMain.Height = t; // otherwise it goes too big.  Fixed by using 16x16 icons only.
                }
            }
        }

        private void newGridToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                CreateGrid();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Creating a new grid failed due to an internal error", ex, false);
            }
        }

        private void tabWindows_Selecting(object sender, TabControlCancelEventArgs e)
        {
            try
            {
                // If there are no tabs to activate, the tab page will be null - in which case we need to do nothing
                if (null == e.TabPage)
                    return;

                WindowInformation info = (WindowInformation)e.TabPage.Tag;
                // The tab may be asked to activate while it is still being set up, hence before it has an associated window.  Handle that case.
                if (null != info && info.HasWindow && !activatingViaWindow)
                {
                    ActivateWindowViaTab(info.Window);
                }
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Swapping windows failed due to an internal error", ex, false);
            }
        }

        private void ActivateWindowViaTab(StatsDirectForm window)
        {
            activatingViaTab = true;
            // Defer activation until after the tab's processing finishes, as otherwise the tab forcibly grabs the focus back after we can't do anything about it.
            mostRecentlySelectedWindow = window;
            postTabTimer.Enabled = true;
            activatingViaTab = false;
        }

        internal void RemoveWindow(StatsDirectForm window)
        {
            RemoveTab(window.WindowInformation.TabPage);
            if (window is IReport)
                RemoveReportFromCombo(window);
        }

        private void RemoveReportFromCombo(StatsDirectForm window)
        {
            cboActiveReport.Items.Remove(new ComboFormAdapter(window));
            // Ensure there's always a selection unless we have no items at all
            if (null == cboActiveReport.SelectedItem && cboActiveReport.Items.Count > 0)
                cboActiveReport.SelectedIndex = cboActiveReport.Items.Count - 1;
        }

        private void RemoveTab(TabPage tabPage)
        {
            if (tabWindows.TabPages.Contains(tabPage))
                tabWindows.TabPages.Remove(tabPage);
            closeToolStripMenuItem.Enabled = (tabWindows.TabPages.Count > 0);
        }

        private void closeTabToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                WindowInformation lastClickedTab = TabStripLastClickedTab();
                if (null != lastClickedTab)
                {
                    lastClickedTab.Window.Close();
                }
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Closing the tab failed due to an internal error", ex, false);
            }
        }

        private WindowInformation TabStripLastClickedTab()
        {
            if (lastClickedTabIndex >= 0)
            {
                return (WindowInformation)tabWindows.TabPages[lastClickedTabIndex].Tag;
            }
            return null;
        }

        /// <summary>
        /// A window has been activated and wants to synchronise its tab.
        /// Ensure the specified tab is activated, carefully avoiding recursive activation chaos if we're already selecting the window via the tab.
        /// </summary>
        /// <param name="tabPage"></param>
        internal void EnsureTabSelected(TabPage tabPage)
        {
            if (!activatingViaTab)
            {
                activatingViaWindow = true;
                tabWindows.SelectedTab = tabPage;
                activatingViaWindow = false;
            }
            if (null == cboActiveReport.SelectedItem)
            {
                WindowInformation wi = (WindowInformation)tabPage.Tag;
                if (null != wi)
                {
                    if (wi.HasWindow)
                    {
                        StatsDirectForm f = wi.Window;
                        cboActiveReport.SelectedItem = new ComboFormAdapter(f);
                    }
                }
            }
        }

        private void newReportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                CreateReport();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Creating a report failed due to an internal error", ex, false);
            }
        }

        private void saveToolStripButton_Click(object sender, EventArgs e)
        {
            try
            {
                WindowInformation activeInfo = ActiveWindowInformation();
                if (null != activeInfo)
                    activeInfo.Window.SaveContents();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Save failed due to an internal error", ex, false);
            }
        }

        /// <summary>
        /// Returns the active window's WindowInformation object, or null if there is no active window or the active window has no WindowInformation.
        /// </summary>
        /// <returns>the active window's WindowInformation object, or null if there is no active window or the active window has no WindowInformation</returns>
        private WindowInformation ActiveWindowInformation()
        {
            if (null == ActiveMdiChild)
                return null;
            return (WindowInformation)ActiveMdiChild.Tag;
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                // Each child form will have been given the opportunity to save its data and close.
                InsideSubformClose = false;

                // If any have refused, they will not be safe to close - we should cancel the close if so.
                bool cancel = false;
                foreach (Form child in MdiChildren)
                {
                    if (child is StatsDirectForm && !((StatsDirectForm)child).SafeToClose)
                        cancel = true;
                }
                if (cancel)
                {
                    e.Cancel = true;
                    foreach (Form child in MdiChildren)
                        if (child is StatsDirectForm)
                            ((StatsDirectForm)child).NoteNonClosure();
                }
                else
                {
                    // Stop handling opens from other StatsDirect instances, as we're about to close.
                    IpcListener.StopListening();
                    SaveApplicationState();
                }
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Closing the main form failed due to an internal error", ex, false);
            }
        }

        /// <summary>
        /// Load any window state we need from persistent storage.
        /// </summary>
        private void LoadWindowState()
        {
            const double FRACTION_OF_PRIMARY = 0.75;

            // If our settings have previously been saved, load them now.  Otherwise, default to 75% width and height, centred, on the primary screen.
            if (Settings.Default.MainWidth <= 0)
            {
                foreach (Screen screen in Screen.AllScreens)
                {
                    if (screen.Primary)
                    {
                        const double inflation = 0.0 - ((1.0 - FRACTION_OF_PRIMARY) / 2.0);
                        Rectangle windowBounds = new Rectangle(screen.Bounds.Location, screen.Bounds.Size);
                        windowBounds.Inflate((int)(screen.Bounds.Width * inflation), (int)(screen.Bounds.Height * inflation));
                        Bounds = windowBounds;
                    }
                }
            }
            else
            {
                try
                {
                    Top = Settings.Default.MainTop;
                    Left = Settings.Default.MainLeft;
                    Width = Settings.Default.MainWidth;
                    Height = Settings.Default.MainHeight;

                    // Check against current screen settings - on remote desktops, for example, a user may now have a smaller screen.
                    // If the window's title bar is completely invisible, force it onto the main screen.
                    Screen primaryScreen = null;
                    bool titleBarIsVisible = false;
                    Rectangle titleBarRect = new Rectangle(Left, Top, Width, 20); // Assume a 20 pixel high title bar - TODO: get from system structures
                    foreach (Screen screen in Screen.AllScreens)
                    {
                        // Fast test, save the primary screen in case we need it later
                        if (screen.Primary)
                            primaryScreen = screen;

                        // Can we see enough of the title bar on this screen to be useful?
                        // Clone the rect as intersect is destructive
                        Rectangle visibleTitleBar = new Rectangle(titleBarRect.X, titleBarRect.Y, titleBarRect.Width, titleBarRect.Height);
                        visibleTitleBar.Intersect(screen.Bounds);
                        // For the sake of argument, a 40x10 pixel rectangle of the title bar is "good enough" to drag it on.
                        if (visibleTitleBar.Height > 10 && visibleTitleBar.Width > 40)
                        {
                            titleBarIsVisible = true;
                            break;
                        }
                    }
                    if (null != primaryScreen && !titleBarIsVisible)
                    {
                        // Move the window onto the primary display
                        Rectangle bounds = primaryScreen.WorkingArea;
                        Top = bounds.Top;
                        Left = bounds.Left;
                        Width = bounds.Width;
                        Height = bounds.Height;
                    }
                }
                catch (NullReferenceException)
                {
                    // The window was saved maximised, so we don't have sizes
                }
                WindowState = Settings.Default.MainWindowState;
                // Prevent starting in a minimised state
                if (FormWindowState.Minimized == WindowState)
                    WindowState = FormWindowState.Normal;
            }
        }

        /// <summary>
        /// Save any window state we need to our Settings object.
        /// </summary>
        private void SaveWindowState()
        {
            // No point saving maximised or minimised settings, they're 0,0 when minimised or screen size when maximised
            if (FormWindowState.Normal == WindowState)
            {
                Settings.Default.MainTop = Top;
                Settings.Default.MainLeft = Left;
                Settings.Default.MainWidth = Width;
                Settings.Default.MainHeight = Height;
            }
            Settings.Default.MainWindowState = WindowState;
        }

        /// <summary>
        /// Save any window state we need to persistent storage.
        /// </summary>
        private void SaveApplicationState()
        {
            SaveWindowState();
            Settings.Default.Save();
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            LoadWindowState();
            AddTemplates();
        }

        public bool CanSelectMultipleRows
        {
            set { lblNonAdjoined.Visible = value; }
        }

        public bool CanSelectGroupMethod
        {
            set
            {
                lblGroupsBy.Visible = value;
                optGroupsByColumn.Visible = value;
                optGroupsByIdentifier.Visible = value;
            }
        }

        public bool GroupsByIdentifier
        {
            set
            {
                optGroupsByColumn.Checked = !value;
                optGroupsByIdentifier.Checked = value;
            }
        }

        /// <summary>
        /// Shows the Input Menubar and waits for the user to select a range of data
        /// </summary>
        /// <param name="selectionMessage">The message to be shown to the user as what they're selecting</param>
        /// <param name="cancelButtonLabel">If null, the cancel button shows its standard message.  If non-null, the cancel button shows this.</param>
        /// <param name="wasPivoted">true if the user changed SelectGroupsByIdentifier, false if not</param>
        /// <returns>true if the user clicked OK, false if the user clicked Cancel</returns>
        public bool SelectCells(string selectionMessage, string cancelButtonLabel, out bool wasPivoted)
        {
            bool status;
            bool oldSelectGroupsByIdentifier = SdApplication.SoleInstance.Preferences.SelectGroupsByIdentifier;
            using (new DefaultCursor())
            {
                lblSelectionMessage.Text = selectionMessage;
                ShowPanel(PanelType.Selection, false);
                string oldCancelText = cmdCancel.Text;
                if (null != cancelButtonLabel)
                    cmdCancel.Text = cancelButtonLabel;
                selectingData = true;
                okPressed = false;
                cancelPressed = false;
                // wait here until user presses OK or Cancel, or does something else suitable
                do
                {
                    Application.DoEvents(); // HACK: Force an inner event loop
                    Thread.Sleep(5);
                } while (selectingData);
                if (null != puntedException)
                {
                    Exception ex = puntedException;
                    puntedException = null;
                    throw ex;
                }
                if (okPressed)
                {
                    status = true;
                    wasPivoted = false;
                }
                else
                {
                    status = false;
                    wasPivoted = (oldSelectGroupsByIdentifier != SdApplication.SoleInstance.Preferences.SelectGroupsByIdentifier);
                }
                if (!wasPivoted)
                    ShowPanel(PanelType.Default, false);
                cmdCancel.Text = oldCancelText;
                // Wait for the screen to update
            }
            return status;
        }

        private void cmdOK_Click(object sender, EventArgs e)
        {
            NoteEndOfSelection(true);
        }

        /// <summary>
        /// Stops any pending selection or input of data.
        /// </summary>
        /// <param name="ok"></param>
        public void NoteEndOfSelection(bool ok)
        {
            okPressed = ok;
            selectingData = false;
            inputtingData = false;
        }

        public bool IsSelecting
        {
            get { return selectingData; }
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            try
            {
                bool wasInputting = inputtingData;
                NoteEndOfSelection(false);
                if (wasInputting)
                {
                    IEnumerable<Parameter> parametersBeingCollected = GetAllOutstandingParameters();
                    MaybeCloseOperationOnCancel(parametersBeingCollected);
                }
            }
            catch (Exception ex)
            {
                PuntThroughEventLoop(ex);
            }
        }

        /// <summary>
        /// A user has just cancelled a running operation.
        /// Under some circumstances, this returns them to the menu of operations they can do.
        /// Under other circumstances, this closes the operation completely.
        /// Depending on which, we either throw a CloseCurrentOperationException (which closes the operation completely) or merely return, which allows the menu of follow-on operations to be shown if required.
        /// </summary>
        private static void MaybeCloseOperationOnCancel(IEnumerable<Parameter> parametersBeingCollected)
        {
            if (ShouldCloseOperationOnCancel(parametersBeingCollected))
                throw new CloseCurrentOperationException();
        }

        /// <summary>
        /// Returns true iff the current operation is one that should close the entire set of operations if it is cancelled while it's processing.
        /// </summary>
        /// <returns></returns>
        private static bool ShouldCloseOperationOnCancel(IEnumerable<Parameter> parametersBeingCollected)
        {
            // #573: Always close operations on cancel.
            /*
            // If the operation is unknown, back out of it as soon as possible.  This case should never occur in theory.
            if (null == mostRecentOperation)
                return true;

            // Operations that are follow-ons should not close completely unless they are the only follow-on; they should offer the choice of doing other follow-ons.
            if (mostRecentOperation.HasPrerequisites)
            {
                // Which prereq was run?  If we can't find one, assume close completely.
                if (null == knownParameters)
                    return true;
                if (!knownParameters.ContainsKey(OPERATION_MEMORY_NAME))
                    return true;
                IList<string> previousOperations = knownParameters[OPERATION_MEMORY_NAME].AsStringList;
                foreach (string prereqName in mostRecentOperation.PrerequisiteOperationNames)
                {
                    if (previousOperations.Contains(prereqName))
                    {
                        // Found the operation - see what it has
                        Operation previousOperation = TemplateFactory.Operations[prereqName];
                        // Does the prereq allow other follow-ons?  If so, keep open; if not, close.
                        return previousOperation.AvailableSuggestedOperations(new TemplateProcessor(SDApplication.SoleInstance), knownParameters).Count <= 1;
                    }
                }

                // If we get here, no operation matched.  Don't know how we got here; close.
                return false;
            }

            // If we get here, it's not a follow-on
             */

            bool atLeastOneCancel = false;
            bool atLeastOneNonCancel = false;
            foreach (Parameter parameter in parametersBeingCollected)
            {
                atLeastOneCancel |= (null != parameter.CancelSkipsParameter);
                atLeastOneNonCancel |= (null == parameter.CancelSkipsParameter);
            }
            // If all the parameters we're gathering are skipped if a cancel happens, skip - and don't close the operation
            if (atLeastOneCancel && !atLeastOneNonCancel)
                return false;

            // Otherwise, operations that are not follow-ons should always close completely
            return true;
        }

        /// <summary>
        /// Returns true iff the current operation is one that should close the entire set of operations if it is cancelled while it's processing.
        /// </summary>
        /// <returns></returns>
        private bool ShouldShowClose()
        {
            // If the operation is unknown, back out of it as soon as possible.  This case should never occur in theory.
            if (null == mostRecentOperation)
                return true;

            // Operations that are follow-ons should not close completely unless they are the only follow-on; they should offer the choice of doing other follow-ons.
            if (mostRecentOperation.HasPrerequisites)
            {
                // Which prereq was run?  If we can't find one, assume close completely.
                if (null == knownParameters)
                    return true;
                if (!knownParameters.ContainsKey(OPERATION_MEMORY_NAME))
                    return true;
                return false;
            }

            // Otherwise, operations that are not follow-ons should always close completely
            return true;
        }

        private IEnumerable<Parameter> GetAllOutstandingParameters()
        {
            List<Parameter> parameters = new List<Parameter>();
            TableLayoutPanel tlp = GetUserInputTable();
            if (null != tlp)
            {
                foreach (Control column in tlp.Controls)
                {
                    foreach (Control control in column.Controls)
                    {
                        if (null != control.Tag)
                        {
                            Parameter parameter = (Parameter)control.Tag;
                            if (!parameters.Contains(parameter))
                                parameters.Add(parameter);
                        }
                    }
                }
            }
            return parameters;
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFile();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Open failed due to an internal error", ex, false);
            }
        }

        internal bool OpenFile()
        {
            // Unknown path, prompt for path to which to save
            openFileDialog.Title = "Open file";
            DialogResult result = openFileDialog.ShowDialog(this);
            if (DialogResult.Cancel == result)
            {
                // User cancelled, failed open
                return false;
            }
            return OpenFile(openFileDialog.FileName, false);
        }

        internal bool OpenFile(string path, bool removeFromRecentFilesIfNotFound)
        {
            using (new WaitCursor())
            {
                string fileName = Path.GetFileName(path);
                bool isTempFile = null != fileName && fileName.StartsWith("~");
                // User wants to open the file - but which file type?
                string extension = Path.GetExtension(path);
                if (null != extension)
                    extension = extension.ToLower();
                if (".xls".Equals(extension) || ".xlsx".Equals(extension))
                {
                    CreateGrid(path, isTempFile, null);
                    return true;
                }
                if (".rtf".Equals(extension) || ".htm".Equals(extension) || ".html".Equals(extension) || ".mht".Equals(extension) || ".mhtml".Equals(extension) || ".txt".Equals(extension))
                {
                    CreateReport(path, isTempFile);
                    return true;
                }
                if (".cs".Equals(extension) || ".vb".Equals(extension))
                {
                    CreateScriptWindow(path, isTempFile);
                    return true;
                }
                if (".sdw".Equals(extension))
                {
                    return OpenSdwOrPrompt(path);
                }
                SdApplication.SoleInstance.MsgboxX("Could not open '" + path + "'.  StatsDirect 3 can only open Excel, rich text, HTML and script files.", MessageBoxButtons.OK, MessageBoxIcon.Error, "StatsDirect", true);
                SdApplication.SoleInstance.NoteRecentFile(path, false);
                return false;
            }
        }

        private bool OpenSdwOrPrompt(string path)
        {
            while (true)
            {
                string sd2Path;
                bool sd2ExistsAndSupportsConversion = FindStatsDirect2(out sd2Path);
                if (sd2ExistsAndSupportsConversion)
                    return ConvertSdwAndOpen(path, sd2Path);

                // If we get here, SD2 exists but does not support conversion (path not null) or does not exist at all (path null)
                bool tryToOpen = PromptUserToInstallOrUpgradeSd2(null != sd2Path);
                if (!tryToOpen)
                    return false;
            }
        }

        private bool ConvertSdwAndOpen(string sdwPath, string sd2Path)
        {
            SdApplication.SoleInstance.StartProgress("Converting file", false);
            string originalSdwPath = sdwPath;
            try
            {
                // Name our converted file and try to create one to see if we can (and hence if we believe SD2 will be able to).
                // Assume the filename ends with ".sdw".  The converted file will be "~fromsd2.xls".
                string convertedPath = sdwPath.Substring(0, sdwPath.Length - 4) + "~fromsd2.xls";
                // If we can convert the file in situ, do so.  If not (because we can't write the new file), copy to a temporary location which we expect to be writable, then convert.
                bool canWrite;
                try
                {
                    // Try to open and then close the converted file; this will throw an exception if we can't.
                    using (Stream s = File.Create(convertedPath))
                    {
                    }
                    // Now that it's created, try to delete it; again this will throw an exception if we can't.
                    File.Delete(convertedPath);

                    // If we get here, we can create and delete the converted file (and it is presently deleted).  Assume SD2 will also be able to create it.
                    canWrite = true;
                }
                catch (UnauthorizedAccessException)
                {
                    // If we get here, we couldn't create or delete the converted file.  Assume SD2 will also be unable to do so.
                    canWrite = false;
                }
                if (!canWrite)
                {
                    // Copy the file to %TEMP% (which should always be writable or else the user will already have considerable other problems) and convert from there.
                    string copiedSdwPath = Path.Combine(Environment.GetEnvironmentVariable("TEMP"), Path.GetFileName(sdwPath));
                    File.Copy(sdwPath, copiedSdwPath);
                    sdwPath = copiedSdwPath;
                    convertedPath = sdwPath.Substring(0, sdwPath.Length - 4) + "~fromsd2.xls";
                }

                string arguments = "/FileConvert \"" + sdwPath + "\"";
                ProcessStartInfo startInfo = new ProcessStartInfo { UseShellExecute = false, FileName = sd2Path, Arguments = arguments, WindowStyle = ProcessWindowStyle.Minimized, CreateNoWindow = true };
                Process p = Process.Start(startInfo);
                while (true)
                {
                    bool exited = p.WaitForExit(50);
                    if (exited)
                        break;
                    if (SdApplication.SoleInstance.UpdateProgress(0))
                    {
                        p.Kill();
                        throw new TemplateOperationCancelledException();
                    }
                }
                int exitCode = p.ExitCode;
                if (0 != exitCode)
                    throw new Exception("The .sdw file was not converted successfully");
                else
                {
                    if (!File.Exists(convertedPath))
                        throw new Exception("The converted file does not exist");

                    // If we get here, the file should exist
                    CreateGrid(convertedPath, true, Path.GetFileNameWithoutExtension(convertedPath).Replace("~fromsd2", ""));
                    File.Delete(convertedPath);
                    // If we couldn't write, we copied the file for conversion.  Delete that copied file.
                    // As a paranoia check, NEVER delete the original - the code should never get here if the two were the same, but even so.
                    if (!canWrite && !originalSdwPath.Equals(sdwPath))
                        File.Delete(sdwPath);
                    return true;
                }
            }
            finally
            {
                SdApplication.SoleInstance.FinishProgress();
            }
        }

        private bool PromptUserToInstallOrUpgradeSd2(bool isUpgrade)
        {
            using (frmInstallStatsDirect2 f = new frmInstallStatsDirect2(isUpgrade))
            {
                f.ShowDialog(this);
                return f.UserThinksStatsDirect2IsInstalled;
            }
        }

        private bool FindStatsDirect2(out string sd2Path)
        {
            string programFilesFolder = Environment.GetFolderPath(Environment.Is64BitOperatingSystem ? Environment.SpecialFolder.ProgramFilesX86 : Environment.SpecialFolder.ProgramFiles);
            string statsDirectFolder = Path.Combine(programFilesFolder, "StatsDirect");
            if (!Directory.Exists(statsDirectFolder))
            {
                sd2Path = null;
                return false;
            }
            string sd2ExePath = Path.Combine(statsDirectFolder, "StatsDirect.exe");
            if (!File.Exists(sd2ExePath))
            {
                sd2Path = null;
                return false;
            }
            // If we get here, there's something claiming to be StatsDirect.exe
            sd2Path = sd2ExePath;
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(sd2ExePath);
            // 2.8.0 was the earliest version with the open functionality.  It's only available in StatsDirect 2.
            return versionInfo.ProductMajorPart == 2 && versionInfo.ProductMinorPart >= 8;
        }

        private void newScriptToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                CreateScriptWindow();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Creating a script window failed due to an internal error", ex, false);
            }
        }

        private void cmdClose_Click(object sender, EventArgs e)
        {
            try
            {
                // The Close button is sometimes visible in place of the Cancel button.  Deal with this!
                if (selectingData || inputtingData)
                {
                    cancelPressed = true;
                    bool wasInputting = inputtingData;
                    IEnumerable<Parameter> parametersBeingCollected = GetAllOutstandingParameters();
                    CancelCurrentOperation();

                    if (wasInputting)
                        MaybeCloseOperationOnCancel(parametersBeingCollected);
                }
                else
                {
                    // If we get here, the Close button closes the operation.
                    // Nuke the cache of starting parameters, so that they are not used by mistake in later tests.
                    knownParameters = null;
                    ShowPanel(PanelType.Default, false);
                }
            }
            catch (Exception ex)
            {
                PuntThroughEventLoop(ex);
            }
        }

        /// <summary>
        /// An unpleasant hack.  In debug mode, CLR exceptions propagate up the stack through sections of native code.
        /// In release code, they don't, hence this mechanism to avoid Windows exceptions being triggered.
        /// </summary>
        /// <param name="ex"></param>
        internal void PuntThroughEventLoop(Exception ex)
        {
            puntedException = ex;
            selectingData = false;
            inputtingData = false;
        }

        /// <summary>
        /// Ensure the specified panel is shown.
        /// </summary>
        /// <param name="panelType"></param>
        /// <param name="enforceHeightOnOperations"></param>
        /// <remarks>Even if the panel is already shown, this re-shows; this is because the panel sometimes requires resizing.</remarks>
        private void ShowPanel(PanelType panelType, bool enforceHeightOnOperations)
        {
            try
            {
                pnlTop.SuspendLayout();
                pnlOperations.Visible = PanelType.Operations == panelType;
                pnlSelection.Visible = PanelType.Selection == panelType;
                pnlDefault.Visible = PanelType.Default == panelType;
                pnlProgress.Visible = PanelType.Progress == panelType;
                pnlModalMessage.Visible = PanelType.ModalMessage == panelType;
                switch (panelType)
                {
                    case PanelType.Default:
                        pnlDefault.BringToFront();
                        pnlTop.Height = pnlDefault.Height;
                        break;
                    case PanelType.ModalMessage:
                        pnlModalMessage.BringToFront();
                        pnlTop.Height = Math.Max(pnlModalMessage.PreferredSize.Height, 58);
                        break;
                    case PanelType.Operations:
                        pnlOperations.BringToFront();
                        ResizeContainer(enforceHeightOnOperations);
                        break;
                    case PanelType.Progress:
                        pnlProgress.BringToFront();
                        pnlTop.Height = pnlProgress.Height;
                        break;
                    case PanelType.Selection:
                        pnlSelection.BringToFront();
                        pnlTop.Height = pnlSelection.Height;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("panelType", panelType, "Unknown panel type to be shown");
                }
            }
            finally
            {
                pnlTop.ResumeLayout();
            }
            currentPanelType = panelType;
        }

        private void ResizeContainer(bool enforceHeightOnOperations)
        {
            int tableHeight = 0;
            if (HasUserInputTable())
            {
                TableLayoutPanel tlp = GetUserInputTable();
                tableHeight = tlp.PreferredSize.Height + tlp.Margin.Top;
                tlp.Width = pnlUser.Width;
            }
            // The operations panel is always at least the same height as the standard panel (58 pixels), but must also fit both left-hand and right-hand content
            int leftHandHeight = tlpSelectOperation.Height;
            int contentHeight = Math.Max(tableHeight, leftHandHeight);
            contentHeight = Math.Max(contentHeight, 58);

            // Set a constraint on the maximum height of the table so that it's never off the bottom of the window
            int constrainedHeight = Math.Min(contentHeight, Height - HEIGHT_BREATHING_SPACE);
            if (enforceHeightOnOperations)
            {
                pnlTop.Height = constrainedHeight;
                pnlOperations.Height = constrainedHeight;
                tlpOperations.Height = contentHeight;
                pnlUser.Height = contentHeight;
            }

            bool shouldScrollVertically = (constrainedHeight < contentHeight);
            tlpOperations.AutoScroll = shouldScrollVertically;
            // The following is a workaround for the TableLayoutPanel apparently not following its own wishes for height, even when the preferred height is reported correctly.  No idea why!
            if (HasUserInputTable())
            {
                TableLayoutPanel tlp = GetUserInputTable();
                tlp.Height = contentHeight;
            }
        }

        private bool HasUserInputTable()
        {
            return pnlUser.Controls.ContainsKey(USER_INPUT_TABLE_NAME);
        }

        private void PushPanel(PanelType panelType, bool enforceHeightOnOperations)
        {
            panelTypeStack.Push(currentPanelType);
            ShowPanel(panelType, enforceHeightOnOperations);
        }

        private void PopPanel(bool enforceHeightOnOperations)
        {
            /** Removed functionality for now - we'll put up with the slight flicker on close in exchange for not having to work out all the ways this might go wrong!
            if (InsideSubformClose)
            {
                pendingPanelPops++;
                return;
            }
             **/
            PanelType pt = PanelType.Default;
            if (panelTypeStack.Count > 0)
                pt = panelTypeStack.Pop();
            ShowPanel(pt, enforceHeightOnOperations);
        }

        /// <summary>
        /// Ensure the relevant operation is performed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cmdCalculate_Click(object sender, EventArgs e)
        {
            try
            {
                // The Calculate button is sometimes visible in place of the OK button.  Deal with this!
                if (selectingData || inputtingData)
                {
                    selectingData = false;
                    inputtingData = false;
                    okPressed = true;
                    return;
                }
                DoCalculate();
            }
            catch (Exception ex)
            {
                PuntThroughEventLoop(ex);
            }
        }

        private void DoCalculate()
        {
            try
            {
                DoCalculateInternal();
            }
            catch (CancelCurrentOperationAndDoException)
            {
                // If this is thrown, there must be an exception handler further up the stack capable of catching it - make sure we don't get in the way.
                throw;
            }
#if !WATCH_EXCEPTIONS
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Error while running operation", ex, false);
            }
#endif
        }

        private void DoCalculateInternal()
        {
            SDListItem selectedItem = (SDListItem)cboOperation.SelectedItem;
            Operation operation = TemplateFactory.Operations[selectedItem.Operation];
            DoOperationOnceOrUntilCancelled(operation, knownParameters);
        }

        /// <summary>
        /// Return true on success, false on failure such as a user close.
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="inputParameters"></param>
        /// <returns></returns>
        internal bool DoOperationOnceOrUntilCancelled(Operation operation, ParameterBag inputParameters)
        {
            Operation currentOperation = DoOperation(operation, inputParameters, false);
            // If we're in batch mode and an operation failed or was cancelled, return indicating failure.  This should cause the calling loop to quit.
            if (null == currentOperation && chkBatchMode.Checked)
                return false;
            // Re-run the same operation if it's a repeated one (such as an instant function)
            while ((null != currentOperation) && currentOperation.SuggestsSelf)
            {
                currentOperation = DoOperation(currentOperation, knownParameters, false);
                // If we're in batch mode and an operation failed or was cancelled, return indicating failure.  This should cause the calling loop to quit.
                if (null == currentOperation && chkBatchMode.Checked)
                    return false;
            }
            // If we get here, the operation may or may not have succeeded.
            return null != currentOperation;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="inputParameters"></param>
        /// <param name="isRedo"></param>
        /// <returns>The operation most recently run to completion (as it can change), or null if the most recent operation was cancelled or otherwise didn't complete</returns>
        internal Operation DoOperation(Operation operation, ParameterBag inputParameters, bool isRedo)
        {
            // If we're already running an operation, another DoOperation further up the stack has control.  Punt this operation to it!
            if (InOperation)
                throw new CancelCurrentOperationAndDoException(operation, inputParameters);

            // If we get here, we're the topmost DoOperation on the stack.  We have responsibility for processing the current operation plus anything that might interrupt it.
            while (true)
            {
                try
                {
                    Text = "StatsDirect: " + operation;
                    return DoOperationInternal(operation, inputParameters, isRedo);
                }
                catch (InvalidDataException ex)
                {
                    string errorMessage = ex.Message;
                    SdApplication.SoleInstance.MsgboxX(errorMessage, MessageBoxButtons.OK, MessageBoxIcon.Error, "StatsDirect", true);
                    // Treat this as a restart of the operation, without keeping any data - we don't know which data is bad, and if we keep it we risk getting stuck in a loop
                    /*
                    // That one failed due to invalid data - keep the same data and try it again, which should prompt the user to fix it!
                    inputParameters = ex.InputParameters;
                     */
                }
                catch (CancelCurrentOperationAndDoException ex)
                {
                    CancelCurrentOperation();
                    ShowPanel(PanelType.Default, false); // As if starting from a new operation
                    operation = ex.Operation;
                    inputParameters = ex.InputParameters;
                    // Go round again, processing this operation
                }
                catch (SelectedOperationChangedException ex)
                {
                    if (isRedo)
                    {
                        // We don't want to risk going round again; there's a good chance of an infinite loop.  Close instead.
                        CloseCurrentOperation();
                        return null;
                    }
                    else
                    {
                        CancelCurrentOperation();
                        SDListItem selectedItem = (SDListItem)cboOperation.SelectedItem;
                        operation = TemplateFactory.Operations[selectedItem.Operation];
                        // Keep existing input parameters.  TODO: Is this correct, or should we be going back to the originals?
                        inputParameters = ex.InputParameters;
                        // Go round again, processing this operation
                    }
                }
                catch (CloseCurrentOperationException)
                {
                    CloseCurrentOperation();
                    // The operation did not run to completion
                    return null;
                }
                finally
                {
                    ResetHelp();
                    Text = "StatsDirect";
                }
            }
        }

        private static void ResetHelp()
        {
            SdApplication.SoleInstance.ActiveHelpUrl = null;
            SdApplication.SoleInstance.ActiveHelpTopic = 0; // ToC
        }

        private void CloseCurrentOperation()
        {
            CancelCurrentOperation();
            ShowPanel(PanelType.Default, false);
        }

        private void CancelCurrentOperation()
        {
            // We may have been selecting or inputting data.  If we were, ensure we're not now.
            if (selectingData || inputtingData)
            {
                selectingData = false;
                inputtingData = false;
                okPressed = false;
                cancelProgressPressed = false;
                // No other state needs fixing.
            }
            // We may have had one or more parameters displayed
            ClearCombinedParameters();
            SdApplication.SoleInstance.EraseAnyOutstandingParameters();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="inputParameters"></param>
        /// <param name="isRedo"></param>
        /// <returns>The most recently run operation if it ran to completion, null otherwise</returns>
        /// <remarks>This is decorated with HandlesChangedOperationAttribute to mark that its *caller* will catch that.  DO NOT allow this to be called by anything that doesn't catch SelectedOperationChangedException and CancelCurrentOperationAndDoException!
        /// Similarly, it's decorated in such a way that the compiler won't inline it, even though the method is only called in one place.  This ensures the attribute is preserved for the stack walk we do in InOperation.</remarks>
        [CallerHandlesChangedOperation]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private Operation DoOperationInternal(Operation operation, ParameterBag inputParameters, bool isRedo)
        {
            using (new WaitCursor())
            {
                mostRecentOperation = operation;
                NoteRecentOperation(operation);

                // Set help
                if (null != operation.HelpContext)
                {
                    SdApplication.SoleInstance.ActiveHelpTopic = operation.HelpContext.ChmId;
                    if (null != operation.HelpContext.Url)
                        SdApplication.SoleInstance.ActiveHelpUrl = operation.HelpContext.Url;
                }

                // Self-referential operations are assumed to be instant and repeatable, so are set up immediately in the interface.  Others are run normally, and only then do they get any follow-on operations.
                TemplateProcessor templateProcessor = new TemplateProcessor(SdApplication.SoleInstance);
                ParameterBag outputParameters;
                SuggestFromOperation(operation, inputParameters, SuggestionTime.BeforeOperation);
                if (operation.SuggestsSelf)
                {
                    if (null == inputParameters)
                        inputParameters = new ParameterBag();
                    outputParameters = SetInterfaceAndTryToRun(templateProcessor, operation, inputParameters);
                }
                else
                {
                    outputParameters = templateProcessor.Execute(operation, inputParameters, isRedo); // Keep the results of this; some functions (notably the best subset of multiple linear regression) relies on replacing parameters
                    // Null output parameters indicate a cancelled operation
                    if (null != outputParameters)
                        NoteOperation(outputParameters, operation);
                }

                ParameterBag endingParameters;
                if (null == outputParameters)
                {
                    // User cancelled - keep the starting parameters for when they run again
                    endingParameters = inputParameters;
                }
                else
                {
                    endingParameters = new ParameterBag();
                    foreach (KeyValuePair<string, FilledParameter> pair in outputParameters.Pairs)
                    {
                        if (pair.Value.IsInputParameter)
                            endingParameters.Add(pair);
                    }
                }
                knownParameters = endingParameters;

                // If the operation succeeded and there are any follow-up operations, suggest them to the user now.
                if (null != outputParameters)
                    SuggestFromOperation(operation, endingParameters, SuggestionTime.AfterOperation);

                return null == outputParameters ? null : operation;
            }
        }

        /// <summary>
        /// The specified operation caused the results to be generated.  Note it in the list of operations that are kept with the results.
        /// </summary>
        /// <param name="results"></param>
        /// <param name="operation"></param>
        private static void NoteOperation(ParameterBag results, Operation operation)
        {
            if (null == results)
                return;

            // TODO: Do we need to clone the list (or, more likely, the FilledParameter and the list) so that operations that are cancelled don't pollute the list of operations that succeed?
            if (!results.ContainsKey(OPERATION_MEMORY_NAME))
                results.Add(OPERATION_MEMORY_NAME, new FilledParameter(true, new List<string>()));
            IList<string> operations = results[OPERATION_MEMORY_NAME].AsStringList;
            string operationName = operation.Name;
            if (!operations.Contains(operationName))
                operations.Add(operationName);
        }

        internal void SuggestFromOperation(Operation operation, ParameterBag inputParameters, SuggestionTime suggestionTime)
        {
            // If there are any follow-up operations, suggest them to the user now.
            // Follow-up operations are found in this order:
            // 1. If the current operation defines any, use only them.
            // 2. If a previous operation that has run with this set of parameters defines any, use the operations on the earliest such operation that ran.
            // 3. Otherwise, there are no follow-up operations.
            Operation suggestingOperation = operation;
            bool hasSuggestedOperations = suggestingOperation.AvailableSuggestedOperations(new TemplateProcessor(SdApplication.SoleInstance), inputParameters).Count > 0;
            if (!hasSuggestedOperations)
            {
                if (null != inputParameters)
                {
                    if (inputParameters.ContainsKey(OPERATION_MEMORY_NAME))
                    {
                        foreach (string operationName in inputParameters[OPERATION_MEMORY_NAME].AsStringList)
                        {
                            if (TemplateFactory.Operations.ContainsKey(operationName))
                            {
                                suggestingOperation = TemplateFactory.Operations[operationName];
                                hasSuggestedOperations = suggestingOperation.AvailableSuggestedOperations(new TemplateProcessor(SdApplication.SoleInstance), inputParameters).Count > 0;
                                if (hasSuggestedOperations)
                                    break;
                            }
                        }
                    }
                }
            }
            IList<SuggestedOperation> availableSuggestedOperations = suggestingOperation.AvailableSuggestedOperations(new TemplateProcessor(SdApplication.SoleInstance), inputParameters);
            bool suggestsOthers = availableSuggestedOperations.Count > 1
                || (availableSuggestedOperations.Count == 1 && TemplateFactory.Operations.ContainsKey(availableSuggestedOperations[0].Name) && suggestingOperation != TemplateFactory.Operations[availableSuggestedOperations[0].Name]);
            bool onlySuggestsFollowOns = (!operation.SuggestsSelf) && suggestingOperation == operation;

            if (SuggestionTime.BeforeOperation == suggestionTime)
            {
                pnlSelectOperation.Visible = hasSuggestedOperations && suggestsOthers && (operation.SuggestsSelf || !onlySuggestsFollowOns);
            }
            else
            {
                pnlSelectOperation.Visible = hasSuggestedOperations && suggestsOthers;
            }
            if (hasSuggestedOperations)
            {
                IList<SDListItem> suggestedListItems = new List<SDListItem>();
                foreach (SuggestedOperation su in availableSuggestedOperations)
                {
                    Operation suggestedOperation = TemplateFactory.Operations[su.Name];
                    string name = suggestedOperation.FriendlyName ?? suggestedOperation.Name;
                    suggestedListItems.Add(new SDListItem(name, su.Name));
                }
                ParameterBag strippedParameters = null == inputParameters ? new ParameterBag() : inputParameters.CopyWithoutOutputParameters();
                SuggestOperations(suggestedListItems, strippedParameters, operation, suggestionTime);
            }
        }

        internal void SuggestOperations(IList<SDListItem> suggestedListItems, ParameterBag inputParameters, Operation currentOperation, SuggestionTime suggestionTime)
        {
            // If it's the same list of operations, don't change the list!
            bool shouldRegenerateList = suggestedListItems.Count != cboOperation.Items.Count;
            if (!shouldRegenerateList)
            {
                // Same number of items - are they the same operations?
                for (int i = 0; i < suggestedListItems.Count; i++)
                {
                    if (!((SDListItem)cboOperation.Items[i]).Operation.Equals(suggestedListItems[i].Operation))
                    {
                        shouldRegenerateList = true;
                        break;
                    }
                }
            }

            try
            {
                int selectedItemNumber = 0;
                settingUpSubOperations = true;
                if (shouldRegenerateList)
                {
                    cboOperation.Items.Clear();
                    int itemNumber = 0;
                    foreach (SDListItem suggestedListItem in suggestedListItems)
                    {
                        cboOperation.Items.Add(suggestedListItem);
                        if (currentOperation.Name.Equals(suggestedListItem.Operation))
                        {
                            selectedItemNumber = itemNumber;
                        }
                        itemNumber++;
                    }
                }
                else
                {
                    int itemNumber = 0;
                    foreach (object oSuggestedOperation in cboOperation.Items)
                    {
                        SDListItem suggestedListItem = (SDListItem)oSuggestedOperation;
                        if (currentOperation.Name.Equals(suggestedListItem.Operation))
                        {
                            selectedItemNumber = itemNumber;
                            break;
                        }
                        itemNumber++;
                    }
                }
                if (cboOperation.Items.Count > 0)
                {
                    // cboOperation.Enabled = cboOperation.Items.Count > 1; Removed in #699
                    ShowPanel(PanelType.Operations, false);
                    knownParameters = inputParameters;
                    // ReSharper disable RedundantCheckBeforeAssignment
                    // Test before assignment, in case the combo throws events when assigning the same value
                    if (cboOperation.SelectedIndex != selectedItemNumber)
                    // ReSharper restore RedundantCheckBeforeAssignment
                    {
                        cboOperation.SelectedIndex = selectedItemNumber;
                    }
                    if (SuggestionTime.AfterOperation == suggestionTime)
                    {
                        SDListItem selectedItem = (SDListItem)cboOperation.SelectedItem;
                        Operation operation = TemplateFactory.Operations[selectedItem.Operation];
                        MaybeRunSelectedOperation(operation);
                    }
                }
            }
            finally
            {
                settingUpSubOperations = false;
            }
        }

        private static bool ShouldRunOperationOnSelection(Operation operation, ParameterBag inputParameters)
        {
            // If the operation has some initial parameters that can be batched, we should start it and let it populate those parameters
            if (operation.Steps.Count > 0)
            {
                Step firstStep = operation.Steps[0];
                if (firstStep is ParametersStep)
                {
                    ParametersStep pStep = (ParametersStep)firstStep;
                    foreach (Parameter p in pStep.Parameters)
                    {
                        if (p.MustRequest || (null != inputParameters && (null == p.Name || !inputParameters.ContainsKey(p.Name))))
                        {
                            // The parameter will probably be requested, unless it will be defaulted.
                            // CI parameters can be defaulted
                            if (p is ConfidenceIntervalParameter)
                            {
                                ConfidenceIntervalParameter cip = (ConfidenceIntervalParameter)p;
                                if (cip.CanDefault && SdApplication.SoleInstance.Preferences.CanDefaultConfidenceInterval)
                                {
                                    // The CI can be defaulted; no decision!
                                }
                                else
                                {
                                    // The CI cannot be defaulted; use our standard decision
                                    return SdApplication.SoleInstance.CanCombine(p);
                                }
                            }
                            else
                            {
                                return SdApplication.SoleInstance.CanCombine(p);
                            }
                        }
                    }
                }
            }
            // If we get here, the operation's first step isn't gathering parameters, or the parameters cannot be placed in the top bar, or no parameters need gathering (at which point the operation shouldn't be kicked off automatically).
            return false;
        }

        /// <summary>
        /// The given operation may be about to be run.  Assume it is and set the interface appropriately.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="operation"></param>
        /// <param name="context"></param>
        private ParameterBag SetInterfaceAndTryToRun(TemplateProcessor processor, Operation operation, ParameterBag context)
        {
            // Iff the operation has some initial parameters that can be batched, we should start it and let it populate those parameters
            bool shouldRun = ShouldRunOperationOnSelection(operation, context);
            pnlFollowOnInstructions.Visible = !shouldRun;
            if (!shouldRun)
                return null; // Cannot be run now, as the operation has no initial parameters, so no results

            ParameterBag results = processor.Execute(operation, context, false);
            if (null != results)
                NoteOperation(results, operation);
            return results;
        }

        private void cboOperation_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                // If we're merely setting up the list, we still get events.  However, they're not user-triggered and should be ignored.
                if (settingUpSubOperations)
                    return;

                SDListItem selectedItem = (SDListItem)cboOperation.SelectedItem;
                Operation operation = TemplateFactory.Operations[selectedItem.Operation];
                MaybeRunSelectedOperation(operation);
            }
            catch (Exception ex)
            {
                PuntThroughEventLoop(ex);
            }
        }

        private void MaybeRunSelectedOperation(Operation operation)
        {
            bool shouldRun = ShouldRunOperationOnSelection(operation, knownParameters);
            pnlFollowOnInstructions.Visible = !shouldRun;
            if (InOperation)
            {
                // There's already an operation running; deal with it
                // The new operation may not auto-run, depending on whether it takes input or not.
                if (shouldRun)
                {
                    // This is only ever reached when the combo box is populated and an operation is run from there.
                    // Most of the time, that is a follow-on operation - which means it may need the original operation's data and certainly needs to know that the original operation was run.
                    // Therefore, pass the current parameters up the stack for use as input parameters for the (newly-changed) operation.
                    throw new SelectedOperationChangedException(knownParameters);
                }
                CancelCurrentOperation();
            }
            else
            {
                // No operation, try to run this one!
                if (shouldRun)
                    DoCalculate();
            }
        }

        public bool IsOperationsPanelVisible
        {
            get { return pnlOperations.Visible; }
        }

        private void optGroupsByColumn_CheckedChanged(object sender, EventArgs e)
        {
            SdApplication.SoleInstance.Preferences.SelectGroupsByIdentifier = !optGroupsByColumn.Checked;
            selectingData = false;
        }

        private void optGroupsByIdentifier_CheckedChanged(object sender, EventArgs e)
        {
            SdApplication.SoleInstance.Preferences.SelectGroupsByIdentifier = optGroupsByIdentifier.Checked;
            selectingData = false;
        }

        internal bool SelectingData
        {
            get { return selectingData; }
        }

        private void frmMain_HelpButtonClicked(object sender, CancelEventArgs e)
        {
            try
            {
                SdApplication.SoleInstance.ShowHelp(this);
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Showing help failed due to an internal error", ex, false);
            }
        }

        private void frmMain_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            try
            {
                ShowHelp();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Showing help failed due to an internal error", ex, false);
            }
        }

        private void ShowHelp()
        {
            // Check for hovering over a menu item
            if (lastSeenMenuItemTag is Dictionary<string, string>)
            {
                Dictionary<string, string> tags = (Dictionary<string, string>)lastSeenMenuItemTag;
                string menuTopic;
                if (tags.TryGetValue("help", out menuTopic))
                {
                    SdApplication.SoleInstance.ShowHelp(this, menuTopic);
                    return;
                }
                string operationName;
                if (tags.TryGetValue("operation", out operationName))
                {
                    Operation operation;
                    if (TemplateFactory.Operations.TryGetValue(operationName, out operation))
                    {
                        if (null != operation.HelpContext)
                        {
                            string operationTopic = operation.HelpContext.Url ?? operation.HelpContext.ChmId.ToString();
                            SdApplication.SoleInstance.ShowHelp(this, operationTopic);
                            return;
                        }
                    }
                }
            }
            if (null == ActiveMdiChild)
            {
                SdApplication.SoleInstance.ShowHelp(this);
                return;
            }
            SdApplication.SoleInstance.ActiveWindow.Window.ShowHelp();
        }

        private void contentsAndIndexToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                SdApplication.SoleInstance.ActiveHelpTopic = 0;
                SdApplication.SoleInstance.ShowHelp(this);
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Showing help contents failed due to an internal error", ex, false);
            }
        }

        private void methodSelectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                SdApplication.SoleInstance.ActiveHelpTopic = 1213;
                SdApplication.SoleInstance.ShowHelp(this);
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Showing help failed due to an internal error", ex, false);
            }
        }

        private bool cancelProgressPressed;

        internal void StartProgress(string operationDescription, bool provideProgress)
        {
            ShowPanel(PanelType.Progress, false);
            progressBar.Style = provideProgress ? ProgressBarStyle.Continuous : ProgressBarStyle.Marquee;
            progressBar.Value = 0;
            lblProgress.Text = operationDescription;
            cancelProgressPressed = false;
        }

        internal bool UpdateProgress(double fractionComplete)
        {
            // Prevent overzealous input causing exceptions
            if (ProgressBarStyle.Marquee != progressBar.Style)
            {
                if (fractionComplete < 0)
                    fractionComplete = 0;
                else if (fractionComplete > 1)
                    fractionComplete = 1;
                progressBar.Value = (int)(progressBar.Maximum * fractionComplete);
            }
            Application.DoEvents(); // Force the display to update, and catch any cancellations
            bool retVal = cancelProgressPressed;
            cancelProgressPressed = false;
            return retVal;
        }

        internal void FinishProgress()
        {
            if (pnlProgress.Visible)
            {
                progressBar.Value = 0;
                lblProgress.Text = "";
                ShowPanel(PanelType.Default, false);
            }
        }

        private void tabWindows_MouseDown(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.Button == MouseButtons.Right)
                {
                    lastClickedTabIndex = -1;
                    for (int i = 0; i < tabWindows.TabCount; i++)
                    {
                        Rectangle box = tabWindows.GetTabRect(i);
                        if (box.Contains(e.Location))
                        {
                            lastClickedTabIndex = i;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                EatException(ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="host"></param>
        /// <param name="processor"></param>
        /// <param name="context"></param>
        /// <param name="outstandingParameters"></param>
        /// <returns></returns>
        /// <remarks>The return value may contain key->null for optional blank parameters.  It is up to the caller to handle this.</remarks>
        internal ParameterBag FillAndValidateCombinedParameters(ITemplateHost host, ITemplateProcessor processor, ParameterBag context, IList<Parameter> outstandingParameters)
        {
            try
            {
                DrawingControl.SuspendDrawing(this);
                ParameterBag outputParameters = new ParameterBag();
                StartCombinedParameters();
                bool willDisplayAtLeastOneParameter = false;
                string cancelSkipsParameterString = null;
                bool atLeastOneNonCancel = false;
                foreach (Parameter parameter in outstandingParameters)
                {
                    if (null != parameter.RubricExpression)
                    {
                        string rubric = parameter.Rubric(processor, context);
                        if (null != rubric)
                        {
                            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);

                            Label lbl = new Label
                            {
                                Tag = parameter,
                                Padding = new Padding(0, 3, 0, 3),
                                AutoSize = true,
                                MaximumSize = new Size(500, 500),
                                Text = rubric
                            };
                            tlp.Controls.Add(lbl);
                            tlp.SetColumnSpan(lbl, 2);
                        }
                    }

                    FilledParameter fp;
                    switch (parameter.Type)
                    {
                        case ParameterType.Boolean:
                            fp = PrepareCombinedParameter(processor, (BooleanParameter)parameter, context);
                            break;
                        case ParameterType.ConfidenceInterval:
                            fp = PrepareCombinedParameter(processor, (ConfidenceIntervalParameter)parameter, context);
                            break;
                        case ParameterType.Custom:
                            if (typeof(ChartOptionsParameter) == parameter.GetType())
                                fp = PrepareCombinedParameter((ChartOptionsParameter)parameter);
                            else if (typeof(FillableParameter) == parameter.GetType())
                                fp = PrepareCombinedParameter(host, (FillableParameter)parameter);
                            else
                                throw new ArgumentOutOfRangeException("processor", "Must be a ChartOptionsParameter or FillableParameter if it is a custom parameter");
                            break;
                        case ParameterType.Date:
                            fp = PrepareCombinedParameter(processor, (DateParameter)parameter, context);
                            break;
                        case ParameterType.Double:
                            fp = PrepareCombinedParameter(processor, (DoubleParameter)parameter, context);
                            break;
                        case ParameterType.Double2By2:
                            fp = PrepareCombinedParameter(processor, (Double2By2Parameter)parameter, context);
                            break;
                        case ParameterType.Double2By2ByK:
                            fp = PrepareCombinedParameter(processor, (Double2By2ByKParameter)parameter, context);
                            break;
                        case ParameterType.EditGrid:
                            fp = PrepareCombinedParameter(processor, (EditGridParameter)parameter, context);
                            break;
                        case ParameterType.Grid:
                            fp = PrepareCombinedParameter(processor, (GridParameter)parameter, context);
                            break;
                        case ParameterType.Integer:
                            fp = PrepareCombinedParameter(processor, (IntegerParameter)parameter, context);
                            break;
                        case ParameterType.Option:
                            fp = PrepareCombinedParameter(processor, (OptionParameter)parameter, context);
                            break;
                        case ParameterType.Options:
                            fp = PrepareCombinedParameter(processor, (OptionsParameter)parameter, context);
                            break;
                        case ParameterType.PickFromList:
                            fp = PrepareCombinedParameter(processor, (PickFromListParameter)parameter, context);
                            break;
                        case ParameterType.PickVariables:
                            fp = PrepareCombinedParameter(processor, (PickVariablesParameter)parameter, context);
                            break;
                        case ParameterType.Special:
                            fp = PrepareCombinedParameter(processor, (SpecialParameter)parameter, context);
                            break;
                        case ParameterType.String:
                            fp = PrepareCombinedParameter(processor, (StringParameter)parameter, context);
                            break;
                        default:
                            throw new Exception("parameter.Type: Only Boolean, ConfidenceInterval, Double, Integer parameters may be combined");
                    }
                    willDisplayAtLeastOneParameter |= null == fp;
                    if (null == fp)
                    {
                        // The parameter will be displayed.

                        // Check to see whether this parameter defines a value for skipping.  If so, set it.
                        if (null == parameter.CancelSkipsParameter)
                            atLeastOneNonCancel = true;
                        else
                        {
                            if (null == cancelSkipsParameterString)
                                cancelSkipsParameterString = parameter.CancelSkipsParameter;
                        }
                    }
                    else
                    {
                        // The parameter's been pre-filled; nothing is presented for this one.  Add it to the output.
                        outputParameters.Add(parameter.Name, fp);
                    }
                }
                IList<Parameter> parametersToValidate = new List<Parameter>(outstandingParameters);
                outstandingParameters.Clear();

                MaybeShowVariables(context);

                // Some parameters (notably CI parameters) may be defaulted - none will be shown.  If that's the case, don't show; just default them all!
                DrawingControl.ResumeDrawing(this);
                FillCombinedParameters(host, processor, context, willDisplayAtLeastOneParameter, atLeastOneNonCancel ? null : cancelSkipsParameterString, parametersToValidate, ref outputParameters);
                return outputParameters;
            }
            finally
            {
                // Make absolutely certain we haven't suspended layout on pnlUser and not fixed that.
                pnlUser.ResumeLayout();
                TableLayoutPanel tlp = GetUserInputTable();
                if (null != tlp)
                {
                    tlp.ResumeLayout(true);
                    foreach (Control col in tlp.Controls)
                        col.ResumeLayout();
                }

                // Make absolutely certain a parameter doesn't survive between operations on the confidence interval drop-down
                cboConfidenceInterval.Tag = null;

                // Make absolutely certain the window doesn't lock up and become unable to repaint
                DrawingControl.ResumeDrawing(this);
            }
        }

        /// <summary>
        /// Return the i'th column in the user input table, creating it and any prior columns if necessary.  The columns are created with layout suspended.
        /// </summary>
        /// <param name="column"></param>
        /// <returns></returns>
        private TableLayoutPanel GetUserInputTableForColumn(int column)
        {
            TableLayoutPanel tlp = GetUserInputTable();
            while (tlp.ColumnCount < column)
            {
                TableLayoutPanel colTlp = CreateUserInputColumn();
                colTlp.SuspendLayout();
                tlp.ColumnCount++;
                tlp.Controls.Add(colTlp);
                tlp.SetCellPosition(colTlp, new TableLayoutPanelCellPosition(tlp.ColumnCount - 1, 0));
                tlp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            }
            // Make use of the fact that we always create columns in the order 1..n, so controls[i-1] is the control that was created i'th in sequence and hence the control in column i.
            return (TableLayoutPanel)tlp.Controls[column - 1];
        }

        /// <summary>
        /// We're about to display a data input screen.  If there are any variables that we already know about, allow the user to examine them.  If not, hide the button!
        /// </summary>
        private void MaybeShowVariables(ParameterBag context)
        {
            bool shouldShow = false;
            StringBuilder sb = new StringBuilder();
            IList<Operation> ops = BuildOperationHistory(context);
            foreach (KeyValuePair<string, FilledParameter> pair in context.Pairs)
            {
                if (null != pair.Value && pair.Value.IsInputParameter && pair.Value.IsDataFrame)
                {
                    // Find the parameter corresponding to the key
                    // Go back through the operation list - in the case of follow-ons, the variable is often from a precursor.  Use more recent operations in preference to older ones.
                    string parameterTitle = TryToFindParameterLabel(ops, context, pair.Key);
                    if (null != parameterTitle)
                    {
                        shouldShow = true;
                        sb.AppendLine(parameterTitle);
                        foreach (Variable v in pair.Value.AsDataFrame.Variables)
                        {
                            sb.AppendLine("   " + ((null == v || null == v.Title) ? "(unnamed)" : v.Title));
                        }
                    }
                }
            }
            pnlVariables.Visible = shouldShow;
            string toolTipText = sb.ToString();
            cmdVariables.Tag = (shouldShow ? toolTipText : null);
            tipVariables.SetToolTip(cmdVariables, toolTipText);
        }

        private IList<Operation> BuildOperationHistory(ParameterBag context)
        {
            List<Operation> ops = new List<Operation>();
            if (null != mostRecentOperation)
                ops.Add(mostRecentOperation);
            if (null != context)
            {
                if (context.ContainsKey(OPERATION_MEMORY_NAME))
                {
                    foreach (string operationName in context[OPERATION_MEMORY_NAME].AsStringList)
                    {
                        Operation op;
                        if (TemplateFactory.Operations.TryGetValue(operationName, out op))
                        {
                            if (!ops.Contains(op))
                                ops.Add(op);
                        }
                    }
                }
            }
            return ops;
        }

        private static string TryToFindParameterLabel(IEnumerable<Operation> ops, ParameterBag context, string parameterName)
        {
            foreach (Operation op in ops)
            {
                string parameterTitle = TryToFindParameterLabel(op.Steps, context, parameterName);
                if (null != parameterTitle)
                    return parameterTitle;
            }
            return null;
        }

        private static string TryToFindParameterLabel(IEnumerable<Step> steps, ParameterBag context, string parameterName)
        {
            foreach (Step step in steps)
            {
                // TODO: Handle branches correctly!
                if (step is ParametersStep)
                {
                    foreach (Parameter p in ((ParametersStep)step).Parameters)
                    {
                        if (parameterName.Equals(p.Name))
                        {
                            // This parameter is providing the input
                            if (null != p.Title)
                            {
                                // If there's a title, use it as the title of the parameter
                                return p.Title;
                            }
                            return p.HasPrompt ? p.Prompt(new TemplateProcessor(SdApplication.SoleInstance), context) : null;
                        }
                    }
                }
            }
            // Didn't find the label
            return null;
        }

        private void ClearCombinedParameters()
        {
            if (pnlConfidenceInterval.Visible)
                pnlConfidenceInterval.Visible = false;
            cboConfidenceInterval.Tag = null;
            if (HasUserInputTable())
            {
                Control table = GetUserInputTable();
                pnlUser.Controls.RemoveByKey(USER_INPUT_TABLE_NAME);
                table.Dispose();
            }
            cmdCalculate.Text = "R&un";
            cmdClose.Text = "&Return";
        }

        internal void StartCombinedParameters()
        {
            pnlUser.SuspendLayout();
            ClearCombinedParameters();
            TableLayoutPanel tlp = CreateUserInputTable();
            tlp.SuspendLayout();
            pnlUser.Controls.Add(tlp);
        }

        private static TableLayoutPanel CreateUserInputColumn()
        {
            TableLayoutPanel tlp = new TableLayoutPanel
            {
                ColumnCount = 2,
                GrowStyle = TableLayoutPanelGrowStyle.AddRows,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                AutoSize = true,
                Location = new Point(0, 0),
                Margin = new Padding(6, 0, 6, 6)
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return tlp;
        }

        private TableLayoutPanel CreateUserInputTable()
        {
            TableLayoutPanel tlp = new TableLayoutPanel
            {
                Name = USER_INPUT_TABLE_NAME,
                ColumnCount = 1,
                GrowStyle = TableLayoutPanelGrowStyle.AddColumns,
                Width = pnlUser.Width,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Tag = "TopLevelUserTable",
                Location = new Point(0, 0),
                Margin = new Padding(0, 0, 0, 0)
            };
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Expect to use at least one column if we're being created at all
            TableLayoutPanel col0 = CreateUserInputColumn();
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tlp.Controls.Add(col0);
            tlp.SetCellPosition(col0, new TableLayoutPanelCellPosition(tlp.ColumnCount - 1, 0));

            return tlp;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, BooleanParameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
            CheckBox cb = new CheckBox
            {
                Padding = new Padding(3, 3, 3, 3),
                AutoSize = true,
                Tag = parameter
            };
            AddAppropriateEventHandlersTo(cb);
            if (context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter && context[parameter.Name].IsBoolean)
            {
                cb.Checked = context[parameter.Name].AsBoolean;
            }
            else
            {
                bool? defaultValue = parameter.DefaultValue(processor, context);
                if (defaultValue.HasValue)
                {
                    cb.Checked = defaultValue.Value;
                    context.AddInput(parameter.Name, defaultValue.Value);
                }
                else
                    cb.Checked = false;
            }
            cb.Text = parameter.HasPrompt ? parameter.Prompt(processor, context) : "";
            MaybeAddHelpTip(cb, parameter);
            tlp.Controls.Add(cb);
            tlp.SetColumnSpan(cb, 2);

            return null;
        }

        private static void MaybeAddHelpTip(Control control, Parameter parameter)
        {
            if (null != parameter.Help)
            {
                ToolTip tt = new ToolTip();
                tt.SetToolTip(control, parameter.Help.Text);
            }
        }

        void EnterMovesDown(object sender, KeyPressEventArgs e)
        {
            try
            {
                // Only interested in ENTER - ignore others
                if ('\r' != e.KeyChar)
                    return;

                Control c = (Control)sender;

                Control next = c;
                do
                {
                    if (null == next)
                        break;
                    // Find the next useful control.  A control is useful if a tab would stop on it, and it is not a label or panel (for some reason, the selection logic stops on those even though they are not TabStops), and it is not a combo box (business logic).
                    // To prevent infinite loops, we also check for coming back to the control we tabbed from, and stop if so.
                    next = GetNextControl(next, true);
                    if (next == c)
                        break;
                } while (!IsUsefulControl(next, false));

                // If we've landed on the Calculate, we should calculate.
                if (next == cmdCalculate)
                {
                    // The Calculate button is sometimes visible in place of the OK button, notably when an operation is ready to be executed.  Deal with this by returning, which breaks out of the selection loop and runs the operation.
                    if (inputtingData)
                    {
                        selectingData = false;
                        inputtingData = false;
                        okPressed = true;
                        e.Handled = true;
                        return;
                    }
                    DoCalculate();
                }
                else
                {
                    // next.Focus();
                    // Select the entered text so it is ready to overwrite
                    if (next is TextBox)
                    {
                        next.Select();
                        // TextBox nText = (TextBox)next;
                        // nText.SelectionStart = 0;
                        // nText.SelectionLength = nText.TextLength;
                    }
                }
                e.Handled = true;
            }
            catch (Exception ex)
            {
                PuntThroughEventLoop(ex);
            }
        }

        private FilledParameter PrepareCombinedParameter(ChartOptionsParameter parameter)
        {
            ChartDefinition chartDefinition = parameter.ChartDefinition;
            ChartOptions chartOptions = chartDefinition.ChartOptions;
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
            Control ctl;
            switch (chartOptions.OptionType)
            {
                case ChartOptionType.Bar:
                case ChartOptionType.BoxWhisker:
                case ChartOptionType.Control:
                case ChartOptionType.ErrorBars:
                case ChartOptionType.Forest:
                case ChartOptionType.Histogram:
                case ChartOptionType.Ladder:
                case ChartOptionType.Normal:
                case ChartOptionType.Pyramid:
                case ChartOptionType.ROC:
                case ChartOptionType.ScatterXY:
                case ChartOptionType.Spread:
                case ChartOptionType.Survival:
                    ctl = new ctlChartOptions(chartDefinition);
                    break;
                case ChartOptionType.Agreement:
                case ChartOptionType.Gini:
                case ChartOptionType.LinearRegression:
                    // Do nothing - there are no options to fill
                    return new FilledParameter(true, parameter.ChartDefinition);
                default:
                    throw new ArgumentOutOfRangeException("parameter", chartOptions.OptionType.ToString(), "ChartOptions.OptionType: Don't know how to ask the user for options for the specified chart type");
            }
            // At this point, ctl is always assigned.
            ctl.Tag = parameter;
            tlp.Controls.Add(ctl);
            tlp.SetColumnSpan(ctl, 2);
            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, ConfidenceIntervalParameter parameter, ParameterBag context)
        {
            if (parameter.CanDefault && SdApplication.SoleInstance.Preferences.CanDefaultConfidenceInterval)
                return new FilledParameter(true, SdApplication.SoleInstance.Preferences.DefaultConfidenceInterval);

            // If this is a "standard" CI and the dedicated CI combo isn't in use, use it.  Otherwise, create one in the flow.
            ComboBox cbo;
            bool useSingle = parameter.CanUseStandard && null == cboConfidenceInterval.Tag && parameter.MinimumSuggestedValue == 0.9 && parameter.MaximumSuggestedValue == 0.99;
            if (useSingle)
            {
                cbo = cboConfidenceInterval;
                pnlConfidenceInterval.Visible = true;
            }
            else
            {
                TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
                cbo = new ComboBox { Size = new Size(55, 18), FormattingEnabled = true };
                AddAppropriateEventHandlersTo(cbo);
                tlp.Controls.Add(cbo);
                MaybeAddHelpTip(cbo, parameter);

                Label lbl = new Label
                {
                    Tag = parameter,
                    Padding = new Padding(0, 6, 0, 3),
                    AutoSize = true,
                    Text =
                                        parameter.HasPrompt
                                            ? parameter.Prompt(processor, context)
                                            : "Confidence (%)"
                };
                tlp.Controls.Add(lbl);
                MaybeAddHelpTip(lbl, parameter);
            }

            cbo.Tag = parameter;
            cbo.Items.Clear();
            for (int multiplier = 0; multiplier < 500; multiplier++)
            {
                double suggestedValue = parameter.MinimumSuggestedValue + (multiplier * parameter.SuggestedStep);
                if (suggestedValue > parameter.MaximumSuggestedValue)
                    break;
                cbo.Items.Add((suggestedValue * 100.0).ToString("##0.0"));
            }

            // If there's a specific default CI, force it.  If not, don't overwrite the CI combo's value, so that a user can persist CI values between operations.
            if (context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter && context[parameter.Name].IsDouble)
            {
                cbo.Text = (context[parameter.Name].AsDouble * 100.0).ToString("##0.0");
            }
            else if (0.0 != parameter.DefaultValue)
            {
                cbo.Text = (parameter.DefaultValue * 100.0).ToString("##0.0");
            }
            else
            {
                // Don't force a CI if there's already one set on the singleton
                if (!useSingle || string.IsNullOrEmpty(cboConfidenceInterval.Text))
                {
                    cbo.Text = SdApplication.SoleInstance.Preferences.CanDefaultConfidenceInterval ? (SdApplication.SoleInstance.Preferences.DefaultConfidenceInterval * 100.0).ToString("##0") : "95";
                }
            }

            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, DateParameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
            TextBox txt = new TextBox { Size = new Size(80, 18), Tag = parameter };
            if ((!parameter.ForceDefault) && context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter && context[parameter.Name].IsInt32)
            {
                txt.Text = context[parameter.Name].AsInt32.ToString();
            }
            else
            {
                if (parameter.HasDefaultValue)
                {
                    txt.Text = parameter.DefaultValue(processor, context).ToString("d");
                }
            }
            AddAppropriateEventHandlersTo(txt);
            MaybeAddHelpTip(txt, parameter);
            tlp.Controls.Add(txt);

            Label lbl = new Label
            {
                Tag = parameter,
                Padding = new Padding(0, 6, 0, 3),
                AutoSize = true,
                Text = parameter.HasPrompt ? parameter.Prompt(processor, context) : ""
            };
            tlp.Controls.Add(lbl);
            MaybeAddHelpTip(lbl, parameter);
            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, DoubleParameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
            TextBox txt = new TextBox { Size = new Size(100, 18), Tag = parameter };
            if ((!parameter.ForceDefault) && context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter && context[parameter.Name].IsDouble)
            {
                double defaultValue = context[parameter.Name].AsDouble;
                if ((!double.IsNaN(defaultValue)) && defaultValue != Constant.MISSING)
                    txt.Text = context[parameter.Name].AsDouble.ToString();
            }
            else
            {
                double? defaultValue = parameter.DefaultValue(processor, context);
                string defaultValueString = "";
                if (defaultValue.HasValue && (!double.IsNaN(defaultValue.Value)) && defaultValue.Value != Constant.MISSING)
                    defaultValueString = defaultValue.Value.ToString();
                txt.Text = defaultValueString;
            }
            AddAppropriateEventHandlersTo(txt);

            string suffix = "";
            if (parameter.ShowLimits)
            {
                double minimumValue = parameter.MinimumValue(processor, context);
                double maximumValue = parameter.MaximumValue(processor, context);
                if (minimumValue > double.MinValue)
                {
                    if (maximumValue < double.MaxValue)
                        suffix = " (" + minimumValue.ToString() + " to " + maximumValue.ToString() + ")";
                    else
                        suffix = " (>= " + minimumValue.ToString() + ")";
                }
                else if (maximumValue < double.MaxValue)
                {
                    suffix = " (<= " + maximumValue.ToString() + ")";
                }
            }

            Label lbl = new Label { Tag = parameter, Padding = new Padding(0, 6, 0, 3), AutoSize = true };
            if (parameter.HasPrompt)
                lbl.Text = parameter.Prompt(processor, context) + suffix;
            else
                lbl.Text = suffix;

            MaybeAddHelpTip(lbl, parameter);
            MaybeAddHelpTip(txt, parameter);

            if (parameter.PromptPrecedesParameter)
            {
                tlp.Controls.Add(lbl);
                tlp.Controls.Add(txt);
            }
            else
            {
                tlp.Controls.Add(txt);
                tlp.Controls.Add(lbl);
            }
            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, Double2By2Parameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
            TableLayoutPanel panel2By2 = new TableLayoutPanel { Tag = parameter, RowCount = 4, ColumnCount = 3, AutoSize = true };
            panel2By2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel2By2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel2By2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            Label lblColumnsPrompt = new Label
            {
                Padding = new Padding(3, 3, 3, 3),
                AutoSize = true,
                Text = parameter.ColumnsPrompt
            };
            panel2By2.Controls.Add(lblColumnsPrompt, 0, 0);
            panel2By2.SetColumnSpan(lblColumnsPrompt, 3);

            Label lblLeftColumnPrompt = new Label
            {
                Padding = new Padding(3, 6, 3, 3),
                AutoSize = true,
                Text = parameter.LeftColumnPrompt
            };
            panel2By2.Controls.Add(lblLeftColumnPrompt, 0, 1);

            Label lblRightColumnPrompt = new Label
            {
                Padding = new Padding(3, 6, 3, 3),
                AutoSize = true,
                Text = parameter.RightColumnPrompt
            };
            panel2By2.Controls.Add(lblRightColumnPrompt, 1, 1);

            Label lblRowsPrompt = new Label
            {
                Padding = new Padding(3, 6, 3, 3),
                AutoSize = true,
                Text = parameter.RowsPrompt
            };
            panel2By2.Controls.Add(lblRowsPrompt, 2, 1);

            TextBox txtTL = new TextBox { Name = "txtTL", Size = new Size(100, 18) };
            if (context.ContainsKey(parameter.TopLeftName) && null != context[parameter.TopLeftName] && context[parameter.TopLeftName].IsInputParameter && context[parameter.TopLeftName].IsDouble)
                txtTL.Text = context[parameter.TopLeftName].AsDouble.ToString();
            AddAppropriateEventHandlersTo(txtTL);
            panel2By2.Controls.Add(txtTL, 0, 2);

            TextBox txtTR = new TextBox { Name = "txtTR", Size = new Size(100, 18) };
            if (context.ContainsKey(parameter.TopRightName) && null != context[parameter.TopRightName] && context[parameter.TopRightName].IsInputParameter && context[parameter.TopRightName].IsDouble)
                txtTR.Text = context[parameter.TopRightName].AsDouble.ToString();
            AddAppropriateEventHandlersTo(txtTR);
            panel2By2.Controls.Add(txtTR, 1, 2);

            Label lblTopRowPrompt = new Label
            {
                Padding = new Padding(3, 6, 3, 3),
                AutoSize = true,
                Text = parameter.TopRowPrompt
            };
            panel2By2.Controls.Add(lblTopRowPrompt, 2, 2);

            TextBox txtBL = new TextBox { Name = "txtBL", Size = new Size(100, 18) };
            if (context.ContainsKey(parameter.BottomLeftName) && null != context[parameter.BottomLeftName] && context[parameter.BottomLeftName].IsInputParameter && context[parameter.BottomLeftName].IsDouble)
                txtBL.Text = context[parameter.BottomLeftName].AsDouble.ToString();
            AddAppropriateEventHandlersTo(txtBL);
            panel2By2.Controls.Add(txtBL, 0, 3);

            TextBox txtBR = new TextBox { Name = "txtBR", Size = new Size(100, 18) };
            if (context.ContainsKey(parameter.BottomRightName) && null != context[parameter.BottomRightName] && context[parameter.BottomRightName].IsInputParameter && context[parameter.BottomRightName].IsDouble)
                txtBR.Text = context[parameter.BottomRightName].AsDouble.ToString();
            AddAppropriateEventHandlersTo(txtBR);
            panel2By2.Controls.Add(txtBR, 1, 3);

            Label lblBottomRowPrompt = new Label
            {
                Padding = new Padding(3, 6, 3, 3),
                AutoSize = true,
                Text = parameter.BottomRowPrompt
            };
            panel2By2.Controls.Add(lblBottomRowPrompt, 2, 3);

            tlp.Controls.Add(panel2By2);
            tlp.SetColumnSpan(panel2By2, 2);

            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, Double2By2ByKParameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
            TableLayoutPanel panel2By2ByK = new TableLayoutPanel
            {
                Tag = parameter,
                RowCount = 5,
                ColumnCount = 3,
                AutoSize = true
            };
            panel2By2ByK.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel2By2ByK.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel2By2ByK.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            Label lblColumnsPrompt = new Label
            {
                Padding = new Padding(3, 6, 3, 3),
                AutoSize = true,
                Text = "Characteristic (press F1 for an example)"
            };
            panel2By2ByK.Controls.Add(lblColumnsPrompt, 0, 0);
            panel2By2ByK.SetColumnSpan(lblColumnsPrompt, 3);

            Label lblLeftColumnPrompt = new Label { Padding = new Padding(3, 6, 3, 3), AutoSize = true, Text = "Present" };
            panel2By2ByK.Controls.Add(lblLeftColumnPrompt, 0, 1);

            Label lblRightColumnPrompt = new Label { Padding = new Padding(3, 6, 3, 3), AutoSize = true, Text = "Absent" };
            panel2By2ByK.Controls.Add(lblRightColumnPrompt, 1, 1);

            Label lblRowsPrompt = new Label { Padding = new Padding(3, 6, 3, 3), AutoSize = true, Text = "Outcome:" };
            panel2By2ByK.Controls.Add(lblRowsPrompt, 2, 1);

            TextBox txtTL = new TextBox { Name = "txtTL", Size = new Size(100, 18) };
            AddAppropriateEventHandlersTo(txtTL);
            panel2By2ByK.Controls.Add(txtTL, 0, 2);

            TextBox txtTR = new TextBox { Name = "txtTR", Size = new Size(100, 18) };
            AddAppropriateEventHandlersTo(txtTR);
            panel2By2ByK.Controls.Add(txtTR, 1, 2);

            Label lblTopRowPrompt = new Label { Padding = new Padding(3, 6, 3, 3), AutoSize = true, Text = "Present" };
            panel2By2ByK.Controls.Add(lblTopRowPrompt, 2, 2);

            TextBox txtBL = new TextBox { Name = "txtBL", Size = new Size(100, 18) };
            AddAppropriateEventHandlersTo(txtBL);
            panel2By2ByK.Controls.Add(txtBL, 0, 3);

            TextBox txtBR = new TextBox { Name = "txtBR", Size = new Size(100, 18) };
            AddAppropriateEventHandlersTo(txtBR);
            panel2By2ByK.Controls.Add(txtBR, 1, 3);

            FlowLayoutPanel pnlNavigation = new FlowLayoutPanel { AutoSize = true, Tag = new[] { new List<double>(), new List<double>() } };
            panel2By2ByK.Controls.Add(pnlNavigation, 0, 4);
            panel2By2ByK.SetColumnSpan(pnlNavigation, 3);

            Button cmdPrevious = new Button { Name = "cmdPrevious", Text = "<", Width = 20 };
            cmdPrevious.Click += cmdPrevious_KeyPress;
            cmdPrevious.Enabled = false;
            pnlNavigation.Controls.Add(cmdPrevious);

            Label lblStratum = new Label { Padding = new Padding(3, 9, 3, 3), AutoSize = true, Text = "Stratum 1 of 1" };
            pnlNavigation.Controls.Add(lblStratum);
            lblStratum.Tag = 1;

            Button cmdNext = new Button { Name = "cmdNext", Text = ">", Width = 20 };
            cmdNext.Click += cmdNext_KeyPress;
            pnlNavigation.Controls.Add(cmdNext);

            Label lblBottomRowPrompt = new Label { Padding = new Padding(3, 6, 3, 3), AutoSize = true, Text = "Absent" };
            panel2By2ByK.Controls.Add(lblBottomRowPrompt, 2, 3);

            // Fill in data for stratum 1 if present; set number of strata if present
            if (context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter && context[parameter.Name].IsDataFrame)
            {
                DataFrame sourceFrame = context[parameter.Name].AsDataFrame;
                int tableCount = sourceFrame.MinRows / 2;
                if (sourceFrame.VariableCount == 2 && sourceFrame.Variables[0].IsDoubleVariable && sourceFrame.Variables[1].IsDoubleVariable)
                {
                    DoubleVariable var1 = sourceFrame.Variables[0].AsDoubleVariable;
                    DoubleVariable var2 = sourceFrame.Variables[1].AsDoubleVariable;
                    txtTL.Text = var1.Data[0].ToString();
                    txtTR.Text = var2.Data[0].ToString();
                    txtBL.Text = var1.Data[1].ToString();
                    txtBR.Text = var2.Data[1].ToString();
                    lblStratum.Text = "Stratum 1 of " + tableCount.ToString();
                    // Copy the data for maintenance and use by the controls
                    List<double>[] newData = (List<double>[])pnlNavigation.Tag;
                    newData[0].AddRange(var1.Data);
                    newData[1].AddRange(var2.Data);
                }
            }

            tlp.Controls.Add(panel2By2ByK);
            tlp.SetColumnSpan(panel2By2ByK, 2);

            return null;
        }

        static void cmdPrevious_KeyPress(object sender, EventArgs e)
        {
            try
            {
                // Find our control and get tag data
                Button cmdPrevious = (Button)sender;
                FlowLayoutPanel pnlNavigation = (FlowLayoutPanel)cmdPrevious.Parent;
                Label lblStratum = (Label)pnlNavigation.Controls[1];
                Button cmdNext = (Button)pnlNavigation.Controls[2];
                TableLayoutPanel panel2By2ByK = (TableLayoutPanel)pnlNavigation.Parent;
                TextBox txtTl = (TextBox)panel2By2ByK.GetControlFromPosition(0, 2);
                TextBox txtTr = (TextBox)panel2By2ByK.GetControlFromPosition(1, 2);
                TextBox txtBl = (TextBox)panel2By2ByK.GetControlFromPosition(0, 3);
                TextBox txtBr = (TextBox)panel2By2ByK.GetControlFromPosition(1, 3);

                int stratum = (int)lblStratum.Tag;
                List<double>[] newData = (List<double>[])pnlNavigation.Tag;
                List<double> var1Data = newData[0];
                List<double> var2Data = newData[1];

                // Fill the stored data from the text boxes
                int offset = (stratum - 1) * 2;
                double tl = Parsing.Cdbl_Txt(txtTl.Text);
                double tr = Parsing.Cdbl_Txt(txtTr.Text);
                double bl = Parsing.Cdbl_Txt(txtBl.Text);
                double br = Parsing.Cdbl_Txt(txtBr.Text);
                while (var1Data.Count < stratum * 2)
                {
                    var1Data.Add(Constant.MISSING);
                    var2Data.Add(Constant.MISSING);
                }
                var1Data[offset] = tl;
                var2Data[offset] = tr;
                var1Data[offset + 1] = bl;
                var2Data[offset + 1] = br;

                // #744: Test for all-missing in the end stratum; delete it if so
                int strata = newData[0].Count / 2;
                int lastOffset = (strata - 1) * 2;
                if (var1Data[lastOffset] == Constant.MISSING && var2Data[lastOffset] == Constant.MISSING && var1Data[lastOffset + 1] == Constant.MISSING && var2Data[lastOffset + 1] == Constant.MISSING)
                {
                    --strata;
                    var1Data.RemoveAt(lastOffset + 1);
                    var1Data.RemoveAt(lastOffset);
                    var2Data.RemoveAt(lastOffset + 1);
                    var2Data.RemoveAt(lastOffset);
                }


                if (stratum > 1)
                    --stratum;
                lblStratum.Tag = stratum;

                // Fill the text boxes from the stored data
                offset = (stratum - 1) * 2;
                txtTl.BackColor = SystemColors.Window;
                txtTr.BackColor = SystemColors.Window;
                txtBl.BackColor = SystemColors.Window;
                txtBr.BackColor = SystemColors.Window;
                txtTl.Text = Formatting.XUnrounded(var1Data[offset]);
                txtTr.Text = Formatting.XUnrounded(var2Data[offset]);
                txtBl.Text = Formatting.XUnrounded(var1Data[offset + 1]);
                txtBr.Text = Formatting.XUnrounded(var2Data[offset + 1]);
                lblStratum.Text = "Stratum " + stratum + " of " + strata;

                cmdPrevious.Enabled = stratum > 1;
                cmdNext.Enabled = true;
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't move to previous stratum due to an internal error", ex, false);
            }
        }

        static void cmdNext_KeyPress(object sender, EventArgs e)
        {
            try
            {
                // Find our control and get tag data
                Button cmdNext = (Button)sender;
                FlowLayoutPanel pnlNavigation = (FlowLayoutPanel)cmdNext.Parent;
                Label lblStratum = (Label)pnlNavigation.Controls[1];
                Button cmdPrevious = (Button)pnlNavigation.Controls[0];
                TableLayoutPanel panel2By2ByK = (TableLayoutPanel)pnlNavigation.Parent;
                TextBox txtTL = (TextBox)panel2By2ByK.GetControlFromPosition(0, 2);
                TextBox txtTR = (TextBox)panel2By2ByK.GetControlFromPosition(1, 2);
                TextBox txtBL = (TextBox)panel2By2ByK.GetControlFromPosition(0, 3);
                TextBox txtBR = (TextBox)panel2By2ByK.GetControlFromPosition(1, 3);

                int stratum = (int)lblStratum.Tag;
                List<double>[] newData = (List<double>[])pnlNavigation.Tag;
                List<double> var1Data = newData[0];
                List<double> var2Data = newData[1];

                // Fill the stored data from the text boxes
                int offset = (stratum - 1) * 2;
                int strata = newData[0].Count / 2;
                double tl = Parsing.Cdbl_Txt(txtTL.Text);
                double tr = Parsing.Cdbl_Txt(txtTR.Text);
                double bl = Parsing.Cdbl_Txt(txtBL.Text);
                double br = Parsing.Cdbl_Txt(txtBR.Text);
                while (var1Data.Count < stratum * 2)
                {
                    var1Data.Add(Constant.MISSING);
                    var2Data.Add(Constant.MISSING);
                }
                var1Data[offset] = tl;
                var2Data[offset] = tr;
                var1Data[offset + 1] = bl;
                var2Data[offset + 1] = br;

                stratum++;
                lblStratum.Tag = stratum;

                // Fill the text boxes from the stored data
                offset = (stratum - 1) * 2;
                if (offset < var1Data.Count)
                {
                    txtTL.Text = Formatting.XUnrounded(var1Data[offset]);
                    txtTR.Text = Formatting.XUnrounded(var2Data[offset]);
                    txtBL.Text = Formatting.XUnrounded(var1Data[offset + 1]);
                    txtBR.Text = Formatting.XUnrounded(var2Data[offset + 1]);
                }
                else
                {
                    // New stratum
                    txtTL.Clear();
                    txtTR.Clear();
                    txtBL.Clear();
                    txtBR.Clear();
                }
                lblStratum.Text = "Stratum " + stratum + " of " + (Math.Max(strata, stratum));
                txtTL.BackColor = SystemColors.Window;
                txtTR.BackColor = SystemColors.Window;
                txtBL.BackColor = SystemColors.Window;
                txtBR.BackColor = SystemColors.Window;

                cmdPrevious.Enabled = true;
                cmdNext.Enabled = true; // Can always Next to create another stratum
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't move to next stratum due to an internal error", ex, false);
            }
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, EditGridParameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
            DataGridView gridEditGrid = new DataGridView();
            ((ISupportInitialize)gridEditGrid).BeginInit();
            DataGridViewTextBoxColumn colKey = new DataGridViewTextBoxColumn();
            DataGridViewTextBoxColumn colValue = new DataGridViewTextBoxColumn();
            gridEditGrid.Tag = parameter;
            gridEditGrid.AllowUserToAddRows = false;
            gridEditGrid.AllowUserToDeleteRows = false;
            gridEditGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridEditGrid.ColumnHeadersVisible = false;
            gridEditGrid.Columns.AddRange(new DataGridViewColumn[] { colKey, colValue });
            gridEditGrid.EditMode = DataGridViewEditMode.EditOnEnter;
            gridEditGrid.MultiSelect = false;
            gridEditGrid.Name = "gridEditGrid";
            gridEditGrid.RowHeadersVisible = false;
            gridEditGrid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            gridEditGrid.Size = new Size(250, 48);
            colKey.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            colKey.HeaderText = "Key";
            colKey.Name = "colKey";
            colKey.ReadOnly = true;
            colKey.SortMode = DataGridViewColumnSortMode.NotSortable;
            colKey.Width = 5;
            colValue.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colValue.HeaderText = "Value";
            colValue.Name = "colValue";
            colValue.SortMode = DataGridViewColumnSortMode.NotSortable;
            tlp.Controls.Add(gridEditGrid);
            ((ISupportInitialize)gridEditGrid).EndInit();
            EditGridParameter egp = parameter;
            DataFrame sourceFrame = context[egp.Source].AsDataFrame;
            StringVariable keyVariable = sourceFrame.FindVariable(egp.KeyVariable).AsStringVariable;
            StringVariable valueVariable = sourceFrame.FindVariable(egp.ValueVariable).AsStringVariable;
            gridEditGrid.Rows.Clear();
            for (int i = 0; i < keyVariable.Length; i++)
            {
                gridEditGrid.Rows.Add(keyVariable.Data[i], valueVariable.Data[i]);
            }
            gridEditGrid.Visible = true;

            Label lbl = new Label
            {
                Tag = parameter,
                Padding = new Padding(0, 6, 0, 3),
                AutoSize = true,
                Text = parameter.HasPrompt ? parameter.Prompt(processor, context) : ""
            };
            tlp.Controls.Add(lbl);
            return null;
        }

        private FilledParameter PrepareCombinedParameter(ITemplateHost host, FillableParameter parameter)
        {
            IFillable fillable = parameter.Fillable;
            string fillerToUse = fillable.FillerToUse;
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
            Control ctl;
            switch (fillerToUse)
            {
                case "ChiSquareGoodnessOfFit":
                    ctl = new ctlChiGFOptions((ChiSquareGoodnessOfFitOptions)fillable);
                    break;
                case "ConvertUnits":
                    ctl = new ctlConvertUnits(host);
                    break;
                case "Distribution":
                    ctl = new ctlPDF((DistributionOptions)fillable, host);
                    break;
                case "Dummy":
                    ctl = new ctlDummyOptions((DummyOptions)fillable);
                    break;
                case "Extraction":
                    ExtractionOptions f = (ExtractionOptions)fillable;
                    if (null == f.IdentifiersFrame)
                        ctl = new ctlFindAndReplaceData(f);
                    else
                        ctl = new ctlExtraction(f);
                    break;
                case "GraphicsOptions":
                    ctl = new ctlGraphicsOptions();
                    break;
                case "ROCCutoff":
                    {
                        ROCCutoff rc = (ROCCutoff)fillable;
                        ctl = new ctlROCCutoff(rc.SeriesRecord, rc.Weight, rc.Title);
                        break;
                    }
                case "SortInPlace":
                    // HACK: Break layering completely
                    frmSpreadsheetGear gearForm = (frmSpreadsheetGear)ActiveMdiChild;
                    if (null == gearForm)
                        return null;
                    IRange range = gearForm.workbookView.RangeSelection.Areas[0];
                    ctl = new ctlSort(range, gearForm.workbookView);
                    break;
                case "Scores":
                    ctl = new ctlScores((ScoresOptions)fillable);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("parameter", fillable.FillerToUse, "fillableParameter.Fillable.FillerToUse: Unknown option");
            }
            ctl.Tag = parameter;
            tlp.Controls.Add(ctl);
            tlp.SetColumnSpan(ctl, 2);
            return null;
        }

        public void ToggleFilters()
        {
            if (ActiveMdiChild is IGrid)
                ((frmSpreadsheetGear)ActiveMdiChild).ToggleFilters();
        }

        /// <summary>
        /// If there is a grid presently displayed in the top bar, return it.  Otherwise return null.
        /// </summary>
        /// <returns></returns>
        private WorkbookView FindGridOrNull()
        {
            return FindGridOrNull(GetUserInputTable());
        }

        private WorkbookView FindGridOrNull(Control root)
        {
            foreach (Control child in root.Controls)
            {
                if (child is WorkbookView)
                    return (WorkbookView)child;
                if (child.Controls.Count > 0)
                {
                    WorkbookView found = FindGridOrNull(child);
                    if (null != found)
                        return found;
                }
            }
            // If we get here, there's no grid
            return null;
        }

        /// <summary>
        /// If this is called, we know we're acquiring "screen" data in the dialog area rather than data from a loaded worksheet.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="parameter"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, GridParameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
            try
            {
                new FileIOPermission(PermissionState.Unrestricted).Assert(); // TODO: Can this be refined, or does SSG really need everything?
                WorkbookView grid = new WorkbookView
                {
                    Tag = parameter,
                    Name = "grid",
                    Size = new Size((int)(494 * currentScaleFactor.Width), (int)(305 * currentScaleFactor.Height)),
                    ContextMenuStrip = contextMenuStrip
                };
                grid.ActiveWorkbookSet.GetLock();
                if (context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter && context[parameter.Name].IsDataFrame)
                {
                    IWorksheet sheet = grid.ActiveWorksheet;
                    IRange usedRange = sheet.UsedRange;
                    DataFrame frame = context[parameter.Name].AsDataFrame;
                    for (int col = 0; col < frame.VariableCount; col++)
                    {
                        DoubleVariable v = frame.Variables[col].AsDoubleVariable;
                        for (int row = 0; row < v.Length; row++)
                            usedRange.Cells[row, col].Value = v.Data[row];
                    }
                }
                grid.ActiveWorksheet.WindowInfo.Zoom = 88; // percent
                int maximumColumns = parameter.MaximumColumns(processor, context);
                if (maximumColumns > 0)
                    grid.ActiveWorksheet.Cells[0, maximumColumns, 0, grid.ActiveWorksheet.Cells.ColumnCount - 1].EntireColumn.Hidden = true;
                grid.ActiveWorkbook.WindowInfo.DisplayWorkbookTabs = false;
                grid.ActiveWorkbookSet.ReleaseLock();
                grid.AllowChartExplorer = false;
                grid.AllowRangeExplorer = false;
                grid.AllowShapeExplorer = false;
                grid.AllowWorkbookDesigner = false;
                grid.AllowWorkbookExplorer = false;
                tlp.Controls.Add(grid);
            }
            finally
            {
                CodeAccessPermission.RevertAssert();
            }

            Label lbl = new Label
            {
                Tag = parameter,
                Padding = new Padding(0, 6, 0, 3),
                AutoSize = true,
                Text = parameter.HasPrompt ? parameter.Prompt(processor, context) : ""
            };
            tlp.Controls.Add(lbl);

            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, IntegerParameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
            TextBox txt = new TextBox { Size = new Size(100, 18), Tag = parameter };
            if ((!parameter.ForceDefault) && context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter && context[parameter.Name].IsInt32)
            {
                txt.Text = context[parameter.Name].AsInt32.ToString();
            }
            else
            {
                if (parameter.HasDefaultValue)
                {
                    int? defaultValue = parameter.DefaultValue(processor, context);
                    if (defaultValue.HasValue)
                        txt.Text = defaultValue.Value.ToString();
                }
            }
            AddAppropriateEventHandlersTo(txt);

            string suffix = "";
            if (parameter.ShowLimits)
            {
                int minimumValue = parameter.MinimumValue;
                int maximumValue = parameter.MaximumValue;
                if (minimumValue > int.MinValue)
                {
                    if (maximumValue < int.MaxValue)
                        suffix = " (" + minimumValue.ToString() + " to " + maximumValue.ToString() + ")";
                    else
                        suffix = " (>= " + minimumValue.ToString() + ")";
                }
                else if (maximumValue < int.MaxValue)
                {
                    suffix = " (<= " + maximumValue.ToString() + ")";
                }
            }

            Label lbl = new Label
            {
                Tag = parameter,
                Padding = new Padding(0, 6, 0, 3),
                AutoSize = true,
                Text = parameter.HasPrompt ? parameter.Prompt(processor, context) + suffix : suffix
            };

            MaybeAddHelpTip(lbl, parameter);
            MaybeAddHelpTip(txt, parameter);

            if (parameter.PromptPrecedesParameter)
            {
                tlp.Controls.Add(lbl);
                tlp.Controls.Add(txt);
            }
            else
            {
                tlp.Controls.Add(txt);
                tlp.Controls.Add(lbl);
            }
            return null;
        }

        private static void AutoSizeCombo(ComboBox cbo)
        {
            // There's no way of autosizing a combo... so we do it by hand!
            int width = cbo.DropDownWidth;
            Graphics g = cbo.CreateGraphics();
            Font font = cbo.Font;
            int vertScrollBarWidth = (cbo.Items.Count > cbo.MaxDropDownItems) ? SystemInformation.VerticalScrollBarWidth : 0;

            foreach (object item in cbo.Items)
            {
                string s = item.ToString();
                int newWidth = (int)g.MeasureString(s, font).Width + vertScrollBarWidth;
                if (width < newWidth)
                    width = newWidth;
            }
            cbo.DropDownWidth = width;
            cbo.Size = new Size(width + SystemInformation.VerticalScrollBarWidth, cbo.PreferredHeight); // Surprisingly, it appears the width of the drop-down arrow part of a ComboBox is the same as that of a vertical scrollbar.
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, OptionParameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);

            switch (parameter.OptionFormatType)
            {
                case OptionFormatType.Dropdown:
                    {
                        string defaultValue = null;
                        if (context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter)
                        {
                            defaultValue = context[parameter.Name].AsString;
                        }
                        else
                        {
                            if (null != parameter.DefaultValue)
                                defaultValue = processor.Evaluate(parameter.DefaultValue, context).ToString();
                        }

                        ComboBox cbo = new ComboBox { Tag = parameter, MaximumSize = new Size(250, 21) };
                        OptionOption defaultOption = parameter.Options[0];
                        foreach (OptionOption optionOption in parameter.Options)
                        {
                            cbo.Items.Add(optionOption);
                            if (null != defaultValue)
                                if (optionOption.Value.Equals(defaultValue))
                                    defaultOption = optionOption;
                        }
                        cbo.SelectedIndex = 0;
                        cbo.DropDownStyle = ComboBoxStyle.DropDownList;
                        AddAppropriateEventHandlersTo(cbo);
                        if (null != defaultOption)
                            cbo.SelectedItem = defaultOption;

                        // There's no way of autosizing a combo... so we do it by hand!
                        AutoSizeCombo(cbo);

                        Label lbl = new Label
                        {
                            Tag = parameter,
                            Padding = new Padding(0, 6, 0, 3),
                            AutoSize = true,
                            MaximumSize = new Size(500, 500),
                            Text = parameter.HasPrompt ? parameter.Prompt(processor, context) : ""
                        };

                        if (parameter.PromptPrecedesParameter)
                        {
                            tlp.Controls.Add(lbl);
                            tlp.Controls.Add(cbo);
                            tlp.SetColumn(lbl, 0);
                            tlp.SetColumn(cbo, 1);
                        }
                        else
                        {
                            tlp.Controls.Add(cbo);
                            tlp.Controls.Add(lbl);
                            tlp.SetColumn(cbo, 0);
                            tlp.SetColumn(lbl, 1);
                        }
                    }
                    break;
                case OptionFormatType.Radio:
                    {
                        GroupBox groupBox = null;
                        if (parameter.HasPrompt)
                        {
                            string prompt = parameter.Prompt(processor, context);
                            if (!string.IsNullOrEmpty(prompt))
                            {
                                groupBox = new SDGroupBox
                                {
                                    Tag = parameter,
                                    Padding = new Padding(3, 3, 3, 3),
                                    AutoSize = true,
                                    Text = prompt
                                };
                                // Add later so that autosizing can size the contained controls as well
                            }
                        }

                        TableLayoutPanel panelOptions = new TableLayoutPanel
                        {
                            Tag = parameter,
                            RowCount = (parameter.Options.Count + 1) / 2,
                            ColumnCount = parameter.Columns,
                            AutoSize = true
                        };
                        for (int column = 0; column < parameter.Columns; column++)
                        {
                            panelOptions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                        }

                        string defaultValue = null;
                        if (context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter)
                        {
                            defaultValue = context[parameter.Name].AsString;
                        }
                        else
                        {
                            if (null != parameter.DefaultValue)
                                defaultValue = processor.Evaluate(parameter.DefaultValue, context).ToString();
                        }

                        foreach (OptionOption optionOption in parameter.Options)
                        {
                            RadioButton rad = new RadioButton
                            {
                                AutoSize = true,
                                Text = optionOption.Label,
                                Tag = optionOption.Value,
                                UseVisualStyleBackColor = true
                            };
                            AddAppropriateEventHandlersTo(rad);
                            panelOptions.Controls.Add(rad);
                            if (null != defaultValue)
                                rad.Checked = optionOption.Value.Equals(defaultValue);
                        }

                        if (null == groupBox)
                        {
                            tlp.Controls.Add(panelOptions);
                            tlp.SetColumnSpan(panelOptions, 2);
                        }
                        else
                        {
                            panelOptions.Height = panelOptions.PreferredSize.Height;
                            panelOptions.Location = new Point(7, 20);
                            groupBox.Controls.Add(panelOptions);
                            tlp.Controls.Add(groupBox);
                            tlp.SetColumnSpan(groupBox, 2);
                        }
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException("parameter", parameter.OptionFormatType, "optionParameter.OptionFormatType: Only Dropdown and Radio are known");
            }

            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, OptionsParameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);

            if (parameter.HasPrompt)
            {
                string prompt = parameter.Prompt(processor, context);
                if (!string.IsNullOrEmpty(prompt))
                {
                    Label lbl = new Label
                    {
                        Tag = parameter,
                        Padding = new Padding(0, 6, 0, 3),
                        AutoSize = true,
                        MaximumSize = new Size(500, 500),
                        Text = prompt
                    };
                    tlp.Controls.Add(lbl);
                    tlp.SetColumnSpan(lbl, 2);
                }
            }

            TableLayoutPanel panelOptions = new TableLayoutPanel
            {
                Tag = parameter,
                RowCount = (parameter.Options.Count + 1) / 2,
                ColumnCount = parameter.Columns,
                AutoSize = true
            };
            for (int column = 0; column < parameter.Columns; column++)
            {
                panelOptions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            }

            foreach (OptionsOption optionsOption in parameter.Options)
            {
                bool isChecked = optionsOption.Selected;
                if (context.ContainsKey(optionsOption.Name) && null != context[optionsOption.Name] && context[optionsOption.Name].IsInputParameter)
                    isChecked = context[optionsOption.Name].AsBoolean;

                CheckBox chk = new CheckBox
                {
                    AutoSize = true,
                    Text = optionsOption.Label,
                    Checked = isChecked,
                    Tag = optionsOption,
                    UseVisualStyleBackColor = true
                };
                panelOptions.Controls.Add(chk);
            }

            tlp.Controls.Add(panelOptions);
            tlp.SetColumnSpan(panelOptions, 2);

            return null;
        }

        /// <summary>
        /// Assuming control is used for data entry in the SD3 dialog area, add appropriate handlers to enable global behaviours for such controls.
        /// </summary>
        void AddAppropriateEventHandlersTo(Control control)
        {
            if (control is CheckBox)
                ((CheckBox)control).CheckedChanged += OptionParameter_CheckedChanged;
            if (control is RadioButton)
                ((RadioButton)control).CheckedChanged += OptionParameter_CheckedChanged;
            if (control is ComboBox)
                ((ComboBox)control).SelectedIndexChanged += OptionParameter_CheckedChanged;
            if (control is ComboBox || control is TextBox)
                control.KeyPress += EnterMovesDown;
            if (control is ctlPickAWindow)
                ((ctlPickAWindow)control).InsideKeyPress += EnterMovesDown;
            control.LostFocus += RunChecksAfterLostFocus;
        }

        void RunChecksAfterLostFocus(object sender, EventArgs e)
        {
            // If we're no longer attached to a window, don't run any checks; they're not relevant and we'll be missing our data anyway.
            Control probe = (Control)sender;
            while (null != probe)
            {
                if (probe is Form)
                    break; // It's still attached
                probe = probe.Parent;
            }
            if (null != probe)
                CheckCombinedParameterVisibilityAndMaybeResize((Control)sender);
        }

        void OptionParameter_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                CheckCombinedParameterVisibilityAndMaybeResize((Control)sender);
            }
            catch (Exception ex)
            {
                EatException(ex);
            }
        }

        private void CheckCombinedParameterVisibilityAndMaybeResize(Control sender)
        {
            while (null != sender)
            {
                if ("TopLevelUserTable".Equals(sender.Tag))
                    break;
                sender = sender.Parent;
            }
            if (null != sender)
            {
                ParameterBag ambientParameters = new ParameterBag();
                ParameterBag context = fillCombinedParametersContext;
                ExtractCurrentValues(new TemplateProcessor(SdApplication.SoleInstance), ambientParameters, context, false);
                if (null != context)
                {
                    // Add in ambient parameters; do not overwrite current parameters (which will include key->null for empty optional parameters)
                    foreach (KeyValuePair<string, FilledParameter> pair in context.Pairs)
                    {
                        if (!ambientParameters.ContainsKey(pair.Key))
                            ambientParameters.Add(pair.Key, pair.Value);
                    }
                }
                // Remove empty optional parameters
                List<string> keysToRemove = new List<string>();
                foreach (KeyValuePair<string, FilledParameter> pair in ambientParameters.Pairs)
                    if (null == pair.Value)
                        keysToRemove.Add(pair.Key);
                foreach (string keyToRemove in keysToRemove)
                    ambientParameters.Remove(keyToRemove);

                if (CheckCombinedParameterVisibility(ambientParameters))
                    ResizeContainer(true);
            }
        }

        /// <returns>true if at least one control's visibility was changed (and hence the container might need to resize)</returns>
        private bool CheckCombinedParameterVisibility(ParameterBag ambientParameters)
        {
            TableLayoutPanel tlp = GetUserInputTable();
            TemplateProcessor processor = null;
            bool layoutSuspended = false;
            bool atLeastOneVisibilityChange = false;

            try
            {
                foreach (Control column in tlp.Controls)
                {
                    foreach (Control control in column.Controls)
                    {
                        if (null != control.Tag)
                        {
                            Parameter parameter = (Parameter)control.Tag;
                            if (parameter.HasAcquireIfTrue)
                            {
                                if (null == processor)
                                    processor = new TemplateProcessor(SdApplication.SoleInstance);
                                bool shouldAcquire = parameter.AcquireIfTrue(processor, ambientParameters);
                                if (control.Visible != shouldAcquire)
                                    atLeastOneVisibilityChange = true;
                                if (!layoutSuspended)
                                {
                                    tlp.SuspendLayout();
                                    foreach (Control col in tlp.Controls)
                                        col.SuspendLayout();
                                    layoutSuspended = true;
                                }
                                control.Visible = shouldAcquire;
                            }
                        }
                    }
                }
            }
            finally
            {
                if (layoutSuspended)
                {
                    tlp.ResumeLayout(true);
                    foreach (Control col in tlp.Controls)
                        col.ResumeLayout();
                }
            }
            return atLeastOneVisibilityChange;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, PickVariablesParameter parameter, ParameterBag context)
        {
            DataFrame frame = context[parameter.ParameterName].AsDataFrame;
            int[] initialState = null;
            if (parameter.PreSelectVariables)
            {
                // Set up at least the minimum variables
                int variableCount = Math.Min(frame.VariableCount, parameter.MinimumVariables);
                initialState = new int[variableCount];
                for (int i = 0; i < initialState.Length; i++)
                    initialState[i] = i;
            }
            if (parameter.MinimumVariables < 1)
                throw new ArgumentOutOfRangeException("parameter", parameter.MinimumVariables, "pickVariablesParameter.MinimumVariables: Must obtain values for at least one variable");
            if (parameter.MinimumVariables > parameter.MaximumVariables)
                throw new ArgumentException("minimumVariables must not be larger than maximumVariables");

            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);

            // Label the parameter above it if required
            if (parameter.HasPrompt)
            {
                Label lbl = new Label
                {
                    Tag = parameter,
                    Padding = new Padding(0, 6, 0, 3),
                    AutoSize = true,
                    Text = parameter.Prompt(processor, context)
                };
                tlp.Controls.Add(lbl);
                tlp.SetColumnSpan(lbl, 2);
            }

            TableLayoutPanel holder = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                RowCount = parameter.MaximumVariables,
                Tag = parameter
            };
            for (int v = 0; v < parameter.MaximumVariables; v++)
            {
                ComboBox cbo = new ComboBox { FormattingEnabled = true };
                for (int i = 0; i < frame.VariableCount; i++)
                {
                    string rubric = (null == frame.Variables[i]) ? "" : frame.Variables[i].Title;
                    cbo.Items.Add(rubric);
                }
                if (null != initialState)
                {
                    if (initialState.Length > v)
                        cbo.SelectedIndex = initialState[v];
                }
                AutoSizeCombo(cbo);
                holder.Controls.Add(cbo);
                Label l = new Label
                {
                    Padding = new Padding(3, 6, 3, 3),
                    AutoSize = true,
                    Text = parameter.LabelAs(processor, context, v)
                };
                holder.Controls.Add(l);
            }
            tlp.Controls.Add(holder);
            tlp.SetColumnSpan(holder, 2);

            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, PickFromListParameter parameter, ParameterBag context)
        {
            DataFrame sourceFrame = context[parameter.Source].AsDataFrame;
            string[] values;
            if (sourceFrame.Variables[0].IsStringVariable)
                values = sourceFrame.Variables[0].AsStringVariable.Data;
            else if (sourceFrame.Variables[0].IsClassifierVariable)
                values = sourceFrame.Variables[0].AsClassifierVariable.SortedCategoryNames;
            else
                throw new ArgumentException("A PickFromListParameter can only pick from string or classifier variables");

            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
            if (parameter.AllowMultiple)
            {
                ListBox lstPickFromList = new ListBox
                {
                    Tag = parameter,
                    FormattingEnabled = true,
                    Name = "lstPickFromList",
                    Size = new Size(250, 48)
                };
                foreach (string value in values)
                    lstPickFromList.Items.Add(value);
                lstPickFromList.SelectionMode = parameter.AllowMultiple ? SelectionMode.MultiSimple : SelectionMode.One;
                tlp.Controls.Add(lstPickFromList);
            }
            else
            {
                ComboBox cbo = new ComboBox { Tag = parameter, MaximumSize = new Size(250, 21) };
                if (parameter.IncludeNoneEntry)
                    cbo.Items.Add("(none)");
                foreach (string value in values)
                    cbo.Items.Add(value);
                cbo.SelectedIndex = 0;
                cbo.DropDownStyle = ComboBoxStyle.DropDownList;

                // There's no way of autosizing a combo... so we do it by hand!
                AutoSizeCombo(cbo);
                tlp.Controls.Add(cbo);
            }

            Label lbl = new Label
            {
                Tag = parameter,
                Padding = new Padding(0, 6, 0, 3),
                AutoSize = true,
                Text = parameter.HasPrompt ? parameter.Prompt(processor, context) : ""
            };
            tlp.Controls.Add(lbl);
            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, SpecialParameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);

            if (("chi-2-column".Equals(parameter.SpecialType))
                || ("chi-3-column".Equals(parameter.SpecialType))
                || ("rr-index".Equals(parameter.SpecialType))
                || ("person-time-size".Equals(parameter.SpecialType))
                || ("likelihood".Equals(parameter.SpecialType)))
            {
                bool isLikelihood = "likelihood".Equals(parameter.SpecialType);
                bool isRrIndex = "rr-index".Equals(parameter.SpecialType);
                bool isPersonTimeSize = "person-time-size".Equals(parameter.SpecialType);
                bool has3Columns = "chi-3-column".Equals(parameter.SpecialType) || isPersonTimeSize;

                TableLayoutPanel ssgContainer = new TableLayoutPanel
                {
                    Tag = parameter,
                    RowCount = 2,
                    ColumnCount = 2,
                    AutoSize = true
                };

                Panel colsPanel = new Panel { Padding = new Padding(0, 0, 0, 0), Margin = new Padding(0, 0, 0, 3), Size = new Size(300, 16), AutoSize = true, AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink };
                ssgContainer.Controls.Add(colsPanel, 1, 0);

                if (isLikelihood)
                {
                    VerticalLabel rowsLabel = new VerticalLabel
                    {
                        Text = "Level",
                        AutoSize = true
                    };
                    ssgContainer.Controls.Add(rowsLabel, 0, 1);
                }

                WorkbookView grid = new WorkbookView
                {
                    ContextMenuStrip = contextMenuStrip,
                    Padding = new Padding(0, 0, 0, 0),
                    Margin = new Padding(0, 0, 0, 0)
                };
                grid.GetLock();
                try
                {
                    if (context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter && context[parameter.Name].IsDataFrame)
                    {
                        DataFrame sourceFrame = context[parameter.Name].AsDataFrame;
                        if (sourceFrame.VariableCount >= 2 && sourceFrame.Variables[0].IsDoubleVariable && sourceFrame.Variables[1].IsDoubleVariable)
                        {
                            DumpIntoSsg((IValues)grid.ActiveWorksheet, 0, sourceFrame.Variables[0].AsDoubleVariable);
                            DumpIntoSsg((IValues)grid.ActiveWorksheet, 1, sourceFrame.Variables[1].AsDoubleVariable);
                            if (has3Columns && sourceFrame.VariableCount >= 3 && sourceFrame.Variables[0].IsDoubleVariable)
                                DumpIntoSsg((IValues)grid.ActiveWorksheet, 2, sourceFrame.Variables[2].AsDoubleVariable);
                        }
                    }
                    grid.ActiveWorksheet.WindowInfo.SplitColumns = has3Columns ? 3 : 2;
                    grid.ActiveWorksheet.WindowInfo.FreezePanes = true;
                    grid.ActiveWorksheet.Cells[0, has3Columns ? 3 : 2, 0, grid.ActiveWorksheet.Cells.ColumnCount - 1].EntireColumn.Hidden = true;
                    grid.ActiveWorkbook.WindowInfo.DisplayWorkbookTabs = false;
                    grid.ActiveWorkbook.WindowInfo.DisplayHorizontalScrollBar = false;

                    // Figure out the width of the row header
                    grid.ActiveWorksheet.Cells[0, 0, 0, 0].EntireColumn.ColumnWidth = 3; // characters - used to simulate row header, which defaults to 3 character width until 1,000th row visible
                    double rowHeaderWidthInPoints = grid.ActiveWorksheet.Cells[0, 0, 0, 0].EntireColumn.Width; // Simulated row header

                    // Reset column widths to a more useful number (11 characters) and get their visible width
                    grid.ActiveWorksheet.Cells[0, 0, 0, has3Columns ? 2 : 1].EntireColumn.ColumnWidth = 11; // characters
                    double visibleColumnsWidthInPoints = grid.ActiveWorksheet.Cells[0, 0, 0, has3Columns ? 2 : 1].EntireColumn.Width; // Visible columns excluding row header and scrollbar
                    double oneColumnWidthInPoints = grid.ActiveWorksheet.Cells[0, 0, 0, 0].EntireColumn.Width; // One column

                    // Set the control size
                    double pointsToPixels = 2; // TODO: HACK: Fudge factor.  How do we get this to be saner?
                    int aHair = 3; // Fudge factor: Extra width in pixels for things like scrollbar edges and ensuring that the right-hand end of the last cell is visible
                    int overallWidthInPixels = (int)((rowHeaderWidthInPoints + visibleColumnsWidthInPoints) * pointsToPixels) + SystemInformation.VerticalScrollBarWidth + aHair;
                    grid.Size = new Size((int)(overallWidthInPixels * currentScaleFactor.Width), (int)(400 * currentScaleFactor.Height));
                    int rowHeaderWidthInPixels = (int)(rowHeaderWidthInPoints * pointsToPixels);
                    int oneColumnWidthInPixels = (int)(oneColumnWidthInPoints * pointsToPixels);
                    int fudge = (int)(3 * pointsToPixels); // Offset of labels from nominal column start, in pixels.  Ideally this should closely match SSG's internal offset.

                    Label col1Label = new Label
                    {
                        Text =
                            isPersonTimeSize
                                ? "Index events"
                                : isRrIndex
                                    ? "Reference rate"
                                    : isLikelihood ? "+ feature" : "+ success",
                        AutoSize = true,
                        Location = new Point(rowHeaderWidthInPixels + (0 * oneColumnWidthInPixels) + fudge, 0)
                    };
                    colsPanel.Controls.Add(col1Label);

                    Label col2Label = new Label
                    {
                        Text =
                            (isPersonTimeSize || isRrIndex)
                                ? "Index Person-time"
                                : isLikelihood ? "- feature" : "- failure",
                        AutoSize = true,
                        Location = new Point(rowHeaderWidthInPixels + (1 * oneColumnWidthInPixels) + fudge, 0)
                    };
                    colsPanel.Controls.Add(col2Label);

                    if (has3Columns)
                    {
                        Label col3Label = new Label
                        {
                            Text = isPersonTimeSize ? "Reference size" : "score",
                            AutoSize = true,
                            Location = new Point(rowHeaderWidthInPixels + (2 * oneColumnWidthInPixels) + fudge, 0)
                        };
                        colsPanel.Controls.Add(col3Label);
                    }

                }
                finally
                {
                    grid.ReleaseLock();
                }
                ssgContainer.Controls.Add(grid, 1, 1);

                tlp.Controls.Add(ssgContainer);
                tlp.SetColumnSpan(ssgContainer, 2);

                grid.Focus();

                return null;
            }
            if ("raters-2d".Equals(parameter.SpecialType))
            {
                TableLayoutPanel ssgContainer = new TableLayoutPanel
                {
                    Tag = parameter,
                    RowCount = 2,
                    ColumnCount = 2,
                    AutoSize = true
                };

                Label colsLabel = new Label { Text = "Rater 2", AutoSize = true };
                ssgContainer.Controls.Add(colsLabel, 1, 0);

                VerticalLabel rowsLabel = new VerticalLabel { Text = "Rater 1", AutoSize = true, TabStop = false };
                ssgContainer.Controls.Add(rowsLabel, 0, 1);

                WorkbookView grid = new WorkbookView { Size = new Size((int)(450 * currentScaleFactor.Width), (int)(400 * currentScaleFactor.Height)), ContextMenuStrip = contextMenuStrip };
                grid.GetLock();
                try
                {
                    if (context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter && context[parameter.Name].IsDataFrame)
                    {
                        DataFrame sourceFrame = context[parameter.Name].AsDataFrame;
                        for (int col = 0; col < sourceFrame.VariableCount; col++)
                            if (sourceFrame.Variables[col].IsDoubleVariable)
                                DumpIntoSsg((IValues)grid.ActiveWorksheet, col, sourceFrame.Variables[col].AsDoubleVariable);
                    }
                    grid.ActiveWorksheet.WindowInfo.Zoom = 88; // percent
                    grid.ActiveWorkbook.WindowInfo.DisplayWorkbookTabs = false;
                }
                finally
                {
                    grid.ReleaseLock();
                }
                ssgContainer.Controls.Add(grid, 1, 1);

                tlp.Controls.Add(ssgContainer);
                tlp.SetColumnSpan(ssgContainer, 2);

                grid.Focus();

                return null;
            }
            if ("addedConstant".Equals(parameter.SpecialType))
            {
                double minimumC;
                double suggestedC;
                DataFrame frame = context["data"].AsDataFrame;
                DoubleVariable dv = frame.Variables[0].AsDoubleVariable;
                Sheet.XConstant(dv.Length, 0, dv.Data, out minimumC, out suggestedC);
                context.AddOutput("a_min", minimumC);

                if (minimumC != Constant.MISSING)
                {
                    DoubleParameter dp = new DoubleParameter
                    {
                        Name = parameter.Name,
                        PromptExpression = parameter.PromptExpression,
                        MinimumValueExpression = new Expression(minimumC.ToString()),
                        DefaultValueExpression = new Expression(suggestedC.ToString()),
                        CancelSkipsParameter = "Skip"
                    };
                    return PrepareCombinedParameter(processor, dp, context);
                }
                return null;
            }
            if ("frame".Equals(parameter.SpecialType))
            {
                ctlPickAWindow ctl = new ctlPickAWindow(OutputType.Frame, parameter) { Tag = parameter };
                AddAppropriateEventHandlersTo(ctl);
                tlp.Controls.Add(ctl);
                tlp.SetColumnSpan(ctl, 2);
                return null;
            }
            if ("report".Equals(parameter.SpecialType))
            {
                ctlPickAWindow ctl = new ctlPickAWindow(OutputType.Report, parameter) { Tag = parameter };
                AddAppropriateEventHandlersTo(ctl);
                tlp.Controls.Add(ctl);
                tlp.SetColumnSpan(ctl, 2);
                return null;
            }
            if ("rubric".Equals(parameter.SpecialType))
            {
                Label ctl = new Label
                {
                    AutoSize = true,
                    Tag = parameter,
                    Text = parameter.Prompt(processor, context)
                };
                tlp.Controls.Add(ctl);
                tlp.SetColumnSpan(ctl, 2);
                return null;
            }
            if ("textToNumbers".Equals(parameter.SpecialType))
            {
                ctlTextToNumbers ctl = new ctlTextToNumbers(context) { Tag = parameter };
                tlp.Controls.Add(ctl);
                tlp.SetColumnSpan(ctl, 2);
                return null;
            }
            if ("scores".Equals(parameter.SpecialType))
            {
                ctlScores ctl = new ctlScores(context) { Tag = parameter };
                tlp.Controls.Add(ctl);
                tlp.SetColumnSpan(ctl, 2);
                return null;
            }
            throw new ArgumentOutOfRangeException("parameter", parameter.SpecialType, "parameter.SpecialType: Unknown option");
        }

        static void DumpIntoSsg(IValues values, int column, DoubleVariable variable)
        {
            double[] data = variable.Data;
            if (null != data)
            {
                for (int i = 0; i < data.Length; i++)
                    if (Constant.MISSING == data[i])
                        values.SetText(i, column, Formatting.ASTERISK);
                    else
                        values.SetNumber(i, column, data[i]);
            }
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, StringParameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
            TextBox txt = new TextBox();
            if (parameter.MaxLength <= 0)
                txt.Size = new Size(250, 18);
            else
            {
                // Windows kerns fonts; so we measure two different strings to give us an idea of the kerning.
                float enWidth;
                float enEnWidth;
                using (Graphics tapeMeasure = txt.CreateGraphics())
                {
                    enWidth = tapeMeasure.MeasureString("n", txt.Font).Width;
                    enEnWidth = tapeMeasure.MeasureString("nn", txt.Font).Width;
                }
                txt.MaxLength = parameter.MaxLength;
                txt.Size = new Size(6 + (int)Math.Ceiling(enWidth + ((enEnWidth - enWidth) * (parameter.MaxLength - 1))), 18);
            }
            txt.Tag = parameter;
            if ((!parameter.ForceDefault) && context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter && context[parameter.Name].IsString)
            {
                txt.Text = context[parameter.Name].AsString;
            }
            else
            {
                if (parameter.HasDefaultValue)
                {
                    txt.Text = parameter.DefaultValue(processor, context);
                }
            }
            AddAppropriateEventHandlersTo(txt);

            Label lbl = new Label
            {
                Tag = parameter,
                Padding = new Padding(0, 6, 0, 3),
                AutoSize = true,
                Text = parameter.HasPrompt ? parameter.Prompt(processor, context) : ""
            };

            MaybeAddHelpTip(lbl, parameter);
            MaybeAddHelpTip(txt, parameter);

            if (parameter.PromptPrecedesParameter)
            {
                tlp.Controls.Add(lbl);
                tlp.Controls.Add(txt);
            }
            else
            {
                tlp.Controls.Add(txt);
                tlp.Controls.Add(lbl);
            }
            return null;
        }

        /// <summary>
        /// A control is useful if a tab would stop on it, and it is not a label or panel (for some reason, the selection logic stops on those even though they are not TabStops).
        /// </summary>
        /// <param name="c">The control to be tested</param>
        /// <param name="outputControlsAreUseful"></param>
        /// <returns>true if the control is useful, false if not</returns>
        internal bool IsUsefulControl(Control c, bool outputControlsAreUseful)
        {
            if (c is Label
                || c is TableLayoutPanel
                || c is Panel
                || c is CheckBox
                || c is ComboBox
                || c is RadioButton
                || c is VerticalLabel
                || c is GroupBox)
                return false;

            if (!c.Visible)
                return false;

            if (outputControlsAreUseful)
                return true;

            return !(c.Tag is Parameter && ((Parameter)c.Tag).Type == ParameterType.Special && "report".Equals(((SpecialParameter)c.Tag).SpecialType));
        }

        internal void SelectFirstUsefulControlIn(Control c)
        {
            Control next = c;
            do
            {
                // Find the next useful control.
                // To prevent infinite loops, we also check for coming back to the control we tabbed from, and stop if so.
                next = GetNextControl(next, true);
                if (null == next)
                    break;
                if (next == c)
                    break;
            } while (!IsUsefulControl(next, true));
            if (null != next)
            {
                next.Select();
                next.Focus();
                if (next is DataGridView)
                {
                    SendKeys.Send("{tab}+{tab}");
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="host"></param>
        /// <param name="processor"></param>
        /// <param name="context"></param>
        /// <param name="shouldShow"></param>
        /// <param name="cancelSkipsParameterString"></param>
        /// <param name="parametersToValidate"></param>
        /// <param name="outputParameters"></param>
        /// <remarks>This may return key->null in outputParameters for optional empty parameters.  It is up to the caller to deal with this.</remarks>
        internal void FillCombinedParameters(ITemplateHost host, ITemplateProcessor processor, ParameterBag context, bool shouldShow, string cancelSkipsParameterString, ICollection<Parameter> parametersToValidate, ref ParameterBag outputParameters)
        {
            TableLayoutPanel tlp = GetUserInputTable();
            pnlUser.ResumeLayout();
            tlp.ResumeLayout(true);
            foreach (Control col in tlp.Controls)
                col.ResumeLayout();
            CheckCombinedParameterVisibility(context);

            if (!shouldShow)
            {
                bool allValid = ExtractCurrentValues(processor, outputParameters, context, true);
                if (!allValid)
                {
                    // TODO: How on earth do we get people to set valid parameters when the defaults are invalid and we're not supposed to show them anything?
                }
                ClearCombinedParameters();
                return;
            }

            // If we get here, at least one parameter should be shown.
            try
            {
                fillCombinedParametersContext = context;
                PushPanel(PanelType.Operations, true);
                SelectFirstUsefulControlIn(tlp); // Must be performed once the operations panel is visible, as Select() only selects controls whose parents are all visible.
                using (new DefaultCursor())
                {
                    cmdCalculate.Text = "R&un";
                    if (cancelSkipsParameterString != null)
                        cmdClose.Text = cancelSkipsParameterString;
                    else
                    {
                        // We use cancel if we're in a follow-on operation that requires input (i.e. if pressing the button would lead to the option of closing the whole thing rather than an auto-close)
                        // #573: Always show Cancel
                        // cmdClose.Text = ShouldShowClose() ? "C&ancel" : "C&ancel";
                        cmdClose.Text = "&Return";
                        picArrowAcross.Visible = true;
                    }
                    while (true)
                    {
                        inputtingData = true;
                        okPressed = false;
                        cancelPressed = false;
                        DoNestedEventLoop();

                        // Did we fall out of the loop?
                        if (!(okPressed || cancelPressed))
                        {
                            outputParameters = null;
                            return;
                        }

                        // Did the user cancel?
                        if (cancelPressed)
                        {
                            // Are all the parameters skippable?
                            bool allSkippable = CheckAllParametersSkippable(tlp);
                            outputParameters = allSkippable ? new ParameterBag() : null;
                            return;
                        }
                        bool allValid = ExtractCurrentValues(processor, outputParameters, context, true);
                        if (allValid)
                        {
                            string validationResult = null;
                            foreach (Parameter outstandingParameter in parametersToValidate)
                            {
                                if (null != outstandingParameter.Validators)
                                    foreach (Validator validator in outstandingParameter.Validators)
                                    {
                                        validationResult = TemplateProcessor.Validate(host, validator.ValidatorName, outstandingParameter, outputParameters, outstandingParameter.ValidationFailMessage);
                                        if (null != validationResult)
                                            break;
                                    }
                                if (null != validationResult)
                                    break;
                            }
                            allValid &= (null == validationResult);
                            if (!allValid)
                                SdApplication.SoleInstance.MsgboxX(validationResult, MessageBoxButtons.OK, MessageBoxIcon.Warning, "StatsDirect", false);
                        }
                        if (allValid)
                        {
                            break;
                        }

                        // Otherwise, at least one parameter's invalid and focus should already have been set to it.  Go round again.
                    }
                }
            }
            finally
            {
                fillCombinedParametersContext = null;
                cmdCalculate.Text = "R&un";
                cmdClose.Text = "&Return";
                picArrowAcross.Visible = false;
                PopPanel(true);
            }

            ClearCombinedParameters();
        }

        void DoNestedEventLoop()
        {
            // wait here until user presses OK or Cancel, or does something else suitable
            do
            {
                Application.DoEvents(); // HACK: Force an inner event loop
                Thread.Sleep(5);
            } while (inputtingData);
            if (null != puntedException)
            {
                Exception ex = puntedException;
                puntedException = null;
                throw ex;
            }
        }

        /// <remarks>Note that outputParameters will contain key->null for parameters that are optional and missing.  Callers must be able to deal with this.</remarks>
        /// <returns>true if doValidation is false, true if everything's valid, false if there are any validation errors</returns>
        bool ExtractCurrentValues(ITemplateProcessor processor, ParameterBag outputParameters, ParameterBag context, bool doValidation)
        {
            bool allValid = true;
            Control firstInvalidControl = null;
            TableLayoutPanel tlp = GetUserInputTable();
            if (null != tlp)
            {
                foreach (Control column in tlp.Controls)
                {
                    foreach (Control control in column.Controls)
                    {
                        Control invalidControlOrNull = ExtractCurrentValue(processor, control, outputParameters, context,
                                                                           doValidation);
                        if (null != invalidControlOrNull && null == firstInvalidControl)
                            firstInvalidControl = invalidControlOrNull;
                        allValid &= (null == invalidControlOrNull);
                    }
                }
            }
            // The CI combo may also be in use
            Control iC = ExtractCurrentValue(processor, cboConfidenceInterval, outputParameters, context, doValidation);
            allValid &= null == iC;
            if (null != iC && null == firstInvalidControl)
                firstInvalidControl = iC;
            if (!allValid /* && null != firstInvalidControl - always the case */)
            {
                // Set focus to the first invalid control.
                firstInvalidControl.BackColor = Color.FromArgb(192, 255, 255);
                firstInvalidControl.Focus();
                // System.Media.SystemSounds.Exclamation.Play(); 
            }
            return allValid;
        }

        private TableLayoutPanel GetUserInputTable()
        {
            return (TableLayoutPanel)pnlUser.Controls[USER_INPUT_TABLE_NAME];
        }

        /// <returns>true if no parameters shown or all skippable, false if there are any required parameters</returns>
        bool CheckAllParametersSkippable(TableLayoutPanel tlp)
        {
            bool allSkippable = true;
            foreach (Control control in tlp.Controls)
            {
                bool skippable = CheckParameterSkippable(control);
                allSkippable &= skippable;
            }
            // The CI combo may also be in use
            allSkippable &= CheckParameterSkippable(cboConfidenceInterval);
            return allSkippable;
        }

        /// <returns>true if the parameter is skippable, false if required.</returns>
        static bool CheckParameterSkippable(Control control)
        {
            // We're interested in non-label controls that have been tagged with parameters.
            // Labels are uninteresting as they'll never contain a useful user-entered value.
            if (control.Tag is Parameter && typeof(Label) != control.GetType())
            {
                Parameter parameter = (Parameter)control.Tag;
                return null != parameter.CancelSkipsParameter;
            }

            // Don't care... so it's OK.
            return true;
        }

        /// <returns>null if the parameter is valid (or has no validation or validation is disabled), the control to be selected if the parameter fails validation.</returns>
        static Control ExtractCurrentValue(ITemplateProcessor processor, Control control, ParameterBag outputParameters, ParameterBag context, bool doValidation)
        {
            // We're interested in non-label controls that have been tagged with parameters.
            // Labels are uninteresting as they'll never contain a useful user-entered value.
            if (control.Tag is Parameter && typeof(Label) != control.GetType())
            {
                // Hidden controls should never have their values extracted and are always OK.
                if (!control.Visible)
                    return null;

                Parameter parameter = (Parameter)control.Tag;

                switch (parameter.Type)
                {
                    case ParameterType.Boolean:
                        {
                            CheckBox cb = (CheckBox)control;
                            bool value = cb.Checked;
                            outputParameters[parameter.Name] = new FilledParameter(true, value);
                            return null;
                        }
                    case ParameterType.ConfidenceInterval:
                        {
                            ComboBox cbo = (ComboBox)control;
                            string raw = cbo.Text.Trim();
                            if (doValidation)
                            {
                                control.BackColor = SystemColors.Window;
                                // Missing or zero-length?
                                if (raw.Length == 0)
                                {
                                    // If the parameter should be filled in, this is an error
                                    if (null == parameter.CancelSkipsParameter)
                                        return cbo;
                                    // If the parameter is optional and also missing, note the missing in the output parameter bag.  It is up to the caller to deal with nulls in the output parameter bag.
                                    outputParameters[parameter.Name] = null;
                                    return null;
                                }
                            }
                            double value = Parsing.Cdbl_Txt(raw);
                            // Turn from percentage to fraction
                            if (value != Constant.MISSING)
                                value /= 100.0;
                            // In range?
                            if (value < 0.0 || value > 1.0)
                                return cbo;
                            // If we get here, it's OK.
                            outputParameters[parameter.Name] = new FilledParameter(true, value);
                            return null;
                        }
                    case ParameterType.Custom:
                        {
                            if (control is IOkable)
                            {
                                IOkable okable = (IOkable)control;
                                okable.OkClicked();
                            }
                            else if (control is IFillParameterBag)
                            {
                                return ((IFillParameterBag)control).Fill(outputParameters, true);
                            }
                            else
                                throw new ArgumentOutOfRangeException("control", "Couldn't request a custom parameter to fill itself in");
                        }
                        break;
                    case ParameterType.Date:
                        {
                            TextBox txt = (TextBox)control;
                            string raw = txt.Text.Trim();
                            if (doValidation)
                            {
                                control.BackColor = SystemColors.Window;
                                // Missing or zero-length?
                                if (raw.Length == 0)
                                {
                                    // If the parameter should be filled in, this is an error
                                    if (null == parameter.CancelSkipsParameter)
                                        return txt;
                                    // If the parameter is optional and also missing, note the missing in the output parameter bag.  It is up to the caller to deal with nulls in the output parameter bag.
                                    outputParameters[parameter.Name] = null;
                                    return null;
                                }
                            }
                            DateTime value = Parsing.Cdate_Txt(raw);
                            outputParameters[parameter.Name] = new FilledParameter(true, value);
                            return null;
                        }
                    case ParameterType.Double:
                        {
                            TextBox txt = (TextBox)control;
                            string raw = txt.Text.Trim();
                            bool isMissing = string.IsNullOrWhiteSpace(raw);
                            if (doValidation)
                            {
                                control.BackColor = SystemColors.Window;
                                // Missing or zero-length?
                                if (isMissing)
                                {
                                    // If the parameter should be filled in, this is an error
                                    if (null == parameter.CancelSkipsParameter)
                                        return txt;
                                    // If the parameter is optional and also missing, note the missing in the output parameter bag.  It is up to the caller to deal with nulls in the output parameter bag.
                                    outputParameters[parameter.Name] = null;
                                    return null;
                                }
                            }
                            double value = Parsing.Cdbl_Txt(raw);
                            if (doValidation)
                            {
                                // In range?
                                DoubleParameter dp = (DoubleParameter)parameter;
                                double minimumValue = dp.MinimumValue(processor, context);
                                double maximumValue = dp.MaximumValue(processor, context);
                                if (value < minimumValue || value > maximumValue)
                                    return txt;
                            }
                            // If we get here, it's OK.
                            outputParameters[parameter.Name] = isMissing ? null : new FilledParameter(true, value);
                            return null;
                        }
                    case ParameterType.Double2By2:
                        {
                            Double2By2Parameter parm = (Double2By2Parameter)parameter;
                            TableLayoutPanel panel2By2 = (TableLayoutPanel)control;
                            TextBox txtTL = (TextBox)panel2By2.Controls["txtTL"];
                            TextBox txtTR = (TextBox)panel2By2.Controls["txtTR"];
                            TextBox txtBL = (TextBox)panel2By2.Controls["txtBL"];
                            TextBox txtBR = (TextBox)panel2By2.Controls["txtBR"];

                            double tl = Parsing.Cdbl_Txt(txtTL.Text);
                            double tr = Parsing.Cdbl_Txt(txtTR.Text);
                            double bl = Parsing.Cdbl_Txt(txtBL.Text);
                            double br = Parsing.Cdbl_Txt(txtBR.Text);

                            if (doValidation)
                            {
                                txtTL.BackColor = SystemColors.Window;
                                txtTR.BackColor = SystemColors.Window;
                                txtBL.BackColor = SystemColors.Window;
                                txtBR.BackColor = SystemColors.Window;
                                // Validate
                                if (tl == Constant.MISSING || tl < 0)
                                {
                                    txtTL.SelectAll();
                                    txtTL.Focus();
                                    return txtTL;
                                }
                                if (tr == Constant.MISSING || tr < 0)
                                {
                                    txtTR.SelectAll();
                                    txtTR.Focus();
                                    return txtTR;
                                }
                                if (bl == Constant.MISSING || bl < 0)
                                {
                                    txtBL.SelectAll();
                                    txtBL.Focus();
                                    return txtBL;
                                }
                                if (br == Constant.MISSING || br < 0)
                                {
                                    txtBR.SelectAll();
                                    txtBR.Focus();
                                    return txtBR;
                                }
                            }

                            if (tl != Constant.MISSING)
                                outputParameters[parm.TopLeftName] = new FilledParameter(true, tl);
                            if (tr != Constant.MISSING)
                                outputParameters[parm.TopRightName] = new FilledParameter(true, tr);
                            if (bl != Constant.MISSING)
                                outputParameters[parm.BottomLeftName] = new FilledParameter(true, bl);
                            if (br != Constant.MISSING)
                                outputParameters[parm.BottomRightName] = new FilledParameter(true, br);
                            return null;
                        }
                    case ParameterType.Double2By2ByK:
                        {
                            TableLayoutPanel pnl2By2ByK = (TableLayoutPanel)control;
                            FlowLayoutPanel pnlNavigation = (FlowLayoutPanel)pnl2By2ByK.GetControlFromPosition(0, 4);
                            Button cmdPrevious = (Button)pnlNavigation.Controls[0];
                            Label lblStratum = (Label)pnlNavigation.Controls[1];
                            Button cmdNext = (Button)pnlNavigation.Controls[2];

                            TextBox txtTl = (TextBox)pnl2By2ByK.GetControlFromPosition(0, 2);
                            TextBox txtTr = (TextBox)pnl2By2ByK.GetControlFromPosition(1, 2);
                            TextBox txtBl = (TextBox)pnl2By2ByK.GetControlFromPosition(0, 3);
                            TextBox txtBr = (TextBox)pnl2By2ByK.GetControlFromPosition(1, 3);

                            int stratum = (int)lblStratum.Tag;
                            List<double>[] newData = (List<double>[])pnlNavigation.Tag;
                            List<double> var1Data = newData[0];
                            List<double> var2Data = newData[1];

                            // Fill the stored data from the text boxes
                            int offset = (stratum - 1) * 2;
                            double tl = Parsing.Cdbl_Txt(txtTl.Text);
                            double tr = Parsing.Cdbl_Txt(txtTr.Text);
                            double bl = Parsing.Cdbl_Txt(txtBl.Text);
                            double br = Parsing.Cdbl_Txt(txtBr.Text);

                            if (doValidation)
                            {
                                txtTl.BackColor = SystemColors.Window;
                                txtTr.BackColor = SystemColors.Window;
                                txtBl.BackColor = SystemColors.Window;
                                txtBr.BackColor = SystemColors.Window;
                                // Validate - find the first missing value in the current stratum
                                if (tl == Constant.MISSING || tl < 0)
                                {
                                    txtTl.SelectAll();
                                    txtTl.Focus();
                                    return txtTl;
                                }
                                if (tr == Constant.MISSING || tr < 0)
                                {
                                    txtTr.SelectAll();
                                    txtTr.Focus();
                                    return txtTr;
                                }
                                if (bl == Constant.MISSING || bl < 0)
                                {
                                    txtBl.SelectAll();
                                    txtBl.Focus();
                                    return txtBl;
                                }
                                if (br == Constant.MISSING || br < 0)
                                {
                                    txtBr.SelectAll();
                                    txtBr.Focus();
                                    return txtBr;
                                }
                            }
                            while (var1Data.Count < stratum * 2)
                            {
                                var1Data.Add(Constant.MISSING);
                                var2Data.Add(Constant.MISSING);
                            }
                            var1Data[offset] = tl;
                            var2Data[offset] = tr;
                            var1Data[offset + 1] = bl;
                            var2Data[offset + 1] = br;

                            if (doValidation)
                            {
                                // Validate - find the first missing value in *any* stratum, set up and highlight
                                for (int i = 0; i < var1Data.Count; i++)
                                {
                                    if (var1Data[i] == Constant.MISSING || var2Data[i] == Constant.MISSING)
                                    {
                                        int failedStratum = i / 2; // Deliberately truncate - if i = 3, it's stratum (3/2) = 1.
                                        tl = var1Data[failedStratum * 2];
                                        tr = var2Data[failedStratum * 2];
                                        bl = var1Data[failedStratum * 2 + 1];
                                        br = var2Data[failedStratum * 2 + 1];
                                        txtTl.Text = Formatting.XUnrounded(tl);
                                        txtTr.Text = Formatting.XUnrounded(tr);
                                        txtBl.Text = Formatting.XUnrounded(bl);
                                        txtBr.Text = Formatting.XUnrounded(br);
                                        lblStratum.Text = "Stratum " + (failedStratum + 1) + " of " + (var1Data.Count / 2);
                                        lblStratum.Tag = failedStratum + 1;
                                        cmdPrevious.Enabled = failedStratum > 0;
                                        cmdNext.Enabled = true;
                                        if (tl == Constant.MISSING || tl < 0)
                                        {
                                            txtTl.SelectAll();
                                            txtTl.Focus();
                                            return txtTl;
                                        }
                                        if (tr == Constant.MISSING || tr < 0)
                                        {
                                            txtTr.SelectAll();
                                            txtTr.Focus();
                                            return txtTr;
                                        }
                                        if (bl == Constant.MISSING || bl < 0)
                                        {
                                            txtBl.SelectAll();
                                            txtBl.Focus();
                                            return txtBl;
                                        }
                                        if (br == Constant.MISSING || br < 0)
                                        {
                                            txtBr.SelectAll();
                                            txtBr.Focus();
                                            return txtBr;
                                        }
                                    }
                                }
                            }

                            DataFrame frame = new DataFrame();
                            DoubleVariable var1 = new DoubleVariable(var1Data.ToArray());
                            DoubleVariable var2 = new DoubleVariable(var2Data.ToArray());
                            frame.Variables.Add(var1);
                            frame.Variables.Add(var2);
                            outputParameters[parameter.Name] = new FilledParameter(true, frame);
                            return null;
                        }
                    case ParameterType.EditGrid:
                        {
                            DataGridView gridEditGrid = (DataGridView)control;
                            string[] data = new string[gridEditGrid.Rows.Count];
                            for (int i = 0; i < gridEditGrid.Rows.Count; i++)
                                data[i] = (string)gridEditGrid.Rows[i].Cells[1].Value;
                            StringVariable newValues = new StringVariable(data);
                            EditGridParameter egp = (EditGridParameter)parameter;
                            newValues.Title = egp.ValueVariable;
                            DataFrame oldFrame = context[egp.Source].AsDataFrame;
                            DataFrame newFrame = new DataFrame();
                            foreach (Variable v in oldFrame.Variables)
                                newFrame.Variables.Add(egp.ValueVariable.Equals(v.Title) ? newValues : v);
                            outputParameters[parameter.Name] = new FilledParameter(true, newFrame);
                            return null;
                        }
                    case ParameterType.Grid:
                        {
                            WorkbookView grid = (WorkbookView)control;
                            IWorksheet sheet = grid.ActiveWorksheet;
                            // TODO: Force end edit if one is current
                            grid.ActiveWorkbookSet.GetLock();
                            IRange usedRange = sheet.UsedRange;
                            DataFrame frame = new DataFrame();
                            for (int col = 0; col < usedRange.ColumnCount; col++)
                            {
                                DoubleVariable v = new DoubleVariable(usedRange.RowCount, "");
                                frame.Variables.Add(v);
                                for (int row = 0; row < usedRange.RowCount; row++)
                                {
                                    object rawValue = usedRange.Cells[row, col].Value;
                                    double parsedValue = frmSpreadsheetGear.ToCellValue(rawValue);
                                    v.Data[row] = parsedValue;
                                }
                            }
                            grid.ActiveWorkbookSet.ReleaseLock();

                            if (doValidation)
                            {
                                // TODO: Validate
                            }

                            // If we get here, it's valid.
                            outputParameters[parameter.Name] = new FilledParameter(true, frame);
                            return null;
                        }
                    case ParameterType.Integer:
                        {
                            TextBox txt = (TextBox)control;
                            string raw = txt.Text.Trim();
                            if (doValidation)
                            {
                                txt.BackColor = SystemColors.Window;
                                // Missing or zero-length?
                                if (raw.Length == 0)
                                {
                                    // If the parameter should be filled in, this is an error
                                    if (null == parameter.CancelSkipsParameter)
                                        return txt;
                                    // If the parameter is optional and also missing, note the missing in the output parameter bag.  It is up to the caller to deal with nulls in the output parameter bag.
                                    outputParameters[parameter.Name] = null;
                                    return null;
                                }
                            }
                            int value = Parsing.Cint_Txt(raw);
                            if (doValidation)
                            {
                                // In range?
                                IntegerParameter ip = (IntegerParameter)parameter;
                                if (value < ip.MinimumValue || value > ip.MaximumValue)
                                    return txt;
                            }
                            // If we get here, it's OK.
                            outputParameters[parameter.Name] = new FilledParameter(true, value);
                            return null;
                        }
                    case ParameterType.Option:
                        {
                            OptionParameter optionParameter = (OptionParameter)parameter;
                            switch (optionParameter.OptionFormatType)
                            {
                                case OptionFormatType.Dropdown:
                                    {
                                        ComboBox cbo = (ComboBox)control;
                                        OptionOption selectedOption = (OptionOption)cbo.SelectedItem;
                                        outputParameters[parameter.Name] = new FilledParameter(true, selectedOption.Value);
                                        return null;
                                    }
                                case OptionFormatType.Radio:
                                    {
                                        Control maybeGroup = control;
                                        if (maybeGroup is GroupBox)
                                            maybeGroup = maybeGroup.Controls[0];
                                        TableLayoutPanel optionPanel = (TableLayoutPanel)maybeGroup;
                                        bool atLeastOneChecked = false;
                                        foreach (Control c in optionPanel.Controls)
                                        {
                                            RadioButton rad = (RadioButton)c;
                                            if (rad.Checked)
                                            {
                                                outputParameters[parameter.Name] = new FilledParameter(true, rad.Tag);
                                                atLeastOneChecked = true;
                                                break;
                                            }
                                        }
                                        return ((null != optionParameter.CancelSkipsParameter) || atLeastOneChecked || !doValidation) ? null : optionPanel.Controls[0];
                                    }
                                default:
                                    throw new Exception("optionParameter.OptionFormatType: Only Dropdown and Radio are known");
                            }
                        }
                    case ParameterType.Options:
                        {
                            TableLayoutPanel optionsPanel = (TableLayoutPanel)control;
                            foreach (Control c in optionsPanel.Controls)
                            {
                                CheckBox chk = (CheckBox)c;
                                OptionsOption oo = (OptionsOption)chk.Tag;
                                outputParameters[oo.Name] = new FilledParameter(true, chk.Checked);
                            }
                            return null;
                        }
                    case ParameterType.PickFromList:
                        {
                            PickFromListParameter p = (PickFromListParameter)control.Tag;
                            if (p.AllowMultiple)
                            {
                                int offset = p.IncludeNoneEntry ? 1 : 0;
                                ListBox lstPickFromList = (ListBox)control;
                                bool[] selected = new bool[lstPickFromList.Items.Count - offset];
                                foreach (int i in lstPickFromList.SelectedIndices)
                                    selected[i - offset] = true;
                                outputParameters[parameter.Name] = new FilledParameter(true, selected);
                            }
                            else
                            {
                                int offset = p.IncludeNoneEntry ? 1 : 0;
                                ComboBox cbo = (ComboBox)control;
                                bool[] selected = new bool[cbo.Items.Count - offset];
                                if (cbo.SelectedIndex >= offset)
                                    selected[cbo.SelectedIndex - offset] = true;
                                if ((!p.IncludeNoneEntry) || cbo.SelectedIndex > 0)
                                    outputParameters[parameter.Name] = new FilledParameter(true, selected);
                            }
                            return null;
                        }
                    case ParameterType.PickVariables:
                        {
                            TableLayoutPanel pickPanel = (TableLayoutPanel)control;
                            int variables = pickPanel.RowCount;
                            int[] ary = new int[variables];
                            for (int v = 0; v < variables; v++)
                            {
                                ComboBox cbo = (ComboBox)pickPanel.Controls[2 * v];
                                ary[v] = cbo.SelectedIndex;
                            }
                            outputParameters[parameter.Name] = new FilledParameter(true, ary);
                            return null;
                        }
                    case ParameterType.Special:
                        {
                            SpecialParameter specialParameter = (SpecialParameter)parameter;
                            if ("chi-2-column".Equals(specialParameter.SpecialType)
                                || "likelihood".Equals(specialParameter.SpecialType)
                                || "rr-index".Equals(specialParameter.SpecialType)
                                || "chi-3-column".Equals(specialParameter.SpecialType)
                                || "person-time-size".Equals(specialParameter.SpecialType))
                            {
                                TableLayoutPanel ssgContainer = (TableLayoutPanel)control;
                                WorkbookView grid = (WorkbookView)ssgContainer.GetControlFromPosition(1, 1);
                                IWorksheet worksheet = grid.ActiveWorksheet;
                                grid.GetLock();
                                object value;
                                try
                                {
                                    value = worksheet.UsedRange.Value;
                                    if (null != value && !value.GetType().IsArray)
                                        value = new[,] { { value } };
                                }
                                finally
                                {
                                    grid.ReleaseLock();
                                }

                                if (null != value)
                                {
                                    object[,] ary = (object[,])value;

                                    DataFrame frame = new DataFrame();
                                    for (int col = ary.GetLowerBound(1); col <= ary.GetUpperBound(1); col++)
                                    {
                                        DoubleVariable dv = new DoubleVariable(ary.GetUpperBound(0) - ary.GetLowerBound(0) + 1, "Column " + (col + 1).ToString());
                                        for (int row = ary.GetLowerBound(0); row <= ary.GetUpperBound(0); row++)
                                        {
                                            dv.Data[row] = frmSpreadsheetGear.ToCellValue(ary[row, col]);
                                        }
                                        frame.Variables.Add(dv);
                                    }
                                    outputParameters[parameter.Name] = new FilledParameter(true, frame);
                                }
                                return null;
                            }
                            if ("raters-2d".Equals(specialParameter.SpecialType))
                            {
                                TableLayoutPanel ssgContainer = (TableLayoutPanel)control;
                                WorkbookView grid = (WorkbookView)ssgContainer.GetControlFromPosition(1, 1);
                                IWorksheet worksheet = grid.ActiveWorksheet;
                                grid.GetLock();
                                object value;
                                try
                                {
                                    value = worksheet.UsedRange.Value;
                                    if (null != value && !value.GetType().IsArray)
                                        value = new[,] { { value } };
                                }
                                finally
                                {
                                    grid.ReleaseLock();
                                }
                                if (null != value)
                                {
                                    object[,] ary = (object[,])value;

                                    DataFrame frame = new DataFrame();
                                    for (int col = ary.GetLowerBound(1); col <= ary.GetUpperBound(1); col++)
                                    {
                                        DoubleVariable dv = new DoubleVariable(ary.GetUpperBound(0) - ary.GetLowerBound(0) + 1, "R2(" + col.ToString() + ")");
                                        for (int row = ary.GetLowerBound(0); row <= ary.GetUpperBound(0); row++)
                                        {
                                            dv.Data[row] = frmSpreadsheetGear.ToCellValue(ary[row, col]);
                                        }
                                        frame.Variables.Add(dv);
                                    }
                                    outputParameters[parameter.Name] = new FilledParameter(true, frame);
                                }
                                return null;
                            }
                            if ("report".Equals(specialParameter.SpecialType)
                                || "frame".Equals(specialParameter.SpecialType)
                                || "dummyVariables".Equals(specialParameter.SpecialType)
                                || "scores".Equals(specialParameter.SpecialType)
                                || "textToNumbers".Equals(specialParameter.SpecialType))
                            {
                                IFillParameterBag ifpb = (IFillParameterBag)control;
                                return ifpb.Fill(outputParameters, doValidation);
                            }
                            throw new Exception("specialParameter.SpecialType: Unknown value");
                        }
                    case ParameterType.String:
                        {
                            TextBox txt = (TextBox)control;
                            outputParameters[parameter.Name] = new FilledParameter(true, txt.Text);
                        }
                        break;
                    default:
                        throw new Exception("Unknown parameter type when parsing results");
                }
            }

            // If we get here, nothing about the control was invalid (but it may never have been a useful control at all!)
            return null;
        }

        [Serializable]
        private class SelectedOperationChangedException : NotAnErrorException
        {
            public ParameterBag InputParameters { get; private set; }
            public SelectedOperationChangedException(ParameterBag inputParameters)
            {
                InputParameters = inputParameters;
            }
        }

        private void frmMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                if (SdApplication.HasInstance)
                    SdApplication.SoleInstance.Shutdown();
            }
            catch (Exception ex)
            {
                EatException(ex);
            }

            // HACK: There are occasions when the main window is closed when we're in a DoEvents loop many levels down the stack.  This deals with the problem that the process can stick around.
            Environment.Exit(0);
        }

        private void frmMain_Shown(object sender, EventArgs e)
        {
            try
            {
                EnsureBuiltInMenuItemsCanShowHelp(mnuMain);

                // We're now ready to accept files from other StatsDirect instances.
                IpcListener.StartListening();

                // We may pre-load a document via a FileOpen parameter.  If we don't, show an opening form.
                if (MdiChildren.Length == 0)
                    SdApplication.SoleInstance.ShowOrQueueDialog(new frmOpening(), null);
            }
            catch (Exception ex)
            {
                EatException(ex);
            }
        }

        public void EnsureBuiltInMenuItemsCanShowHelp(MenuStrip menuStrip)
        {
            foreach (ToolStripItem candidate in menuStrip.Items)
            {
                if (candidate.Tag is string && ((string)(candidate.Tag)).StartsWith("#{") && ((string)(candidate.Tag)).Contains("help="))
                {
                    candidate.MouseEnter += menuItem_MouseEnter;
                    candidate.MouseLeave += menuItem_MouseLeave;
                    // candidate.BackColor = Color.PaleGreen;
                }
                if (candidate is ToolStripMenuItem && null != ((ToolStripMenuItem)candidate).DropDown)
                {
                    EnsureBuiltInMenuItemsCanShowHelp(((ToolStripMenuItem)candidate).DropDown);
                }
            }
        }

        private void EnsureBuiltInMenuItemsCanShowHelp(ToolStripDropDown toolStripDropDown)
        {
            foreach (ToolStripItem candidate in toolStripDropDown.Items)
            {
                if (candidate.Tag is string && ((string)(candidate.Tag)).StartsWith("#{") && ((string)(candidate.Tag)).Contains("help="))
                {
                    candidate.MouseEnter += menuItem_MouseEnter;
                    candidate.MouseLeave += menuItem_MouseLeave;
                    // candidate.BackColor = Color.PaleGreen;
                }
                if (candidate is ToolStripMenuItem && null != ((ToolStripMenuItem)candidate).DropDown)
                {
                    EnsureBuiltInMenuItemsCanShowHelp(((ToolStripMenuItem)candidate).DropDown);
                }
            }
        }

        public void UpdateFileList()
        {
            if (null == recentFileEntries)
            {
                recentFileEntries = new List<ToolStripMenuItem>();
            }
            else
            {
                // Remove current entries
                foreach (ToolStripMenuItem item in recentFileEntries)
                {
                    fileToolStripMenuItem.DropDownItems.Remove(item);
                }
                recentFileEntries.Clear();
            }
            IList<string> recentFiles = SdApplication.SoleInstance.RecentFiles;
            for (int i = 0; i < recentFiles.Count; i++)
            {
                string recentFile = recentFiles[i];
                ToolStripMenuItem menuItem = new ToolStripMenuItem
                {
                    DisplayStyle = ToolStripItemDisplayStyle.Text,
                    Size = new Size(167, 22)
                };
                // 167,22 is merely a convenient magic size that came from the VS2005 designer; it may not be "right", but it works.
                string displayedFile = Formatting.ShortPath(recentFile);
                menuItem.Text = (i + 1).ToString() + ". " + displayedFile;
                if (!recentFile.Equals(displayedFile))
                    menuItem.ToolTipText = recentFile;
                menuItem.Tag = "#{path=" + recentFile + "|help=1148}";
                menuItem.MouseEnter += menuItem_MouseEnter;
                menuItem.MouseLeave += menuItem_MouseLeave;
                // menuItem.BackColor = Color.PaleGreen;
                menuItem.Click += RecentFileHandler;
                menuItem.MergeAction = MergeAction.Replace;
                fileToolStripMenuItem.DropDownItems.Insert(fileToolStripMenuItem.DropDownItems.Count - 2, menuItem);
                recentFileEntries.Add(menuItem);
            }
            fileListToolStripSeparator.Visible = recentFiles.Count > 0;
        }

        private void RecentFileHandler(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = (ToolStripMenuItem)sender;
            object tagObject = ToTagObject(menuItem);
            if (!(tagObject is Dictionary<string, string>))
                return;
            Dictionary<string, string> tagDictionary = (Dictionary<string, string>)tagObject;
            string fileName;
            if (!tagDictionary.TryGetValue("path", out fileName))
                return;
            if (null == fileName)
                return;

            OpenFile(fileName, true);
        }

        private List<ToolStripMenuItem> toolsMenuItems;

        /// <summary>
        /// We have reason to believe the tools menu may not be populated (for example at the start of the program) or may not be up to date (for example after the list of tools has been changed by the user).  Set it up.
        /// </summary>
        /// <remarks>Precondition: We are on the UI thread.</remarks>
        public void UpdateToolsMenu()
        {
            // Ensure the menu is blank
            if (null == toolsMenuItems)
            {
                toolsMenuItems = new List<ToolStripMenuItem>();
            }
            else
            {
                // Remove current entries
                foreach (ToolStripMenuItem item in toolsMenuItems)
                    toolsToolStripMenuItem.DropDownItems.Remove(item);
                toolsMenuItems.Clear();
            }

            // Set up the new items
            StringCollection names = Settings.Default.ToolsNames;
            StringCollection paths = Settings.Default.ToolsPrograms;
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                string path = paths[i];
                ToolStripMenuItem menuItem = new ToolStripMenuItem
                {
                    DisplayStyle = ToolStripItemDisplayStyle.Text,
                    Size = new Size(167, 22),
                    Text = name,
                    Tag = "#{help=1156|cmd=" + path + "}"
                };
                // 167,22 is merely a convenient magic size that came from the VS2005 designer; it may not be "right", but it works.
                menuItem.Click += ToolsMenuItemHandler;
                menuItem.MergeAction = MergeAction.Replace;
                toolsToolStripMenuItem.DropDownItems.Insert(toolsToolStripMenuItem.DropDownItems.Count - 3, menuItem);
                toolsMenuItems.Add(menuItem);
            }
            fileListToolStripSeparator.Visible = names.Count > 0;
        }

        private void ToolsMenuItemHandler(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = (ToolStripMenuItem)sender;
            try
            {
                object tagObject = ToTagObject(menuItem);
                if (!(tagObject is Dictionary<string, string>))
                    return;
                Dictionary<string, string> tagDictionary = (Dictionary<string, string>)tagObject;
                string commandLine;
                if (!tagDictionary.TryGetValue("cmd", out commandLine))
                    return;
                if (null == commandLine)
                    return;

                // Split off any arguments
                string arguments = "";
                int firstSpace = commandLine.IndexOf(' ');
                if (firstSpace >= 0)
                {
                    arguments = commandLine.Substring(firstSpace + 1);
                    commandLine = commandLine.Substring(0, firstSpace);
                }
                // Perform any required substitutions
                Assembly mainAssembly = GetType().Assembly;
                string mainFileName = mainAssembly.Location;
                string installPath = Path.GetDirectoryName(mainFileName);
                commandLine = commandLine.Replace("%STATSDIRECT%", installPath);
                Process.Start(commandLine, arguments);
            }
            catch (Win32Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't start " + menuItem.Text, ex, false);
            }
            catch (IOException ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't start " + menuItem.Text, ex, false);
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't start " + menuItem.Text, ex, false);
            }
        }

        private void setupToolsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                SdApplication.SoleInstance.ShowOrQueueDialog(new frmSetupTools(), (f, result) =>
                {
                    UpdateToolsMenu();
                });
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't show tools setup form due to an internal error", ex, false);
            }
        }

        private void aboutsStatsDirectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                SdApplication.SoleInstance.ShowOrQueueDialog(new frmAbout(), null);
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't show About form due to an internal error", ex, false);
            }
        }

        private void openToolStripButton_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFile();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't open file due to an internal error", ex, false);
            }
        }

        private void helpToolStripButton_Click(object sender, EventArgs e)
        {
            try
            {
                ShowHelp();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't show help due to an internal error", ex, false);
            }
        }

        private void cutToolStripButton_Click(object sender, EventArgs e)
        {
            try
            {
                SdApplication.SoleInstance.ActiveWindow.EditCut();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Cut failed due to an internal error", ex, false);
            }
        }

        private void copyToolStripButton_Click(object sender, EventArgs e)
        {
            try
            {
                SdApplication.SoleInstance.ActiveWindow.EditCopy();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Copy failed due to an internal error", ex, false);
            }
        }

        private void pasteToolStripButton_Click(object sender, EventArgs e)
        {
            try
            {
                SdApplication.SoleInstance.ActiveWindow.EditPaste();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Paste failed due to an internal error", ex, false);
            }
        }

        private void printToolStripButton_Click(object sender, EventArgs e)
        {
            try
            {
                SdApplication.SoleInstance.ActiveWindow.Print();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Print failed due to an internal error", ex, false);
            }
        }

        private void cmdHelp_Click(object sender, EventArgs e)
        {
            try
            {
                SdApplication.SoleInstance.ShowCurrentHelp();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Show help failed due to an internal error", ex, false);
            }
        }

        private void cascadeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                LayoutMdi(MdiLayout.Cascade);
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Layout failed due to an internal error", ex, false);
            }
        }

        private void tileHorizontallyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                LayoutMdi(MdiLayout.TileHorizontal);
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Layout failed due to an internal error", ex, false);
            }
        }

        private void tileVerticallyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                LayoutMdi(MdiLayout.TileVertical);
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Layout failed due to an internal error", ex, false);
            }
        }

        private void arrangeIconsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                LayoutMdi(MdiLayout.ArrangeIcons);
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Layout failed due to an internal error", ex, false);
            }
        }

        private void maximiseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (null != ActiveMdiChild)
                    ActiveMdiChild.WindowState = FormWindowState.Maximized;
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Maximise failed due to an internal error", ex, false);
            }
        }

        internal bool InOperation
        {
            get
            {
                // Walk the stack, finding out how many times we're defended against changed operations.  If we are, we must be in one.
                StackTrace trc = new StackTrace();
                // Start at frame 1 (our caller) as we know that we don't have the attribute
                for (int frameOffset = 1; frameOffset < trc.FrameCount; frameOffset++)
                {
                    StackFrame frame = trc.GetFrame(frameOffset);
                    object[] customAttributes = frame.GetMethod().GetCustomAttributes(typeof(CallerHandlesChangedOperationAttribute), true);
                    if (customAttributes.Length > 0)
                        return true;
                }

                // If we get here and haven't seen the attribute, we're not defended against changed operations, so we can't be in an operation.
                return false;
            }
        }

        public bool AppendToolStrip(ToolStrip sourceToolStrip)
        {
            return ToolStripManager.Merge(sourceToolStrip, toolStrip);
        }

        public bool RemoveToolStrip(ToolStrip toolStripToRemove)
        {
            return ToolStripManager.RevertMerge(toolStrip, toolStripToRemove);
        }

#if ALLOW_OPTIONAL_UNMANAGED_CODE
        /// <summary>
        /// Use unmanaged code to activate the specified MDI child window, avoiding flicker.
        /// </summary>
        /// <param name="childToActivate"></param>
        public new void ActivateMdiChild(Form childToActivate)
        {
            try
            {
                if (ActiveMdiChild != childToActivate)
                {
                    MdiClient mdiClient = GetMDIClient();

                    int pos = mdiClient.Controls.IndexOf(childToActivate);
                    if (pos < 0)
                        throw new InvalidOperationException("MDIChild form not found");
                    Control form = pos == 0 ? mdiClient.Controls[1] : mdiClient.Controls[pos - 1];


                    // flag indicating whether to activate previous or next MDIChild
                    IntPtr direction = new IntPtr(pos == 0 ? 1 : 0);
                    SendMessage(mdiClient.Handle, WM_MDINEXT, form.Handle, direction);
                }
            }
            catch (Exception)
            {
                // Deliberately do nothing.  The child has not activated; the user will have to try again.
                // This happens unexpectedly - I *think* the Controls collection may be updated on another thread while this code is running.
            }
        }

        public MdiClient GetMDIClient()
        {
            foreach (Control c in Controls)
            {
                if (c is MdiClient)
                    return (MdiClient)c;
            }
            throw new InvalidOperationException("No MDIClient !!!");
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg,
                                    IntPtr wParam, IntPtr lParam);

        public const int WM_MDINEXT = 0x224;
#endif

        private void cmdSelectionHelp_Click(object sender, EventArgs e)
        {
            try
            {
                SdApplication.SoleInstance.ShowCurrentHelp();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Show help failed due to an internal error", ex, false);
            }
        }

        private void cmdCancelProgress_Click(object sender, EventArgs e)
        {
            cancelProgressPressed = true;
        }

        private void newToolStripButton_Click(object sender, EventArgs e)
        {
            try
            {
                CreateNewInstanceOfCurrentWindow();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Create new window failed due to an internal error", ex, false);
            }
        }

        private void CreateNewInstanceOfCurrentWindow()
        {
            if (null == SdApplication.SoleInstance.ActiveWindow
                || null == SdApplication.SoleInstance.ActiveWindow.Window)
            {
                CreateGrid();
            }
            else
            {
                StatsDirectForm activeWindow = SdApplication.SoleInstance.ActiveWindow.Window;
                if (activeWindow is IGrid)
                    CreateGrid();
                else if (activeWindow is IReport)
                    CreateReport();
                else if (activeWindow is IScriptWindow)
                    CreateScriptWindow();
            }
        }

        internal IReport SelectedReportWindow
        {
            get
            {
                ComboFormAdapter selectedReport = (ComboFormAdapter)cboActiveReport.SelectedItem;
                if (null == selectedReport)
                    return (IReport)CreateReport();
                IReport rpt = (IReport)selectedReport.StatsDirectForm;
                if (null == rpt)
                {
                    return (IReport)CreateReport();
                }
                return rpt;
            }
        }

        private void saveContextMenuToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                WindowInformation lastClickedTab = TabStripLastClickedTab();
                if (null != lastClickedTab)
                {
                    lastClickedTab.Window.SaveContents();
                }
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Save failed due to an internal error", ex, false);
            }
        }

        private void saveAsContextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                WindowInformation lastClickedTab = TabStripLastClickedTab();
                if (null != lastClickedTab)
                {
                    lastClickedTab.Window.SaveAsContents();
                }
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Save failed due to an internal error", ex, false);
            }
        }

        private void printToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            try
            {
                WindowInformation lastClickedTab = TabStripLastClickedTab();
                if (null != lastClickedTab)
                {
                    lastClickedTab.Window.Print();
                }
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Print failed due to an internal error", ex, false);
            }
        }

        private void renameContextMenuToolStripTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                if ('\n' == e.KeyChar || '\r' == e.KeyChar)
                {
                    string newName = renameContextMenuToolStripTextBox.Text.Trim();
                    if (!string.IsNullOrEmpty(newName))
                    {
                        WindowInformation lastClickedTab = TabStripLastClickedTab();
                        if (null != lastClickedTab)
                        {
                            lastClickedTab.FriendlyName = newName;

                            // If it's a report, we might need to add it with its new name
                            if (lastClickedTab.HasWindow && (lastClickedTab.Window is IReport))
                            {
                                // Force the update - the list only notices a name change when we remove and re-add.
                                StatsDirectForm f = lastClickedTab.Window;
                                int index = cboActiveReport.Items.IndexOf(new ComboFormAdapter(f));
                                cboActiveReport.Items[index] = new ComboFormAdapter(f);
                            }
                        }
                    }

                    e.Handled = true;

                    tabContextMenuStrip.Close(ToolStripDropDownCloseReason.Keyboard);
                }
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Rename failed due to an internal error", ex, false);
            }
        }

        private void renameContextMenutoolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                renameContextMenuToolStripTextBox.Focus();
            }
            catch (Exception ex)
            {
                EatException(ex);
            }
        }

        private void tabContextMenuStrip_Opening(object sender, CancelEventArgs e)
        {
            try
            {
                WindowInformation lastClickedTab = TabStripLastClickedTab();
                if (null != lastClickedTab)
                {
                    bool isNew = lastClickedTab.IsNew;
                    renameContextMenuToolStripTextBox.Enabled = isNew;
                    renameContextMenutoolStripMenuItem.Enabled = isNew;
                    if (isNew)
                    {
                        renameContextMenuToolStripTextBox.Text = lastClickedTab.FriendlyName;
                    }

                }
            }
            catch (Exception ex)
            {
                EatException(ex);
            }
        }

        /// <summary>
        /// Activate the most recently selected (via tab) window.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks>Required because tabs select themselves on the way out of the event handling, after we have the opportunity to select anything else.</remarks>
        private void postTabTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                postTabTimer.Enabled = false;
                if (null != mostRecentlySelectedWindow)
                {
                    ActivateMdiChild(mostRecentlySelectedWindow);
                    mostRecentlySelectedWindow = null;
                }
            }
            catch (Exception ex)
            {
                EatException(ex);
            }
        }

        /// <summary>
        /// An exception has occurred that we don't want to present to the user.  Silently discard it.  A future implementation might log it for later debug purposes.
        /// </summary>
        private void EatException(Exception ex)
        {
        }

        private void cutContextMenuItem1_Click(object sender, EventArgs e)
        {
            try
            {
                WorkbookView workbookView = FindGridOrNull();
                if (null == workbookView)
                    return;
                workbookView.Cut();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Cut failed due to an internal error", ex, false);
            }
        }

        private void copyContextMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                WorkbookView workbookView = FindGridOrNull();
                if (null == workbookView)
                    return;
                workbookView.Copy();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Copy failed due to an internal error", ex, false);
            }
        }

        private void pasteContextMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                WorkbookView workbookView = FindGridOrNull();
                if (null == workbookView)
                    return;
                workbookView.Paste();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Paste failed due to an internal error", ex, false);
            }
        }

        private void pasteSpecialContextMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                WorkbookView workbookView = FindGridOrNull();
                if (null == workbookView)
                    return;
                using (frmPasteSpecial frm = new frmPasteSpecial())
                {
                    frm.ShowDialog(this);
                    if (!frm.UserCancelled)
                        workbookView.PasteSpecial(frm.PasteType, PasteOperation.None, false, false);
                }
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Paste Special due to an internal error", ex, false);
            }
        }

        private void insertContextMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                InsertCells();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Insert Cells failed due to an internal error", ex, false);
            }
        }

        private void InsertCells()
        {
            WorkbookView workbookView = FindGridOrNull();
            if (null == workbookView)
                return;
            using (frmInsertCells frm = new frmInsertCells())
            {
                frm.ShowDialog(this);
                if (!frm.UserCancelled)
                {
                    workbookView.GetLock();
                    try
                    {
                        if (frm.IsEntire)
                        {
                            if (InsertShiftDirection.Right == frm.InsertShiftDirection)
                            {
                                workbookView.RangeSelection.EntireColumn.Insert();
                            }
                            else
                            {
                                workbookView.RangeSelection.EntireRow.Insert();
                            }
                        }
                        else
                        {
                            workbookView.RangeSelection.Insert(frm.InsertShiftDirection);
                        }
                    }
                    finally
                    {
                        workbookView.ReleaseLock();
                    }
                }
            }
        }

        private void deleteContextMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                DeleteSpecial();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Delete cells failed due to an internal error", ex, false);
            }
        }

        private void DeleteSpecial()
        {
            WorkbookView workbookView = FindGridOrNull();
            if (null == workbookView)
                return;
            using (frmDeleteSpecial frm = new frmDeleteSpecial())
            {
                frm.ShowDialog(this);
                if (!frm.UserCancelled)
                {
                    workbookView.GetLock();
                    try
                    {
                        if (frm.IsEntire)
                        {
                            if (DeleteShiftDirection.Left == frm.DeleteShiftDirection)
                            {
                                workbookView.RangeSelection.EntireColumn.Delete();
                            }
                            else
                            {
                                workbookView.RangeSelection.EntireRow.Delete();
                            }
                        }
                        else
                        {
                            workbookView.RangeSelection.Delete(frm.DeleteShiftDirection);
                        }
                    }
                    finally
                    {
                        workbookView.ReleaseLock();
                    }
                }
            }

        }

        private void clearContentsContextMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                WorkbookView workbookView = FindGridOrNull();
                if (null == workbookView)
                    return;
                workbookView.Focus();
                SendKeys.Send("{DEL}");
                Application.DoEvents(); // Force processing of events, in this case clearing the selection
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Clear Contents failed due to an internal error", ex, false);
            }
        }

        private void goToContextMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                GoToCell();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Go to cell failed due to an internal error", ex, false);
            }
        }

        private void GoToCell()
        {
            WorkbookView workbookView = FindGridOrNull();
            if (null == workbookView)
                return;
            try
            {
                string cell = SdApplication.SoleInstance.GetString("Enter the cell address, for example G54", "Go to cell", "");
                workbookView.GetLock();
                if (null != cell)
                {
                    workbookView.ActiveWorksheet.Cells[cell].Activate();
                }
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.Warning(ex.Message, "Go to cell");
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        private void findAndReplaceContextMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                WorkbookView workbookView = FindGridOrNull();
                if (null == workbookView)
                    return;
                // TODO: Fix this rather nasty workaround once SpreadsheetGear has API support for its replace dialog
                workbookView.Focus();
                SendKeys.Send("^h");
                Application.DoEvents(); // Force processing of events, in this case showing the replace dialog
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Showing the find+replace dialog failed due to an internal error", ex, false);
            }
        }

        private void checkForUpdatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                SdApplication.SoleInstance.CheckForUpdates();
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Checking for updates failed due to an internal error", ex, false);
            }
        }

        private int mostRecentModalMessageButtonPressed;
        private bool waitingForModalMessage;

        internal DialogResult ShowModalMessage(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, string helpFile, HelpNavigator helpNavigator, string helpTopic)
        {
            // Sometimes a multiple click results in a second call before the dialog is fully set up.  If so, ignore the multiple clicks.  TODO: Speed up the presentation of the dialog box to reduce the chance of this happening.
            if (waitingForModalMessage)
                return DialogResult.Cancel;

            IButtonControl oldAcceptButton = AcceptButton;
            IButtonControl oldCancelButton = CancelButton;
            lblModalMessageText.Text = text;
            Bitmap rawIcon = IconFromMessageBoxIcon(icon);
            if (null != rawIcon)
                picModalMessageIcon.Image = rawIcon;
            SetupModalButtons(buttons, defaultButton);
            PushPanel(PanelType.ModalMessage, false);
            try
            {
                WaitForModalMessage();
                return DecodeModalButtons(buttons);
            }
            finally
            {
                AcceptButton = oldAcceptButton;
                CancelButton = oldCancelButton;
                PopPanel(true);
            }
        }

        private void SetupModalButtons(MessageBoxButtons buttons, MessageBoxDefaultButton defaultButton)
        {
            switch (buttons)
            {
                case MessageBoxButtons.AbortRetryIgnore:
                    cmdModalMessage1.Visible = true;
                    cmdModalMessage2.Visible = true;
                    cmdModalMessage3.Visible = true;
                    cmdModalMessage1.Text = "Abort";
                    cmdModalMessage2.Text = "Retry";
                    cmdModalMessage3.Text = "Ignore";
                    break;
                case MessageBoxButtons.OK:
                    cmdModalMessage1.Visible = true;
                    cmdModalMessage2.Visible = false;
                    cmdModalMessage3.Visible = false;
                    cmdModalMessage1.Text = "OK";
                    break;
                case MessageBoxButtons.OKCancel:
                    cmdModalMessage1.Visible = true;
                    cmdModalMessage2.Visible = true;
                    cmdModalMessage3.Visible = false;
                    cmdModalMessage1.Text = "OK";
                    cmdModalMessage2.Text = "Cancel";
                    CancelButton = cmdModalMessage2;
                    break;
                case MessageBoxButtons.RetryCancel:
                    cmdModalMessage1.Visible = true;
                    cmdModalMessage2.Visible = true;
                    cmdModalMessage3.Visible = false;
                    cmdModalMessage1.Text = "Retry";
                    cmdModalMessage2.Text = "Cancel";
                    CancelButton = cmdModalMessage2;
                    break;
                case MessageBoxButtons.YesNo:
                    cmdModalMessage1.Visible = true;
                    cmdModalMessage2.Visible = true;
                    cmdModalMessage3.Visible = false;
                    cmdModalMessage1.Text = "Yes";
                    cmdModalMessage2.Text = "No";
                    break;
                case MessageBoxButtons.YesNoCancel:
                    cmdModalMessage1.Visible = true;
                    cmdModalMessage2.Visible = true;
                    cmdModalMessage3.Visible = true;
                    cmdModalMessage1.Text = "Yes";
                    cmdModalMessage2.Text = "No";
                    cmdModalMessage3.Text = "Cancel";
                    CancelButton = cmdModalMessage3;
                    break;
                default:
                    throw new NotImplementedException();
            }

            Button db = null;
            switch (defaultButton)
            {
                case MessageBoxDefaultButton.Button1:
                    db = cmdModalMessage1;
                    break;
                case MessageBoxDefaultButton.Button2:
                    db = cmdModalMessage2;
                    break;
                case MessageBoxDefaultButton.Button3:
                    db = cmdModalMessage3;
                    break;
            }
            if (null != db)
            {
                AcceptButton = db;
                db.Focus();
            }
        }

        private DialogResult DecodeModalButtons(MessageBoxButtons buttons)
        {
            switch (buttons)
            {
                case MessageBoxButtons.AbortRetryIgnore:
                    switch (mostRecentModalMessageButtonPressed)
                    {
                        case 1:
                            return DialogResult.Abort;
                        case 2:
                            return DialogResult.Retry;
                        case 3:
                            return DialogResult.Ignore;
                        default:
                            return DialogResult.None;
                    }
                case MessageBoxButtons.OK:
                    switch (mostRecentModalMessageButtonPressed)
                    {
                        case 1:
                            return DialogResult.OK;
                        default:
                            return DialogResult.None;
                    }
                case MessageBoxButtons.OKCancel:
                    switch (mostRecentModalMessageButtonPressed)
                    {
                        case 1:
                            return DialogResult.OK;
                        case 2:
                            return DialogResult.Cancel;
                        default:
                            return DialogResult.None;
                    }
                case MessageBoxButtons.RetryCancel:
                    switch (mostRecentModalMessageButtonPressed)
                    {
                        case 1:
                            return DialogResult.Retry;
                        case 2:
                            return DialogResult.Cancel;
                        default:
                            return DialogResult.None;
                    }
                case MessageBoxButtons.YesNo:
                    switch (mostRecentModalMessageButtonPressed)
                    {
                        case 1:
                            return DialogResult.Yes;
                        case 2:
                            return DialogResult.No;
                        default:
                            return DialogResult.None;
                    }
                case MessageBoxButtons.YesNoCancel:
                    switch (mostRecentModalMessageButtonPressed)
                    {
                        case 1:
                            return DialogResult.Yes;
                        case 2:
                            return DialogResult.No;
                        case 3:
                            return DialogResult.Cancel;
                        default:
                            return DialogResult.None;
                    }
                default:
                    return DialogResult.None;
            }
        }

        private void WaitForModalMessage()
        {
            // wait here until user presses one of the modal dialog buttons or an equivalent key
            waitingForModalMessage = true;
            mnuMain.Enabled = false;
            // Disabling a form appears to pop any enabled form over the top of it.  Therefore, disable the active form last.  See #681.
            Form activeForm = ActiveMdiChild;
            foreach (Form f in MdiChildren)
                if (f != activeForm)
                    f.Enabled = false;
            if (null != activeForm)
                activeForm.Enabled = false;
            do
            {
                Application.DoEvents(); // HACK: Force an inner event loop
                Thread.Sleep(5);
            } while (waitingForModalMessage);

            // If we still have a main form and menus (the user might have done strange things like close the window), re-enable them.
            if (null != mnuMain)
                mnuMain.Enabled = true;
            if (null != MdiChildren)
            {
                foreach (Form f in MdiChildren)
                    f.Enabled = true;
            }
            if (null != puntedException)
            {
                Exception ex = puntedException;
                puntedException = null;
                throw ex;
            }
        }

        private void cmdModalMessage1_Click(object sender, EventArgs e)
        {
            mostRecentModalMessageButtonPressed = 1;
            waitingForModalMessage = false;
        }

        private void cmdModalMessage2_Click(object sender, EventArgs e)
        {
            mostRecentModalMessageButtonPressed = 2;
            waitingForModalMessage = false;
        }

        private void cmdModalMessage3_Click(object sender, EventArgs e)
        {
            mostRecentModalMessageButtonPressed = 3;
            waitingForModalMessage = false;
        }

        private static Bitmap IconFromMessageBoxIcon(MessageBoxIcon icon)
        {
            Icon rawIcon;
            switch (icon)
            {
                case MessageBoxIcon.Asterisk:
                    // case MessageBoxIcon.Information:
                    rawIcon = SystemIcons.Asterisk;
                    break;
                case MessageBoxIcon.Error:
                    // case MessageBoxIcon.Hand:
                    // case MessageBoxIcon.Stop:
                    rawIcon = SystemIcons.Error;
                    break;
                case MessageBoxIcon.Exclamation:
                    // case MessageBoxIcon.Warning:
                    rawIcon = SystemIcons.Exclamation;
                    break;
                case MessageBoxIcon.None:
                    rawIcon = null;
                    break;
                case MessageBoxIcon.Question:
                    rawIcon = SystemIcons.Question;
                    break;
                default:
                    rawIcon = null;
                    break;
            }
            if (null == rawIcon)
                return null;
            Icon sizedIcon = new Icon(rawIcon, 40, 40);
            Bitmap bmp = new Bitmap(sizedIcon.Width, sizedIcon.Height);
            Graphics gxMem = Graphics.FromImage(bmp);
            gxMem.DrawIcon(sizedIcon, 0, 0);
            gxMem.Dispose();
            return bmp;
        }

        private void cmdVariables_Click(object sender, EventArgs e)
        {
            const int durationMilliseconds = 10000;
            tipVariables.Show(tipVariables.GetToolTip(cmdVariables), cmdVariables, durationMilliseconds);
        }

        private void cmdModalMessageKeyPress(object sender, KeyPressEventArgs e)
        {
            switch (e.KeyChar)
            {
                case 'y':
                case 'Y':
                    e.Handled = FindAndFakeModalButtonPress("Yes");
                    break;
                case 'n':
                case 'N':
                    e.Handled = FindAndFakeModalButtonPress("No");
                    break;
                case 'c':
                case 'C':
                    e.Handled = FindAndFakeModalButtonPress("Cancel");
                    break;
            }
        }

        private bool FindAndFakeModalButtonPress(string buttonLabel)
        {
            int buttonNumber = 0;
            if (cmdModalMessage1.Text.Equals(buttonLabel))
                buttonNumber = 1;
            else if (cmdModalMessage2.Text.Equals(buttonLabel))
                buttonNumber = 2;
            else if (cmdModalMessage3.Text.Equals(buttonLabel))
                buttonNumber = 3;
            bool found = buttonNumber > 0;
            if (found)
            {
                mostRecentModalMessageButtonPressed = buttonNumber;
                waitingForModalMessage = false;
            }
            return found;
        }

        private void cboRecentOperations_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                // If we're merely setting up the list, we still get events.  However, they're not user-triggered and should be ignored.
                if (settingUpRecentOperations)
                    return;

                // If we've just selected the topmost item on the list - a (none) or (select) item - give up now
                if (0 == cboRecentOperations.SelectedIndex)
                    return;

                SDListItem selectedItem = (SDListItem)cboRecentOperations.SelectedItem;
                Operation operation = TemplateFactory.Operations[selectedItem.Operation];
                // If it's a grid operation, ensure the most recently used one is visible (#696)
                if (operation.RequiresGrid)
                {
                    if (null != SdApplication.SoleInstance && null != SdApplication.SoleInstance.ActiveGrid && SdApplication.SoleInstance.ActiveGrid.HasWindow)
                    {
                        ((IGrid)SdApplication.SoleInstance.ActiveGrid.Window).ClearSelection();
                        ActivateMdiChild(SdApplication.SoleInstance.ActiveGrid.Window);
                        Application.DoEvents();
                    }
                }
                DoOperationOnceOrUntilCancelled(operation, new ParameterBag());
            }
            catch (Exception ex)
            {
                PuntThroughEventLoop(ex);
            }
        }

        private void NoteRecentOperation(Operation operation)
        {
            // We're not willing to add operations with prereqs to the recent operations list, as we can't guarantee the prereq has been run at the instant the operation is invoked.
            if (operation.HasPrerequisites)
                return;

            // Prevent rogue calls from modifying the list
            settingUpRecentOperations = true;

            // If we previously had no operations, we now have some and can select from them
            if (cboRecentOperations.Items.Count <= 1)
            {
                cboRecentOperations.Items.Clear();
                cboRecentOperations.Items.Add("(select)");
            }

            // Insert the new candidate at the top, removing it if it was further down the list.
            SDListItem candidate = new SDListItem(operation.FriendlyName, operation.Name);
            if (cboRecentOperations.Items.Contains(candidate))
                cboRecentOperations.Items.Remove(candidate);
            cboRecentOperations.Items.Insert(1, candidate);

            // Trim the recent operation list by removing least recently used
            while (cboRecentOperations.Items.Count > MAX_RECENT_OPERATIONS)
                cboRecentOperations.Items.RemoveAt(cboRecentOperations.Items.Count - 1);

            // Ensure the (select) is visible
            cboRecentOperations.SelectedIndex = 0;

            settingUpRecentOperations = false;
        }

        internal void NoteASubformCloseIsStarting()
        {
            InsideSubformClose = true;
        }

        internal void NoteASubformCloseIsCancelled()
        {
            InsideSubformClose = false;
            while (pendingPanelPops > 0)
            {
                PopPanel(true);
                pendingPanelPops--;
            }
        }

        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            base.ScaleControl(factor, specified);
            // Record the running scale factor used, for sizing controls we add dynamically where they don't do it themselves
            currentScaleFactor = new SizeF(currentScaleFactor.Width * factor.Width, currentScaleFactor.Height * factor.Height);
        }

        private void rGuiToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StartRGui();
        }

        private void StartRGui()
        {
            try
            {
                RVersion preferredVersion = RController.PreferredRVersion();
                while (null == preferredVersion)
                {
                    if (!RController.UserMightHaveInstalledR())
                        return;
                    preferredVersion = RController.PreferredRVersion();
                }
                string guiPath = preferredVersion.GuiPath;
                Process.Start(guiPath);
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't start R", ex, false);
            }
        }
    }
}