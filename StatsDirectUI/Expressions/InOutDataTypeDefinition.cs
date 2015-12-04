using System.Collections.Generic;
using System.Linq;

namespace StatsDirect.Expressions
{
    public class InOutDataTypeDefinition
    {
        public DataType ReturnType { get; private set; }
        public DataType[] InputTypes { get; private set; }

        public InOutDataTypeDefinition(DataType returnType, IEnumerable<DataType> inputTypes)
        {
            ReturnType = returnType;
            InputTypes = inputTypes.ToArray();
        }
    }
}