using System;
using System.Windows.Forms;
using StatsDirect.Builtins;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.UI
{
    public partial class ctlScores : UserControl, IFillParameterBag
    {
        private int count1;
        private int count2;

        protected ctlScores()
        {
            InitializeComponent();
        }
        public ctlScores(ScoresOptions options)
            : this()
        {
            SetFormFromOptions(options);
        }

        public ctlScores(ParameterBag parameters)
            : this()
        {
            gridScores.Columns[0].HeaderText = parameters["c1"].AsDataFrame.Variables[0].Title;
            gridScores.Columns[1].HeaderText = parameters["c2"].AsDataFrame.Variables[0].Title;
            count1 = parameters["ycats"].AsInt32;
            count2 = parameters["xcats"].AsInt32;
            double[] values1 = parameters.ContainsKey("values1") && ((double[])parameters["values1"].AsObject).Length == count1 ? (double[])parameters["values1"].AsObject : null;
            double[] values2 = parameters.ContainsKey("values2") && ((double[])parameters["values2"].AsObject).Length == count1 ? (double[])parameters["values2"].AsObject : null;
            gridScores.RowCount = Math.Max(count1, count2);
            for (int i = 0; i < count1; i++)
                gridScores[0, i].Value = (null == values1 ? i + 1 : values1[i]).ToString();
            for (int i = 0; i < count2; i++)
                gridScores[1, i].Value = (null == values2 ? i + 1 : values2[i]).ToString();
        }

        private void SetFormFromOptions(ScoresOptions options)
        {
            gridScores.Columns[0].HeaderText = options.Title1;
            gridScores.Columns[1].HeaderText = options.Title2;
            count1 = options.Values1.Count;
            count2 = options.Values2.Count;
            gridScores.RowCount = Math.Max(count1, count2);
            for (int i = 0; i < count1; i++)
                gridScores[0, i].Value = options.Values1[i].ToString();
            for (int i = 0; i < count2; i++)
                gridScores[1, i].Value = options.Values2[i].ToString();
        }

        public Control Fill(ParameterBag outputParameters, bool doValidation)
        {
            double[] values1 = new double[count1];
            double[] values2 = new double[count2];
            for (int i = 0; i < count1; i++)
                values1[i] = Parsing.Cdbl_Txt((string)gridScores[0, i].Value);
            for (int i = 0; i < count2; i++)
                values2[i] = Parsing.Cdbl_Txt((string)gridScores[1, i].Value);
            outputParameters.AddInput("values1", values1);
            outputParameters.AddInput("values2", values2);
            return null;
        }
    }
}
