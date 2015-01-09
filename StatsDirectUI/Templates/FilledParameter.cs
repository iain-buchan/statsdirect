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
        private bool isInputParameter;
        private object data;

        private FilledParameter()
        {
        }

        public FilledParameter(bool isInputParameter, object data)
        {
            this.isInputParameter = isInputParameter;
            this.data = data;
        }

        [XmlIgnore]
        public bool HasData
        {
            get { return null != data; }
        }

        [XmlElement("is-input")]
        public bool IsInputParameter
        {
            get { return isInputParameter; }
        }

        [XmlIgnore]
        public object Data
        {
            get { return data; }
            set { data = value; }
        }

        [XmlIgnore]
        public bool AsBoolean
        {
            get { return (bool)data; }
        }

        [XmlIgnore]
        public ChartOptions AsChartOptions
        {
            get { return (ChartOptions)data; }
        }

        [XmlIgnore]
        public DataFrame AsDataFrame
        {
            get { return (DataFrame)data; }
        }

        [XmlIgnore]
        public DataFrame2D AsDataFrame2D
        {
            get { return (DataFrame2D)data; }
        }

        [XmlIgnore]
        public DateTime AsDate
        {
            get { return (DateTime)data; }
        }

        [XmlIgnore]
        public double AsDouble
        {
            get { return (double)data; }
        }

        [XmlIgnore]
        public int AsInt32
        {
            get { return (int)data; }
        }

        [XmlIgnore]
        public Pane AsPane
        {
            get { return (Pane)data; }
        }

        [XmlIgnore]
        public PaneAndPosition AsPaneAndPosition
        {
            get { return (PaneAndPosition)data; }
        }

        [XmlIgnore]
        public ParameterBag AsParameterBag
        {
            get { return (ParameterBag)data; }
        }

        [XmlIgnore]
        public IList<ParameterBag> AsParameterBagList
        {
            get { return (IList<ParameterBag>)data; }
        }

        [XmlIgnore]
        public ScaleParameters AsScaleParameters
        {
            get { return (ScaleParameters)data; }
        }

        [XmlIgnore]
        public string AsString
        {
            get { return (string)data; }
        }

        [XmlIgnore]
        public IList<string> AsStringList
        {
            get { return (IList<string>)data; }
        }

        [XmlIgnore]
        public bool IsBoolean
        {
            get { return data is Boolean; }
        }

        [XmlIgnore]
        public bool IsDataFrame
        {
            get { return data is DataFrame; }
        }

        [XmlIgnore]
        public bool IsDouble
        {
            get { return data is Double; }
        }

        [XmlIgnore]
        public bool IsInt32
        {
            get { return data is Int32; }
        }

        [XmlIgnore]
        public bool IsParameterBag
        {
            get { return data is ParameterBag; }
        }

        [XmlIgnore]
        public bool IsParameterBagList
        {
            get { return data is IList<ParameterBag>; }
        }

        [XmlIgnore]
        public bool IsString
        {
            get { return data is String; }
        }

        internal FilledParameter CopyAndStripForRedo(bool shouldKeepData)
        {
            object copiedData;
            if (data is IStripForRedo)
            {
                copiedData = ((IStripForRedo)data).CopyAndStripForRedo(shouldKeepData);
                if (null == copiedData)
                    return null;
            }
            else
            {
                copiedData = data;
            }

            FilledParameter copy = new FilledParameter {isInputParameter = isInputParameter, data = copiedData};
            return copy;
        }

        internal void RefillForRedo(IRefillSource refillSource)
        {
            if (data is IStripForRedo)
            {
                ((IStripForRedo)data).RefillForRedo(refillSource);
            }
        }
    }
}
