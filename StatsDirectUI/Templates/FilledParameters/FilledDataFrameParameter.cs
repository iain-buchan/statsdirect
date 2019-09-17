using StatsDirect.Data;
using System;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class FilledDataFrameParameter : FilledParameter
    {
        internal FilledDataFrameParameter()
        {
        }

        public FilledDataFrameParameter(FilledParameterDirection direction, DataFrame data)
            : base(direction)
        {
            Direction = direction;
            Data = data;
        }

        public override bool HasData => true;

        public DataFrame Data { get; set; }

        public override DataFrame AsDataFrame => Data;

        public override object AsObject => Data;

        public override bool IsDataFrame => true;

        internal override FilledParameter CopyAndStripForRedo(bool shouldKeepData)
        {
            DataFrame copiedData = null == Data ? null : (DataFrame)Data.CopyAndStripForRedo(shouldKeepData);
            return null == copiedData? null : FilledParameterFactory.Make(Direction, copiedData);
        }

        internal override void RefillForRedo(IRefillSource refillSource)
        {
            base.RefillForRedo(refillSource);
            if (Data is IStripForRedo)
                ((IStripForRedo)Data).RefillForRedo(refillSource);
        }

        public override string ToString() => $"FP({Direction}, {Data})";

        public override void Accept(IFilledParameterVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
