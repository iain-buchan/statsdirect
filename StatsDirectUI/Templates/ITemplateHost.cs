using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using StatsDirect.Data;
using StatsDirect.UI;
using StatsDirect.Utilities;

namespace StatsDirect.Templates
{
    /// <summary>
    /// The signature of any function that can be called as a builtin.
    /// </summary>
    /// <param name="host">The host in which the function is running</param>
    /// <param name="parameters">Name-to-object mappings for any parameters that are handed to the builtin</param>
    /// <returns>A new set of name-to-object mappings.  Builtins *must not* alter Parameters and hand it back; they *must* allocate a new Dictionary.</returns>
    public delegate StepResult BuiltinFunction(ITemplateHost host, ParameterBag parameters);

    public class Builtin : IMightRequireInput
    {
        readonly string name;
        private readonly BuiltinFunction func;
        private readonly InputDuringStep requiresInput;

        public Builtin(string name, BuiltinFunction func, InputDuringStep requiresInput)
        {
            this.name = name;
            this.func = func;
            this.requiresInput = requiresInput;
        }

        public InputDuringStep RequiresInput
        {
            get { return requiresInput; }
        }

        public BuiltinFunction FunctionToCall
        {
            get { return func; }
        }

        public string Name
        {
            get { return name; }
        }
    }

    /// <summary>
    /// An application capable of hosting the template language.
    /// </summary>
    public interface ITemplateHost
    {
        /// <summary>
        /// Presents the specified options to the user in some appropriate way; modifies the options in-place with the user's selections.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <returns>a parameter bag if the options are to be used, null if the user cancelled the option selection.</returns>
        ParameterBag DisplayOptions(OptionDescriptor descriptor);

        string RoundU(double amount);

        /// <summary>
        /// Format a probability, using the default number of decimal places
        /// </summary>
        string pval(double p);

        string pval_half(double p);

        string zvalp1(double xz);
        string zvalp2(double xz);

        bool GetBoolean(string prompt, string Title, bool initialValue, out bool cancelled);
        bool GetBoolean(string prompt, string Title, bool InitialValue, int HelpIndex, out bool cancelled);
        double GetDouble(string prompt, string Title, double InitialValue, out bool cancelled);
        int GetInteger(string prompt, string Title, int initialValue, out bool cancelled);

        /// <summary>
        /// Prompt the user for a string; return the user-entered string, or Nothing if the user cancels.
        /// </summary>
        /// <returns>The user-entered string, or Nothing if the user cancels</returns>
        /// <remarks></remarks>
        string GetString(string prompt, string title, string initialValue);

        bool MetaPlotCI { get; }

        int MetaPlotMethod { get; }

        bool CheckScale(ScaleParameters scaleParameters);
        /// <summary>
        /// Cause the report to be output in some way, for example by asking the user where to render it, then rendering it.
        /// </summary>
        /// <param name="rtf"></param>
        /// <param name="operation"></param>
        /// <param name="redoInformation">Any text that should be stored to assist in redoing the operation with new data at a later date</param>
        /// <param name="preferredOutputLocation">If non-null, indicates a possible host-controlled place to put the output</param>
        /// <returns>The host-assigned identity of the report that was used, or null if the report was not output at all.</returns>
        object OutputReport(string rtf, Operation operation, string redoInformation, object preferredOutputLocation);

        /// <summary>
        /// The user (or similar decision-maker) should be allowed to amend whatever is deemed appropriate of the options.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="context"></param>
        /// <returns>a parameter bag if the options shold be processed, null if the user cancelled or the operation failed</returns>
        ParameterBag Amend(IFillable options, ParameterBag context);

        /// <summary>
        /// Return a clean, initialised instance of a script engine capable of running code in the specified language.
        /// </summary>
        /// <returns></returns>
        IScriptEngine GetScriptEngine(string language);

        /// <summary>
        /// Show/log an error to the user.
        /// </summary>
        /// <param name="Message"></param>
        /// <param name="Caption"></param>
        void Error(string Message, string Caption);

        /// <summary>
        /// Show/log a warning to the user.
        /// </summary>
        /// <param name="Message"></param>
        /// <param name="Caption"></param>
        void Warning(string Message, string Caption);

        /// <summary>
        /// Ask the user an OK/Cancel question.
        /// </summary>
        /// <param name="Message"></param>
        /// <param name="Caption"></param>
        /// <returns>true if the user selected OK, false if the user selected Cancel</returns>
        bool Query(string Message, string Caption);

        /// <summary>
        /// Returns true if the host is willing to combine this parameter with any previous parameters requested with FillParameter and shouldCombine=true; false if not.
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        bool CanCombine(Parameter parameter);

        /// <summary>
        /// Present and allow the user to fill any outstanding parameters.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="context">The already filled-in parameters - some parameters require these</param>
        /// <returns>The filled-in parameters</returns>
        /// <exception cref="TemplateOperationCancelledException">if the user cancels the acquisition of the parameter</exception>
        ParameterBag FillCombinedParameters(ITemplateProcessor processor, ParameterBag context);

        /// <summary>
        /// Get the value(s) of the user-entered parameter(s) and return them in a new ParameterBag to be merged with the other parameters.
        /// This must be able to deal with all subclasses of Parameter.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="Parameter"></param>
        /// <param name="context">The already filled-in parameters - some parameters require these</param>
        /// <param name="shouldCombine">If true, the host should batch this Parameter with any others it receives until a call to FillCombinedParameters.  If false, the parameter should be filled immediately.</param>
        /// <returns>The filled-in parameters, or null if shouldCombine is true</returns>
        /// <exception cref="TemplateOperationCancelledException">if the user cancels the acquisition of the parameter</exception>
        ParameterBag FillParameter(ITemplateProcessor processor, Parameter Parameter, ParameterBag context, bool shouldCombine);

        /// <summary>
        /// Notes that this user-entered parameter will at some point be required, and prepares to produce it.
        /// This must be able to deal with all subclasses of Parameter.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="Parameter"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        void PrepareParameter(ITemplateProcessor processor, Parameter Parameter, ParameterBag context);

        /// <summary>
        /// Cause the frame to be output in some way, for example by asking the user in which grid to put it, then filling in the grid.
        /// </summary>
        /// <param name="frame">The frame to be output</param>
        /// <param name="keepSelection">If true, the host should keep outputting to the current location; if false, the host should internally SelectOutputForFrame().</param>
        /// <param name="isFormulae">If true, the frame is assumed to contain strings to be set as formulae; if false, the frame is assumed to contain data values.</param>
        /// <param name="missingIndicator">The text to place in the output if a value in the frame is missing.</param>
        /// <param name="preferredOutputLocation">If non-null, indicates a possible host-controlled place to put the output</param>
        void OutputFrame(DataFrame frame, bool keepSelection, bool isFormulae, string missingIndicator, PaneAndPosition preferredOutputLocation);
        /*
        /// <summary>
        /// In systems that have a choice of output location for frames (such as multiple grids), ask the user where they want to output future frames.
        /// </summary>
        void SelectOutputForFrame();
        */
        /// <summary>
        /// Notes that an operation has started with the specified description that might take a while.
        /// A host might elect to show a progress bar at this point, for example.
        /// </summary>
        /// <param name="operationDescription"></param>
        void StartProgress(string operationDescription);

        /// <summary>
        /// Notes that any current progress operation has now finished.
        /// A host that shows a progress bar might hide the progress bar, for example.
        /// </summary>
        void FinishProgress();

        /// <summary>
        /// A long-running operation is now fractionComplete complete, ranging from 0.0 to 1.0.
        /// A host may elect to update its progress notification.
        /// </summary>
        /// <param name="fractionComplete"></param>
        /// <returns>true if the caller should abandon the long-running operation, false if the operation should continue.</returns>
        bool UpdateProgress(double fractionComplete);

        /// <summary>
        /// A location to store parameters that will persist as long as the host does and are kept per-operation.
        /// </summary>
        IDictionary<string, ParameterBag> SessionParametersPerOperation { get; }

        /// <summary>
        /// A location to store parameters that will persist as long as the host does and are common across all operations that use the same name for their parameters.
        /// </summary>
        ParameterBag SessionParametersAcrossOperations { get; }

        int PDecimalPlaces
        {
            get;
        }

        /// <summary>
        /// A non-fatal exception has occurred (generally in calculation).
        /// The process will continue, but the user should be warned not to rely on the results of the operation.
        /// </summary>
        /// <param name="ex">The exception that caused the problem, in case it's of any use.</param>
        void NoteError(Exception ex);

        /// <summary>
        /// The current set of preferences.
        /// Hosts MAY provide a way to change these through the interface, and SHOULD persist them between invocations.
        /// </summary>
        SDPreferences Preferences
        {
            get;
        }

        Operation Operation
        {
            get;
            set;
        }

        /// <summary>
        /// True iff the host is able to present a .Net panel to obtain data (i.e. it's SD3 and we're running interactively)
        /// </summary>
        bool CanPresentPanel
        {
            get;
        }

        /// <summary>
        /// True iff the host is able to present a .Net window to obtain data (i.e. it's SD3 and we're running interactively)
        /// </summary>
        bool CanPresentWindow
        {
            get;
        }

        /// <summary>
        /// Show the specified built-in help content.
        /// </summary>
        /// <param name="helpContextId"></param>
        /// <remarks>Shouldn't really be in the interface, but it's occasionally useful.</remarks>
        void ShowHelp(int helpContextId);

        IDictionary<string, object> Session
        {
            get;
        }
    }
}
