using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StatsDirect.Templates
{
    /// <summary>
    /// The signature of any function that can be called as a builtin.
    /// </summary>
    /// <param name="host">The host in which the function is running</param>
    /// <param name="parameters">Name-to-object mappings for any parameters that are handed to the builtin</param>
    /// <returns>A new set of name-to-object mappings.  Builtins *must not* alter parameters and hand it back; they *must* allocate a new Dictionary.</returns>
    public delegate ParameterBag BuiltinFunction(ITemplateHost host, ParameterBag parameters);

    public class Builtin : IMightRequireInput
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

        public InputDuringStep RequiresInputGiven(ParameterBag parameters)
        {
            return requiresInput;
        }

        public BuiltinFunction FunctionToCall
        {
            get { return func; }
        }

        public string Name
        {
            get { return name; }
        }
    }
}
