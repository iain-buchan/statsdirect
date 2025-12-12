using System.Windows.Forms;

namespace StatsDirect.Builtins
{
    /// <summary>
    /// TODO: HACK: A horrible carrier for OpenFileDialog / SaveFileDialog options while we've separated Engine from UI but haven't got rid of Host.
    /// It stops us needing the SD application window in Engine, and that's about its only good point.
    /// </summary>
    /// <remarks>Immutable.</remarks>
    public abstract class FileDialogOptions
    {
        public string Filter { get; init; }
        public bool ShowHelp { get; init; }
        public string Title { get; init; }
    }

    public class OpenFileDialogOptions
        : FileDialogOptions
    {
        public bool CheckFileExists { get; init; }
    }

    public class SaveFileDialogOptions
        : FileDialogOptions
    {
        public string FileName { get; init; }
        public bool OverwritePrompt { get; init; }
    }

    public class FileDialogResult
    {
        public DialogResult DialogResult { get; init; }
        public string FileName { get; init; }
    }
}
