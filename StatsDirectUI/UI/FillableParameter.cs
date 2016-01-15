using StatsDirect.Templates;

namespace StatsDirect.UI
{
    /// <summary>
    /// A shim to allow fillables to be passed around as Parameters, and hence filled in by the UI.
    /// </summary>
    public class FillableParameter : Parameter
    {
        private readonly IFillable fillable;

        public FillableParameter(string name, IFillable fillable)
        {
            Name = name;
            this.fillable = fillable;
        }

        public IFillable Fillable
        {
            get { return fillable; }
        }

        public override InputDuringStep RequiresInputGiven(ParameterBag parameters)
        {
            return (MustRequest || null != Name && null != parameters && !parameters.ContainsKey(Name)) ? InputDuringStep.Always : InputDuringStep.Never;
        }

        public override void Accept(IParameterVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
