using System.Runtime.InteropServices;
using StatsDirect.UI;

internal static partial class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "SendMessage")]
    private static extern IntPtr ScriptMenuMessage(IntPtr window, int message, IntPtr parameter, IntPtr point);

    static async Task CheckScriptContextMenu(frmMain main)
    {
        if (Environment.GetEnvironmentVariable("STATSDIRECT_SKIP_CLIPBOARD") == "1") { Console.WriteLine("SKIP ScriptContextMenu clipboard checks (STATSDIRECT_SKIP_CLIPBOARD=1 preserves the user's clipboard)"); return; }
        using var script = (Form)Call(main, "CreateScriptWindow");
        Call(script, "ShowR", "before <- 1\nafter <- 2", "Context menu check", null, false);
        var editor = (RichTextBox)Field(script, "rtbDoc");
        var output = (Control)Field(script, "outputPane");
        await ((Task)Get(output, "Ready")).WaitAsync(TimeSpan.FromSeconds(45));
        Call(output, "AppendText", "Output selection must not replace selected code");
        await (Task)Call(output, "SettledAsync");
        var menu = editor.ContextMenuStrip;
        Check(menu != null && string.Join("|", menu.Items.Cast<ToolStripItem>().Select(item => item is ToolStripSeparator ? "-" : item.Text.Replace("&", "")))
            == "Undo|Redo|-|Cut|Copy|Paste|Delete|-|Select All", "R code editor has the standard editing context menu in document order");
        ToolStripMenuItem Item(string name) => (ToolStripMenuItem)menu.Items["script" + name];
        int openings = 0;
        menu.Opening += (_, _) => openings++;
        async Task Open(bool keyboard = false, bool outputFirst = false)
        {
            menu.Close();
            script.Activate();
            if (outputFirst)
            {
                await (Task)Call(output, "SelectAllAsync");
                output.Focus();
                await Task.Delay(40);
                Check(ReferenceEquals(Field(script, "editTarget"), output), "output is the last edit target before the code editor context menu opens");
            }
            else editor.Focus();
            int before = openings;
            if (keyboard)
                ScriptMenuMessage(editor.Handle, 0x007B, editor.Handle, new IntPtr(-1)); // WM_CONTEXTMENU from keyboard
            else
            {
                Point p = editor.GetPositionFromCharIndex(editor.TextLength);
                int x = Math.Clamp(p.X + 15, 1, Math.Max(1, editor.ClientSize.Width - 2));
                int y = Math.Clamp(p.Y + 2, 1, Math.Max(1, editor.ClientSize.Height - 2));
                IntPtr location = new((y << 16) | x);
                ScriptMenuMessage(editor.Handle, 0x0204, new IntPtr(2), location); // WM_RBUTTONDOWN
                ScriptMenuMessage(editor.Handle, 0x0205, IntPtr.Zero, location); // WM_RBUTTONUP
            }
            await Task.Yield();
            Check(openings == before + 1 && menu.Visible && menu.SourceControl == editor,
                keyboard ? "keyboard context-menu request opens the actual code editor menu" : "native mouse right click opens the actual code editor menu");
            Check(ReferenceEquals(Field(script, "editTarget"), editor), "editor context menu routes commands to code rather than the output pane");
        }
        void Click(string command) { Item(command).PerformClick(); menu.Close(); }
        void Reset(string text) { menu.Close(); editor.ReadOnly = false; editor.Text = text; editor.ClearUndo(); }

        try
        {
            editor.Select(0, 6);
            await Open();
            Check(editor.SelectedText == "before" && Item("Copy").Enabled && Item("Cut").Enabled && Item("Delete").Enabled,
                "right clicking beyond selected R code preserves the selection and enables its editing commands");
            Check(Item("Undo").ShortcutKeys == (Keys.Control | Keys.Z) && Item("Redo").ShortcutKeys == (Keys.Control | Keys.Y)
                && Item("Copy").ShortcutKeys == (Keys.Control | Keys.C) && Item("Paste").ShortcutKeys == (Keys.Control | Keys.V),
                "code context menu advertises the existing Windows editing shortcuts");
            Click("Delete");
            Check(editor.Text == " <- 1\nafter <- 2", "context Delete removes selected code without changing the output");
            await Open(true); Click("Undo");
            Check(editor.Text == "before <- 1\nafter <- 2", "context Undo restores the deleted code through native editor history");
            await Open(true); Click("Redo");
            Check(editor.Text == " <- 1\nafter <- 2", "context Redo repeats the code deletion");
            await Open(true, true); Click("SelectAll");
            Check(editor.SelectionLength == editor.TextLength && editor.SelectedText == editor.Text,
                "context Select All selects code when the output had focus last");

            Reset("abc"); editor.Select(0, 2);
            await Open(true); menu.Close(); editor.Select(0, 0);
            ScriptMenuMessage(editor.Handle, 0x0100, new IntPtr((int)Keys.Delete), IntPtr.Zero);
            ScriptMenuMessage(editor.Handle, 0x0101, new IntPtr((int)Keys.Delete), IntPtr.Zero);
            Check(Item("Delete").ShortcutKeys == Keys.None && editor.Text == "bc",
                "closing a menu opened with selected code leaves normal Delete-at-caret editing intact");
            editor.SelectAll();

            editor.ReadOnly = true;
            await Open(true);
            Check(Item("Copy").Enabled && Item("SelectAll").Enabled
                && new[] { "Undo", "Redo", "Cut", "Paste", "Delete" }.All(name => !Item(name).Enabled),
                "read-only code permits copying while disabling every modifying context command");
            Reset("");
            await Open(true);
            Check(new[] { "Undo", "Redo", "Cut", "Copy", "Delete", "SelectAll" }.All(name => !Item(name).Enabled),
                "empty code and an empty selection disable unavailable context commands");
            menu.Close();

            // Preserve every format before touching the user's clipboard. An
            // opaque/delayed original skips physical checks rather than losing it.
            IDataObject original; var saved = new DataObject(); var retained = new List<IDisposable>();
            uint originalSequence = ImagePasteClipboardSequence(), ownedSequence = 0;
            try
            {
                original = Clipboard.GetDataObject();
                if (original != null)
                    foreach (string format in original.GetFormats(false))
                    {
                        // A bitmap from another process comes only through the converting read; it goes back as the one Bitmap format.
                        if (format == DataFormats.Bitmap || format == typeof(Bitmap).FullName)
                        {
                            if (saved.GetDataPresent(DataFormats.Bitmap)) continue;
                            // a plain CF_BITMAP from a Win32 write comes only through GetImage, as the report window knows
                    Image kept = (original.GetData(DataFormats.Bitmap) as Image ?? Clipboard.GetImage())?.Clone() as Image ?? throw new InvalidOperationException("Unavailable clipboard format: " + format);
                            saved.SetData(DataFormats.Bitmap, true, kept); retained.Add(kept); continue;
                        }
                        object value = original.GetData(format, false) ?? throw new InvalidOperationException("Unavailable clipboard format: " + format);
                        value = value switch
                        {
                            MemoryStream memory => new MemoryStream(memory.ToArray()),
                            Image image => image.Clone(),
                            byte[] bytes => bytes.ToArray(),
                            string[] strings => strings.ToArray(),
                            string or bool or char or int or long or float or double or decimal => value,
                            _ => throw new InvalidOperationException("Unsupported clipboard format: " + format)
                        };
                        saved.SetData(format, false, value);
                        if (value is IDisposable resource) retained.Add(resource);
                    }
            }
            catch (Exception ex)
            {
                foreach (var resource in retained) resource.Dispose();
                Console.WriteLine("SKIP physical code context-menu clipboard checks: could not preserve the existing clipboard (" + ex.Message + ").");
                return;
            }
            bool OwnsClipboard() => ImagePasteClipboardSequence() == (ownedSequence == 0 ? originalSequence : ownedSequence);
            async Task WriteClipboard(IDataObject data)
            {
                for (int attempt = 0; ; attempt++)
                    try { Clipboard.SetDataObject(data, true, 0, 0); ownedSequence = ImagePasteClipboardSequence(); return; }
                    catch (ExternalException) when (attempt < 40) { await Task.Delay(50); }
            }
            try
            {
                if (!OwnsClipboard()) { Console.WriteLine("SKIP code context-menu clipboard checks: another application changed the clipboard."); return; }
                const string replacement = "change_β";
                await WriteClipboard(new DataObject(DataFormats.UnicodeText, replacement));
                Reset("before <- 1\nafter <- 2"); editor.Select(0, 6);
                await Open(true, true);
                Check(Item("Paste").Enabled, "Unicode text on the clipboard enables context Paste");
                if (!OwnsClipboard()) { Console.WriteLine("SKIP code context-menu clipboard checks: another application changed the clipboard."); return; }
                Click("Paste");
                Check(editor.Text == replacement + " <- 1\nafter <- 2", "context Paste replaces selected R code after output focus without running it");
                await Open(true); Click("Undo");
                Check(editor.Text == "before <- 1\nafter <- 2", "context Undo restores code replaced by Paste");
                await Open(true); Click("Redo");
                Check(editor.Text.StartsWith(replacement, StringComparison.Ordinal), "context Redo restores pasted Unicode code");
                editor.Select(0, replacement.Length);
                await Open(false, true);
                if (!OwnsClipboard()) { Console.WriteLine("SKIP code context-menu clipboard checks: another application changed the clipboard."); return; }
                Click("Copy"); ownedSequence = ImagePasteClipboardSequence();
                Check(Clipboard.GetText() == replacement && editor.SelectedText == replacement,
                    "context Copy writes selected code rather than the selected output and preserves the code selection");
                await Open(true, true);
                if (!OwnsClipboard()) { Console.WriteLine("SKIP code context-menu clipboard checks: another application changed the clipboard."); return; }
                Click("Cut"); ownedSequence = ImagePasteClipboardSequence();
                Check(editor.Text == " <- 1\nafter <- 2" && Clipboard.GetText() == replacement,
                    "context Cut copies and removes selected code through the editor");
                await Open(true); Click("Undo");
                Check(editor.Text.StartsWith(replacement, StringComparison.Ordinal), "context Undo restores cut R code");
                if (!OwnsClipboard()) { Console.WriteLine("SKIP remaining code context-menu clipboard checks: another application changed the clipboard."); return; }
                var nonText = new DataObject(); nonText.SetData("StatsDirect.ContextMenuCheck", false, new byte[] { 1, 2, 3 });
                await WriteClipboard(nonText);
                await Open(true);
                Check(!Item("Paste").Enabled, "clipboard content without script text disables context Paste");
            }
            finally
            {
                menu.Close();
                if (ownedSequence != 0 && ImagePasteClipboardSequence() == ownedSequence)
                {
                    if (original == null || saved.GetFormats(false).Length == 0)
                        for (int attempt = 0; ; attempt++)
                            try { Clipboard.Clear(); break; } catch (ExternalException) when (attempt < 40) { await Task.Delay(50); }
                    else await WriteClipboard(saved);
                }
                foreach (var resource in retained) resource.Dispose();
            }
        }
        finally
        {
            menu.Close(); script.Dispose();
            Check(menu.IsDisposed, "closing the R script form disposes its context menu");
        }
    }
}
