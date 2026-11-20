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
                File.WriteAllText(profile, $$"""
                    local({
                        folder <- dirname(Sys.getenv("R_PROFILE_USER"))
                        script <- file.path(folder, "{{ImportScriptName}}")
                        tryCatch({
                            # Keep worksheet headings in UTF-8 even with a non-Unicode R locale.
                            lines <- readLines(script, encoding = "UTF-8", warn = FALSE)
                            eval(parse(text = lines, encoding = "UTF-8"), envir = .GlobalEnv)
                        }, finally = unlink(c(script, file.path(folder, "{{WorksheetRData.CsvFileName}}")), expand = FALSE))
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
