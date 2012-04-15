using System.Windows.Forms;
using StatsDirect.Builtins;

namespace StatsDirect.UI
{
    public partial class ctlDummyOptions : UserControl, IOkable
    {
        private readonly DummyOptions options;

        public ctlDummyOptions(DummyOptions options)
        {
            this.options = options;
            InitializeComponent();
            FillControlFromOptions();
        }

        private void FillControlFromOptions()
        {
            lblRubric.Text = "You are about to create " + options.Names.Count.ToString() + ", or " + (options.Names.Count + 1).ToString() + " if you select <none>, dummy (indicator) variables to represent the " + (options.Names.Count + 1).ToString() + " categories in your data.\n\nBy convention, the smallest value category is dropped. Alternatively, you might choose to drop the most prevalent category, which is " + options.MaxCatTi + ".\n\nChoose the category to drop from the list below:";
            foreach (string ti in options.Names)
            {
                lstVariables.Items.Add(ti);
            }
            lstVariables.Items.Add("<none>");
            lstVariables.SelectedIndex = 0;
        }

        private void FillOptionsFromControl()
        {
            options.JDrop = lstVariables.SelectedIndex;
        }

        public void OkClicked()
        {
            FillOptionsFromControl();
        }
    }
}
