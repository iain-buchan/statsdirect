namespace StatsDirect.Templates
{
    /// <summary>
    /// The signature of any function that can be called as a builtin.
    /// </summary>
    /// <param name="host">The host in which the function is running</param>
    /// <param name="parameters">Name-to-object mappings for any parameters that are handed to the builtin</param>
    /// <returns>A new set of name-to-object mappings.  Builtins *must not* alter parameters and hand it back; they *must* allocate a new Dictionary.</returns>
    public delegate ParameterBag BuiltinFunction(ITemplateHost host, ParameterBag parameters);

    /// <summary>
    /// The signature of any function that can be called as a builtin.
    /// </summary>
    /// <param name="parameters">Name-to-object mappings for any parameters that are handed to the builtin</param>
    /// <returns>A new set of name-to-object mappings.  Builtins *must not* alter parameters and hand it back; they *must* allocate a new Dictionary.</returns>
    public delegate ParameterBag PureBuiltinFunction(ParameterBag parameters);

    public interface IBuiltin : IMightRequireInput
    {
        ParameterBag Invoke(ITemplateHost host, ParameterBag parameters);

        string Name { get; }
    }
}
