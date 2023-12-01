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
        private IScriptEngineHost ScriptEngineHost { get; }

        public SanityChecker(IScriptEngineHost scriptEngineHost)
        {
            ScriptEngineHost = scriptEngineHost;
        }

        public void Check()
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
        private void CheckAllDynamicContentCompiles(IList<Operation> operations)
        {
            foreach (Operation operation in operations)
                CheckAllDynamicContentCompiles(operation);
        }

        /// <summary>
        /// Designed to check that anything that could be compiled and run by our internal compiler at least compiles.
        /// </summary>
        private void CheckAllTemplatesParse(IList<Operation> operations)
        {
            foreach (Operation operation in operations)
                CheckAllTemplatesParse(operation);
        }

        private void CheckAllDynamicContentCompiles(Operation operation)
        {
            foreach (CustomValidator customValidator in operation.CustomValidators)
                CheckAllDynamicContentCompiles(customValidator, operation.Name + ".CustomValidators");
            foreach (Precondition precondition in operation.Preconditions)
                CheckAllDynamicContentCompiles(precondition, operation.Name + ".Preconditions");
            foreach (SuggestedOperation suggestedOperation in operation.SuggestedOperations)
                CheckAllDynamicContentCompiles(suggestedOperation, operation.Name + ".SuggestedOperations");
            CheckAllDynamicContentCompiles(operation.Steps, operation.Name + ".Steps");
        }

        private void CheckAllTemplatesParse(Operation operation)
        {
            CheckAllTemplatesParse(operation.Steps, operation.Name + ".Steps");
        }

        private void CheckAllDynamicContentCompiles(IList<Step> steps, string prefix)
        {
            for (int i = 0; i < steps.Count; i++)
                steps[i].Accept(new DynamicContentStepChecker(this, $"{prefix}[{i}]"));
        }

        private void CheckAllTemplatesParse(IList<Step> steps, string prefix)
        {
            for (int i = 0; i < steps.Count; i++)
                steps[i].Accept(new TemplateStepChecker($"{prefix}[{i}]"));
        }

        private void CheckAllDynamicContentCompiles(IList<Parameter> parameters)
        {
            for (int i = 0; i < parameters.Count; i++)
                parameters[i].Accept(new DynamicContentParameterChecker(this));
        }

        private void CheckAllDynamicContentCompiles(SuggestedOperation suggestedOperation, string prefix)
        {
            if (suggestedOperation is null)
                return;
            CheckAllDynamicContentCompiles(suggestedOperation.SuggestIf, prefix + "[" + suggestedOperation.Name + "].SuggestIf");
        }

        private void CheckAllDynamicContentCompiles(Precondition? precondition, string prefix)
        {
            if (precondition is null)
                return;
            CheckAllDynamicContentCompiles(precondition.Condition, prefix + ".Condition");
        }

        private void CheckAllDynamicContentCompiles(Expression? condition, string prefix)
        {
            if (condition?.Body is null)
                return;
            if (!condition.Body.StartsWith("="))
                return;
            CheckScript(condition.Language, condition.Body.Substring(1), ScriptType.Expression, null, prefix + "." + condition.Body);
        }

        private void CheckAllDynamicContentCompiles(CustomValidator customValidator, string prefix)
        {
            CheckScript(customValidator.Language, customValidator.Script, ScriptType.Validator, null, prefix + "." + customValidator.Name);
        }

        private void CheckAllDynamicContentCompiles(Validator[] validators, string prefix)
        {
            if (validators is null)
                return;
            foreach (Validator validator in validators)
                CheckAllDynamicContentCompiles(validator, prefix + "." + validator.ValidatorName);
        }

        private void CheckAllDynamicContentCompiles(Validator validator, string prefix)
        {
            CheckAllDynamicContentCompiles(validator.TestIfTrueExpression, prefix + ".TestIfTrue");
        }

        private void CheckScript(string language, string script, ScriptType scriptType, string entryPoint, string identifier)
        {
            if (!ScriptEngineHost.TryGetScriptEngine(language, out IScriptEngine? scriptEngine))
                throw new Exception($"{identifier} failed: could not get script engine for '{language}'");
            string result = scriptEngine.Check(language, script, scriptType, entryPoint);
            if (result is not null)
                throw new Exception(identifier + " failed: " + result);
        }

        private static void CheckTemplateParses(string content, string identifier)
        {
            if (!CreoleReader.IsValid(content, out string result))
                throw new Exception(identifier + " failed: " + result);
        }

        private class DynamicContentParameterChecker : IParameterVisitor
        {
            private SanityChecker parent;

            public DynamicContentParameterChecker(SanityChecker parent)
            {
                this.parent = parent;
            }

            private void CheckCommon(Parameter parameter)
            {
                parent.CheckAllDynamicContentCompiles(parameter.AcquireIfTrueExpression, ".AcquireIfTrueExpression");
                parent.CheckAllDynamicContentCompiles(parameter.PromptExpression, ".PromptExpression");
                parent.CheckAllDynamicContentCompiles(parameter.RubricExpression, ".RubricExpression");
                parent.CheckAllDynamicContentCompiles(parameter.Validators, ".Validators");
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
                parent.CheckAllDynamicContentCompiles(parameter.DefaultValueExpression, ".DefaultValueExpression");
                parent.CheckAllDynamicContentCompiles(parameter.MaximumValueExpression, ".MaximumValueExpression");
                parent.CheckAllDynamicContentCompiles(parameter.MinimumValueExpression, ".MinimumValueExpression");
            }

            public void Visit(FillableParameter parameter)
            {
                CheckCommon(parameter);
            }

            public void Visit(FrameParameter parameter)
            {
                CheckCommon(parameter);
                parent.CheckAllDynamicContentCompiles(parameter.LengthExpression, ".LengthExpression");
                parent.CheckAllDynamicContentCompiles(parameter.MaximumColumnsExpression, ".MaximumColumnsExpressio");
                parent.CheckAllDynamicContentCompiles(parameter.MinimumColumnsExpression, ".MinimumColumnsExpression");
            }

            public void Visit(IntegerParameter parameter)
            {
                CheckRange(parameter);
                parent.CheckAllDynamicContentCompiles(parameter.DefaultValueExpression, ".DefaultValueExpression");
            }

            public void Visit(OptionsParameter parameter)
            {
                CheckCommon(parameter);
            }

            public void Visit(PickVariablesParameter parameter)
            {
                CheckCommon(parameter);
                parent.CheckAllDynamicContentCompiles(parameter.LabelAsExpression, ".LabelAsExpression");
            }

            public void Visit(StringParameter parameter)
            {
                CheckCommon(parameter);
                parent.CheckAllDynamicContentCompiles(parameter.DefaultValueExpression, ".DefaultValueExpression");
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
                parent.CheckAllDynamicContentCompiles(parameter.DefaultValueExpression, ".DefaultValueExpression");
            }

            public void Visit(GroupedCovarianceParameter parameter)
            {
                CheckCommon(parameter);
            }

            public void Visit(Frame2DParameter parameter)
            {
                CheckCommon(parameter);
                parent.CheckAllDynamicContentCompiles(parameter.SubPromptExpression, ".SubPromptExpression");
                parent.CheckAllDynamicContentCompiles(parameter.LengthExpression, ".LengthExpression");
                parent.CheckAllDynamicContentCompiles(parameter.MaximumColumnsExpression, ".MaximumColumnsExpressio");
                parent.CheckAllDynamicContentCompiles(parameter.MinimumColumnsExpression, ".MinimumColumnsExpression");
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
                parent.CheckAllDynamicContentCompiles(parameter.DefaultValueExpression, ".DefaultValueExpression");
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
                parent.CheckAllDynamicContentCompiles(parameter.DefaultValueExpression, ".DefaultValueExpression");
            }
        }

        private class DynamicContentStepChecker : IStepVisitor
        {
            private SanityChecker parent;
            private readonly string prefix;

            public DynamicContentStepChecker(SanityChecker parent, string prefix)
            {
                this.parent = parent;
                this.prefix = prefix;
            }

            public void Visit(IterationStep step)
            {
                parent.CheckAllDynamicContentCompiles(step.Steps, prefix + ".Steps");
            }

            public void Visit(ParametersStep step)
            {
                parent.CheckAllDynamicContentCompiles(step.Parameters);
            }

            public void Visit(ScriptStep step)
            {
                ScriptType scriptType = null == step.EntryPoint ? ScriptType.Function : ScriptType.MultipleMethods;
                parent.CheckScript(step.Language, step.Body, scriptType, step.EntryPoint, prefix);
            }

            public void Visit(TestStep step)
            {
                parent.CheckAllDynamicContentCompiles(step.Condition, prefix + ".Condition");
                parent.CheckAllDynamicContentCompiles(step.FalseSteps, prefix + ".FalseSteps");
                parent.CheckAllDynamicContentCompiles(step.TrueSteps, prefix + ".TrueSteps");
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
                parent.CheckAllDynamicContentCompiles(step.XAxisTitleExpression, prefix + ".XAxisTitleExpression");
                parent.CheckAllDynamicContentCompiles(step.YAxisTitleExpression, prefix + ".YAxisTitleExpression");
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
                CheckTemplateParses(step.GetContent(), prefix + " (" + (step.FileName ?? "inline") + ")");
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