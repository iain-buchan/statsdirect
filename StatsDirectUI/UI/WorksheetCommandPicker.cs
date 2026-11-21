using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace StatsDirect.UI;

internal sealed record WorksheetCommand(ToolStripMenuItem Item, string Name, string[] Groups, string Operation)
{
    internal string MenuPath => string.Join(" › ", Groups);
    internal bool CanRun => Item.Available && Item.Enabled;
}

/// <summary>Finds and browses the worksheet's existing menu commands from the Analyse button.</summary>
internal sealed class WorksheetCommandPicker : ToolStripDropDown
{
    private readonly Label searchCaption = new() { Text = "Search methods", AutoSize = true };
    private readonly TextBox search = new()
    {
        Name = "worksheetCommandSearch", AccessibleName = "Search methods",
        BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Top
    };
    private readonly TreeView commands = new()
    {
        Name = "worksheetCommandResults", AccessibleName = "Methods", HideSelection = false,
        ShowNodeToolTips = true, ShowLines = false, FullRowSelect = true, BorderStyle = BorderStyle.None,
        Dock = DockStyle.Fill
    };
    private readonly LinkLabel help = new() { Text = "Search Help", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Bottom, Visible = false };
    private readonly TableLayoutPanel panel = new()
    {
        ColumnCount = 1, RowCount = 4, BackColor = SystemColors.Window, TabStop = false
    };
    private readonly ToolStripControlHost host;
    private readonly HashSet<string> expanded = new(StringComparer.Ordinal);
    private WorksheetCommand[] source = [];
    private bool rebuilding;

    internal event Action<WorksheetCommand> CommandChosen;
    internal event Action<string> HelpSearchRequested;

    internal WorksheetCommandPicker()
    {
        AccessibleName = "Analyse worksheet";
        AutoSize = false;
        AutoClose = true;
        BackColor = SystemColors.Window;
        Renderer = new ToolStripSystemRenderer();

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(searchCaption, 0, 0);
        panel.Controls.Add(search, 0, 1);
        panel.Controls.Add(commands, 0, 2);
        panel.Controls.Add(help, 0, 3);
        host = new ToolStripControlHost(panel) { AutoSize = false, Margin = Padding.Empty, Padding = Padding.Empty };
        Items.Add(host);

        search.TextChanged += (_, _) => UpdateResults();
        search.KeyDown += (_, e) => { if (HandleKey(e.KeyData)) e.SuppressKeyPress = true; };
        commands.AfterExpand += (_, e) => { if (!rebuilding && string.IsNullOrWhiteSpace(search.Text)) expanded.Add(e.Node.FullPath); };
        commands.AfterCollapse += (_, e) => { if (!rebuilding && string.IsNullOrWhiteSpace(search.Text)) expanded.Remove(e.Node.FullPath); };
        commands.NodeMouseClick += (_, e) =>
        {
            var hit = commands.HitTest(e.Location);
            if (e.Button != MouseButtons.Left || (hit.Location & (TreeViewHitTestLocations.Label | TreeViewHitTestLocations.RightOfLabel)) == 0)
                return;

            commands.SelectedNode = e.Node;
            ChooseSelection();
        };
        help.LinkClicked += (_, _) =>
        {
            string query = search.Text.Trim();
            Close(ToolStripDropDownCloseReason.ItemClicked);
            HelpSearchRequested?.Invoke(query);
        };
    }

    internal void Prepare(WorksheetCommand[] available, Control owner, ToolStripItem button)
    {
        source = available;
        if (search.TextLength == 0) UpdateResults();
        else search.Clear();

        var anchor = (button.GetCurrentParent() ?? owner).PointToScreen(button.Bounds.Location);
        var area = Screen.FromPoint(anchor).WorkingArea;
        Font = panel.Font = owner.Font;
        Padding = new Padding(1);
        panel.Padding = new Padding(owner.LogicalToDeviceUnits(9));
        searchCaption.Margin = new Padding(0, 0, 0, owner.LogicalToDeviceUnits(4));
        search.Margin = new Padding(0, 0, 0, owner.LogicalToDeviceUnits(6));
        commands.Margin = Padding.Empty;
        commands.Indent = owner.LogicalToDeviceUnits(18);
        commands.ItemHeight = Math.Max(panel.Font.Height + owner.LogicalToDeviceUnits(5), owner.LogicalToDeviceUnits(23));
        help.Height = owner.LogicalToDeviceUnits(25);
        Size = new Size(Math.Min(area.Width, owner.LogicalToDeviceUnits(420)), Math.Min(area.Height, owner.LogicalToDeviceUnits(370)));
        host.Size = panel.Size = new Size(Width - Padding.Horizontal, Height - Padding.Vertical);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        BeginInvoke(() => { if (Visible && !IsDisposed) search.Focus(); });
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) => HandleKey(keyData) || base.ProcessCmdKey(ref msg, keyData);
    protected override bool ProcessDialogKey(Keys keyData) => HandleKey(keyData) || base.ProcessDialogKey(keyData);

    private bool HandleKey(Keys key)
    {
        if (!Visible) return false;
        if (key == Keys.Escape)
        {
            Close(ToolStripDropDownCloseReason.Keyboard);
            return true;
        }

        if (key == Keys.Enter && !help.Focused)
        {
            ChooseSelection();
            return true;
        }

        if (key is Keys.Tab or (Keys.Shift | Keys.Tab))
        {
            Control focused = search.Focused ? search : commands.Focused ? commands : help;
            panel.SelectNextControl(focused, key == Keys.Tab, true, true, true);
            return true;
        }

        if (!search.Focused || key is not (Keys.Up or Keys.Down)) return false;
        if (string.IsNullOrWhiteSpace(search.Text))
        {
            if (commands.Nodes.Count > 0)
            {
                commands.SelectedNode = key == Keys.Down ? commands.Nodes[0] : commands.Nodes[^1];
                commands.Focus();
            }
        }
        else
        {
            var matches = Nodes(commands.Nodes).Where(IsAvailable).ToArray();
            if (matches.Length > 0)
            {
                int start = Array.IndexOf(matches, commands.SelectedNode);
                int next = start < 0 ? key == Keys.Down ? 0 : matches.Length - 1 : start + (key == Keys.Down ? 1 : -1);
                commands.SelectedNode = matches[Math.Clamp(next, 0, matches.Length - 1)];
                commands.SelectedNode.EnsureVisible();
            }
        }
        return true;
    }

    private void ChooseSelection()
    {
        if (commands.SelectedNode is not { } node) return;
        if (node.Tag is WorksheetCommand command)
        {
            if (!command.CanRun) return;
            Close(ToolStripDropDownCloseReason.ItemClicked);
            CommandChosen?.Invoke(command);
        }
        else if (node.Nodes.Count > 0 && (!search.Focused || string.IsNullOrWhiteSpace(search.Text))) node.Toggle();
    }

    private void UpdateResults()
    {
        string[] query = Words(search.Text);
        bool searching = query.Length > 0;
        rebuilding = true;
        commands.BeginUpdate();
        try
        {
            commands.Nodes.Clear();
            if (!searching)
            {
                AddMethods(source, showPath: false);
                RestoreExpanded(commands.Nodes);
            }
            else
            {
                string phrase = string.Join(" ", query);
                var matches = Find(source, query).ToArray();
                var exact = matches.TakeWhile(command => string.Join(" ", Words(command.Name)) == phrase).ToArray();
                if (exact.Length > 0)
                {
                    var group = commands.Nodes.Add("Exact matches");
                    foreach (var command in exact) group.Nodes.Add(CommandNode(command, true));
                }

                AddMethods(matches.Skip(exact.Length), showPath: true);
                commands.ExpandAll();
            }

            if (commands.Nodes.Count == 0)
                commands.Nodes.Add(new TreeNode(searching ? "No matching methods" : "No methods available") { ForeColor = SystemColors.GrayText });
            commands.SelectedNode = Nodes(commands.Nodes).FirstOrDefault(IsAvailable) ?? (searching ? null : commands.Nodes[0]);
        }
        finally
        {
            commands.EndUpdate();
            rebuilding = false;
        }
        help.Visible = search.Text.Trim().Length > 0;
    }

    private void AddMethods(IEnumerable<WorksheetCommand> methods, bool showPath)
    {
        foreach (var command in methods)
        {
            var level = commands.Nodes;
            foreach (string group in command.Groups)
            {
                if (!level.ContainsKey(group)) level.Add(group, group);
                level = level[group].Nodes;
            }
            level.Add(CommandNode(command, showPath));
        }
    }

    private void RestoreExpanded(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            if (!expanded.Contains(node.FullPath)) continue;
            node.Expand();
            RestoreExpanded(node.Nodes);
        }
    }

    private static IEnumerable<TreeNode> Nodes(TreeNodeCollection collection)
    {
        foreach (TreeNode node in collection)
        {
            yield return node;
            if (!node.IsExpanded) continue;
            foreach (var child in Nodes(node.Nodes)) yield return child;
        }
    }

    private static bool IsAvailable(TreeNode node) => node.Tag is WorksheetCommand command && command.CanRun;

    private static TreeNode CommandNode(WorksheetCommand command, bool showPath) =>
        new(showPath ? $"{command.Name}  ({command.MenuPath})" : command.Name)
        {
            Tag = command, ForeColor = command.CanRun ? SystemColors.WindowText : SystemColors.GrayText,
            ToolTipText = command.MenuPath
        };


    private static IEnumerable<WorksheetCommand> Find(WorksheetCommand[] source, string[] query)
    {
        string phrase = string.Join(" ", query);
        return source.Select(command =>
        {
            string[] name = Words(command.Name);
            string label = string.Join(" ", name);
            if (label == phrase) return (command, rank: 0);
            if (label.StartsWith(phrase, StringComparison.Ordinal)) return (command, rank: 1);
            if (Matches(name, query)) return (command, rank: 2);
            string[] context = Words(command.MenuPath + " " + command.Operation);
            return (command, rank: Matches(name.Concat(context), query) ? 3 : 4);
        })
        .Where(result => result.rank < 4)
        .OrderBy(result => result.rank)
        .Select(result => result.command);
    }

    private static bool Matches(IEnumerable<string> words, string[] query) =>
        query.All(term => words.Any(word => word.StartsWith(term, StringComparison.Ordinal)));

    private static string[] Words(string text)
    {
        var normalized = new StringBuilder(text.Length);
        foreach (char letter in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(letter) != UnicodeCategory.NonSpacingMark)
                normalized.Append(char.IsLetterOrDigit(letter) ? char.ToLowerInvariant(letter) : ' ');
        }
        return normalized.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}
