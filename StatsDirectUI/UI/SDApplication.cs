using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

using StatsDirect.Templates;
using StatsDirect.Configuration;
using StatsDirect.Data;
using StatsDirect.TemplateProcessing;
using StatsDirect.Utilities;
using System.Text;
using System.Collections;

namespace StatsDirect.UI
{
    /// <summary>
    /// The central class for managing the state of StatsDirect.
    /// </summary>
    /// <remarks>This class is a Singleton (ref Gamma et al "Design Patterns")</remarks>
    public sealed class SdApplication : ITemplateHost
    {
        private const int MAX_RECENT_FILES = 7;

        private int sActiveHelpTopic;
        private string activeHelpUrl;
        private static SdApplication soleInstance;

        // public PaneAndBoolean MostRecentlySelectedGrid { get; set; }
        public PaneAndPosition MostRecentlySelectedReport { get; set; }

        private SDPreferences preferences;

        private List<Parameter> outstandingParameters;

        private Dictionary<string, ParameterBag> sessionParametersPerOperation;

        private ParameterBag sessionParametersAcrossOperations;

        internal void NoteASubformCloseIsStarting()
        {
            MainWindow.NoteASubformCloseIsStarting();
        }

        private readonly Queue<DialogAndAction> queuedDialogs;
        /// <summary>
        /// If true, the UI is presently showing a dialog that was submitted using QueueDialog.
        /// </summary>
        private bool showingDialogThatCouldBeQueued;
        private static readonly object soleInstanceGate = new();

        /// <summary>
        /// Returns the single instance of the application, creating it if necessary.
        /// </summary>
        internal static SdApplication SoleInstance
        {
            get
            {
                Contract.Ensures(null != Contract.Result<SdApplication>());
                // The check for a new version at start-up reaches this from a thread-pool thread while the main thread may be
                // creating the instance: without the lock each could create its own, and a dialog waiting in the other one was lost.
                if (soleInstance is null)
                {
                    lock (soleInstanceGate)
                        soleInstance ??= new SdApplication();
                }
                return soleInstance;
            }
        }

        internal static ITemplateHost TemplateHost => SoleInstance;

        internal static bool HasInstance => null != soleInstance;

        /// <summary>
        /// Sole constructor.  Because this follows the singleton pattern, the constructor is private.
        /// </summary>
        private SdApplication()
        {
            queuedDialogs = new Queue<DialogAndAction>();
            InitialiseFunctionRegistry();
        }

        internal void NoteASubformCloseIsCancelled()
        {
            MainWindow.NoteASubformCloseIsCancelled();
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

        private readonly List<Form> dialogsWaitingForMainWindow = new();
        private bool mainWindowShown;

        /// <summary>
        /// Shows a dialog raised by a background task (the check for a new version at start-up) once the main window is on the screen
        /// and its message loop is running. Safe to call from any thread at any time: a dialog raised before the main window is shown
        /// waits for it. On the UI thread the dialog goes through ShowOrQueueDialog, so it takes its turn behind the opening dialog.
        /// </summary>
        internal void ShowWhenMainWindowShown(Form f)
        {
            Utilities.DiagnosticLog.Write("ShowWhenMainWindowShown: instance " + GetHashCode() + ", mainWindowShown " + mainWindowShown + ", MainWindow " + (MainWindow is null ? "null" : "set"));
            lock (dialogsWaitingForMainWindow)
            {
                if (!mainWindowShown)
                {
                    dialogsWaitingForMainWindow.Add(f);
                    return;
                }
            }
            MainWindow.BeginInvoke(new Action(() => ShowOrQueueDialog(f, null)));
        }

        /// <summary>
        /// Called by the main window when it is first shown: releases any dialog that was waiting for it.
        /// </summary>
        internal void MainWindowIsShown()
        {
            Form[] waiting;
            lock (dialogsWaitingForMainWindow)
            {
                mainWindowShown = true;
                waiting = dialogsWaitingForMainWindow.ToArray();
                dialogsWaitingForMainWindow.Clear();
            }
            Utilities.DiagnosticLog.Write("MainWindowIsShown: instance " + GetHashCode() + ", waiting dialogs " + waiting.Length);
            foreach (Form f in waiting)
                MainWindow.BeginInvoke(new Action(() => ShowOrQueueDialog(f, null)));
        }

        internal bool OpenFile()
        {
            return MainWindow.OpenFile();
        }

        internal bool OpenFile(string path, bool removeFromRecentFilesIfNotFound)
        {
            return MainWindow.OpenFile(path, removeFromRecentFilesIfNotFound);
        }

        internal void CreateMainWindow()
        {
            MainWindow = new frmMain();
        }

        internal StatsDirectForm CreateReport()
        {
            return MainWindow.CreateReport();
        }

        internal StatsDirectForm CreateGrid()
        {
            return MainWindow.CreateGrid();
        }

        internal void Run()
        {
            Application.Run(MainWindow);
        }

        /// <summary>
        /// We know we're safe to show a dialog now (whether immediate or whether we just got it off the queue).  Show it on the UI thread, wait for a user response, and if necessary run its post display action (also on the UI thread).
        /// If another dialog has been queued while we're showing this one, show that.
        /// </summary>
        /// <param name="f"></param>
        /// <param name="postDisplayAction"></param>
        private void ShowDialogOnUiThread(Form f, Action<Form, DialogResult> postDisplayAction)
        {
            if (null != MainWindow)
                if (MainWindow.InvokeRequired)
                {
                    MainWindow.Invoke(new Action(() => { DialogResult result = f.ShowDialog(MainWindow);
                        postDisplayAction?.Invoke(f, result);
                        f.Dispose(); }));
                }
                else
                {
                    DialogResult result;
                    try
                    {
                        result = f.ShowDialog(MainWindow);
                    }
                    catch (Exception ex)
                    {
                        Utilities.DiagnosticLog.Write("ShowDialogOnUiThread: " + f.GetType().Name + " threw " + ex);
                        throw;
                    }
                    postDisplayAction?.Invoke(f, result);
                    f.Dispose();
                }
            else
            {
                // If there's no main window at present, we'd expect to be on the UI thread.  TODO: Prove this assumption.
                DialogResult result = f.ShowDialog();
                postDisplayAction?.Invoke(f, result);
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

        /// <summary>
        /// The application is closing down.  Save anything we need!
        /// </summary>
        internal void Shutdown()
        {
            if (ClosingForUpgrade)
                FetchTheUpgrade();
        }

        public static void FetchTheUpgrade()
        {
            Process.Start(new ProcessStartInfo("http://www.statsdirect.com/download/StatsDirectSetup.exe") { UseShellExecute = true });
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
                foreach (WindowInformation wi in Windows)
                {
                    if (wi.HasWindow && wi.Window is IGrid)
                    {
                        string windowName = wi.FriendlyName;
                        if (windowName.StartsWith("Data "))
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
        internal int GetReportNumber()
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

                    string windowName = wi.FriendlyName;
                    if (!windowName.StartsWith("Report "))
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
        internal int GetScriptWindowNumber()
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
                        string windowName = wi.FriendlyName;
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

        /// <summary>
        /// The MDI window in which newly-created children are placed
        /// </summary>
        private frmMain MainWindow { get; set; }

        internal void AddWindow(WindowInformation info)
        {
            Windows.Add(info);
        }

        internal void RemoveWindow(WindowInformation info)
        {
            if (Windows.Contains(info))
                Windows.Remove(info);
        }

        internal WindowInformation ActiveWindow { get; private set; }

        internal WindowInformation ActiveGrid { get; private set; }

        public ICollection<WindowInformation> Windows { get; } = new HashSet<WindowInformation>();

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
        /// <param name="info"></param>
        internal void NoteFormActivated(WindowInformation info)
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

        internal void ShowHelp(Form Parent, string Topic)
        {
            Help.ShowHelp(Parent, HelpFilePath, HelpNavigator.TopicId, Topic);
        }

        internal void ShowHelp(Form Parent)
        {
            if (null != ActiveHelpUrl)
            {
                // Show the URL
                Help.ShowHelp(Parent, ActiveHelpUrl);
            }
            else if (0 != ActiveHelpTopic)
            {
                // Specific help - show it.
                Help.ShowHelp(Parent, HelpFilePath, HelpNavigator.TopicId, ActiveHelpTopic.ToString());
            }
            else
            {
                // Nothing in particular, guess something useful or show the ToC if we can't.
                if (SoleInstance?.ActiveWindow != null && SoleInstance.ActiveWindow.HasWindow && SoleInstance.ActiveWindow.Window is IGrid)
                {
                    // Grid - show the worksheet help, which is 1040.
                    Help.ShowHelp(Parent, HelpFilePath, HelpNavigator.TopicId, "1040");
                }
                else
                {
                    Help.ShowHelp(Parent, HelpFilePath, HelpNavigator.TableOfContents);
                }
            }
        }

        internal string HelpFilePath => SDConfiguration.HelpFilePath;

        internal int ActiveHelpTopic
        {
            get => sActiveHelpTopic;
            set { sActiveHelpTopic = value; activeHelpUrl = null; }
        }

        internal string ActiveHelpUrl
        {
            get => activeHelpUrl;
            set { sActiveHelpTopic = 0; activeHelpUrl = value; }
        }

        internal IList<Pane> AvailableReportPanes()
        {
            IList<Pane> availableWindows = new List<Pane>();
            foreach (WindowInformation info in Windows)
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

        internal void DoOperationOnceOrUntilCancelled(Operation operation, ParameterBag parameterBag) => MainWindow.DoOperationOnceOrUntilCancelled(operation, parameterBag);

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
            foreach (WindowInformation info in Windows)
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
                    SpecialParameter parameter = new()
                    {
                        SpecialType = "frame",
                        Name = KEY,
                        ExtraData = new object[] { relativePosition },
                        PromptExpression = new Expression("Pick the sheet in which you want the output to appear")
                    };
                    ParameterBag results = FillSingleParameter(parameter); // Will never return a null value as the parameter cannot be skipped
                    bool cancelled = null == results || !results.ContainsKey(KEY);
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
            if (selectedPane?.WindowInformation == null)
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
        object IUserInterface.OutputReport(IRenderable renderable, Operation operation, object preferredOutputLocation)
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
            report.AppendRenderable(renderable, ActiveHelpTopic, operation);
            report.EnsureActive();
            return report.SelectedPane;
        }

        /// <summary>
        /// Append the data in the frame to a new or existing user-selected grid window.
        /// </summary>
        void IUserInterface.OutputFrame(DataFrame frame, bool keepSelection, bool isFormulae, string missingIndicator, PaneAndPosition preferredOutputLocation, RelativePosition defaultPosition)
        {
            IGrid grid;
            RelativePosition writePosition = defaultPosition;

            if (keepSelection && null != ActiveGrid)
            {
                grid = (IGrid)ActiveGrid.Window;
                // If we're writing multiple outputs that won't be written over the top of each other, each one is selected after it is written.  Therefore we can use that to ensure subsequent output is written directly after the initial output.
                writePosition = defaultPosition != RelativePosition.ReplaceSelection ? RelativePosition.AfterSelection : defaultPosition;
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

        public static void InitialiseFunctionRegistry()
        {
            BuiltinRegistry.SoleInstance.AddAll(Builtins.Registry.GetFunctionRegistry());
        }

        IScriptEngine IScriptEngineHost.GetScriptEngine(string language)
        {
            if (ScriptEngine.CanHandle(language))
                return new ScriptEngine();
            return null;
        }

        void IUserInterface.PrepareParameter(ITemplateProcessor processor, Parameter parameter, ParameterBag context)
        {
            if (parameter is PickFromListParameter pickFromListParameter)
                PrepareParameter(processor, pickFromListParameter, context);
        }

        bool IUserInterface.CanCombine(Parameter parameter)
        {
            if (parameter is Frame2DParameter)
                return false;
            if (parameter is GroupedCovarianceParameter)
                return false;
            if (parameter is FrameParameter frameParameter)
            {
                // Grids that must be entered rather than selected can be combined, as an entry grid will appear at the top.
                return !frameParameter.CanSelect;
            }
            return true;
        }

        internal void PuntThroughEventLoop(Exception ex)
        {
            MainWindow.PuntThroughEventLoop(ex);
        }

        internal void EraseAnyOutstandingParameters()
        {
            outstandingParameters = null;
        }

        ParameterBag IUserInterface.FillAndValidateCombinedParameters(ITemplateProcessor processor, ParameterBag context)
        {
            if (null == MainWindow)
                throw new Exception("Attempt to fill combined parameters with no main window open");
            if (null == outstandingParameters || outstandingParameters.Count == 0)
                return new ParameterBag();
            return MainWindow.FillAndValidateCombinedParameters(this, processor, context, outstandingParameters);
        }

        ParameterBag IUserInterface.FillParameter(ITemplateProcessor processor, Parameter parameter, ParameterBag context, bool shouldCombine)
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
            bool lastHadValidationError = false;
            while (true)
            {
                ImmediateParameterFiller filler = new() { Context = context, Processor = processor, IsRepeatAfterValidationError = lastHadValidationError };
                parameter.Accept(filler);

                // Validate; if no errors, stop.  If there are errors, show them and go round again.
                string validationResult = null;
                if (null != parameter.Validators)
                {
                    foreach (Validator validator in parameter.Validators)
                    {
                        validationResult = Validate(validator, parameter, filler.OutputParameters, parameter.ValidationFailMessage);
                        if (null != validationResult)
                            break;
                    }
                }
                if (null == validationResult)
                    return filler.OutputParameters;

                MsgboxX(validationResult, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                lastHadValidationError = true;
            }
        }

        public string Validate(Validator validator, Parameter parameter, ParameterBag filledParameters, string failedValidationMessage)
        {
            ValidationResult validationResult = ValidationProcessor.Validate(this, validator.ValidatorName, parameter, filledParameters, failedValidationMessage);
            switch (validationResult.Validity)
            {
                case Validity.Valid:
                    return null;
                case Validity.Invalid:
                    return validationResult.FailedValidationMessage;
                case Validity.NeedMoreInformation:
                    bool result = GetBoolean(validationResult.PromptForMoreInformation, validationResult.TitleForMoreInformation, false, validationResult.HelpContextId, out bool wasCancelled);
                    if (wasCancelled)
                        throw new TemplateOperationCancelledException();
                    ValidationAction validationAction = result ? validationResult.ActionOnMoreInformationYes : validationResult.ActionOnMoreInformationNo;
                    switch (validationAction)
                    {
                        case ValidationAction.CancelOperation:
                            throw new TemplateOperationCancelledException();
                        case ValidationAction.RequestAgain:
                            return validationResult.FailedValidationMessage;
                        case ValidationAction.UseAsIs:
                            return null;
                        default:
                            throw new Exception("Unknown validation action requested");
                    }
                default:
                    throw new Exception("Unknown validity");
            }
        }

        static void PrepareParameter(ITemplateProcessor processor, PickFromListParameter parameter, ParameterBag context)
        {
            // TODO: Move logic out of SetOperation() into here
        }

        private ParameterBag FillChartOptions(Charting.ChartDefinition ChartDefinition, ParameterBag context)
        {
            ITemplateHost ith = this;
            ITemplateProcessor processor = new TemplateProcessor(ith);
            ChartOptionsParameter chartOptionsParameter = new("dummy", ChartDefinition);
            ith.FillParameter(processor, chartOptionsParameter, context, true);
            return ith.FillAndValidateCombinedParameters(processor, context);
        }

        internal void NoteEndOfSelection(bool ok)
        {
            MainWindow.NoteEndOfSelection(ok);
        }

        internal bool SelectCells(string fullSelectionMessage, string cancelButtonLabel, bool canSelectMultipleRows, bool allowUserToPivot, out bool wasPivoted)
        {
            MainWindow.CanSelectMultipleRows = canSelectMultipleRows;
            MainWindow.CanSelectGroupMethod = allowUserToPivot;
            if (allowUserToPivot)
                MainWindow.GroupsByIdentifier = Preferences.SelectGroupsByIdentifier;
            return MainWindow.SelectCells(fullSelectionMessage, cancelButtonLabel, out wasPivoted);
        }

        /// <summary>
        /// Returns a display value of Amount, rounded to DisplayDecimalPlaces if sensible.
        /// </summary>
        /// <returns></returns>
        public string RoundU(double amount) => Formatting.XRound(amount, Preferences.DisplayDecimalPlaces);

        string IFormatting.pval(double p) => Formatting.pval(p, Preferences.PDecimalPlaces, Preferences.UseScientificNotationForSmallPValues);

        string IFormatting.pval_half(double p) => Formatting.pval_half(p, Preferences.PDecimalPlaces, Preferences.UseScientificNotationForSmallPValues);

        /// <summary>
        /// An expected error has occurred.  Tell the user in a suitable manner.
        /// </summary>
        /// <param name="explanation">An explanation of what the application was doing that caused the error, in terms a user could follow</param>
        /// <param name="ex">The exception that was expected</param>
        /// <param name="showHelpButton"></param>
        internal void FriendlyError(string explanation, Exception ex, bool showHelpButton)
        {
            MsgboxX(explanation + (null == ex ? string.Empty : "\r\n" + ex.Message), MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "StatsDirect", showHelpButton);
            if (null != ex)
                WriteToBlackbox(explanation, ex);
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
                if (null == MainWindow || !MainWindow.Visible || MainWindow.WindowState == FormWindowState.Minimized || ModalDialogShowing())
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
            if (null == MainWindow || !MainWindow.Visible || MainWindow.WindowState == FormWindowState.Minimized || ModalDialogShowing())
                return MessageBox.Show(MainWindow, text, caption, buttons, icon, defaultButton, 0, HelpFilePath, HelpNavigator.TopicId, helpTopic.ToString());
            return MainWindow.ShowModalMessage(text, caption, buttons, icon, defaultButton, HelpFilePath, HelpNavigator.TopicId, helpTopic.ToString());
        }

        public bool GetBoolean(string prompt, string caption, bool defaultValue, out bool cancelled)
        {
            cancelled = false;
            return MsgboxX(prompt, MessageBoxButtons.YesNo, MessageBoxIcon.Question, caption, true) == DialogResult.Yes;
        }

        bool GetBoolean(string prompt, string caption, bool defaultValue, int helpTopic, out bool Cancelled)
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
            ITemplateProcessor processor = new TemplateProcessor(host);
            ParameterBag context = new();
            host.FillParameter(processor, parameter, context, true);
            return host.FillAndValidateCombinedParameters(processor, context);
        }

        public int GetInteger(string prompt, string caption, int defaultValue, out bool cancelled)
        {
            const string key = "solo";
            IntegerParameter parameter = new()
            {
                Name = key,
                PromptExpression = new Expression(prompt),
                DefaultValueExpression = new Expression(defaultValue.ToString()),
                CancelSkipsParameter = "Skip"
            };
            ParameterBag results = FillSingleParameter(parameter);
            cancelled = null == results || !results.ContainsKey(key) || null == results[key];
            return cancelled ? 0 : results[key].AsInt32;
        }

        public string GetString(string prompt, string caption, string defaultValue) => Prompt(prompt, caption, defaultValue);

        public void Error(string message, string caption) => MsgboxX(message, MessageBoxButtons.OK, MessageBoxIcon.Error, caption, true);

        IProgressBar IProgressBarHost.StartProgress(string operationDescription, bool provideProgress, bool display)
        {
            if (display)
                MainWindow?.StartProgress(operationDescription, provideProgress);
            return new SdProgressBarHolder(display);
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

        ParameterBag IUserInterface.Amend(IFillable fillable, ParameterBag context)
        {
            return fillable.FillerToUse switch
            {
                "ChiSquareGoodnessOfFit" or "ConvertUnits" or "Distribution" or "Dummy" or "Extraction" or "GraphicsOptions" or "ROCCutoff" or "Scores" or "SortInPlace" => AmendUsingControl(fillable),
                "ToggleFilters" => ToggleFilters(),
                "Categorise" => Amend((Builtins.CategoriseOptions)fillable),
                "ChartExplorer" => throw new NotImplementedException("Chart explorer is not implemented in StatsDirect"),
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

        private ParameterBag AmendUsingControl(IFillable fillable)
        {
            ITemplateHost ith = this;
            ITemplateProcessor processor = new TemplateProcessor(ith);
            ParameterBag context = new();
            FillableParameter fillableParameter = new("dummy", fillable);
            ith.FillParameter(processor, fillableParameter, context, true);
            ParameterBag filledParameters = ith.FillAndValidateCombinedParameters(processor, context);
            return filledParameters;
        }

        private ParameterBag Amend(Builtins.CategoriseOptions categoriseOptions)
        {
            using frmCategorise options = new(categoriseOptions);
            using (new DefaultCursor())
            {
                options.ShowDialog(MainWindow);
            }
            return options.UserCancelled ? null : new ParameterBag();
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
            using frmSummaryStatistics options = new(summaryStatisticsOptions);
            using (new DefaultCursor())
            {
                options.ShowDialog(MainWindow);
            }
            return new ParameterBag();
        }

        public SDPreferences Preferences => preferences ??= LoadPreferences();

        private class SDPreferencesImpl : SDPreferences
        {
            public bool UseScientificNotationForSmallPValues
            {
                get => Properties.Settings.Default.UseScientificNotationForSmallPValues;
                set => Properties.Settings.Default.UseScientificNotationForSmallPValues = value;
            }

            public bool CanDefaultConfidenceInterval
            {
                get => Properties.Settings.Default.CanDefaultConfidenceInterval;
                set => Properties.Settings.Default.CanDefaultConfidenceInterval = value;
            }

            public double DefaultConfidenceInterval
            {
                get => Properties.Settings.Default.DefaultConfidenceInterval;
                set => Properties.Settings.Default.DefaultConfidenceInterval = value;
            }

            public bool SelectGroupsByIdentifier
            {
                get => Properties.Settings.Default.SelectGroupsByIdentifier;
                set => Properties.Settings.Default.SelectGroupsByIdentifier = value;
            }

            public double MetaCC
            {
                get => Properties.Settings.Default.MetaCC;
                set => Properties.Settings.Default.MetaCC = value;
            }

            public bool MetaExact
            {
                get => Properties.Settings.Default.MetaExact;
                set => Properties.Settings.Default.MetaExact = value;
            }

            public bool DelayContinuityCorrection
            {
                get => Properties.Settings.Default.DelayContinuityCorrection;
                set => Properties.Settings.Default.DelayContinuityCorrection = value;
            }

            public int DisplayDecimalPlaces
            {
                get => Properties.Settings.Default.DisplayDecimalPlaces;
                set => Properties.Settings.Default.DisplayDecimalPlaces = value;
            }

            public int PDecimalPlaces
            {
                get => Properties.Settings.Default.PDecimalPlaces;
                set => Properties.Settings.Default.PDecimalPlaces = value;
            }

            public int MetaPlotMethod
            {
                get => Properties.Settings.Default.MetaPlotMethod;
                set => Properties.Settings.Default.MetaPlotMethod = value;
            }

            public bool MetaPlotCI
            {
                get => Properties.Settings.Default.MetaPlotCI;
                set => Properties.Settings.Default.MetaPlotCI = value;
            }

            public string DECP_CHAR => System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

            public string Numeric_Thousands_Separator => System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator;

            public int MaxRows => 64000;

            public bool ShouldKeepData
            {
                get => Properties.Settings.Default.ShouldKeepData;
                set => Properties.Settings.Default.ShouldKeepData = value;
            }

            public bool ShouldUseColour
            {
                get => Properties.Settings.Default.ShouldUseColour;
                set => Properties.Settings.Default.ShouldUseColour = value;
            }
        }

        private static SDPreferences LoadPreferences()
        {
            return new SDPreferencesImpl();
        }

        internal bool SelectingData => null != MainWindow && MainWindow.SelectingData;

        public Operation Operation { get; set; }

        internal void ShowCurrentHelp()
        {
            ShowHelp(MainWindow);
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
            using frmInputBox ib = new(prompt, caption, defaultValue);
            ib.ShowDialog(MainWindow);
            if (ib.UserCancelled)
                return null;
            return ib.Value;
        }

        internal void NoteRecentFile(string path, bool openedOk)
        {
            // Ensure the path is the most recently used and appears no more than once; ensure no more than MAX_RECENT_FILES files are kept
            List<string> recentFiles = new(Properties.Settings.Default.RecentFileList ?? Array.Empty<string>());
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
            MainWindow?.UpdateFileList();
        }

        internal IList<string> RecentFiles
        {
            get
            {
                // Stored in reverse order (most recent last), so reverse on the way out
                IReadOnlyList<string> recentFiles = Properties.Settings.Default.RecentFileList ?? Array.Empty<string>();
                IList<string> output = new List<string>();
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

        IDictionary<string, ParameterBag> ISession.SessionParametersPerOperation => sessionParametersPerOperation ??= new Dictionary<string, ParameterBag>();

        ParameterBag ISession.SessionParametersAcrossOperations => sessionParametersAcrossOperations ??= new ParameterBag();

        public string TemplateFileForNewReports => Path.Combine(SDConfiguration.TemplatePath, "blank.rtf");

        internal void ClearBatchMode()
        {
            // Ensure no windows might remember anything to do with batching
            foreach (WindowInformation wi in Windows)
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
            if (null == MainWindow)
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        public static void WriteToBlackbox(string message, Exception ex)
        {
            try
            {
                using Stream boxStream = File.OpenWrite(Path.Combine(SDConfiguration.MyStatsDirectFolder, "Blackbox.txt"));
                using TextWriter boxWriter = new StreamWriter(boxStream, Encoding.UTF8);
                boxWriter.WriteLine("StatsDirect exception log generated at {0} local time ({1} UTC)", DateTime.Now, DateTime.UtcNow);
                boxWriter.WriteLine();
                boxWriter.WriteLine("This file contains a trace of what StatsDirect was doing when your error occurred. If we've asked you for it, please attach the file or, if you prefer, paste the contents into an email to us.");
                boxWriter.WriteLine();
                if (null != message)
                {
                    boxWriter.WriteLine("Message generated from StatsDirect: {0}", message);
                    boxWriter.WriteLine();
                }
                WriteExceptionToBlackbox(boxWriter, ex, false);
            }
            catch (Exception)
            {
                // If our black box can't operate, we're hosed.  Ignore this error!
            }
        }

        private static void WriteExceptionToBlackbox(TextWriter boxWriter, Exception ex, bool isInnerException)
        {
            if (null == ex)
                return;

            if (isInnerException)
                boxWriter.WriteLine("Inner exception:");
            boxWriter.WriteLine(ex.GetType().FullName);
            boxWriter.WriteLine(ex.Message);
            boxWriter.WriteLine(ex.Source);
            boxWriter.WriteLine(ex.StackTrace);
            if (null != ex.Data)
                foreach (DictionaryEntry de in ex.Data)
                    boxWriter.WriteLine("{0} = {1}", de.Key, de.Value);

            if (null != ex.InnerException)
                WriteExceptionToBlackbox(boxWriter, ex.InnerException, true);
            boxWriter.WriteLine();
        }

        internal void EnsureBuiltInMenuItemsCanShowHelp(MenuStrip menuStrip)
        {
            MainWindow.EnsureBuiltInMenuItemsCanShowHelp(menuStrip);
        }

        public bool ClosingForUpgrade { get; private set; }

        public static bool IsRunningOnMono => Type.GetType("Mono.Runtime") != null;

        public Form DialogOwner => MainWindow;

        public bool InOperation => MainWindow.InOperation;

        internal void DoOperation(string operationName)
        {
            MainWindow.DoOperation(operationName);
        }

        internal bool IsSelecting => MainWindow.IsSelecting;

        public Form ActiveMdiChild => MainWindow.ActiveMdiChild;

        internal void OpenFileOnUiThread(string path)
        {
            if (null != MainWindow)
                if (MainWindow.InvokeRequired)
                    MainWindow.Invoke(new Action(() => OpenFile(path, false)));
        }

        private class SdProgressBarHolder : IProgressBar
        {
            private bool Display { get; }

            public SdProgressBarHolder(bool display)
            {
                Display = display;
            }

            public void Finish()
            {
                if (Display)
                    SoleInstance.FinishProgress();
            }

            bool IProgressBar.Update(double fractionComplete)
            {
                return Display && SoleInstance.UpdateProgress(fractionComplete);
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
