// #define RELEASE_EXCEPTIONS
#define WATCH_EXCEPTIONS

// If ALLOW_OPTIONAL_UNMANAGED_CODE is defined, the application is free to use unmanaged code to get around annoyances.
// Current uses:
// - Removes flicker when swapping between maximised MDI children using tabs
#define ALLOW_OPTIONAL_UNMANAGED_CODE

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Security;
using System.Text;
using System.Windows.Forms;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using System.Security.Permissions;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.IO;
using SpreadsheetGear.Windows.Forms;
using SpreadsheetGear;

namespace StatsDirect.UI
{
    public partial class frmMain : Form, IToolStripHost
    {
        private static readonly char[] BAR = { '|' };
        private static readonly char[] EQUALS = { '=' };

        /// <summary>
        /// The minimum amount of other decoration that must be preserved above and below the operations panel.  Forces large panels to scroll.
        /// </summary>
        // private const int HEIGHT_BREATHING_SPACE = 100;

        /// <summary>
        /// The name of the parameter to be passed around a result set that contains a list of operations that have contributed to the list.
        /// </summary>
        private const string OPERATION_MEMORY_NAME = "statsdirect-operation-list";
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
            panelTypeStack = new Stack<PanelType>();
            ShowPanel(PanelType.Default, false);
            UpdateFileList();
            UpdateToolsMenu();
            cboActiveReport.Items.Add(new ComboFormAdapter(null));
            cboActiveReport.SelectedIndex = 0;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void AddTemplates()
        {
            SDMenuItem rootItem = LoadMenuItems(Path.Combine(Configuration.SDConfiguration.InstallationDirectory, Properties.Settings.Default.MenuFileName));
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
            SetMenuVisibility(false);
        }

        internal void SetMenuVisibility(bool isGridVisible)
        {
            foreach (ToolStripItem item in mnuMain.Items)
            {
                SetMenuVisibility(item, isGridVisible);
            }
        }

        private bool SetMenuVisibility(ToolStripItem item, bool isGridVisible)
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
                return new ToolStripSeparator {Size = new Size(167, 6)};
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
            System.Xml.Serialization.XmlSerializer s = new System.Xml.Serialization.XmlSerializer(typeof (SDMenuItem));
            using (TextReader r = new StreamReader(pathName))
            {
                return (SDMenuItem) s.Deserialize(r);
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
            return new SDMenuItem {Label = "&User-defined", SubItems = items.ToArray()};
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
            if (null == tagObject || !(tagObject is Dictionary<string, string>))
                return;
            Dictionary<string, string> tags = (Dictionary<string, string>) tagObject;
            if (tags.Count > 1)
            {
                
            }
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
                {
                    PuntThroughEventLoop(ex);
                }
                else
                {
                    throw;
                }
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
                    try
                    {
                        bool succeeded = SDApplication.SoleInstance.MainWindow.DoOperationOnceOrUntilCancelled(operation, null);
                        if (!succeeded)
                            break;
                    }
                    catch (ValidationException ex)
                    {
                        SDApplication.SoleInstance.FriendlyError("Validation error", ex, true);
                    }
                    if (chkBatchMode.Checked)
                    {
                        // Ensure the next loop doesn't start with the data that's currently highlighted.
                        if (null != SDApplication.SoleInstance
                            && null != SDApplication.SoleInstance.ActiveGrid
                            && SDApplication.SoleInstance.ActiveGrid.HasWindow)
                        {
                            ((IGrid)SDApplication.SoleInstance.ActiveGrid.Window).ClearSelection();
                        }
                    }
                    // If we're batching, then we must be doing something with grid input.  So, if we're going round again, ensure our grid is visible.
                    if (chkBatchMode.Checked)
                    {
                        if (SDApplication.SoleInstance.ActiveGrid != null)
                            SDApplication.SoleInstance.ActiveGrid.Window.Activate();
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
#if RELEASE_EXCEPTIONS
            catch (Exception ex)
            {
                SDApplication.SoleInstance.EraseAnyOutstandingParameters();
                SDApplication.SoleInstance.FriendlyError("Error while running operation", ex, false);
            }
#endif
        }

        private void ClearBatchMode()
        {
            chkBatchMode.Checked = false;
            // Ensure there are no remembered batch details
            SDApplication.SoleInstance.ClearBatchMode();
        }

        public void DoOperation(string operationName)
        {
            try
            {
                Operation operation = TemplateFactory.Operations[operationName];
                SDApplication.SoleInstance.MainWindow.DoOperationOnceOrUntilCancelled(operation, null);
            }
            catch (ValidationException ex)
            {
                SDApplication.SoleInstance.FriendlyError("Validation error", ex, true);
            }
#if RELEASE_EXCEPTIONS
            catch (Exception ex)
            {
                SDApplication.SoleInstance.EraseAnyOutstandingParameters();
                SDApplication.SoleInstance.FriendlyError("Error while running operation", ex, false);
            }
#endif
        }

        /// <summary>
        /// Create and add a new grid window
        /// </summary>
        internal StatsDirectForm CreateGrid()
        {
            // Make and add the child window
            using (new WaitCursor())
            {
                StatsDirectForm child = new frmSpreadsheetGear();
                string childName = child.Text + " " + SDApplication.SoleInstance.GetGridNumber();
                child.Text = childName;
                SetUpForm(child);
                return child;
            }
        }

        internal StatsDirectForm FindOrOpenGrid(string filename)
        {
            foreach (WindowInformation wi in SDApplication.SoleInstance.Windows)
            {
                if (wi.IsFile(filename))
                    return wi.Window;
            }
            // If we get here, no existing grid has the file open - we'll have to reopen it.
            return CreateGrid(filename, true);
        }

        internal StatsDirectForm CreateGrid(string filename, bool isTempFile)
        {
            using (new WaitCursor())
            {
                StatsDirectForm newGrid = CreateGrid();
                bool opened = false;
                try
                {
                    opened = newGrid.OpenFile(filename, isTempFile);
                    if (!isTempFile)
                        SDApplication.SoleInstance.NoteRecentFile(filename, opened);
                }
                catch (IOException ex)
                {
                    SDApplication.SoleInstance.FriendlyError("Couldn't open spreadsheet", ex, true);
                }
                if (!opened)
                {
                    newGrid.Close();
                    SDApplication.SoleInstance.NoteFormClosing(newGrid, new FormClosingEventArgs(CloseReason.None, false));
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
                string childName = child.Text + " " + SDApplication.SoleInstance.GetReportNumber();
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
                opened = newReport.OpenFile(filename, isTempFile);
                if (!isTempFile)
                    SDApplication.SoleInstance.NoteRecentFile(filename, opened);
            }
            catch (IOException ex)
            {
                SDApplication.SoleInstance.FriendlyError("Couldn't open report", ex, true);
            }
            if (!opened)
            {
                newReport.Close();
                SDApplication.SoleInstance.NoteFormClosing(newReport, new FormClosingEventArgs(CloseReason.None, false));
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
                string childName = child.Text + " " + SDApplication.SoleInstance.GetScriptWindowNumber();
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
                opened = newScriptWindow.OpenFile(filename, isTempFile);
                if (!isTempFile)
                    SDApplication.SoleInstance.NoteRecentFile(filename, opened);
            }
            catch (IOException ex)
            {
                SDApplication.SoleInstance.FriendlyError("Couldn't open script", ex, true);
            }
            if (!opened)
            {
                newScriptWindow.Close();
                SDApplication.SoleInstance.NoteFormClosing(newScriptWindow, new FormClosingEventArgs(CloseReason.None, false));
            }
            return opened ? newScriptWindow : null;
        }

        /// <summary>
        /// Create and add a new grid window
        /// </summary>
        private void SetUpForm(StatsDirectForm child)
        {
            // child.WindowState = FormWindowState.Minimized;
            Cursor = Cursors.WaitCursor;
            // Make and add the child window
            child.MdiParent = this;

            // Make and add the corresponding tab(page)
            TabPage tabPage = new TabPage(child.Text);
            tabWindows.TabPages.Add(tabPage);
            tabWindows.SelectedTab = tabPage;

            // Add the report to the drop-down reports list
            if (child.ImplementsIReport)
            {
                ComboFormAdapter cfa = new ComboFormAdapter(child);
                cboActiveReport.Items.Add(cfa);
                cboActiveReport.SelectedItem = cfa;
            }

            // Store the information about the window
            WindowInformation info = new WindowInformation {TabPage = tabPage, Window = child};
            child.Tag = info;
            tabPage.Tag = info;
            SDApplication.SoleInstance.AddWindow(info);

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

            Cursor = Cursors.Default;
        }

        private void newGridToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CreateGrid();
        }

        private void tabWindows_Selecting(object sender, TabControlCancelEventArgs e)
        {
            // If there are no tabs to activate, the tab page will be null - in which case we need to do nothing
            if (null == e.TabPage)
                return;

            WindowInformation info = (WindowInformation)e.TabPage.Tag;
            // The tab may be asked to activate while it is still being set up, hence before it has an associated window.  Handle that case.
            if (null != info && null != info.Window && !activatingViaWindow)
            {
                activatingViaTab = true;
                // Defer activation until after the tab's processing finishes, as otherwise the tab forcibly grabs the focus back after we can't do anything about it.
                mostRecentlySelectedWindow = info.Window;
                postTabTimer.Enabled = true;
                activatingViaTab = false;
            }
        }

        internal void RemoveWindow(StatsDirectForm window)
        {
            RemoveTab(window.WindowInformation.TabPage);
            if (window.ImplementsIReport)
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
            closeToolStripMenuItem.Enabled = (tabWindows.TabPages.Count > 0) ;
        }

        private void closeTabToolStripMenuItem_Click(object sender, EventArgs e)
        {
            WindowInformation lastClickedTab = TabStripLastClickedTab();
            if (null != lastClickedTab)
            {
                lastClickedTab.Window.Close();
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
            CreateReport();
        }

        private void saveToolStripButton_Click(object sender, EventArgs e)
        {
            WindowInformation activeInfo = ActiveWindowInformation();
            if (null != activeInfo)
                activeInfo.Window.SaveContents();
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
            // Each child form will have been given the opportunity to save its data and close.
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
                SaveApplicationState();
        }

        /// <summary>
        /// Load any window state we need from persistent storage.
        /// </summary>
        private void LoadWindowState()
        {
            const double FRACTION_OF_PRIMARY = 0.75;

            // If our settings have previously been saved, load them now.  Otherwise, default to 75% width and height, centred, on the primary screen.
            if (Properties.Settings.Default.MainWidth <= 0)
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
                    Top = Properties.Settings.Default.MainTop;
                    Left = Properties.Settings.Default.MainLeft;
                    Width = Properties.Settings.Default.MainWidth;
                    Height = Properties.Settings.Default.MainHeight;

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
                WindowState = Properties.Settings.Default.MainWindowState;
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
                Properties.Settings.Default.MainTop = Top;
                Properties.Settings.Default.MainLeft = Left;
                Properties.Settings.Default.MainWidth = Width;
                Properties.Settings.Default.MainHeight = Height;
            }
            Properties.Settings.Default.MainWindowState = WindowState;
        }

        /// <summary>
        /// Save any window state we need to persistent storage.
        /// </summary>
        private void SaveApplicationState()
        {
            SaveWindowState();
            Properties.Settings.Default.Save();
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
        /// <param name="wasPivoted">true if the user changed GIDV, false if not</param>
        /// <returns>true if the user clicked OK, false if the user clicked Cancel</returns>
        public bool SelectCells(string selectionMessage, string cancelButtonLabel, out bool wasPivoted)
        {
            bool status;
            bool oldGidv = SDApplication.SoleInstance.Preferences.GIDV;
            bool wasWaiting = Application.UseWaitCursor;
            if (wasWaiting)
                Application.UseWaitCursor = false;
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
                System.Threading.Thread.Sleep(5);
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
                wasPivoted = (oldGidv != SDApplication.SoleInstance.Preferences.GIDV);
            }
            if (!wasPivoted)
                ShowPanel(PanelType.Default, false);
            cmdCancel.Text = oldCancelText;
            // Wait for the screen to update
            if (wasWaiting)
                Application.UseWaitCursor = true;
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
        private void MaybeCloseOperationOnCancel(IEnumerable<Parameter> parametersBeingCollected)
        {
            if (ShouldCloseOperationOnCancel(parametersBeingCollected))
                throw new CloseCurrentOperationException();
        }

        /// <summary>
        /// Returns true iff the current operation is one that should close the entire set of operations if it is cancelled while it's processing.
        /// </summary>
        /// <returns></returns>
        private bool ShouldCloseOperationOnCancel(IEnumerable<Parameter> parametersBeingCollected)
        {
            // #573: Always close operations on cancel.
            return true;

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
             */
        }

        /// <summary>
        /// Returns true iff the current operation is one that should close the entire set of operations if it is cancelled while it's processing.
        /// </summary>
        /// <returns></returns>
        private bool ShouldShowClose()
        {
            // #
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
            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];
            if (null != tlp)
            {
                foreach (Control control in tlp.Controls)
                {
                    if (null != control.Tag)
                    {
                        Parameter parameter = (Parameter)control.Tag;
                        if (!parameters.Contains(parameter))
                            parameters.Add(parameter);
                    }
                }
            }
            return parameters;
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFile();
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
                bool isTempFile = fileName.StartsWith("~");
                // User wants to open the file - but which file type?
                string extension = Path.GetExtension(path);
                if (null != extension)
                    extension = extension.ToLower();
                if (".xls".Equals(extension) || ".xlsx".Equals(extension))
                {
                    CreateGrid(path, isTempFile);
                    UpdateFileList();
                    return true;
                }
                if (".rtf".Equals(extension) || ".htm".Equals(extension) || ".html".Equals(extension) || ".mht".Equals(extension) || ".mhtml".Equals(extension) || ".txt".Equals(extension))
                {
                    CreateReport(path, isTempFile);
                    UpdateFileList();
                    return true;
                }
                if (".cs".Equals(extension) || ".vb".Equals(extension))
                {
                    CreateScriptWindow(path, isTempFile);
                    UpdateFileList();
                    return true;
                }
                if (".sdw".Equals(extension))
                {
                    SDApplication.SoleInstance.msgbox_x("StatsDirect 3 cannot open .sdw files. Please use StatsDirect 2 to save the file in Excel format.", MessageBoxButtons.OK, MessageBoxIcon.Error, "StatsDirect", true);
                    return false;
                }
                SDApplication.SoleInstance.msgbox_x("Could not open '" + path + "'.  StatsDirect 3 can only open Excel, rich text, HTML and script files.", MessageBoxButtons.OK, MessageBoxIcon.Error, "StatsDirect", true);
                SDApplication.SoleInstance.NoteRecentFile(path, false);
                UpdateFileList();
                return false;
            }
        }

        private void newScriptToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CreateScriptWindow();
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
                    pnlTop.Height = pnlModalMessage.Height;
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
            pnlTop.ResumeLayout();
            currentPanelType = panelType;
        }

        private void ResizeContainer(bool enforceHeightOnOperations)
        {
            int tableHeight = 0;
            if (pnlUser.Controls.ContainsKey("table"))
            {
                TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];
                tableHeight = tlp.PreferredSize.Height + tlp.Margin.Top;
                tlp.Width = pnlUser.Width;
            }
            // The operations panel is always at least the same height as the standard panel (58 pixels), but must also fit both left-hand and right-hand content
            int leftHandHeight = tlpSelectOperation.Height;
            int contentHeight = Math.Max(tableHeight, leftHandHeight);
            contentHeight = Math.Max(contentHeight, 58);

            // Set a constraint on the maximum height of the table so that it's never off the bottom of the window
            int constrainedHeight = contentHeight; // Math.Min(contentHeight, this.Height - HEIGHT_BREATHING_SPACE);
            if (enforceHeightOnOperations)
            {
                pnlTop.Height = constrainedHeight;
                pnlOperations.Height = constrainedHeight;
                tlpOperations.Height = contentHeight;
                pnlUser.Height = contentHeight;
            }

            // bool shouldScrollVertically = (constrainedHeight < contentHeight);
            // bool shouldScrollHorizontally = false;
            // tlpOperations.AutoScroll = shouldScrollVertically | shouldScrollHorizontally;
            // The following is a workaround for the TableLayoutPanel apparently not following its own wishes for height, even when the preferred height is reported correctly.  No idea why!
            if (pnlUser.Controls.ContainsKey("table"))
            {
                TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];
                tlp.Height = contentHeight;
            }
        }

        private void PushPanel(PanelType panelType, bool enforceHeightOnOperations)
        {
            panelTypeStack.Push(currentPanelType);
            ShowPanel(panelType, enforceHeightOnOperations);
        }

        private void PopPanel(bool enforceHeightOnOperations)
        {
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
#if RELEASE_EXCEPTIONS
            try
            {
                DoCalculateInternal();
            }
            catch (CancelCurrentOperationAndDoException)
            {
                // If this is thrown, there must be an exception handler further up the stack capable of catching it - make sure we don't get in the way.
                throw;
            }
            catch (Exception ex)
            {
                SDApplication.SoleInstance.FriendlyError("Error while running operation", ex, false);
            }
#else
            DoCalculateInternal();
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
                    if (null != operation.FriendlyNames && operation.FriendlyNames.Count > 0 && !string.IsNullOrEmpty(operation.FriendlyNames[0]))
                        Text = "StatsDirect: " + operation.FriendlyNames[0];
                    return DoOperationInternal(operation, inputParameters, isRedo);
                }
                catch (Templates.InvalidDataException ex)
                {
                    string errorMessage = ex.Message;
                    SDApplication.SoleInstance.msgbox_x(errorMessage, MessageBoxButtons.OK, MessageBoxIcon.Error, "StatsDirect", true);
                    // That one failed due to invalid data - keep the same data and try it again, which should prompt the user to fix it!
                    inputParameters = ex.InputParameters;
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
                        SDListItem selectedItem = (SDListItem) cboOperation.SelectedItem;
                        operation = TemplateFactory.Operations[selectedItem.Operation];
                        // Keep existing input parameters.  TODO: Is this correct, or should we be going back to the originals?
                        inputParameters = ex.InputParameters;
                        // Go round again, processing this operation

                        if (!ShouldRunOperationOnSelection(operation, inputParameters))
                        {
                        }
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
            SDApplication.SoleInstance.ActiveHelpUrl = null;
            SDApplication.SoleInstance.ActiveHelpTopic = 0; // ToC
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
            SDApplication.SoleInstance.EraseAnyOutstandingParameters();
            Application.UseWaitCursor = false;
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
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        private Operation DoOperationInternal(Operation operation, ParameterBag inputParameters, bool isRedo)
        {
            using (new WaitCursor())
            {
                mostRecentOperation = operation;

                // Set help
                if (null != operation.HelpContext)
                {
                    SDApplication.SoleInstance.ActiveHelpTopic = operation.HelpContext.ChmId;
                    if (null != operation.HelpContext.Url)
                        SDApplication.SoleInstance.ActiveHelpUrl = operation.HelpContext.Url;
                }

                // Self-referential operations are assumed to be instant and repeatable, so are set up immediately in the interface.  Others are run normally, and only then do they get any follow-on operations.
                TemplateProcessor templateProcessor = new TemplateProcessor(SDApplication.SoleInstance);
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
        private void NoteOperation(ParameterBag results, Operation operation)
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
            bool hasSuggestedOperations = suggestingOperation.AvailableSuggestedOperations(new TemplateProcessor(SDApplication.SoleInstance), inputParameters).Count > 0;
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
                                hasSuggestedOperations = suggestingOperation.AvailableSuggestedOperations(new TemplateProcessor(SDApplication.SoleInstance), inputParameters).Count > 0;
                                if (hasSuggestedOperations)
                                    break;
                            }
                        }
                    }
                }
            }
            IList<SuggestedOperation> availableSuggestedOperations = suggestingOperation.AvailableSuggestedOperations(new TemplateProcessor(SDApplication.SoleInstance), inputParameters);
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
                    string name = suggestedOperation.Name;
                    if (suggestedOperation.FriendlyNames.Count > 0)
                        name = suggestedOperation.FriendlyNames[0];
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
                    cboOperation.Enabled = cboOperation.Items.Count > 1;
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
                        MaybeRunSelectedOperation();
                }
            }
            finally
            {
                settingUpSubOperations = false;
            }
        }

        private bool ShouldRunOperationOnSelection(Operation operation, ParameterBag inputParameters)
        {
            // If the operation has some initial parameters that can be batched, we should start it and let it populate those parameters
            if (operation.Steps.Count > 0)
            {
                Step firstStep = operation.Steps[0];
                if (firstStep.Type == Step.StepType.Parameters)
                {
                    ParametersStep pStep = (ParametersStep)firstStep;
                    if (pStep.CombineWherePossible)
                    {
                        foreach (Parameter p in pStep.Parameters)
                        {
                            if (p.MustRequest || (null != inputParameters && (null == p.Name || !inputParameters.ContainsKey(p.Name))))
                            {
                                // The parameter will probably be requested, unless it will be defaulted.
                                // CI parameters can be defaulted - TODO: Think about how to make this a more generic check.
                                if (p is ConfidenceIntervalParameter)
                                {
                                    ConfidenceIntervalParameter cip = (ConfidenceIntervalParameter)p;
                                    if (cip.CanDefault && SDApplication.SoleInstance.Preferences.CanDefaultConfidenceInterval)
                                    {
                                        // The CI can be defaulted; no decision!
                                    }
                                    else
                                    {
                                        // The CI cannot be defaulted; use our standard decision
                                        return ((ITemplateHost)SDApplication.SoleInstance).CanCombine(p);
                                    }
                                }
                                else
                                {
                                    return ((ITemplateHost)SDApplication.SoleInstance).CanCombine(p);
                                }
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
            if (!ShouldRunOperationOnSelection(operation, context))
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

                MaybeRunSelectedOperation();
            }
            catch (Exception ex)
            {
                PuntThroughEventLoop(ex);
            }
        }

        private void MaybeRunSelectedOperation()
        {
            SDListItem selectedItem = (SDListItem)cboOperation.SelectedItem;
            Operation operation = TemplateFactory.Operations[selectedItem.Operation];

            if (InOperation)
            {
                // There's already an operation running; deal with it
                // The new operation may not auto-run, depending on whether it takes input or not.
                if (ShouldRunOperationOnSelection(operation, knownParameters))
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
                if (ShouldRunOperationOnSelection(operation, knownParameters))
                    DoCalculate();
            }
        }

        public bool IsOperationsPanelVisible
        {
            get { return pnlOperations.Visible; }
        }

        private void optGroupsByColumn_CheckedChanged(object sender, EventArgs e)
        {
            SDApplication.SoleInstance.Preferences.GIDV = !optGroupsByColumn.Checked;
            selectingData = false;
        }

        private void optGroupsByIdentifier_CheckedChanged(object sender, EventArgs e)
        {
            SDApplication.SoleInstance.Preferences.GIDV = optGroupsByIdentifier.Checked;
            selectingData = false;
        }

        internal bool SelectingData
        {
            get { return selectingData; }
        }

        private void frmMain_HelpButtonClicked(object sender, CancelEventArgs e)
        {
            SDApplication.SoleInstance.ShowHelp(this);
        }

        private void frmMain_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            ShowHelp();
        }

        private void ShowHelp()
        {
            // Check for hovering over a menu item
            if (null != lastSeenMenuItemTag && lastSeenMenuItemTag is Dictionary<string, string>)
            {
                Dictionary<string, string> tags = (Dictionary<string, string>) lastSeenMenuItemTag;
                string menuTopic;
                if (tags.TryGetValue("help", out menuTopic))
                {
                    SDApplication.SoleInstance.ShowHelp(this, menuTopic);
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
                            SDApplication.SoleInstance.ShowHelp(this, operationTopic);
                            return;
                        }
                    }
                }
            }
            if (null == ActiveMdiChild)
            {
                SDApplication.SoleInstance.ShowHelp(this);
                return;
            }
            SDApplication.SoleInstance.ActiveWindow.Window.ShowHelp();
        }

        private void contentsAndIndexToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SDApplication.SoleInstance.ActiveHelpTopic = 0;
            SDApplication.SoleInstance.ShowHelp(this);
        }

        private void methodSelectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SDApplication.SoleInstance.ActiveHelpTopic = 1213;
            SDApplication.SoleInstance.ShowHelp(this);
        }

        private bool cancelProgressPressed;

        internal void StartProgress(string operationDescription)
        {
            ShowPanel(PanelType.Progress, false);
            progressBar.Value = 0;
            lblProgress.Text = operationDescription;
            cancelProgressPressed = false;
        }

        internal bool UpdateProgress(double fractionComplete)
        {
            // Prevent overzealous input causing exceptions
            if (fractionComplete < 0)
                fractionComplete = 0;
            else if (fractionComplete > 1)
                fractionComplete = 1;
            progressBar.Value = (int)(progressBar.Maximum * fractionComplete);
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="host"></param>
        /// <param name="processor"></param>
        /// <param name="context"></param>
        /// <param name="outstandingParameters"></param>
        /// <returns></returns>
        /// <remarks>The return value may contain key->null for optional blank parameters.  It is up to the caller to handle this.</remarks>
        internal ParameterBag FillCombinedParameters(ITemplateHost host, ITemplateProcessor processor, ParameterBag context, IList<Parameter> outstandingParameters)
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
                            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];

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
                            {
                                fp = PrepareCombinedParameter(processor, (BooleanParameter)parameter, context);
                                break;
                            }
                        case ParameterType.ConfidenceInterval:
                            {
                                fp = PrepareCombinedParameter(processor, (ConfidenceIntervalParameter)parameter, context);
                                break;
                            }
                        case ParameterType.Custom:
                            {
                                if (typeof(ChartOptionsParameter) == parameter.GetType())
                                {
                                    fp = PrepareCombinedParameter((ChartOptionsParameter)parameter);
                                }
                                else if (typeof(FillableParameter) == parameter.GetType())
                                {
                                    fp = PrepareCombinedParameter(host, (FillableParameter)parameter);
                                }
                                else
                                    throw new ArgumentOutOfRangeException("processor", "Must be a ChartOptionsParameter or FillableParameter if it is a custom parameter");
                                break;
                            }
                        case ParameterType.Date:
                            {
                                fp = PrepareCombinedParameter(processor, (DateParameter)parameter, context);
                                break;
                            }
                        case ParameterType.Double:
                            {
                                fp = PrepareCombinedParameter(processor, (DoubleParameter)parameter, context);
                                break;
                            }
                        case ParameterType.Double2By2:
                            {
                                fp = PrepareCombinedParameter(processor, (Double2By2Parameter)parameter, context);
                                break;
                            }
                        case ParameterType.Double2By2ByK:
                            {
                                fp = PrepareCombinedParameter(processor, (Double2By2ByKParameter)parameter, context);
                                break;
                            }
                        case ParameterType.EditGrid:
                            {
                                fp = PrepareCombinedParameter(processor, (EditGridParameter)parameter, context);
                                break;
                            }
                        case ParameterType.Grid:
                            {
                                fp = PrepareCombinedParameter(processor, (GridParameter)parameter, context);
                                break;
                            }
                        case ParameterType.Integer:
                            {
                                fp = PrepareCombinedParameter(processor, (IntegerParameter)parameter, context);
                                break;
                            }
                        case ParameterType.MultipleOptions:
                            {
                                fp = PrepareCombinedParameter((MultipleOptionsParameter)parameter);
                                break;
                            }
                        case ParameterType.Option:
                            {
                                fp = PrepareCombinedParameter(processor, (OptionParameter)parameter, context);
                                break;
                            }
                        case ParameterType.Options:
                            {
                                fp = PrepareCombinedParameter(processor, (OptionsParameter)parameter, context);
                                break;
                            }
                        case ParameterType.PickFromList:
                            {
                                fp = PrepareCombinedParameter(processor, (PickFromListParameter)parameter, context);
                                break;
                            }
                        case ParameterType.PickVariables:
                            {
                                fp = PrepareCombinedParameter(processor, (PickVariablesParameter)parameter, context);
                                break;
                            }
                        case ParameterType.Special:
                            {
                                fp = PrepareCombinedParameter(processor, (SpecialParameter)parameter, context);
                                break;
                            }
                        case ParameterType.String:
                            {
                                fp = PrepareCombinedParameter(processor, (StringParameter)parameter, context);
                                break;
                            }
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
                outstandingParameters.Clear();

                MaybeShowVariables(context);

                // Some parameters (notably CI parameters) may be defaulted - none will be shown.  If that's the case, don't show; just default them all!
                DrawingControl.ResumeDrawing(this);
                FillCombinedParameters(processor, context, willDisplayAtLeastOneParameter, atLeastOneNonCancel ? null : cancelSkipsParameterString, ref outputParameters);
                return outputParameters;
            }
            finally
            {
                // Make absolutely certain a parameter doesn't survive between operations on the confidence interval drop-down
                cboConfidenceInterval.Tag = null;

                // Make absolutely certain the window doesn't lock up and become unable to repaint
                DrawingControl.ResumeDrawing(this);
            }
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

        private string TryToFindParameterLabel(IEnumerable<Operation> ops, ParameterBag context, string parameterName)
        {
            foreach (Operation op in ops)
            {
                string parameterTitle = TryToFindParameterLabel(op.Steps, context, parameterName);
                if (null != parameterTitle)
                    return parameterTitle;
            }
            return null;
        }

        private string TryToFindParameterLabel(IEnumerable<Step> steps, ParameterBag context, string parameterName)
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
                            return null != p.PromptExpression ? p.Prompt(new TemplateProcessor(SDApplication.SoleInstance), context) : null;
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
            if (pnlUser.Controls.ContainsKey("table"))
            {
                Control table = pnlUser.Controls["table"];
                pnlUser.Controls.RemoveByKey("table");
                table.Dispose();
            }
            cmdCalculate.Text = "&OK";
            cmdClose.Text = "C&ancel";
        }

        internal void StartCombinedParameters()
        {
            pnlUser.SuspendLayout();
            ClearCombinedParameters();
            TableLayoutPanel tlp = new TableLayoutPanel
                                       {
                                           Name = "table",
                                           ColumnCount = 2,
                                           GrowStyle = TableLayoutPanelGrowStyle.AddRows,
                                           Width = pnlUser.Width,
                                           Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                                           Tag = "TopLevelUserTable"
                                       };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tlp.Location = new Point(0, 0);
            tlp.Margin = new Padding(0, 0, 0, 0);
            tlp.SuspendLayout();
            pnlUser.Controls.Add(tlp);
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, BooleanParameter booleanParameter, ParameterBag context)
        {
            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];
            CheckBox cb = new CheckBox
                              {
                                  Padding = new Padding(3, 3, 3, 3),
                                  AutoSize = true,
                                  Tag = booleanParameter
                              };
            AddAppropriateEventHandlersTo(cb);
            if (context.ContainsKey(booleanParameter.Name) && null != context[booleanParameter.Name] && context[booleanParameter.Name].IsInputParameter && context[booleanParameter.Name].IsBoolean)
            {
                cb.Checked = context[booleanParameter.Name].AsBoolean;
            }
            else
            {
                if (null != booleanParameter.DefaultValue)
                    cb.Checked = (bool)processor.Evaluate(booleanParameter.DefaultValue, context);
                else
                    cb.Checked = false;
            }
            cb.Text = booleanParameter.HasPrompt ? booleanParameter.Prompt(processor, context) : "";
            MaybeAddHelpTip(cb, booleanParameter);
            tlp.Controls.Add(cb);
            tlp.SetColumnSpan(cb, 2);

            return null;
        }

        private void MaybeAddHelpTip(Control control, Parameter parameter)
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

                // this.SelectNextControl(c, true, true, true, false);
                // Control next = this.ActiveControl;
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

        private FilledParameter PrepareCombinedParameter(ChartOptionsParameter chartOptionsParameter)
        {
            Charting.ChartDefinition chartDefinition = chartOptionsParameter.ChartDefinition;
            Charting.ChartOptions chartOptions = chartDefinition.ChartOptions;
            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];
            Control ctl;
            switch (chartOptions.OptionType)
            {
                case Charting.ChartOptions.OptionTypes.Bar:
                case Charting.ChartOptions.OptionTypes.BoxWhisker:
                case Charting.ChartOptions.OptionTypes.Control:
                case Charting.ChartOptions.OptionTypes.ErrorBars:
                case Charting.ChartOptions.OptionTypes.Forest:
                case Charting.ChartOptions.OptionTypes.Histogram:
                case Charting.ChartOptions.OptionTypes.Ladder:
                case Charting.ChartOptions.OptionTypes.Normal:
                case Charting.ChartOptions.OptionTypes.Pyramid:
                case Charting.ChartOptions.OptionTypes.ROC:
                case Charting.ChartOptions.OptionTypes.ScatterXY:
                case Charting.ChartOptions.OptionTypes.Spread:
                case Charting.ChartOptions.OptionTypes.Survival:
                    ctl = new ctlChartOptions(chartDefinition);
                    break;
                case Charting.ChartOptions.OptionTypes.Agreement:
                case Charting.ChartOptions.OptionTypes.Gini:
                case Charting.ChartOptions.OptionTypes.LinearRegression:
                    // Do nothing - there are no options to fill
                    return new FilledParameter(true, chartOptionsParameter.ChartDefinition);
                default:
                    throw new ArgumentOutOfRangeException("chartOptionsParameter", chartOptions.OptionType.ToString(), "ChartOptions.OptionType: Don't know how to ask the user for options for the specified chart type");
            }
            // At this point, ctl is always assigned.
            ctl.Tag = chartOptionsParameter;
            tlp.Controls.Add(ctl);
            tlp.SetColumnSpan(ctl, 2);
            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, ConfidenceIntervalParameter confidenceIntervalParameter, ParameterBag context)
        {
            if (confidenceIntervalParameter.CanDefault && SDApplication.SoleInstance.Preferences.CanDefaultConfidenceInterval)
                return new FilledParameter(true, SDApplication.SoleInstance.Preferences.DefaultConfidenceInterval);

            // If this is a "standard" CI and the dedicated CI combo isn't in use, use it.  Otherwise, create one in the flow.
            ComboBox cbo;
            bool useSingle = null == cboConfidenceInterval.Tag && confidenceIntervalParameter.MinimumSuggestedValue == 0.9 && confidenceIntervalParameter.MaximumSuggestedValue == 0.99;
            if (useSingle)
            {
                cbo = cboConfidenceInterval;
                pnlConfidenceInterval.Visible = true;
            }
            else
            {
                TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];
                cbo = new ComboBox {Size = new Size(55, 18), FormattingEnabled = true};
                AddAppropriateEventHandlersTo(cbo);
                tlp.Controls.Add(cbo);
                MaybeAddHelpTip(cbo, confidenceIntervalParameter);

                Label lbl = new Label
                                {
                                    Tag = confidenceIntervalParameter,
                                    Padding = new Padding(0, 6, 0, 3),
                                    AutoSize = true,
                                    Text =
                                        confidenceIntervalParameter.HasPrompt
                                            ? confidenceIntervalParameter.Prompt(processor, context)
                                            : "Confidence (%)"
                                };
                tlp.Controls.Add(lbl);
                MaybeAddHelpTip(lbl, confidenceIntervalParameter);
            }

            cbo.Tag = confidenceIntervalParameter;
            cbo.Items.Clear();
            for (int multiplier = 0; multiplier < 500; multiplier++)
            {
                double suggestedValue = confidenceIntervalParameter.MinimumSuggestedValue + (multiplier * confidenceIntervalParameter.SuggestedStep);
                if (suggestedValue > confidenceIntervalParameter.MaximumSuggestedValue)
                    break;
                cbo.Items.Add((suggestedValue * 100.0).ToString("##0.0"));
            }

            // If there's a specific default CI, force it.  If not, don't overwrite the CI combo's value, so that a user can persist CI values between operations.
            if (context.ContainsKey(confidenceIntervalParameter.Name) && null != context[confidenceIntervalParameter.Name] && context[confidenceIntervalParameter.Name].IsInputParameter && context[confidenceIntervalParameter.Name].IsDouble)
            {
                cbo.Text = (context[confidenceIntervalParameter.Name].AsDouble * 100.0).ToString("##0.0");
            }
            else if (0.0 != confidenceIntervalParameter.DefaultValue)
            {
                cbo.Text = (confidenceIntervalParameter.DefaultValue * 100.0).ToString("##0.0");
            }
            else
            {
                // Don't force a CI if there's already one set on the singleton
                if (!useSingle || string.IsNullOrEmpty(cboConfidenceInterval.Text))
                {
                    cbo.Text = SDApplication.SoleInstance.Preferences.CanDefaultConfidenceInterval ? (SDApplication.SoleInstance.Preferences.DefaultConfidenceInterval * 100.0).ToString("##0") : "95";
                }
            }

            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, DateParameter dateParameter, ParameterBag context)
        {
            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];
            TextBox txt = new TextBox {Size = new Size(80, 18), Tag = dateParameter};
            if ((!dateParameter.ForceDefault) && context.ContainsKey(dateParameter.Name) && null != context[dateParameter.Name] && context[dateParameter.Name].IsInputParameter && context[dateParameter.Name].IsInt32)
            {
                txt.Text = context[dateParameter.Name].AsInt32.ToString();
            }
            else
            {
                if (dateParameter.HasDefaultValue)
                {
                    txt.Text = dateParameter.DefaultValue(processor, context).ToString("d");
                }
            }
            AddAppropriateEventHandlersTo(txt);
            MaybeAddHelpTip(txt, dateParameter);
            tlp.Controls.Add(txt);

            Label lbl = new Label
                            {
                                Tag = dateParameter,
                                Padding = new Padding(0, 6, 0, 3),
                                AutoSize = true,
                                Text = dateParameter.HasPrompt ? dateParameter.Prompt(processor, context) : ""
                            };
            tlp.Controls.Add(lbl);
            MaybeAddHelpTip(lbl, dateParameter);
            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, DoubleParameter doubleParameter, ParameterBag context)
        {
            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];
            TextBox txt = new TextBox {Size = new Size(70, 18), Tag = doubleParameter};
            if ((!doubleParameter.ForceDefault) && context.ContainsKey(doubleParameter.Name) && null != context[doubleParameter.Name] && context[doubleParameter.Name].IsInputParameter && context[doubleParameter.Name].IsDouble)
            {
                double defaultValue = context[doubleParameter.Name].AsDouble;
                if ((!double.IsNaN(defaultValue)) && defaultValue != Constant.MISSING)
                    txt.Text = context[doubleParameter.Name].AsDouble.ToString();
            }
            else
            {
                double? defaultValue = doubleParameter.DefaultValue(processor, context);
                string defaultValueString = "";
                if (defaultValue.HasValue && (!double.IsNaN(defaultValue.Value)) && defaultValue.Value != Constant.MISSING)
                    defaultValueString = defaultValue.Value.ToString();
                txt.Text = defaultValueString;
            }
            AddAppropriateEventHandlersTo(txt);

            string suffix = "";
            if (doubleParameter.ShowLimits)
            {
                double minimumValue = doubleParameter.MinimumValue(processor, context);
                double maximumValue = doubleParameter.MaximumValue(processor, context);
                if (minimumValue > double.MinValue || maximumValue < double.MaxValue)
                {
                    suffix = " (";
                    if (minimumValue > double.MinValue)
                        suffix += minimumValue.ToString();
                    else
                        suffix += "-\u221E";
                    suffix += " to ";
                    if (maximumValue < double.MaxValue)
                        suffix += maximumValue.ToString();
                    else
                        suffix += "\u221E";
                    suffix += ")";
                }
            }

            Label lbl = new Label {Tag = doubleParameter, Padding = new Padding(0, 6, 0, 3), AutoSize = true};
            if (doubleParameter.HasPrompt)
                lbl.Text = doubleParameter.Prompt(processor, context) + suffix;
            else
                lbl.Text = suffix;

            MaybeAddHelpTip(lbl, doubleParameter);
            MaybeAddHelpTip(txt, doubleParameter);

            if (doubleParameter.PromptPrecedesParameter)
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

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, Double2By2Parameter double2By2Parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];
            TableLayoutPanel panel2By2 = new TableLayoutPanel
                                             {Tag = double2By2Parameter, RowCount = 4, ColumnCount = 3, AutoSize = true};
            panel2By2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel2By2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel2By2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            Label lblColumnsPrompt = new Label
                                         {
                                             Padding = new Padding(3, 3, 3, 3),
                                             AutoSize = true,
                                             Text = double2By2Parameter.ColumnsPrompt
                                         };
            panel2By2.Controls.Add(lblColumnsPrompt, 0, 0);
            panel2By2.SetColumnSpan(lblColumnsPrompt, 3);

            Label lblLeftColumnPrompt = new Label
                                            {
                                                Padding = new Padding(3, 6, 3, 3),
                                                AutoSize = true,
                                                Text = double2By2Parameter.LeftColumnPrompt
                                            };
            panel2By2.Controls.Add(lblLeftColumnPrompt, 0, 1);

            Label lblRightColumnPrompt = new Label
                                             {
                                                 Padding = new Padding(3, 6, 3, 3),
                                                 AutoSize = true,
                                                 Text = double2By2Parameter.RightColumnPrompt
                                             };
            panel2By2.Controls.Add(lblRightColumnPrompt, 1, 1);

            Label lblRowsPrompt = new Label
                                      {
                                          Padding = new Padding(3, 6, 3, 3),
                                          AutoSize = true,
                                          Text = double2By2Parameter.RowsPrompt
                                      };
            panel2By2.Controls.Add(lblRowsPrompt, 2, 1);

            TextBox txtTL = new TextBox {Name = "txtTL", Size = new Size(100, 18)};
            if (context.ContainsKey(double2By2Parameter.TopLeftName) && null != context[double2By2Parameter.TopLeftName] && context[double2By2Parameter.TopLeftName].IsInputParameter && context[double2By2Parameter.TopLeftName].IsDouble)
                txtTL.Text = context[double2By2Parameter.TopLeftName].AsDouble.ToString();
            AddAppropriateEventHandlersTo(txtTL);
            panel2By2.Controls.Add(txtTL, 0, 2);

            TextBox txtTR = new TextBox {Name = "txtTR", Size = new Size(100, 18)};
            if (context.ContainsKey(double2By2Parameter.TopRightName) && null != context[double2By2Parameter.TopRightName] && context[double2By2Parameter.TopRightName].IsInputParameter && context[double2By2Parameter.TopRightName].IsDouble)
                txtTR.Text = context[double2By2Parameter.TopRightName].AsDouble.ToString();
            AddAppropriateEventHandlersTo(txtTR);
            panel2By2.Controls.Add(txtTR, 1, 2);

            Label lblTopRowPrompt = new Label
                                        {
                                            Padding = new Padding(3, 6, 3, 3),
                                            AutoSize = true,
                                            Text = double2By2Parameter.TopRowPrompt
                                        };
            panel2By2.Controls.Add(lblTopRowPrompt, 2, 2);

            TextBox txtBL = new TextBox {Name = "txtBL", Size = new Size(100, 18)};
            if (context.ContainsKey(double2By2Parameter.BottomLeftName) && null != context[double2By2Parameter.BottomLeftName] && context[double2By2Parameter.BottomLeftName].IsInputParameter && context[double2By2Parameter.BottomLeftName].IsDouble)
                txtBL.Text = context[double2By2Parameter.BottomLeftName].AsDouble.ToString();
            AddAppropriateEventHandlersTo(txtBL);
            panel2By2.Controls.Add(txtBL, 0, 3);

            TextBox txtBR = new TextBox {Name = "txtBR", Size = new Size(100, 18)};
            if (context.ContainsKey(double2By2Parameter.BottomRightName) && null != context[double2By2Parameter.BottomRightName] && context[double2By2Parameter.BottomRightName].IsInputParameter && context[double2By2Parameter.BottomRightName].IsDouble)
                txtBR.Text = context[double2By2Parameter.BottomRightName].AsDouble.ToString();
            AddAppropriateEventHandlersTo(txtBR);
            panel2By2.Controls.Add(txtBR, 1, 3);

            Label lblBottomRowPrompt = new Label
                                           {
                                               Padding = new Padding(3, 6, 3, 3),
                                               AutoSize = true,
                                               Text = double2By2Parameter.BottomRowPrompt
                                           };
            panel2By2.Controls.Add(lblBottomRowPrompt, 2, 3);

            tlp.Controls.Add(panel2By2);
            tlp.SetColumnSpan(panel2By2, 2);

            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, Double2By2ByKParameter double2By2ByKParameter, ParameterBag context)
        {
            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];
            TableLayoutPanel panel2By2ByK = new TableLayoutPanel
                                                {
                                                    Tag = double2By2ByKParameter,
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

            Label lblLeftColumnPrompt = new Label();
            lblLeftColumnPrompt.Padding = new Padding(3, 6, 3, 3);
            lblLeftColumnPrompt.AutoSize = true;
            lblLeftColumnPrompt.Text = "Present";
            panel2By2ByK.Controls.Add(lblLeftColumnPrompt, 0, 1);

            Label lblRightColumnPrompt = new Label {Padding = new Padding(3, 6, 3, 3), AutoSize = true, Text = "Absent"};
            panel2By2ByK.Controls.Add(lblRightColumnPrompt, 1, 1);

            Label lblRowsPrompt = new Label {Padding = new Padding(3, 6, 3, 3), AutoSize = true, Text = "Outcome:"};
            panel2By2ByK.Controls.Add(lblRowsPrompt, 2, 1);

            TextBox txtTL = new TextBox {Name = "txtTL", Size = new Size(100, 18)};
            AddAppropriateEventHandlersTo(txtTL);
            panel2By2ByK.Controls.Add(txtTL, 0, 2);

            TextBox txtTR = new TextBox {Name = "txtTR", Size = new Size(100, 18)};
            AddAppropriateEventHandlersTo(txtTR);
            panel2By2ByK.Controls.Add(txtTR, 1, 2);

            Label lblTopRowPrompt = new Label {Padding = new Padding(3, 6, 3, 3), AutoSize = true, Text = "Present"};
            panel2By2ByK.Controls.Add(lblTopRowPrompt, 2, 2);

            TextBox txtBL = new TextBox {Name = "txtBL", Size = new Size(100, 18)};
            AddAppropriateEventHandlersTo(txtBL);
            panel2By2ByK.Controls.Add(txtBL, 0, 3);

            TextBox txtBR = new TextBox {Name = "txtBR", Size = new Size(100, 18)};
            AddAppropriateEventHandlersTo(txtBR);
            panel2By2ByK.Controls.Add(txtBR, 1, 3);

            FlowLayoutPanel pnlNavigation = new FlowLayoutPanel
                                                {AutoSize = true, Tag = new[] {new List<double>(), new List<double>()}};
            panel2By2ByK.Controls.Add(pnlNavigation, 0, 4);
            panel2By2ByK.SetColumnSpan(pnlNavigation, 3);

            Button cmdPrevious = new Button {Name = "cmdPrevious", Text = "<", Width = 20};
            cmdPrevious.Click += cmdPrevious_KeyPress;
            cmdPrevious.Enabled = false;
            pnlNavigation.Controls.Add(cmdPrevious);

            Label lblStratum = new Label {Padding = new Padding(3, 9, 3, 3), AutoSize = true, Text = "Stratum 1 of 1"};
            pnlNavigation.Controls.Add(lblStratum);
            lblStratum.Tag = 1;

            Button cmdNext = new Button {Name = "cmdNext", Text = ">", Width = 20};
            cmdNext.Click += cmdNext_KeyPress;
            pnlNavigation.Controls.Add(cmdNext);

            Label lblBottomRowPrompt = new Label {Padding = new Padding(3, 6, 3, 3), AutoSize = true, Text = "Absent"};
            panel2By2ByK.Controls.Add(lblBottomRowPrompt, 2, 3);

            // Fill in data for stratum 1 if present; set number of strata if present
            if (context.ContainsKey(double2By2ByKParameter.Name) && null != context[double2By2ByKParameter.Name] && context[double2By2ByKParameter.Name].IsInputParameter && context[double2By2ByKParameter.Name].IsDataFrame)
            {
                DataFrame sourceFrame = context[double2By2ByKParameter.Name].AsDataFrame;
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
            /* Old version
            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];
            DataGridView gridEditGrid = new DataGridView();
            ((ISupportInitialize)gridEditGrid).BeginInit();
            DataGridViewTextBoxColumn col1 = new DataGridViewTextBoxColumn();
            DataGridViewTextBoxColumn col2 = new DataGridViewTextBoxColumn();
            gridEditGrid.Tag = double2By2ByKParameter;
            gridEditGrid.AllowUserToAddRows = false;
            gridEditGrid.AllowUserToDeleteRows = false;
            gridEditGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridEditGrid.ColumnHeadersVisible = false;
            gridEditGrid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { col1, col2 });
            gridEditGrid.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            gridEditGrid.MultiSelect = false;
            gridEditGrid.Name = "gridEditGrid";
            gridEditGrid.RowHeadersVisible = false;
            gridEditGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            gridEditGrid.Size = new System.Drawing.Size(250, 48);
            col1.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            col1.HeaderText = "Key";
            col1.Name = "col1";
            col1.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            col1.Width = 100;
            col2.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            col2.Width = 100;
            col2.Name = "col2";
            col2.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            gridEditGrid.Rows.Add(900);
            for (int table = 1; table <= 300; table++)
            {
                int headerRow = (table - 1) * 3;
                DataGridViewRow r = gridEditGrid.Rows[headerRow];
                r.DefaultCellStyle.BackColor = Color.Black;
                r.DefaultCellStyle.ForeColor = Color.White;
                r.Cells[0].Value = "Table " + table.ToString();
                r.ReadOnly = true;
            }
            tlp.Controls.Add(gridEditGrid);
            ((ISupportInitialize)gridEditGrid).EndInit();

            if (context.ContainsKey(double2By2ByKParameter.Name) && null != context[double2By2ByKParameter.Name] && context[double2By2ByKParameter.Name].IsInputParameter && context[double2By2ByKParameter.Name].IsDataFrame)
            {
                Data.DataFrame sourceFrame = context[double2By2ByKParameter.Name].AsDataFrame;
                int tableCount = Math.Min(300, sourceFrame.MinRows / 2);
                if (sourceFrame.VariableCount == 2 && sourceFrame.Variables[0].IsDoubleVariable && sourceFrame.Variables[1].IsDoubleVariable)
                {
                    Data.DoubleVariable var1 = sourceFrame.Variables[0].AsDoubleVariable;
                    Data.DoubleVariable var2 = sourceFrame.Variables[1].AsDoubleVariable;
                    for (int table = 0; table < tableCount; table++)
                    {
                        int gridBase = table * 3 + 1;
                        int varBase = table * 2;
                        gridEditGrid.Rows[gridBase].Cells[0].Value = var1.Data[varBase];
                        gridEditGrid.Rows[gridBase].Cells[1].Value = var2.Data[varBase];
                        gridEditGrid.Rows[gridBase + 1].Cells[0].Value = var1.Data[varBase + 1];
                        gridEditGrid.Rows[gridBase + 1].Cells[1].Value = var2.Data[varBase + 1];
                    }
                }
            }
            gridEditGrid.Size = new Size(250, 200);
            gridEditGrid.MaximumSize = new Size(250, 200);
            gridEditGrid.ScrollBars = ScrollBars.Vertical;
            gridEditGrid.CurrentCell = gridEditGrid.Rows[1].Cells[0];
            gridEditGrid.Visible = true;

            Label lbl = new Label();
            lbl.Tag = double2By2ByKParameter;
            lbl.Padding = new Padding(3, 6, 3, 3);
            lbl.AutoSize = true;
            if (double2By2ByKParameter.HasPrompt)
                lbl.Text = double2By2ByKParameter.Prompt(processor, context);
            else
                lbl.Text = "";
            tlp.Controls.Add(lbl);
            return null;
             */
        }

        void cmdPrevious_KeyPress(object sender, EventArgs e)
        {
            // Find our control and get tag data
            Button cmdPrevious = (Button)sender;
            FlowLayoutPanel pnlNavigation = (FlowLayoutPanel)cmdPrevious.Parent;
            Label lblStratum = (Label)pnlNavigation.Controls[1];
            Button cmdNext = (Button)pnlNavigation.Controls[2];
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

            if (stratum > 1)
                --stratum;
            lblStratum.Tag = stratum;

            // Fill the text boxes from the stored data
            offset = (stratum - 1) * 2;
            txtTL.Text = Formatting.XUnrounded(var1Data[offset]);
            txtTR.Text = Formatting.XUnrounded(var2Data[offset]);
            txtBL.Text = Formatting.XUnrounded(var1Data[offset + 1]);
            txtBR.Text = Formatting.XUnrounded(var2Data[offset + 1]);
            lblStratum.Text = "Stratum " + stratum.ToString() + " of " + strata.ToString();

            cmdPrevious.Enabled = stratum > 1;
            cmdNext.Enabled = true;
        }

        void cmdNext_KeyPress(object sender, EventArgs e)
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
            lblStratum.Text = "Stratum " + stratum.ToString() + " of " + (Math.Max(strata, stratum)).ToString();

            cmdPrevious.Enabled = true;
            cmdNext.Enabled = true; // Can always Next to create another stratum
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, EditGridParameter editGridParameter, ParameterBag context)
        {
            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];
            DataGridView gridEditGrid = new DataGridView();
            ((ISupportInitialize)gridEditGrid).BeginInit();
            DataGridViewTextBoxColumn colKey = new DataGridViewTextBoxColumn();
            DataGridViewTextBoxColumn colValue = new DataGridViewTextBoxColumn();
            gridEditGrid.Tag = editGridParameter;
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
            EditGridParameter egp = editGridParameter;
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
                                Tag = editGridParameter,
                                Padding = new Padding(0, 6, 0, 3),
                                AutoSize = true,
                                Text = editGridParameter.HasPrompt ? editGridParameter.Prompt(processor, context) : ""
                            };
            tlp.Controls.Add(lbl);
            return null;
        }

        private FilledParameter PrepareCombinedParameter(ITemplateHost host, FillableParameter fillableParameter)
        {
            IFillable fillable = fillableParameter.Fillable;
            string fillerToUse = fillable.FillerToUse;
            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];
            Control ctl;
            if ("ChiSquareGoodnessOfFit".Equals(fillerToUse))
                ctl = new ctlChiGFOptions((Builtins.ChiSquareGoodnessOfFitOptions)fillable);
            else if ("Distribution".Equals(fillerToUse))
                ctl = new ctlPDF((Builtins.DistributionOptions)fillable, host);
            else if ("Dummy".Equals(fillerToUse))
                ctl = new ctlDummyOptions((Builtins.DummyOptions)fillable);
            else if ("Extraction".Equals(fillerToUse))
                ctl = new ctlExtraction((Builtins.ExtractionOptions)fillable);
            else if ("OptionDescriptor".Equals(fillable.FillerToUse))
                ctl = new ctlOptions((OptionDescriptor)fillable);
            else if ("SortInPlace".Equals(fillerToUse))
            {
                // HACK: Break layering completely
                frmSpreadsheetGear gearForm = (frmSpreadsheetGear)ActiveMdiChild;
                if (null == gearForm)
                    return null;
                IRange range = gearForm.workbookView.RangeSelection.Areas[0];
                ctl = new ctlSort(range, gearForm.workbookView);
            }
            /**
        else if ("Extraction".Equals(fillable.FillerToUse))
            return Amend((StatsDirect.Builtins.ExtractionOptions)fillable);
        else if ("GraphicsOptions".Equals(fillable.FillerToUse))
            return Amend((StatsDirect.Builtins.GraphicsOptions)fillable);
        else if ("ROCCutoff".Equals(fillable.FillerToUse))
            return Amend((Charting.SDChart.ROCCutoff)fillable);
        else if ("Scores".Equals(fillable.FillerToUse))
            return Amend((StatsDirect.Builtins.ScoresOptions)fillable);
        else if ("SummaryStatistics".Equals(fillable.FillerToUse))
            return Amend((StatsDirect.Builtins.SummaryStatisticsOptions)fillable);
             **/
            else
                throw new ArgumentOutOfRangeException("fillableParameter", fillable.FillerToUse, "fillableParameter.Fillable.FillerToUse: Unknown option");
            ctl.Tag = fillableParameter;
            tlp.Controls.Add(ctl);
            tlp.SetColumnSpan(ctl, 2);
            return null;
        }

        /// <summary>
        /// If there is a grid presently displayed in the top bar, return it.  Otherwise return null.
        /// </summary>
        /// <returns></returns>
        private WorkbookView FindGridOrNull()
        {
            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];
            // Make use of the fact that grids are always added directly to the panel
            foreach (Control c in tlp.Controls)
                if (c is WorkbookView)
                    return (WorkbookView) c;
            // If we get here, there's no grid
            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, GridParameter gridParameter, ParameterBag context)
        {
            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];
            try
            {
                new FileIOPermission(PermissionState.Unrestricted).Assert(); // TODO: Can this be refined, or does SSG really need everything?
                WorkbookView grid = new WorkbookView
                                        {
                                            Tag = gridParameter,
                                            Name = "grid",
                                            Size = new Size(500 - 2*3, 305),
                                            ContextMenuStrip = contextMenuStrip
                                        };
                grid.ActiveWorkbookSet.GetLock();
                if (context.ContainsKey(gridParameter.Name) && null != context[gridParameter.Name] && context[gridParameter.Name].IsInputParameter && context[gridParameter.Name].IsDataFrame)
                {
                    IWorksheet sheet = grid.ActiveWorksheet;
                    IRange usedRange = sheet.UsedRange;
                    DataFrame frame = context[gridParameter.Name].AsDataFrame;
                    for (int col = 0; col < frame.VariableCount; col++)
                    {
                        DoubleVariable v = frame.Variables[col].AsDoubleVariable;
                        for (int row = 0; row < v.Length; row++)
                        {
                            usedRange.Cells[row, col].Value = v.Data[row];
                        }
                    }
                }
                grid.ActiveWorksheet.WindowInfo.Zoom = 88; // percent
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
                                Tag = gridParameter,
                                Padding = new Padding(0, 6, 0, 3),
                                AutoSize = true,
                                Text = gridParameter.HasPrompt ? gridParameter.Prompt(processor, context) : ""
                            };
            tlp.Controls.Add(lbl);

            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, IntegerParameter integerParameter, ParameterBag context)
        {
            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];
            TextBox txt = new TextBox {Size = new Size(70, 18), Tag = integerParameter};
            if ((!integerParameter.ForceDefault) && context.ContainsKey(integerParameter.Name) && null != context[integerParameter.Name] && context[integerParameter.Name].IsInputParameter && context[integerParameter.Name].IsInt32)
            {
                txt.Text = context[integerParameter.Name].AsInt32.ToString();
            }
            else
            {
                if (integerParameter.HasDefaultValue)
                {
                    txt.Text = integerParameter.DefaultValue(processor, context).ToString();
                }
            }
            AddAppropriateEventHandlersTo(txt);

            string suffix = "";
            if (integerParameter.ShowLimits)
            {
                if (integerParameter.MinimumValue > Int32.MinValue || integerParameter.MaximumValue < Int32.MaxValue)
                {
                    suffix = " (";
                    if (integerParameter.MinimumValue > Int32.MinValue)
                        suffix += integerParameter.MinimumValue.ToString();
                    else
                        suffix += "-\u221E";
                    suffix += " to ";
                    if (integerParameter.MaximumValue < Int32.MaxValue)
                        suffix += integerParameter.MaximumValue.ToString();
                    else
                        suffix += "\u221E";
                    suffix += ")";
                }
            }

            Label lbl = new Label
                            {
                                Tag = integerParameter,
                                Padding = new Padding(0, 6, 0, 3),
                                AutoSize = true,
                                Text =
                                    integerParameter.HasPrompt
                                        ? integerParameter.Prompt(processor, context) + suffix
                                        : suffix
                            };

            MaybeAddHelpTip(lbl, integerParameter);
            MaybeAddHelpTip(txt, integerParameter);

            if (integerParameter.PromptPrecedesParameter)
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

        private void AutoSizeCombo(ComboBox cbo)
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
            cbo.Size = new Size(width, cbo.PreferredHeight);
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, OptionParameter optionParameter, ParameterBag context)
        {
            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];

            switch (optionParameter.OptionFormatType)
            {
                case OptionFormatType.Dropdown:
                    {
                        string defaultValue = null;
                        if (context.ContainsKey(optionParameter.Name) && null != context[optionParameter.Name] && context[optionParameter.Name].IsInputParameter)
                        {
                            defaultValue = context[optionParameter.Name].AsString;
                        }
                        else
                        {
                            if (null != optionParameter.DefaultValue)
                                defaultValue = processor.Evaluate(optionParameter.DefaultValue, context).ToString();
                        }

                        ComboBox cbo = new ComboBox {Tag = optionParameter, MaximumSize = new Size(250, 21)};
                        OptionOption defaultOption = optionParameter.Options[0];
                        foreach (OptionOption optionOption in optionParameter.Options)
                        {
                            cbo.Items.Add(optionOption);
                            if (optionOption.Value.Equals(defaultValue))
                                defaultOption = optionOption;
                        }
                        cbo.SelectedIndex = 0;
                        cbo.DropDownStyle = ComboBoxStyle.DropDownList;
                        cbo.SelectedItem = defaultOption;

                        // There's no way of autosizing a combo... so we do it by hand!
                        AutoSizeCombo(cbo);

                        Label lbl = new Label
                                        {
                                            Tag = optionParameter,
                                            Padding = new Padding(0, 6, 0, 3),
                                            AutoSize = true,
                                            MaximumSize = new Size(500, 500),
                                            Text =
                                                optionParameter.HasPrompt
                                                    ? optionParameter.Prompt(processor, context)
                                                    : ""
                                        };

                        if (optionParameter.PromptPrecedesParameter)
                        {
                            tlp.Controls.Add(lbl);
                            tlp.Controls.Add(cbo);
                        }
                        else
                        {
                            tlp.Controls.Add(cbo);
                            tlp.Controls.Add(lbl);
                        }
                    }
                    break;
                case OptionFormatType.Radio:
                    {
                        GroupBox groupBox = null;
                        if (optionParameter.HasPrompt)
                        {
                            string prompt = optionParameter.Prompt(processor, context);
                            if (!string.IsNullOrEmpty(prompt))
                            {
                                groupBox = new SDGroupBox
                                               {
                                                   Tag = optionParameter,
                                                   Padding = new Padding(3, 0, 3, 3),
                                                   AutoSize = true,
                                                   Text = prompt
                                               };
                                tlp.Controls.Add(groupBox);
                                tlp.SetColumnSpan(groupBox, 2);
                            }
                        }

                        TableLayoutPanel panelOptions = new TableLayoutPanel
                                                            {
                                                                Tag = optionParameter,
                                                                RowCount = (optionParameter.Options.Count + 1)/2,
                                                                ColumnCount = optionParameter.Columns,
                                                                AutoSize = true
                                                            };
                        for (int column = 0; column < optionParameter.Columns; column++)
                        {
                            panelOptions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                        }

                        string defaultValue = null;
                        if (context.ContainsKey(optionParameter.Name) && null != context[optionParameter.Name] && context[optionParameter.Name].IsInputParameter)
                        {
                            defaultValue = context[optionParameter.Name].AsString;
                        }
                        else
                        {
                            if (null != optionParameter.DefaultValue)
                                defaultValue = processor.Evaluate(optionParameter.DefaultValue, context).ToString();
                        }

                        foreach (OptionOption optionOption in optionParameter.Options)
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
                            groupBox.Height = panelOptions.PreferredSize.Height + 20;
                            panelOptions.Location = new Point(7, 15);
                            groupBox.Controls.Add(panelOptions);
                        }
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException("optionParameter", optionParameter.OptionFormatType, "optionParameter.OptionFormatType: Only Dropdown and Radio are known");
            }

            return null;
        }

        private FilledParameter PrepareCombinedParameter(MultipleOptionsParameter parameter)
        {
            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];
            Control ctl;
            if ("effectOptions".Equals(parameter.FormatHint))
                ctl = new ctlEffectOptions(parameter);
            else
            {
                OptionDescriptor descriptor = new OptionDescriptor();
                foreach (OptionsOption option in parameter.Options)
                {
                    CheckBoxDescriptor cb = new CheckBoxDescriptor
                                                {
                                                    Checked = option.Selected,
                                                    IsExclusive = true,
                                                    Text = option.Label,
                                                    Name = option.Name
                                                };
                    descriptor.CheckBoxes.Add(cb);
                }
                foreach (OptionsSelect option in parameter.Selects)
                {
                    SelectionBoxDescriptor sb = new SelectionBoxDescriptor {Title = option.Prompt, Name = option.Name};
                    foreach (OptionOption o in option.Options)
                    {
                        sb.Labels.Add(o.Label);
                    }
                    sb.SelectedValue = option.DefaultValue;
                    descriptor.SelectionBoxes.Add(sb);
                }
                ctl = new ctlOptions(descriptor);
            }
            ctl.Tag = parameter;
            tlp.Controls.Add(ctl);
            tlp.SetColumnSpan(ctl, 2);
            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, OptionsParameter optionsParameter, ParameterBag context)
        {
            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];

            if (optionsParameter.HasPrompt)
            {
                string prompt = optionsParameter.Prompt(processor, context);
                if (!string.IsNullOrEmpty(prompt))
                {
                    Label lbl = new Label
                                    {
                                        Tag = optionsParameter,
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
                                                    Tag = optionsParameter,
                                                    RowCount = (optionsParameter.Options.Count + 1)/2,
                                                    ColumnCount = optionsParameter.Columns,
                                                    AutoSize = true
                                                };
            for (int column = 0; column < optionsParameter.Columns; column++)
            {
                panelOptions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            }

            foreach (OptionsOption optionsOption in optionsParameter.Options)
            {
                bool isChecked = optionsOption.Selected;
                if (context.ContainsKey(optionsOption.Name) && null != context[optionsOption.Name] && context[optionsOption.Name].IsInputParameter)
                {
                    isChecked = context[optionsOption.Name].AsBoolean;
                }
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
            CheckCombinedParameterVisibilityAndMaybeResize((Control)sender);
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
                TableLayoutPanel tlp = (TableLayoutPanel)sender;
                ParameterBag ambientParameters = new ParameterBag();
                ParameterBag context = fillCombinedParametersContext;
                ExtractCurrentValues(new TemplateProcessor(SDApplication.SoleInstance), ambientParameters, context, false);
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

                if (CheckCombinedParameterVisibility(tlp, ambientParameters))
                    ResizeContainer(true);
            }
        }

        /// <returns>true if at least one control's visibility was changed (and hence the container might need to resize)</returns>
        private bool CheckCombinedParameterVisibility(TableLayoutPanel tlp, ParameterBag ambientParameters)
        {
            TemplateProcessor processor = null;
            bool layoutSuspended = false;
            bool atLeastOneVisibilityChange = false;

            foreach (Control control in tlp.Controls)
            {
                if (null != control.Tag)
                {
                    Parameter parameter = (Parameter)control.Tag;
                    if (parameter.HasAcquireIfTrue)
                    {
                        if (null == processor)
                            processor = new TemplateProcessor(SDApplication.SoleInstance);
                        bool shouldAcquire = parameter.AcquireIfTrue(processor, ambientParameters);
                        if (control.Visible != shouldAcquire)
                            atLeastOneVisibilityChange = true;
                        if (!layoutSuspended)
                        {
                            tlp.SuspendLayout();
                            layoutSuspended = true;
                        }
                        control.Visible = shouldAcquire;
                    }
                }
            }
            if (layoutSuspended)
            {
                tlp.ResumeLayout(true);
            }
            return atLeastOneVisibilityChange;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, PickVariablesParameter pickVariablesParameter, ParameterBag context)
        {
            DataFrame frame = context[pickVariablesParameter.ParameterName].AsDataFrame;
            int[] initialState = null;
            if (pickVariablesParameter.PreSelectVariables)
            {
                // Set up at least the minimum variables
                int variableCount = Math.Min(frame.VariableCount, pickVariablesParameter.MinimumVariables);
                initialState = new int[variableCount];
                for (int i = 0; i < initialState.Length; i++)
                    initialState[i] = i;
            }
            if (pickVariablesParameter.MinimumVariables < 1)
                throw new ArgumentOutOfRangeException("pickVariablesParameter", pickVariablesParameter.MinimumVariables, "pickVariablesParameter.MinimumVariables: Must obtain values for at least one variable");
            if (pickVariablesParameter.MinimumVariables > pickVariablesParameter.MaximumVariables)
                throw new ArgumentException("minimumVariables must not be larger than maximumVariables");

            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];

            // Label the parameter above it if required
            if (pickVariablesParameter.HasPrompt)
            {
                Label lbl = new Label
                                {
                                    Tag = pickVariablesParameter,
                                    Padding = new Padding(0, 6, 0, 3),
                                    AutoSize = true,
                                    Text = pickVariablesParameter.Prompt(processor, context)
                                };
                tlp.Controls.Add(lbl);
                tlp.SetColumnSpan(lbl, 2);
            }

            TableLayoutPanel holder = new TableLayoutPanel
                                          {
                                              AutoSize = true,
                                              ColumnCount = 2,
                                              RowCount = pickVariablesParameter.MaximumVariables,
                                              Tag = pickVariablesParameter
                                          };
            for (int v = 0; v < pickVariablesParameter.MaximumVariables; v++)
            {
                ComboBox cbo = new ComboBox {FormattingEnabled = true};
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
                                  Text = "Variable " + (v + 1).ToString()
                              };
                holder.Controls.Add(l);
            }
            tlp.Controls.Add(holder);
            tlp.SetColumnSpan(holder, 2);

            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, PickFromListParameter pickFromListParameter, ParameterBag context)
        {
            DataFrame sourceFrame = context[pickFromListParameter.Source].AsDataFrame;
            string[] values;
            if (sourceFrame.Variables[0].IsStringVariable)
                values = sourceFrame.Variables[0].AsStringVariable.Data;
            else if (sourceFrame.Variables[0].IsClassifier)
                values = sourceFrame.Variables[0].AsClassifierVariable.SortedCategoryNames;
            else
                throw new ArgumentException("A PickFromListParameter can only pick from string or classifier variables");

            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];
            if (pickFromListParameter.AllowMultiple)
            {
                ListBox lstPickFromList = new ListBox
                                              {
                                                  Tag = pickFromListParameter,
                                                  FormattingEnabled = true,
                                                  Name = "lstPickFromList",
                                                  Size = new Size(250, 48)
                                              };
                foreach (string value in values)
                    lstPickFromList.Items.Add(value);
                lstPickFromList.SelectionMode = pickFromListParameter.AllowMultiple ? SelectionMode.MultiSimple : SelectionMode.One;
                tlp.Controls.Add(lstPickFromList);
            }
            else
            {
                ComboBox cbo = new ComboBox {Tag = pickFromListParameter, MaximumSize = new Size(250, 21)};
                if (pickFromListParameter.IncludeNoneEntry)
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
                                Tag = pickFromListParameter,
                                Padding = new Padding(0, 6, 0, 3),
                                AutoSize = true,
                                Text =
                                    pickFromListParameter.HasPrompt
                                        ? pickFromListParameter.Prompt(processor, context)
                                        : ""
                            };
            tlp.Controls.Add(lbl);
            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, SpecialParameter specialParameter, ParameterBag context)
        {
            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];

            if (("chi-2-column".Equals(specialParameter.SpecialType))
                || ("chi-3-column".Equals(specialParameter.SpecialType))
                || ("rr-index".Equals(specialParameter.SpecialType))
                || ("person-time-size".Equals(specialParameter.SpecialType))
                || ("likelihood".Equals(specialParameter.SpecialType)))
            {
                bool isLikelihood = "likelihood".Equals(specialParameter.SpecialType);
                bool isRrIndex = "rr-index".Equals(specialParameter.SpecialType);
                bool isPersonTimeSize = "person-time-size".Equals(specialParameter.SpecialType);
                bool has3Columns = "chi-3-column".Equals(specialParameter.SpecialType) || isPersonTimeSize;

                TableLayoutPanel ssgContainer = new TableLayoutPanel
                                                    {
                                                        Tag = specialParameter,
                                                        RowCount = 2,
                                                        ColumnCount = 2,
                                                        AutoSize = true
                                                    };

                Panel colsPanel = new Panel {Padding = new Padding(0, 0, 0, 0), Margin = new Padding(0,0,0,0), Size = new Size(300, 16)};
                ssgContainer.Controls.Add(colsPanel, 1, 0);

                Label col1Label = new Label
                                      {
                                          Text =
                                              isPersonTimeSize
                                                  ? "Index events"
                                                  : isRrIndex
                                                        ? "Reference rate"
                                                        : isLikelihood ? "+ feature" : "+ success",
                                          AutoSize = true,
                                          Location = new Point(30, 0)
                                      };
                colsPanel.Controls.Add(col1Label);

                Label col2Label = new Label
                                      {
                                          Text =
                                              (isPersonTimeSize || isRrIndex)
                                                  ? "Index PT"
                                                  : isLikelihood ? "- feature" : "- failure",
                                          AutoSize = true,
                                          Location = new Point(120, 0)
                                      };
                colsPanel.Controls.Add(col2Label);

                if (has3Columns)
                {
                    Label col3Label = new Label
                                          {
                                              Text = isPersonTimeSize ? "Reference size" : "score",
                                              AutoSize = true,
                                              Location = new Point(210, 0)
                                          };
                    colsPanel.Controls.Add(col3Label);
                }

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
                                            Size = new Size(has3Columns ? 320 : 230, 400),
                                            ContextMenuStrip = contextMenuStrip,
                                            Padding = new Padding(0,0,0,0),
                                            Margin = new Padding(0,0,0,0)
                                        };
                grid.GetLock();
                try
                {
                    if (context.ContainsKey(specialParameter.Name) && null != context[specialParameter.Name] && context[specialParameter.Name].IsInputParameter && context[specialParameter.Name].IsDataFrame)
                    {
                        DataFrame sourceFrame = context[specialParameter.Name].AsDataFrame;
                        if (sourceFrame.VariableCount >= 2 && sourceFrame.Variables[0].IsDoubleVariable && sourceFrame.Variables[1].IsDoubleVariable)
                        {
                            DumpIntoSsg((SpreadsheetGear.Advanced.Cells.IValues)grid.ActiveWorksheet, 0, sourceFrame.Variables[0].AsDoubleVariable);
                            DumpIntoSsg((SpreadsheetGear.Advanced.Cells.IValues)grid.ActiveWorksheet, 1, sourceFrame.Variables[1].AsDoubleVariable);
                            if (has3Columns && sourceFrame.VariableCount >= 3 && sourceFrame.Variables[0].IsDoubleVariable)
                            {
                                DumpIntoSsg((SpreadsheetGear.Advanced.Cells.IValues)grid.ActiveWorksheet, 2, sourceFrame.Variables[2].AsDoubleVariable);
                            }
                        }
                    }
                    // grid.ActiveWorksheet.WindowInfo.Zoom = 88; // percent
                    grid.ActiveWorksheet.Cells[0, has3Columns ? 3 : 2, 0, grid.ActiveWorksheet.Cells.ColumnCount - 1].EntireColumn.Hidden = true;
                    grid.ActiveWorksheet.Cells[0, 0, 0, has3Columns ? 2 : 1].EntireColumn.ColumnWidth = 11; // characters
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
            if ("raters-2d".Equals(specialParameter.SpecialType))
            {
                TableLayoutPanel ssgContainer = new TableLayoutPanel
                                                    {
                                                        Tag = specialParameter,
                                                        RowCount = 2,
                                                        ColumnCount = 2,
                                                        AutoSize = true
                                                    };

                Label colsLabel = new Label {Text = "Rater 2", AutoSize = true};
                ssgContainer.Controls.Add(colsLabel, 1, 0);

                VerticalLabel rowsLabel = new VerticalLabel {Text = "Rater 1", AutoSize = true, TabStop = false};
                ssgContainer.Controls.Add(rowsLabel, 0, 1);

                WorkbookView grid = new WorkbookView {Size = new Size(450, 400), ContextMenuStrip = contextMenuStrip};
                grid.GetLock();
                try
                {
                    if (context.ContainsKey(specialParameter.Name) && null != context[specialParameter.Name] && context[specialParameter.Name].IsInputParameter && context[specialParameter.Name].IsDataFrame)
                    {
                        DataFrame sourceFrame = context[specialParameter.Name].AsDataFrame;
                        for (int col = 0; col < sourceFrame.VariableCount; col++)
                            if (sourceFrame.Variables[col].IsDoubleVariable)
                                DumpIntoSsg((SpreadsheetGear.Advanced.Cells.IValues)grid.ActiveWorksheet, col, sourceFrame.Variables[col].AsDoubleVariable);
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
            if ("addedConstant".Equals(specialParameter.SpecialType))
            {
                double minimumC;
                double suggestedC;
                DataFrame frame = context["data"].AsDataFrame;
                DoubleVariable dv = frame.Variables[0].AsDoubleVariable;
                Builtins.Sheet.XConstant(dv.Length, 0, dv.Data, out minimumC, out suggestedC);
                context.AddOutput("a_min", minimumC);

                if (minimumC != Constant.MISSING)
                {
                    DoubleParameter dp = new DoubleParameter
                                             {
                                                 Name = specialParameter.Name,
                                                 PromptExpression = specialParameter.PromptExpression,
                                                 MinimumValueExpression = new Expression(minimumC.ToString()),
                                                 DefaultValueExpression = new Expression(suggestedC.ToString()),
                                                 CancelSkipsParameter = "Skip"
                                             };
                    return PrepareCombinedParameter(processor, dp, context);
                }
                return null;
            }
            if ("frame".Equals(specialParameter.SpecialType))
            {
                ctlPickAWindow ctl = new ctlPickAWindow(OutputType.Frame, specialParameter) {Tag = specialParameter};
                AddAppropriateEventHandlersTo(ctl);
                tlp.Controls.Add(ctl);
                tlp.SetColumnSpan(ctl, 2);
                return null;
            }
            if ("report".Equals(specialParameter.SpecialType))
            {
                ctlPickAWindow ctl = new ctlPickAWindow(OutputType.Report, specialParameter) {Tag = specialParameter};
                AddAppropriateEventHandlersTo(ctl);
                tlp.Controls.Add(ctl);
                tlp.SetColumnSpan(ctl, 2);
                return null;
            }
            if ("rubric".Equals(specialParameter.SpecialType))
            {
                Label ctl = new Label
                                {
                                    AutoSize = true,
                                    Tag = specialParameter,
                                    Text = specialParameter.Prompt(processor, context)
                                };
                tlp.Controls.Add(ctl);
                tlp.SetColumnSpan(ctl, 2);
                return null;
            }
            if ("textToNumbers".Equals(specialParameter.SpecialType))
            {
                ctlTextToNumbers ctl = new ctlTextToNumbers(context) {Tag = specialParameter};
                tlp.Controls.Add(ctl);
                tlp.SetColumnSpan(ctl, 2);
                return null;
            }
            throw new ArgumentOutOfRangeException("specialParameter", specialParameter.SpecialType, "specialParameter.SpecialType: Unknown option");
        }

        static void DumpIntoSsg(SpreadsheetGear.Advanced.Cells.IValues values, int column, DoubleVariable variable)
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

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, StringParameter stringParameter, ParameterBag context)
        {
            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];
            TextBox txt = new TextBox();
            if (stringParameter.MaxLength <= 0)
                txt.Size = new Size(250, 18);
            else
            {
                // TODO: Measure length in the face of multiple fonts and sizes.
                txt.MaxLength = stringParameter.MaxLength;
                txt.Size = new Size(6 + CHARWIDTH * stringParameter.MaxLength, 18);
            }
            txt.Tag = stringParameter;
            if ((!stringParameter.ForceDefault) && context.ContainsKey(stringParameter.Name) && null != context[stringParameter.Name] && context[stringParameter.Name].IsInputParameter && context[stringParameter.Name].IsInt32)
            {
                txt.Text = context[stringParameter.Name].AsInt32.ToString();
            }
            else
            {
                if (stringParameter.HasDefaultValue)
                {
                    txt.Text = stringParameter.DefaultValue(processor, context);
                }
            }
            AddAppropriateEventHandlersTo(txt);

            Label lbl = new Label
                            {
                                Tag = stringParameter,
                                Padding = new Padding(0, 6, 0, 3),
                                AutoSize = true,
                                Text = stringParameter.HasPrompt ? stringParameter.Prompt(processor, context) : ""
                            };

            MaybeAddHelpTip(lbl, stringParameter);
            MaybeAddHelpTip(txt, stringParameter);

            if (stringParameter.PromptPrecedesParameter)
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
            {
                return true;
            }
            return !(null != c.Tag && c.Tag is Parameter && ((Parameter)c.Tag).Type == ParameterType.Special && "report".Equals(((SpecialParameter)c.Tag).SpecialType));
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
        /// <param name="processor"></param>
        /// <param name="context"></param>
        /// <param name="shouldShow"></param>
        /// <param name="cancelSkipsParameterString"></param>
        /// <param name="outputParameters"></param>
        /// <remarks>This may return key->null in outputParameters for optional empty parameters.  It is up to the caller to deal with this.</remarks>
        internal void FillCombinedParameters(ITemplateProcessor processor, ParameterBag context, bool shouldShow, string cancelSkipsParameterString, ref ParameterBag outputParameters)
        {
            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];
            pnlUser.ResumeLayout();
            tlp.ResumeLayout(true);
            CheckCombinedParameterVisibility(tlp, context);

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
                    cmdCalculate.Text = "&OK";
                    if (cancelSkipsParameterString != null)
                        cmdClose.Text = cancelSkipsParameterString;
                    else
                    {
                        // We use cancel if we're in a follow-on operation that requires input (i.e. if pressing the button would lead to the option of closing the whole thing rather than an auto-close)
                        // #573: Always show Cancel
                        cmdClose.Text = ShouldShowClose() ? "C&ancel" : "C&ancel";
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
                            break;
                        }

                        // Otherwise, at least one parameter's invalid and focus should already have been set to it.  Go round again.
                        // SDApplication.SoleInstance.msgbox_x("Invalid data. Please correct it and try the operation again.", MessageBoxButtons.OK, MessageBoxIcon.Warning, "StatsDirect", false);
                    }
                }
            }
            finally
            {
                fillCombinedParametersContext = null;
                cmdCalculate.Text = "&OK";
                cmdClose.Text = "&Cancel";
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
                System.Threading.Thread.Sleep(5);
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
            TableLayoutPanel tlp = (TableLayoutPanel)pnlUser.Controls["table"];
            if (null != tlp)
            {
                foreach (Control control in tlp.Controls)
                {
                    Control invalidControlOrNull = ExtractCurrentValue(processor, control, outputParameters, context, doValidation);
                    if (null != invalidControlOrNull && null == firstInvalidControl)
                        firstInvalidControl = invalidControlOrNull;
                    allValid &= (null == invalidControlOrNull);
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
        bool CheckParameterSkippable(Control control)
        {
            // We're interested in non-label controls that have been tagged with Parameters.
            // Labels are uninteresting as they'll never contain a useful user-entered value.
            if (null != control.Tag && control.Tag is Parameter && typeof(Label) != control.GetType())
            {
                Parameter parameter = (Parameter)control.Tag;
                return null != parameter.CancelSkipsParameter;
            }

            // Don't care... so it's OK.
            return true;
        }

        /// <returns>null if the parameter is valid (or has no validation or validation is disabled), the control to be selected if the parameter fails validation.</returns>
        Control ExtractCurrentValue(ITemplateProcessor processor, Control control, ParameterBag outputParameters, ParameterBag context, bool doValidation)
        {
            // We're interested in non-label controls that have been tagged with Parameters.
            // Labels are uninteresting as they'll never contain a useful user-entered value.
            if (null != control.Tag && control.Tag is Parameter && typeof(Label) != control.GetType())
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
                                throw new ArgumentOutOfRangeException("control", "control.Tag: Only ChartOptionParameter and FillableParameter are known types of custom parameter");
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
                                DoubleParameter dp = (DoubleParameter) parameter;
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
                                if (tl == Constant.MISSING)
                                {
                                    txtTL.SelectAll();
                                    txtTL.Focus();
                                    return txtTL;
                                }
                                if (tr == Constant.MISSING)
                                {
                                    txtTR.SelectAll();
                                    txtTR.Focus();
                                    return txtTR;
                                }
                                if (bl == Constant.MISSING)
                                {
                                    txtBL.SelectAll();
                                    txtBL.Focus();
                                    return txtBL;
                                }
                                if (br == Constant.MISSING)
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
                            TableLayoutPanel panel2By2ByK = (TableLayoutPanel)control;
                            FlowLayoutPanel pnlNavigation = (FlowLayoutPanel)panel2By2ByK.GetControlFromPosition(0, 4);
                            Label lblStratum = (Label)pnlNavigation.Controls[1];

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
                                if (tl == Constant.MISSING)
                                {
                                    txtTL.SelectAll();
                                    txtTL.Focus();
                                    return txtTL;
                                }
                                if (tr == Constant.MISSING)
                                {
                                    txtTR.SelectAll();
                                    txtTR.Focus();
                                    return txtTR;
                                }
                                if (bl == Constant.MISSING)
                                {
                                    txtBL.SelectAll();
                                    txtBL.Focus();
                                    return txtBL;
                                }
                                if (br == Constant.MISSING)
                                {
                                    txtBR.SelectAll();
                                    txtBR.Focus();
                                    return txtBR;
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
                                IntegerParameter ip = (IntegerParameter) parameter;
                                if (value < ip.MinimumValue || value > ip.MaximumValue)
                                    return txt;
                            }
                            // If we get here, it's OK.
                            outputParameters[parameter.Name] = new FilledParameter(true, value);
                            return null;
                        }
                    case ParameterType.MultipleOptions:
                        {
                            IFillParameterBag fpb = (IFillParameterBag)control;
                            return fpb.Fill(outputParameters, doValidation);
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
                                || "rr-index".Equals(specialParameter.SpecialType))
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
                            if ("chi-3-column".Equals(specialParameter.SpecialType)
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
                                    if (null != ary)
                                    {
                                        for (int col = ary.GetLowerBound(1); col <= ary.GetUpperBound(1); col++)
                                        {
                                            DoubleVariable dv = new DoubleVariable(ary.GetUpperBound(0) - ary.GetLowerBound(0) + 1, "R2(" + col.ToString() + ")");
                                            for (int row = ary.GetLowerBound(0); row <= ary.GetUpperBound(0); row++)
                                            {
                                                dv.Data[row] = frmSpreadsheetGear.ToCellValue(ary[row, col]);
                                            }
                                            frame.Variables.Add(dv);
                                        }
                                    }
                                    outputParameters[parameter.Name] = new FilledParameter(true, frame);
                                }
                                return null;
                            }
                            if ("report".Equals(specialParameter.SpecialType)
                                || "frame".Equals(specialParameter.SpecialType)
                                || "dummyVariables".Equals(specialParameter.SpecialType)
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
        private class SelectedOperationChangedException : Exception
        {
            public ParameterBag InputParameters { get; private set; }
            public SelectedOperationChangedException(ParameterBag inputParameters)
            {
                InputParameters = inputParameters;
            }
        }

        private void frmMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (SDApplication.HasInstance)
                SDApplication.SoleInstance.Shutdown();

            // HACK: There are occasions when the main window is closed when we're in a DoEvents loop many levels down the stack.  This deals with the problem that the process can stick around.
            Environment.Exit(0);
        }

        private void frmMain_Shown(object sender, EventArgs e)
        {
            EnsureBuiltInMenuItemsCanShowHelp(mnuMain);

            // We may pre-load a document via a FileOpen parameter.  If we don't, show an opening form.
            if (MdiChildren.Length == 0)
            {
                using (frmOpening opening = new frmOpening())
                {
                    opening.ShowDialog(this);
                }
            }
        }

        public void EnsureBuiltInMenuItemsCanShowHelp(MenuStrip menuStrip)
        {
            foreach (ToolStripItem candidate in menuStrip.Items)
            {
                if (null != candidate.Tag && candidate.Tag is string && ((string)(candidate.Tag)).StartsWith("#{") && ((string)(candidate.Tag)).Contains("help="))
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
                if (null != candidate.Tag && candidate.Tag is string && ((string)(candidate.Tag)).StartsWith("#{") && ((string)(candidate.Tag)).Contains("help="))
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
            IList<string> recentFiles = SDApplication.SoleInstance.RecentFiles;
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

        public void UpdateToolsMenu()
        {
            if (null == toolsMenuItems)
            {
                toolsMenuItems = new List<ToolStripMenuItem>();
            }
            else
            {
                // Remove current entries
                foreach (ToolStripMenuItem item in toolsMenuItems)
                {
                    toolsToolStripMenuItem.DropDownItems.Remove(item);
                }
                toolsMenuItems.Clear();
            }
            System.Collections.Specialized.StringCollection names = Properties.Settings.Default.ToolsNames;
            System.Collections.Specialized.StringCollection paths = Properties.Settings.Default.ToolsPrograms;
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
            ToolStripMenuItem menuItem = (ToolStripMenuItem) sender;
            try
            {
                object tagObject = ToTagObject(menuItem);
                if (!(tagObject is Dictionary<string, string>))
                    return;
                Dictionary<string, string> tagDictionary = (Dictionary<string, string>) tagObject;
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
                System.Reflection.Assembly mainAssembly = GetType().Assembly;
                string mainFileName = mainAssembly.Location;
                string installPath = Path.GetDirectoryName(mainFileName);
                commandLine = commandLine.Replace("%STATSDIRECT%", installPath);
                Process.Start(commandLine, arguments);
            }
            catch (Win32Exception ex)
            {
                SDApplication.SoleInstance.FriendlyError("Couldn't start " + menuItem.Text, ex, false);
            }
            catch (IOException ex)
            {
                SDApplication.SoleInstance.FriendlyError("Couldn't start " + menuItem.Text, ex, false);
            }
            catch (Exception ex)
            {
                SDApplication.SoleInstance.FriendlyError("Couldn't start " + menuItem.Text, ex, false);
            }
        }

        private void setupToolsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (frmSetupTools frm = new frmSetupTools())
            {
                frm.ShowDialog(this);
            }
            UpdateToolsMenu();
        }

        private void aboutsStatsDirectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (frmAbout frm = new frmAbout())
            {
                frm.ShowDialog(this);
            }
            GC.Collect();
        }

        private void openToolStripButton_Click(object sender, EventArgs e)
        {
            OpenFile();
        }

        private void helpToolStripButton_Click(object sender, EventArgs e)
        {
            ShowHelp();
        }

        private void cutToolStripButton_Click(object sender, EventArgs e)
        {
            SDApplication.SoleInstance.ActiveWindow.EditCut();
        }

        private void copyToolStripButton_Click(object sender, EventArgs e)
        {
            SDApplication.SoleInstance.ActiveWindow.EditCopy();
        }

        private void pasteToolStripButton_Click(object sender, EventArgs e)
        {
            SDApplication.SoleInstance.ActiveWindow.EditPaste();
        }

        private void printToolStripButton_Click(object sender, EventArgs e)
        {
            SDApplication.SoleInstance.ActiveWindow.Print();
        }

        private void cmdHelp_Click(object sender, EventArgs e)
        {
            SDApplication.SoleInstance.ShowCurrentHelp();
        }

        private void cascadeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LayoutMdi(MdiLayout.Cascade);
        }

        private void tileHorizontallyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LayoutMdi(MdiLayout.TileHorizontal);
        }

        private void tileVerticallyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LayoutMdi(MdiLayout.TileVertical);
        }

        private void arrangeIconsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LayoutMdi(MdiLayout.ArrangeIcons);
        }

        private void maximiseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (null != ActiveMdiChild)
                ActiveMdiChild.WindowState = FormWindowState.Maximized;
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

        public enum SuggestionTime
        {
            BeforeOperation,
            AfterOperation
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

        [SuppressUnmanagedCodeSecurity]
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg,
                                    IntPtr wParam, IntPtr lParam);

        public const int WM_MDINEXT = 0x224;
#endif

        private void cmdSelectionHelp_Click(object sender, EventArgs e)
        {
            SDApplication.SoleInstance.ShowCurrentHelp();
        }

        private void cmdCancelProgress_Click(object sender, EventArgs e)
        {
            cancelProgressPressed = true;
        }

        private void newToolStripButton_Click(object sender, EventArgs e)
        {
            CreateNewInstanceOfCurrentWindow();
        }

        private void CreateNewInstanceOfCurrentWindow()
        {
            if (null == SDApplication.SoleInstance.ActiveWindow
                || null == SDApplication.SoleInstance.ActiveWindow.Window)
            {
                CreateGrid();
            }
            else
            {
                StatsDirectForm activeWindow = SDApplication.SoleInstance.ActiveWindow.Window;
                if (activeWindow.ImplementsIGrid)
                    CreateGrid();
                else if (activeWindow.ImplementsIReport)
                    CreateReport();
                if (activeWindow.ImplementsIScriptWindow)
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
            WindowInformation lastClickedTab = TabStripLastClickedTab();
            if (null != lastClickedTab)
            {
                lastClickedTab.Window.SaveContents();
            }
        }

        private void saveAsContextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            WindowInformation lastClickedTab = TabStripLastClickedTab();
            if (null != lastClickedTab)
            {
                lastClickedTab.Window.SaveAsContents();
            }
        }

        private void printToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            WindowInformation lastClickedTab = TabStripLastClickedTab();
            if (null != lastClickedTab)
            {
                lastClickedTab.Window.Print();
            }
        }

        private void renameContextMenuToolStripTextBox_KeyPress(object sender, KeyPressEventArgs e)
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
                        if (lastClickedTab.HasWindow && lastClickedTab.Window.ImplementsIReport)
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

        private void renameContextMenutoolStripMenuItem_Click(object sender, EventArgs e)
        {
            renameContextMenuToolStripTextBox.Focus();
        }

        private void tabContextMenuStrip_Opening(object sender, CancelEventArgs e)
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

        /// <summary>
        /// Activate the most recently selected (via tab) window.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks>Required because tabs select themselves on the way out of the event handling, after we have the opportunity to select anything else.</remarks>
        private void postTabTimer_Tick(object sender, EventArgs e)
        {
            postTabTimer.Enabled = false;
            if (null != mostRecentlySelectedWindow)
            {
                ActivateMdiChild(mostRecentlySelectedWindow);
                mostRecentlySelectedWindow = null;
            }
        }
        private void cutContextMenuItem1_Click(object sender, EventArgs e)
        {
            WorkbookView workbookView = FindGridOrNull();
            if (null == workbookView)
                return;
            workbookView.Cut();
        }

        private void copyContextMenuItem_Click(object sender, EventArgs e)
        {
            WorkbookView workbookView = FindGridOrNull();
            if (null == workbookView)
                return;
            workbookView.Copy();
        }

        private void pasteContextMenuItem_Click(object sender, EventArgs e)
        {
            WorkbookView workbookView = FindGridOrNull();
            if (null == workbookView)
                return;
            workbookView.Paste();
        }

        private void pasteSpecialContextMenuItem_Click(object sender, EventArgs e)
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

        private void insertContextMenuItem_Click(object sender, EventArgs e)
        {
            InsertCells();
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
            DeleteSpecial();
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
            WorkbookView workbookView = FindGridOrNull();
            if (null == workbookView)
                return;
            workbookView.Focus();
            SendKeys.Send("{DEL}");
            Application.DoEvents(); // Force processing of events, in this case clearing the selection
        }

        private void goToContextMenuItem_Click(object sender, EventArgs e)
        {
            GoToCell();
        }

        private void GoToCell()
        {
            WorkbookView workbookView = FindGridOrNull();
            if (null == workbookView)
                return;
            try
            {
                string cell = ((ITemplateHost)SDApplication.SoleInstance).GetString("Enter the cell address, for example G54", "Go to cell", "");
                workbookView.GetLock();
                if (null != cell)
                {
                    workbookView.ActiveWorksheet.Cells[cell].Activate();
                }
            }
            catch (Exception ex)
            {
                ((ITemplateHost)SDApplication.SoleInstance).Warning(ex.Message, "Go to cell");
            }
            finally
            {
                workbookView.ReleaseLock();
            }
        }

        private void findAndReplaceContextMenuItem_Click(object sender, EventArgs e)
        {
            WorkbookView workbookView = FindGridOrNull();
            if (null == workbookView)
                return;
            // TODO: Fix this rather nasty workaround once SpreadsheetGear has API support for its replace dialog
            workbookView.Focus();
            SendKeys.Send("^h");
            Application.DoEvents(); // Force processing of events, in this case showing the replace dialog
        }

        private void checkForUpdatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SDApplication.SoleInstance.CheckForUpdates();
        }

        private int mostRecentModalMessageButtonPressed;
        private bool waitingForModalMessage;

        internal DialogResult ShowModalMessage(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, string helpFile, HelpNavigator helpNavigator, string helpTopic)
        {
            IButtonControl oldAcceptButton = this.AcceptButton;
            IButtonControl oldCancelButton = this.CancelButton;
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
                this.AcceptButton = oldAcceptButton;
                this.CancelButton = oldCancelButton;
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
                    this.CancelButton = cmdModalMessage2;
                    break;
                case MessageBoxButtons.RetryCancel:
                    cmdModalMessage1.Visible = true;
                    cmdModalMessage2.Visible = true;
                    cmdModalMessage3.Visible = false;
                    cmdModalMessage1.Text = "Retry";
                    cmdModalMessage2.Text = "Cancel";
                    this.CancelButton = cmdModalMessage2;
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
                    this.CancelButton = cmdModalMessage3;
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
                this.AcceptButton = db;
                db.Focus();
            }
        }

        private System.Windows.Forms.DialogResult DecodeModalButtons(MessageBoxButtons buttons)
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
                System.Threading.Thread.Sleep(5);
            } while (waitingForModalMessage);
            mnuMain.Enabled = true;
            foreach (Form f in MdiChildren)
            {
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

        private Bitmap IconFromMessageBoxIcon(MessageBoxIcon icon)
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
            Icon sizedIcon = new Icon(rawIcon, 40, 40);
            Bitmap bmp = new Bitmap(sizedIcon.Width, sizedIcon.Height);
            Graphics gxMem = Graphics.FromImage(bmp);
            gxMem.DrawIcon(sizedIcon, 0, 0);
            gxMem.Dispose();
            return bmp;
        }

        private void cmdVariables_Click(object sender, EventArgs e)
        {
            int durationMilliseconds = 10000;
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
                default:
                    // Do nothing
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
    }

    class DrawingControl
    {
        private const int WM_SETREDRAW = 11;

        private static int suspendCounter;

        public static void SuspendDrawing(Control parent)
        {
            if (0 == suspendCounter)
            {
                Message msgSuspendUpdate = Message.Create(parent.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
                NativeWindow window = NativeWindow.FromHandle(parent.Handle);
                window.DefWndProc(ref msgSuspendUpdate);
            }
            suspendCounter++;
        }

        public static void ResumeDrawing(Control parent)
        {
            if (suspendCounter > 0)
                suspendCounter--;
            if (0 == suspendCounter)
            {
                IntPtr wparam = new IntPtr(1);
                Message msgResumeUpdate = Message.Create(parent.Handle, WM_SETREDRAW, wparam, IntPtr.Zero);
                NativeWindow window = NativeWindow.FromHandle(parent.Handle);
                window.DefWndProc(ref msgResumeUpdate);

                parent.Refresh();
            }
        }
    }

    [Serializable]
    public class CancelCurrentOperationAndDoException : Exception
    {
        private readonly Operation operation;
        private readonly ParameterBag inputParameters;

        public CancelCurrentOperationAndDoException(Operation operation, ParameterBag inputParameters)
        {
            this.operation = operation;
            this.inputParameters = inputParameters;
        }

        public Operation Operation
        {
            get { return operation; }
        }

        public ParameterBag InputParameters
        {
            get { return inputParameters; }
        }
    }

    [Serializable]
    public class CloseCurrentOperationException : Exception
    {
    }

    internal sealed class CallerHandlesChangedOperationAttribute : Attribute
    {
    }

    internal sealed class ComboFormAdapter
    {
        private readonly StatsDirectForm statsDirectForm;

        public ComboFormAdapter(StatsDirectForm statsDirectForm)
        {
            this.statsDirectForm = statsDirectForm;
        }

        public StatsDirectForm StatsDirectForm
        {
            get { return statsDirectForm; }
        }

        public override bool Equals(object obj)
        {
            if (!(obj is ComboFormAdapter))
                return false;
            ComboFormAdapter rhs = (ComboFormAdapter)obj;
            // Check for null forms on either side.  If both are null, we're OK...
            if (null == statsDirectForm && null == rhs.statsDirectForm)
                return true;
            // ... otherwise if either is null, the other isn't...
            if (null == statsDirectForm || null == rhs.statsDirectForm)
                return false;
            // ... otherwise both are non-null.
            return rhs.statsDirectForm.Equals(statsDirectForm);
        }

        public override int GetHashCode()
        {
            return null == statsDirectForm ? 0 : statsDirectForm.GetHashCode();
        }

        public override string ToString()
        {
            return null == statsDirectForm ? "New report" : statsDirectForm.Text;
        }
    }

    public class WaitCursor : IDisposable
    {
        private readonly Cursor m_cursorOld;

        public WaitCursor()
        {
            m_cursorOld = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposeManaged)
        {
            Cursor.Current = m_cursorOld;
        }
    }

    public class DefaultCursor : IDisposable
    {
        private readonly Cursor m_cursorOld;

        public DefaultCursor()
        {
            m_cursorOld = Cursor.Current;
            Cursor.Current = Cursors.Default;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposeManaged)
        {
            Cursor.Current = m_cursorOld;
        }
    }
}