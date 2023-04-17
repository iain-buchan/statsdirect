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

        StepOutput ExecuteInternal(BuiltinStep step, ParameterBag parameters, bool isRedo);
        StepOutput ExecuteInternal(ChartStep step, ParameterBag parameters, bool isRedo);
        StepOutput ExecuteInternal(IterationStep step, ParameterBag parameters, bool isRedo);
        StepOutput ExecuteInternal(OutputFrameStep step, ParameterBag parameters, bool isRedo);
        StepOutput ExecuteInternal(ParametersStep step, ParameterBag parameters, bool isRedo);
        StepOutput ExecuteInternal(ReportStep step, ParameterBag parameters, bool isRedo);
        StepOutput ExecuteInternal(ScriptStep step, ParameterBag parameters, bool isRedo);
        StepOutput ExecuteInternal(TestStep step, ParameterBag parameters, bool isRedo);

        object Evaluate(Expression expression, ParameterBag parameters);

        /// <summary>
        /// Returns a unique, monotonically increasing value for this processor that is intended to keep variables acquired at the same point together.
        /// </summary>
        int NextOriginGroup();

        /// <summary>
        /// Run the operation to completion or error.
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="startingParameters">If non-null, some parameters to be used as defaults.</param>
        /// <param name="isRedo"> </param>
        StepOutput Execute(Operation operation, ParameterBag startingParameters, bool isRedo);
    }
}
