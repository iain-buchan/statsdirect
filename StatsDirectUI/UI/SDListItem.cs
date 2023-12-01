using System;

namespace StatsDirect.UI
{
    public sealed class SDListItem
    {
        public SDListItem(string label, string operation)
        {
            Label = label;
            Operation = operation;
        }

        public string Label { get; }

        public string Operation { get; }

        public override string ToString() => Label;

        public override int GetHashCode() =>
            HashCode.Combine(Label, Operation);

        public override bool Equals(object? obj)
        {
            if (obj is not SDListItem other)
                return false;
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
