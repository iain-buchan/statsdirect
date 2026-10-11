using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    /// <summary>
    /// What a window gives the find bar: the next match of a term after the selection, round to the start; the selection replaced when
    /// it is the term; every match replaced; the selected text as a term when the bar opens; and the focus back on the content.
    /// </summary>
    internal interface IFindTarget
    {
        Task<bool> FindNextAsync(string term, bool matchCase);
        Task<bool> ReplaceSelectionAsync(string term, string replacement, bool matchCase);
        Task<int> ReplaceAllAsync(string term, string replacement, bool matchCase);
        string SelectedText { get; }
        void FocusContent();
    }

    /// <summary>
    /// Find and replace as a bar at the top of a window, below its toolbar, in place of floating dialogs: the term, Match case, Find next,
    /// the replacement, Replace, Replace all, the result of the search, and Close.  Enter finds or replaces, Escape closes, and the bar
    /// opens with the selected text as the term.  Hidden until asked.
    /// </summary>
    internal sealed class FindBar : Panel
    {
        private readonly IFindTarget target;
        private readonly TextBox txtFind = new(), txtReplace = new();
        private readonly CheckBox chkMatchCase;
        private readonly Label lblStatus;

        /// <summary>Whether the bar is open: Visible answers for the window as a whole, which may not be showing.</summary>
        public bool Shown { get; private set; }

        public FindBar(IFindTarget target)
        {
            this.target = target;
            Dock = DockStyle.Top;
            AutoSize = true;
            Visible = false;
            Padding beside = new(3, 6, 3, 3);   // labels and the check box sit level with the boxes and buttons
            chkMatchCase = new CheckBox { Text = "Match case", AutoSize = true, Margin = beside };
            lblStatus = new Label { AutoSize = true, ForeColor = System.Drawing.SystemColors.GrayText, Margin = beside };
            Button Button(string text, Func<Task> click) { Button b = new() { Text = text, AutoSize = true }; b.Click += async (_, _) => await click(); return b; }
            FlowLayoutPanel row = new() { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(4, 2, 4, 2) };
            row.Controls.AddRange(new Control[]
            {
                new Label { Text = "Find", AutoSize = true, Margin = beside }, txtFind, chkMatchCase, Button("Find next", FindNext),
                new Label { Text = "Replace with", AutoSize = true, Margin = beside }, txtReplace, Button("Replace", Replace), Button("Replace all", ReplaceAll),
                lblStatus, Button("Close", () => { Close(); return Task.CompletedTask; }),
            });
            Controls.Add(row);
            txtFind.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await FindNext(); } else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; Close(); } };
            txtReplace.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await Replace(); } else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; Close(); } };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            txtFind.Width = txtReplace.Width = LogicalToDeviceUnits(220);   // made in code after the window was scaled, so scaled here
        }

        /// <summary>Opens the bar, with the focus in the term or in the replacement, and the selected text as the term when there is one.</summary>
        public void Open(bool replace)
        {
            Visible = true;
            Shown = true;
            lblStatus.Text = "";
            string selected = target.SelectedText;
            if (!string.IsNullOrEmpty(selected))
                txtFind.Text = selected;
            (replace ? txtReplace : txtFind).Focus();
            txtFind.SelectAll();
        }

        public void Close()
        {
            Visible = false;
            Shown = false;
            target.FocusContent();
        }

        private async Task FindNext()
        {
            string term = txtFind.Text;
            if (term.Length == 0) return;
            bool found = await target.FindNextAsync(term, chkMatchCase.Checked);
            lblStatus.Text = found ? "" : "Not found: " + term;
        }

        private async Task Replace()
        {
            // the match found last is replaced, then the next is found
            string term = txtFind.Text;
            if (term.Length == 0) return;
            await target.ReplaceSelectionAsync(term, txtReplace.Text, chkMatchCase.Checked);
            await FindNext();
        }

        private async Task ReplaceAll()
        {
            string term = txtFind.Text;
            if (term.Length == 0) return;
            int count = await target.ReplaceAllAsync(term, txtReplace.Text, chkMatchCase.Checked);
            lblStatus.Text = count == 0 ? "Not found: " + term : count + " replaced";
        }
    }
}
