using System;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class ctlROCCutoff : UserControl, IOkable
    {
        private readonly double inc;
        private double add;
        private double lastcut;
        private readonly Charting.ROCSeriesRecord originalRecord;
        private Charting.ROCSeriesRecord currentRecord;
        private readonly double weight;
        private readonly string titleSuffix;

        public Charting.ROCSeriesRecord CurrentRecord => currentRecord;

        public ctlROCCutoff(Charting.ROCSeriesRecord seriesRecord, double weight, string titleSuffix)
        {
            InitializeComponent();
            originalRecord = seriesRecord;
            currentRecord = seriesRecord.Clone();
            this.weight = weight;
            this.titleSuffix = titleSuffix;
            inc = seriesRecord.cutoff / 300.0;
        }

        private void PopulateFormFromData(Charting.ROCSeriesRecord record)
        {
            txtA.Text = record.a.ToString();
            txtB.Text = record.b.ToString();
            txtC.Text = record.c.ToString();
            txtD.Text = record.d.ToString();
            txtSensitivity.Text = SdApplication.SoleInstance.RoundU(record.sens);
            txtSpecificity.Text = SdApplication.SoleInstance.RoundU(record.spec);
            txtCutoff.Text = SdApplication.SoleInstance.RoundU(record.cutoff);
            double ppv = record.a / ((double)record.a + record.b);
            double npv = record.d / ((double)record.d + record.c);
            txtPositive.Text = Numerics.Constant.MISSING == ppv ? "*" : SdApplication.SoleInstance.RoundU(ppv);
            txtNegative.Text = Numerics.Constant.MISSING == npv ? "*" : SdApplication.SoleInstance.RoundU(npv);
            lastcut = record.cutoff;
        }

        private void PopulateDataFromForm(Charting.ROCSeriesRecord record)
        {
            record.cutoff = Utilities.Parsing.Cdbl_Txt(txtCutoff.Text);
            record.a = Utilities.Parsing.Cint_Txt(txtA.Text);
            record.b = Utilities.Parsing.Cint_Txt(txtB.Text);
            record.c = Utilities.Parsing.Cint_Txt(txtC.Text);
            record.d = Utilities.Parsing.Cint_Txt(txtD.Text);
            record.sens = Utilities.Parsing.Cdbl_Txt(txtSensitivity.Text);
            record.spec = Utilities.Parsing.Cdbl_Txt(txtSpecificity.Text);
        }

        private void ReCutAndDisplay()
        {
            currentRecord.ReCut();
            PopulateFormFromData(currentRecord);
        }

        private void btnUp_MouseDown(object sender, MouseEventArgs e)
        {
            StartSpinUp();
        }

        private void btnUp_MouseUp(object sender, MouseEventArgs e)
        {
            StopSpinUp();
        }

        private void btnDown_MouseDown(object sender, MouseEventArgs e)
        {
            StartSpinDown();
        }

        private void btnDown_MouseUp(object sender, MouseEventArgs e)
        {
            StopSpinDown();
        }

        private void cmdReset_Click(object sender, EventArgs e)
        {
            currentRecord = originalRecord.Clone();
            PopulateFormFromData(currentRecord);
        }

        private void txtCutoff_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (13 == e.KeyChar)
            {
                currentRecord.cutoff = Utilities.Parsing.Cdbl_Txt(txtCutoff.Text);
                if (currentRecord.cutoff != lastcut)
                {
                    ReCutAndDisplay(); // Sets LASTCUT, so we don't need to
                }
                e.Handled = true;
           }
        }

        private void frmROCCutoff_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Up:
                    StartSpinUp();
                    e.Handled = true;
                    break;
                case Keys.Down:
                    StartSpinDown();
                    e.Handled = true;
                    break;
            }
        }

        private void frmROCCutoff_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Up:
                    StopSpinUp();
                    e.Handled = true;
                    break;
                case Keys.Down:
                    StopSpinDown();
                    e.Handled = true;
                    break;
            }
        }

        private void StartSpinDown()
        {
            add = -inc;
            timer1.Enabled = true;
        }

        private void StartSpinUp()
        {
            add = inc;
            timer1.Enabled = true;
        }

        private void StopSpinDown()
        {
            timer1.Enabled = false;
        }

        private void StopSpinUp()
        {
            timer1.Enabled = false;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            currentRecord.cutoff += add;
            if (currentRecord.cutoff != lastcut)
            {
                ReCutAndDisplay(); // Sets LASTCUT, so we don't have to
            }
        }

        private void ctlROCCutoff_Load(object sender, EventArgs e)
        {
            Text += titleSuffix;
            lblOptimum.Text = "For optimum, sensitivity:specificity weighting = " + weight.ToString() + ":1";
            string Q = string.Empty;
            switch (originalRecord.comp)
            {
                case Charting.ComparisonValue.GE:
                    Q = ">=";
                    break;
                case Charting.ComparisonValue.GT:
                    Q = ">";
                    break;
                case Charting.ComparisonValue.LE:
                    Q = "<=";
                    break;
                case Charting.ComparisonValue.LT:
                    Q = "<";
                    break;
            }
            lblCutoff.Text += " " + Q;
            PopulateFormFromData(currentRecord);
        }

        public void OkClicked()
        {
            PopulateDataFromForm(originalRecord);
        }
    }
}