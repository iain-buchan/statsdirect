using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using StatsDirect.Data;
using StatsDirect.UI;
using StatsDirect.Charting;

namespace StatsDirect.Templates
{
    [XmlRoot("filled-parameter")]
    [Serializable]
    public sealed class FilledParameter
    {
        private FilledParameter()
        {
        }

        public FilledParameter(FilledParameterDirection direction, object data)
        {
            Direction = direction;
            Data = data;
        }

        [XmlIgnore]
        public bool HasData => null != Data;

        [XmlIgnore]
        public bool IsInputParameter => FilledParameterDirection.Input == Direction;

        [XmlIgnore]
        public object Data { get; set; }

        [XmlElement("direction")]
        public FilledParameterDirection Direction { get; set; }

        [XmlIgnore]
        public bool AsBoolean => (bool)Data;

        [XmlIgnore]
        public ChartOptions AsChartOptions => (ChartOptions)Data;

        [XmlIgnore]
        public DataFrame AsDataFrame => (DataFrame)Data;

        [XmlIgnore]
        public DataFrame2D AsDataFrame2D => (DataFrame2D)Data;

        [XmlIgnore]
        public DateTime AsDate => (DateTime)Data;

        [XmlIgnore]
        public double AsDouble => (double)Data;

        [XmlIgnore]
        public int AsInt32 => (int)Data;

        [XmlIgnore]
        public Pane AsPane => (Pane)Data;

        [XmlIgnore]
        public PaneAndPosition AsPaneAndPosition => (PaneAndPosition)Data;

        [XmlIgnore]
        public ParameterBag AsParameterBag => (ParameterBag)Data;

        [XmlIgnore]
        public IList<ParameterBag> AsParameterBagList => (IList<ParameterBag>)Data;

        [XmlIgnore]
        public ScaleParameters AsScaleParameters => (ScaleParameters)Data;

        [XmlIgnore]
        public string AsString => (string)Data;

        [XmlIgnore]
        public IList<string> AsStringList => (IList<string>)Data;

        [XmlIgnore]
        public bool IsBoolean => Data is bool;

        [XmlIgnore]
        public bool IsDataFrame => Data is DataFrame;

        [XmlIgnore]
        public bool IsDouble => Data is double;

        [XmlIgnore]
        public bool IsInt32 => Data is int;

        [XmlIgnore]
        public bool IsParameterBag => Data is ParameterBag;

        [XmlIgnore]
        public bool IsParameterBagList => Data is IList<ParameterBag>;

        [XmlIgnore]
        public bool IsString => Data is string;

        internal FilledParameter CopyAndStripForRedo(bool shouldKeepData)
        {
            object copiedData;
            if (Data is IStripForRedo)
            {
                copiedData = ((IStripForRedo)Data).CopyAndStripForRedo(shouldKeepData);
                if (null == copiedData)
                    return null;
            }
            else
            {
                copiedData = Data;
            }

            return new FilledParameter {Direction = Direction, Data = copiedData};
        }

        internal void RefillForRedo(IRefillSource refillSource)
        {
            if (Data is IStripForRedo)
                ((IStripForRedo)Data).RefillForRedo(refillSource);
        }

        public override string ToString()
        {
            return "FP(" + Direction.ToString() + ", " + (null == Data ? "(null)" : Data.ToString()) + ")";
        }
    }

    public enum FilledParameterDirection
    {
        Output = 0,
        Input = 1,
        Default = 2
    }
}
