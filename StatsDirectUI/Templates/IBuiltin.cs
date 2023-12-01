namespace StatsDirect.Templates
{
    /// <summary>
    /// The signature of a function that can be called as a builtin that only takes parameters.
    /// </summary>
    /// <param name="parameters">Name-to-object mappings for any parameters that are handed to the builtin</param>
    /// <returns>A new set of name-to-object mappings.  Builtins *must not* alter parameters and hand it back; they *must* allocate a new Dictionary.</returns>
    public delegate StepOutput? BuiltinFunction(ParameterBag parameters);

    public interface IBuiltin : IMightRequireInput
    {
        StepOutput? Invoke(ITemplateHost host, ParameterBag parameters);

        string Name { get; }
    }
}
