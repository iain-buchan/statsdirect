using SpreadsheetGear;
using StatsDirect.Templates;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StatsDirect.UI.Learning;

internal sealed record WorksheetAnalysisTarget(
    frmSpreadsheetGear Grid, IWorkbook Workbook, IWorksheet Worksheet,
    string WorkbookName, string WorksheetName, string Selection, int Version, bool WorkbookShared)
{
    internal WorksheetAnalysisProposal Propose(WorksheetCommand command) => new(this, command);
}

internal sealed class WorksheetAnalysisProposal
{
    private static readonly Dictionary<string, string> expectedInputs = new(StringComparer.Ordinal)
    {
        ["TPaired"] = "Two matched numeric columns or one column of differences.",
        ["TUnpaired"] = "Two independent numeric samples in separate columns or with a grouping column.",
        ["MannWhitney"] = "Two independent numeric samples in separate columns or with a grouping column.",
        ["Wilcoxon"] = "Two matched numeric columns or one column of differences.",
        ["Spearman"] = "Two numeric columns containing matched observations.",
        ["Agreement"] = "Two or more matched sets of numeric measurements.",
        ["UnivariateSummary"] = "One or more numeric columns to summarise.",
        ["Normality"] = "One or more numeric columns to check for departure from normality."
    };

    private readonly WorksheetCommand command;
    private readonly StatsDirectForm destination;
    internal WorksheetAnalysisTarget Source { get; }
    internal string MethodName { get; }
    internal string MenuPath => command.MenuPath;
    internal string Inputs { get; }
    internal string DestinationTitle { get; }
    internal string Effect { get; }

    internal WorksheetAnalysisProposal(WorksheetAnalysisTarget target, WorksheetCommand command)
    {
        var (main, source) = target.Grid.ResolveWorksheetAnalysisTarget(target, allowSelectionChanges: false);
        if (command == null || !command.CanRun)
            throw new InvalidOperationException("That method is unavailable. Choose another method in Analyse.");

        this.command = command;
        Source = source;
        if (command.Operation.Length == 0)
        {
            MethodName = command.Name;
            Effect = "Opens the StatsDirect calculator.";
            return;
        }

        if (!TemplateFactory.Operations.TryGetValue(command.Operation, out var operation))
            throw new InvalidOperationException("That method is unavailable. Choose another method in Analyse.");
        MethodName = operation.FriendlyName ?? command.Name;
        Inputs = expectedInputs.GetValueOrDefault(command.Operation);
        if (HasStep(operation.Steps, step => step is ReportStep or ChartStep))
        {
            destination = main.ExistingReportDestination;
            DestinationTitle = destination?.Text ?? "A new report";
        }
        bool changesWorksheet = command.Groups[0] == "Data" || HasStep(operation.Steps, step => step is OutputFrameStep);
        Effect = changesWorksheet
            ? "This command can change worksheet data. StatsDirect uses its usual controls."
            : "StatsDirect uses these cells where they fit and prompts for any other data or settings.";
    }

    internal void Open()
    {
        var (main, _) = Source.Grid.ResolveWorksheetAnalysisTarget(Source, allowSelectionChanges: false);
        if (DestinationTitle != null)
        {
            var currentDestination = main.ExistingReportDestination;
            if (!ReferenceEquals(currentDestination, destination) || (destination != null && destination.Text != DestinationTitle))
                throw new InvalidOperationException("The report destination changed. Choose the method again.");
        }
        main.OpenReviewedWorksheetCommand(command);
    }

    private static bool HasStep(IEnumerable<Step> steps, Func<Step, bool> match) => steps.Any(step => match(step) || (step switch
    {
        TestStep condition => HasStep(condition.TrueSteps, match) || HasStep(condition.FalseSteps, match),
        IterationStep iteration => HasStep(iteration.Steps, match),
        _ => false
    }));
}
