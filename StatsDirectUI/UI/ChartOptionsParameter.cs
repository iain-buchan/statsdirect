using StatsDirect.Templates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StatsDirect.UI
{
    /// <summary>
    /// A shim to allow chart options to be passed around as Parameters, and hence filled in by the UI
    /// </summary>
    internal class ChartOptionsParameter : Parameter
    {
        private readonly Charting.ChartDefinition chartDefinition;

        public ChartOptionsParameter(string name, Charting.ChartDefinition chartDefinition)
        {
            Name = name;
            this.chartDefinition = chartDefinition;
        }

        public Charting.ChartDefinition ChartDefinition
        {
            get { return chartDefinition; }
        }

        public override ParameterType Type
        {
            get { return ParameterType.Custom; }
        }
    }
}
