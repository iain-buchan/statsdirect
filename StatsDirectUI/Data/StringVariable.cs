using System;

namespace StatsDirect.Data
{
    [Serializable]
    public sealed class StringVariable : GenericVariable<string>
    {
        public StringVariable()
            : base()
        {
        }

        public StringVariable(string[] data)
            : base(data)
        {
        }

        public StringVariable(string[] data, string title)
            : base(data, title)
        {
        }

        public StringVariable(int length, string title)
            : base(length, title)
        {
        }

        public override object CopyAndStripForRedo(bool shouldKeepData)
        {
            StringVariable copy = new StringVariable();
            CopyAndStripForRedoInto(copy, shouldKeepData);
            return copy;
        }

        public override void Accept(IVariableVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
