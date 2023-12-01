namespace StatsDirect.Templates
{
    public class Builtin : IBuiltin
    {
        readonly string name;
        private readonly BuiltinFunction func;

        public Builtin(string name, BuiltinFunction func)
        {
            this.name = name;
            this.func = func;
        }

        StepOutput? IBuiltin.Invoke(ITemplateHost host, ParameterBag parameters)
        {
            return func(parameters);
        }

        InputDuringStep IMightRequireInput.RequiresInputGiven(ParameterBag parameters)
        {
            return InputDuringStep.Never;
        }

        string IBuiltin.Name => name;
    }
}
