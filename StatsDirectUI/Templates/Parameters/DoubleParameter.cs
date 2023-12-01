using System;
using System.Xml.Serialization;

namespace StatsDirect.Templates
{
    [Serializable]
    public sealed class DoubleParameter: RangeParameter, IDefaultParameter<double>
    {
        [XmlElement(ElementName = "default-value")]
        public Expression? DefaultValueExpression { get; set; }

        [XmlElement(ElementName = "minimum-value")]
        public Expression? MinimumValueExpression { get; set; }

        [XmlElement(ElementName = "maximum-value")]
        public Expression? MaximumValueExpression { get; set; }

        public double? DefaultValue(ITemplateProcessor processor, ParameterBag parameters)
        {
            if (null == DefaultValueExpression || null == DefaultValueExpression.Body)
                return null;
            object o = processor.Evaluate(DefaultValueExpression, parameters);
            return o is int i
                ? i
                : (double)o;
        }

        /// <summary>
        /// true iff the parameter defines a default.
        /// </summary>
        public bool HasDefaultValue => null != DefaultValueExpression;

        public double MinimumValue(ITemplateProcessor processor, ParameterBag parameters)
        {
            if (null == MinimumValueExpression || null == MinimumValueExpression.Body)
                return double.MinValue;
            object o = processor.Evaluate(MinimumValueExpression, parameters);
            return o is int i
                ? i
                : (double)o;
        }

        public double MaximumValue(ITemplateProcessor processor, ParameterBag parameters)
        {
            if (null == MaximumValueExpression || null == MaximumValueExpression.Body)
                return double.MaxValue;
            object o = processor.Evaluate(MaximumValueExpression, parameters);
            return o is int i
                ? i
                : (double)o;
        }

        public override ParameterBag AllDefaults(ITemplateProcessor processor, ParameterBag context)
        {
            double? defaultValue = DefaultValue(processor, context);
            if (defaultValue.HasValue)
                return new ParameterBag().AddDefault(Name, defaultValue.Value);
            return new ParameterBag();
        }

        public override void Accept(IParameterVisitor visitor) => visitor.Visit(this);
    }
}
