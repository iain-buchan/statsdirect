using System;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class frmSummaryStatistics : Form
    {
        private readonly Builtins.SummaryStatisticsOptions summaryStatisticsOptions;

        public frmSummaryStatistics(Builtins.SummaryStatisticsOptions summaryStatisticsOptions)
        {
            InitializeComponent();
            this.summaryStatisticsOptions = summaryStatisticsOptions;
            SetFormFromOptions();
        }

        private void SetFormFromOptions()
        {
            txtSummary.Text = summaryStatisticsOptions.Text;
        }

        private void cmdOk_Click(object? sender, EventArgs e)
        {
            Close();
        }
    }
}
