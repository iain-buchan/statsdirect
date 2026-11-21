using System.Diagnostics;
using System.Text.RegularExpressions;
using StatsDirect.R;
using StatsDirect.Configuration;

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
        // runs going at the same time, as several script windows run their scripts at once: each returns its own script's output (the
        // runs had shared two file names, so one could read another's script, or fail on the file the other R held)
        string[] parallel = Task.WhenAll(Enumerable.Range(1, 4).Select(i => Task.Run(() => RController.RunScriptCapturingOutput($"Sys.sleep(0.5)\ncat('run {i} says {i * 11}\\n')\n", version, CancellationToken.None, echo: i % 2 == 0)))).Result;
        Check(parallel.Select((o, i) => o.Contains($"run {i + 1} says {(i + 1) * 11}") && !Regex.IsMatch(o, $"says (?!{(i + 1) * 11}\\b)\\d")).All(ok => ok) && !Directory.EnumerateFiles(SDConfiguration.MyStatsDirectRFolder, "continue-*.r").Any(),
              "four runs at once each return their own script's output, not another's, and leave no run files behind");
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
              && profileText.Contains("f <- \"C:/Users/someone/Documents/StatsDirect/R/Script 1_ Paired t test.R\"") && profileText.Contains("try(source(exprs = parse(text = lines")
              && profileText.Contains("echo = TRUE") && profileText.Contains("if (interactive()) file.edit(f)"),
              "the installed R has its own window program, and the profile Open in R starts it on runs the script as a session and opens it in R's editor, with the program's library on the path");
        // Each R gets a start-up file of its own (R+ used twice in quick succession must not give the first R the second script).
        string otherProfile = RController.OpenInRProfile(@"C:\Users\someone\Documents\StatsDirect\R\Script 2_ Paired t test.R");
        Check(!string.Equals(profile, otherProfile, StringComparison.OrdinalIgnoreCase) && Path.GetDirectoryName(profile) != Path.GetDirectoryName(otherProfile)
              && File.ReadAllText(profile).Contains("Script 1_ Paired t test.R") && File.ReadAllText(otherProfile).Contains("Script 2_ Paired t test.R"),
              "each R started for Open in R has a start-up file of its own, in a folder of its own, and a second does not change the first");
        foreach (string written in new[] { profile, otherProfile })
            Directory.Delete(Path.GetDirectoryName(written)!, true);
        // the profile runs before R attaches its default packages: a script using the stats package must still run from it (Iain's R
        // window said it could not find t.test); R run headlessly on the profile shows the script as a session and its result
        string folder = Path.Combine(TemporaryRoot, "statsdirect-rscript-checks", "open-in-r");
        Directory.CreateDirectory(folder);
        string statsScript = Path.Combine(folder, "stats.R");
        // a comment with quotes that a C locale lacks, as a worksheet script's first line has
        File.WriteAllText(statsScript, "# Data copied from the worksheet \u201CSheet1\u201D, A1:A3.\nr <- t.test(c(1, 2, 3), mu = 0)\ncat(\"p\", format(r$p.value, digits = 4), \"\n\")\n",
            new System.Text.UTF8Encoding(false));
        // RULES.md 6.1, as for the worksheet's R+: the user's own start-up file (here ./.Rprofile) runs first, and R processes started
        // from this R do not inherit the profile, so they do not run the script again.
        File.WriteAllText(Path.Combine(folder, ".Rprofile"), "options(statsdirect.own.profile = TRUE)\n");
        string statsProfile = RController.OpenInRProfile(statsScript);
        System.Diagnostics.ProcessStartInfo start = new()
        {
            FileName = Path.Combine(version.BinPath, "Rscript.exe"), WorkingDirectory = folder,
            Arguments = "-e \"cat('end', nzchar(Sys.getenv('R_PROFILE_USER')), isTRUE(getOption('statsdirect.own.profile')), '\\n')\"",
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        };
        start.Environment["R_PROFILE_USER"] = statsProfile;
        using System.Diagnostics.Process r = System.Diagnostics.Process.Start(start);
        string viaProfile = r.StandardOutput.ReadToEnd() + r.StandardError.ReadToEnd();
        r.WaitForExit();
        viaProfile = viaProfile.Replace("\r", "");
        Check(viaProfile.Contains("> r <- t.test(c(1, 2, 3), mu = 0)") && viaProfile.Contains("p 0.07418") && viaProfile.Contains("end") && !viaProfile.Contains("could not find function"),
              "R started on the profile has its default packages attached before the script runs, shows the script as a session, and gives its result");
        Check(viaProfile.Contains("end FALSE TRUE"),
              "R started on the profile has run the user's own start-up file, and R processes started from it do not inherit the profile");
        // In a locale without the comment's quotes the script still runs: it is read as UTF-8, not converted to the locale first.
        start.Environment["LC_ALL"] = "C";
        using (System.Diagnostics.Process inC = System.Diagnostics.Process.Start(start))
        {
            string inLocaleC = (inC.StandardOutput.ReadToEnd() + inC.StandardError.ReadToEnd()).Replace("\r", "");
            inC.WaitForExit();
            Check(inLocaleC.Contains("p 0.07418") && !inLocaleC.Contains("invalid input"),
                  "R started on the profile in the C locale still runs a script whose comment holds characters that locale lacks: " + inLocaleC.Trim());
        }
        Directory.Delete(Path.GetDirectoryName(statsProfile)!, true);
        // ...and with an R folder whose name the C locale lacks (Codex's second review): the start-up file writes the folder with escapes,
        // so R still reads it, makes the program's library there and works in it.
        string unicodeFolder = Path.Combine(TemporaryRoot, "statsdirect-rscript-checks", "profile-\u017D\u75C5", "R");
        Directory.CreateDirectory(unicodeFolder);
        string unicodeProfile = RController.OpenInRProfile(statsScript, unicodeFolder);
        System.Diagnostics.ProcessStartInfo unicodeStart = new()
        {
            FileName = Path.Combine(version.BinPath, "Rscript.exe"), WorkingDirectory = folder,
            Arguments = "-e \"cat('wd', dir.exists(file.path(getwd(), 'Lib')), '\\n')\"",
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        };
        unicodeStart.Environment["R_PROFILE_USER"] = unicodeProfile;
        unicodeStart.Environment["LC_ALL"] = "C";
        using (System.Diagnostics.Process inUnicode = System.Diagnostics.Process.Start(unicodeStart))
        {
            string unicodeOutput = (inUnicode.StandardOutput.ReadToEnd() + inUnicode.StandardError.ReadToEnd()).Replace("\r", "");
            inUnicode.WaitForExit();
            Check(inUnicode.ExitCode == 0 && unicodeOutput.Contains("p 0.07418") && unicodeOutput.Contains("wd TRUE") && Directory.Exists(Path.Combine(unicodeFolder, "Lib")),
                  "R started on the profile in the C locale, with an R folder whose name that locale lacks, works in that folder and runs the script: " + unicodeOutput.Trim());
        }
        Directory.Delete(Path.GetDirectoryName(unicodeProfile)!, true);
    }
}
