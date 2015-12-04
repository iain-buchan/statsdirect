using System.Collections.Generic;
using System.Linq;

namespace StatsDirect.Expressions
{
    public class DyadicOperatorDefinition
    {
        public DyadicOperator Operator { get; private set; }
        public string ClrName { get; private set; }
        public bool ClrIsPrefix { get; private set; }
        public List<InOutDataTypeDefinition> InOutDataTypeDefinitions { get; private set; }

        public DyadicOperatorDefinition(DyadicOperator op, string clrName, bool clrIsPrefix, IEnumerable<InOutDataTypeDefinition> inOutDataTypeDefinitions)
        {
            Operator = op;
            ClrName = clrName;
            ClrIsPrefix = clrIsPrefix;

            if (null == inOutDataTypeDefinitions)
                InOutDataTypeDefinitions = new List<InOutDataTypeDefinition>();
            else
                InOutDataTypeDefinitions = new List<InOutDataTypeDefinition>(inOutDataTypeDefinitions);
        }

        public override string ToString()
        {
            return Operator.ToString() + "(" + string.Join(", ", InOutDataTypeDefinitions.Select(x=>x.ToString()).ToArray()) + ")";
        }
    }
}
