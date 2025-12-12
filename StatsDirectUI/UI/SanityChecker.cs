using StatsDirect.Creole;
using StatsDirect.Templates;
using System;
using System.Collections.Generic;

namespace StatsDirect.UI
{
    /// <summary>
    /// Pre-release checker for things that could go wrong.
    /// </summary>
    internal class SanityChecker
    {
        public static void Check()
        {
            List<Operation> operations = new();
            operations.AddRange(TemplateFactory.Operations.Values);
            operations.AddRange(TemplateFactory.UserOperations);
            CheckAllTemplatesParse(operations);
            CheckAllDynamicContentCompiles(operations);
        }

        /// <summary>
        /// Designed to check that anything that could be compiled and run by our internal compiler at least compiles.
        /// </summary>
        private static void CheckAllDynamicContentCompiles(IList<Operation> operations)
        {
            foreach (Operation operation in operations)
                CheckAllDynamicContentCompiles(operation);
        }

        /// <summary>
        /// Designed to check that anything that could be compiled and run by our internal compiler at least compiles.
        /// </summary>
        private static void CheckAllTemplatesParse(IList<Operation> operations)
        {
            foreach (Operation operation in operations)
                CheckAllTemplatesParse(operation);
        }

        private static void CheckAllDynamicContentCompiles(Operation operation)
        {
            foreach (CustomValidator customValidator in operation.CustomValidators)
                CheckAllDynamicContentCompiles(customValidator, operation.Name + ".CustomValidators");
            foreach (Precondition precondition in operation.Preconditions)
                CheckAllDynamicContentCompiles(precondition, operation.Name + ".Preconditions");
            foreach (SuggestedOperation suggestedOperation in operation.SuggestedOperations)
                CheckAllDynamicContentCompiles(suggestedOperation, operation.Name + ".SuggestedOperations");
            CheckAllDynamicContentCompiles(operation.Steps, operation.Name + ".Steps");
        }

        private static void CheckAllTemplatesParse(Operation operation)
        {
            CheckAllTemplatesParse(operation.Steps, operation.Name + ".Steps");
        }

        private static void CheckAllDynamicContentCompiles(IList<Step> steps, string prefix)
        {
            for (int i = 0; i < steps.Count; i++)
                steps[i].Accept(new DynamicContentStepChecker(prefix + "[" + i + "]"));
        }

        private static void CheckAllTemplatesParse(IList<Step> steps, string prefix)
        {
            for (int i = 0; i < steps.Count; i++)
                steps[i].Accept(new TemplateStepChecker(prefix + "[" + i + "]"));
        }

        private static void CheckAllDynamicContentCompiles(IList<Parameter> parameters)
        {
            for (int i = 0; i < parameters.Count; i++)
                parameters[i].Accept(new DynamicContentParameterChecker());
        }

        private static void CheckAllDynamicContentCompiles(SuggestedOperation suggestedOperation, string prefix)
        {
            if (null == suggestedOperation)
                return;
            CheckAllDynamicContentCompiles(suggestedOperation.SuggestIf, prefix + "[" + suggestedOperation.Name + "].SuggestIf");
        }

        private static void CheckAllDynamicContentCompiles(Precondition precondition, string prefix)
        {
            if (null == precondition)
                return;
            CheckAllDynamicContentCompiles(precondition.Condition, prefix + ".Condition");
        }

        private static void CheckAllDynamicContentCompiles(Expression condition, string prefix)
        {
            if (condition?.Body == null)
                return;
            if (!condition.Body.StartsWith("="))
                return;
            CheckScript(condition.Language, condition.Body.Substring(1), ScriptType.Expression, null, prefix + "." + condition.Body);
        }

        private static void CheckAllDynamicContentCompiles(CustomValidator customValidator, string prefix)
        {
            CheckScript(customValidator.Language, customValidator.Script, ScriptType.Validator, null, prefix + "." + customValidator.Name);
        }

        private static void CheckAllDynamicContentCompiles(Validator[] validators, string prefix)
        {
            if (null == validators)
                return;
            foreach (Validator validator in validators)
                CheckAllDynamicContentCompiles(validator, prefix + "." + validator.ValidatorName);
        }

        private static void CheckAllDynamicContentCompiles(Validator validator, string prefix)
        {
            CheckAllDynamicContentCompiles(validator.TestIfTrueExpression, prefix + ".TestIfTrue");
        }

        private static void CheckScript(string language, string script, ScriptType scriptType, string entryPoint, string identifier)
        {
            IScriptEngine engine = ((ITemplateHost)SdApplication.SoleInstance).GetScriptEngine(language);
            string result = engine.Check(language, script, scriptType, entryPoint);
            if (null != result)
                throw new Exception(identifier + " failed: " + result);
        }

        private static void CheckTemplateParses(string content, string identifier)
        {
            if (!CreoleReader.IsValid(content, out string result))
                throw new Exception(identifier + " failed: " + result);
        }

        private class DynamicContentParameterChecker : IParameterVisitor
        {
            private static void CheckCommon(Parameter parameter)
            {
                CheckAllDynamicContentCompiles(parameter.AcquireIfTrueExpression, ".AcquireIfTrueExpression");
                CheckAllDynamicContentCompiles(parameter.PromptExpression, ".PromptExpression");
                CheckAllDynamicContentCompiles(parameter.RubricExpression, ".RubricExpression");
                CheckAllDynamicContentCompiles(parameter.Validators, ".Validators");
            }

            private void CheckRange(RangeParameter parameter)
            {
                CheckCommon(parameter);
            }

            public void Visit(Double2By2Parameter parameter)
            {
                CheckCommon(parameter);
            }

            public void Visit(DoubleParameter parameter)
            {
                CheckRange(parameter);
                CheckAllDynamicContentCompiles(parameter.DefaultValueExpression, ".DefaultValueExpression");
                CheckAllDynamicContentCompiles(parameter.MaximumValueExpression, ".MaximumValueExpression");
                CheckAllDynamicContentCompiles(parameter.MinimumValueExpression, ".MinimumValueExpression");
            }

            public void Visit(FillableParameter parameter)
            {
                CheckCommon(parameter);
            }

            public void Visit(FrameParameter parameter)
            {
                CheckCommon(parameter);
                CheckAllDynamicContentCompiles(parameter.LengthExpression, ".LengthExpression");
                CheckAllDynamicContentCompiles(parameter.MaximumColumnsExpression, ".MaximumColumnsExpressio");
                CheckAllDynamicContentCompiles(parameter.MinimumColumnsExpression, ".MinimumColumnsExpression");
            }

            public void Visit(IntegerParameter parameter)
            {
                CheckRange(parameter);
                CheckAllDynamicContentCompiles(parameter.DefaultValueExpression, ".DefaultValueExpression");
            }

            public void Visit(OptionsParameter parameter)
            {
                CheckCommon(parameter);
            }

            public void Visit(PickVariablesParameter parameter)
            {
                CheckCommon(parameter);
                CheckAllDynamicContentCompiles(parameter.LabelAsExpression, ".LabelAsExpression");
            }

            public void Visit(StringParameter parameter)
            {
                CheckCommon(parameter);
                CheckAllDynamicContentCompiles(parameter.DefaultValueExpression, ".DefaultValueExpression");
            }

            public void Visit(SpecialParameter parameter)
            {
                CheckCommon(parameter);
            }

            public void Visit(PickFromListParameter parameter)
            {
                CheckCommon(parameter);
            }

            public void Visit(OptionParameter parameter)
            {
                CheckCommon(parameter);
                CheckAllDynamicContentCompiles(parameter.DefaultValueExpression, ".DefaultValueExpression");
            }

            public void Visit(GroupedCovarianceParameter parameter)
            {
                CheckCommon(parameter);
            }

            public void Visit(Frame2DParameter parameter)
            {
                CheckCommon(parameter);
                CheckAllDynamicContentCompiles(parameter.SubPromptExpression, ".SubPromptExpression");
                CheckAllDynamicContentCompiles(parameter.LengthExpression, ".LengthExpression");
                CheckAllDynamicContentCompiles(parameter.MaximumColumnsExpression, ".MaximumColumnsExpressio");
                CheckAllDynamicContentCompiles(parameter.MinimumColumnsExpression, ".MinimumColumnsExpression");
            }

            public void Visit(EditGridParameter parameter)
            {
                CheckCommon(parameter);
            }

            public void Visit(Double2By2ByKParameter parameter)
            {
                CheckCommon(parameter);
            }

            public void Visit(DateParameter parameter)
            {
                CheckCommon(parameter);
                CheckAllDynamicContentCompiles(parameter.DefaultValueExpression, ".DefaultValueExpression");
            }

            public void Visit(ConfidenceIntervalParameter parameter)
            {
                CheckCommon(parameter);
            }

            public void Visit(ChartOptionsParameter parameter)
            {
                CheckCommon(parameter);
            }

            public void Visit(BooleanParameter parameter)
            {
                CheckCommon(parameter);
                CheckAllDynamicContentCompiles(parameter.DefaultValueExpression, ".DefaultValueExpression");
            }
        }

        private class DynamicContentStepChecker : IStepVisitor
        {
            private readonly string prefix;

            public DynamicContentStepChecker(string prefix)
            {
                this.prefix = prefix;
            }

            public void Visit(IterationStep step)
            {
                CheckAllDynamicContentCompiles(step.Steps, prefix + ".Steps");
            }

            public void Visit(ParametersStep step)
            {
                CheckAllDynamicContentCompiles(step.Parameters);
            }

            public void Visit(ScriptStep step)
            {
                ScriptType scriptType = null == step.EntryPoint ? ScriptType.Function : ScriptType.MultipleMethods;
                CheckScript(step.Language, step.Body, scriptType, step.EntryPoint, prefix);
            }

            public void Visit(TestStep step)
            {
                CheckAllDynamicContentCompiles(step.Condition, prefix + ".Condition");
                CheckAllDynamicContentCompiles(step.FalseSteps, prefix + ".FalseSteps");
                CheckAllDynamicContentCompiles(step.TrueSteps, prefix + ".TrueSteps");
            }

            public void Visit(ReportStep step)
            {
                // Nothing to check
            }

            public void Visit(OutputFrameStep step)
            {
                // Nothing to check
            }

            public void Visit(ChartStep step)
            {
                CheckAllDynamicContentCompiles(step.XAxisTitleExpression, prefix + ".XAxisTitleExpression");
                CheckAllDynamicContentCompiles(step.YAxisTitleExpression, prefix + ".YAxisTitleExpression");
            }

            public void Visit(BuiltinStep step)
            {
                // Nothing to check
            }
        }

        private class TemplateStepChecker : IStepVisitor
        {
            private readonly string prefix;

            public TemplateStepChecker(string prefix)
            {
                this.prefix = prefix;
            }

            public void Visit(IterationStep step)
            {
                // Nothing to check
            }

            public void Visit(ParametersStep step)
            {
                // Nothing to check
            }

            public void Visit(ScriptStep step)
            {
                // Nothing to check
            }

            public void Visit(TestStep step)
            {
                // Nothing to check
            }

            public void Visit(ReportStep step)
            {
                string content = step.Text ?? SdApplication.SoleInstance.GetReportTemplate(step.FileName);
                CheckTemplateParses(content, prefix + " (" + (step.FileName ?? "inline") + ")");
            }

            public void Visit(OutputFrameStep step)
            {
                // Nothing to check
            }

            public void Visit(ChartStep step)
            {
                // Nothing to check
            }

            public void Visit(BuiltinStep step)
            {
                // Nothing to check
            }
        }
    }
}