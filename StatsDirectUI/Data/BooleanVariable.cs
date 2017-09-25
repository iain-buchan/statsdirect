using System;
using StatsDirect.Numerics;

namespace StatsDirect.Data
{
    [Serializable]
    public class BooleanVariable : GenericVariable<bool>
    {
        public BooleanVariable()
            : base()
        {
        }

        public BooleanVariable(bool[] data)
            : base(data)
        {
        }

        public BooleanVariable(bool[] data, string title)
            : base(data, title)
        {
        }

        public BooleanVariable(int length, string title)
            : base(length, title)
        {
        }

        public override object CopyAndStripForRedo(bool shouldKeepData)
        {
            BooleanVariable copy = new BooleanVariable();
            CopyAndStripForRedoInto(copy, shouldKeepData);
            return copy;
        }

        public override void Accept(IVariableVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
