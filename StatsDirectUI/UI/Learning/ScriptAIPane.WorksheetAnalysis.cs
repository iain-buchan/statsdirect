using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace StatsDirect.UI.Learning;

internal sealed partial class ScriptAIPane
{
    private sealed record ClarificationOption(string Id, string Label, string Answer);

    private sealed record AnalysisClarification(string Dimension, string Question, ClarificationOption[] Options)
    {
        internal static AnalysisClarification Create(string dimension) => dimension switch
        {
            "goal" => new(dimension, "What would you like to find out?",
            [
                new("means", "Compare means", "I want to compare means."),
                new("ordered", "Compare ordered scores", "I want to compare ordered scores or relative ordering."),
                new("proportions", "Compare proportions", "I want to compare proportions."),
                new("association", "Assess association", "I want to assess an association between variables."),
                new("agreement", "Assess agreement", "I want to assess agreement between measurements."),
                new("unsure", "I’m not sure", "I am not sure what comparison best answers my question. Help me work it out.")
            ]),
            "scale" => new(dimension, "How was the outcome measured?",
            [
                new("quantitative", "Numeric measurement", "My outcome is a quantitative measurement."),
                new("ordinal", "Ordered score or category", "My outcome is an ordered score or category."),
                new("binary", "Two categories", "My outcome has two categories."),
                new("nominal", "Unordered categories", "My outcome has more than two unordered categories."),
                new("unsure", "I’m not sure", "I am not sure of the outcome scale. Help me identify it.")
            ]),
            "design" => new(dimension, "How were the observations collected?",
            [
                new("paired", "Matched observations", "The measurements are matched on the same people or units."),
                new("independent", "Separate groups", "The observations come from separate, independent groups."),
                new("repeated", "Repeated or grouped observations", "The observations are repeated over more than two occasions or grouped within clusters. Ask which applies if needed."),
                new("unsure", "I’m not sure", "I am not sure how the observations are related. Help me identify the study design.")
            ]),
            _ => throw new ArgumentException("Choose goal, scale or design.")
        };
    }

    private sealed record IntervalDecision(bool Paired, PairedDifferenceDiagnostics Pairs = null,
        IndependentSampleDiagnostics Columns = null, bool SeparateSamples = false)
    {
        internal string Kind => Paired ? "paired-interval" : "independent-interval";

        internal bool CanCheck => Paired ? Pairs == null : !SeparateSamples;

        internal bool? LargeSample => Pairs is { } pairs
            ? pairs.Count > MeasurementDistributionDiagnostics.LargeSampleThreshold
            : SeparateSamples && Columns is { } columns
                ? Math.Min(columns.First.Count, columns.Second.Count) > MeasurementDistributionDiagnostics.LargeSampleThreshold
                : null;

        internal IntervalDecision ReadSelection(frmSpreadsheetGear grid, bool confirmSeparateSamples = false) => Paired
            ? this with { Pairs = grid.CheckSelectedPairedDifferences() }
            : this with { Columns = grid.CheckSelectedIndependentSamples(), SeparateSamples = confirmSeparateSamples };

        internal string Method(string branch) => (Paired, branch) switch
        {
            (true, "non-normal") => "Wilcoxon",
            (true, _) => "TPaired",
            (false, "large") => "ZUnpaired",
            (false, "non-normal") => "MannWhitney",
            _ => "TUnpaired"
        };

        internal string Advice(string branch) => (Paired, branch) switch
        {
            (true, "large") => "The large-sample branch recommends the paired t test for the mean difference.",
            (true, "normal") => "The paired t test addresses the mean difference. This branch assumes approximately normal differences.",
            (true, "non-normal") => "The signed-rank test concerns a shift in location and assumes roughly symmetric differences. If the aim is specifically to estimate or test the mean difference, consider whether this answers your question.",
            (false, "large") => "The Help routes large, well-behaved samples to the two-sample Z test for the difference in means.",
            (false, "normal") => "The unpaired t test compares means. Consider the spread in each group before using an equal-variance result.",
            _ => "Mann–Whitney compares relative ordering between groups. Use that question to decide whether it fits your goal."
        };

        internal string Conflict(bool large) => Paired
            ? large
                ? "The local check found more than 100 complete pairs. Choose the large-sample route for the mean difference or the non-normal route for Wilcoxon signed ranks."
                : "The local check found at most 100 complete pairs. Choose the branch for the distribution of the differences."
            : large
                ? "The local check found more than 100 observations in each sample. Review their distributions before choosing a method."
                : "The local check found at most 100 observations in at least one sample. Choose the branch for the distributions within groups.";

        internal void AddDetails(JsonObject card)
        {
            card["sizeKnown"] = LargeSample.HasValue;
            card["canCheck"] = CanCheck;
            if (Pairs is { } pairs)
            {
                string summary = pairs.Count == 1 ? "Local check found 1 complete pair" : $"Local check found {pairs.Count} complete pairs";
                summary += NormalityText(pairs.ShapiroWilkW, pairs.ShapiroWilkP);
                if (pairs.ShapiroWilkW == null && LargeSample == false)
                    summary += ". Shapiro–Wilk needs at least three finite differences with some variation.";
                var lines = new JsonArray(summary);
                if (LargeSample == true && pairs.Shape is { } shape)
                {
                    lines.Add($"Shape check · {shape.FarOutCount} beyond the 3× IQR fences");
                    if (shape.Skewness is double skewness && shape.ExcessKurtosis is double kurtosis)
                        lines.Add($"Skewness {skewness:G3} · Excess kurtosis {kurtosis:G3}");
                }
                card["diagnostics"] = lines;
            }
            else if (Columns is { } columns)
            {
                var lines = new JsonArray();
                if (!SeparateSamples)
                    lines.Add("Selected columns, if they hold the two samples separately:");
                lines.Add($"Column 1: n = {columns.First.Count}" + NormalityText(columns.First.ShapiroWilkW, columns.First.ShapiroWilkP));
                lines.Add($"Column 2: n = {columns.Second.Count}" + NormalityText(columns.Second.ShapiroWilkW, columns.Second.ShapiroWilkP));
                card["diagnostics"] = lines;
            }
        }

        internal static (string Branch, string Reason) SuggestPairedBranch(PairedDifferenceDiagnostics pairs)
        {
            if (pairs.Count < 3)
                return (null, "There are too few complete pairs to assess the distribution. Check the selected measurements before choosing.");
            if (!pairs.HasVariation)
                return (null, "All the paired differences are the same, leaving zero variation for the test. Check the selected measurements.");
            if (pairs.Count > MeasurementDistributionDiagnostics.LargeSampleThreshold)
            {
                var signals = new List<string>();
                if (pairs.HasPotentialOutlier) signals.Add("far-out differences");
                if (pairs.HasSkewSignal) signals.Add("pronounced skew");
                if (pairs.Shape?.HasHeavyTails == true) signals.Add("heavy tails");
                if (signals.Count == 0)
                    return ("large", "There are more than 100 complete pairs and the shape checks found no substantial departure. A paired t test answers the question about the mean difference.");

                string evidence = "The shape checks flagged " + string.Join(", ", signals) + ". ";
                string signedRanks = pairs.HasSkewSignal
                    ? "Wilcoxon signed ranks addresses a shift in location and assumes roughly symmetric differences. Pronounced skew makes that assumption doubtful."
                    : "Consider Wilcoxon signed ranks for a shift in location if the differences look roughly symmetric.";
                return (null, evidence + signedRanks + " A paired t test still answers the question about the mean difference. Review any influential differences before choosing.");
            }
            if (pairs.HasPotentialOutlier)
                return (null, "The box-plot check flagged an unusually distant difference. Review whether it reflects a data error or strongly affects the mean before choosing.");
            if (pairs.HasSkewSignal)
                return (null, "The skewness check flagged an uneven shape. Examine the differences before choosing. Signed-rank inference about a shift in location requires roughly symmetric differences.");
            if (pairs.ShapiroWilkP is not double p)
                return (null, "The normality check could not assess these differences. Look at their shape before choosing.");
            if (p < 0.05)
                return ("non-normal", "Shapiro–Wilk found evidence of non-normal differences. Consider Wilcoxon signed-rank if the differences look roughly symmetric and a shift in location answers your question.");
            return ("normal", "A paired t test answers the question about the mean difference. Shapiro–Wilk found no clear departure from normality, and the shape checks flagged no unusual differences. With this sample, those checks provide limited evidence.");
        }

        private static string NormalityText(double? w, double? p) => w.HasValue && p.HasValue
            ? $". Shapiro–Wilk W = {w.Value:G4}, P = {p.Value:G4}."
            : "";
    }

    private sealed record WorksheetAction(string Kind, WorksheetAnalysisTarget Source = null,
        WorksheetAnalysisProposal Proposal = null, string Design = null,
        AnalysisClarification Clarification = null, IntervalDecision Interval = null, string IntervalBranch = null,
        string Feedback = null)
    {
        internal string Token { get; init; } = Guid.NewGuid().ToString("N");

        internal JsonObject Display()
        {
            var card = new JsonObject { ["token"] = Token, ["kind"] = Kind };
            if (Clarification != null)
            {
                card["dimension"] = Clarification.Dimension;
                card["question"] = Clarification.Question;
                card["options"] = new JsonArray(Clarification.Options.Select(option => (JsonNode)new JsonObject
                {
                    ["id"] = option.Id, ["label"] = option.Label
                }).ToArray());
            }
            Interval?.AddDetails(card);
            if (Kind == "paired-interval" && Interval?.Pairs is { } pairs)
            {
                var (branch, reason) = IntervalDecision.SuggestPairedBranch(pairs);
                card["suggestedBranch"] = branch;
                card["recommendation"] = reason;
            }
            if (Feedback != null) card["feedback"] = Feedback;
            if (Kind == "confirm")
            {
                card["choice"] = Design;
                card["method"] = Proposal.MethodName;
                card["workbook"] = Source.WorkbookName;
                card["sheet"] = Source.WorksheetName;
                card["selection"] = Source.Selection.Replace("$", "", StringComparison.Ordinal);
                card["inputs"] = Proposal.Inputs;
                card["menuPath"] = Proposal.MenuPath;
                card["destination"] = Proposal.DestinationTitle;
                card["effect"] = Proposal.Effect;
                if (Interval is { } interval) card["intervalAdvice"] = interval.Advice(IntervalBranch);
            }
            return card;
        }
    }

    private readonly WorksheetAction emptyWorkbook = new("empty");
    private readonly WorksheetAction chooseMethod = new("compare");
    private WorksheetAction worksheetAction;
    private bool workbookHasData, worksheetRefreshQueued, workbookContentsDirty;

    private WorksheetAction VisibleWorksheetAction =>
        WorksheetMode && pending == null && !service.Tutor.Disabled
            ? workbookHasData ? worksheetAction ?? chooseMethod : emptyWorkbook
            : null;

    private bool RefreshWorksheetContents()
    {
        bool hasData = script is frmSpreadsheetGear grid && grid.HasWorkbookData();
        if (WorksheetMode)
        {
            questionCaption.Text = hasData ? "Ask a question about your data" : "Ask a question";
            Composer.AccessibleName = questionCaption.Text;
        }
        if (workbookHasData == hasData) return false;
        workbookHasData = hasData;
        if (!hasData) ClearWorksheetAnalysis();
        return true;
    }

    private void WorkbookContentsChanged(object sender, EventArgs e) => QueueWorksheetRefresh(contentsChanged: true);
    private void WorkbookSelectionChanged(object sender, EventArgs e) => QueueWorksheetRefresh(contentsChanged: false);

    private void QueueWorksheetRefresh(bool contentsChanged)
    {
        if (disposed || !IsOpen || !view.IsHandleCreated) return;
        workbookContentsDirty |= contentsChanged;
        if (worksheetRefreshQueued) return;
        worksheetRefreshQueued = true;
        view.BeginInvoke(new Action(() =>
        {
            worksheetRefreshQueued = false;
            bool refreshContents = workbookContentsDirty;
            workbookContentsDirty = false;
            if (disposed || !IsOpen || !WorksheetMode) return;

            UpdateWorksheetSelection();
            if (pending != null) return;
            bool changed = refreshContents && RefreshWorksheetContents();
            if (RefreshWorksheetGuidance() || changed) Render();
        }));
    }

    private void UpdateWorksheetSelection()
    {
        if (script is not frmSpreadsheetGear grid)
        {
            worksheetSelection.Visible = false;
            return;
        }
        var (sheet, range, singleCell) = grid.WorksheetSelectionInfo();
        worksheetSelection.Text = range.Length == 0 ? "" : (singleCell ? "Current cell: " : "Selection: ") + sheet + "!" + range;
        worksheetSelection.Visible = range.Length > 0;
    }

    private bool RefreshWorksheetGuidance()
    {
        var updated = WithCurrentWorksheetSelection(worksheetAction);
        if (ReferenceEquals(updated, worksheetAction)) return false;
        worksheetAction = updated;
        return true;
    }

    private static WorksheetAction ConfirmWorksheetAnalysis(WorksheetAnalysisTarget source, WorksheetCommand command,
        IntervalDecision interval = null, string intervalBranch = null)
    {
        var proposal = source.Propose(command);
        string design = command.Operation switch
        {
            "TPaired" => "paired",
            "TUnpaired" => "independent",
            _ => null
        };
        return new WorksheetAction("confirm", proposal.Source, proposal, design, Interval: interval, IntervalBranch: intervalBranch);
    }

    private static WorksheetAnalysisTarget CaptureCurrentGuidanceTarget(WorksheetAnalysisTarget target)
    {
        var current = target.Grid.CaptureWorksheetAnalysisTarget(target.WorkbookShared);
        if (!ReferenceEquals(current.Workbook, target.Workbook))
            throw new InvalidOperationException("The workbook changed. Ask for a new analysis suggestion.");
        return current;
    }

    private static WorksheetAction WithCurrentWorksheetSelection(WorksheetAction action, bool newCard = false)
    {
        // Guidance is still a question. A specific proposal keeps the cells and version the user saw.
        if (action?.Kind is not ("choose" or "clarify" or "paired-interval" or "independent-interval"))
            return action;
        WorksheetAnalysisTarget current;
        try { current = CaptureCurrentGuidanceTarget(action.Source); }
        catch (InvalidOperationException) { return action; }

        if (!newCard && current == action.Source) return action;
        // A delayed click belongs to the selection and evidence the user saw.
        var updated = action with { Source = current, Feedback = null, Token = Guid.NewGuid().ToString("N") };
        if (action.Interval == null) return updated;

        var interval = new IntervalDecision(action.Interval.Paired);
        try { interval = interval.ReadSelection(current.Grid); }
        catch (InvalidOperationException) { /* The user can describe the layout if it cannot be read as two columns. */ }
        return updated with { Interval = interval };
    }

    private void ChooseWorksheetAction(string token, string choice)
    {
        if (disposed || !IsOpen || script is not frmSpreadsheetGear grid || grid.IsDisposed || grid.Disposing
            || grid.MdiParent is not frmMain main) return;
        UpdateWorksheetSelection();
        bool changed = RefreshWorksheetContents();
        if (RefreshWorksheetGuidance() || changed) Render();
        var action = VisibleWorksheetAction;
        if (action == null || action.Token != token) return;

        if (action.Kind == "empty")
        {
            if (choice == "example") main.OpenExampleWorkbook();
            else if (choice == "help") SdApplication.SoleInstance.ShowHelp(main, "basics/worksheet.htm");
            return;
        }
        // The pane stays with its workbook when an analysis brings a report forward.
        // Asking for help or browsing can bring it back. Confirmation still requires it to be active.
        if (main.ActiveMdiChild != grid && action.Kind == "compare" && choice is "browse" or "guide"
            && !main.InOperation && !main.LearningCalculationRunning)
        {
            grid.Activate();
            if (script != grid || !ReferenceEquals(action, VisibleWorksheetAction)) return;
        }
        if (!ReferenceEquals(main.ActiveMdiChild, grid))
        {
            status.Text = "Activate “" + WorksheetName + "” to continue this analysis.";
            return;
        }

        try
        {
            switch (action.Kind, choice)
            {
                case ("compare", "browse"):
                    BrowseWorksheetMethods(action, grid, main);
                    return;
                case ("compare", "guide"):
                    _ = SendAsync("Help me choose a statistical method.");
                    return;
                case ("choose", "paired" or "independent"):
                    grid.ResolveWorksheetAnalysisTarget(action.Source, allowSelectionChanges: false);
                    var interval = new IntervalDecision(choice == "paired");
                    worksheetAction = WithCurrentWorksheetSelection(
                        new WorksheetAction(interval.Kind, action.Source, Interval: interval), newCard: true);
                    break;
                case ("paired-interval" or "independent-interval", "check") when action.Interval.CanCheck:
                    try
                    {
                        var selected = grid.ResolveWorksheetAnalysisTarget(action.Source, allowSelectionChanges: true).Source;
                        worksheetAction = new WorksheetAction(action.Kind, selected, Interval: action.Interval.ReadSelection(grid, confirmSeparateSamples: true));
                    }
                    catch (InvalidOperationException ex)
                    {
                        ShowIntervalFeedback(action, ex.Message);
                        return;
                    }
                    break;
                case ("paired-interval" or "independent-interval", "large" or "normal" or "non-normal"):
                    grid.ResolveWorksheetAnalysisTarget(action.Source, allowSelectionChanges: false);
                    var decision = action.Interval;
                    if (decision.LargeSample is bool large && large != (choice == "large") && choice != "non-normal")
                    {
                        ShowIntervalFeedback(action, decision.Conflict(large));
                        return;
                    }
                    worksheetAction = ConfirmWorksheetAnalysis(action.Source,
                        FindWorksheetCommand(main, decision.Method(choice)), decision, choice);
                    break;
                case ("paired-interval" or "independent-interval", "back"):
                    worksheetAction = new WorksheetAction("choose", action.Source);
                    break;
                case ("confirm", "sample") when action.Interval is { } previous:
                    worksheetAction = WithCurrentWorksheetSelection(new WorksheetAction(previous.Kind, action.Source, Interval: previous));
                    break;
                case ("clarify", _):
                    var answer = action.Clarification.Options.FirstOrDefault(option => option.Id == choice);
                    if (answer == null || service.Tutor.DataSharingDisabled) return;
                    grid.ResolveWorksheetAnalysisTarget(action.Source, allowSelectionChanges: false);
                    ClearWorksheetAnalysis();
                    Render();
                    _ = SendAsync(answer.Answer, isClarificationReply: true);
                    return;
                case ("confirm", "change") when action.Design != null:
                    worksheetAction = WithCurrentWorksheetSelection(new WorksheetAction("choose", action.Source));
                    break;
                case ("confirm", "continue"):
                    // Retire the token before native controls can open a modal message loop.
                    ClearWorksheetAnalysis();
                    Render();
                    action.Proposal.Open();
                    return;
                default:
                    return;
            }
            status.Text = "";
            Render();
        }
        catch (InvalidOperationException ex)
        {
            ClearWorksheetAnalysis();
            status.Text = ex.Message;
            Render();
        }
    }

    private void ShowIntervalFeedback(WorksheetAction action, string message)
    {
        worksheetAction = action with { Feedback = message };
        status.Text = "";
        Render();
    }

    private static WorksheetCommand FindWorksheetCommand(frmMain main, string operation) =>
        main.WorksheetCommands().FirstOrDefault(method => method.Operation == operation && method.CanRun);

    private void BrowseWorksheetMethods(WorksheetAction action, frmSpreadsheetGear grid, frmMain main)
    {
        var source = grid.CaptureWorksheetAnalysisTarget();
        var conversation = chat;
        main.ChooseWorksheetCommand(command =>
        {
            if (disposed || !IsOpen || conversation != chat || grid != script
                || !ReferenceEquals(action, VisibleWorksheetAction)) return;
            try
            {
                worksheetAction = ConfirmWorksheetAnalysis(source, command);
                status.Text = "";
            }
            catch (InvalidOperationException ex) { status.Text = ex.Message; }
            Render();
        });
    }

    private bool ClearWorksheetAnalysis()
    {
        if (worksheetAction == null) return false;
        worksheetAction = null;
        status.Text = "";
        return true;
    }
}
