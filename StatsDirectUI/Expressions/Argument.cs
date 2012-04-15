namespace StatsDirect.Expressions
{
    public class Argument
    {
        public string ExplicitParameterName { get; set; }
        public string BuiltExpression { get; set; }

        public Argument()
        {
        }

        public Argument(string builtExpression)
        {
            BuiltExpression = builtExpression;
        }

        public Argument(string explicitParameterName, string builtExpression)
        {
            ExplicitParameterName = explicitParameterName;
            BuiltExpression = builtExpression;
        }
    }
}
