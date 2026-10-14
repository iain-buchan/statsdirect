using StatsDirect.UI.Learning;
using System.Text.Json.Nodes;
using System.Windows.Forms;

internal static partial class Program
{
    // Enter in the AI pane's question box sends the question; Shift+Enter is left to the box, which adds a line.
    static async Task TestComposerKeys(LearningService service)
    {
        var pane = (ScriptAIPane)Field(service, "ai");
        pane.ShareScript.Checked = false; pane.ShareWorkbook.Checked = false;
        var messages = (JsonArray)Field(Field(pane, "chat"), "Messages"); int before = messages.Count;
        Check(!pane.ComposerKey(Keys.Shift | Keys.Enter) && !pane.ComposerKey(Keys.A), "Shift+Enter and ordinary keys are left to the question box");
        pane.Composer.Text = "Sent with Enter";
        Check(pane.ComposerKey(Keys.Enter), "Enter in the question box is taken as Send");
        await Wait(() => messages.Count >= before + 2, "Enter sends the question and the reply arrives");
        await pane.Transcript.SettledAsync();
        Check(messages[before].S("text") == "Sent with Enter" && pane.Composer.Text.Length == 0, "the question sent with Enter is the box's text, and the box is cleared");
    }
}
