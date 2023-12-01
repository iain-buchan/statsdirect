using System;
using System.Windows.Forms;
using StatsDirect.Utilities;

namespace StatsDirect.UI
{
    public partial class frmScores : Form
    {
        private bool userCancelled;
        private readonly Builtins.ScoresOptions options;

        public frmScores(Builtins.ScoresOptions options)
        {
            InitializeComponent();
            this.options = options;
            SetFormFromOptions();
        }

        private void cmdCancel_Click(object? sender, EventArgs e)
        {
            userCancelled = true;
            Close();
        }

        private void cmdOk_Click(object? sender, EventArgs e)
        {
            userCancelled = false;
            SetOptionsFromForm();
            Close();
        }

        public bool UserCancelled => userCancelled;

        private void SetFormFromOptions()
        {
            gridScores.Columns[0].HeaderText = options.Title1;
            gridScores.Columns[1].HeaderText = options.Title2;
            gridScores.RowCount = Math.Max(options.Values1.Count, options.Values2.Count);
            for (int i = 0; i < options.Values1.Count; i++)
            {
                gridScores[0, i].Value = options.Values1[i].ToString();
            }
            for (int i = 0; i < options.Values2.Count; i++)
            {
                gridScores[1, i].Value = options.Values2[i].ToString();
            }
        }

        private void SetOptionsFromForm()
        {
            for (int i = 0; i < options.Values1.Count; i++)
            {
                options.Values1[i] = Parsing.Cdbl_Txt((string)gridScores[0, i].Value);
            }
            for (int i = 0; i < options.Values2.Count; i++)
            {
                options.Values2[i] = Parsing.Cdbl_Txt((string)gridScores[1, i].Value);
            }
        }
    }
}
