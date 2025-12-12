using System.Collections.Generic;

namespace StatsDirect.Expressions
{
    /// <remarks>Singleton.</remarks>
    public class MonadicOperatorRegistry
    {
        private readonly Dictionary<MonadicOperator, MonadicOperatorDefinition> definitions;

        private static MonadicOperatorRegistry soleInstance;

        public static MonadicOperatorRegistry SoleInstance => soleInstance ?? (soleInstance = new MonadicOperatorRegistry());

        private MonadicOperatorRegistry()
        {
            // A few useful parameters that can be re-used
            definitions = new Dictionary<MonadicOperator, MonadicOperatorDefinition>();
            AddAll(new[]
            {
                new MonadicOperatorDefinition(MonadicOperator.Factorial, "SDMath.Factorial", new[] { new InOutDataTypeDefinition(DataType.Double, new[] { DataType.Double }) }),
                new MonadicOperatorDefinition(MonadicOperator.Not, "!", new[] { new InOutDataTypeDefinition(DataType.Boolean, new[] { DataType.Boolean }) }),
            });

        }

        private void AddAll(IEnumerable<MonadicOperatorDefinition> definitions)
        {
            foreach (MonadicOperatorDefinition definition in definitions)
                this.definitions.Add(definition.Operator, definition);
        }

        public MonadicOperatorDefinition DefinitionFor(MonadicOperator op)
        {
            definitions.TryGetValue(op, out MonadicOperatorDefinition definition);
            return definition;
        }
    }
}
