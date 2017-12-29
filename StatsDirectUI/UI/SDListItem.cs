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

        public string Label { get; }

        public string Operation { get; }

        public override string ToString()
        {
            return Label;
        }

        public override int GetHashCode()
        {
            // Beware of bitwise-XOR in this case; the two values may frequently be identical, leading to hash codes of 0 after an XOR.
            return Label.GetHashCode() + Operation.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is SDListItem))
                return false;
            SDListItem other = (SDListItem)obj;
            if (string.IsNullOrEmpty(Label))
                return string.IsNullOrEmpty(other.Label);
            if (!string.Equals(Label, other.Label))
                return false;
            if (string.IsNullOrEmpty(Operation))
                return string.IsNullOrEmpty(other.Operation);
            return string.Equals(Operation, other.Operation);
        }
    }
}
