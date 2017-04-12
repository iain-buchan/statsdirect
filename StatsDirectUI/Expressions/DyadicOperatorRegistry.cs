using System.Collections.Generic;

namespace StatsDirect.Expressions
{
    /// <remarks>Singleton.</remarks>
    public class DyadicOperatorRegistry
    {
        private readonly Dictionary<DyadicOperator, DyadicOperatorDefinition> definitions;

        private static DyadicOperatorRegistry soleInstance;

        public static DyadicOperatorRegistry SoleInstance => soleInstance ?? (soleInstance = new DyadicOperatorRegistry());

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
                new DyadicOperatorDefinition(DyadicOperator.IntegerDivide,       "SDMath.IDiv({0}, {1})",       new[] { idd }),
                new DyadicOperatorDefinition(DyadicOperator.Pow,                 "Math.Pow({0}, {1})",          new[] { ddd }),
                new DyadicOperatorDefinition(DyadicOperator.Add,                 "({0}) + ({1})",               new[] { iii, ddd, sss, ssd, ssi, ssb }),
                new DyadicOperatorDefinition(DyadicOperator.And,                 "({0}) && ({1})",              new[] { bbb }),
                new DyadicOperatorDefinition(DyadicOperator.Divide,              "({0}) / ({1})",               new[] { ddd }),
                new DyadicOperatorDefinition(DyadicOperator.Modulo,              "({0}) % ({1})",               new[] { ddd }),
                new DyadicOperatorDefinition(DyadicOperator.Multiply,            "({0}) * ({1})",               new[] { ddd }),
                new DyadicOperatorDefinition(DyadicOperator.Or,                  "({0}) || ({1})",              new[] { bbb }),
                new DyadicOperatorDefinition(DyadicOperator.Subtract,            "({0}) - ({1})",               new[] { iii, ddd }),
                new DyadicOperatorDefinition(DyadicOperator.Equal,               "({0}).CompareTo({1}) == 0",   new[] { bbb, bii, bdd, bss }),
                new DyadicOperatorDefinition(DyadicOperator.GreaterThan,         "({0}).CompareTo({1}) > 0",    new[] { bii, bdd, bss }),
                new DyadicOperatorDefinition(DyadicOperator.GreaterThanOrEqual,  "({0}).CompareTo({1}) >= 0",   new[] { bii, bdd, bss }),
                new DyadicOperatorDefinition(DyadicOperator.LessThan,            "({0}).CompareTo({1}) < 0",    new[] { bii, bdd, bss }),
                new DyadicOperatorDefinition(DyadicOperator.LessThanOrEqual,     "({0}).CompareTo({1}) <= 0",   new[] { bii, bdd, bss }),
                new DyadicOperatorDefinition(DyadicOperator.NotEqual,            "({0}).CompareTo({1}) != 0",   new[] { bbb, bii, bdd, bss })
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
