using System;
using System.Collections.Generic;

namespace StatsDirect.Templates
{
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
    }

    public class OptionDescriptor: IFillable
    {
        public string Title;
        public List<SelectionBoxDescriptor> SelectionBoxes;

        public OptionDescriptor()
        {
            SelectionBoxes = new List<SelectionBoxDescriptor>();
        }

        public string FillerToUse
        {
            get { return "OptionDescriptor"; }
        }
    }
}
