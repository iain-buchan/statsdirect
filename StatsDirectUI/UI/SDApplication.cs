using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Security;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Diagnostics;
using System.Windows.Forms;
using System.Security.Permissions;

using Lambda.Collections.Generic;

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
    public sealed class SDApplication : ITemplateHost, IRefillSource
    {
        private const int MAX_RECENT_FILES = 4;

        private int sActiveHelpTopic;
        private string activeHelpUrl;
        private static SDApplication soleInstance;
        private UserInfo userInfo;

        private bool closingForUpgrade;

        /// <summary>
        /// The MDI window in which newly-created children are placed
        /// </summary>
        private frmMain mainWindow;
        private readonly ICollection<WindowInformation> windows = new Set<WindowInformation>();
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

        /// <summary>
        /// Returns the single instance of the application, creating it if necessary.
        /// </summary>
        internal static SDApplication SoleInstance
        {
            get
            {
                Contract.Ensures(null != Contract.Result<SDApplication>());
                return soleInstance ?? (soleInstance = new SDApplication());
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
        private SDApplication()
        {
            InitialiseFunctionRegistry();
            LoadPersistentValues();
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

        private static void FetchTheUpgrade()
        {
            const string DOWNLOAD_URL = "http://www.statsdirect.com/download/statsdirect.msi";
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
                    if (wi.HasWindow && wi.Window.ImplementsIGrid)
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
                    if (wi.Window.ImplementsIReport)
                    {
                        string windowName = wi.FriendlyName;
                        if (windowName.StartsWith("Report "))
                        {
                            // We don't want to load Report 1.rtf and create Report 1 again (#673).  Strip any suffix before comparison.
                            if (Path.HasExtension(windowName))
                                windowName = Path.GetFileNameWithoutExtension(windowName);
                            string windowNumberAsString = (null == windowName) ? "" : windowName.Substring(7).Trim();
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
                    if (wi.Window.ImplementsIScriptWindow)
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

        public UserInfo UserInfo
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
                if (info.Window.ImplementsIGrid)
                    activeGrid = info;
                mainWindow.EnsureTabSelected(info.TabPage);
                mainWindow.SetMenuVisibility(info.Window.ImplementsIGrid);
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
                if (null != SoleInstance && null != SoleInstance.ActiveWindow && SoleInstance.ActiveWindow.HasWindow && SoleInstance.ActiveWindow.Window is IGrid)
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
                if (info.Window.ImplementsIReport)
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
            if (null != ActiveGrid)
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
                if (info.Window.ImplementsIGrid)
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
            /*
            try
            {
                IList<Pane> availableWindows = AvailableReportPanes();
                Pane selectedPane = null;
                if (availableWindows.Count > 0)
                {
                    // selectedPane = PickAWindow(availableWindows, AllowNew ? "New Report" : null, mostRecentlySelectedReport);
                    const string KEY = "solo";
                    SpecialParameter parameter = new SpecialParameter();
                    parameter.SpecialType = "report";
                    parameter.Name = KEY;
                    parameter.PromptExpression = new Expression("Pick the report in which you want the output to appear");
                    ParameterBag results = FillSingleParameter(parameter);
                    bool cancelled = (null == results || !results.ContainsKey(KEY));
                    if (cancelled)
                        throw new TemplateOperationCancelledException();
                    selectedPane = (Pane)results[KEY].AsPane;
                }
                return SelectReportWindow(selectedPane);
            }
            catch (TemplateOperationCancelledException)
            {
                // User cancelled
                return null;
            }
            */
        }

        internal IReport SelectReportWindow(Pane selectedPane)
        {
            // As we now don't remember reports for output, this is equivalent to a PickReportWindow.
            return PickReportWindow(false);

            /*
            if (null == selectedPane || null == selectedPane.WindowInformation)
            {
                // Create a new report
                StatsDirectForm rpt = mainWindow.CreateReport();
                mostRecentlySelectedReport = ((IForm)rpt).SelectedPane;
                return (IReport)rpt;
            }
            else
            {
                // Existing window
                mostRecentlySelectedReport = ((IForm)selectedPane.WindowInformation.Window).SelectedPane;
                return (IReport)selectedPane.WindowInformation.Window;
            }
             */
        }

        /// <summary>
        /// Allows the user to pick from existing grid windows, plus potentially a new one.
        /// </summary>
        /// <param name="allowNew">If true, the user may select a new grid as well as any existing ones.  If false, only existing grids may be picked.</param>
        /// <param name="relativePosition"></param>
        /// <returns></returns>
        public IGrid PickGridWindow(bool allowNew, out RelativePosition relativePosition)
        {
            IList<Pane> availableWindows = AvailableFramePanes();
            try
            {
                Pane selectedPane = null;
                if (availableWindows.Count > 0)
                {
                    // selectedPane = PickAWindow(availableWindows, AllowNew ? "New data" : null, mostRecentlySelectedGrid);
                    const string KEY = "solo";
                    SpecialParameter parameter = new SpecialParameter
                                                     {
                                                         SpecialType = "frame",
                                                         Name = KEY,
                                                         PromptExpression =
                                                             new Expression(
                                                             "Pick the sheet in which you want the output to appear")
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
            try
            {
                new UIPermission(UIPermissionWindow.AllWindows).Assert();
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
            finally
            {
                CodeAccessPermission.RevertAssert();
            }
        }

        /// <summary>
        /// Append the data in the frame to a new or existing user-selected grid window.
        /// </summary>
        /// <param name="frame">The frame to output</param>
        /// <param name="keepSelection"></param>
        /// <param name="isFormulae"></param>
        /// <param name="missingIndicator"> </param>
        /// <param name="preferredOutputLocation"></param>
        void ITemplateHost.OutputFrame(DataFrame frame, bool keepSelection, bool isFormulae, string missingIndicator, PaneAndPosition preferredOutputLocation)
        {
            IGrid grid;
            RelativePosition writePosition;

            if (keepSelection && null != activeGrid)
            {
                grid = (IGrid)activeGrid.Window;
                // If we're writing multiple outputs, each one is selected after it is written.  Therefore we can use that to ensure subsequent output is written directly after the initial output.
                writePosition = RelativePosition.AfterSelection;
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
                    grid = PickGridWindow(true, out writePosition);
                }
            }
            if (null == grid)
                throw new TemplateOperationCancelledException();
            grid.WriteDataFrame(frame, isFormulae, missingIndicator, writePosition);
            grid.EnsureActive();
        }
        /*
        /// <summary>
        /// Select a new or existing user-selected grid window for future output.
        /// </summary>
        public void SelectOutputForFrame()
        {
            RelativePosition scrap;
            IGrid grid = PickGridWindow(true, out scrap);
            if (null == grid)
                throw new TemplateOperationCancelledException();
        }
        */
        private static void InitialiseFunctionRegistry()
        {
            BuiltinRegistry.SoleInstance.AddAll(Builtins.Registry.GetFunctionRegistry());
        }

        IScriptEngine ITemplateHost.GetScriptEngine(string Language)
        {
            // At present, all languages are handled by the ScriptEngine.  This may never change.
            return new ScriptEngine();
        }

        void ITemplateHost.PrepareParameter(ITemplateProcessor processor, Parameter parameter, ParameterBag context)
        {
            switch (parameter.Type)
            {
                case ParameterType.Boolean:
                case ParameterType.ConfidenceInterval:
                case ParameterType.Date:
                case ParameterType.Double:
                case ParameterType.Double2By2:
                case ParameterType.Double2By2ByK:
                case ParameterType.EditGrid:
                case ParameterType.Grid:
                case ParameterType.Grid2D:
                case ParameterType.GroupedCovariance:
                case ParameterType.Integer:
                case ParameterType.Option:
                case ParameterType.Options:
                case ParameterType.PickVariables:
                case ParameterType.Special:
                case ParameterType.String:
                    // Do nothing
                    break;
                case ParameterType.PickFromList:
                    PrepareParameter(processor, (PickFromListParameter)parameter, context);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("parameter", parameter, "parameter.Type: Unexpected parameter type");
            }
        }

        bool ITemplateHost.CanCombine(Parameter parameter)
        {
            switch (parameter.Type)
            {
                case ParameterType.Boolean:
                case ParameterType.ConfidenceInterval:
                case ParameterType.Date:
                case ParameterType.Double:
                case ParameterType.Double2By2:
                case ParameterType.Double2By2ByK:
                case ParameterType.EditGrid:
                case ParameterType.Integer:
                case ParameterType.Option:
                case ParameterType.Options:
                case ParameterType.PickVariables:
                case ParameterType.PickFromList:
                case ParameterType.Special:
                case ParameterType.String:
                    return true;
                case ParameterType.Grid2D:
                case ParameterType.GroupedCovariance:
                    return false;
                case ParameterType.Grid:
                    // Grids that must be entered rather than selected can be combined, as an entry grid will appear at the top.
                    return !((GridParameter)parameter).CanSelect;
                default:
                    throw new ArgumentOutOfRangeException("parameter", parameter, "parameter.Type: Only ConfidenceInterval, Grid, Integer, Option and PickVariables known");
            }
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
            UIPermission p = new UIPermission(UIPermissionWindow.AllWindows, UIPermissionClipboard.AllClipboard);
            try
            {
                p.Assert();
                return mainWindow.FillAndValidateCombinedParameters(this, processor, context, outstandingParameters);
            }
            finally
            {
                CodeAccessPermission.RevertAssert();
            }
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

            while (true)
            {
                ParameterBag outputParameters;
                switch (parameter.Type)
                {
                    case ParameterType.Boolean:
                        outputParameters = FillParameter(processor, (BooleanParameter) parameter, context);
                        break;
                    case ParameterType.ConfidenceInterval:
                        outputParameters = FillParameter(processor, (ConfidenceIntervalParameter) parameter, context);
                        break;
                    case ParameterType.Date:
                        throw new ArgumentOutOfRangeException("parameter", parameter.Type, "Date should always be filled inline");
                    case ParameterType.Double:
                        outputParameters = FillParameter(processor, (DoubleParameter) parameter, context);
                        break;
                    case ParameterType.Double2By2:
                        throw new ArgumentOutOfRangeException("parameter", parameter.Type, "Double2By2 should always be filled inline");
                    case ParameterType.Double2By2ByK:
                        throw new ArgumentOutOfRangeException("parameter", parameter.Type, "Double2By2ByK should always be filled inline");
                    case ParameterType.EditGrid:
                        throw new ArgumentOutOfRangeException("parameter", parameter.Type, "EditGrid should always be filled inline");
                    case ParameterType.Grid:
                        {
                            frmSpreadsheetGear gearWindow = (frmSpreadsheetGear) ActiveGrid.Window;
                            outputParameters = gearWindow.FillGridParameter(parameter, processor, this, context);
                        }
                        break;
                    case ParameterType.Grid2D:
                        {
                            frmSpreadsheetGear gearWindow = (frmSpreadsheetGear) ActiveGrid.Window;
                            DataFrame2D frame = gearWindow.FillGridParameter2D(parameter, processor, this, context);
                            outputParameters = null == frame ? null : new ParameterBag(parameter.Name, new FilledParameter(true, frame));
                        }
                        break;
                    case ParameterType.GroupedCovariance:
                        outputParameters = FillParameter((GroupedCovarianceParameter) parameter);
                        break;
                    case ParameterType.Integer:
                        outputParameters = FillParameter(processor, (IntegerParameter) parameter, context);
                        break;
                    case ParameterType.Option:
                        outputParameters = FillParameter(processor, (OptionParameter) parameter, context);
                        break;
                    case ParameterType.Options:
                        outputParameters = FillParameter(processor, (OptionsParameter) parameter, context);
                        break;
                    case ParameterType.PickFromList:
                        throw new ArgumentOutOfRangeException("parameter", parameter.Type, "PickFromList should always be filled inline");
                    case ParameterType.PickVariables:
                        throw new ArgumentOutOfRangeException("parameter", parameter.Type, "PickVariables should always be filled inline");
                    case ParameterType.Special:
                        throw new ArgumentOutOfRangeException("parameter", parameter.Type, "Special should always be filled inline");
                    case ParameterType.String:
                        throw new ArgumentOutOfRangeException("parameter", parameter.Type, "String should always be filled inline");
                    default:
                        throw new ArgumentOutOfRangeException("parameter", parameter.Type, "Only ConfidenceInterval, Grid, Integer, Option and PickVariables known");
                }

                // Validate; if no errors, stop.  If there are errors, show them and go round again.
                string validationResult = null;
                if (null != parameter.Validators)
                {
                    foreach (Validator validator in parameter.Validators)
                    {
                        validationResult = TemplateProcessor.Validate(this, validator.ValidatorName, parameter, outputParameters, parameter.ValidationFailMessage);
                        if (null != validationResult)
                            break;
                    }
                }
                if (null == validationResult)
                    return outputParameters;

                MsgboxX(validationResult, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        ParameterBag FillParameter(ITemplateProcessor processor, ConfidenceIntervalParameter Parameter, ParameterBag context)
        {
            // Can we get away without asking?
            if (Parameter.CanDefault && Preferences.CanDefaultConfidenceInterval)
                return new ParameterBag(Parameter.Name, new FilledParameter(true, Preferences.DefaultConfidenceInterval));

            // Prompt - keep going until the user cancels or gives a valid entry
            string prompt = Parameter.Prompt(processor, context);
            if (string.IsNullOrEmpty(prompt))
                prompt = "Enter confidence interval (%, in the range [0, 100])";

            double result = 0.0;
            if (Preferences.CanDefaultConfidenceInterval)
                result = Preferences.DefaultConfidenceInterval;
            if (Parameter.DefaultValue > 0.0)
                result = Parameter.DefaultValue;

            while (true)
            {
                string response = Prompt(prompt, "StatsDirect", (result * 100.0).ToString());
                if (string.IsNullOrEmpty(response))
                    throw new TemplateOperationCancelledException();
                if (Double.TryParse(response, out result))
                {
                    result /= 100.0;
                    if (result >= 0.0 && result <= 1.0)
                    {
                        return new ParameterBag(Parameter.Name, new FilledParameter(true, result));
                    }
                }
                // else go round again
            }
        }

        static void PrepareParameter(ITemplateProcessor processor, PickFromListParameter parameter, ParameterBag context)
        {
            // TODO: Move logic out of SetOperation() into here
        }

        ParameterBag FillParameter(GroupedCovarianceParameter Parameter)
        {
            frmSpreadsheetGear gearWindow = (frmSpreadsheetGear)ActiveGrid.Window;
            Builtins.GroupedCovarianceData data = gearWindow.FillGroupedCovarianceParameter();
            if (null == data)
                return null;
            return new ParameterBag(Parameter.Name, new FilledParameter(true, data));
        }

        ParameterBag FillParameter(ITemplateProcessor processor, IntegerParameter Parameter, ParameterBag context)
        {
            // Prompt for the range
            string suffix = "";
            if (Parameter.MinimumValue > Int32.MinValue || Parameter.MaximumValue < Int32.MaxValue)
            {
                suffix = " (";
                if (Parameter.MinimumValue > Int32.MinValue)
                    suffix += Parameter.MinimumValue.ToString();
                suffix += " to ";
                if (Parameter.MaximumValue < Int32.MaxValue)
                    suffix += Parameter.MaximumValue.ToString();
                suffix += ")";
            }
            while (true)
            {
                int defaultValue = 0;
                if (Parameter.HasDefaultValue)
                {
                    defaultValue = Parameter.DefaultValue(processor, context);
                }
                string response = Prompt(Parameter.Prompt(processor, context) + suffix, "StatsDirect", defaultValue.ToString());
                if (string.IsNullOrEmpty(response))
                {
                    if (null != Parameter.CancelSkipsParameter)
                    {
                        return new ParameterBag();
                    }
                    throw new TemplateOperationCancelledException();
                }
                int result = Parsing.Cint_Txt(response);
                if (result >= Parameter.MinimumValue && result <= Parameter.MaximumValue)
                    return new ParameterBag(Parameter.Name, new FilledParameter(true, result));
                // else go round and prompt again
            }
        }

        ParameterBag FillParameter(ITemplateProcessor processor, BooleanParameter Parameter, ParameterBag context)
        {
            DialogResult result = MsgboxX(Parameter.Prompt(processor, context), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, "StatsDirect", true);
            if (DialogResult.Cancel == result)
            {
                if (null != Parameter.CancelSkipsParameter)
                {
                    return new ParameterBag();
                }
                throw new TemplateOperationCancelledException();
            }
            return new ParameterBag(Parameter.Name, new FilledParameter(true, DialogResult.Yes == result));
        }

        ParameterBag FillParameter(ITemplateProcessor processor, DoubleParameter Parameter, ParameterBag context)
        {
            double minimumValue = Parameter.MinimumValue(processor, context);
            double maximumValue = Parameter.MaximumValue(processor, context);
            // Prompt for the range
            string suffix = "";
            if (minimumValue > double.MinValue || maximumValue < double.MaxValue)
            {
                suffix = " (";
                if (minimumValue > double.MinValue)
                    suffix += minimumValue.ToString();
                suffix += " to ";
                if (maximumValue < double.MaxValue)
                    suffix += maximumValue.ToString();
                suffix += ")";
            }
            while (true)
            {
                string defaultValueString = "";
                double? defaultValue = Parameter.DefaultValue(processor, context);
                if (defaultValue.HasValue && !double.IsNaN(defaultValue.Value))
                    defaultValueString = defaultValue.ToString();
                string response = Prompt(Parameter.Prompt(processor, context) + suffix, "StatsDirect", defaultValueString);
                if (string.IsNullOrEmpty(response))
                {
                    if (null != Parameter.CancelSkipsParameter)
                    {
                        return new ParameterBag();
                    }
                    throw new TemplateOperationCancelledException();
                }
                double result = Parsing.Cdbl_Txt(response);
                if (result >= minimumValue && result <= maximumValue)
                    return new ParameterBag(Parameter.Name, new FilledParameter(true, result));
                // else go round and prompt again
            }
        }

        ParameterBag FillParameter(ITemplateProcessor processor, OptionParameter Parameter, ParameterBag context)
        {
            OptionDescriptor descriptor = new OptionDescriptor { Title = Parameter.Prompt(processor, context) };
            foreach (OptionOption opt in Parameter.Options)
            {
                CheckBoxDescriptor cd = new CheckBoxDescriptor { IsExclusive = true, IsRadio = true, Text = opt.Label };
                descriptor.CheckBoxes.Add(cd);
            }
            if (descriptor.CheckBoxes.Count > 0)
                descriptor.CheckBoxes[0].Checked = true;
            if (null == DisplayOptions(descriptor))
            {
                if (null != Parameter.CancelSkipsParameter)
                {
                    return new ParameterBag();
                }
                throw new TemplateOperationCancelledException();
            }
            // Find the selected option - TODO: alter DisplayOptions to take values as well as labels!
            foreach (CheckBoxDescriptor cd in descriptor.CheckBoxes)
                if (cd.Checked)
                    foreach (OptionOption opt in Parameter.Options)
                        if (cd.Text.Equals(opt.Label))
                            return new ParameterBag(Parameter.Name, new FilledParameter(true, opt.Value));
            return null;
        }

        ParameterBag FillParameter(ITemplateProcessor processor, OptionsParameter parameter, ParameterBag context)
        {
            OptionDescriptor descriptor = new OptionDescriptor { Title = parameter.Prompt(processor, context) };
            foreach (OptionsOption opt in parameter.Options)
            {
                CheckBoxDescriptor cd = new CheckBoxDescriptor
                                            {
                                                IsExclusive = false,
                                                IsRadio = false,
                                                Text = opt.Label,
                                                Checked = opt.Selected
                                            };
                descriptor.CheckBoxes.Add(cd);
            }
            if (null == DisplayOptions(descriptor))
            {
                if (null != parameter.CancelSkipsParameter)
                {
                    return new ParameterBag();
                }
                throw new TemplateOperationCancelledException();
            }
            // Find the selected option - TODO: alter DisplayOptions to take values as well as labels!
            ParameterBag outputParameters = new ParameterBag();
            foreach (CheckBoxDescriptor cd in descriptor.CheckBoxes)
                foreach (OptionsOption opt in parameter.Options)
                    if (cd.Text.Equals(opt.Label))
                        outputParameters.Add(opt.Name, new FilledParameter(true, cd.Checked));
            return outputParameters;
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
            return Formatting.pval(p, Properties.Settings.Default.PDecimalPlaces);
        }

        public string pval_half(double p)
        {
            return Formatting.pval_half(p, Properties.Settings.Default.PDecimalPlaces);
        }

        /// <summary>
        /// An expected error has occurred.  Tell the user in a suitable manner.
        /// </summary>
        /// <param name="explanation">An explanation of what the application was doing that caused the error, in terms a user could follow</param>
        /// <param name="ex">The exception that was expected</param>
        /// <param name="showHelpButton"></param>
        internal void FriendlyError(string explanation, Exception ex, bool showHelpButton)
        {
            MsgboxX(explanation + (null == ex ? "" : ("\r\n" + ex.Message)), MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "StatsDirect", showHelpButton);
        }

        public DialogResult MsgboxX(string text, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return MsgboxX(text, buttons, icon, "StatsDirect", false);
        }

        public DialogResult MsgboxX(string text, MessageBoxButtons buttons, MessageBoxIcon icon, string caption, bool showHelpButton, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
        {
            if (showHelpButton)
                return MsgboxX(text, buttons, icon, caption, SoleInstance.ActiveHelpTopic, defaultButton);

            // Use a Windows message box if our own interface isn't visible; use our own if it is.
            if (null == mainWindow || !mainWindow.Visible || mainWindow.WindowState == FormWindowState.Minimized || ModalDialogShowing())
                return MessageBox.Show(mainWindow, text, caption, buttons, icon, defaultButton, 0);
            return mainWindow.ShowModalMessage(text, caption, buttons, icon, defaultButton, null, HelpNavigator.TableOfContents, null);
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

        public ParameterBag DisplayOptions(OptionDescriptor Descriptor)
        {
            return AmendUsingControl(Descriptor);
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

        public string GetString(string Prompt, string Caption, string DefaultValue)
        {
            return this.Prompt(Prompt, Caption, DefaultValue);
        }

        public void Error(string Message, string Caption)
        {
            MsgboxX(Message, MessageBoxButtons.OK, MessageBoxIcon.Error, Caption, true);
        }

        public void StartProgress(string operationDescription)
        {
            if (null != mainWindow)
                mainWindow.StartProgress(operationDescription);
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
                case "Scores":
                case "SortInPlace":
                    return AmendUsingControl(fillable);
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
        /// <param name="Prompt"></param>
        /// <param name="Caption"></param>
        /// <param name="DefaultValue"></param>
        /// <returns>null if the user cancelled, otherwise the entered value.</returns>
        internal string Prompt(string Prompt, string Caption, string DefaultValue)
        {
            using (frmInputBox ib = new frmInputBox(Prompt, Caption, DefaultValue))
            {
                ib.ShowDialog(mainWindow);
                if (ib.UserCancelled)
                    return null;
                return ib.Value;
            }
        }

        bool ITemplateHost.CanPresentPanel
        {
            get { return false; }
        }

        bool ITemplateHost.CanPresentWindow
        {
            get { return false; }
        }

        public string zvalp1(double xz)
        {
            double P = 1 - Numerics.PDF.alnorm(xz);
            if (P > 1 - P)
                P = 1 - P;
            return pval(P);
        }

        public string zvalp2(double xz)
        {
            double P = 1 - Numerics.PDF.alnorm(xz);
            if (P > 1 - P)
                P = 1 - P;
            return pval(P * 2);
        }

        internal void NoteRecentFile(string path, bool openedOk)
        {
            // Ensure the path is the most recently used and appears no more than once; ensure no more than MAX_RECENT_FILES files are kept
            System.Collections.Specialized.StringCollection recentFiles = Properties.Settings.Default.RecentFileList ??
                                                                          new System.Collections.Specialized.StringCollection();
            if (recentFiles.Contains(path))
                recentFiles.Remove(path);
            if (openedOk)
            {
                recentFiles.Add(path);
                if (recentFiles.Count > MAX_RECENT_FILES)
                    recentFiles.RemoveAt(0);
            }
            Properties.Settings.Default.RecentFileList = recentFiles;
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
                    string mySdPath = SDConfiguration.InstallationDirectory;
                    string defaultRecentlyUsedFile = Properties.Settings.Default.DefaultRecentlyUsedFile;
                    string defaultRecentlyUsedPath = Path.Combine(mySdPath, defaultRecentlyUsedFile);
                    output.Add(defaultRecentlyUsedPath);
                }
                return output;
            }
        }

        public bool CheckScale(ScaleParameters scaleParameters)
        {
            using (frmScale f = new frmScale(scaleParameters))
            {
                f.ShowDialog(mainWindow);
                return !f.Cancelled;
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

        public void Refill(Variable variable)
        {
            IOrigin origin = variable.Origin;
            if (null == origin)
            {
                // Hope it's got its data still included!
                return;
            }
            if (!(origin is WorksheetOrigin))
            {
                throw new NotImplementedException("At present, only data taken from worksheets can be refilled");
            }

            WorksheetOrigin worksheetOrigin = (WorksheetOrigin)origin;
            Refill(variable, worksheetOrigin);
        }

        public void Refill(Variable variable, WorksheetOrigin worksheetOrigin)
        {
            if (null == worksheetOrigin)
                return;
            if (null == worksheetOrigin.WorkbookPath)
                return;
            StatsDirectForm gridWindow = mainWindow.FindOrOpenGrid(worksheetOrigin.WorkbookPath);
            if (null == gridWindow)
            {
                throw new Exception("Cannot refill variable as the workbook \"" + worksheetOrigin.WorkbookPath + "\" no longer exists.");
            }
            IGrid grid = (IGrid)gridWindow;
            grid.Refill(variable, worksheetOrigin);
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
            using (Form f = new frmUpdateCheck())
            {
                f.ShowDialog(mainWindow);
            }
        }

        internal void CloseAndUpdate()
        {
            if (null != mainWindow)
            {
                closingForUpgrade = true;
                mainWindow.Close();
            }
        }

        public bool ClosingForUpgrade
        {
            get { return closingForUpgrade; }
        }
    }

    /// <summary>
    /// A shim to allow chart options to be passed around as Parameters, and hence filled in by the UI
    /// </summary>
    internal class ChartOptionsParameter : Parameter
    {
        private readonly Charting.ChartDefinition chartDefinition;

        public ChartOptionsParameter(string name, Charting.ChartDefinition chartDefinition)
        {
            Name = name;
            this.chartDefinition = chartDefinition;
        }

        public Charting.ChartDefinition ChartDefinition
        {
            get { return chartDefinition; }
        }

        public override ParameterType Type
        {
            get { return ParameterType.Custom; }
        }
    }

    /// <summary>
    /// A shim to allow fillables to be passed around as Parameters, and hence filled in by the UI.
    /// </summary>
    internal class FillableParameter : Parameter
    {
        private readonly IFillable fillable;

        public FillableParameter(string name, IFillable fillable)
        {
            Name = name;
            this.fillable = fillable;
        }

        public IFillable Fillable
        {
            get { return fillable; }
        }

        public override ParameterType Type
        {
            get { return ParameterType.Custom; }
        }
    }
}
