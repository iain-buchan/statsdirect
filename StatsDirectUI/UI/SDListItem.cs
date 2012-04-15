namespace StatsDirect.UI
{
    public sealed class SDListItem
    {
        public SDListItem()
        {
            // Nothing else required
        }

        public SDListItem(string label, string operation)
        {
            Label = label;
            Operation = operation;
        }

        public string Label { get; set; }

        public string Operation { get; set; }

        public override string ToString()
        {
            return Label;
        }
    }
}
