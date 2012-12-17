namespace StatsDirect.UI
{
    class Conversion
    {
        public string Expression { get; private set; }
        public string ResultUnit { get; private set; }
        public string Label { get; private set; }

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
