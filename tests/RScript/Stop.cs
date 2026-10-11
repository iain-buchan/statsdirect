using System.Diagnostics;
using StatsDirect.R;

internal static partial class Program
{
    // A run of a script with the installed R can be stopped: Run in the script window while the previous run is still going stops that
    // run and starts the edited script, and closing the window stops a run.  The stop must end the R process at once, not wait for it,
    // and a run that is not stopped must return what R printed.
    private static void CheckStop()
    {
        RVersion version = RController.PreferredRVersion();
        Check(version != null, "the installed R is found for the run");
        Stopwatch watch = Stopwatch.StartNew();
        string output = RController.RunScriptCapturingOutput("cat('before\\n')\nSys.sleep(60)\ncat('after\\n')\n", version, new CancellationTokenSource(TimeSpan.FromSeconds(3)).Token);
        watch.Stop();
        Console.WriteLine($"      a stopped run returned after {watch.Elapsed.TotalSeconds:F1} s: {output.Replace("\n", " | ")}");
        Check(watch.Elapsed < TimeSpan.FromSeconds(20) && output.Contains("(stopped)") && !output.Contains("after"), "a run stopped while R sleeps returns within seconds, marked stopped, without what would have followed");
        output = RController.RunScriptCapturingOutput("cat('done\\n')\n", version, CancellationToken.None);
        Check(output.Contains("done") && !output.Contains("(stopped)"), "a run that is not stopped returns what R printed, not marked stopped");
        // the session look: each command after R's prompt with its output under it, and an error in its place, not gathered at the end
        string echoed = RController.RunScriptCapturingOutput("x <- 1:3\nx * 2\ncat('a\\n')\nstop('oops')\ncat('b\\n')\n", version, CancellationToken.None, echo: true);
        echoed = echoed.Replace("\r", "");   // R on Windows ends its lines with a carriage return too
        int command = echoed.IndexOf("> x * 2", StringComparison.Ordinal), value = echoed.IndexOf("[1] 2 4 6", StringComparison.Ordinal), a = echoed.IndexOf("\na\n", StringComparison.Ordinal), error = echoed.IndexOf("Error", StringComparison.Ordinal);
        Check(echoed.Contains("> x <- 1:3") && command >= 0 && value > command && a > value && error > a && !echoed.Contains("\nb\n") && !echoed.Contains("userdir"),
              "run as a session, the script shows each command after R's prompt with its output under it, an error in its place, and none of the run's own head");
        // Open in R without RStudio: R's own window, started on a profile that runs the script as a session and opens it in R's editor
        string profile = RController.OpenInRProfile(@"C:\Users\someone\Documents\StatsDirect\R\Script 1_ Paired t test.R");
        string profileText = File.ReadAllText(profile);
        Check(File.Exists(Path.Combine(version.BinPath, "Rgui.exe")) && profileText.Contains(".libPaths(c(rlib, .libPaths()))")
              && profileText.Contains("f <- \"C:/Users/someone/Documents/StatsDirect/R/Script 1_ Paired t test.R\"") && profileText.Contains("try(source(f, echo = TRUE") && profileText.Contains("if (interactive()) file.edit(f)"),
              "the installed R has its own window program, and the profile Open in R starts it on runs the script as a session and opens it in R's editor, with the program's library on the path");
        // the profile runs before R attaches its default packages: a script using the stats package must still run from it (Iain's R
        // window said it could not find t.test); R run headlessly on the profile shows the script as a session and its result
        string folder = Path.Combine(Path.GetTempPath(), "statsdirect-rscript-checks", "open-in-r");
        Directory.CreateDirectory(folder);
        string statsScript = Path.Combine(folder, "stats.R");
        File.WriteAllText(statsScript, "r <- t.test(c(1, 2, 3), mu = 0)\ncat(\"p\", format(r$p.value, digits = 4), \"\n\")\n");
        string statsProfile = RController.OpenInRProfile(statsScript);
        System.Diagnostics.ProcessStartInfo start = new()
        {
            FileName = Path.Combine(version.BinPath, "Rscript.exe"), Arguments = "-e \"cat('end\n')\"", WorkingDirectory = folder,
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        };
        start.Environment["R_PROFILE_USER"] = statsProfile;
        using System.Diagnostics.Process r = System.Diagnostics.Process.Start(start);
        string viaProfile = r.StandardOutput.ReadToEnd() + r.StandardError.ReadToEnd();
        r.WaitForExit();
        viaProfile = viaProfile.Replace("\r", "");
        Check(viaProfile.Contains("> r <- t.test(c(1, 2, 3), mu = 0)") && viaProfile.Contains("p 0.07418") && viaProfile.Contains("end") && !viaProfile.Contains("could not find function"),
              "R started on the profile has its default packages attached before the script runs, shows the script as a session, and gives its result");
    }
}
