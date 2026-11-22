using SpreadsheetGear;
using StatsDirect.Templates;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace StatsDirect.UI.Learning;

internal sealed record WorksheetAnalysisTarget(
    frmSpreadsheetGear Grid, IWorkbook Workbook, IWorksheet Worksheet,
    string WorkbookName, string WorksheetName, string Selection, int Version, bool WorkbookShared)
{
    internal WorksheetAnalysisProposal Propose(string operation) => new(this, operation);
}

internal sealed class WorksheetAnalysisProposal
{
    // These methods can start from a native worksheet selection and produce a standard report.
    private static readonly Dictionary<string, string> expectedInputs = new(StringComparer.Ordinal)
    {
        ["TPaired"] = "Two matched numeric columns or one column of differences.",
        ["TUnpaired"] = "Two independent numeric samples in separate columns or with a grouping column.",
        ["Wilcoxon"] = "Two matched numeric columns or one column of differences.",
        ["Spearman"] = "Two numeric columns containing matched observations.",
        ["Agreement"] = "Two or more matched sets of numeric measurements.",
        ["UnivariateSummary"] = "One or more numeric columns to summarise.",
        ["Normality"] = "One or more numeric columns to check for departure from normality."
    };

    private readonly WorksheetAnalysisTarget target;
    private readonly Operation operation;
    private readonly string inputs;
    private ReviewState approved;

    internal WorksheetAnalysisProposal(WorksheetAnalysisTarget target, string operationName)
    {
        if (operationName == null || !expectedInputs.TryGetValue(operationName, out inputs))
            throw new ArgumentException("This method cannot be proposed from worksheet chat. Use Analyse to choose another method.");
        if (!TemplateFactory.Operations.TryGetValue(operationName, out operation) || operation.HasPrerequisites)
            throw new InvalidOperationException("This method is unavailable in this build.");
        target.Grid.ResolveWorksheetAnalysisTarget(target, allowSelectionChanges: true);
        this.target = target;
    }

    internal static IReadOnlyList<string> SupportedOperations { get; } = Array.AsReadOnly(expectedInputs.Keys.ToArray());

    internal string MethodName => operation.FriendlyName;

    internal bool Review(IWin32Window owner, Func<bool> canOpen = null)
    {
        approved = null;
        CheckPermission(canOpen);
        var (main, source) = target.Grid.ResolveWorksheetAnalysisTarget(target, allowSelectionChanges: true);
        var destination = main.ExistingReportDestination;
        var review = new ReviewState(source, destination, destination?.Text ?? "A new report");

        using var dialog = new ReviewDialog(MethodName, inputs, review);
        if (dialog.ShowDialog(owner) != DialogResult.OK) return false;

        CheckPermission(canOpen);
        ConfirmReview(review);
        approved = review;
        return true;
    }

    internal void Open(Func<bool> canOpen = null)
    {
        var review = approved ?? throw new InvalidOperationException("Review the proposed analysis before starting it.");
        approved = null;
        CheckPermission(canOpen);
        ConfirmReview(review).OpenReviewedWorksheetAnalysis(operation);
    }

    private frmMain ConfirmReview(ReviewState review)
    {
        var (main, _) = target.Grid.ResolveWorksheetAnalysisTarget(review.Source, allowSelectionChanges: false);
        var destination = main.ExistingReportDestination;
        if (!ReferenceEquals(destination, review.Destination) || (destination != null && destination.Text != review.DestinationTitle))
            throw new InvalidOperationException("The report destination changed during review. Review the analysis again.");
        return main;
    }

    private static void CheckPermission(Func<bool> canOpen)
    {
        if (canOpen?.Invoke() == false)
            throw new InvalidOperationException("The worksheet analysis proposal is no longer available.");
    }

    private sealed record ReviewState(WorksheetAnalysisTarget Source, StatsDirectForm Destination, string DestinationTitle);

    private sealed class ReviewDialog : Form
    {
        private readonly Panel body;
        private readonly TableLayoutPanel details;
        private readonly Label method, source, expected, destination, instructions;
        private readonly Font headingFont;

        internal ReviewDialog(string methodName, string inputs, ReviewState review)
        {
            Name = "worksheetAnalysisReview";
            Text = "Review analysis";
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;

            body = new Panel { AutoScroll = true, Dock = DockStyle.Fill, Padding = new Padding(16, 16, 16, 0) };
            details = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 6
            };
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 133));
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < details.RowCount; i++) details.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            headingFont = new Font(Font, FontStyle.Bold);
            method = Caption("worksheetAnalysisMethod", methodName, 13);
            method.Font = headingFont;
            AddFullRow(method, 0);
            source = Caption("worksheetAnalysisSource", $"{review.Source.WorkbookName}\r\nWorksheet {review.Source.WorksheetName}");
            AddDetail("Data source", source, 1);
            bool longSelection = review.Source.Selection.Length > 45;
            var selection = new TextBox
            {
                Name = "worksheetAnalysisSelection",
                AccessibleName = "Selected cells",
                Text = review.Source.Selection,
                Multiline = longSelection,
                WordWrap = true,
                ScrollBars = longSelection ? ScrollBars.Vertical : ScrollBars.None,
                ReadOnly = true,
                Height = longSelection ? 46 : 23,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 0, 0, 8)
            };
            AddDetail("Selected cells", selection, 2);
            expected = Caption("worksheetAnalysisInputs", inputs);
            AddDetail("Expected data", expected, 3);
            destination = Caption("worksheetAnalysisDestination", review.DestinationTitle);
            AddDetail("Report destination", destination, 4);
            instructions = Caption("worksheetAnalysisInstructions",
                "StatsDirect uses these cells if they fit the method and prompts for any other data or settings before running locally.", 0);
            AddFullRow(instructions, 5);

            var open = new Button
            {
                Name = "openWorksheetAnalysis", Text = "Run and add to report", AutoSize = true,
                DialogResult = DialogResult.OK, Padding = new Padding(8, 2, 8, 2)
            };
            var cancel = new Button
            {
                Name = "cancelWorksheetAnalysis", Text = "Cancel", AutoSize = true,
                DialogResult = DialogResult.Cancel, Padding = new Padding(8, 2, 8, 2)
            };
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(13, 8, 13, 10)
            };
            actions.Controls.Add(cancel);
            actions.Controls.Add(open);
            body.Controls.Add(details);
            Controls.Add(body);
            Controls.Add(actions);
            CancelButton = cancel;
            Shown += (_, _) => cancel.Focus();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Form host = Owner ?? this;
            Rectangle area = Screen.FromControl(host).WorkingArea;
            Size border = SizeFromClientSize(Size.Empty);
            Size preferred = host.LogicalToDeviceUnits(new Size(555, 310));
            ClientSize = new Size(Math.Min(preferred.Width, area.Width - border.Width), Math.Min(preferred.Height, area.Height - border.Height));

            // Give wrapped labels the final width instead of the form's small design-time bounds.
            int fullWidth = Math.Max(1, ClientSize.Width - body.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
            int captionWidth = host.LogicalToDeviceUnits(133);
            details.ColumnStyles[0].Width = captionWidth;
            int valueWidth = Math.Max(1, fullWidth - captionWidth);
            method.MaximumSize = instructions.MaximumSize = new Size(fullWidth, 0);
            source.MaximumSize = expected.MaximumSize = destination.MaximumSize = new Size(valueWidth, 0);
            body.PerformLayout();

            Location = new Point(
                Math.Clamp(host.Left + (host.Width - Width) / 2, area.Left, area.Right - Width),
                Math.Clamp(host.Top + (host.Height - Height) / 2, area.Top, area.Bottom - Height));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) headingFont?.Dispose();
        }

        private void AddDetail(string title, Control value, int row)
        {
            var field = Caption("", title);
            field.Margin = new Padding(0, 0, 8, 8);
            details.Controls.Add(field, 0, row);
            details.Controls.Add(value, 1, row);
        }

        private void AddFullRow(Label label, int row)
        {
            details.Controls.Add(label, 0, row);
            details.SetColumnSpan(label, 2);
        }

        private static Label Caption(string name, string text, int spacing = 8) => new()
        {
            Name = name,
            Text = text,
            AutoSize = true,
            UseMnemonic = false,
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Margin = new Padding(0, 0, 0, spacing)
        };
    }
}
