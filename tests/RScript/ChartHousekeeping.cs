using System.Reflection;
using StatsDirect.R;

internal static partial class Program
{
    // The automatic chart files of R runs, Rplot-<run>-<device>-<page>.svg, are removed from the R folder once a week old; files
    // named by the user or by R itself, and recent automatic charts, stay.
    private static void CheckChartHousekeeping()
    {
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-housekeeping-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        DateTime now = new(2026, 10, 18, 9, 0, 0);
        void Write(string name, double daysOld) { string path = Path.Combine(folder, name); File.WriteAllText(path, "<svg/>"); File.SetLastWriteTime(path, now.AddDays(-daysOld)); }
        string run = Guid.NewGuid().ToString("N"), other = Guid.NewGuid().ToString("N");
        Write($"Rplot-{run}-1-001.svg", 8); Write($"Rplot-{run}-1-002.svg", 7); Write($"Rplot-{other}-2-001.svg", 6.9); Write($"Rplot-{other}-1-001.svg", 0.5);
        Write("Rplot001.svg", 30); Write("Rplot-mine.svg", 30); Write("agreement.svg", 30); Write($"Rplot-{run}-1-001.pdf", 30);
        // the console's own files: plot snapshots, device pages, file copies and previews, and plot indexes
        Write($"console-{run}-1-plot-1-rev-1.svg", 8); Write($"console-{run}-1-device-1-001.svg", 8); Write($"console-{run}-1-file-1.png", 8);
        Write($"console-{run}-1-file-1-preview.png", 8); Write($"console-plots-{run}-1.tsv", 8);
        Write($"console-{other}-1-plot-1-rev-1.svg", 1); Write($"console-plots-{other}-1.tsv", 1); Write("console-notes.txt", 30);
        MethodInfo housekeep = typeof(RController).GetMethod("HousekeepAutomaticCharts", BindingFlags.Static | BindingFlags.NonPublic);
        housekeep.Invoke(null, new object[] { folder, now });
        var left = Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Check(!left.Contains($"Rplot-{run}-1-001.svg") && !left.Contains($"Rplot-{run}-1-002.svg"), "automatic chart files a week old or more are removed");
        Check(left.Contains($"Rplot-{other}-2-001.svg") && left.Contains($"Rplot-{other}-1-001.svg"), "recent automatic chart files stay");
        Check(left.Contains("Rplot001.svg") && left.Contains("Rplot-mine.svg") && left.Contains("agreement.svg") && left.Contains($"Rplot-{run}-1-001.pdf"),
              "files named by R, by the user, or of other formats are not touched however old");
        Check(!left.Any(n => n.StartsWith("console-" + run) || n == $"console-plots-{run}-1.tsv") && left.Contains($"console-{other}-1-plot-1-rev-1.svg") && left.Contains($"console-plots-{other}-1.tsv") && left.Contains("console-notes.txt"),
              "the console's snapshot, device, file-copy, preview and index files a week old or more are removed, recent ones and a user's console-named file stay");
        housekeep.Invoke(null, new object[] { Path.Combine(folder, "absent"), now });
        Check(true, "housekeeping of a folder that does not exist is a no-op");
        Directory.Delete(folder, true);
    }
}
