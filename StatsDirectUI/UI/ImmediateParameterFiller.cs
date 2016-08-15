using StatsDirect.Data;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    internal class ImmediateParameterFiller : IParameterVisitor
    {
        public ParameterBag context { get; set; }
        public ParameterBag outputParameters { get; private set; }
        public ITemplateProcessor processor { get; set; }

        public void Visit(ConfidenceIntervalParameter parameter)
        {
            // Can we get away without asking?
            if (parameter.CanDefault && SdApplication.SoleInstance.Preferences.CanDefaultConfidenceInterval)
            {
                outputParameters = new ParameterBag(parameter.Name, new FilledParameter(FilledParameterDirection.Input, SdApplication.SoleInstance.Preferences.DefaultConfidenceInterval));
                return;
            }

            // Prompt - keep going until the user cancels or gives a valid entry
            string prompt = parameter.Prompt(processor, context);
            if (string.IsNullOrEmpty(prompt))
                prompt = "Enter confidence interval (%, in the range [0, 100])";

            double result = 0.0;
            if (SdApplication.SoleInstance.Preferences.CanDefaultConfidenceInterval)
                result = SdApplication.SoleInstance.Preferences.DefaultConfidenceInterval;
            double? defaultValue = parameter.DefaultValue(processor, context);
            if (defaultValue.HasValue && defaultValue.Value > 0.0)
                result = defaultValue.Value;

            while (true)
            {
                string response = SdApplication.SoleInstance.GetString(prompt, "StatsDirect", (result * 100.0).ToString());
                if (string.IsNullOrEmpty(response))
                    throw new TemplateOperationCancelledException();
                if (double.TryParse(response, out result))
                {
                    result /= 100.0;
                    if (result >= 0.0 && result <= 1.0)
                        outputParameters = new ParameterBag(parameter.Name, new FilledParameter(FilledParameterDirection.Input, result));
                }
                // else go round again
            }
        }

        public void Visit(Double2By2Parameter parameter)
        {
            throw new NotImplementedException();
        }

        public void Visit(DoubleParameter parameter)
        {
            double minimumValue = parameter.MinimumValue(processor, context);
            double maximumValue = parameter.MaximumValue(processor, context);
            // Prompt for the range
            string suffix = string.Empty;
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
                string defaultValueString = string.Empty;
                double? defaultValue = parameter.DefaultValue(processor, context);
                if (defaultValue.HasValue && !double.IsNaN(defaultValue.Value))
                    defaultValueString = defaultValue.Value.ToString();
                string response = SdApplication.SoleInstance.GetString(parameter.Prompt(processor, context) + suffix, "StatsDirect", defaultValueString);
                if (string.IsNullOrEmpty(response))
                {
                    if (null != parameter.CancelSkipsParameter)
                    {
                        outputParameters = new ParameterBag();
                        return;
                    }
                    throw new TemplateOperationCancelledException();
                }
                double result = Parsing.Cdbl_Txt(response);
                if (result >= minimumValue && result <= maximumValue)
                {
                    outputParameters = new ParameterBag(parameter.Name, new FilledParameter(FilledParameterDirection.Input, result));
                    break;
                }
                // else go round and prompt again
            }
        }

        public void Visit(FillableParameter parameter)
        {
            throw new NotImplementedException();
        }

        public void Visit(FrameParameter parameter)
        {
            if (null == SdApplication.SoleInstance.ActiveGrid || !SdApplication.SoleInstance.ActiveGrid.HasWindow)
            {
                SdApplication.SoleInstance.FriendlyError("There are no workbooks open from which to select data. Please create or open a workbook containing your data, then run the operation again.", null, false);
                throw new TemplateOperationCancelledException();
            }
            IGrid grid = (IGrid)SdApplication.SoleInstance.ActiveGrid.Window;
            outputParameters = new GridSelectionProcessor(grid).FillFrameParameter(parameter, processor, SdApplication.SoleInstance, context);
        }

        public void Visit(IntegerParameter parameter)
        {
            // Prompt for the range
            string suffix = string.Empty;
            if (parameter.MinimumValue > int.MinValue || parameter.MaximumValue < int.MaxValue)
            {
                suffix = " (";
                if (parameter.MinimumValue > int.MinValue)
                    suffix += parameter.MinimumValue.ToString();
                suffix += " to ";
                if (parameter.MaximumValue < int.MaxValue)
                    suffix += parameter.MaximumValue.ToString();
                suffix += ")";
            }
            while (true)
            {
                int? defaultValue = 0;
                if (parameter.HasDefaultValue)
                    defaultValue = parameter.DefaultValue(processor, context);

                string response = SdApplication.SoleInstance.GetString(parameter.Prompt(processor, context) + suffix, "StatsDirect", defaultValue.HasValue ? defaultValue.Value.ToString() : string.Empty);
                if (string.IsNullOrEmpty(response))
                {
                    if (null != parameter.CancelSkipsParameter)
                    {
                        outputParameters = new ParameterBag();
                        return;
                    }
                    throw new TemplateOperationCancelledException();
                }
                int result = Parsing.Cint_Txt(response);
                if (result >= parameter.MinimumValue && result <= parameter.MaximumValue)
                {
                    outputParameters = new ParameterBag(parameter.Name, new FilledParameter(FilledParameterDirection.Input, result));
                    return;
                }
                // else go round and prompt again
            }
        }

        public void Visit(OptionsParameter parameter)
        {
            throw new NotImplementedException();
        }

        public void Visit(PickVariablesParameter parameter)
        {
            throw new NotImplementedException();
        }

        public void Visit(StringParameter parameter)
        {
            throw new NotImplementedException();
        }

        public void Visit(SpecialParameter parameter)
        {
            throw new NotImplementedException();
        }

        public void Visit(PickFromListParameter parameter)
        {
            throw new NotImplementedException();
        }

        public void Visit(OptionParameter parameter)
        {
            throw new NotImplementedException();
        }

        public void Visit(GroupedCovarianceParameter parameter)
        {
            IGrid grid = (IGrid)SdApplication.SoleInstance.ActiveGrid.Window;
            Builtins.GroupedCovarianceData data = new GridSelectionProcessor(grid).FillGroupedCovarianceParameter(processor);
            if (null != data)
                outputParameters = new ParameterBag(parameter.Name, new FilledParameter(FilledParameterDirection.Input, data));
        }

        public void Visit(Frame2DParameter parameter)
        {
            if (null == SdApplication.SoleInstance.ActiveGrid || !SdApplication.SoleInstance.ActiveGrid.HasWindow)
            {
                SdApplication.SoleInstance.FriendlyError("There are no workbooks open from which to select data. Please create or open a workbook containing your data, then run the operation again.", null, false);
                throw new TemplateOperationCancelledException();
            }
            IGrid grid = (IGrid)SdApplication.SoleInstance.ActiveGrid.Window;
            DataFrame2D frame = new GridSelectionProcessor(grid).FillFrameParameter2D(parameter, processor, SdApplication.SoleInstance, context);
            outputParameters = null == frame ? null : new ParameterBag(parameter.Name, new FilledParameter(FilledParameterDirection.Input, frame));
        }

        public void Visit(EditGridParameter parameter)
        {
            throw new NotImplementedException();
        }

        public void Visit(Double2By2ByKParameter parameter)
        {
            throw new NotImplementedException();
        }

        public void Visit(DateParameter parameter)
        {
            throw new NotImplementedException();
        }

        public void Visit(ChartOptionsParameter parameter)
        {
            throw new NotImplementedException();
        }

        public void Visit(BooleanParameter parameter)
        {
            DialogResult result = SdApplication.SoleInstance.MsgboxX(parameter.Prompt(processor, context), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, "StatsDirect", true);
            if (DialogResult.Cancel == result)
            {
                if (null != parameter.CancelSkipsParameter)
                {
                    outputParameters = new ParameterBag();
                    return;
                }
                throw new TemplateOperationCancelledException();
            }
            outputParameters = new ParameterBag(parameter.Name, new FilledParameter(FilledParameterDirection.Input, DialogResult.Yes == result));
        }
    }
}
