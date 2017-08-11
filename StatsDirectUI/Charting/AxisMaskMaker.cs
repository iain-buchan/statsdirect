using System;
using System.Collections.Generic;
using StatsDirect.Builtins;
using StatsDirect.Charting.Renderer;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.Charting
{
    public class AxisMaskMaker : IAxisScaleVisitor
    {
        private IAxisMasker axisMasker;
        public string AxisMaskFor(IAxisScale axisScale)
        {
            axisScale.Accept(this);
            return axisMasker.AxisMask(axisScale);
        }

        public void Visit(CategoryAxisScale _)
        {
            axisMasker = new CategoryAxisMasker();
        }

        public void Visit(DateAxisScale _)
        {
            axisMasker = new DateAxisMasker();
        }

        public void Visit(LinearAxisScale _)
        {
            axisMasker = new LinearAxisMasker();
        }

        public void Visit(Log10AxisScale _)
        {
            axisMasker = new Log10AxisMasker();
        }

        public void Visit(Log2AxisScale _)
        {
            axisMasker = new Log2AxisMasker();
        }

        public void Visit(NewLinearAxisScale _)
        {
            axisMasker = new LinearAxisMasker();
        }

        public void Visit(TalbotLinHanrahanAxisScale _)
        {
            axisMasker = new LinearAxisMasker();
        }
    }
}
