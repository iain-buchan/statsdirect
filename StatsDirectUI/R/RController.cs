using Antlr4.Runtime;
using Microsoft.Win32;
using StatsDirect.Configuration;
using StatsDirect.Templates;
using StatsDirect.UI;
using StatsDirect.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace StatsDirect.R
{
    public static class RController
    {
        const string RSCRIPT_EXE_NAME = "Rscript.exe";
        const string RSCRIPT_NAME = "script.r";
        const string RESULTS_FILE_NAME = "results.txt";
        const string ERROR_FILE_NAME = "error.txt";
        const string SCRIPT_HEAD = "userdir<-\"{0}\"\r\nlibdir<-\"Lib\"\r\nrlib=file.path(userdir, libdir)\r\ndir.create(rlib,recursive=T,showWarnings=F)\r\nsetwd(file.path(userdir))\r\n.libPaths(c(rlib, .libPaths()))";
        const string STATSDIRECT_HEAD = "zz <- file(\"{0}\", open = \"wt\")\r\nsink(zz, type = \"message\")\r\nreturning.to.statsdirect <- TRUE";
        /// <summary>
        /// Checks whether R is installed and, if so, what versions.
        /// </summary>
        public static List<RVersion> CheckR()
        {
            string[] rLocations = { @"Software\R-core\R", @"Software\R-core\R64" };
            List<RVersion> installedVersions = new();
            foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                foreach (string rLocation in rLocations)
                {
                    try
                    {
                        using RegistryKey root = RegistryKey.OpenBaseKey(hive, view);
                        using RegistryKey rKey = root.OpenSubKey(rLocation);
                        if (rKey == null) continue;
                        foreach (string versionName in rKey.GetSubKeyNames())
                        {
                            using RegistryKey versionKey = rKey.OpenSubKey(versionName);
                            AddInstalledVersion(installedVersions, versionKey?.GetValue("InstallPath") as string, versionName);
                        }
                        // Some installations record only the current version at the root.
                        AddInstalledVersion(installedVersions, rKey.GetValue("InstallPath") as string, rKey.GetValue("Current Version") as string);
                    }
                    catch (System.Security.SecurityException) { }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }
            }
            return installedVersions;
        }

        internal static void AddInstalledVersion(List<RVersion> versions, string path, string versionName)
        {
            if (string.IsNullOrEmpty(path) || !Version.TryParse(versionName, out _)) return;
            foreach (bool x64 in new[] { true, false })
            {
                var candidate = new RVersion { InstallPath = path, VersionString = versionName, IsX64 = x64 };
                if (File.Exists(Path.Combine(candidate.BinPath, RSCRIPT_EXE_NAME)) &&
                    !versions.Exists(v => v.BinPath.Equals(candidate.BinPath, StringComparison.OrdinalIgnoreCase)))
                    versions.Add(candidate);
            }
        }

        /// <summary>
        /// Rules: Prefer highest version, then highest bitness
        /// </summary>
        /// <returns></returns>
        public static RVersion PreferredRVersion()
        {
            ICollection<RVersion> candidates = CheckR();
            RVersion preferred = null;
            foreach (RVersion candidate in candidates)
                if (candidate.CompareTo(preferred) > 0)
                    preferred = candidate;
            return preferred;
        }

        /// <param name="scriptBody"></param>
        /// <param name="reportScriptBody">A version of the script body that contains everything necessary to run the script, as plain text for the HTML report renderer.</param>
        /// <param name="host"></param>
        /// <returns> <code>true</code> if the script appears to have been run successfully, <code>false</code> otherwise.</returns>
        public static Process RunScriptAndQuit(ITemplateHost host, string scriptBody, out string reportScriptBody)
        {
            string rFolder = SDConfiguration.MyStatsDirectRFolder;
            // Just in case this is the first time the user has run an R script.  TODO: Is there a more sensible place for this?
            if (!Directory.Exists(rFolder))
                Directory.CreateDirectory(rFolder);
            string scriptPath = Path.Combine(rFolder, RSCRIPT_NAME);

            // We get a right mix of terminations at this point; we need Windows newlines in order to match the rest of the file format and meet the requirement to be openable in Notepad.
            string repairedScriptBody = scriptBody
                .Replace("\r", string.Empty)
                .Replace("\n", "\r\n");
            reportScriptBody = string.Format(SCRIPT_HEAD, rFolder.Replace(@"\", @"\\")) + "\r\n" + repairedScriptBody;

            using (TextWriter tw = new StreamWriter(scriptPath, false, new UTF8Encoding(false)))
            {
                tw.Write(SCRIPT_HEAD, rFolder.Replace(@"\", @"\\"));
                tw.WriteLine();
                tw.Write(STATSDIRECT_HEAD, ERROR_FILE_NAME);
                tw.WriteLine();
                tw.WriteLine(repairedScriptBody);
                tw.WriteLine("quit()");
            }

            string errorFilePath = Path.Combine(rFolder, ERROR_FILE_NAME);
            if (File.Exists(errorFilePath))
                File.Delete(errorFilePath);

            // TODO: Probably don't do this per-script in the future.
            RVersion preferredVersion = PreferredRVersion();
            while (null == preferredVersion)
            {
                if (!UserMightHaveInstalledR())
                    throw new TemplateOperationCancelledException();
                preferredVersion = PreferredRVersion();
            }

            ProcessStartInfo startInfo = new()
            {
                FileName = Path.Combine(preferredVersion.BinPath, RSCRIPT_EXE_NAME),
                WorkingDirectory = rFolder,
                Arguments = $"--vanilla --encoding=UTF-8 \"{scriptPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            //  Rscript.exe is not DPI-aware, so on a scaled display Windows tells it the screen is 96 dpi while the metafile it records is framed against the
            //  display's true resolution: at 225% R draws its chart into the top left 40% of the picture and leaves the rest blank.  This compatibility layer
            //  makes Windows give R the real figures, so the drawing fills its frame.  Any layers already set for this process are kept.
            const string compatLayerVariable = "__COMPAT_LAYER";
            const string highDpiAwareLayer = "HighDpiAware";
            //  (TryGetValue, because the indexer throws when the variable is not set, which is the normal case.)
            startInfo.Environment.TryGetValue(compatLayerVariable, out string existingLayers);
            if (string.IsNullOrWhiteSpace(existingLayers))
                startInfo.Environment[compatLayerVariable] = highDpiAwareLayer;
            else if (existingLayers.IndexOf(highDpiAwareLayer, StringComparison.OrdinalIgnoreCase) < 0)
                startInfo.Environment[compatLayerVariable] = existingLayers + " " + highDpiAwareLayer;

            return Process.Start(startInfo);
        }

        /// <summary>
        /// Runs an R script with the installed R and returns what it printed, as the script window shows it: the script of a result that
        /// the user continues in R.  The working folder is the user's R folder, so the files a script writes, charts among them, land there.
        /// </summary>
        public static string RunScriptCapturingOutput(string scriptBody)
        {
            RVersion preferredVersion = PreferredRVersion();
            while (null == preferredVersion)
            {
                if (!UserMightHaveInstalledR())
                    throw new TemplateOperationCancelledException();
                preferredVersion = PreferredRVersion();
            }
            return RunScriptCapturingOutput(scriptBody, preferredVersion);
        }

        /// <summary>
        /// The same with the R to use already found, so that the run can go on a background thread: finding R may ask the user to install it.
        /// A stop asked for through the token ends the R process; the run then returns "(stopped)" with whatever R had printed.
        /// </summary>
        /// <param name="echo">As an R session shows a script: each command after R's prompt, its output under it, and messages and errors in
        /// their place rather than gathered at the end.  Without it the script runs as a batch and prints only what it prints.</param>
        public static string RunScriptCapturingOutput(string scriptBody, RVersion preferredVersion, System.Threading.CancellationToken stop = default, bool echo = false)
            => RunScriptCapturingOutput(scriptBody, preferredVersion, stop, echo, Guid.NewGuid());

        /// <summary>The script window supplies the run identity so it can recognise this run's automatic charts among concurrent runs.</summary>
        internal static string RunScriptCapturingOutput(string scriptBody, RVersion preferredVersion, System.Threading.CancellationToken stop, bool echo, Guid runId)
        {
            string rFolder = SDConfiguration.MyStatsDirectRFolder;
            if (!Directory.Exists(rFolder))
                Directory.CreateDirectory(rFolder);
            // A random identity stays unique across concurrent runs, app restarts and processes whose Windows IDs are reused.
            string scriptPath = Path.Combine(rFolder, "continue-" + runId.ToString("N") + ".r");
            string sourcePath = Path.Combine(rFolder, "script-" + runId.ToString("N") + ".r");
            string repairedScriptBody = scriptBody.Replace("\r", string.Empty).Replace("\n", "\r\n");
            if (echo)
            {
                // the script in a file of its own, sourced with echo by the run's head, so that the head's own lines do not show
                using (TextWriter ts = new StreamWriter(sourcePath, false, new UTF8Encoding(false)))
                    ts.WriteLine(repairedScriptBody);
                repairedScriptBody = "sink(stdout(), type = \"message\")\r\nsource(\"" + sourcePath.Replace('\\', '/') + "\", echo = TRUE, max.deparse.length = Inf, spaced = FALSE, encoding = \"UTF-8\")";
            }
            using (TextWriter tw = new StreamWriter(scriptPath, false, new UTF8Encoding(false)))
            {
                tw.Write(SCRIPT_HEAD, rFolder.Replace(@"\", @"\\"));
                tw.WriteLine();
                // Ordinary R plots use its standard 7 by 7 inch PDF device, with every page retained in one file per device.
                // The run/device identity preserves earlier charts and links; the page placeholder also supports onefile=FALSE.
                // Explicit devices in the script and the separate SVG report-chart path keep their own formats.
                // dev.new() keeps only arguments named in the device's formals, so expose pdf's formals instead of just (...).
                // Forward only supplied arguments: pdf itself resolves its defaults, including those depending on other arguments.
                tw.WriteLine($$"""
                    options(device = local({
                      n <- 0
                      device <- function() {
                        n <<- n + 1
                        args <- mget(as.character(names(as.list(match.call())[-1L])), envir = environment(), inherits = FALSE)
                        if (!"file" %in% names(args))
                          args$file <- file.path(userdir, sprintf("Rplot-{{runId:N}}-%d-%%03d.pdf", n))
                        do.call(grDevices::pdf, args)
                      }
                      formals(device) <- formals(grDevices::pdf)
                      device
                    }))
                    """);
                tw.WriteLine(repairedScriptBody);
            }
            try
            {
                ProcessStartInfo startInfo = new()
                {
                    FileName = Path.Combine(preferredVersion.BinPath, RSCRIPT_EXE_NAME),
                    WorkingDirectory = rFolder,
                    Arguments = $"--vanilla --encoding=UTF-8 \"{scriptPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                using Process p = Process.Start(startInfo);
                // a stop kills R and waits for it to go, so that a run started next can write the script file afresh
                using System.Threading.CancellationTokenRegistration stopping = stop.Register(() => { try { p.Kill(true); p.WaitForExit(3000); } catch (Exception) { } });
                System.Threading.Tasks.Task<string> errors = p.StandardError.ReadToEndAsync();
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                if (stop.IsCancellationRequested)
                    return output + "\n(stopped)";
                StringBuilder result = new(output);
                if (errors.Result.Length > 0)
                    result.Append('\n').Append(errors.Result);
                if (0 != p.ExitCode)
                    result.Append("\n(R stopped with exit code ").Append(p.ExitCode).Append(')');
                result.Append("\n(files the script writes are in ").Append(rFolder).Append(')');
                return result.ToString();
            }
            finally
            {
                // the run's files go when it is over; a file R still holds (a stop not yet complete) is left behind
                foreach (string file in new[] { scriptPath, sourcePath })
                    try { if (File.Exists(file)) File.Delete(file); } catch (IOException) { }
            }
        }

        /// <summary>The profile R's own window runs at its start for Open in R: the program's library on the path as the runs here have it, the
        /// script run in the console as a session shows it, and the script opened in R's editor.  The path of the profile written.</summary>
        public static string OpenInRProfile(string scriptPath)
        {
            string rFolder = SDConfiguration.MyStatsDirectRFolder;
            if (!Directory.Exists(rFolder))
                Directory.CreateDirectory(rFolder);
            string profile = Path.Combine(rFolder, "open-in-r.r");
            using (TextWriter tw = new StreamWriter(profile, false, new UTF8Encoding(false)))
            {
                tw.Write(SCRIPT_HEAD, rFolder.Replace(@"\", @"\\"));
                tw.WriteLine();
                // the profile runs before R attaches its default packages (stats, utils and the rest), so it attaches them itself first
                tw.WriteLine("for (p in getOption(\"defaultPackages\")) suppressPackageStartupMessages(library(p, character.only = TRUE))");
                tw.WriteLine("local({ f <- \"" + scriptPath.Replace('\\', '/') + "\"; try(source(f, echo = TRUE, max.deparse.length = Inf, spaced = FALSE, encoding = \"UTF-8\")); if (interactive()) file.edit(f) })");
            }
            return profile;
        }

        /// <summary>Opens R's own window on the script: R reads the profile above at its start, through the environment.</summary>
        public static void OpenInRGui(RVersion version, string scriptPath)
        {
            string profile = OpenInRProfile(scriptPath);
            ProcessStartInfo start = new()
            {
                FileName = Path.Combine(version.BinPath, "Rgui.exe"),
                WorkingDirectory = SDConfiguration.MyStatsDirectRFolder,
                Arguments = "--no-save --no-restore",
                UseShellExecute = false
            };
            start.Environment["R_PROFILE_USER"] = profile;
            Process.Start(start);
        }

        /// <summary>RStudio's program when it is installed in one of its usual places, else null: a user at home in R may take a script there.</summary>
        public static string FindRStudio()
        {
            string[] candidates =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "RStudio", "rstudio.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "RStudio", "bin", "rstudio.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "RStudio", "rstudio.exe"),
            };
            foreach (string candidate in candidates)
                if (File.Exists(candidate))
                    return candidate;
            return null;
        }

        /// <summary>
        /// If necessary, prompt the user to install R.  Return true if we think the user might have installed R successfully, or false if there's no chance (for example, the user's told us that they're not going to)
        /// </summary>
        /// <returns></returns>
        public static bool UserMightHaveInstalledR()
        {
            using frmInstallR f = new();
            f.ShowDialog(SdApplication.SoleInstance.DialogOwner);
            return f.UserThinksRIsInstalled;
        }

        internal static ParameterBag FilesToParameterBag()
        {
            string rFolder = SDConfiguration.MyStatsDirectRFolder;
            string resultsPath = Path.Combine(rFolder, RESULTS_FILE_NAME);
            using Stream s = File.OpenRead(resultsPath);
            AntlrInputStream input = new(s);
            RResultsLexer lexer = new(input);
            CommonTokenStream tokenStream = new(lexer);
            RResultsParser parser = new(tokenStream);
            RResultsParser.CompileUnitContext retval = parser.compileUnit();
            if (parser.NumberOfSyntaxErrors > 0)
            {
                throw new Exception("Couldn't parse file: " + parser.NumberOfSyntaxErrors + " error(s)");
            }
            // The parser seems to dislike recognising EOF (for some reason - TODO: find out why) so instead test that we're at EOF at the end of the parse
            if (!"<EOF>".Equals(parser.CurrentToken.Text))
                throw new Exception("Couldn't parse file: syntax error near \"" + parser.CurrentToken.Text + "\"");
            //if (null == retval || null == retval.builtExpression)
            //    throw new Exception("Syntax error");
            return DictionaryToParameterBag(retval.Values);
        }

        /// <summary>
        /// Split a string of the form name$name$name so that each non-leaf name becomes a dictionary named in that way inside the current dictionary, and the leaf name is returned.
        /// </summary>
        private static ParameterBag GetBag(string nameToParse, ParameterBag current, out string leafName)
        {
            int pos = nameToParse.IndexOf('$');
            if (pos < 0)
            {
                // No more $ signs; we're at a leaf
                leafName = nameToParse;
                return current;
            }

            // A branch
            string branchName = "*" + nameToParse.Substring(0, pos);
            string rhs = nameToParse.Substring(pos + 1);
            // Branches always have indexed lists of bags as immediate children
            IList<ParameterBag> child;
            if (current.ContainsKey(branchName))
            {
                child = current[branchName].AsParameterBagList;
            }
            else
            {
                child = new List<ParameterBag>();
                current.AddOutput(branchName, child);
            }
            // The next name in the list might be non-numeric (it's the name of the next bag level at index 0 of this bag) or numeric (it's the index of a bag at this level).  Find the bag, creating as necessary.
            int nextPos = rhs.IndexOf('$');
            ParameterBag subBag;
            if (nextPos < 0 || !int.TryParse(rhs.Substring(0, nextPos), out int bagIndex))
            {
                // No $: Next is a leaf; we need to put the leaf into index 0
                // $ but non-numeric: Next is a branch; we need to put the leaf into index 0
                if (child.Count == 0)
                    child.Add(new ParameterBag());
                subBag = child[0];
            }
            else
            {
                // $ and next is numeric: We need to offset to that bag in the current list, creating any bags we're missing.  Note that R indices are 1-based, C# indices are 0-based.
                while (child.Count < bagIndex)
                    child.Add(new ParameterBag());
                subBag = child[bagIndex - 1];
                rhs = rhs.Substring(nextPos + 1);
            }
            // Search down the branch
            return GetBag(rhs, subBag, out leafName);
        }

        private static ParameterBag DictionaryToParameterBag(Dictionary<string, object> dictionary)
        {
            ParameterBag outputParameters = new();
            foreach (KeyValuePair<string, object> pair in dictionary)
            {
                ParameterBag thisBag = GetBag(pair.Key, outputParameters, out string leafName);
                if (pair.Value is TitleAndValue)
                {
                    TitleAndValue tv = (TitleAndValue)pair.Value;
                    thisBag.AddOutput(leafName, RConvert.ToFrame(leafName, tv.Title, (List<object>)tv.Value));
                }
                else
                    thisBag.AddOutput(leafName, pair.Value);
            }
            return outputParameters;
        }

        /// <summary>
        /// Returns the most recent error text from R.
        /// </summary>
        /// <returns>The most recent error text, or null if no error text could be retrieved</returns>
        public static string GetErrorText()
        {
            string rFolder = SDConfiguration.MyStatsDirectRFolder;
            string errorFilePath = Path.Combine(rFolder, ERROR_FILE_NAME);
            if (!File.Exists(errorFilePath))
                return null;

            using TextReader tr = new StreamReader(errorFilePath);
            return tr.ReadToEnd().Replace("\r", string.Empty);
        }
    }
}
