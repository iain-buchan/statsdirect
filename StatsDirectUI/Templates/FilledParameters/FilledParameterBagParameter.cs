using StatsDirect.Data;
using System;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class FilledParameterBagParameter : FilledParameter
    {
        internal FilledParameterBagParameter()
        {
        }

        public FilledParameterBagParameter(FilledParameterDirection direction, ParameterBag data)
            : base(direction)
        {
            Direction = direction;
            Data = data;
        }

        public override bool HasData => true;

        public ParameterBag Data { get; }

        public override ParameterBag AsParameterBag => Data;

        public override object AsObject => Data;

        public override bool IsParameterBag => true;

        internal override FilledParameter CopyAndStripForRedo(bool shouldKeepData)
        {
            ParameterBag copiedData = null == Data ? null : Data.CopyAndStripForRedo(shouldKeepData);
            return null == copiedData? null : FilledParameterFactory.Make(Direction, copiedData);
        }

        internal override void RefillForRedo(IRefillSource refillSource)
        {
            base.RefillForRedo(refillSource);
            if (null != Data)
                Data.RefillForRedo(refillSource);
        }

        public override string ToString() => $"FP({Direction}, {Data})";

        public override void Accept(IFilledParameterVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
