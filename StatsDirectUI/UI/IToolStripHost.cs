using System.Windows.Forms;

namespace StatsDirect.UI
{
    internal interface IToolStripHost
    {
        bool AppendToolStrip(ToolStrip sourceToolStrip);
        bool RemoveToolStrip(ToolStrip toolStrip);
    }
}
