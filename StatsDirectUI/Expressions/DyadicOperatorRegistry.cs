using System.Collections.Generic;

namespace StatsDirect.Expressions
{
    /// <remarks>Singleton.</remarks>
    public class DyadicOperatorRegistry
    {
        private readonly Dictionary<DyadicOperator, DyadicOperatorDefinition> definitions;

        private static DyadicOperatorRegistry soleInstance;

        public static DyadicOperatorRegistry SoleInstance
        {
            get { return soleInstance ?? (soleInstance = new DyadicOperatorRegistry()); }
        }

        private DyadicOperatorRegistry()
        {
            // A few useful parameters that can be re-used
            InOutDataTypeDefinition bbb = new InOutDataTypeDefinition(DataType.Boolean, new[] { DataType.Boolean, DataType.Boolean });
            InOutDataTypeDefinition bdd = new InOutDataTypeDefinition(DataType.Boolean, new[] { DataType.Double, DataType.Double });
            InOutDataTypeDefinition bii = new InOutDataTypeDefinition(DataType.Boolean, new[] { DataType.Integer, DataType.Integer });
            InOutDataTypeDefinition bss = new InOutDataTypeDefinition(DataType.Boolean, new[] { DataType.String, DataType.String });
            InOutDataTypeDefinition ddd = new InOutDataTypeDefinition(DataType.Double, new[] { DataType.Double, DataType.Double });
            InOutDataTypeDefinition idd = new InOutDataTypeDefinition(DataType.Integer, new[] { DataType.Double, DataType.Double });
            InOutDataTypeDefinition iii = new InOutDataTypeDefinition(DataType.Integer, new[] { DataType.Integer, DataType.Integer });
            InOutDataTypeDefinition ssb = new InOutDataTypeDefinition(DataType.String, new[] { DataType.String, DataType.Boolean });
            InOutDataTypeDefinition ssd = new InOutDataTypeDefinition(DataType.String, new[] { DataType.String, DataType.Double });
            InOutDataTypeDefinition ssi = new InOutDataTypeDefinition(DataType.String, new[] { DataType.String, DataType.Integer });
            InOutDataTypeDefinition sss = new InOutDataTypeDefinition(DataType.String, new[] { DataType.String, DataType.String });
            definitions = new Dictionary<DyadicOperator, DyadicOperatorDefinition>();
            AddAll(new[]
            {
                new DyadicOperatorDefinition(DyadicOperator.IntegerDivide,       "SDMath.IDiv",  true,  new[] { idd }),
                new DyadicOperatorDefinition(DyadicOperator.Pow,                 "Math.Pow",     true,  new[] { ddd }),
                new DyadicOperatorDefinition(DyadicOperator.Add,                 " + ",          false, new[] { iii, ddd, sss, ssd, ssi, ssb }),
                new DyadicOperatorDefinition(DyadicOperator.And,                 " && ",         false, new[] { bbb }),
                new DyadicOperatorDefinition(DyadicOperator.Divide,              " / ",          false, new[] { ddd }),
                new DyadicOperatorDefinition(DyadicOperator.Modulo,              " % ",          false, new[] { ddd }),
                new DyadicOperatorDefinition(DyadicOperator.Multiply,            " * ",          false, new[] { ddd }),
                new DyadicOperatorDefinition(DyadicOperator.Or,                  " || ",         false, new[] { bbb }),
                new DyadicOperatorDefinition(DyadicOperator.Subtract,            " - ",          false, new[] { iii, ddd }),
                new DyadicOperatorDefinition(DyadicOperator.Equal,               " == ",         false, new[] { bbb, bii, bdd, bss }),
                new DyadicOperatorDefinition(DyadicOperator.GreaterThan,         " > ",          false, new[] { bii, bdd, bss }),
                new DyadicOperatorDefinition(DyadicOperator.GreaterThanOrEqual,  " >= ",         false, new[] { bii, bdd, bss }),
                new DyadicOperatorDefinition(DyadicOperator.LessThan,            " < ",          false, new[] { bii, bdd, bss }),
                new DyadicOperatorDefinition(DyadicOperator.LessThanOrEqual,     " <= ",         false, new[] { bii, bdd, bss }),
                new DyadicOperatorDefinition(DyadicOperator.NotEqual,            " != ",         false, new[] { bbb, bii, bdd, bss })
            });

        }

        private void AddAll(IEnumerable<DyadicOperatorDefinition> definitions)
        {
            foreach (DyadicOperatorDefinition definition in definitions)
                this.definitions.Add(definition.Operator, definition);
        }

        public DyadicOperatorDefinition DefinitionFor(DyadicOperator op)
        {
            DyadicOperatorDefinition definition;
            definitions.TryGetValue(op, out definition);
            return definition;
        }
    }
}
