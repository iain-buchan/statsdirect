using System;
using System.Collections.Generic;
using System.Linq;

namespace StatsDirect.Expressions
{
    public class FunctionDefinition
    {
        public string Name { get; private set; }
        public string ClrName { get; private set; }
        public List<ArgumentDefinition> ArgumentDefinitions { get; private set; }
        public DataType DataType { get; private set; }

        public FunctionDefinition(string name, DataType dataType, string clrName, IEnumerable<ArgumentDefinition> argumentDefinitions)
        {
            Name = name;
            ClrName = clrName;
            DataType = dataType;

            if (null == argumentDefinitions)
                ArgumentDefinitions = new List<ArgumentDefinition>();
            else
                ArgumentDefinitions = new List<ArgumentDefinition>(argumentDefinitions);
        }

        public override string ToString()
        {
            return Name + "(" + string.Join(", ", ArgumentDefinitions.Select(x=>x.ToString()).ToArray()) + ")";
        }
    }
}
