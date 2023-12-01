using System;
using System.Collections.Generic;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.UI;
using StatsDirect.Utilities;
using System.Globalization;
using StatsDirect.Templates;

namespace StatsDirect.TemplateProcessing
{
    /// <summary>
    /// An interface-agnostic template operation processor.
    /// </summary>
    public sealed class TemplateProcessor : ITemplateProcessor
    {
        private const string STATSDIRECT_CHART_OPTIONS = "statsdirect-chart-options";
        private const string STATSDIRECT_CHART_SCALE_PARAMETERS = "statsdirect-chart-scale-parameters";
        private const string STATSDIRECT_FRAME_PANE = "statsdirect-frame-pane";
        private const string STATSDIRECT_REPORT_PANE = "statsdirect-report-pane";
        private static readonly TakeANumber takeAnOriginGroup = new();

        private IChartPreferences ChartPreferences { get; }
        private ISdPreferences SdPreferences { get; }
        private ISession Session { get; }
        private ITemplateHost TemplateHost { get; }

        public TemplateProcessor(IChartPreferences chartPreferences, ISdPreferences sdPreferences, ISession session, ITemplateHost templateHost)
        {
            ChartPreferences = chartPreferences;
            SdPreferences = sdPreferences;
            Session = session;
            TemplateHost = templateHost;
        }

        /// <summary>
        /// Run the operation to completion or error.
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="startingParameters">If non-null, some parameters to be used as defaults.</param>
        /// <param name="isRedo"> </param>
        StepOutput? ITemplateProcessor.Execute(Operation operation, ParameterBag startingParameters, bool isRedo)
        {
            TemplateHost.Operation = operation;
            ParameterBag filledParameters = startingParameters ?? new ParameterBag();

            // Check preconditions; fail if any fail.
            foreach (Precondition precondition in operation.Preconditions)
            {
                if (!precondition.Check(this, filledParameters))
                {
                    TemplateHost.Error(precondition.FailureMessage, "Cannot run operation");
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
                    StepOutput stepOutput = Execute(step, filledParameters, isRedo);
                    filledParameters = stepOutput.ParameterBag;
                }
                catch (TemplateOperationCancelledException ex)
                {
                    if (ex.ShouldShowError)
                        TemplateHost.Error(ex.Message, ex.Caption);
                    // The user cancelled the operation
                    return null;
                }
            }
            TemplateHost.Operation = null;
#if RENDER_TESTS
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            using (System.IO.TextWriter sw = new System.IO.StringWriter(sb))
                new System.Xml.Serialization.XmlSerializer(typeof(OperationTest), new System.Xml.Serialization.XmlRootAttribute("test")).Serialize(sw, OperationTestRenderer.Render(filledParameters));
            Debug.Print(sb.ToString());
#endif
            return new StepOutput(filledParameters);
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
                TemplateHost.PrepareParameter(parameter, parameters);
        }

        /// <summary>
        /// Execute the operation with the passed-in parameters, returning some results that can be used for the next operation.
        /// Implementers <strong>must</strong> ensure that a new dictionary is used for the output.
        /// </summary>
        /// <param name="step"></param>
        /// <param name="parameters"></param>
        /// <param name="isRedo"></param>
        /// <returns></returns>
        private StepOutput Execute(Step step, ParameterBag parameters, bool isRedo)
        {
            if (null == parameters)
                return StepOutput.Empty();
            
            StepOutput result = step.ExecuteInternal(this, parameters, isRedo);
            // If required, transfer input parameters where the same name is not already present in the results.
            if (null != result.ParameterBag)
            {
                if (step.ShouldCopyInputParameters)
                {
                    foreach (KeyValuePair<string, FilledParameter?> inputParameter in parameters.Pairs)
                        if (!result.ParameterBag.ContainsKey(inputParameter.Key))
                            result.ParameterBag.Add(inputParameter.Key, inputParameter.Value);
                }
                // Remove explicit blanks now that they have prevented copying.
                List<string> keysToRemove = new();
                foreach (KeyValuePair<string, FilledParameter?> pair in result.ParameterBag.Pairs)
                    if (null == pair.Value)
                        keysToRemove.Add(pair.Key);
                foreach (string keyToRemove in keysToRemove)
                    result.ParameterBag.Remove(keyToRemove);
            }
            return result;
        }

        public StepOutput ExecuteInternal(BuiltinStep step, ParameterBag parameters, bool isRedo)
        {
            IBuiltin builtin = BuiltinRegistry.SoleInstance.Builtin(step.FunctionName);
            if (builtin is null)
                throw new Exception($"No function '{step.FunctionName}' is supplied by the host.");
            return builtin.Invoke(TemplateHost, parameters);
        }

        ChartDefinition CreateChartDefinition(ChartStep step, ParameterBag parameters, bool isRedo, string? dataName, IReadOnlyList<ISeries>? xSeries, IReadOnlyList<ISeries>? ySeries, string? xAxisTitle, string? yAxisTitle)
        {
            ChartDefinition definition = FindOrPreprocessChartDefinition(step, parameters, isRedo, xSeries, ySeries, dataName, xAxisTitle, yAxisTitle);
            definition.ScaleParameters = MaybeFindScaleParameters(step, parameters, isRedo);

            // TODO: Gross hack (see #993): Forest lin/log depends on the chart options for the data.
            if (step.ChartType == ChartType.Forest)
            {
                // #987: If the summary statistic variable ("odds") contains the word "ratio" then select log plot by default, else linear plot
                definition.ScaleParameters.X.ScaleType = definition.ChartOptions.XAxisTitle.Contains("ratio") || definition.ChartOptions.XAxisTitle.Contains("Ratio")
                    ? ScaleType.Log10
                    : ScaleType.Linear;
            }

            if (step.RequestUserInput)
            {
                if (null == TemplateHost.Amend(definition, parameters))
                    throw new TemplateOperationCancelledException();
            }
            new ChartDefinitionProcessor(ChartPreferences, SdPreferences).PostProcessFilledChartOptions(definition);
            return definition;
        }

        private ChartDefinition FindOrPreprocessChartDefinition(ChartStep step, ParameterBag parameters, bool isRedo, IReadOnlyList<ISeries> xSeries, IReadOnlyList<ISeries> ySeries, string? dataName, string? xAxisTitle, string? yAxisTitle)
        {
            // If we're redoing a previous operation, we should in theory have the previous ChartOptions.  Go look!
            if (isRedo)
            {
                string possibleParameterName = STATSDIRECT_CHART_OPTIONS + (step.ChartName ?? string.Empty);
                if (parameters.TryGetValue(possibleParameterName, out FilledParameter? fp))
                {
                    if (null != fp && fp.HasData)
                        return new ChartDefinition(step.ChartType, fp.AsChartOptions, xSeries, ySeries);
                }
            }

            // If we're not redoing, or we can't find the options, then we need to fill them in now.
            return new ChartDefinitionProcessor(ChartPreferences, SdPreferences).Preprocess(step, parameters, xSeries, ySeries, dataName);
        }

        private static ScaleParameters? MaybeFindScaleParameters(ChartStep step, ParameterBag parameters, bool isRedo)
        {
            // If we're redoing a previous operation, we should in theory have the previous ScaleParameters.  Go look!
            if (isRedo)
            {
                string possibleParameterName = STATSDIRECT_CHART_SCALE_PARAMETERS + (step.ChartName ?? string.Empty);
                if (parameters.TryGetValue(possibleParameterName, out FilledParameter? fp))
                {
                    if (null != fp && fp.HasData)
                        return fp.AsScaleParameters;
                }
            }

            // If we're not redoing, or we can't find the options, then leave blank and they'll be filled later.
            return null;
        }

        public StepOutput ExecuteInternal(ChartStep step, ParameterBag parameters, bool isRedo)
        {
            // Series: First X...
            string? dataName = null;
            ISeries[]? xSeries = null;
            if (null != step.XSeriesDataName)
            {
                if (!parameters.ContainsKey(step.XSeriesDataName))
                    throw new Exception("Chart expected parameter \"" + step.XSeriesDataName + "\", which was not supplied");
                DataFrame frame = parameters[step.XSeriesDataName].AsDataFrame;
                xSeries = new ISeries[frame.VariableCount];
                for (int v = 0; v < frame.VariableCount; v++)
                {
                    DoubleVariable variable = frame.Variables[v] as DoubleVariable;
                    xSeries[v] = ChartDefinitionProcessor.VariableToSeries(variable);
                }
                dataName = frame.Name;
            }
            // ... then Y
            ISeries[]? ySeries = null;
            if (null != step.YSeriesDataName)
            {
                if (!parameters.ContainsKey(step.YSeriesDataName))
                    throw new Exception("Chart expected parameter \"" + step.YSeriesDataName + "\", which was not supplied");
                DataFrame frame = parameters[step.YSeriesDataName].AsDataFrame;
                ySeries = new ISeries[frame.VariableCount];
                for (int v = 0; v < frame.VariableCount; v++)
                {
                    DoubleVariable variable = frame.Variables[v] as DoubleVariable;
                    ySeries[v] = ChartDefinitionProcessor.VariableToSeries(variable);
                }
                dataName = frame.Name;
            }

            string? xAxisTitle = step.XAxisTitle(this, parameters);
            string? yAxisTitle = step.YAxisTitle(this, parameters);
            if (string.IsNullOrWhiteSpace(xAxisTitle))
                xAxisTitle = null;
            if (string.IsNullOrWhiteSpace(yAxisTitle))
                yAxisTitle = null;
            ChartDefinition definition = CreateChartDefinition(step, parameters, isRedo, dataName, xSeries, ySeries, xAxisTitle, yAxisTitle);

            // Run a plot in case it needs to return some results - TODO: This requires plotting twice, which feels like a potential mess.
            ParameterBag results = new ChartRendererFactory(SdPreferences, TemplateHost).PlotForResultsOnly(definition);
            results.AddOutput(step.ChartName, definition);
            SaveChartDefinition(step, results, definition);
            return new StepOutput(results);
        }

        private static void SaveChartDefinition(ChartStep step, ParameterBag results, ChartDefinition definition)
        {
            results.AddInput(STATSDIRECT_CHART_OPTIONS + (step.ChartName ?? string.Empty), definition.ChartOptions);
            results.AddInput(STATSDIRECT_CHART_SCALE_PARAMETERS + (step.ChartName ?? string.Empty), definition.ScaleParameters);
        }

        public StepOutput ExecuteInternal(IterationStep step, ParameterBag parms, bool isRedo)
        {
            // Detect bounds: default 0 to 0 inclusive (1 iteration), then add any fixed values, then any variables if found.
            int lower = 0;
            int upper = 0;
            if (step.LowerBound.HasValue)
                lower = step.LowerBound.Value;
            if (step.UpperBound.HasValue)
                upper = step.UpperBound.Value;
            if (null != step.LowerBoundParameterName)
                if (parms.ContainsKey(step.LowerBoundParameterName))
                    lower = parms[step.LowerBoundParameterName].AsInt32;
            if (null != step.UpperBoundParameterName)
                if (parms.ContainsKey(step.UpperBoundParameterName))
                    upper = parms[step.UpperBoundParameterName].AsInt32;
            ParameterBag filledParameters = new();
            for (int i = lower; i <= upper; i++)
            {
                if (null != step.LoopVariableName)
                    filledParameters[step.LoopVariableName] = FilledParameterFactory.Output(i);
                foreach (Step s in step.Steps)
                {
                    // TODO: How to handle execution failures?
                    StepOutput result = s.ExecuteInternal(this, filledParameters, isRedo);
                    // Add in any required parameters, combining everything into one big mass of outputs.
                    // Overwrite earlier loop results with later ones.
                    foreach (string k in result.ParameterBag.Keys)
                        filledParameters[k] = result.ParameterBag[k];
                }
            }
            return new StepOutput(filledParameters);
        }

        public StepOutput ExecuteInternal(OutputFrameStep step, ParameterBag parameters, bool isRedo)
        {
            DataFrame frame = parameters[step.ParameterName].AsDataFrame;
            if (null != frame)
            {
                PaneAndPosition? preferredPaneAndPosition = null;
                if (parameters.ContainsKey(STATSDIRECT_FRAME_PANE)
                    && null != parameters[STATSDIRECT_FRAME_PANE])
                    preferredPaneAndPosition = parameters[STATSDIRECT_FRAME_PANE].AsPaneAndPosition;
                TemplateHost.OutputFrame(frame, step.KeepSelection, step.IsFormulae, step.MissingIndicator, preferredPaneAndPosition, step.DefaultPlacement);
            }
            return StepOutput.Empty();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="step"></param>
        /// <param name="parms"></param>
        /// <param name="isRedo"></param>
        /// <returns></returns>
        /// <remarks>Note that this may return parameters with null values; it is up to the caller to remove these.</remarks>
        public StepOutput ExecuteInternal(ParametersStep step, ParameterBag parms, bool isRedo)
        {
            // If we're redoing a previous operation, then all parameters are taken from the previous operation.  We do not request any.
            if (isRedo)
                return StepOutput.Empty();

            try
            {
                ParameterBag filledParameters = new();
                List<Parameter> outstandingParameters = new();
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
                    bool shouldCombine = TemplateHost.CanCombine(parameter);
                    if (!shouldCombine)
                    {
                        // We've hit a parameter we should not or cannot combine.  Ensure any grouped parameters are handled at this point.
                        // We also know that we definitely cannot request an output window at this point - so don't!
                        if (outstandingParameters.Count > 0)
                        {
                            // Fill in and validate previous parameters
                            ParameterBag outstandingFilledParameters = TemplateHost.FillAndValidateCombinedParameters(parmsAndFilledParameters);
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
                    ParameterBag newFilledParameters = TemplateHost.FillParameter(parameter, parmsAndFilledParameters, shouldCombine);
                    if (shouldCombine)
                    {
                        outstandingParameters.Add(parameter);

                        // If the parameter has (a) default value(s), add any that aren't already known (and hence overridden) to the context.
                        // This is critical when first displaying e.g. a set of controls where one isn't displayed initially due to a default value in another.
                        // Bug #1244
                        ParameterBag oldAndNewFilledParameters = CombinePreferringLater(parmsAndFilledParameters, newFilledParameters);
                        ParameterBag defaults = parameter.AllDefaults(this, oldAndNewFilledParameters);
                        foreach (KeyValuePair<string, FilledParameter?> fp in defaults.Pairs)
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
                            foreach (KeyValuePair<string, FilledParameter?> pair in newFilledParameters.Pairs)
                                filledParameters.Add(pair.Key, pair.Value);
                    }
                }

                // We've reached the end of the list.  Ensure any grouped parameters are handled at this point.
                // We might also be able to request output frame or report parameters now, if the operation doesn't do anything else.
                if (outstandingParameters.Count > 0)
                {
                    // Union parms with filledParameters before we pass in, so that this has access to earlier parameters in the same series
                    ParameterBag parmsAndFilledParameters = CombinePreferringLater(parms, filledParameters);

                    if (step.Operation.ShouldRequestTargetAfter(step, typeof(OutputFrameStep), out Step frameStep) == HasInput.NoAndTypeFound)
                    {
                        RelativePosition rp = null == frameStep ? RelativePosition.AfterSelection : ((OutputFrameStep)frameStep).DefaultPlacement;
                        string missingIndicator = null == frameStep ? Formatting.ASTERISK : ((OutputFrameStep)frameStep).MissingIndicator;
                        SpecialParameter frameParameter = new() { Name = STATSDIRECT_FRAME_PANE, SpecialType = "frame", ExtraData = new object[] { rp, missingIndicator } };
                        TemplateHost.FillParameter(frameParameter, parmsAndFilledParameters, true);
                    }

                    // Handle previous parameter fill-in, validation and combination
                    ParameterBag outstandingFilledParameters = TemplateHost.FillAndValidateCombinedParameters(parmsAndFilledParameters);
                    if (null == outstandingFilledParameters)
                        throw new TemplateOperationCancelledException();
                    foreach (Parameter outstandingParameter in outstandingParameters)
                        MaybeRemember(outstandingParameter, outstandingFilledParameters);
                    foreach (KeyValuePair<string, FilledParameter> pair in outstandingFilledParameters.Pairs)
                        filledParameters[pair.Key] = pair.Value;
                    outstandingParameters.Clear();
                }
                return new StepOutput(filledParameters);
            }
            catch (TemplateExecutionHandlesMeSpeciallyException)
            {
                // We don't ever want this caught by the general exception catcher below, so we make a special case.
                throw;
            }
#if !WATCH_EXCEPTIONS
            catch (Exception ex)
            {
                // Fail the operation
                TemplateHost.Error("Internal error: " + ex.Message, "Operation terminated");
                throw new Utilities.TemplateOperationCancelledException();
            }
#endif
        }

        /// <summary>
        /// Combine bag1 and bag2 into a new bag (returned).  Where bag1 and bag2 contain the same parameter, prefer the one from bag2 unless it is a default parameter (in which case prefer bag1).
        /// </summary>
        private static ParameterBag CombinePreferringLater(ParameterBag? bag1, ParameterBag? bag2)
        {
            ParameterBag combinedParameters = new();
            if (null != bag2)
                foreach (KeyValuePair<string, FilledParameter> pair in bag2.Pairs)
                    combinedParameters.Add(pair);

            // Add/overwrite with older parameters if (a) there is no matching newer parameter or (b) the newer parameter is a default, in which case we want the real value.
            if (null != bag1)
            {
                foreach (KeyValuePair<string, FilledParameter> pair in bag1.Pairs)
                    if (!combinedParameters.ContainsKey(pair.Key) || combinedParameters[pair.Key].Direction == FilledParameterDirection.Default)
                        combinedParameters[pair.Key] = pair.Value;
            }

            return combinedParameters;
        }

        private void TryToRecallParameterForAllOperations(ParameterBag filledParameters, Parameter parameter)
        {
            // Check the parameter isn't already known to us.  Only known input parameters should be checked here; if it's a default parameter we still choose to recall and overwrite it.
            if (filledParameters.ContainsKey(parameter.Name) && filledParameters[parameter.Name].IsInputParameter)
                return;

            TryToRecallSavedParameterFromBag(Session.SessionParametersAcrossOperations, filledParameters, parameter.Name);
        }

        private void TryToRecallParameterForThisOperation(ParameterBag filledParameters, Parameter parameter)
        {
            // Check the parameter isn't already known to us.  Only known input parameters should be checked here; if it's a default parameter we still choose to recall and overwrite it.
            if (filledParameters.ContainsKey(parameter.Name) && filledParameters[parameter.Name].IsInputParameter)
                return;

            IDictionary<string, ParameterBag> savedParametersPerOperation = Session.SessionParametersPerOperation;
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

        private static void TryToRecallSavedParameterFromBag(ParameterBag savedParameters, ParameterBag filledParameters, string name)
        {
            if (savedParameters.TryGetValue(name, out FilledParameter? savedParameter))
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
        private void MaybeRemember(Parameter parameter, ParameterBag? parameterBag)
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
                        IDictionary<string, ParameterBag> savedParametersPerOperation = Session.SessionParametersPerOperation;
                        ParameterBag? savedParameterBag;
                        if (parameter is OptionsParameter)
                        {
                            foreach (OptionsOption opt in ((OptionsParameter)parameter).Options)
                            {
                                // Find the parameter to remember.  If it's not present in the bag, do nothing.
                                if (!parameterBag.TryGetValue(opt.Name, out FilledParameter? filledParameterToSave))
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
                            if (!parameterBag.TryGetValue(parameter.Name, out FilledParameter? filledParameterToSave))
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
                        if (!parameterBag.TryGetValue(parameter.Name, out FilledParameter? filledParameterToSave))
                            return;

                        ParameterBag savedParameterBag = Session.SessionParametersAcrossOperations;
                        savedParameterBag[parameter.Name] = filledParameterToSave;
                    }
                    break;
            }
        }

        public StepOutput ExecuteInternal(ReportStep reportStep, ParameterBag parameters, bool isRedo)
        {
            ReportTemplateAndParameters filledTemplate = new(new ReportTemplate(reportStep.GetContent(), reportStep.MimeType), parameters);

            object? /* Pane */ preferredPane = null;
            if (parameters.ContainsKey(STATSDIRECT_REPORT_PANE)
                && parameters[STATSDIRECT_REPORT_PANE] is not null)
                preferredPane = parameters[STATSDIRECT_REPORT_PANE].AsPane;
            preferredPane = TemplateHost.OutputReport(filledTemplate, reportStep.Operation, preferredPane);

            // Log the ID of the report that was actually used
            ParameterBag outputParameters = new();
            if (!parameters.ContainsKey(STATSDIRECT_REPORT_PANE))
                outputParameters.AddInput(STATSDIRECT_REPORT_PANE, preferredPane);
            return new StepOutput(outputParameters);
        }

        public StepOutput ExecuteInternal(ScriptStep step, ParameterBag parameters, bool isRedo)
        {
            if (!TemplateHost.TryGetScriptEngine(step.Language, out IScriptEngine? scriptEngine))
                ;
            string entryPoint = step.EntryPoint;
            ScriptType scriptType = entryPoint is null
                ? ScriptType.Function
                : ScriptType.MultipleMethods;
            return new StepOutput((ParameterBag)scriptEngine.Run(step.Language, step.Body, scriptType, TemplateHost, parameters, null, entryPoint));
        }

        public StepOutput ExecuteInternal(TestStep step, ParameterBag parms, bool isRedo)
        {
            // HACK: This is not a proper interpreter, and should be!
            bool result = (bool)Evaluate(step.Condition, parms);
            IList<Step> steps = result ? step.TrueSteps : step.FalseSteps;
            foreach (Step s in steps)
            {
                StepOutput stepResult = Execute(s, parms, isRedo);
                if (stepResult.ParameterBag is null)
                    return stepResult;
                parms = stepResult.ParameterBag;
            }
            return new StepOutput(parms);
        }

        public object Evaluate(Expression expression, ParameterBag parameters)
        {
            if (expression.Body.StartsWith("="))
            {
                if (!TemplateHost.TryGetScriptEngine(expression.Language, out IScriptEngine? scriptEngine))
                    ;
                return scriptEngine.Run(expression.Language, expression.Body.Substring(1), ScriptType.Expression, TemplateHost, parameters, null, null);
            }
            if (int.TryParse(expression.Body, out int candidateInt))
                return candidateInt;
            if (double.TryParse(expression.Body, NumberStyles.Float, CultureInfo.InvariantCulture, out double candidateDouble))
                return candidateDouble;
            if (bool.TryParse(expression.Body, out bool candidateBoolean))
                return candidateBoolean;
            if (DateTime.TryParse(expression.Body, out DateTime candidateDateTime))
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

            ClassifierVariable cv = new() { Title = v.Title, Data = v.Data };
            for (int i = 0; i < ng; i++)
                cv.Groups.Add(new Group(g[i].ToString(), g[i]) { NBin = gin[i] });
            return cv;
        }

        public int NextOriginGroup() => takeAnOriginGroup.Next();
    }
}
