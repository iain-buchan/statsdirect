namespace StatsDirect.UI
{
    class Conversion
    {
        public string Expression { get; }
        public string ResultUnit { get; }
        public string Label { get; }

        public Conversion(string expression, string resultUnit, string label)
        {
            Expression = expression;
            ResultUnit = resultUnit;
            Label = label;
        }

        public override string ToString()
        {
            return Label;
        }
    }
}
