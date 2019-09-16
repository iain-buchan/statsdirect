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
    public abstract class FilledParameter
    {
        public static FilledParameter Input(object data) => new FilledObjectParameter(FilledParameterDirection.Input, data);
        public static FilledParameter Output(object data) => new FilledObjectParameter(FilledParameterDirection.Output, data);
        public static FilledParameter Default(object data) => new FilledObjectParameter(FilledParameterDirection.Default, data);
        public static FilledParameter Make(FilledParameterDirection direction, object data) => new FilledObjectParameter(direction, data);

        protected FilledParameter()
        {
        }

        protected FilledParameter(FilledParameterDirection direction)
        {
            Direction = direction;
        }

        [XmlIgnore]
        public virtual bool HasData { get; }

        [XmlIgnore]
        public bool IsInputParameter => FilledParameterDirection.Input == Direction;

        [XmlElement("direction")]
        public FilledParameterDirection Direction { get; set; }

        [XmlIgnore]
        public virtual bool AsBoolean { get; }

        [XmlIgnore]
        public virtual ChartOptions AsChartOptions { get; }

        [XmlIgnore]
        public virtual DataFrame AsDataFrame { get; }

        [XmlIgnore]
        public virtual DataFrame2D AsDataFrame2D { get; }

        [XmlIgnore]
        public virtual DateTime AsDate { get; }

        [XmlIgnore]
        public virtual double AsDouble { get; }

        [XmlIgnore]
        public virtual int AsInt32 { get; }

        [XmlIgnore]
        public virtual object AsObject { get; }

        [XmlIgnore]
        public virtual Pane AsPane { get; }

        [XmlIgnore]
        public virtual PaneAndPosition AsPaneAndPosition { get; }

        [XmlIgnore]
        public virtual ParameterBag AsParameterBag { get; }

        [XmlIgnore]
        public virtual IList<ParameterBag> AsParameterBagList { get; }

        [XmlIgnore]
        public virtual ScaleParameters AsScaleParameters { get; }

        [XmlIgnore]
        public virtual string AsString { get; }

        [XmlIgnore]
        public virtual IList<string> AsStringList { get; }

        [XmlIgnore]
        public virtual bool IsBoolean { get; }

        [XmlIgnore]
        public virtual bool IsDataFrame { get; }

        [XmlIgnore]
        public virtual bool IsDouble { get; }

        [XmlIgnore]
        public virtual bool IsInt32 { get; }

        [XmlIgnore]
        public virtual bool IsParameterBag { get; }

        [XmlIgnore]
        public virtual bool IsParameterBagList { get; }

        [XmlIgnore]
        public virtual bool IsString { get; }

        internal abstract FilledParameter CopyAndStripForRedo(bool shouldKeepData);

        internal abstract void RefillForRedo(IRefillSource refillSource);
    }
}
