using System;
using System.Collections.Generic;
using System.Windows.Forms;

using StatsDirect.Numerics;
using StatsDirect.Utilities;
using StatsDirect.Data;
using StatsDirect.Templates;

namespace StatsDirect.UI
{
    public partial class ctlTextToNumbers : UserControl, IFillParameterBag
    {
        private readonly Dictionary<string, int> textsToNumbers = new Dictionary<string, int>();
        private DataFrame data;
        private readonly ParameterBag context;

        public ctlTextToNumbers(ParameterBag context)
        {
            this.context = context;
            InitializeComponent();
            FillFormFromOptions();
        }

        private void FillOptionsFromForm()
        {
            textsToNumbers.Clear();
            for (int i = 0; i < gridNumbers.Rows.Count; i++)
            {
                string text = gridNumbers.Rows[i].Cells[0].Value.ToString();
                object val = gridNumbers.Rows[i].Cells[1].Value;
                if (null != val)
                {
                    int value = Parsing.Cint_Txt((string)val);
                    textsToNumbers[text] = value;
                }
            }
        }

        private void FillFormFromOptions()
        {
            textsToNumbers.Clear();
            MakeTable(context, !chkIgnoreTitle.Checked);
            SortedDictionary<int, string> numbersToTexts = new SortedDictionary<int, string>();
            foreach (KeyValuePair<string, int> pair in textsToNumbers)
                numbersToTexts.Add(pair.Value, pair.Key);
            gridNumbers.Rows.Clear();
            gridNumbers.Rows.Add(numbersToTexts.Count);
            int row = 0;
            foreach (KeyValuePair<int, string> pair in numbersToTexts)
            {
                gridNumbers.Rows[row].Cells[0].Value = pair.Value;
                gridNumbers.Rows[row].Cells[1].Value = pair.Key.ToString();
                row++;
            }
            gridNumbers.CurrentCell = gridNumbers.Rows[0].Cells[1];
        }

        private void ProduceOutput(ParameterBag outputParameters)
        {
            DataFrame outputFrame = new DataFrame();
            for (int c = 0; c < data.VariableCount; c++)
            {
                StringVariable v = data.Variables[c].AsStringVariable;
                DoubleVariable outputVariable = new DoubleVariable(v.Length, v.Title);
                outputFrame.Variables.Add(outputVariable);
                for (int r = 0; r < v.Length; r++)
                {
                    string tmp = v.Data[r];
                    int value;
                    if (textsToNumbers.TryGetValue(tmp, out value))
                        outputVariable.Data[r] = value;
                    else
                        outputVariable.Data[r] = Constant.MISSING;
                }
            }
            outputParameters.AddOutput("output", outputFrame);
        }

        private void MakeTable(ParameterBag parameters, bool useTitle)
        {
            data = parameters["data"].AsDataFrame;
            int nextValue = 1;
            for (int c = 0; c < data.VariableCount; c++)
            {
                StringVariable v = data.Variables[c].AsStringVariable;
                // get text codes and assign number codes
                foreach (string tmp in v.Data)
                    MaybeAssignCode(tmp, ref nextValue);
                if (useTitle && !string.IsNullOrEmpty(v.Title))
                    MaybeAssignCode(v.Title, ref nextValue);
            }
        }

        private void MaybeAssignCode(string tmp, ref int nextValue)
        {
            if ((!Formatting.ASTERISK.Equals(tmp)) && !string.IsNullOrEmpty(tmp))
                if (!textsToNumbers.ContainsKey(tmp))
                    textsToNumbers.Add(tmp, nextValue++);
        }

        public Control Fill(ParameterBag outputParameters)
        {
            FillOptionsFromForm();
            ProduceOutput(outputParameters);
            return null;
        }

        private void chkIgnoreTitle_CheckedChanged(object sender, EventArgs e)
        {
            FillFormFromOptions();
        }
    }
}
