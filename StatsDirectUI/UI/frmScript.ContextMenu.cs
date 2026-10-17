using System;
using System.Windows.Forms;

namespace StatsDirect.UI;

public partial class frmScript
{
    private void BuildScriptContextMenu()
    {
        var menu = new ContextMenuStrip { Name = "scriptEditorContextMenu" };
        ToolStripMenuItem Item(string name, string text, Keys shortcut, EventHandler command)
        {
            var item = new ToolStripMenuItem(text) { Name = name, ShortcutKeys = shortcut };
            item.Click += (sender, args) =>
            {
                // Opening a menu can leave focus with the output or a menu item.
                // These commands always belong to the code editor under this menu.
                rtbDoc.Focus();
                editTarget = rtbDoc;
                command(sender, args);
            };
            return item;
        }
        var undo = Item("scriptUndo", "&Undo", Keys.Control | Keys.Z, mnuUndo_Click);
        var redo = Item("scriptRedo", "&Redo", Keys.Control | Keys.Y, mnuRedo_Click);
        var cut = Item("scriptCut", "Cu&t", Keys.Control | Keys.X, CutToolStripMenuItem_Click);
        var copy = Item("scriptCopy", "&Copy", Keys.Control | Keys.C, CopyToolStripMenuItem_Click);
        var paste = Item("scriptPaste", "&Paste", Keys.Control | Keys.V, PasteToolStripMenuItem_Click);
        var delete = Item("scriptDelete", "&Delete", Keys.None, (_, _) =>
        {
            if (Editable && rtbDoc.SelectionLength > 0) rtbDoc.SelectedText = "";
        });
        delete.ShortcutKeyDisplayString = "Del"; // The editor handles Delete at a caret, even after this menu closes.
        var selectAll = Item("scriptSelectAll", "Select &All", Keys.Control | Keys.A, SelectAllToolStripMenuItem_Click);
        menu.Items.AddRange(new ToolStripItem[]
        {
            undo, redo, new ToolStripSeparator(), cut, copy, paste, delete,
            new ToolStripSeparator(), selectAll
        });
        menu.Opening += (_, args) =>
        {
            if (!rtbDoc.Enabled) { args.Cancel = true; return; }
            rtbDoc.Focus();
            editTarget = rtbDoc;
            bool editable = !rtbDoc.ReadOnly, selected = rtbDoc.SelectionLength > 0;
            undo.Enabled = editable && rtbDoc.CanUndo;
            redo.Enabled = editable && rtbDoc.CanRedo;
            cut.Enabled = delete.Enabled = editable && selected;
            copy.Enabled = selected;
            paste.Enabled = editable && (rtbDoc.CanPaste(DataFormats.GetFormat(DataFormats.UnicodeText))
                || rtbDoc.CanPaste(DataFormats.GetFormat(DataFormats.Text))
                || rtbDoc.CanPaste(DataFormats.GetFormat(DataFormats.Rtf)));
            selectAll.Enabled = rtbDoc.TextLength > 0;
        };
        rtbDoc.ContextMenuStrip = menu; // Windows supplies mouse, Shift+F10 and Menu-key activation.
        Disposed += (_, _) => menu.Dispose();
    }
}
