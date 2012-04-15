using System.Collections.Generic;

namespace StatsDirect.Templates
{
    public class BuiltinRegistry
    {
        private readonly Dictionary<string, Builtin> builtins;

        private static BuiltinRegistry soleInstance;

        public static BuiltinRegistry SoleInstance
        {
            get { return soleInstance ?? (soleInstance = new BuiltinRegistry()); }
        }

        private BuiltinRegistry()
        {
            builtins = new Dictionary<string,Builtin>();
        }

        public Builtin Builtin(string name)
        {
            return builtins[name];
        }

        public void AddAll(ICollection<Builtin> candidates)
        {
            foreach (Builtin candidate in candidates)
            {
                builtins.Add(candidate.Name, candidate);
            }
        }
    }
}
