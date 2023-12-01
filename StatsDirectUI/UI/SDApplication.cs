using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Forms;

using StatsDirect.Configuration;
using StatsDirect.Data;
using StatsDirect.Templates;
using StatsDirect.TemplateProcessing;
using StatsDirect.Utilities;
using StatsDirect.Charting;

namespace StatsDirect.UI
{
    /// <summary>
    /// The central class for managing the state of StatsDirect.
    /// </summary>
    public sealed class SdApplication : ITemplateHost, ISdApplication, ISession
    {
        private const int MAX_RECENT_FILES = 7;

        public WindowInformation? ActiveGrid { get; private set; }
        private int sActiveHelpTopic;
        private string? activeHelpUrl;
        public WindowInformation? ActiveWindow { get; private set; }
        public bool ClosingForUpgrade { get; private set; }

        /// <summary>
        /// The MDI window in which newly-created children are placed
        /// </summary>
        private frmMain MainWindow { get; }
        PaneAndPosition? ISdApplication.MostRecentlySelectedReport { get; set; }
        private List<Parameter>? outstandingParameters;
        private ParameterBag? sessionParametersAcrossOperations;
        private Dictionary<string, ParameterBag>? sessionParametersPerOperation;
        private ISet<WindowInformation> Windows { get; } = new HashSet<WindowInformation>();

        private IChartPreferences ChartPreferences { get; }
        private IChartRendererFactory ChartRendererFactory { get; }
        private ISdPreferences SdPreferences { get; }
        private ITemplateProcessorFactory TemplateProcessorFactory { get; }
        private IUiPreferences UiPreferences { get; }

        private readonly Queue<DialogAndAction> queuedDialogs;
        /// <summary>
        /// If true, the UI is presently showing a dialog that was submitted using QueueDialog.
        /// </summary>
        private bool showingDialogThatCouldBeQueued;

        private ITemplateHost TemplateHost => this;

        public SdApplication(IChartPreferences chartPreferences, IChartRendererFactory chartRendererFactory, ISdPreferences sdPreferences, ITemplateProcessorFactory templateProcessorFactory, IUiPreferences uiPreferences)
        {
            ChartPreferences = chartPreferences;
            ChartRendererFactory = chartRendererFactory;
            SdPreferences = sdPreferences;
            TemplateProcessorFactory = templateProcessorFactory;
            UiPreferences = uiPreferences;

            queuedDialogs = new Queue<DialogAndAction>();
            InitialiseFunctionRegistry();
            LoadPersistentValues();
            MainWindow = new frmMain(chartPreferences, chartRendererFactory, this, SdPreferences, this, templateProcessorFactory, uiPreferences);
        }

        void ISdApplication.NoteASubformCloseIsCancelled() => MainWindow.NoteASubformCloseIsCancelled();

        void ISdApplication.NoteASubformCloseIsStarting() => MainWindow.NoteASubformCloseIsStarting();

        /// <summary>
        /// On occasion, we have the potential for dialogs created in a background thread to be displayable while another dialog is on-screen.  Show this dialog if possible, but force a queue so that no more than one dialog is on-screen at one time.
        /// </summary>
        /// <param name="f">The dialog to display once any current dialog has closed.</param>
        /// <param name="postDisplayAction">Code to be run on the UI thread when the form closes; takes the form as its first parameter.</param>
        void ISdApplication.ShowOrQueueDialog(Form f, Action<Form, DialogResult>? postDisplayAction)
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

        bool ISdApplication.OpenFile() => MainWindow.OpenFile();

        bool ISdApplication.OpenFile(string path, bool removeFromRecentFilesIfNotFound) => MainWindow.OpenFile(path, removeFromRecentFilesIfNotFound);

        StatsDirectForm ISdApplication.CreateReport() => MainWindow.CreateReport();

        StatsDirectForm ISdApplication.CreateGrid() => MainWindow.CreateGrid();

        void ISdApplication.Run() => Application.Run(MainWindow);

        /// <summary>
        /// We know we're safe to show a dialog now (whether immediate or whether we just got it off the queue).  Show it on the UI thread, wait for a user response, and if necessary run its post display action (also on the UI thread).
        /// If another dialog has been queued while we're showing this one, show that.
        /// </summary>
        private void ShowDialogOnUiThread(Form f, Action<Form, DialogResult>? postDisplayAction)
        {
            if (MainWindow.InvokeRequired)
            {
                MainWindow.Invoke(new Action(() => {
                    DialogResult result = f.ShowDialog(MainWindow);
                    postDisplayAction?.Invoke(f, result);
                    f.Dispose();
                }));
            }
            else
            {
                DialogResult result = f.ShowDialog(MainWindow);
                postDisplayAction?.Invoke(f, result);
                f.Dispose();
            }

            // If there's another dialog ready to go, dequeue and show it.  Yes, this uses tail-recursion; the compiler can choose to optimise it away, or we can rely on the fact that this is a rare operation and hence the stack won't get deep.
            DialogAndAction? newDialog = null;
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
            string loadPath = SDConfiguration.PersistentValueFilePath;
            if (File.Exists(loadPath))
            {
                try
                {
                    BinaryFormatter fmt = new();
                    using Stream ws = new FileStream(loadPath, FileMode.Open, FileAccess.Read);
                    sessionParametersAcrossOperations = (ParameterBag)fmt.Deserialize(ws);
                    sessionParametersPerOperation = (Dictionary<string, ParameterBag>)fmt.Deserialize(ws);
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
        void ISdApplication.Shutdown()
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
                string savePath = SDConfiguration.PersistentValueFilePath;
                BinaryFormatter fmt = new();
                using Stream ws = new FileStream(savePath, FileMode.Create, FileAccess.Write);
                if (sessionParametersAcrossOperations is null)
                    sessionParametersAcrossOperations = new ParameterBag();
                fmt.Serialize(ws, sessionParametersAcrossOperations);
                if (sessionParametersPerOperation is null)
                    sessionParametersPerOperation = new Dictionary<string, ParameterBag>();
                fmt.Serialize(ws, sessionParametersPerOperation);
                // #760
                Application.DoEvents();
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
        int ISdApplication.GetGridNumber()
        {
            // Horribly inefficient O(n^2) algorithm, but we're relying on there rarely being more than a few windows open.
            int candidateNumber = 1;
            while (true)
            {
                bool acceptable = true;
                foreach (WindowInformation wi in Windows)
                {
                    if (wi.HasWindow && wi.Window is IGrid)
                    {
                        string? windowName = wi.FriendlyName;
                        if (null != windowName && windowName.StartsWith("Data "))
                        {
                            string windowNumberAsString = windowName[5..].Trim();
                            if (int.TryParse(windowNumberAsString, out int windowNumber))
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
        int ISdApplication.GetReportNumber()
        {
            // Horribly inefficient O(n^2) algorithm, but we're relying on there rarely being more than a few windows open.
            int candidateNumber = 1;
            while (true)
            {
                bool acceptable = true;
                foreach (WindowInformation wi in Windows)
                {
                    if (wi.Window is not IReport)
                        continue;

                    string? windowName = wi.FriendlyName;
                    if (windowName is null || !windowName.StartsWith("Report "))
                        continue;

                    // We don't want to load Report 1.rtf and create Report 1 again (#673).  Strip any suffix before comparison.
                    if (Path.HasExtension(windowName))
                        windowName = Path.GetFileNameWithoutExtension(windowName);
                    string windowNumberAsString = windowName[7..].Trim();
                    if (int.TryParse(windowNumberAsString, out int windowNumber))
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
        int ISdApplication.GetScriptWindowNumber()
        {
            // Horribly inefficient O(n^2) algorithm, but we're relying on there rarely being more than a few windows open.
            int candidateNumber = 1;
            while (true)
            {
                bool acceptable = true;
                foreach (WindowInformation wi in Windows)
                {
                    if (wi.Window is IScriptWindow)
                    {
                        string? windowName = wi.FriendlyName;
                        if (windowName.StartsWith("Script "))
                        {
                            string windowNumberAsString = windowName[7..].Trim();
                            if (int.TryParse(windowNumberAsString, out int windowNumber))
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

        public void AddWindow(WindowInformation info)
        {
            Windows.Add(info);
        }

        internal void RemoveWindow(WindowInformation info)
        {
            if (Windows.Contains(info))
                Windows.Remove(info);
        }

        /// <summary>
        /// A form has found itself closing by some means and has informed us.
        /// Make sure other features of the interface related to that form are tidied up, and remove our memory of the form.
        /// </summary>
        /// <param name="window"></param>
        /// <param name="e"></param>
        void ISdApplication.NoteFormClosing(StatsDirectForm window, FormClosingEventArgs e)
        {
            WindowInformation info = (WindowInformation)window.Tag;
            if (null != info)
            {
                if (null != info.TabPage)
                    MainWindow?.RemoveWindow(window);

                // Break reference cycles
                info.Window = null;
                info.TabPage = null;
                Windows.Remove(info);
            }
        }

        /// <summary>
        /// A window has found itself activated.  Ensure the tabs are synchronised.
        /// </summary>
        void ISdApplication.NoteFormActivated(WindowInformation info)
        {
            ActiveWindow = info;
            if (null != info.Window)
            {
                if (info.Window is IGrid)
                    ActiveGrid = info;
                MainWindow.EnsureTabSelected(info.TabPage);
                MainWindow.SetMenuVisibility(info.Window is IGrid);
            }
        }

        void ISdApplication.ShowHelp(Form parent, string topic)
        {
            Help.ShowHelp(parent, SDConfiguration.HelpFilePath, HelpNavigator.TopicId, topic);
        }

        void ISdApplication.ShowHelp(Form parent)
        {
            if (null != ActiveHelpUrl)
            {
                // Show the URL
                Help.ShowHelp(parent, ActiveHelpUrl);
            }
            else if (0 != ActiveHelpTopic)
            {
                // Specific help - show it.
                Help.ShowHelp(parent, SDConfiguration.HelpFilePath, HelpNavigator.TopicId, ActiveHelpTopic.ToString());
            }
            else
            {
                // Nothing in particular, guess something useful or show the ToC if we can't.
                if (ActiveWindow is not null && ActiveWindow.HasWindow && ActiveWindow.Window is IGrid)
                {
                    // Grid - show the worksheet help, which is 1040.
                    Help.ShowHelp(parent, SDConfiguration.HelpFilePath, HelpNavigator.TopicId, "1040");
                }
                else
                {
                    Help.ShowHelp(parent, SDConfiguration.HelpFilePath, HelpNavigator.TableOfContents);
                }
            }
        }

        public int ActiveHelpTopic
        {
            get => sActiveHelpTopic;
            set { sActiveHelpTopic = value; activeHelpUrl = null; }
        }

        public string? ActiveHelpUrl
        {
            get => activeHelpUrl;
            set { sActiveHelpTopic = 0; activeHelpUrl = value; }
        }

        internal IList<Pane> AvailableReportPanes()
        {
            IList<Pane> availableWindows = new List<Pane>();
            foreach (WindowInformation info in Windows)
                if (info.Window is IReport)
                    foreach (Pane pane in info.Window.AvailablePanes)
                        availableWindows.Add(pane);
            return availableWindows;
        }

        void ISdApplication.DoOperationOnceOrUntilCancelled(Operation operation, ParameterBag? parameterBag) =>
            MainWindow.DoOperationOnceOrUntilCancelled(operation, parameterBag);

        IList<PaneAndPosition> ISdApplication.AvailableReportPanesAndPositions()
        {
            IList<PaneAndPosition> availableWindows = new List<PaneAndPosition>();
            foreach (Pane pane in AvailableReportPanes())
                availableWindows.Add(new PaneAndPosition(pane, RelativePosition.LastColumn));
            return availableWindows;
        }

        IList<PaneAndPosition> ISdApplication.AvailableFramePanesAndPositions()
        {
            // PaneAndBoolean mostRecent = MostRecentlySelectedGrid;
            Pane? mostRecentPane = null;
            if (null != ActiveGrid && ActiveGrid.HasWindow)
                mostRecentPane = ActiveGrid.Window?.SelectedPane;
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
            foreach (WindowInformation info in Windows)
                if (info.Window is IGrid)
                    foreach (Pane pane in info.Window.AvailablePanes)
                        availableWindows.Add(pane);
            return availableWindows;
        }

        /// <summary>
        /// Allows the user to select from existing report windows, plus potentially a new one.
        /// </summary>
        /// <param name="AllowNew">If true, the user may select a new report as well as any existing ones.  If false, only existing reports may be picked.</param>
        public IReport? PickReportWindow(bool allowNew)
        {
            return MainWindow.SelectedReportWindow;
        }

        internal IReport? SelectReportWindow(Pane selectedPane)
        {
            // As we now don't remember reports for output, this is equivalent to a PickReportWindow.
            return PickReportWindow(false);
        }

        /// <summary>
        /// Allows the user to pick from existing grid windows, plus potentially a new one.
        /// </summary>
        /// <param name="allowNew">If true, the user may select a new grid as well as any existing ones.  If false, only existing grids may be picked.</param>
        /// <param name="relativePosition"></param>
        public IGrid? PickGridWindow(bool allowNew, ref RelativePosition relativePosition)
        {
            IList<Pane> availableWindows = AvailableFramePanes();
            try
            {
                Pane? selectedPane = null;
                if (availableWindows.Count > 0)
                {
                    const string KEY = "solo";
                    SpecialParameter parameter = new()
                    {
                        SpecialType = "frame",
                        Name = KEY,
                        ExtraData = new object[] { relativePosition },
                        PromptExpression = new Expression("Pick the sheet in which you want the output to appear")
                    };
                    ParameterBag results = FillSingleParameter(parameter); // Will never return a null value as the parameter cannot be skipped
                    bool cancelled = results is null || !results.ContainsKey(KEY);
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

        private IGrid? SelectGridWindow(Pane? selectedPane, ref RelativePosition writePosition)
        {
            if (selectedPane?.WindowInformation is null)
            {
                // Create a new grid, write at the end of it
                IGrid grid = (IGrid)MainWindow.CreateGrid();
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
        object IUserInterface.OutputReport(IRenderable renderable, Operation operation, object? preferredOutputLocation)
        {
            // Locate the existing report window if it still exists
            IReport? report;
            if (preferredOutputLocation is not null)
            {
                Pane pane = (Pane)preferredOutputLocation;
                report = SelectReportWindow(pane);
            }
            else
            {
                report = PickReportWindow(true);
            }
            if (report is null)
                throw new TemplateOperationCancelledException();
            report.AppendRenderable(renderable, ActiveHelpTopic, operation);
            report.EnsureActive();
            return report.SelectedPane;
        }

        /// <summary>
        /// Append the data in the frame to a new or existing user-selected grid window.
        /// </summary>
        void IUserInterface.OutputFrame(DataFrame frame, bool keepSelection, bool isFormulae, string missingIndicator, PaneAndPosition? preferredOutputLocation, RelativePosition defaultPosition)
        {
            IGrid? grid;
            RelativePosition writePosition = defaultPosition;

            if (keepSelection && ActiveGrid is not null)
            {
                grid = (IGrid)ActiveGrid.Window;
                // If we're writing multiple outputs that won't be written over the top of each other, each one is selected after it is written.  Therefore we can use that to ensure subsequent output is written directly after the initial output.
                writePosition = defaultPosition != RelativePosition.ReplaceSelection ? RelativePosition.AfterSelection : defaultPosition;
            }
            else
            {
                if (preferredOutputLocation is not null)
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
            if (grid is null)
                throw new TemplateOperationCancelledException();
            grid.WriteDataFrame(frame, isFormulae, missingIndicator, writePosition);
            grid.EnsureActive();
        }

        public void InitialiseFunctionRegistry()
        {
            BuiltinRegistry.SoleInstance.AddAll(new Builtins.Registry(ChartPreferences, ChartRendererFactory, this, SdPreferences, this).GetFunctionRegistry());
        }

        bool IScriptEngineHost.TryGetScriptEngine(string language, [NotNullWhen(true)] out IScriptEngine? scriptEngine)
        {
            if (ScriptEngine.CanHandle(language))
            {
                scriptEngine = new ScriptEngine();
                return true;
            }
            scriptEngine = null;
            return false;
        }

        void IUserInterface.PrepareParameter(Parameter parameter, ParameterBag context)
        {
            if (parameter is PickFromListParameter pickFromListParameter)
                PrepareParameter(pickFromListParameter, context);
        }

        bool IUserInterface.CanCombine(Parameter parameter) =>
            parameter switch
            {
                Frame2DParameter => false,
                GroupedCovarianceParameter => false,
                FrameParameter frameParameter => !frameParameter.CanSelect,// Grids that must be entered rather than selected can be combined, as an entry grid will appear at the top.
                _ => true,
            };

        void ISdApplication.EraseAnyOutstandingParameters()
        {
            outstandingParameters = null;
        }

        ParameterBag? IUserInterface.FillAndValidateCombinedParameters(ParameterBag context)
        {
            if (MainWindow is null)
                throw new Exception("Attempt to fill combined parameters with no main window open");
            if (outstandingParameters is null || outstandingParameters.Count == 0)
                return new ParameterBag();
            return MainWindow.FillAndValidateCombinedParameters(context, outstandingParameters);
        }

        ParameterBag? IUserInterface.FillParameter(Parameter parameter, ParameterBag context, bool shouldCombine)
        {
            if (shouldCombine)
            {
                outstandingParameters ??= new List<Parameter>();
                outstandingParameters.Add(parameter);
                return null;
            }

            // We can't combine the parameter.  Check whether we need to acquire it at all.
            if (parameter.HasAcquireIfTrue && !parameter.AcquireIfTrue(TemplateProcessorFactory.CreateTemplateProcessor(), context))
                    return null;

            // We can't combine the parameter and need to acquire it.
            bool lastHadValidationError = false;
            while (true)
            {
                ImmediateParameterFiller filler = new(context, lastHadValidationError, this, SdPreferences, TemplateProcessorFactory, UiPreferences, this);
                parameter.Accept(filler);

                // Validate; if no errors, stop.  If there are errors, show them and go round again.
                string? validationResult = null;
                if (parameter.Validators is not null)
                {
                    foreach (Validator validator in parameter.Validators)
                    {
                        validationResult = Validate(validator, parameter, filler.OutputParameters, parameter.ValidationFailMessage);
                        if (null != validationResult)
                            break;
                    }
                }
                if (validationResult is null)
                    return filler.OutputParameters;

                MsgboxX(validationResult, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                lastHadValidationError = true;
            }
        }

        public string? Validate(Validator validator, Parameter parameter, ParameterBag filledParameters, string failedValidationMessage)
        {
            ValidationResult validationResult = ValidationProcessor.Validate(this, validator.ValidatorName, parameter, filledParameters, failedValidationMessage);
            switch (validationResult.Validity)
            {
                case Validity.Valid:
                    return null;
                case Validity.Invalid:
                    return validationResult.FailedValidationMessage;
                case Validity.NeedMoreInformation:
                    bool result = ((ISdApplication)this).GetBoolean(validationResult.PromptForMoreInformation, validationResult.TitleForMoreInformation, false, validationResult.HelpContextId, out bool wasCancelled);
                    if (wasCancelled)
                        throw new TemplateOperationCancelledException();
                    ValidationAction validationAction = result ? validationResult.ActionOnMoreInformationYes : validationResult.ActionOnMoreInformationNo;
                    return validationAction switch
                    {
                        ValidationAction.CancelOperation => throw new TemplateOperationCancelledException(),
                        ValidationAction.RequestAgain => validationResult.FailedValidationMessage,
                        ValidationAction.UseAsIs => null,
                        _ => throw new Exception("Unknown validation action requested"),
                    };
                default:
                    throw new Exception("Unknown validity");
            }
        }

        static void PrepareParameter(PickFromListParameter parameter, ParameterBag context)
        {
            // TODO: Move logic out of SetOperation() into here
        }

        private ParameterBag? FillChartOptions(Charting.ChartDefinition ChartDefinition, ParameterBag context)
        {
            ChartOptionsParameter chartOptionsParameter = new("dummy", ChartDefinition);
            TemplateHost.FillParameter(chartOptionsParameter, context, true);
            return TemplateHost.FillAndValidateCombinedParameters(context);
        }

        void ISdApplication.NoteEndOfSelection(bool ok)
        {
            MainWindow.NoteEndOfSelection(ok);
        }

        bool ISdApplication.SelectCells(string fullSelectionMessage, string cancelButtonLabel, bool canSelectMultipleRows, bool allowUserToPivot, out bool wasPivoted)
        {
            MainWindow.CanSelectMultipleRows = canSelectMultipleRows;
            MainWindow.CanSelectGroupMethod = allowUserToPivot;
            if (allowUserToPivot)
                MainWindow.GroupsByIdentifier = UiPreferences.SelectGroupsByIdentifier;
            return MainWindow.SelectCells(fullSelectionMessage, cancelButtonLabel, out wasPivoted);
        }

        /// <summary>
        /// An expected error has occurred.  Tell the user in a suitable manner.
        /// </summary>
        /// <param name="explanation">An explanation of what the application was doing that caused the error, in terms a user could follow</param>
        /// <param name="ex">The exception that was expected</param>
        /// <param name="showHelpButton"></param>
        void ISdApplication.FriendlyError(string explanation, Exception ex, bool showHelpButton)
        {
            MsgboxX(explanation + (ex is null ? string.Empty : Environment.NewLine + ex.Message), MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "StatsDirect", showHelpButton);
            if (null != ex)
                LastChanceCatcher.WriteToBlackbox(explanation, ex);
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
                    return MsgboxX(text, buttons, icon, caption, ActiveHelpTopic, defaultButton);

                // Use a Windows message box if our own interface isn't visible; use our own if it is.
                if (MainWindow is null || !MainWindow.Visible || MainWindow.WindowState == FormWindowState.Minimized || ModalDialogShowing())
                    return MessageBox.Show(MainWindow, text, caption, buttons, icon, defaultButton, 0);
                return MainWindow.ShowModalMessage(text, caption, buttons, icon, defaultButton, null, HelpNavigator.TableOfContents, null);
            }
        }

        private bool ModalDialogShowing() => null != MainWindow && ModalDialogShowing(MainWindow);

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
            if (MainWindow is null || !MainWindow.Visible || MainWindow.WindowState == FormWindowState.Minimized || ModalDialogShowing())
                return MessageBox.Show(MainWindow, text, caption, buttons, icon, defaultButton, 0, SDConfiguration.HelpFilePath, HelpNavigator.TopicId, helpTopic.ToString());
            return MainWindow.ShowModalMessage(text, caption, buttons, icon, defaultButton, SDConfiguration.HelpFilePath, HelpNavigator.TopicId, helpTopic.ToString());
        }

        /// <summary>
        /// TODO: This is in ISdApplication and in IUserInterface, and probably shouldn't be.
        /// </summary>
        public bool GetBoolean(string prompt, string caption, bool defaultValue, out bool cancelled)
        {
            cancelled = false;
            return MsgboxX(prompt, MessageBoxButtons.YesNo, MessageBoxIcon.Question, caption, true) == DialogResult.Yes;
        }

        bool ISdApplication.GetBoolean(string prompt, string caption, bool defaultValue, int helpTopic, out bool Cancelled)
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
        private ParameterBag? FillSingleParameter(Parameter parameter)
        {
            ITemplateHost host = this;
            ParameterBag context = new();
            host.FillParameter(parameter, context, true);
            return host.FillAndValidateCombinedParameters(context);
        }

        int ISdApplication.GetInteger(string prompt, string caption, int defaultValue, out bool cancelled)
        {
            const string key = "solo";
            IntegerParameter parameter = new()
            {
                Name = key,
                PromptExpression = new Expression(prompt),
                DefaultValueExpression = new Expression(defaultValue.ToString()),
                CancelSkipsParameter = "Skip"
            };
            ParameterBag? results = FillSingleParameter(parameter);
            cancelled = results is null || !results.ContainsKey(key) || null == results[key];
            return cancelled
                ? 0
                : results[key].AsInt32;
        }

        string? ISdApplication.GetString(string prompt, string caption, string defaultValue) => Prompt(prompt, caption, defaultValue);

        /// <summary>
        /// TODO: This is in ISdApplication and in IUserInterface, and probably shouldn't be.
        /// </summary>
        public void Error(string message, string caption) => MsgboxX(message, MessageBoxButtons.OK, MessageBoxIcon.Error, caption, true);

        IProgressBar IProgressBarHost.StartProgress(string operationDescription, bool provideProgress, bool display)
        {
            if (display)
                MainWindow?.StartProgress(operationDescription, provideProgress);
            return new SdProgressBarHolder(display, this);
        }

        private bool UpdateProgress(double fractionComplete) => null != MainWindow && MainWindow.UpdateProgress(fractionComplete);

        private void FinishProgress() => MainWindow?.FinishProgress();

        void IUserInterface.Warning(string message, string caption)
        {
            MsgboxX(message, MessageBoxButtons.OK, MessageBoxIcon.Warning, caption, true);
        }

        public bool Query(string message, string caption)
        {
            DialogResult result = MsgboxX(message, MessageBoxButtons.OKCancel, MessageBoxIcon.Question, caption, true);
            return DialogResult.OK == result;
        }

        ParameterBag? IUserInterface.Amend(IFillable fillable, ParameterBag context)
        {
            return fillable.FillerToUse switch
            {
                "ChiSquareGoodnessOfFit" or "ConvertUnits" or "Distribution" or "Dummy" or "Extraction" or "GraphicsOptions" or "ROCCutoff" or "Scores" or "SortInPlace" => AmendUsingControl(fillable),
                "ToggleFilters" => ToggleFilters(),
                "Categorise" => Amend((Builtins.CategoriseOptions)fillable),
                "ChartExplorer" => throw new NotImplementedException("Chart explorer is not implemented in StatsDirect 3.0"),
                "ChartOptions" => FillChartOptions((Charting.ChartDefinition)fillable, context),
                "SummaryStatistics" => Amend((Builtins.SummaryStatisticsOptions)fillable),
                _ => throw new ArgumentOutOfRangeException(nameof(fillable), fillable, "fillable.FillerToUse: Unknown option"),
            };
        }

        private ParameterBag ToggleFilters()
        {
            MainWindow?.ToggleFilters();
            return new ParameterBag();
        }

        private ParameterBag? AmendUsingControl(IFillable fillable)
        {
            ParameterBag context = new();
            FillableParameter fillableParameter = new("dummy", fillable);
            TemplateHost.FillParameter(fillableParameter, context, true);
            ParameterBag? filledParameters = TemplateHost.FillAndValidateCombinedParameters(context);
            return filledParameters;
        }

        private ParameterBag? Amend(Builtins.CategoriseOptions categoriseOptions)
        {
            using frmCategorise options = new(categoriseOptions, this);
            using (new DefaultCursor())
            {
                options.ShowDialog(MainWindow);
            }
            return options.UserCancelled ? null : new ParameterBag();
        }

        private ParameterBag Amend(Builtins.SummaryStatisticsOptions summaryStatisticsOptions)
        {
            using frmSummaryStatistics options = new(summaryStatisticsOptions);
            using (new DefaultCursor())
            {
                options.ShowDialog(MainWindow);
            }
            return new ParameterBag();
        }

        bool ISdApplication.SelectingData => null != MainWindow && MainWindow.SelectingData;

        public Operation? Operation { get; set; }

        void ISdApplication.ShowCurrentHelp()
        {
            ((ISdApplication)this).ShowHelp(MainWindow);
        }

        /// <summary>
        /// Prompts the user for the specified information.  Returns null if they cancelled, otherwise the entered value.
        /// </summary>
        /// <param name="prompt"></param>
        /// <param name="caption"></param>
        /// <param name="defaultValue"></param>
        /// <returns>null if the user cancelled, otherwise the entered value.</returns>
        private string? Prompt(string prompt, string caption, string defaultValue)
        {
            using frmInputBox ib = new(prompt, caption, defaultValue, this);
            ib.ShowDialog(MainWindow);
            if (ib.UserCancelled)
                return null;
            return ib.Value;
        }

        void ISdApplication.NoteRecentFile(string path, bool openedOk)
        {
            // Ensure the path is the most recently used and appears no more than once; ensure no more than MAX_RECENT_FILES files are kept
            IList<string> recentFiles = UiPreferences.RecentFileList ?? new List<string>();
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
            UiPreferences.RecentFileList = recentFiles;
            MainWindow?.UpdateFileList();
        }

        IReadOnlyList<string> ISdApplication.RecentFiles
        {
            get
            {
                // Stored in reverse order (most recent last), so reverse on the way out
                IList<string> recentFiles = UiPreferences.RecentFileList ?? new List<string>();
                List<string> output = new();

                string? appPath = Path.GetDirectoryName(Application.ExecutablePath);
                if (null != appPath)
                {
                    for (int i = recentFiles.Count - 1; i >= 0; --i)
                    {
                        // Remove references to anything in the installation directory
                        if (!recentFiles[i].StartsWith(appPath))
                            output.Add(recentFiles[i]);
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

        IDictionary<string, ParameterBag> ISession.SessionParametersPerOperation => sessionParametersPerOperation ??= new Dictionary<string, ParameterBag>();

        ParameterBag ISession.SessionParametersAcrossOperations => sessionParametersAcrossOperations ??= new ParameterBag();

        public void ClearBatchMode()
        {
            // Ensure no windows might remember anything to do with batching
            foreach (WindowInformation wi in Windows)
            {
                if (wi.HasWindow)
                    wi.Window.ClearBatchMode();
            }
        }

        void ISdApplication.CheckForUpdates()
        {
            ((ISdApplication)this).ShowOrQueueDialog(new frmUpdateCheck(false, this), null);
        }

        void ISdApplication.CloseAndUpdate()
        {
            if (MainWindow is null)
            {
                FetchTheUpgrade();
                Application.Exit();
            }
            else
            {
                ClosingForUpgrade = true;
                MainWindow.Close();
            }
        }

        void ISdApplication.EnsureBuiltInMenuItemsCanShowHelp(MenuStrip menuStrip)
        {
            MainWindow.EnsureBuiltInMenuItemsCanShowHelp(menuStrip);
        }

        Form ISdApplication.DialogOwner => MainWindow;

        bool ISdApplication.IsRunningOnMono => Type.GetType("Mono.Runtime") is not null;

        public bool InOperation => MainWindow.InOperation;

        void ISdApplication.DoOperation(string operationName)
        {
            MainWindow.DoOperation(operationName);
        }

        bool ISdApplication.IsSelecting => MainWindow.IsSelecting;

        Form ISdApplication.ActiveMdiChild => MainWindow.ActiveMdiChild;

        internal void OpenFileOnUiThread(string path)
        {
            if (MainWindow.InvokeRequired)
                MainWindow.Invoke(new Action(() => ((ISdApplication)this).OpenFile(path, false)));
        }

        void ISdApplication.PuntThroughEventLoop(Exception ex) => MainWindow.PuntThroughEventLoop(ex);

        WindowInformation? ISdApplication.FindWindowInformationForPath(string path)
        {
            foreach (WindowInformation wi in Windows)
                if (wi.IsFile(path))
                    return wi;
            return null;
        }

        private class SdProgressBarHolder : IProgressBar
        {
            private bool Display { get; }
            private SdApplication SdApplication { get; }

            public SdProgressBarHolder(bool display, SdApplication sdApplication)
            {
                Display = display;
                SdApplication = sdApplication;
            }

            public void Finish()
            {
                if (Display)
                    SdApplication.FinishProgress();
            }

            bool IProgressBar.Update(double fractionComplete)
            {
                return Display && SdApplication.UpdateProgress(fractionComplete);
            }

            #region IDisposable Support
            private bool disposedValue = false; // To detect redundant calls

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                        Finish();
                    disposedValue = true;
                }
            }

            // This code added to correctly implement the disposable pattern.
            void IDisposable.Dispose()
            {
                // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                Dispose(true);
            }
            #endregion
        }
    }
}
