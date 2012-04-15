using System;
using System.Collections.Generic;

namespace StatsDirect.Templates
{
    public class CheckBoxDescriptor
    {
        public bool IsRadio;
        public bool IsExclusive;
        public string Text;
        public bool Checked;
        public string Name;

        public CheckBoxDescriptor()
        {
            // Nothing else required
        }

        public CheckBoxDescriptor(string Name, string Text, bool Checked, bool IsRadio)
        {
            this.Name = Name;
            this.Text = Text;
            this.Checked = Checked;
            this.IsRadio = IsRadio;
        }
    }

    public class SelectionBoxDescriptor
    {
        public string Name;
        public string Title;
        public List<string> Labels;
        public int SelectedIndex;
        public string Value;

        public SelectionBoxDescriptor()
        {
            Labels = new List<string>();
        }

        public void FillFactor(double first, double last, double steps, string msk, int def)
        {
            if (steps > 0)
            {
                for (double C = first; C <= last; C += steps)
                    Labels.Add(C.ToString(msk));
            }
            else if (steps < 0)
            {
                for (double C = first; C <= last; C += steps)
                    Labels.Add(C.ToString(msk));
            }
            else
                throw new ArgumentOutOfRangeException("steps", "steps must be non-zero");
            SelectedIndex = def;
        }

        public string SelectedValue
        {
            set
            {
                for (int i = 0; i < Labels.Count; i++)
                {
                    if (value == Labels[i])
                    {
                        SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        public void SetAsConfidence()
        {
            FillFactor(90, 99, 1, "", 5);
        }
    }

    public class TextBoxDescriptor
    {
        public string Label;
        public bool Text;
    }

    public class OptionDescriptor: IFillable
    {
        /// <summary>
        /// If non-blank, the host *may*, but does not have to, examine the string and see whether it has a custom mode of displaying options that matches that string.
        /// This is a convenient way of special-casing some dialogs without requiring a very wide API, and with graceful degradation if a host doesn't support the particular forms.
        /// </summary>
        public string CustomFormatHint;
        public string Title;
        public List<CheckBoxDescriptor> CheckBoxes;
        public List<SelectionBoxDescriptor> SelectionBoxes;
        public List<TextBoxDescriptor> TextBoxes;

        public OptionDescriptor()
        {
            CheckBoxes = new List<CheckBoxDescriptor>();
            SelectionBoxes = new List<SelectionBoxDescriptor>();
            TextBoxes = new List<TextBoxDescriptor>();
        }

        #region IFillable Members

        string IFillable.FillerToUse
        {
            get { return "OptionDescriptor"; }
        }

        #endregion
    }
}
