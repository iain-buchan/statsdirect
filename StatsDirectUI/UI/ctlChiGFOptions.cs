using System;
using System.Windows.Forms;
using StatsDirect.Utilities;

namespace StatsDirect.UI
{
    public partial class ctlChiGFOptions : UserControl, IOkable
    {
        private readonly Builtins.ChiSquareGoodnessOfFitOptions options;
        private bool useProportionOfN;
        private int listIndexAtFocus;
        private bool valueIsDirty;

        public ctlChiGFOptions(Builtins.ChiSquareGoodnessOfFitOptions options)
        {
            InitializeComponent();
            this.options = options;
            SetFormFromOptions();
        }

        private void SetFormFromOptions()
        {
            txtDegreesOfFreedom.Text = options.Df.ToString();
            lblCategories.Text = "Categories = " + options.Categories.ToString();
            lblN.Text = "n = " + options.N.ToString();
            for (int i = 0; i < options.X.Count; i++)
            {
                ListViewItem item = new ListViewItem(new[] { options.X[i], options.Xn[i].ToString(), options.Xe[i].ToString() });
                lstFrequencies.Items.Add(item);
            }
        }

        private void SetOptionsFromForm()
        {
            options.Df = Utilities.Parsing.Cint_Txt(txtDegreesOfFreedom.Text);
            for (int i = 0; i < options.Xe.Count; i++)
            {
                options.Xe[i] = Utilities.Parsing.Cdbl_Txt(lstFrequencies.Items[i].SubItems[2].Text);
            }
        }

        private void lstFrequencies_MouseUp(object sender, MouseEventArgs e)
        {
            SetValueFromList();
        }

        private void SetValueFromList()
        {
            if (lstFrequencies.SelectedIndices.Count >= 1)
            {
                int index = lstFrequencies.SelectedIndices[0];
                double rawValue = Parsing.Cdbl_Txt(lstFrequencies.Items[index].SubItems[2].Text);
                if (useProportionOfN)
                    rawValue /= options.N;
                txtValue.Text = rawValue.ToString();
                txtValue.SelectAll();
                txtValue.Select();
            }
        }

        private void rdoExpected_Click(object sender, EventArgs e)
        {
            useProportionOfN = !rdoExpected.Checked;
            rdoN.Checked = useProportionOfN;
            SetValueFromList();
        }

        private void rdoN_Click(object sender, EventArgs e)
        {
            useProportionOfN = rdoN.Checked;
            rdoExpected.Checked = !useProportionOfN;
            SetValueFromList();
        }

        private void lstFrequencies_KeyPress(object sender, KeyPressEventArgs e)
        {
            SetValueFromList();
        }

        private void txtValue_Enter(object sender, EventArgs e)
        {
            listIndexAtFocus = lstFrequencies.SelectedIndices[0];
        }

        private void txtValue_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (13 == e.KeyChar)
            {
                double ex = Utilities.Parsing.Cdbl_Txt(txtValue.Text);
                if (ex <= 0)
                {
                    txtValue.Text = "";
                }
                else
                {
                    if (useProportionOfN && valueIsDirty)
                    {
                        ex *= options.N;
                    }
                    valueIsDirty = false;
                    lstFrequencies.Items[listIndexAtFocus].SubItems[2].Text = ex.ToString();
                    if (listIndexAtFocus + 1 < lstFrequencies.Items.Count)
                    {
                        listIndexAtFocus++;
                        lstFrequencies.Items[listIndexAtFocus].Selected = true;
                        SetValueFromList();
                    }
                }
                e.Handled = true;
            }
            else
            {
                valueIsDirty = true;
            }
        }

        #region IOkable Members

        void IOkable.OkClicked()
        {
            SetOptionsFromForm();
        }

        #endregion
    }
}