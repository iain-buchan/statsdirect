using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Diagnostics;
using System.Windows.Forms;

using StatsDirect.Templates;
using StatsDirect.Configuration;
using StatsDirect.Data;
using StatsDirect.Utilities;

namespace StatsDirect.UI
{
    /// <summary>
    /// The central class for managing the state of StatsDirect.
    /// </summary>
    /// <remarks>This class is a Singleton (ref Gamma et al "Design Patterns")</remarks>
    public sealed class SdApplication : ITemplateHost, IRefillSource
    {
        private const int MAX_RECENT_FILES = 7;

        private int sActiveHelpTopic;
        private string activeHelpUrl;
        private static SdApplication soleInstance;
        private UserInfo userInfo;

        private bool closingForUpgrade;

        /// <summary>
        /// The MDI window in which newly-created children are placed
        /// </summary>
        private frmMain mainWindow;
        private readonly ICollection<WindowInformation> windows = new HashSet<WindowInformation>();
        private WindowInformation activeWindow;
        private WindowInformation activeGrid;

        // public PaneAndBoolean MostRecentlySelectedGrid { get; set; }
        public PaneAndPosition MostRecentlySelectedReport { get; set; }

        private SDPreferences preferences;

        private List<Parameter> outstandingParameters;

        /// <summary>
        /// Holder for variables that should be preserved during a run of this host, but not between runs.
        /// </summary>
        private Dictionary<string, object> session;

        private Dictionary<string, ParameterBag> sessionParametersPerOperation;

        private ParameterBag sessionParametersAcrossOperations;

        private Queue<DialogAndAction> queuedDialogs;
        /// <summary>
        /// If true, the UI is presently showing a dialog that was submitted using QueueDialog.
        /// </summary>
        private bool showingDialogThatCouldBeQueued;

        /// <summary>
        /// Returns the single instance of the application, creating it if necessary.
        /// </summary>
        internal static SdApplication SoleInstance
        {
            get
            {
                Contract.Ensures(null != Contract.Result<SdApplication>());
                return soleInstance ?? (soleInstance = new SdApplication());
            }
        }

        internal static bool HasInstance
        {
            get
            {
                return null != soleInstance;
            }
        }

        /// <summary>
        /// Sole constructor.  Because this follows the singleton pattern, the constructor is private.
        /// </summary>
        private SdApplication()
        {
            queuedDialogs = new Queue<DialogAndAction>();
            InitialiseFunctionRegistry();
            LoadPersistentValues();
        }

        /// <summary>
        /// On occasion, we have the potential for dialogs created in a background thread to be displayable while another dialog is on-screen.  Show this dialog if possible, but force a queue so that no more than one dialog is on-screen at one time.
        /// </summary>
        /// <param name="f">The dialog to display once any current dialog has closed.</param>
        /// <param name="postDisplayAction">Code to be run on the UI thread when the form closes; takes the form as its first parameter.</param>
        internal void ShowOrQueueDialog(Form f, Action<Form, DialogResult> postDisplayAction)
        {
            // Check whether we're safe to show the dialog now.  If not, queue it and return; if so, set the flag that we're showing to prevent anything else showing once we're out of the locked region.
            lock(queuedDialogs)
            {
                if (showingDialogThatCouldBeQueued)
                {
                    queuedDialogs.Enqueue(new DialogAndAction(f, postDisplayAction));
                    return;
                }
                else
                    showingDialogThatCouldBeQueued = true;
            }

            // If we get here, we're safe to show the dialog now.
            ShowDialogOnUiThread(f, postDisplayAction);
        }

        /// <summary>
        /// We know we're safe to show a dialog now (whether immediate or whether we just got it off the queue).  Show it on the UI thread, wait for a user response, and if necessary run its post display action (also on the UI thread).
        /// If another dialog has been queued while we're showing this one, show that.
        /// </summary>
        /// <param name="f"></param>
        /// <param name="postDisplayAction"></param>
        private void ShowDialogOnUiThread(Form f, Action<Form, DialogResult> postDisplayAction)
        {
            if (null != mainWindow)
                if (mainWindow.InvokeRequired)
                {
                    mainWindow.Invoke(new Action(() => { DialogResult result = f.ShowDialog(mainWindow); if (null != postDisplayAction) postDisplayAction(f, result); f.Dispose(); }));
                }
                else
                {
                    DialogResult result = f.ShowDialog(mainWindow);
                    if (null != postDisplayAction)
                        postDisplayAction(f, result);
                    f.Dispose();
                }
            else
            {
                // If there's no main window at present, we'd expect to be on the UI thread.  TODO: Prove this assumption.
                DialogResult result = f.ShowDialog();
                if (null != postDisplayAction)
                    postDisplayAction(f, result);
                f.Dispose();
            }

            // If there's another dialog ready to go, dequeue and show it.  Yes, this uses tail-recursion; the compiler can choose to optimise it away, or we can rely on the fact that this is a rare operation and hence the stack won't get deep.
            DialogAndAction newDialog = null;
            lock(queuedDialogs)
            {
                showingDialogThatCouldBeQueued = false;
                if (queuedDialogs.Count > 0)
                {
                    newDialog = queuedDialogs.Dequeue();
                    showingDialogThatCouldBeQueued = true;
                }
            }
            if (null != newDialog)
                ShowDialogOnUiThread(newDialog.Form, newDialog.PostCloseAction);
        }

        private void LoadPersistentValues()
        {
            string loadPath = Path.Combine(SDConfiguration.MyStatsDirectFolder, SDConfiguration.PERSISTENT_VALUE_FILE_NAME);
            if (File.Exists(loadPath))
            {
                try
                {
                    BinaryFormatter fmt = new BinaryFormatter();
                    using (Stream ws = new FileStream(loadPath, FileMode.Open, FileAccess.Read))
                    {
                        sessionParametersAcrossOperations = (ParameterBag)fmt.Deserialize(ws);
                        sessionParametersPerOperation = (Dictionary<string, ParameterBag>)fmt.Deserialize(ws);
                    }
                }
                catch (Exception)
                {
                    // Silently ignore the problem
                }
            }
        }

        /// <summary>
        /// The application is closing down.  Save anything we need!
        /// </summary>
        internal void Shutdown()
        {
            SavePersistentValues();
            if (ClosingForUpgrade)
                FetchTheUpgrade();
        }

        public static void FetchTheUpgrade()
        {
            const string DOWNLOAD_URL = "http://www.statsdirect.com/download/StatsDirectSetup.exe";
            Process.Start(DOWNLOAD_URL);
        }

        private void SavePersistentValues()
        {
            try
            {
                string savePath = Path.Combine(SDConfiguration.MyStatsDirectFolder, SDConfiguration.PERSISTENT_VALUE_FILE_NAME);
                BinaryFormatter fmt = new BinaryFormatter();
                using (Stream ws = new FileStream(savePath, FileMode.Create, FileAccess.Write))
                {
                    if (null == sessionParametersAcrossOperations)
                        sessionParametersAcrossOperations = new ParameterBag();
                    fmt.Serialize(ws, sessionParametersAcrossOperations);
                    if (null == sessionParametersPerOperation)
                        sessionParametersPerOperation = new Dictionary<string, ParameterBag>();
                    fmt.Serialize(ws, sessionParametersPerOperation);
                    // #760
                    Application.DoEvents();
                }
            }
            catch (IOException)
            {
                // If multiple SDs close at once (for example via a "Close All" gesture), they can all try to write at the same time.  Fail silently! 
            }
        }

        /// <summary>
        /// Return the next unused number that can be used for a blank grid
        /// </summary>
        /// <returns></returns>
        internal int GetGridNumber()
        {
            // Horribly inefficient O(n^2) algorithm, but we're relying on there rarely being more than a few windows open.
            int candidateNumber = 1;
            while (true)
            {
                bool acceptable = true;
                foreach (WindowInformation wi in windows)
                {
                    if (wi.HasWindow && (wi.Window is IGrid))
                    {
                        string windowName = wi.FriendlyName;
                        if (windowName.StartsWith("Data "))
                        {
                            string windowNumberAsString = windowName.Substring(5).Trim();
                            int windowNumber;
                            if (int.TryParse(windowNumberAsString, out windowNumber))
                            {
                                if (windowNumber == candidateNumber)
                                {
                                    acceptable = false;
                                    break;
                                }
                            }
                        }
                    }
                }
                if (acceptable)
                    break;
                candidateNumber++;
            }
            return candidateNumber;
        }

        /// <summary>
        /// Return the next unused number that can be used for a blank report
        /// </summary>
        /// <returns></returns>
        internal int GetReportNumber()
        {
            // Horribly inefficient O(n^2) algorithm, but we're relying on there rarely being more than a few windows open.
            int candidateNumber = 1;
            while (true)
            {
                bool acceptable = true;
                foreach (WindowInformation wi in windows)
                {
                    if (!(wi.Window is IReport))
                        continue;

                    string windowName = wi.FriendlyName;
                    if (!windowName.StartsWith("Report "))
                        continue;

                    // We don't want to load Report 1.rtf and create Report 1 again (#673).  Strip any suffix before comparison.
                    if (Path.HasExtension(windowName))
                        windowName = Path.GetFileNameWithoutExtension(windowName);
                    string windowNumberAsString = windowName.Substring(7).Trim();
                    int windowNumber;
                    if (int.TryParse(windowNumberAsString, out windowNumber))
                    {
                        if (windowNumber == candidateNumber)
                        {
                            acceptable = false;
                            break;
                        }
                    }
                }
                if (acceptable)
                    break;
                candidateNumber++;
            }
            return candidateNumber;
        }

        /// <summary>
        /// Return the next unused number that can be used for a blank script window
        /// </summary>
        /// <returns></returns>
        internal int GetScriptWindowNumber()
        {
            // Horribly inefficient O(n^2) algorithm, but we're relying on there rarely being more than a few windows open.
            int candidateNumber = 1;
            while (true)
            {
                bool acceptable = true;
                foreach (WindowInformation wi in windows)
                {
                    if (wi.Window is IScriptWindow)
                    {
                        string windowName = wi.FriendlyName;
                        if (windowName.StartsWith("Script "))
                        {
                            string windowNumberAsString = windowName.Substring(7).Trim();
                            int windowNumber;
                            if (int.TryParse(windowNumberAsString, out windowNumber))
                            {
                                if (windowNumber == candidateNumber)
                                {
                                    acceptable = false;
                                    break;
                                }
                            }
                        }
                    }
                }
                if (acceptable)
                    break;
                candidateNumber++;
            }
            return candidateNumber;
        }

        internal UserInfo UserInfo
        {
            get { return userInfo; }
            set
            {
                // Security check - no cheating!
                if (null != userInfo)
                    throw new Exception("Can only assign once to UserInfo");
                userInfo = value;
            }
        }

        internal frmMain MainWindow
        {
            get { return mainWindow; }
            set { mainWindow = value; }
        }

        internal void AddWindow(WindowInformation info)
        {
            windows.Add(info);
        }

        internal void RemoveWindow(WindowInformation info)
        {
            if (windows.Contains(info))
                windows.Remove(info);
        }

        internal WindowInformation ActiveWindow
        {
            get { return activeWindow; }
        }

        internal WindowInformation ActiveGrid
        {
            get { return activeGrid; }
        }

        public ICollection<WindowInformation> Windows
        {
            get { return windows; }
        }

        /// <summary>
        /// A form has found itself closing by some means and has informed us.
        /// Make sure other features of the interface related to that form are tidied up, and remove our memory of the form.
        /// </summary>
        /// <param name="window"></param>
        /// <param name="e"></param>
        internal void NoteFormClosing(StatsDirectForm window, FormClosingEventArgs e)
        {
            WindowInformation info = (WindowInformation)window.Tag;
            if (null != info)
            {
                if (null != info.TabPage && null != mainWindow)
                {
                    mainWindow.RemoveWindow(window);
                }
                // Break reference cycles
                info.Window = null;
                info.TabPage = null;
                windows.Remove(info);
            }
        }

        /// <summary>
        /// A window has found itself activated.  Ensure the tabs are synchronised.
        /// </summary>
        /// <param name="info"></param>
        internal void NoteFormActivated(WindowInformation info)
        {
            activeWindow = info;
            if (null != info.Window)
            {
                if (info.Window is IGrid)
                    activeGrid = info;
                mainWindow.EnsureTabSelected(info.TabPage);
                mainWindow.SetMenuVisibility(info.Window is IGrid);
            }
        }

        void ITemplateHost.ShowHelp(int helpContextId)
        {
            System.Windows.Forms.Help.ShowHelp(SoleInstance.MainWindow, HelpFilePath, System.Windows.Forms.HelpNavigator.TopicId, helpContextId.ToString());
        }

        internal void ShowHelp(Form Parent, string Topic)
        {
            System.Windows.Forms.Help.ShowHelp(Parent, HelpFilePath, System.Windows.Forms.HelpNavigator.TopicId, Topic);
        }

        internal void ShowHelp(Form Parent)
        {
            if (null != ActiveHelpUrl)
            {
                // Show the URL
                System.Windows.Forms.Help.ShowHelp(Parent, ActiveHelpUrl);
            }
            else if (0 != ActiveHelpTopic)
            {
                // Specific help - show it.
                System.Windows.Forms.Help.ShowHelp(Parent, HelpFilePath, System.Windows.Forms.HelpNavigator.TopicId, ActiveHelpTopic.ToString());
            }
            else
            {
                // Nothing in particular, guess something useful or show the ToC if we can't.
                if (null != SoleInstance && null != SoleInstance.ActiveWindow && SoleInstance.ActiveWindow.HasWindow && (SoleInstance.ActiveWindow.Window is IGrid))
                {
                    // Grid - show the worksheet help, which is 1040.
                    System.Windows.Forms.Help.ShowHelp(Parent, HelpFilePath, System.Windows.Forms.HelpNavigator.TopicId, "1040");
                }
                else
                {
                    System.Windows.Forms.Help.ShowHelp(Parent, HelpFilePath, System.Windows.Forms.HelpNavigator.TableOfContents);
                }
            }
        }

        internal string HelpFilePath
        {
            get { return SDConfiguration.HelpFilePath; }
        }

        internal int ActiveHelpTopic
        {
            get { return sActiveHelpTopic; }
            set { sActiveHelpTopic = value; activeHelpUrl = null; }
        }

        internal string ActiveHelpUrl
        {
            get { return activeHelpUrl; }
            set { sActiveHelpTopic = 0; activeHelpUrl = value; }
        }

        internal IList<Pane> AvailableReportPanes()
        {
            IList<Pane> availableWindows = new List<Pane>();
            foreach (WindowInformation info in windows)
            {
                if (info.Window is IReport)
                {
                    foreach (Pane pane in info.Window.AvailablePanes)
                    {
                        availableWindows.Add(pane);
                    }
                }
            }
            return availableWindows;
        }

        internal IList<PaneAndPosition> AvailableReportPanesAndPositions()
        {
            IList<PaneAndPosition> availableWindows = new List<PaneAndPosition>();
            foreach (Pane pane in AvailableReportPanes())
                availableWindows.Add(new PaneAndPosition(pane, RelativePosition.LastColumn));
            return availableWindows;
        }

        internal IList<PaneAndPosition> AvailableFramePanesAndPositions()
        {
            // PaneAndBoolean mostRecent = MostRecentlySelectedGrid;
            Pane mostRecentPane = null;
            if (null != ActiveGrid && ActiveGrid.HasWindow)
                mostRecentPane = ActiveGrid.Window.SelectedPane;
            IList<PaneAndPosition> availableWindows = new List<PaneAndPosition>();
            foreach (Pane pane in AvailableFramePanes())
            {
                if (pane.Equals(mostRecentPane))
                    availableWindows.Insert(0, new PaneAndPosition(pane, RelativePosition.AfterSelection));
                availableWindows.Add(new PaneAndPosition(pane, RelativePosition.LastColumn));
            }
            return availableWindows;
        }

        internal IList<Pane> AvailableFramePanes()
        {
            IList<Pane> availableWindows = new List<Pane>();
            foreach (WindowInformation info in windows)
            {
                if (info.Window is IGrid)
                {
                    foreach (Pane pane in info.Window.AvailablePanes)
                    {
                        availableWindows.Add(pane);
                    }
                }
            }
            return availableWindows;
        }

        /// <summary>
        /// Allows the user to select from existing report windows, plus potentially a new one.
        /// </summary>
        /// <param name="AllowNew">If true, the user may select a new report as well as any existing ones.  If false, only existing reports may be picked.</param>
        /// <returns></returns>
        public IReport PickReportWindow(bool AllowNew)
        {
            return MainWindow.SelectedReportWindow;
        }

        internal IReport SelectReportWindow(Pane selectedPane)
        {
            // As we now don't remember reports for output, this is equivalent to a PickReportWindow.
            return PickReportWindow(false);
        }

        /// <summary>
        /// Allows the user to pick from existing grid windows, plus potentially a new one.
        /// </summary>
        /// <param name="allowNew">If true, the user may select a new grid as well as any existing ones.  If false, only existing grids may be picked.</param>
        /// <param name="relativePosition"></param>
        /// <returns></returns>
        public IGrid PickGridWindow(bool allowNew, ref RelativePosition relativePosition)
        {
            IList<Pane> availableWindows = AvailableFramePanes();
            try
            {
                Pane selectedPane = null;
                if (availableWindows.Count > 0)
                {
                    const string KEY = "solo";
                    SpecialParameter parameter = new SpecialParameter
                    {
                        SpecialType = "frame",
                        Name = KEY,
                        ExtraData = new object[] { relativePosition },
                        PromptExpression = new Expression("Pick the sheet in which you want the output to appear")
                    };
                    ParameterBag results = FillSingleParameter(parameter); // Will never return a null value as the parameter cannot be skipped
                    bool cancelled = (null == results || !results.ContainsKey(KEY));
                    if (cancelled)
                        throw new TemplateOperationCancelledException();
                    PaneAndPosition selectedPaneAndCurrent = results[KEY].AsPaneAndPosition;
                    selectedPane = selectedPaneAndCurrent.Pane;
                    relativePosition = selectedPaneAndCurrent.WritePosition;
                }
                else
                {
                    relativePosition = RelativePosition.LastColumn;
                }
                return SelectGridWindow(selectedPane, ref relativePosition);
            }
            catch (TemplateOperationCancelledException)
            {
                // User cancelled
                relativePosition = RelativePosition.LastColumn;
                return null;
            }
        }

        private IGrid SelectGridWindow(Pane selectedPane, ref RelativePosition writePosition)
        {
            if (null == selectedPane || null == selectedPane.WindowInformation)
            {
                // Create a new grid, write at the end of it
                IGrid grid = (IGrid)mainWindow.CreateGrid();
                writePosition = RelativePosition.LastColumn;
                // MostRecentlySelectedGrid = new PaneAndBoolean(grid.SelectedPane, writeAtCurrentLocation);
                return grid;
            }
            // Existing window
            IGrid selectedGrid = (IGrid)selectedPane.WindowInformation.Window;
            if (!selectedGrid.SelectPane(selectedPane))
            {
                // Selection failed; the grid probably no longer exists.  Fail.
                writePosition = RelativePosition.LastColumn;
                return null;
            }
            // MostRecentlySelectedGrid = new PaneAndBoolean(selectedGrid.SelectedPane, writeAtCurrentLocation);
            return selectedGrid;
        }

        /// <summary>
        /// Append the report to a new or existing user-selected report window.
        /// </summary>
        /// <param name="rtf">The RTF to append</param>
        /// <param name="operation"></param>
        /// <param name="redoInformation"></param>
        /// <param name="preferredOutputLocation"></param>
        object ITemplateHost.OutputReport(string rtf, Operation operation, string redoInformation, object preferredOutputLocation)
        {
            // Locate the existing report window if it still exists
            IReport report;
            if (null != preferredOutputLocation)
            {
                Pane pane = (Pane)preferredOutputLocation;
                report = SelectReportWindow(pane);
            }
            else
            {
                report = PickReportWindow(true);
            }
            if (null == report)
                throw new TemplateOperationCancelledException();
            report.AppendRtfText(rtf, ActiveHelpTopic, operation, redoInformation);
            report.EnsureActive();
            return report.SelectedPane;
        }

        /// <summary>
        /// Append the data in the frame to a new or existing user-selected grid window.
        /// </summary>
        void ITemplateHost.OutputFrame(DataFrame frame, bool keepSelection, bool isFormulae, string missingIndicator, PaneAndPosition preferredOutputLocation, RelativePosition defaultPosition)
        {
            IGrid grid;
            RelativePosition writePosition = defaultPosition;

            if (keepSelection && null != activeGrid)
            {
                grid = (IGrid)activeGrid.Window;
                // If we're writing multiple outputs that won't be written over the top of each other, each one is selected after it is written.  Therefore we can use that to ensure subsequent output is written directly after the initial output.
                if (defaultPosition != RelativePosition.ReplaceSelection)
                    writePosition = RelativePosition.AfterSelection;
                else
                    writePosition = defaultPosition;
            }
            else
            {
                if (null != preferredOutputLocation)
                {
                    PaneAndPosition paneAndPosition = preferredOutputLocation;
                    Pane pane = paneAndPosition.Pane;
                    writePosition = paneAndPosition.WritePosition;
                    grid = SelectGridWindow(pane, ref writePosition);
                }
                else
                {
                    grid = PickGridWindow(true, ref writePosition);
                }
            }
            if (null == grid)
                throw new TemplateOperationCancelledException();
            grid.WriteDataFrame(frame, isFormulae, missingIndicator, writePosition);
            grid.EnsureActive();
        }

        private static void InitialiseFunctionRegistry()
        {
            BuiltinRegistry.SoleInstance.AddAll(Builtins.Registry.GetFunctionRegistry());
        }

        IScriptEngine ITemplateHost.GetScriptEngine(string Language)
        {
            // At present, all languages are handled by the ScriptEngine.  This may never change, but at least this method provides an extension point in case it does.
            return new ScriptEngine();
        }

        void ITemplateHost.PrepareParameter(ITemplateProcessor processor, Parameter parameter, ParameterBag context)
        {
            if (parameter is PickFromListParameter)
                PrepareParameter(processor, (PickFromListParameter)parameter, context);
        }

        public bool CanCombine(Parameter parameter)
        {
            if (parameter is Grid2DParameter)
                return false;
            if (parameter is GroupedCovarianceParameter)
                return false;
            if (parameter is GridParameter)
            {
                // Grids that must be entered rather than selected can be combined, as an entry grid will appear at the top.
                return !((GridParameter)parameter).CanSelect;
            }
            return true;
        }

        internal void EraseAnyOutstandingParameters()
        {
            outstandingParameters = null;
        }

        ParameterBag ITemplateHost.FillAndValidateCombinedParameters(ITemplateProcessor processor, ParameterBag context)
        {
            if (null == mainWindow)
                throw new Exception("Attempt to fill combined parameters with no main window open");
            if (null == outstandingParameters || outstandingParameters.Count == 0)
                return new ParameterBag();
            return mainWindow.FillAndValidateCombinedParameters(this, processor, context, outstandingParameters);
        }

        ParameterBag ITemplateHost.FillParameter(ITemplateProcessor processor, Parameter parameter, ParameterBag context, bool shouldCombine)
        {
            if (shouldCombine)
            {
                if (null == outstandingParameters)
                    outstandingParameters = new List<Parameter>();
                outstandingParameters.Add(parameter);
                return null;
            }

            // We can't combine the parameter.  Check whether we need to acquire it at all.
            if (parameter.HasAcquireIfTrue && !parameter.AcquireIfTrue(processor, context))
                    return null;

            // We can't combine the parameter and need to acquire it.
            while (true)
            {
                ImmediateParameterFiller filler = new ImmediateParameterFiller { context = context, processor = processor };
                parameter.Accept(filler);

                // Validate; if no errors, stop.  If there are errors, show them and go round again.
                string validationResult = null;
                if (null != parameter.Validators)
                {
                    foreach (Validator validator in parameter.Validators)
                    {
                        validationResult = TemplateProcessor.Validate(this, validator.ValidatorName, parameter, filler.outputParameters, parameter.ValidationFailMessage);
                        if (null != validationResult)
                            break;
                    }
                }
                if (null == validationResult)
                    return filler.outputParameters;

                MsgboxX(validationResult, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        static void PrepareParameter(ITemplateProcessor processor, PickFromListParameter parameter, ParameterBag context)
        {
            // TODO: Move logic out of SetOperation() into here
        }

        private ParameterBag FillChartOptions(Charting.ChartDefinition ChartDefinition, ParameterBag context)
        {
            ITemplateHost ith = this;
            TemplateProcessor processor = new TemplateProcessor(ith);
            ChartOptionsParameter chartOptionsParameter = new ChartOptionsParameter("dummy", ChartDefinition);
            ith.FillParameter(processor, chartOptionsParameter, context, true);
            return ith.FillAndValidateCombinedParameters(processor, context);
        }

        /// <summary>
        /// Returns a display value of Amount, rounded to DisplayDecimalPlaces if sensible.
        /// </summary>
        /// <returns></returns>
        public string RoundU(double amount)
        {
            return Formatting.XRound(amount, Preferences.DisplayDecimalPlaces);
        }

        public string pval(double p)
        {
            return Formatting.pval(p, Preferences.PDecimalPlaces, Preferences.UseScientificNotationForSmallPValues);
        }

        public string pval_half(double p)
        {
            return Formatting.pval_half(p, Preferences.PDecimalPlaces, Preferences.UseScientificNotationForSmallPValues);
        }

        /// <summary>
        /// An expected error has occurred.  Tell the user in a suitable manner.
        /// </summary>
        /// <param name="explanation">An explanation of what the application was doing that caused the error, in terms a user could follow</param>
        /// <param name="ex">The exception that was expected</param>
        /// <param name="showHelpButton"></param>
        internal void FriendlyError(string explanation, Exception ex, bool showHelpButton)
        {
            MsgboxX(explanation + (null == ex ? string.Empty : ("\r\n" + ex.Message)), MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "StatsDirect", showHelpButton);
        }

        public DialogResult MsgboxX(string text, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return MsgboxX(text, buttons, icon, "StatsDirect", false);
        }

        public DialogResult MsgboxX(string text, MessageBoxButtons buttons, MessageBoxIcon icon, string caption, bool showHelpButton, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
        {
            using (new DefaultCursor())
            {
                if (showHelpButton)
                    return MsgboxX(text, buttons, icon, caption, SoleInstance.ActiveHelpTopic, defaultButton);

                // Use a Windows message box if our own interface isn't visible; use our own if it is.
                if (null == mainWindow || !mainWindow.Visible || mainWindow.WindowState == FormWindowState.Minimized || ModalDialogShowing())
                    return MessageBox.Show(mainWindow, text, caption, buttons, icon, defaultButton, 0);
                return mainWindow.ShowModalMessage(text, caption, buttons, icon, defaultButton, null, HelpNavigator.TableOfContents, null);
            }
        }

        private bool ModalDialogShowing()
        {
            return null != mainWindow && ModalDialogShowing(mainWindow);
        }

        private static bool ModalDialogShowing(Form f)
        {
            // Approximate by detecting child forms of the main window and any MDI children.  Most are modal; this will therefore fail safe and occasionally show a dialog box when it could have presented in the main window.
            if (f.OwnedForms.Length > 0)
                return true;
            foreach (Form child in f.MdiChildren)
                if (ModalDialogShowing(child))
                    return true;
            return false;
        }

        public DialogResult MsgboxX(string text, MessageBoxButtons buttons, MessageBoxIcon icon, string caption, int helpTopic, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
        {
            if (null == mainWindow || !mainWindow.Visible || mainWindow.WindowState == FormWindowState.Minimized || ModalDialogShowing())
                return MessageBox.Show(mainWindow, text, caption, buttons, icon, defaultButton, 0, HelpFilePath, HelpNavigator.TopicId, helpTopic.ToString());
            return mainWindow.ShowModalMessage(text, caption, buttons, icon, defaultButton, HelpFilePath, HelpNavigator.TopicId, helpTopic.ToString());
        }

        public bool MetaPlotCI
        {
            get { return Preferences.MetaPlotCI; }
        }

        public int MetaPlotMethod
        {
            get { return Preferences.MetaPlotMethod; }
        }

        public bool GetBoolean(string prompt, string caption, bool defaultValue, out bool cancelled)
        {
            cancelled = false;
            return MsgboxX(prompt, MessageBoxButtons.YesNo, MessageBoxIcon.Question, caption, true) == DialogResult.Yes;
        }

        public bool GetBoolean(string prompt, string caption, bool defaultValue, int helpTopic, out bool Cancelled)
        {
            Cancelled = false;
            return MsgboxX(prompt, MessageBoxButtons.YesNo, MessageBoxIcon.Question, caption, helpTopic) == DialogResult.Yes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        /// <remarks>The returned bag may contain key->null pairs; it is up to the caller to handle this.</remarks>
        private ParameterBag FillSingleParameter(Parameter parameter)
        {
            ITemplateHost host = this;
            TemplateProcessor processor = new TemplateProcessor(host);
            ParameterBag context = new ParameterBag();
            host.FillParameter(processor, parameter, context, true);
            return host.FillAndValidateCombinedParameters(processor, context);
        }

        public double GetDouble(string Prompt, string caption, double defaultValue, out bool cancelled)
        {
            const string key = "solo";
            DoubleParameter parameter = new DoubleParameter
            {
                Name = key,
                PromptExpression = new Expression(Prompt),
                DefaultValueExpression = new Expression(defaultValue.ToString()),
                CancelSkipsParameter = "Skip"
            };
            ParameterBag results = FillSingleParameter(parameter);
            cancelled = (null == results || !results.ContainsKey(key) || null == results[key]);
            return cancelled ? 0.0 : results[key].AsDouble;
        }

        public int GetInteger(string prompt, string caption, int defaultValue, out bool cancelled)
        {
            const string key = "solo";
            IntegerParameter parameter = new IntegerParameter
            {
                Name = key,
                PromptExpression = new Expression(prompt),
                DefaultValueExpression = new Expression(defaultValue.ToString()),
                CancelSkipsParameter = "Skip"
            };
            ParameterBag results = FillSingleParameter(parameter);
            cancelled = (null == results || !results.ContainsKey(key) || null == results[key]);
            return cancelled ? 0 : results[key].AsInt32;
        }

        public int GetOption(string prompt, string caption, List<string> options, int selectedIndex, out bool cancelled)
        {
            const string key = "solo";
            OptionParameter parameter = new OptionParameter
            {
                Name = key,
                PromptExpression = new Expression(prompt),
                CancelSkipsParameter = "Skip"
            };
            for (int i = 0; i < options.Count; i++ )
                parameter.Options.Add(new OptionOption { Label = options[i], Value = i.ToString() });
            ParameterBag results = FillSingleParameter(parameter);
            cancelled = (null == results || !results.ContainsKey(key) || null == results[key]);
            return cancelled ? 0 : int.Parse(results[key].AsString);
        }

        public string GetString(string prompt, string caption, string defaultValue)
        {
            return Prompt(prompt, caption, defaultValue);
        }

        public void Error(string message, string caption)
        {
            MsgboxX(message, MessageBoxButtons.OK, MessageBoxIcon.Error, caption, true);
        }

        public void StartProgress(string operationDescription, bool provideProgress)
        {
            if (null != mainWindow)
                mainWindow.StartProgress(operationDescription, provideProgress);
        }

        public bool UpdateProgress(double fractionComplete)
        {
            if (null == mainWindow)
                return false;
            return mainWindow.UpdateProgress(fractionComplete);
        }

        public void FinishProgress()
        {
            if (null != mainWindow)
                mainWindow.FinishProgress();
        }

        public int PDecimalPlaces
        {
            get { return Properties.Settings.Default.PDecimalPlaces; }
        }

        public void NoteError(Exception ex)
        {
            MsgboxX("Error in calculation, report invalid.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "StatsDirect", true);
        }

        public void Warning(string message, string caption)
        {
            MsgboxX(message, MessageBoxButtons.OK, MessageBoxIcon.Warning, caption, true);
        }

        public bool Query(string message, string caption)
        {
            DialogResult result = MsgboxX(message, MessageBoxButtons.OKCancel, MessageBoxIcon.Question, caption, true);
            return DialogResult.OK == result;
        }

        public ParameterBag Amend(IFillable fillable, ParameterBag context)
        {
            switch (fillable.FillerToUse)
            {
                case "ChiSquareGoodnessOfFit":
                case "ConvertUnits":
                case "Distribution":
                case "Dummy":
                case "Extraction":
                case "GraphicsOptions":
                case "ROCCutoff":
                case "Scores":
                case "SortInPlace":
                    return AmendUsingControl(fillable);
                case "ToggleFilters":
                    return ToggleFilters();
                case "Categorise":
                    return Amend((Builtins.CategoriseOptions)fillable);
                case "ChartExplorer":
                    throw new NotImplementedException("Chart explorer is not implemented in StatsDirect 3.0");
                    // return Amend((Builtins.ChartExplorerOptions)fillable);
                case "ChartOptions":
                    return FillChartOptions((Charting.ChartDefinition)fillable, context);
                case "SummaryStatistics":
                    return Amend((Builtins.SummaryStatisticsOptions)fillable);
                default:
                    throw new ArgumentOutOfRangeException("fillable", fillable, "fillable.FillerToUse: Unknown option");
            }
        }

        private ParameterBag ToggleFilters()
        {
            if (null != mainWindow)
                mainWindow.ToggleFilters();
            return new ParameterBag();
        }

        private ParameterBag AmendUsingControl(IFillable fillable)
        {
            ITemplateHost ith = this;
            TemplateProcessor processor = new TemplateProcessor(ith);
            ParameterBag context = new ParameterBag();
            FillableParameter fillableParameter = new FillableParameter("dummy", fillable);
            ith.FillParameter(processor, fillableParameter, context, true);
            ParameterBag filledParameters = ith.FillAndValidateCombinedParameters(processor, context);
            return filledParameters;
        }

        private ParameterBag Amend(Builtins.CategoriseOptions categoriseOptions)
        {
            using (frmCategorise options = new frmCategorise(categoriseOptions))
            {
                using (new DefaultCursor())
                {
                    options.ShowDialog(mainWindow);
                }
                return options.UserCancelled ? null : new ParameterBag();
            }
        }

        /*
    private ParameterBag Amend(Builtins.ChartExplorerOptions options)
    {
        using (frmChartExplorer f = new frmChartExplorer(options))
        {
        f.ShowDialog(mainWindow);
        bool userCancelled = f.UserCancelled;
        }
        return !userCancelled;
    }
         */

        private ParameterBag Amend(Builtins.SummaryStatisticsOptions summaryStatisticsOptions)
        {
            using (frmSummaryStatistics options = new frmSummaryStatistics(summaryStatisticsOptions))
            {
                using (new DefaultCursor())
                {
                    options.ShowDialog(mainWindow);
                }
                return new ParameterBag();
            }
        }

        public void NoteEndOfSelection(bool ok)
        {
            mainWindow.NoteEndOfSelection(ok);
        }

        public bool IsSelecting
        {
            get { return mainWindow.IsSelecting; }
        }

        public SDPreferences Preferences
        {
            get { return preferences ?? (preferences = LoadPreferences()); }
        }

        public class SDPreferencesImpl : SDPreferences
        {
            public bool UseScientificNotationForSmallPValues
            {
                get
                {
                    return Properties.Settings.Default.UseScientificNotationForSmallPValues;
                }
                set
                {
                    Properties.Settings.Default.UseScientificNotationForSmallPValues = value;
                }
            }

            public bool CanDefaultConfidenceInterval
            {
                get
                {
                    return Properties.Settings.Default.CanDefaultConfidenceInterval;
                }
                set
                {
                    Properties.Settings.Default.CanDefaultConfidenceInterval = value;
                }
            }

            public double DefaultConfidenceInterval
            {
                get
                {
                    return Properties.Settings.Default.DefaultConfidenceInterval;
                }
                set
                {
                    Properties.Settings.Default.DefaultConfidenceInterval = value;
                }
            }

            public bool SelectGroupsByIdentifier
            {
                get
                {
                    return Properties.Settings.Default.SelectGroupsByIdentifier;
                }
                set
                {
                    Properties.Settings.Default.SelectGroupsByIdentifier = value;
                }
            }

            public double MetaCC
            {
                get
                {
                    return Properties.Settings.Default.MetaCC;
                }
                set
                {
                    Properties.Settings.Default.MetaCC = value;
                }
            }

            public bool MetaExact
            {
                get
                {
                    return Properties.Settings.Default.MetaExact;
                }
                set
                {
                    Properties.Settings.Default.MetaExact = value;
                }
            }

            public bool DelayContinuityCorrection
            {
                get
                {
                    return Properties.Settings.Default.DelayContinuityCorrection;
                }
                set
                {
                    Properties.Settings.Default.DelayContinuityCorrection = value;
                }
            }

            public int DisplayDecimalPlaces
            {
                get
                {
                    return Properties.Settings.Default.DisplayDecimalPlaces;
                }
                set
                {
                    Properties.Settings.Default.DisplayDecimalPlaces = value;
                }
            }

            public int PDecimalPlaces
            {
                get
                {
                    return Properties.Settings.Default.PDecimalPlaces;
                }
                set
                {
                    Properties.Settings.Default.PDecimalPlaces = value;
                }
            }

            public int MetaPlotMethod
            {
                get
                {
                    return Properties.Settings.Default.MetaPlotMethod;
                }
                set
                {
                    Properties.Settings.Default.MetaPlotMethod = value;
                }
            }

            public bool MetaPlotCI
            {
                get
                {
                    return Properties.Settings.Default.MetaPlotCI;
                }
                set
                {
                    Properties.Settings.Default.MetaPlotCI = value;
                }
            }

            public string DECP_CHAR
            {
                get
                {
                    return System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                }
            }

            public string Numeric_Thousands_Separator
            {
                get
                {
                    return System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator;
                }
            }

            public int MaxRows
            {
                get { return 64000; }
            }

            public bool ShouldKeepData
            {
                get { return Properties.Settings.Default.ShouldKeepData; }
                set { Properties.Settings.Default.ShouldKeepData = value; }
            }

            public bool ShouldUseColour
            {
                get { return Properties.Settings.Default.ShouldUseColour; }
                set { Properties.Settings.Default.ShouldUseColour = value; }
            }
        }

        private static SDPreferences LoadPreferences()
        {
            return new SDPreferencesImpl();
        }

        internal bool SelectingData
        {
            get
            {
                return null != mainWindow && mainWindow.SelectingData;
            }
        }

        public Operation Operation { get; set; }

        internal void ShowCurrentHelp()
        {
            ShowHelp(mainWindow);
        }

        /// <summary>
        /// Prompts the user for the specified information.  Returns null if they cancelled, otherwise the entered value.
        /// </summary>
        /// <param name="prompt"></param>
        /// <param name="caption"></param>
        /// <param name="defaultValue"></param>
        /// <returns>null if the user cancelled, otherwise the entered value.</returns>
        private string Prompt(string prompt, string caption, string defaultValue)
        {
            using (frmInputBox ib = new frmInputBox(prompt, caption, defaultValue))
            {
                ib.ShowDialog(mainWindow);
                if (ib.UserCancelled)
                    return null;
                return ib.Value;
            }
        }

        string ITemplateHost.zvalp1(double xz)
        {
            double P = 1 - Numerics.PDF.alnorm(xz);
            if (P > 1 - P)
                P = 1 - P;
            return pval(P);
        }

        string ITemplateHost.zvalp2(double xz)
        {
            double P = 1 - Numerics.PDF.alnorm(xz);
            if (P > 1 - P)
                P = 1 - P;
            return pval(P * 2);
        }

        internal void NoteRecentFile(string path, bool openedOk)
        {
            // Ensure the path is the most recently used and appears no more than once; ensure no more than MAX_RECENT_FILES files are kept
            System.Collections.Specialized.StringCollection recentFiles = Properties.Settings.Default.RecentFileList ?? new System.Collections.Specialized.StringCollection();
            if (recentFiles.Contains(path))
                recentFiles.Remove(path);
            if (openedOk)
            {
                recentFiles.Add(path);
                if (recentFiles.Count > MAX_RECENT_FILES)
                {
                    // Never remove the example file; keep it as the oldest entry even if that means removing a younger file
                    recentFiles.RemoveAt(recentFiles[0].Equals(SDConfiguration.MyTestFilePath) ? 1 : 0);
                }
            }
            Properties.Settings.Default.RecentFileList = recentFiles;
            if (null != mainWindow)
                mainWindow.UpdateFileList();
        }

        internal IList<string> RecentFiles
        {
            get
            {
                // Stored in reverse order (most recent last), so reverse on the way out
                System.Collections.Specialized.StringCollection recentFiles = Properties.Settings.Default.RecentFileList;
                IList<string> output = new List<string>();
                if (null == recentFiles)
                {
                    recentFiles = new System.Collections.Specialized.StringCollection();
                }

                string appPath = Path.GetDirectoryName(Application.ExecutablePath);
                if (null != appPath)
                {
                    for (int i = recentFiles.Count - 1; i >= 0; --i)
                    {
                        // Remove references to anything in the installation directory
                        if (!recentFiles[i].StartsWith(appPath))
                        {
                            output.Add(recentFiles[i]);
                        }
                    }
                }

                // If this is the first time we've been started, so there are no recently used files, add the centrally-maintained test.xlsx for this version.
                if (0 == output.Count)
                {
                    string defaultRecentlyUsedPath = SDConfiguration.MyTestFilePath;
                    output.Add(defaultRecentlyUsedPath);
                }
                return output;
            }
        }

        public void ReplayWithCurrentData(string operationName, string freezeDriedData)
        {
            ParameterBag parameters = ParameterBag.DeserializeAndRefillForRedo(freezeDriedData, this);
            if (null != parameters)
            {
                if (null != mainWindow)
                {
                    Operation operation = TemplateFactory.Operations[operationName];
                    mainWindow.DoOperation(operation, parameters, true);
                }
            }
        }

        void IRefillSource.Refill(IList<Variable> variables)
        {
            // Split up the variables, which might occasionally have come from more than one selection, into their different selections.
            Dictionary<int, List<Variable>> variablesByOriginGroup = new Dictionary<int, List<Variable>>();
            foreach (Variable variable in variables)
            {
                if (null == variable.Origin)
                    continue;
                List<Variable> variablesByThisGroup;
                if (!variablesByOriginGroup.TryGetValue(variable.Origin.OriginGroup, out variablesByThisGroup))
                {
                    variablesByThisGroup = new List<Variable>();
                    variablesByOriginGroup.Add(variable.Origin.OriginGroup, variablesByThisGroup);
                }
                variablesByThisGroup.Add(variable);
            }

            // For each selection, check they all have the same workbook (we can't handle cross-workbook selections as we hand off to an IGrid), load it and delegate the refill to it.
            foreach (List<Variable> candidates in variablesByOriginGroup.Values)
            {
                string workbookPath = null;
                bool atLeastOneFailedVariable = false;
                foreach (Variable candidate in candidates)
                {
                    if (!(candidate.Origin is WorksheetOrigin))
                    {
                        atLeastOneFailedVariable = true;
                        break;
                    }
                    WorksheetOrigin worksheetOrigin = (WorksheetOrigin)candidate.Origin;
                    if (null == worksheetOrigin.WorkbookPath)
                    {
                        atLeastOneFailedVariable = true;
                        break;
                    }
                    if (null == workbookPath)
                        workbookPath = worksheetOrigin.WorkbookPath;
                    else
                    {
                        if (!(workbookPath.Equals(worksheetOrigin.WorkbookPath)))
                        {
                            atLeastOneFailedVariable = true;
                            break;
                        }
                    }
                }
                if (atLeastOneFailedVariable)
                {
                    // Can't refill this
                    break;
                }

                StatsDirectForm gridWindow = mainWindow.FindOrOpenGrid(workbookPath);
                if (null == gridWindow)
                {
                    FriendlyError("Cannot replay the operation as it took data from the unsaved workbook \"" + workbookPath + "\", which is no longer open.", null, false);
                    throw new TemplateOperationCancelledException();
                }
                IGrid grid = (IGrid)gridWindow;
                grid.Refill(candidates);
            }
        }

        public IDictionary<string, ParameterBag> SessionParametersPerOperation
        {
            get { return sessionParametersPerOperation ?? (sessionParametersPerOperation = new Dictionary<string, ParameterBag>()); }
        }

        public ParameterBag SessionParametersAcrossOperations
        {
            get { return sessionParametersAcrossOperations ?? (sessionParametersAcrossOperations = new ParameterBag()); }
        }

        public IDictionary<string, object> Session
        {
            get { return session ?? (session = new Dictionary<string, object>()); }
        }

        public string TemplateFileForNewReports
        {
            get
            {
                return Path.Combine(SDConfiguration.TemplatePath, "blank.rtf");
            }
        }

        internal void ClearBatchMode()
        {
            // Ensure no windows might remember anything to do with batching
            foreach (WindowInformation wi in windows)
            {
                if (wi.HasWindow)
                    wi.Window.ClearBatchMode();
            }
        }

        public void CheckForUpdates()
        {
            ShowOrQueueDialog(new frmUpdateCheck(false), null);
        }

        internal void CloseAndUpdate()
        {
            if (null == mainWindow)
            {
                FetchTheUpgrade();
                Application.Exit();
            }
            else
            {
                closingForUpgrade = true;
                mainWindow.Close();
            }
        }

        public bool ClosingForUpgrade
        {
            get { return closingForUpgrade; }
        }

        public static bool IsRunningOnMono
        {
            get { return Type.GetType("Mono.Runtime") != null; }
        }
    }
}
