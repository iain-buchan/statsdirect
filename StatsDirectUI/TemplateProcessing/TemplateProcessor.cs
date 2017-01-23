using System;
using System.Collections.Generic;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.UI;
using StatsDirect.Utilities;
using System.Globalization;

namespace StatsDirect.Templates
{
    /// <summary>
    /// An interface-agnostic template operation processor.
    /// </summary>
    public sealed class TemplateProcessor : ITemplateProcessor
    {
        private readonly ITemplateHost host;
        private readonly TakeANumber takeAnOriginGroup;
        private const string STATSDIRECT_CHART_OPTIONS = "statsdirect-chart-options";
        private const string STATSDIRECT_CHART_SCALE_PARAMETERS = "statsdirect-chart-scale-parameters";
        private const string STATSDIRECT_FRAME_PANE = "statsdirect-frame-pane";
        private const string STATSDIRECT_REPORT_PANE = "statsdirect-report-pane";

        public TemplateProcessor(ITemplateHost host)
        {
            this.host = host;
            this.takeAnOriginGroup = new TakeANumber();
        }

        /// <summary>
        /// Run the operation to completion or error.
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="startingParameters">If non-null, some parameters to be used as defaults.</param>
        /// <param name="isRedo"> </param>
        public ParameterBag Execute(Operation operation, ParameterBag startingParameters, bool isRedo)
        {
            host.Operation = operation;
            ParameterBag filledParameters = startingParameters ?? new ParameterBag();

            // Check preconditions; fail if any fail.
            foreach (Precondition precondition in operation.Preconditions)
            {
                if (!precondition.Check(this, filledParameters))
                {
                    host.Error(precondition.FailureMessage, "Cannot run operation");
                    return null;
                }
            }

            // Prepare the steps, to give an opportunity for some parts of the system to set themselves up
            foreach (Step step in operation.Steps)
            {
                Prepare(step, filledParameters);
            }

            // Run each step in turn
            foreach (Step step in operation.Steps)
            {
                try
                {
                    filledParameters = Execute(step, filledParameters, isRedo);
                }
                catch (InvalidDataException)
                {
                    throw;
                }
                catch (TemplateOperationCancelledException)
                {
                    // The user cancelled the operation
                    return null;
                }
            }
            host.Operation = null;
            return filledParameters;
        }

        /// <summary>
        /// Prepare the operation with the passed-in parameters.
        /// </summary>
        /// <param name="step"></param>
        /// <param name="parameters"></param>
        private void Prepare(Step step, ParameterBag parameters)
        {
            step.PrepareInternal(this, parameters);
        }

        public void PrepareInternal(ParametersStep step, ParameterBag parameters)
        {
            foreach (Parameter parameter in step.Parameters)
            {
                host.PrepareParameter(this, parameter, parameters);
            }
        }

        /// <summary>
        /// Execute the operation with the passed-in parameters, returning some results that can be used for the next operation.
        /// Implementers <strong>must</strong> ensure that a new dictionary is used for the output.
        /// </summary>
        /// <param name="step"></param>
        /// <param name="parameters"></param>
        /// <param name="isRedo"></param>
        /// <returns></returns>
        private ParameterBag Execute(Step step, ParameterBag parameters, bool isRedo)
        {
            ParameterBag result = null;
            if (null != parameters)
                result = step.ExecuteInternal(this, parameters, isRedo);

            // If required, transfer input parameters where the same name is not already present in the results.
            if (null != result)
            {
                if (step.ShouldCopyInputParameters)
                {
                    foreach (KeyValuePair<string, FilledParameter> inputParameter in parameters.Pairs)
                        if (!result.ContainsKey(inputParameter.Key))
                            result.Add(inputParameter.Key, inputParameter.Value);
                }
                // Remove explicit blanks now that they have prevented copying.
                List<string> keysToRemove = new List<string>();
                foreach (KeyValuePair<string, FilledParameter> pair in result.Pairs)
                    if (null == pair.Value)
                        keysToRemove.Add(pair.Key);
                foreach (string keyToRemove in keysToRemove)
                    result.Remove(keyToRemove);
            }
            return result;
        }

        public ParameterBag ExecuteInternal(BuiltinStep step, ParameterBag parameters, bool isRedo)
        {
            if (null == parameters)
                throw new ArgumentOutOfRangeException("parameters", "parameters must be a dictionary and cannot be null. Did a previous script step return null?");
            Builtin builtin = BuiltinRegistry.SoleInstance.Builtin(step.FunctionName);
            if (null == builtin)
                throw new Exception("No function '" + step.FunctionName + "' is supplied by the host.");
            BuiltinFunction toCall = builtin.FunctionToCall;
            ParameterBag outputResult = toCall(host, parameters);
            // Ensure no stray progress bars stay around
            host.FinishProgress();
            return outputResult;
        }

        void FillChartDefinition(ChartStep step, ParameterBag parameters, bool isRedo, ChartDefinition definition, string dataName)
        {
            definition.ScaleParameters = MaybeFindScaleParameters(step, parameters, isRedo);
            definition.ChartOptions = FindOrPreprocessChartOptions(step, parameters, isRedo, definition, dataName);

            // TODO: Gross hack (see #993): Forest lin/log depends on the chart options for the data.
            if (step.ChartType == ChartType.Forest)
            {
                // #987: If the summary statistic variable ("odds") contains the word "ratio" then select log plot by default, else linear plot
                if (definition.ChartOptions.XAxisTitle.Contains("ratio") || definition.ChartOptions.XAxisTitle.Contains("Ratio"))
                    definition.ScaleParameters.X.ScaleType = ScaleType.Log10;
                else
                    definition.ScaleParameters.X.ScaleType = ScaleType.Linear;
            }

            if (step.RequestUserInput)
            {
                if (null == host.Amend(definition, parameters))
                    throw new TemplateOperationCancelledException();
            }
            ChartOptionProcessor.PostProcessFilledChartOptions(definition);
        }

        private ChartOptions FindOrPreprocessChartOptions(ChartStep step, ParameterBag parameters, bool isRedo, ChartDefinition definition, string dataName)
        {
            // If we're redoing a previous operation, we should in theory have the previous ChartOptions.  Go look!
            if (isRedo)
            {
                string possibleParameterName = STATSDIRECT_CHART_OPTIONS + (step.ChartName ?? string.Empty);
                FilledParameter fp;
                if (parameters.TryGetValue(possibleParameterName, out fp))
                {
                    if (null != fp && fp.HasData)
                        return fp.AsChartOptions;
                }
            }

            // If we're not redoing, or we can't find the options, then we need to fill them in now.
            return ChartOptionProcessor.PreprocessChartOptions(step, parameters, definition, dataName, host);
        }

        private static ScaleParameters MaybeFindScaleParameters(ChartStep step, ParameterBag parameters, bool isRedo)
        {
            // If we're redoing a previous operation, we should in theory have the previous ScaleParameters.  Go look!
            if (isRedo)
            {
                string possibleParameterName = STATSDIRECT_CHART_SCALE_PARAMETERS + (step.ChartName ?? string.Empty);
                FilledParameter fp;
                if (parameters.TryGetValue(possibleParameterName, out fp))
                {
                    if (null != fp && fp.HasData)
                        return fp.AsScaleParameters;
                }
            }

            // If we're not redoing, or we can't find the options, then leave blank and they'll be filled later.
            return null;
        }

        public ParameterBag ExecuteInternal(ChartStep step, ParameterBag parameters, bool isRedo)
        {
            ChartDefinition definition = new ChartDefinition { ChartType = step.ChartType };
            // Series: First X...
            string dataName = null;
            if (null != step.XSeriesDataName)
            {
                if (!parameters.ContainsKey(step.XSeriesDataName))
                    throw new Exception("Chart expected parameter \"" + step.XSeriesDataName + "\", which was not supplied");
                DataFrame frame = parameters[step.XSeriesDataName].AsDataFrame;
                for (int v = 0; v < frame.VariableCount; v++)
                {
                    DoubleVariable variable = frame.Variables[v]as DoubleVariable;
                    definition.AddXSeriesAt(ChartOptionProcessor.VariableToSeries(variable), v);
                }
                dataName = frame.Name;
            }
            // ... then Y
            if (null != step.YSeriesDataName)
            {
                if (!parameters.ContainsKey(step.YSeriesDataName))
                    throw new Exception("Chart expected parameter \"" + step.YSeriesDataName + "\", which was not supplied");
                DataFrame frame = parameters[step.YSeriesDataName].AsDataFrame;
                for (int v = 0; v < frame.VariableCount; v++)
                {
                    DoubleVariable variable = frame.Variables[v]as DoubleVariable;
                    definition.AddYSeriesAt(ChartOptionProcessor.VariableToSeries(variable), v);
                }
                dataName = frame.Name;
            }

            FillChartDefinition(step, parameters, isRedo, definition, dataName);
            string xAxisTitle = step.XAxisTitle(this, parameters);
            string yAxisTitle = step.YAxisTitle(this, parameters);
            if (!string.IsNullOrEmpty(xAxisTitle))
                definition.ChartOptions.XAxisTitle = xAxisTitle;
            if (!string.IsNullOrEmpty(yAxisTitle))
                definition.ChartOptions.YAxisTitle = yAxisTitle;

            // Plot to metafile if ascii, text otherwise
            ParameterBag results;
            using (IChartRenderer ch = ChartRendererFactory.ChartRendererFor(definition))
            {
                ch.IsAscii = step.IsAscii;
                string rtf;
                results = RtfImageRenderer.PlotAndReturnRtf(host, ch, out rtf);
                results.Add(step.ChartName, new FilledParameter(FilledParameterDirection.Output, rtf));
                SaveChartDefinition(step, results, definition);
            }
            return results;
        }

        private static void SaveChartDefinition(ChartStep step, ParameterBag results, ChartDefinition definition)
        {
            results.Add(STATSDIRECT_CHART_OPTIONS + (step.ChartName ?? string.Empty), new FilledParameter(FilledParameterDirection.Input, definition.ChartOptions));
            results.Add(STATSDIRECT_CHART_SCALE_PARAMETERS + (step.ChartName ?? string.Empty), new FilledParameter(FilledParameterDirection.Input, definition.ScaleParameters));
        }

        public ParameterBag ExecuteInternal(IterationStep step, ParameterBag parms, bool isRedo)
        {
            // Detect bounds: default 0 to 0 inclusive (1 iteration), then add any fixed values, then any variables if found.
            int lower = 0;
            int upper = 0;
            if (step.LowerBound.HasValue)
            {
                lower = step.LowerBound.Value;
            }
            if (step.UpperBound.HasValue)
            {
                upper = step.UpperBound.Value;
            }
            if (null != step.LowerBoundParameterName)
            {
                if (parms.ContainsKey(step.LowerBoundParameterName))
                    lower = parms[step.LowerBoundParameterName].AsInt32;
            }
            if (null != step.UpperBoundParameterName)
            {
                if (parms.ContainsKey(step.UpperBoundParameterName))
                    upper = parms[step.UpperBoundParameterName].AsInt32;
            }
            ParameterBag filledParameters = new ParameterBag();
            for (int i = lower; i <= upper; i++)
            {
                if (null != step.LoopVariableName)
                {
                    filledParameters[step.LoopVariableName] = new FilledParameter(FilledParameterDirection.Output, i);
                }
                foreach (Step s in step.Steps)
                {
                    // TODO: How to handle execution failures?
                    ParameterBag result = s.ExecuteInternal(this, filledParameters, isRedo);
                    // Add in any required parameters, combining everything into one big mass of outputs.
                    // Overwrite earlier loop results with later ones.
                    foreach (string k in result.Keys)
                        filledParameters[k] = result[k];
                }
            }
            return filledParameters;
        }

        public ParameterBag ExecuteInternal(OutputFrameStep step, ParameterBag parameters, bool isRedo)
        {
            DataFrame frame = parameters[step.ParameterName].AsDataFrame;
            if (null != frame)
            {
                PaneAndPosition preferredPaneAndPosition = null;
                if (parameters.ContainsKey(STATSDIRECT_FRAME_PANE)
                    && null != parameters[STATSDIRECT_FRAME_PANE])
                    preferredPaneAndPosition = parameters[STATSDIRECT_FRAME_PANE].AsPaneAndPosition;
                host.OutputFrame(frame, step.KeepSelection, step.IsFormulae, step.MissingIndicator, preferredPaneAndPosition, step.DefaultPlacement);
            }
            return new ParameterBag();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="step"></param>
        /// <param name="parms"></param>
        /// <param name="isRedo"></param>
        /// <returns></returns>
        /// <remarks>Note that this may return parameters with null values; it is up to the caller to remove these.</remarks>
        public ParameterBag ExecuteInternal(ParametersStep step, ParameterBag parms, bool isRedo)
        {
            // If we're redoing a previous operation, then all parameters are taken from the previous operation.  We do not request any.
            if (isRedo)
                return new ParameterBag();

            try
            {
                ParameterBag filledParameters = new ParameterBag();
                List<Parameter> outstandingParameters = new List<Parameter>();
                foreach (Parameter parameter in step.Parameters)
                {
                    // If the parameter is already present in the input bag, and we're copying the input, skip acquiring it again.
                    // This is typically due to this being a follow-on from another operation, and some parameters already being set.
                    if (step.ShouldCopyInputParameters && !parameter.MustRequest && null != parameter.Name && parms.ContainsKey(parameter.Name))
                        continue;

                    // Check prerequisites and skip this parameter if they're not met.
                    if (null != parameter.RequiresParameter)
                    {
                        // The required parameter must be present...
                        if (!filledParameters.ContainsKey(parameter.RequiresParameter))
                            continue;
                        // ... and non-blank.
                        if (null == filledParameters[parameter.RequiresParameter])
                            continue;
                    }

                    // Fill and validate the parameter
                    // Union parms with filledParameters before we pass in, so that this has access to earlier parameters in the same series
                    ParameterBag parmsAndFilledParameters = CombinePreferringLater(parms, filledParameters);

                    // If we don't already have the parameter and its lifetime is something other than just this operation, see whether it's already in the session
                    if (parameter.Lifetime == ParameterLifetime.SessionForThisOperation && null != parameter.Name)
                        TryToRecallParameterForThisOperation(filledParameters, parameter);
                    else if (parameter.Lifetime == ParameterLifetime.SessionForAllOperations && null != parameter.Name)
                        TryToRecallParameterForAllOperations(filledParameters, parameter);

                    // Try to combine requests for parameters where possible.  The host can always refuse a request.
                    bool shouldCombine = host.CanCombine(parameter);
                    if (!shouldCombine)
                    {
                        // We've hit a parameter we should not or cannot combine.  Ensure any grouped parameters are handled at this point.
                        // We also know that we definitely cannot request an output window at this point - so don't!
                        if (outstandingParameters.Count > 0)
                        {
                            // Fill in and validate previous parameters
                            ParameterBag outstandingFilledParameters = host.FillAndValidateCombinedParameters(this, parmsAndFilledParameters);
                            if (null == outstandingFilledParameters)
                                throw new TemplateOperationCancelledException();
                            foreach (Parameter outstandingParameter in outstandingParameters)
                                MaybeRemember(outstandingParameter, outstandingFilledParameters);
                            foreach (KeyValuePair<string, FilledParameter> pair in outstandingFilledParameters.Pairs)
                            {
                                // Handle removal of explicit blanks
                                if (null == pair.Value)
                                {
                                    // Don't copy this parameter over; but remove it from filledParameters if present.
                                    if (filledParameters.ContainsKey(pair.Key))
                                        filledParameters.Remove(pair.Key);
                                }
                                else
                                    filledParameters[pair.Key] = pair.Value;
                            }
                            outstandingParameters.Clear();
                        }
                        // Ensure recently-acquired parameters are added to the context for the next parameter acquisition
                        parmsAndFilledParameters = CombinePreferringLater(parms, filledParameters);
                    }
                    ParameterBag newFilledParameters = host.FillParameter(this, parameter, parmsAndFilledParameters, shouldCombine);
                    if (shouldCombine)
                    {
                        outstandingParameters.Add(parameter);

                        // If the parameter has (a) default value(s), add any that aren't already known (and hence overridden) to the context.
                        // This is critical when first displaying e.g. a set of controls where one isn't displayed initially due to a default value in another.
                        // Bug #1244
                        ParameterBag oldAndNewFilledParameters = CombinePreferringLater(parmsAndFilledParameters, newFilledParameters);
                        ParameterBag defaults = parameter.AllDefaults(this, oldAndNewFilledParameters);
                        foreach (KeyValuePair<string, FilledParameter> fp in defaults.Pairs)
                        {
                            if (null != fp.Value && fp.Value.HasData)
                            {
                                if (null == filledParameters)
                                    filledParameters = defaults;
                                else if (!filledParameters.ContainsKey(fp.Key))
                                    filledParameters.Add(fp);
                            }
                        }
                    }
                    else
                    {
                        MaybeRemember(parameter, newFilledParameters);
                        if (null != newFilledParameters)
                            foreach (KeyValuePair<string, FilledParameter> pair in newFilledParameters.Pairs)
                                filledParameters.Add(pair.Key, pair.Value);
                    }
                }

                // We've reached the end of the list.  Ensure any grouped parameters are handled at this point.
                // We might also be able to request output frame or report parameters now, if the operation doesn't do anything else.
                if (outstandingParameters.Count > 0)
                {
                    // Union parms with filledParameters before we pass in, so that this has access to earlier parameters in the same series
                    ParameterBag parmsAndFilledParameters = CombinePreferringLater(parms, filledParameters);

                    Step frameStep;
                    if (step.Operation.ShouldRequestTargetAfter(step, typeof(OutputFrameStep), out frameStep) == HasInput.NoAndTypeFound)
                    {
                        RelativePosition rp = (null == frameStep) ? RelativePosition.AfterSelection : ((OutputFrameStep)frameStep).DefaultPlacement;
                        string missingIndicator = null == frameStep ? Formatting.ASTERISK : ((OutputFrameStep)frameStep).MissingIndicator;
                        SpecialParameter frameParameter = new SpecialParameter { Name = STATSDIRECT_FRAME_PANE, SpecialType = "frame", ExtraData = new object[] { rp, missingIndicator } };
                        host.FillParameter(this, frameParameter, parmsAndFilledParameters, true);
                    }

                    // Handle previous parameter fill-in, validation and combination
                    ParameterBag outstandingFilledParameters = host.FillAndValidateCombinedParameters(this, parmsAndFilledParameters);
                    if (null == outstandingFilledParameters)
                        throw new TemplateOperationCancelledException();
                    foreach (Parameter outstandingParameter in outstandingParameters)
                        MaybeRemember(outstandingParameter, outstandingFilledParameters);
                    foreach (KeyValuePair<string, FilledParameter> pair in outstandingFilledParameters.Pairs)
                        filledParameters[pair.Key] = pair.Value;
                    outstandingParameters.Clear();
                }
                return filledParameters;
            }
            catch (NotAnErrorException)
            {
                // We don't ever want this caught by the general exception catcher below, so we make a special case.
                throw;
            }
#if !WATCH_EXCEPTIONS
            catch (Exception ex)
            {
                // Fail the operation
                host.Error("Internal error: " + ex.Message, "Operation terminated");
                throw new Utilities.TemplateOperationCancelledException();
            }
#endif
        }

        /// <summary>
        /// Combine bag1 and bag2 into a new bag (returned).  Where bag1 and bag2 contain the same parameter, prefer the one from bag2 unless it is a default parameter (in which case prefer bag1).
        /// </summary>
        /// <param name="bag1"></param>
        /// <param name="bag2"></param>
        /// <returns></returns>
        private static ParameterBag CombinePreferringLater(ParameterBag bag1, ParameterBag bag2)
        {
            ParameterBag combinedParameters = new ParameterBag();
            if (null != bag2)
                foreach (KeyValuePair<string, FilledParameter> pair in bag2.Pairs)
                    combinedParameters.Add(pair);

            // Add/overwrite with older parameters if (a) there is no matching newer parameter or (b) the newer parameter is a default, in which case we want the real value.
            if (null != bag1)
            {
                foreach (KeyValuePair<string, FilledParameter> pair in bag1.Pairs)
                    if ((!combinedParameters.ContainsKey(pair.Key)) || combinedParameters[pair.Key].Direction == FilledParameterDirection.Default)
                        combinedParameters[pair.Key] = pair.Value;
            }

            return combinedParameters;
        }

        private void TryToRecallParameterForAllOperations(ParameterBag filledParameters, Parameter parameter)
        {
            // Check the parameter isn't already known to us.  Only known input parameters should be checked here; if it's a default parameter we still choose to recall and overwrite it.
            if (filledParameters.ContainsKey(parameter.Name) && filledParameters[parameter.Name].IsInputParameter)
                return;

            TryToRecallSavedParameterFromBag(host.SessionParametersAcrossOperations, filledParameters, parameter.Name);
        }

        private void TryToRecallParameterForThisOperation(ParameterBag filledParameters, Parameter parameter)
        {
            // Check the parameter isn't already known to us.  Only known input parameters should be checked here; if it's a default parameter we still choose to recall and overwrite it.
            if (filledParameters.ContainsKey(parameter.Name) && filledParameters[parameter.Name].IsInputParameter)
                return;

            IDictionary<string, ParameterBag> savedParametersPerOperation = host.SessionParametersPerOperation;
            if (parameter is OptionsParameter)
            {
                if (savedParametersPerOperation.ContainsKey(parameter.Operation.Name))
                {
                    ParameterBag savedParameters = savedParametersPerOperation[parameter.Operation.Name];
                    foreach (OptionsOption opt in ((OptionsParameter)parameter).Options)
                        TryToRecallSavedParameterFromBag(savedParameters, filledParameters, opt.Name);
                }
            }
            else
            {
                if (savedParametersPerOperation.ContainsKey(parameter.Operation.Name))
                {
                    ParameterBag savedParameters = savedParametersPerOperation[parameter.Operation.Name];
                    TryToRecallSavedParameterFromBag(savedParameters, filledParameters, parameter.Name);
                }
            }
        }

        private void TryToRecallSavedParameterFromBag(ParameterBag savedParameters, ParameterBag filledParameters, string name)
        {
            FilledParameter savedParameter;
            if (savedParameters.TryGetValue(name, out savedParameter))
            {
                // #1289: In rare cases, operations overwrite input parameters with outputs and the outputs get saved to session.ser. To allow us to use old (arguably corrupt) session files rather than insist everyone deletes them, filter out problematic values.
                if (savedParameter.IsInputParameter)
                    filledParameters.Add(name, savedParameter);
            }
        }

        /// <summary>
        /// A parameter has just been acquired.  If it should be remembered for the session, remember it.
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="parameterBag"></param>
        private void MaybeRemember(Parameter parameter, ParameterBag parameterBag)
        {
            // Null parameter bags come from cancelling optional parameters.
            if (null == parameterBag)
                return;

            // Multiple option parameters have null names; don't fill these at present.
            // TODO: Fill multiple-option parameters specially
            if (null == parameter || null == parameter.Name)
                return;

            switch (parameter.Lifetime)
            {
                case ParameterLifetime.SessionForThisOperation:
                    {
                        IDictionary<string, ParameterBag> savedParametersPerOperation = host.SessionParametersPerOperation;
                        ParameterBag savedParameterBag;
                        if (parameter is OptionsParameter)
                        {
                            foreach (OptionsOption opt in ((OptionsParameter)parameter).Options)
                            {
                                // Find the parameter to remember.  If it's not present in the bag, do nothing.
                                FilledParameter filledParameterToSave;
                                if (!parameterBag.TryGetValue(opt.Name, out filledParameterToSave))
                                    continue;

                                if (!savedParametersPerOperation.TryGetValue(parameter.Operation.Name, out savedParameterBag))
                                {
                                    savedParameterBag = new ParameterBag();
                                    savedParametersPerOperation.Add(parameter.Operation.Name, savedParameterBag);
                                }
                                savedParameterBag[opt.Name] = filledParameterToSave;
                            }
                        }
                        else
                        {
                            // Find the parameter to remember.  If it's not present in the bag, do nothing.
                            FilledParameter filledParameterToSave;
                            if (!parameterBag.TryGetValue(parameter.Name, out filledParameterToSave))
                                return;

                            if (!savedParametersPerOperation.TryGetValue(parameter.Operation.Name, out savedParameterBag))
                            {
                                savedParameterBag = new ParameterBag();
                                savedParametersPerOperation.Add(parameter.Operation.Name, savedParameterBag);
                            }
                            savedParameterBag[parameter.Name] = filledParameterToSave;
                        }
                    }
                    break;
                case ParameterLifetime.SessionForAllOperations:
                    {
                        // Find the parameter to remember.  If it's not present in the bag, do nothing.
                        FilledParameter filledParameterToSave;
                        if (!parameterBag.TryGetValue(parameter.Name, out filledParameterToSave))
                            return;

                        ParameterBag savedParameterBag = host.SessionParametersAcrossOperations;
                        savedParameterBag[parameter.Name] = filledParameterToSave;
                    }
                    break;
            }
        }

        public ParameterBag ExecuteInternal(ReportStep reportStep, ParameterBag parameters, bool isRedo)
        {
            string filledReport = reportStep.Substitute(host, parameters);

            object /* Pane */ preferredPane = null;
            if (parameters.ContainsKey(STATSDIRECT_REPORT_PANE)
                && null != parameters[STATSDIRECT_REPORT_PANE])
                preferredPane = parameters[STATSDIRECT_REPORT_PANE].AsPane;
            string xml = null;
            try
            {
                bool shouldKeepData = host.Preferences.ShouldKeepData;
                xml = parameters.SerializeForRedo(shouldKeepData);
            }
            catch (Exception)
            {
                // TODO: Log what failed to be serialized so that it's possible to fix the problem.
            }
            preferredPane = host.OutputReport(filledReport, reportStep.Operation, xml, preferredPane);

            // Log the ID of the report that was actually used
            ParameterBag outputParameters = new ParameterBag();
            // outputParameters.Add(REPORT_ID_NAME, new FilledParameter(FilledParameterDirection.Input, reportId));
            if (!parameters.ContainsKey(STATSDIRECT_REPORT_PANE))
                outputParameters.Add(STATSDIRECT_REPORT_PANE, new FilledParameter(FilledParameterDirection.Input, preferredPane));
            return outputParameters;
        }

        public ParameterBag ExecuteInternal(ScriptStep step, ParameterBag parameters, bool isRedo)
        {
            IScriptEngine scriptEngine = host.GetScriptEngine(step.Language);
            string entryPoint = step.EntryPoint;
            ScriptType scriptType = null == entryPoint ? ScriptType.Function : ScriptType.MultipleMethods;
            return (ParameterBag)scriptEngine.Run(step.Language, step.Body, scriptType, host, parameters, null, entryPoint);
        }

        public ParameterBag ExecuteInternal(TestStep step, ParameterBag parms, bool isRedo)
        {
            // HACK: This is not a proper interpreter, and should be!
            bool result = (bool)Evaluate(step.Condition, parms);
            IList<Step> steps = result ? step.TrueSteps : step.FalseSteps;
            foreach (Step s in steps)
            {
                ParameterBag stepResult = Execute(s, parms, isRedo);
                if (null == stepResult)
                    return null;
                parms = stepResult;
            }
            return parms;
        }

        public object Evaluate(Expression expression, ParameterBag parameters)
        {
            if (expression.Body.StartsWith("="))
            {
                IScriptEngine scriptEngine = host.GetScriptEngine(expression.Language);
                return scriptEngine.Run(expression.Language, expression.Body.Substring(1), ScriptType.Expression, host, parameters, null, null);
            }
            int candidateInt;
            if (Int32.TryParse(expression.Body, out candidateInt))
                return candidateInt;
            double candidateDouble;
            if (double.TryParse(expression.Body, NumberStyles.Float, CultureInfo.InvariantCulture, out candidateDouble))
                return candidateDouble;
            bool candidateBoolean;
            if (bool.TryParse(expression.Body, out candidateBoolean))
                return candidateBoolean;
            DateTime candidateDateTime;
            if (DateTime.TryParse(expression.Body, out candidateDateTime))
                return candidateDateTime;
            return expression.Body;
        }

        /// <summary>
        /// for any gidx call - cdat().bins is not populated
        /// this sub calculates the bins if required e.g. by rpt_frequency
        /// </summary>
        public static ClassifierVariable gidx_bins(DoubleVariable v)
        {
            // find number of categories
            int ng = 1;
            // Space/time trade-off: never reallocate g or gin, but they're large!
            double[] g = new double[v.Length]; // There will be at most v.Length groups
            int[] gin = new int[v.Length]; // There will be at most v.Length groups
            for (int j = 0; j < v.Length; j++)
            {
                if (v.Data[j] != Constant.MISSING)
                {
                    g[0] = v.Data[j];
                    gin[0] = 1;
                    break;
                }
            }
            for (int j = 1; j < v.Length; j++)
            {
                bool newa = true;
                int mg = 0;
                if (v.Data[j] == Constant.MISSING)
                {
                    newa = false;
                }
                else
                {
                    for (int i = 0; i < ng; i++)
                    {
                        if (v.Data[j] == g[i])
                        {
                            newa = false;
                            mg = i;
                            break;
                        }
                    }
                }
                if (newa)
                {
                    g[ng] = v.Data[j];
                    gin[ng] = 1;
                    ng++;
                }
                else
                {
                    if (v.Data[j] != Constant.MISSING)
                        gin[mg]++;
                }
            }

            ClassifierVariable cv = new ClassifierVariable { Title = v.Title, Data = v.Data };
            for (int i = 0; i < ng; i++)
            {
                Group grp = new Group(g[i].ToString(), g[i]) { NBin = gin[i] };
                cv.Groups.Add(grp);
            }
            return cv;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="host"></param>
        /// <param name="validatorName"> </param>
        /// <param name="parameter"></param>
        /// <param name="filledParameters"></param>
        /// <param name="failedValidationMessage"></param>
        /// <returns>A string containing at least one validation error, or none if there are no validation errors detected.</returns>
        public static string Validate(ITemplateHost host, string validatorName, Parameter parameter, ParameterBag filledParameters, string failedValidationMessage)
        {
            // Does the operation define a custom validator with that name?  If so, use it.
            if (null != parameter.Operation.CustomValidators)
            {
                foreach (CustomValidator candidate in parameter.Operation.CustomValidators)
                {
                    if (candidate.Name.Equals(validatorName))
                    {
                        string language = candidate.Language ?? "CSharp";
                        IScriptEngine scriptEngine = host.GetScriptEngine(language);
                        object result = scriptEngine.Run(language, candidate.Script, ScriptType.Validator, host, filledParameters, parameter, null);
                        if (null == result)
                            return null;
                        return result.ToString();
                    }
                }
            }
            // If there's no custom validator with that name, use a generic if we have one.
            switch (validatorName)
            {
                case "Pooling":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        // Otherwise ensure all values are in {-1, 0, 1}
                        DataFrame dataFrame = filledParameters[parameter.Name].AsDataFrame;
                        DoubleVariable variable = dataFrame.Variables[0]as DoubleVariable;
                        foreach (double value in variable.Data)
                        {
                            if (0 != value && -1 != value && 1 != value)
                                return failedValidationMessage ?? "Pooling indicator must be 0 (not pooled), 1 (subgroup) or -1 (pooled) only";
                        }
                    }
                    return null;
                case "Square":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        // Otherwise ensure the number of values is a square number
                        DataFrame dataFrame = filledParameters[parameter.Name].AsDataFrame;
                        DoubleVariable variable = dataFrame.Variables[0]as DoubleVariable;
                        if (Math.Sqrt(variable.Length) != Math.Floor(Math.Sqrt(variable.Length)))
                        {
                            return failedValidationMessage ?? "Number of observations can not be arranged as a square (i.e. integer square root)";
                        }
                    }
                    return null;
                case "SquareBins":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        // Otherwise ensure the number of bins is the square root of the number of values
                        DataFrame dataFrame = filledParameters[parameter.Name].AsDataFrame;
                        DoubleVariable variable = dataFrame.Variables[0]as DoubleVariable;
                        ClassifierVariable cv = gidx_bins(variable);
                        if (Math.Sqrt(variable.Length) != cv.GroupCount)
                        {
                            return failedValidationMessage ?? "There should be " + Math.Sqrt(variable.Length).ToString("N0") + " classes";
                        }
                    }
                    return null;
                case "CheckForNonDummiedCategories":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        // Otherwise ensure the number of bins, if >2, is at least 12 (or they're all distinct)
                        DataFrame frame = filledParameters[parameter.Name].AsDataFrame;
                        foreach (DoubleVariable v in frame.Variables)
                        {
                            double[] data = v.Data;
                            bool skip = false;
                            // skip if all not integers
                            foreach (double t in data)
                            {
                                if (t != Math.Floor(t))
                                {
                                    skip = true;
                                    break;
                                }
                            }
                            if (!skip)
                            {
                                // get number of categories if all integers
                                int ng = 1;
                                int[] g = new int[data.Length];
                                foreach (double t in data)
                                {
                                    if (t != Constant.MISSING)
                                    {
                                        g[0] = (int)t;
                                        break;
                                    }
                                }
                                for (int j = 1; j < data.Length; j++)
                                {
                                    bool newa = true;
                                    for (int i = 0; i < ng; i++)
                                    {
                                        if (data[j] == g[i] || data[j] == Constant.MISSING)
                                        {
                                            newa = false;
                                            break;
                                        }
                                    }
                                    if (newa)
                                    {
                                        g[ng++] = (int)data[j];
                                    }
                                }
                                if (ng > 2 && ng < Math.Min(data.Length - 2, 12))
                                {
                                    bool wasCancelled;
                                    bool sortOutData = host.GetBoolean("The variable named '" + v.Title + "' seems to contain categorical data.\r\n\r\nIf you want to use categorical data containing more than two categories,\r\nthen please use the 'Data_Dummy Variables' menu item to convert this variable\r\nto dummy variables before running the regression again.\r\n\r\nDo you want to quit this regression and sort out your data?", "Regression Predictor Scan", false, 140766, out wasCancelled);
                                    if (wasCancelled || sortOutData)
                                        throw new TemplateOperationCancelledException();
                                }
                            }
                        }
                        // If we get here, we're fine
                    }
                    return null;
                case "Boolean":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        // Otherwise ensure all values are in {0, 1}
                        DataFrame dataFrame = filledParameters[parameter.Name].AsDataFrame;
                        foreach (Variable variable in dataFrame.Variables)
                        {
                            if (variable is DoubleVariable)
                            {
                                DoubleVariable doubleVariable = variable as DoubleVariable;
                                foreach (double value in doubleVariable.Data)
                                {
                                    if (0.0 != value && 1.0 != value)
                                    {
                                        return failedValidationMessage ?? "Case-control indicator must be 1 for case or 0 for control only";
                                    }
                                }
                            }
                        }
                    }
                    return null;
                case "Positive":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        // Otherwise ensure all values are > 0
                        DataFrame dataFrame = filledParameters[parameter.Name].AsDataFrame;
                        foreach (Variable variable in dataFrame.Variables)
                        {
                            if (variable is DoubleVariable)
                            {
                                DoubleVariable doubleVariable = variable as DoubleVariable;
                                foreach (double value in doubleVariable.Data)
                                {
                                    if (value <= 0)
                                    {
                                        return failedValidationMessage ?? "Data must be positive non-zero numbers";
                                    }
                                }
                            }
                        }
                    }
                    return null;
                case "PositiveRows":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        DataFrame dataFrame = filledParameters[parameter.Name].AsDataFrame;
                        foreach (Variable variable in dataFrame.Variables)
                            if (!(variable is DoubleVariable))
                                return "Data must be numeric";

                        // Otherwise ensure the sum of all values across a row is > 0
                        for (int row = 0; row < dataFrame.MinRows; row++)
                        {
                            double sum = 0;
                            foreach (Variable variable in dataFrame.Variables)
                            {
                                double x = (variable as DoubleVariable).Data[row];
                                if (!(x == Constant.MISSING || double.IsInfinity(x)))
                                    sum += x;
                            }
                            if (sum <= 0)
                                return failedValidationMessage ?? "Invalid data: row " + (row + 1).ToString() + " total is not greater than zero, which it must be for this calculation";
                        }
                    }
                    return null;
                case "PositiveRowsExceptLastColumn":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        DataFrame dataFrame = filledParameters[parameter.Name].AsDataFrame;
                        foreach (Variable variable in dataFrame.Variables)
                            if (!(variable is DoubleVariable))
                                return "Data must be numeric";

                        // Otherwise ensure the sum of all values across a row except the last column is > 0
                        for (int row = 0; row < dataFrame.MinRows; row++)
                        {
                            double sum = 0;
                            for (int col = 0; col < dataFrame.Variables.Count - 1; col++)
                            {
                                Variable variable = dataFrame.Variables[col];
                                double x = (variable as DoubleVariable).Data[row];
                                if (!(x == Constant.MISSING || double.IsInfinity(x)))
                                    sum += x;
                            }
                            if (sum <= 0)
                                return failedValidationMessage ?? "Invalid data: row " + (row + 1).ToString() + " total is not greater than zero, which it must be for this calculation";
                        }
                    }
                    return null;
                case "NonNegative":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        // Otherwise ensure all values are >= 0
                        DataFrame dataFrame = filledParameters[parameter.Name].AsDataFrame;
                        foreach (Variable variable in dataFrame.Variables)
                        {
                            if (variable is DoubleVariable)
                            {
                                DoubleVariable doubleVariable = variable as DoubleVariable;
                                foreach (double value in doubleVariable.Data)
                                {
                                    if (value < 0)
                                    {
                                        return failedValidationMessage ?? "Data must be positive or zero numbers";
                                    }
                                }
                            }
                        }
                    }
                    return null;
                case "ZeroToOneExclusive":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        // Otherwise ensure all values are in the range (0, 1)
                        DataFrame dataFrame = filledParameters[parameter.Name].AsDataFrame;
                        foreach (Variable variable in dataFrame.Variables)
                        {
                            if (variable is DoubleVariable)
                            {
                                DoubleVariable doubleVariable = variable as DoubleVariable;
                                foreach (double value in doubleVariable.Data)
                                {
                                    if (value <= 0 || value >= 1)
                                    {
                                        return failedValidationMessage ?? "Data must lie between 0 and 1";
                                    }
                                }
                            }
                        }
                    }
                    return null;
                case "ZeroToOneInclusive":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        // Otherwise ensure all values are in the range [0, 1]
                        DataFrame dataFrame = filledParameters[parameter.Name].AsDataFrame;
                        foreach (Variable variable in dataFrame.Variables)
                        {
                            if (variable is DoubleVariable)
                            {
                                DoubleVariable doubleVariable = variable as DoubleVariable;
                                foreach (double value in doubleVariable.Data)
                                    if (value < 0 || value > 1)
                                        return failedValidationMessage ?? "Data must lie between 0 and 1 inclusive";
                            }
                        }
                    }
                    return null;
                case "TwoBinsAndNoMissingData":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        // Otherwise ensure there are exactly two bins in the classifier variable
                        DataFrame dataFrame = filledParameters[parameter.Name].AsDataFrame;
                        ClassifierVariable variable = dataFrame.Variables[0] as ClassifierVariable;
                        if (variable.GroupCount != 2)
                        {
                            return failedValidationMessage ?? "Group identifier must contain two groups and no missing data";
                        }
                    }
                    return null;
                case "NoMissingData":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        // Otherwise ensure there's no missing data in any of the numeric variables in the frame
                        DataFrame dataFrame = filledParameters[parameter.Name].AsDataFrame;
                        foreach (Variable variable in dataFrame.Variables)
                        {
                            if (variable is DoubleVariable)
                            {
                                DoubleVariable doubleVariable = variable as DoubleVariable;
                                foreach (double value in doubleVariable.Data)
                                {
                                    if (value == Constant.MISSING)
                                    {
                                        return failedValidationMessage ?? "Data with missing values cannot be used here";
                                    }
                                }
                            }
                        }
                    }
                    return null;
                case "PersonTimeSize":
                    {
                        // Assumes no missing data, no data < 0
                        DataFrame datFrame = filledParameters["data"].AsDataFrame;
                        DoubleVariable datV0 = datFrame.Variables[0]as DoubleVariable;
                        DoubleVariable datV1 = datFrame.Variables[1]as DoubleVariable;
                        DoubleVariable datV2 = datFrame.Variables[2]as DoubleVariable;
                        int rows = datFrame.MaxRows;

                        double refntot = 0.0;
                        for (int j = 1; j <= rows; j++)
                        {
                            double xy = datV0.Data[j - 1];
                            double xn = datV1.Data[j - 1];
                            double rf = datV2.Data[j - 1];
                            refntot += rf;
                            if (xn <= 0)
                                return "Person-time must be greater than zero";
                            if (xy > xn)
                                return "Number of events must be greater then person-time, do not scale person-time";
                        }

                        if (refntot <= 0.0)
                            return "Total reference group size must be greater than zero";
                    }
                    return null;
                default:
                    throw new ArgumentOutOfRangeException("validatorName", validatorName, "No validator with the specified name");
            }
        }

        public int NextOriginGroup()
        {
            return takeAnOriginGroup.Next();
        }
    }
}
