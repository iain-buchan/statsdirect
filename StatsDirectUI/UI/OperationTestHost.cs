using System;
using System.Collections.Generic;
using System.Globalization;
using StatsDirect.Data;
using StatsDirect.Templates;

namespace StatsDirect.UI
{
    internal class OperationTestHost : ITemplateHost
    {
        private Dictionary<string, OperationTestInputParameter> InputParameters { get; }

        public OperationTestHost(IList<OperationTestInputParameter> inputs)
        {
            InputParameters = new Dictionary<string, OperationTestInputParameter>();
            foreach (OperationTestInputParameter input in inputs)
                InputParameters.Add(input.Name, input);
        }

        bool ITemplateHost.MetaPlotCI => throw new NotImplementedException();

        int ITemplateHost.MetaPlotMethod => throw new NotImplementedException();

        IDictionary<string, ParameterBag> ITemplateHost.SessionParametersPerOperation => throw new NotImplementedException();

        ParameterBag ITemplateHost.SessionParametersAcrossOperations => throw new NotImplementedException();

        SDPreferences ITemplateHost.Preferences => throw new NotImplementedException();

        Operation ITemplateHost.Operation { get; set; }

        IDictionary<string, object> ITemplateHost.Session => throw new NotImplementedException();

        ParameterBag ITemplateHost.Amend(IFillable options, ParameterBag context)
        {
            // No amendment during tests
            return context;
        }

        bool ITemplateHost.CanCombine(Parameter parameter)
        {
            return false; // We always force the use of single parameters to make the host's life easy
        }

        void ITemplateHost.Error(string Message, string Caption)
        {
            throw new NotImplementedException();
        }

        ParameterBag ITemplateHost.FillAndValidateCombinedParameters(ITemplateProcessor processor, ParameterBag context)
        {
            // Should never be called
            throw new NotImplementedException();
        }

        ParameterBag ITemplateHost.FillParameter(ITemplateProcessor processor, Parameter Parameter, ParameterBag context, bool shouldCombine)
        {
            // Do nothing; we already have all the inputs we're going to get
            return new ParameterBag();
        }

        void ITemplateHost.FinishProgress()
        {
            // No UI during a test
        }

        bool ITemplateHost.GetBoolean(string prompt, string Title, bool initialValue, out bool cancelled)
        {
            throw new NotImplementedException();
        }

        bool ITemplateHost.GetBoolean(string prompt, string Title, bool InitialValue, int HelpIndex, out bool cancelled)
        {
            throw new NotImplementedException();
        }

        double ITemplateHost.GetDouble(string prompt, string Title, double InitialValue, out bool cancelled)
        {
            throw new NotImplementedException();
        }

        int ITemplateHost.GetInteger(string prompt, string Title, int initialValue, out bool cancelled)
        {
            throw new NotImplementedException();
        }

        IScriptEngine ITemplateHost.GetScriptEngine(string language)
        {
            throw new NotImplementedException();
        }

        string ITemplateHost.GetString(string prompt, string title, string initialValue)
        {
            throw new NotImplementedException();
        }

        void ITemplateHost.NoteError(Exception ex)
        {
            throw ex;
        }

        void ITemplateHost.OutputFrame(DataFrame frame, bool keepSelection, bool isFormulae, string missingIndicator, PaneAndPosition preferredOutputLocation, RelativePosition defaultPosition)
        {
            throw new NotImplementedException();
        }

        object ITemplateHost.OutputReport(IRenderable renderable, Operation operation, string redoInformation, object preferredOutputLocation)
        {
            // No UI during a test
            return null;
        }

        void ITemplateHost.PrepareParameter(ITemplateProcessor processor, Parameter parameter, ParameterBag context)
        {
            if (!InputParameters.TryGetValue(parameter.Name, out OperationTestInputParameter input))
                throw new NotImplementedException($"Operation {parameter.Operation.Name} expects parameter {parameter.Name} which was not specified in the test inputs");
            context.AddInput(parameter.Name, new InputParameterFiller(input).Fill(parameter));
        }

        string ITemplateHost.pval(double p)
        {
            throw new NotImplementedException();
        }

        string ITemplateHost.pval_half(double p)
        {
            throw new NotImplementedException();
        }

        bool ITemplateHost.Query(string Message, string Caption)
        {
            throw new NotImplementedException();
        }

        string ITemplateHost.RoundU(double amount)
        {
            throw new NotImplementedException();
        }

        void ITemplateHost.ShowHelp(int helpContextId)
        {
            // No UI during a test
        }

        void ITemplateHost.StartProgress(string operationDescription, bool provideProgress)
        {
            // No UI during a test
        }

        bool ITemplateHost.UpdateProgress(double fractionComplete)
        {
            // No UI during a test
            return true;
        }

        void ITemplateHost.Warning(string Message, string Caption)
        {
            // No UI during a test
        }

        private class InputParameterFiller : IParameterVisitor
        {
            private OperationTestInputParameter Input { get; }

            object parsedInput;

            public InputParameterFiller(OperationTestInputParameter input)
            {
                this.Input = input;
            }
            void IParameterVisitor.Visit(BooleanParameter parameter)
            {
                throw new NotImplementedException();
            }

            void IParameterVisitor.Visit(ChartOptionsParameter parameter)
            {
                throw new NotImplementedException();
            }

            void IParameterVisitor.Visit(ConfidenceIntervalParameter parameter)
            {
                parsedInput = double.Parse(Input.Value, CultureInfo.InvariantCulture);
            }

            void IParameterVisitor.Visit(DateParameter parameter)
            {
                throw new NotImplementedException();
            }

            void IParameterVisitor.Visit(Double2By2Parameter parameter)
            {
                throw new NotImplementedException();
            }

            void IParameterVisitor.Visit(Double2By2ByKParameter parameter)
            {
                throw new NotImplementedException();
            }

            void IParameterVisitor.Visit(DoubleParameter parameter)
            {
                parsedInput = double.Parse(Input.Value, CultureInfo.InvariantCulture);
            }

            void IParameterVisitor.Visit(EditGridParameter parameter)
            {
                throw new NotImplementedException();
            }

            void IParameterVisitor.Visit(FillableParameter parameter)
            {
                throw new NotImplementedException();
            }

            void IParameterVisitor.Visit(Frame2DParameter parameter)
            {
                throw new NotImplementedException();
            }

            void IParameterVisitor.Visit(FrameParameter parameter)
            {
                throw new NotImplementedException();
            }

            void IParameterVisitor.Visit(GroupedCovarianceParameter parameter)
            {
                throw new NotImplementedException();
            }

            void IParameterVisitor.Visit(IntegerParameter parameter)
            {
                parsedInput = int.Parse(Input.Value, CultureInfo.InvariantCulture);
            }

            void IParameterVisitor.Visit(OptionParameter parameter)
            {
                throw new NotImplementedException();
            }

            void IParameterVisitor.Visit(OptionsParameter parameter)
            {
                throw new NotImplementedException();
            }

            void IParameterVisitor.Visit(PickFromListParameter parameter)
            {
                throw new NotImplementedException();
            }

            void IParameterVisitor.Visit(PickVariablesParameter parameter)
            {
                throw new NotImplementedException();
            }

            void IParameterVisitor.Visit(SpecialParameter parameter)
            {
                throw new NotImplementedException();
            }

            void IParameterVisitor.Visit(StringParameter parameter)
            {
                parsedInput = Input.Value;
            }

            internal object Fill(Parameter parameter)
            {
                parameter.Accept(this);
                return parsedInput;
            }
        }
    }
}