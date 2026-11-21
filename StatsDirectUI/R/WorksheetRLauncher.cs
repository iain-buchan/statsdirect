#nullable enable
using StatsDirect.Configuration;
using StatsDirect.UI;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace StatsDirect.R;

internal static class WorksheetRLauncher
{
    private const string ImportFolderPrefix = "StatsDirect-worksheet-r-";
    private const string ImportScriptName = "load-worksheet.r";
    private const string ProfileName = "profile.r";

    internal static string CreateImportFolder() => Directory.CreateTempSubdirectory(ImportFolderPrefix).FullName;

    internal static RVersion? FindRGui()
    {
        var version = InstalledRGui();
        if (version == null && RController.UserMightHaveInstalledR(token => RInstallation.ReadyAsync(InstalledRGui(), token)))
            version = InstalledRGui();
        return version;
    }

    private static RVersion? InstalledRGui()
    {
        RVersion? preferred = null;
        foreach (var candidate in RController.CheckR())
            if (File.Exists(candidate.GuiPath) && candidate.CompareTo(preferred) > 0)
                preferred = candidate;
        return preferred;
    }

    internal static void Launch(RVersion version, string? script, string? importFolder)
    {
        if ((script == null) != (importFolder == null))
            throw new ArgumentException("A worksheet import requires both the script and its data folder.");

        try
        {
            string workingDirectory = SDConfiguration.MyStatsDirectRFolder;
            Directory.CreateDirectory(workingDirectory);
            var start = new ProcessStartInfo(version.GuiPath)
            {
                UseShellExecute = false,
                WorkingDirectory = workingDirectory
            };
            start.ArgumentList.Add("--no-save");
            start.ArgumentList.Add("--no-restore");

            if (script != null && importFolder != null)
            {
                File.WriteAllText(Path.Combine(importFolder, ImportScriptName), script);
                string profile = Path.Combine(importFolder, ProfileName);
                // RULES.md 6.1: StatsDirect's start-up file takes the place of the user's, so it unsets R_PROFILE_USER (R processes
                // started from this session then find their own) and runs the user's profile as R would, before the import.
                // unlink() without expand = keeps R before 4.0 working.
                File.WriteAllText(profile, $$"""
                    local({
                        folder <- dirname(Sys.getenv("R_PROFILE_USER"))
                        Sys.unsetenv("R_PROFILE_USER")
                        own <- if (file.exists(".Rprofile")) ".Rprofile" else path.expand("~/.Rprofile")
                        if (file.exists(own))
                            tryCatch(source(own, local = FALSE),
                                error = function(e) message("Error in ", own, ": ", conditionMessage(e)))
                        script <- file.path(folder, "{{ImportScriptName}}")
                        if (file.exists(script)) tryCatch({
                            # Keep worksheet headings in UTF-8 even with a non-Unicode R locale.
                            lines <- readLines(script, encoding = "UTF-8", warn = FALSE)
                            eval(parse(text = lines, encoding = "UTF-8"), envir = .GlobalEnv)
                        }, finally = unlink(c(script, file.path(folder, "{{WorksheetRData.CsvFileName}}"))))
                    })
                    """);
                start.Environment["R_PROFILE_USER"] = profile;
            }

            var process = Process.Start(start) ?? throw new InvalidOperationException("R could not be started.");
            if (importFolder != null)
                _ = DeleteImportAfterExitAsync(process, importFolder);
            else
                process.Dispose();
        }
        catch
        {
            DeleteImportFolder(importFolder);
            throw;
        }
    }

    private static async Task DeleteImportAfterExitAsync(Process process, string importFolder)
    {
        using (process)
            await process.WaitForExitAsync().ConfigureAwait(false);
        DeleteImportFolder(importFolder);
    }

    /// <summary>
    /// RULES.md 6.2: copies of worksheet data left over (an R window open when StatsDirect closed, or a crash) are removed when
    /// StatsDirect closes and when it next starts.  An external R has read its copy at its start, so only its start-up file goes.
    /// </summary>
    internal static void DeleteLeftoverImports()
    {
        try
        {
            foreach (string folder in Directory.GetDirectories(Path.GetTempPath(), ImportFolderPrefix + "*"))
                DeleteImportFolder(folder);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    internal static void DeleteImportFolder(string? folder)
    {
        if (folder == null) return;
        string path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        string temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        if (!string.Equals(Path.GetDirectoryName(path), temp, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(path).StartsWith(ImportFolderPrefix, StringComparison.Ordinal))
            return;

        foreach (string file in new[] { WorksheetRData.CsvFileName, ImportScriptName, ProfileName })
        {
            try { File.Delete(Path.Combine(path, file)); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        try { Directory.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
