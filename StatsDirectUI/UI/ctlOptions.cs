using System;
using System.Collections.Generic;
using System.Windows.Forms;
using StatsDirect.Templates;

namespace StatsDirect.UI
{
    public partial class ctlOptions : UserControl, IOkable, IFillParameterBag
    {
        private readonly OptionDescriptor optionDescriptor;
        private readonly IList<Control> checkBoxes;

        const int CHECKBOX_WIDTH = 150;
        const int CHECKBOX_HEIGHT = 17;
        const int MARGIN = 3;

        public ctlOptions(OptionDescriptor descriptor)
        {
            optionDescriptor = descriptor;
            Text = optionDescriptor.Title;
            checkBoxes = new List<Control>();
            InitializeComponent();

            SuspendLayout();
            pnlCheck.SuspendLayout();

            Text = descriptor.Title;

            // Set up check boxes
            if (optionDescriptor.CheckBoxes.Count > 0)
            {
                for (int i = 0; i < optionDescriptor.CheckBoxes.Count; i++)
                {
                    int row = i / 2;
                    int col = i % 2;
                    CheckBoxDescriptor d = optionDescriptor.CheckBoxes[i];
                    if (d.IsRadio)
                    {
                        RadioButton rad = new RadioButton
                                              {
                                                  Location =
                                                      new System.Drawing.Point(
                                                      MARGIN + (col*(CHECKBOX_WIDTH + MARGIN)),
                                                      MARGIN + (row*(CHECKBOX_HEIGHT + MARGIN))),
                                                  Size = new System.Drawing.Size(CHECKBOX_WIDTH, CHECKBOX_HEIGHT),
                                                  Text = d.Text,
                                                  Checked = d.Checked,
                                                  UseVisualStyleBackColor = true
                                              };
                        checkBoxes.Add(rad);
                    }
                    else
                    {
                        CheckBox chk = new CheckBox
                                           {
                                               Location =
                                                   new System.Drawing.Point(MARGIN + (col*(CHECKBOX_WIDTH + MARGIN)),
                                                                            MARGIN + (row*(CHECKBOX_HEIGHT + MARGIN))),
                                               Size = new System.Drawing.Size(CHECKBOX_WIDTH, CHECKBOX_HEIGHT),
                                               Text = d.Text,
                                               Checked = d.Checked,
                                               Tag = d.IsExclusive,
                                               UseVisualStyleBackColor = true
                                           };
                        checkBoxes.Add(chk);
                    }
                    pnlCheck.Controls.Add(checkBoxes[i]);
                }
            }
            else
            {
                pnlCheck.Visible = false;
            }

            // Set up combo boxes
            if (optionDescriptor.SelectionBoxes.Count > 0)
            {
                if (optionDescriptor.SelectionBoxes.Count > 2)
                    throw new ArgumentOutOfRangeException("optionDescriptor.SelectionBoxes.Count", optionDescriptor.SelectionBoxes.Count, "Can only handle up to 2 combo boxes");
                lbl1.Visible = optionDescriptor.SelectionBoxes.Count > 1;
                cbo1.Visible = optionDescriptor.SelectionBoxes.Count > 1;
                lbl0.Text = optionDescriptor.SelectionBoxes[0].Title;
                foreach (string s in optionDescriptor.SelectionBoxes[0].Labels)
                {
                    cbo0.Items.Add(s);
                }
                cbo0.SelectedIndex = optionDescriptor.SelectionBoxes[0].SelectedIndex;
                if (optionDescriptor.SelectionBoxes.Count > 1)
                {
                    lbl1.Text = optionDescriptor.SelectionBoxes[1].Title;
                    foreach (string s in optionDescriptor.SelectionBoxes[1].Labels)
                    {
                        cbo1.Items.Add(s);
                    }
                    cbo1.SelectedIndex = optionDescriptor.SelectionBoxes[1].SelectedIndex;
                }
            }
            else
            {
                pnlCombo.Visible = false;
            }
            pnlCheck.ResumeLayout(false);
            pnlCheck.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        private void FillDescriptorFromForm()
        {
            for (int i = 0; i < checkBoxes.Count; i++)
            {
                optionDescriptor.CheckBoxes[i].Checked = checkBoxes[i].GetType().Name.Contains("Check") ? ((CheckBox)checkBoxes[i]).Checked : ((RadioButton)checkBoxes[i]).Checked;
            }
            if (optionDescriptor.SelectionBoxes.Count > 0)
            {
                optionDescriptor.SelectionBoxes[0].Value = cbo0.Text;
                optionDescriptor.SelectionBoxes[0].SelectedIndex = cbo0.SelectedIndex;
            }
            if (optionDescriptor.SelectionBoxes.Count > 1)
            {
                optionDescriptor.SelectionBoxes[1].Value = cbo1.Text;
                optionDescriptor.SelectionBoxes[1].SelectedIndex = cbo1.SelectedIndex;
            }
        }

        #region IOkable Members

        void IOkable.OkClicked()
        {
            FillDescriptorFromForm();
        }

        #endregion

        #region IFillParameterBag Members

        Control IFillParameterBag.Fill(ParameterBag outputParameters, bool doValidation)
        {
            FillDescriptorFromForm();
            foreach (CheckBoxDescriptor cb in optionDescriptor.CheckBoxes)
            {
                if (cb.IsExclusive)
                {
                    outputParameters.Add(cb.Name, new FilledParameter(true, cb.Checked));
                }
            }
            foreach (SelectionBoxDescriptor sb in optionDescriptor.SelectionBoxes)
            {
                outputParameters.Add(sb.Name, new FilledParameter(true, sb.Value));
            }
            return null;
        }

        #endregion
    }
}