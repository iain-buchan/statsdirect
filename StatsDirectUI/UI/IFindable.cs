using System.Windows.Forms;

namespace StatsDirect.UI
{
    internal interface IFindable
    {
        RichTextBox Rtb { get; }
        void SetFocus();
    }
}
