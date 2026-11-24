using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace StatsDirect.UI.Learning;

internal sealed partial class ScriptAIPane
{
    private sealed record WorksheetAction(string Kind, WorksheetAnalysisTarget Source = null,
        WorksheetAnalysisProposal Proposal = null, string Design = null, bool ShowCompare = true)
    {
        internal string Token { get; } = Guid.NewGuid().ToString("N");

        internal JsonObject Display()
        {
            var card = new JsonObject { ["token"] = Token, ["kind"] = Kind };
            if (Kind == "compare") card["showCompare"] = ShowCompare;
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
            }
            return card;
        }
    }

    private readonly WorksheetAction compareMeans = new("compare");
    private readonly WorksheetAction chooseMethod = new("compare", ShowCompare: false);
    private WorksheetAction worksheetAction;

    private WorksheetAction VisibleWorksheetAction =>
        WorksheetMode && pending == null && !service.Tutor.Disabled
            ? worksheetAction ?? (chat?.Messages.Count == 0 ? compareMeans : chooseMethod)
            : null;

    private static WorksheetAction ConfirmWorksheetAnalysis(WorksheetAnalysisTarget source, WorksheetCommand command)
    {
        var proposal = source.Propose(command);
        string design = command.Operation switch
        {
            "TPaired" => "paired",
            "TUnpaired" => "independent",
            _ => null
        };
        return new WorksheetAction("confirm", proposal.Source, proposal, design);
    }

    private void ChooseWorksheetAction(string token, string choice)
    {
        var action = VisibleWorksheetAction;
        if (disposed || !IsOpen || action == null || action.Token != token
            || script is not frmSpreadsheetGear grid || grid.IsDisposed || grid.Disposing
            || grid.MdiParent is not frmMain main) return;
        // The pane stays with its workbook when an analysis brings a report forward.
        // Browsing can bring that workbook back, but continuing a reviewed operation still requires it to be active.
        if (main.ActiveMdiChild != grid && action.Kind == "compare" && choice is "browse" or "compare"
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
                case ("compare", "compare") when action.ShowCompare:
                    worksheetAction = new WorksheetAction("choose", grid.CaptureWorksheetAnalysisTarget());
                    break;
                case ("choose", "paired"):
                case ("choose", "independent"):
                    string operation = choice == "paired" ? "TPaired" : "TUnpaired";
                    var command = main.WorksheetCommands().FirstOrDefault(method => method.Operation == operation && method.CanRun);
                    worksheetAction = ConfirmWorksheetAnalysis(action.Source, command);
                    break;
                case ("confirm", "change") when action.Design != null:
                    worksheetAction = new WorksheetAction("choose", action.Source);
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
