using StatsDirect.UI;

internal static partial class Program
{
    static async Task CheckNewRSession(frmMain main)
    {
        var file = (ToolStripMenuItem)Field(main, "fileToolStripMenuItem");
        var command = (ToolStripMenuItem)file.DropDownItems["newScriptToolStripMenuItem"]!;
        Check(command.Available && command.Enabled && command.Text == "New R session"
            && file.DropDownItems.IndexOf(command) == file.DropDownItems.IndexOfKey("newReportToolStripMenuItem") + 1
            && file.DropDownItems.IndexOf(command) < file.DropDownItems.IndexOfKey("openToolStripMenuItem"),
            "File offers New R session beside New Data Workbook and New Report, before Open");
        var before = main.MdiChildren.ToHashSet();
        command.PerformClick();
        await Task.Yield();
        var created = main.MdiChildren.Where(child => !before.Contains(child)).ToArray();
        Check(created.Length == 1 && created[0].GetType().Name == "frmScript" && main.ActiveMdiChild == created[0],
            "File > New R session creates and activates one script editor through the normal document workflow");
        using var session = created[0];
        Check((string)Get(session, "LearningLanguage") == "R"
            && ((RichTextBox)Field(session, "rtbDoc")).TextLength == 0
            && !(bool)Get(session, "LearningRunning") && (string)Get(session, "LearningOutput") == "",
            "New R session opens a blank R editor without executing code or starting a result");
    }
}
