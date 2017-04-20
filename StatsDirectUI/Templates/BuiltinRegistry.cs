using System;
using System.Collections.Generic;

namespace StatsDirect.Templates
{
    public class BuiltinRegistry
    {
        private readonly Dictionary<string, Builtin> builtins;

        private static BuiltinRegistry soleInstance;

        public static BuiltinRegistry SoleInstance => soleInstance ?? (soleInstance = new BuiltinRegistry());

        private BuiltinRegistry()
        {
            builtins = new Dictionary<string,Builtin>();
        }

        public Builtin Builtin(string name)
        {
            if (!builtins.TryGetValue(name, out Builtin builtin))
                throw new Exception("No built-in operation named '" + name + "' exists in the function registry.");
            return builtin;
        }

        public void AddAll(ICollection<Builtin> candidates)
        {
            foreach (Builtin candidate in candidates)
                builtins.Add(candidate.Name, candidate);
        }
    }
}
