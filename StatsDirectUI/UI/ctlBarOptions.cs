using System;
using System.Windows.Forms;
using StatsDirect.Charting;

namespace StatsDirect.UI
{
    /// <summary>
    /// A user interface for modifying bar options.
    /// </summary>
    public partial class ctlBarOptions : UserControl, IOkable
    {
        private ChartDefinition definition;
        private BarOptions options;

        public event EventHandler BarTypeChanged;

        public ctlBarOptions()
        {
            InitializeComponent();
        }

        public ChartDefinition ChartDefinition
        {
            set
            {
                definition = value;
                options = (BarOptions)definition.ChartOptions;
                FillFormFromOptions();
            }
        }

        void IOkable.OkClicked()
        {
            FillOptionsFromForm();
        }

        public void FillOptionsFromForm()
        {
            FillOptionsFromType();
        }

        private void FillOptionsFromType()
        {
            options.Stacked = rdoTypeStacked100.Checked || rdoTypeStacked.Checked;
            options.Stacked100Percent = rdoTypeStacked100.Checked;
        }

        public void FillFormFromOptions()
        {
            FillTypeFromOptions();
        }

        private void FillTypeFromOptions()
        {
            if (options.Stacked)
            {
                if (options.Stacked100Percent)
                {
                    rdoTypeStacked100.Checked = true;
                }
                else
                {
                    rdoTypeStacked.Checked = true;
                }
            }
            else
            {
                rdoTypeClustered.Checked = true;
            }
        }

        private void rdoType_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton rb = (RadioButton)sender;
            if (rb.Checked)
            {
                FillOptionsFromType();
                NoteTypeChanged();
            }
        }

        private void NoteTypeChanged()
        {
            if (null != BarTypeChanged)
                BarTypeChanged(this, EventArgs.Empty);
        }
    }
}