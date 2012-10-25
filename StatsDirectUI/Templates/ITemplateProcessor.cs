namespace StatsDirect.Templates
{
    /// <summary>
    /// The methods a template processor should implement.
    /// Required because the implementation of a template processor must be separated from Templates to prevent circular references, but
    /// the double-dispatching approach taken to process the steps also requires a circular reference unless the dependencies are broken in this way.
    /// </summary>
    public interface ITemplateProcessor
    {
        void PrepareInternal(ParametersStep step, ParameterBag parameters);

        StepResult ExecuteInternal(BuiltinStep step, ParameterBag parameters, bool isRedo);
        StepResult ExecuteInternal(ChartStep step, ParameterBag parameters, bool isRedo);
        StepResult ExecuteInternal(IterationStep step, ParameterBag parameters, bool isRedo);
        StepResult ExecuteInternal(OutputFrameStep step, ParameterBag parameters, bool isRedo);
        StepResult ExecuteInternal(ParametersStep step, ParameterBag parameters, bool isRedo);
        StepResult ExecuteInternal(ReportStep step, ParameterBag parameters, bool isRedo);
        StepResult ExecuteInternal(ScriptStep step, ParameterBag parameters, bool isRedo);
        // StepResult ExecuteInternal(SelectOutputForFrameStep step, ParameterBag parameters, bool isRedo);
        StepResult ExecuteInternal(TestStep step, ParameterBag parameters, bool isRedo);

        object Evaluate(Expression expression, ParameterBag parameters);
    }
}
