namespace StatsDirect.Templates
{
    public enum StepSuccess
    {
        Success,
        Failed
    }

    public class StepResult
    {
        public StepSuccess StepSuccess { get; set; }
        public ParameterBag ParameterBag { get; set; }

        public StepResult(StepSuccess stepSuccess, ParameterBag parameterBag)
        {
            StepSuccess = stepSuccess;
            ParameterBag = parameterBag;
        }
    }
}
