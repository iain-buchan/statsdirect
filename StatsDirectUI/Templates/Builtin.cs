namespace StatsDirect.Templates
{
    public class Builtin : IBuiltin
    {
        readonly string name;
        private readonly BuiltinFunction func;
        private readonly InputDuringStep requiresInput;

        public Builtin(string name, BuiltinFunction func, InputDuringStep requiresInput = InputDuringStep.Never)
        {
            this.name = name;
            this.func = func;
            this.requiresInput = requiresInput;
        }

        ParameterBag IBuiltin.Invoke(ITemplateHost host, ParameterBag parameters)
        {
            return func(host, parameters);
        }

        InputDuringStep IMightRequireInput.RequiresInputGiven(ParameterBag parameters)
        {
            return requiresInput;
        }

        string IBuiltin.Name => name;
    }
}
