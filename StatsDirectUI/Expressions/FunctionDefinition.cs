using System;
using System.Collections.Generic;
using System.Linq;

namespace StatsDirect.Expressions
{
    public class FunctionDefinition
    {
        public string Name { get; set; }
        public string ClrName { get; set; }
        public List<ArgumentDefinition> ArgumentDefinitions { get; private set; }

        public FunctionDefinition(string name, string clrName, IEnumerable<ArgumentDefinition> argumentDefinitions)
        {
            Name = name;
            ClrName = clrName;

            if (null == argumentDefinitions)
                return;

            ArgumentDefinitions = new List<ArgumentDefinition>(argumentDefinitions);
        }

        public override string ToString()
        {
            return Name + "(" + string.Join(", ", ArgumentDefinitions.Select(x=>x.ToString()).ToArray()) + ")";
        }
    }
}
