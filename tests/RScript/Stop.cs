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
    }
}
