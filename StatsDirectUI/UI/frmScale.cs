using System;
using System.Windows.Forms;
using StatsDirect.Charting;
using StatsDirect.Utilities;

namespace StatsDirect.UI
{
    public partial class frmScale : Form
    {
        private readonly Templates.ScaleParameters scaleParameters;
        private bool cancelled = true; // To catch the case where it was closed by the X at the top

        public frmScale(Templates.ScaleParameters scaleParameters)
        {
            InitializeComponent();
            this.scaleParameters = scaleParameters;
            FillFormFromParameters();
        }

        public bool Cancelled
        {
            get { return cancelled; }
        }

        private void FillFormFromParameters()
        {
            chkRequestLimits.Checked = ChartRenderer.DefaultRequestScaleLimits;

            lblXLowerLimit.Enabled = scaleParameters.X.ShouldCheck;
            lblXUpperLimit.Enabled = scaleParameters.X.ShouldCheck;
            txtXLowerLimit.Enabled = scaleParameters.X.ShouldCheck;
            txtXUpperLimit.Enabled = scaleParameters.X.ShouldCheck;
            lblXScale.Enabled = scaleParameters.X.ShouldCheck;
            lstXScale.Enabled = scaleParameters.X.ShouldCheck;
            grpXAxis.Enabled = scaleParameters.X.ShouldCheck;

            lblYLowerLimit.Enabled = scaleParameters.Y.ShouldCheck;
            lblYUpperLimit.Enabled = scaleParameters.Y.ShouldCheck;
            txtYLowerLimit.Enabled = scaleParameters.Y.ShouldCheck;
            txtYUpperLimit.Enabled = scaleParameters.Y.ShouldCheck;
            lblYScale.Enabled = scaleParameters.Y.ShouldCheck;
            lstYScale.Enabled = scaleParameters.Y.ShouldCheck;
            grpYAxis.Enabled = scaleParameters.Y.ShouldCheck;

            if (scaleParameters.X.ShouldCheck)
            {
                txtXUpperLimit.Text = scaleParameters.X.Max.ToString();
                txtXLowerLimit.Text = scaleParameters.X.Min.ToString();
            }

            if (scaleParameters.Y.ShouldCheck)
            {
                txtYUpperLimit.Text = scaleParameters.Y.Max.ToString();
                txtYLowerLimit.Text = scaleParameters.Y.Min.ToString();
            }
        }

        private void FillParametersFromForm()
        {
            if (scaleParameters.X.ShouldCheck)
            {
                scaleParameters.X.Max = Parsing.Cdbl_Txt(txtXUpperLimit.Text);
                scaleParameters.X.Min = Parsing.Cdbl_Txt(txtXLowerLimit.Text);
            }

            if (scaleParameters.Y.ShouldCheck)
            {
                scaleParameters.Y.Max = Parsing.Cdbl_Txt(txtYUpperLimit.Text);
                scaleParameters.Y.Min = Parsing.Cdbl_Txt(txtYLowerLimit.Text);
            }
        }

        private void txtYUpperLimit_TextChanged(object sender, EventArgs e)
        {
            RescaleYAxis();
        }

        private void txtYLowerLimit_TextChanged(object sender, EventArgs e)
        {
            RescaleYAxis();
        }

        private void txtXUpperLimit_TextChanged(object sender, EventArgs e)
        {
            RescaleXAxis();
        }

        private void txtXLowerLimit_TextChanged(object sender, EventArgs e)
        {
            RescaleXAxis();
        }

        private void RescaleYAxis()
        {
            double min = Parsing.Cdbl_Txt(txtYLowerLimit.Text);
            double max = Parsing.Cdbl_Txt(txtYUpperLimit.Text);
            int ydiv;
            double zmin = 0.0;
            double zint = 0.0;
            int jmpDiv;
            AxisScaler.Q_Axis (ref min, ref max, out ydiv, ref zmin, ref zint, out jmpDiv, Templates.ScaleType.Linear);
            if (ydiv > 0)
            {
                string msk = AxisScaler.AxisMask(zint, zmin, ydiv, jmpDiv, Templates.ScaleType.Linear);
                lstYScale.Items.Clear();
                for (int c = 0; c <= ydiv; c += jmpDiv)
                {
                    ListViewItem item = new ListViewItem(new[] { (zmin + c * zint).ToString(msk) });
                    lstYScale.Items.Add(item);
                }
            }
        }

        private void RescaleXAxis()
        {
            double min = Parsing.Cdbl_Txt(txtXLowerLimit.Text);
            double max = Parsing.Cdbl_Txt(txtXUpperLimit.Text);
            int ydiv;
            double zmin = 0.0;
            double zint = 0.0;
            int jmpDiv;
            AxisScaler.Q_Axis(ref min, ref max, out ydiv, ref zmin, ref zint, out jmpDiv, Templates.ScaleType.Linear);
            if (ydiv > 0)
            {
                string msk = AxisScaler.AxisMask(zint, zmin, ydiv, jmpDiv, Templates.ScaleType.Linear);
                lstXScale.Items.Clear();
                for (int c = 0; c <= ydiv; c += jmpDiv)
                {
                    ListViewItem item = new ListViewItem(new[] { (zmin + c * zint).ToString(msk) });
                    lstXScale.Items.Add(item);
                }
            }
        }

        private void cmdOk_Click(object sender, EventArgs e)
        {
            FillParametersFromForm();
            cancelled = false;
            Close();
        }
    }
}
