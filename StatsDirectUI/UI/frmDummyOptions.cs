using System;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class frmDummyOptions : Form
    {
        private bool userCancelled;
        private readonly Builtins.DummyOptions options;

        public frmDummyOptions(Builtins.DummyOptions options)
        {
            this.options = options;
            InitializeComponent();
            SetFormFromOptions();
        }

        public bool UserCancelled
        {
            get { return userCancelled; }
        }

        private void cmdOK_Click(object sender, EventArgs e)
        {
            SetOptionsFromForm();
            Close();
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            userCancelled = true;
            Close();
        }

        private void SetFormFromOptions()
        {
            lblRubric.Text = "You are about to create " + options.Names.Count.ToString() + ", or " + (options.Names.Count + 1).ToString() + " if you select <none>, dummy (indicator) variables to represent the " + (options.Names.Count + 1).ToString() + " categories in your data.\n\nBy convention, the smallest value category is dropped. Alternatively, you might choose to drop the most prevalent category, which is " + options.MaxCatTi + ".\n\nChoose the category to drop from the list below:";
            foreach (string ti in options.Names)
            {
                lstVariables.Items.Add(ti);
            }
            lstVariables.Items.Add("<none>");
            lstVariables.SelectedIndex = 0;
        }

        private void SetOptionsFromForm()
        {
            options.JDrop = lstVariables.SelectedIndex;
        }
    }
}
