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
using System.Linq;
using System.Text;

namespace StatsDirect.R
{
    class RController
    {
        const string RSCRIPT_EXE_NAME = "Rscript.exe";
        const string RSCRIPT_NAME = "script.r";
        const string RESULTS_FILE_NAME = "results.txt";
        const string SCRIPT_HEAD = "userdir<-\"{0}\"\nlibdir<-\"Lib\"\nrlib=file.path(userdir, libdir)\ndir.create(rlib,recursive=T,showWarnings=F)\nsetwd(file.path(userdir))\n.libPaths(rlib)\nzz <- file(\"warn.txt\", open = \"wt\")\nsink(zz, type = \"message\")";
        /// <summary>
        /// Checks whether R is installed and, if so, what versions.
        /// </summary>
        public static List<RVersion> CheckR()
        {
            string[] rLocations = new string[] { @"Software\R-core\R", @"Software\R-core\R64" };
            List<RVersion> installedVersions = new List<RVersion>();
            try
            {
                RegistryKey hklm = Registry.LocalMachine;
                foreach (string rLocation in rLocations)
                {
                    RegistryKey rKey = hklm.OpenSubKey(rLocation);
                    foreach (string version32 in rKey.GetSubKeyNames())
                    {
                        RegistryKey versionKey = rKey.OpenSubKey(version32);
                        object installPathObject = versionKey.GetValue("InstallPath");
                        if (null != installPathObject)
                        {
                            bool isX64 = rLocation.EndsWith("64");
                            string installPath = (string)installPathObject;
                            RVersion version = new RVersion { IsX64 = isX64, Version = version32, InstallPath = installPath };
                            // Probe for a binary there to check it's still around and hasn't been uninstalled/deleted
                            string binaryPath = Path.Combine(version.BinPath, "Rscript.exe");
                            if (File.Exists(binaryPath))
                            {
                                installedVersions.Add(version);
                            }
                        }
                        versionKey.Close();
                    }
                    rKey.Close();
                }
                hklm.Close();
            }
            catch (Exception)
            {
                // Return whatever we have
            }
            return installedVersions;
        }

        /// <summary>
        /// Rules: Prefer highest version, then highest bitness
        /// </summary>
        /// <param name="candidates"></param>
        /// <returns></returns>
        public static RVersion PreferredRVersion(ICollection<RVersion> candidates)
        {
            RVersion preferred = null;
            foreach (RVersion candidate in candidates)
            {
                if (null == preferred)
                    preferred = candidate;
                else
                {
                    // Parse candidate version, expect x.y.z
                    int candidateMajor;
                    int candidateMinor;
                    int candidate3;
                    string[] candidateSplit = candidate.Version.Split('.');
                    if (candidateSplit.Length != 3)
                        continue;
                    if (!int.TryParse(candidateSplit[0], out candidateMajor))
                        continue;
                    if (!int.TryParse(candidateSplit[1], out candidateMinor))
                        continue;
                    if (!int.TryParse(candidateSplit[2], out candidate3))
                        continue;

                    // Parse preferred version, expect x.y.z
                    int preferredMajor;
                    int preferredMinor;
                    int preferred3;
                    string[] preferredSplit = preferred.Version.Split('.');
                    if (preferredSplit.Length != 3)
                        continue;
                    if (!int.TryParse(preferredSplit[0], out preferredMajor))
                        continue;
                    if (!int.TryParse(preferredSplit[1], out preferredMinor))
                        continue;
                    if (!int.TryParse(preferredSplit[2], out preferred3))
                        continue;

                    if (preferredMajor > candidateMajor)
                        continue;
                    if (preferredMinor > candidateMinor)
                        continue;
                    if (preferred3 > candidate3)
                        continue;

                    if (preferredMajor == candidateMajor && preferredMinor == candidateMinor && preferred3 == candidate3)
                    {
                        // Check bitness
                        if (preferred.IsX64 && !candidate.IsX64)
                            continue;
                    }

                    // If we get here, the candidate is of a greater version or of the same version but greater bitness.
                    preferred = candidate;
                }
            }
            return preferred;
        }

        /// <returns> <code>true</code> if the script appears to have been run successfully, <code>false</code> otherwise.</returns>
        public static Process RunScriptAndQuit(ITemplateHost host, string scriptBody)
        {
            string rFolder = SDConfiguration.MyStatsDirectRFolder;
            // Just in case this is the first time the user has run an R script.  TODO: Is there a more sensible place for this?
            if (!Directory.Exists(rFolder))
                Directory.CreateDirectory(rFolder);
            string scriptPath = Path.Combine(rFolder, RSCRIPT_NAME);

            using (TextWriter tw = new StreamWriter(scriptPath, false, Encoding.ASCII))
            {
                tw.Write(SCRIPT_HEAD, rFolder.Replace(@"\", @"\\"));
                tw.WriteLine();
                tw.WriteLine(scriptBody);
                tw.WriteLine("quit()");
            }

            // TODO: Probably don't do this per-script in the future.
            RVersion preferredVersion = PreferredRVersion(CheckR());
            while (null == preferredVersion)
            {
                if (!UserMightHaveInstalledR())
                    throw new TemplateOperationCancelledException();
                preferredVersion = PreferredRVersion(CheckR());
            }

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = Path.Combine(preferredVersion.BinPath, RSCRIPT_EXE_NAME);
            startInfo.Arguments = string.Format("--vanilla \"{0}\"", scriptPath);
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            return Process.Start(startInfo);
        }

        /// <summary>
        /// If necessary, prompt the user to install R.  Return true if we think the user might have installed R successfully, or false if there's no chance (for example, the user's told us that they're not going to)
        /// </summary>
        /// <returns></returns>
        private static bool UserMightHaveInstalledR()
        {
            using (frmInstallR f = new frmInstallR())
            {
                f.ShowDialog(SdApplication.SoleInstance.MainWindow);
                return f.UserThinksRIsInstalled;
            }
        }

        internal static ParameterBag FilesToParameterBag()
        {
            string rFolder = SDConfiguration.MyStatsDirectRFolder;
            string resultsPath = Path.Combine(rFolder, RESULTS_FILE_NAME);
            using (Stream s = File.OpenRead(resultsPath))
            {
                AntlrInputStream input = new AntlrInputStream(s);
                RResultsLexer lexer = new RResultsLexer(input);
                CommonTokenStream tokenStream = new CommonTokenStream(lexer);
                RResultsParser parser = new RResultsParser(tokenStream);
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
        }

        private static ParameterBag DictionaryToParameterBag(Dictionary<string, object> dictionary)
        {
            ParameterBag outputParameters = new ParameterBag();
            foreach (KeyValuePair<string, object> pair in dictionary)
                if (pair.Value is List<object>)
                    outputParameters.AddOutput(pair.Key, RConvert.ToFrame(pair.Key, (List<object>)pair.Value));
                else
                    outputParameters.AddOutput(pair.Key, pair.Value);
            return outputParameters;
        }
    }

    public class RVersion
    {
        public string InstallPath { get; set; }
        public string Version { get; set; }
        public bool IsX64 { get; set; }

        public string BinPath
        {
            get { return Path.Combine(InstallPath, "bin", IsX64 ? "x64" : "i386"); }
        }

        public string GuiPath
        {
            get { return Path.Combine(BinPath, "Rgui.exe"); }
        }
    }
}
