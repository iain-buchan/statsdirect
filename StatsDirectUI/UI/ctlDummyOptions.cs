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
            if (options.AllowUserToTreatAsContinuous)
                lblRubric.Text = "StatsDirect has detected that variable '" + options.VariableName + "' might be a categorical variable that needs to be converted to " + options.CategoryNames.Count.ToString() + " or " + (options.CategoryNames.Count + 1).ToString() + " dummy variables for use in regression.\n\nClick \"Treat as continuous\" if you want to ignore this and treat the variable as continuous or select a reference category if you want to create dummy variables. By convention the category with the smallest value is dropped and becomes the reference category, but you can choose another reference/index category if you like.\n\nSelect the reference category from the list below.";
            else
                lblRubric.Text = "You are about to create " + options.CategoryNames.Count.ToString() + ", or " + (options.CategoryNames.Count + 1).ToString() + " if you select <none>, dummy (indicator) variables to represent the " + (options.CategoryNames.Count + 1).ToString() + " categories in your data.\n\nBy convention, the smallest value category is dropped. Alternatively, you might choose to drop the most prevalent category, which is " + options.LargestCategoryTitle + ".\n\nChoose the category to drop from the list below:";
            foreach (string ti in options.CategoryNames)
            {
                lstVariables.Items.Add(ti);
            }
            lstVariables.Items.Add("Keep all categories as dummies");
            if (options.AllowUserToTreatAsContinuous)
                lstVariables.Items.Add("Treat as continuous"); // Change FillOptionsFromControl if this is ever not the last item
            lstVariables.SelectedIndex = 0;
        }

        private void FillOptionsFromControl()
        {
            options.JDrop = lstVariables.SelectedIndex;
            // ASSUME: "Treat as continuous" is last option if it is allowed at all
            options.TreatAsContinuous = options.AllowUserToTreatAsContinuous && lstVariables.SelectedIndex == lstVariables.Items.Count - 1;
        }

        public void OkClicked()
        {
            FillOptionsFromControl();
        }
    }
}
