using System;
using System.Windows.Forms;
using StatsDirect.Charting;
using StatsDirect.Charting.Options;

namespace StatsDirect.UI
{
    /// <summary>
    /// A user interface for modifying bar options.
    /// </summary>
    public partial class ctlBarOptions : UserControl, IBarOptions
    {
        private ChartDefinition definition;
        private IBarOptions options;

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

        double IBarOptions.MaxBarWidth => throw new NotImplementedException();

        bool IBarOptions.RotateWhenStacked => throw new NotImplementedException();

        bool IBarOptions.Stacked => rdoTypeStacked100.Checked || rdoTypeStacked.Checked;

        bool IBarOptions.Stacked100Percent => rdoTypeStacked100.Checked;

        public IBarOptions OptionsFromForm() => this;

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

        private void rdoType_CheckedChanged(object? sender, EventArgs e)
        {
            RadioButton rb = (RadioButton)sender;
            if (rb.Checked)
                NoteTypeChanged();
        }

        private void NoteTypeChanged() => BarTypeChanged?.Invoke(this, EventArgs.Empty);
    }
}