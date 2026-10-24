namespace StatsDirect.UI;

public partial class frmScript
{
    internal void LoadNotebookScript(string script, string title)
    {
        ShowR(script, title, runImmediately: false);
        currentFile = null;
        Path = null;
        rtbDoc.Modified = true;
        Dirty = true;
    }
}
