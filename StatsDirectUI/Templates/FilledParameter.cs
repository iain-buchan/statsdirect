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
        public bool HasData
        {
            get { return null != Data; }
        }

        [XmlIgnore]
        public bool IsInputParameter
        {
            get { return FilledParameterDirection.Input == Direction; }
        }

        [XmlIgnore]
        public object Data { get; set; }

        [XmlElement("direction")]
        public FilledParameterDirection Direction { get; set; }

        [XmlIgnore]
        public bool AsBoolean
        {
            get { return (bool)Data; }
        }

        [XmlIgnore]
        public ChartOptions AsChartOptions
        {
            get { return (ChartOptions)Data; }
        }

        [XmlIgnore]
        public DataFrame AsDataFrame
        {
            get { return (DataFrame)Data; }
        }

        [XmlIgnore]
        public DataFrame2D AsDataFrame2D
        {
            get { return (DataFrame2D)Data; }
        }

        [XmlIgnore]
        public DateTime AsDate
        {
            get { return (DateTime)Data; }
        }

        [XmlIgnore]
        public double AsDouble
        {
            get { return (double)Data; }
        }

        [XmlIgnore]
        public int AsInt32
        {
            get { return (int)Data; }
        }

        [XmlIgnore]
        public Pane AsPane
        {
            get { return (Pane)Data; }
        }

        [XmlIgnore]
        public PaneAndPosition AsPaneAndPosition
        {
            get { return (PaneAndPosition)Data; }
        }

        [XmlIgnore]
        public ParameterBag AsParameterBag
        {
            get { return (ParameterBag)Data; }
        }

        [XmlIgnore]
        public IList<ParameterBag> AsParameterBagList
        {
            get { return (IList<ParameterBag>)Data; }
        }

        [XmlIgnore]
        public ScaleParameters AsScaleParameters
        {
            get { return (ScaleParameters)Data; }
        }

        [XmlIgnore]
        public string AsString
        {
            get { return (string)Data; }
        }

        [XmlIgnore]
        public IList<string> AsStringList
        {
            get { return (IList<string>)Data; }
        }

        [XmlIgnore]
        public bool IsBoolean
        {
            get { return Data is bool; }
        }

        [XmlIgnore]
        public bool IsDataFrame
        {
            get { return Data is DataFrame; }
        }

        [XmlIgnore]
        public bool IsDouble
        {
            get { return Data is double; }
        }

        [XmlIgnore]
        public bool IsInt32
        {
            get { return Data is int; }
        }

        [XmlIgnore]
        public bool IsParameterBag
        {
            get { return Data is ParameterBag; }
        }

        [XmlIgnore]
        public bool IsParameterBagList
        {
            get { return Data is IList<ParameterBag>; }
        }

        [XmlIgnore]
        public bool IsString
        {
            get { return Data is string; }
        }

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
    }

    public enum FilledParameterDirection
    {
        Output = 0,
        Input = 1
    }
}
