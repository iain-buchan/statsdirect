using StatsDirect.Templates;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    internal interface ISdApplication
    {
        WindowInformation? ActiveGrid { get; }
        int ActiveHelpTopic { get; set; }
        string? ActiveHelpUrl { get; set; }
        Form ActiveMdiChild { get; }
        WindowInformation? ActiveWindow { get; }
        void AddWindow(WindowInformation info);
        IList<PaneAndPosition> AvailableFramePanesAndPositions();
        IList<PaneAndPosition> AvailableReportPanesAndPositions();
        void CheckForUpdates();
        void ClearBatchMode();
        void CloseAndUpdate();
        StatsDirectForm CreateGrid();
        StatsDirectForm CreateReport();
        Form DialogOwner { get; }
        void DoOperation(string operationName);
        void DoOperationOnceOrUntilCancelled(Operation operation, ParameterBag? parameterBag);
        void EnsureBuiltInMenuItemsCanShowHelp(MenuStrip menuStrip);
        void EraseAnyOutstandingParameters();
        void Error(string message, string caption);
        WindowInformation? FindWindowInformationForPath(string path);
        void FriendlyError(string explanation, Exception ex, bool showHelpButton);
        bool GetBoolean(string prompt, string caption, bool defaultValue, out bool cancelled);
        bool GetBoolean(string prompt, string caption, bool defaultValue, int helpTopic, out bool Cancelled);
        int GetGridNumber();
        int GetInteger(string prompt, string caption, int defaultValue, out bool cancelled);
        int GetReportNumber();
        int GetScriptWindowNumber();
        string? GetString(string prompt, string caption, string defaultValue);
        bool InOperation { get; }
        bool IsRunningOnMono { get; }
        bool IsSelecting { get; }
        PaneAndPosition? MostRecentlySelectedReport { get; set; }
        DialogResult MsgboxX(string text, MessageBoxButtons buttons, MessageBoxIcon icon);
        DialogResult MsgboxX(string text, MessageBoxButtons buttons, MessageBoxIcon icon, string caption, bool showHelpButton, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1);
        DialogResult MsgboxX(string text, MessageBoxButtons buttons, MessageBoxIcon icon, string caption, int helpTopic, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1);
        void NoteASubformCloseIsCancelled();
        void NoteASubformCloseIsStarting();
        void NoteEndOfSelection(bool ok);
        void NoteFormActivated(WindowInformation info);
        void NoteFormClosing(StatsDirectForm window, FormClosingEventArgs e);
        void NoteRecentFile(string path, bool openedOk);
        bool OpenFile();
        bool OpenFile(string path, bool removeFromRecentFilesIfNotFound);
        /// <summary>
        /// TODO: An abomination that needs removing as soon as possible.  Unfortunately it's a huge amount of work.
        /// </summary>
        void PuntThroughEventLoop(Exception ex);
        bool Query(string message, string caption);
        IReadOnlyList<string> RecentFiles { get; }
        void Run();
        bool SelectCells(string fullSelectionMessage, string cancelButtonLabel, bool canSelectMultipleRows, bool allowUserToPivot, out bool wasPivoted);
        bool SelectingData { get; }
        void ShowCurrentHelp();
        void ShowHelp(Form parent);
        void ShowHelp(Form parent, string operationTopic);
        void ShowOrQueueDialog(Form f, Action<Form, DialogResult>? postDisplayAction);
        void Shutdown();
        string? Validate(Validator validator, Parameter outstandingParameter, ParameterBag outputParameters, string validationFailMessage);
    }
}
