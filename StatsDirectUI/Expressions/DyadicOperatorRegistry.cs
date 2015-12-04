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
            definitions = new Dictionary<DyadicOperator, DyadicOperatorDefinition>();
            AddAll(new[]
                       {
                           new DyadicOperatorDefinition(DyadicOperator.Factorial, "SDMath.Factorial", true, new[] { new InOutDataTypeDefinition(DataType.Double, new[] { DataType.Double }) }),
                           new DyadicOperatorDefinition(DyadicOperator.Not, "!", true, new[] { new InOutDataTypeDefinition(DataType.Boolean, new[] { DataType.Boolean }) }),
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
