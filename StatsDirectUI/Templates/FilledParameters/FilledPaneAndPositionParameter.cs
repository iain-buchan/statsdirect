using StatsDirect.Data;
using StatsDirect.UI;
using System;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class FilledPaneAndPositionParameter : FilledParameter
    {
        internal FilledPaneAndPositionParameter()
        {
        }

        public FilledPaneAndPositionParameter(FilledParameterDirection direction, PaneAndPosition data)
            : base(direction)
        {
            Direction = direction;
            Data = data;
        }

        public override bool HasData => true;

        public PaneAndPosition Data { get; set; }

        public override PaneAndPosition AsPaneAndPosition => Data;

        public override object AsObject => Data;

        internal override FilledParameter CopyAndStripForRedo(bool shouldKeepData)
        {
            object copiedData = Data.CopyAndStripForRedo(shouldKeepData);
            if (null == copiedData)
                return null;
            return FilledParameterFactory.Make(Direction, copiedData);
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
