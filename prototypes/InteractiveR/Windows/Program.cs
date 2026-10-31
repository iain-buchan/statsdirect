namespace StatsDirect.InteractiveR;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try { Application.Run(new MainForm(PrototypeOptions.Parse(args))); }
        catch (Exception error) { MessageBox.Show(error.Message, "Interactive R prototype", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}

internal sealed record PrototypeOptions(string NativeHelper, string RHome, string WorkingDirectory, string? Script, string? UiSmokeResult)
{
    internal static PrototypeOptions Parse(string[] args)
    {
        string? Value(string option)
        {
            int index = Array.IndexOf(args, option);
            if (index < 0) return null;
            if (index + 1 >= args.Length) throw new ArgumentException($"A value is required after {option}.");
            return args[index + 1];
        }
        string installationFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "R");
        string rHome = Value("--r-home") ?? Environment.GetEnvironmentVariable("R_HOME") ??
            (Directory.Exists(installationFolder) ? Directory.EnumerateDirectories(installationFolder, "R-*") : [])
                .Where(path => File.Exists(Path.Combine(path, "bin", "x64", "R.dll")))
                .OrderByDescending(path => Version.TryParse(Path.GetFileName(path)[2..], out var version) ? version : new Version()).FirstOrDefault()
            ?? throw new FileNotFoundException("R was not found. Supply --r-home with the installed R folder.");
        string helper = Value("--native-helper") ?? Path.Combine(AppContext.BaseDirectory, "StatsDirect.RConsole.Native.exe");
        string folder = Value("--working-directory") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StatsDirect", "InteractiveRPrototype", "Sessions", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return new(Path.GetFullPath(helper), Path.GetFullPath(rHome), Path.GetFullPath(folder), Value("--script"), Value("--ui-smoke"));
    }
}
