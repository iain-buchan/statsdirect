#nullable enable
using StatsDirect.Configuration;
using StatsDirect.UI;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace StatsDirect.R;

internal static class WorksheetRLauncher
{
    private const string ImportFolderPrefix = "StatsDirect-worksheet-r-";
    private const string ImportScriptName = "load-worksheet.r";
    private const string ProfileName = "profile.r";
    // The process that still needs a folder's files: the StatsDirect whose R window holds the data, or the RGui that R+ started.
    private const string OwnerName = "owner";

    internal static string CreateImportFolder()
    {
        string folder = Directory.CreateTempSubdirectory(ImportFolderPrefix).FullName;
        using (var self = Process.GetCurrentProcess())
            WriteOwner(folder, self);
        return folder;
    }

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
                start.Environment["R_PROFILE_USER"] = WriteStartupFiles(script, importFolder);

            var process = Process.Start(start) ?? throw new InvalidOperationException("R could not be started.");
            if (importFolder != null)
            {
                // From now on RGui needs the files, whether or not this StatsDirect is still running.
                try { WriteOwner(importFolder, process); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception) { }
                _ = DeleteImportAfterExitAsync(process, importFolder);
            }
            else
                process.Dispose();
        }
        catch
        {
            DeleteImportFolder(importFolder);
            throw;
        }
    }

    /// <summary>
    /// Writes R+'s import script and the start-up file RGui is given through R_PROFILE_USER, in the import folder, and returns the
    /// start-up file's path.  RULES.md 6.1: the start-up file takes the place of the user's.  It runs the user's own start-up file as R
    /// would, then the import.  Its own files are read without re-encoding, so the user's options(encoding = ) cannot change them.
    /// unlink() without expand = keeps R before 4.0 working.
    /// </summary>
    internal static string WriteStartupFiles(string script, string importFolder)
    {
        File.WriteAllText(Path.Combine(importFolder, ImportScriptName), script);
        string profile = Path.Combine(importFolder, ProfileName);
        File.WriteAllText(profile, $$"""
            local({
                folder <- dirname(Sys.getenv("R_PROFILE_USER"))
            {{UserProfileCode()}}
                script <- file.path(folder, "{{ImportScriptName}}")
                if (file.exists(script)) tryCatch({
                    # Keep worksheet headings in UTF-8 even with a non-Unicode R locale.
                    lines <- readLines(file(script, encoding = "native.enc"), encoding = "UTF-8", warn = FALSE)
                    eval(parse(text = lines, encoding = "UTF-8"), envir = .GlobalEnv)
                }, finally = unlink(c(script, file.path(folder, "{{WorksheetRData.CsvFileName}}"))))
            })
            """);
        return profile;
    }

    /// <summary>
    /// R code for a start-up file that takes the place of the user's (RULES.md 6.1).  R processes started from the session get the
    /// user's own R_PROFILE_USER back, or none, so nothing StatsDirect set is inherited.  Then the user's own start-up file runs as R
    /// would run it: R_PROFILE_USER's file when the user set one, else ./.Rprofile, else ~/.Rprofile, in the global environment,
    /// printing what it prints.  The user's .First is left for R to run.
    /// </summary>
    internal static string UserProfileCode()
    {
        string own = Environment.GetEnvironmentVariable("R_PROFILE_USER") ?? "";
        return $$"""
            local({
                own <- {{WorksheetRImport.QuotePath(own)}}
                if (nzchar(own)) Sys.setenv(R_PROFILE_USER = own) else Sys.unsetenv("R_PROFILE_USER")
                if (!nzchar(own)) own <- if (file.exists(".Rprofile")) ".Rprofile" else path.expand("~/.Rprofile")
                if (file.exists(own))
                    tryCatch(source(own, local = FALSE, print.eval = TRUE),
                        error = function(e) message("Error in ", own, ": ", conditionMessage(e)))
            })
            """;
    }

    private static async Task DeleteImportAfterExitAsync(Process process, string importFolder)
    {
        using (process)
            await process.WaitForExitAsync().ConfigureAwait(false);
        DeleteImportFolder(importFolder);
    }

    /// <summary>
    /// RULES.md 6.2: copies of worksheet data left over (an R window open when StatsDirect closed, or a crash) are removed when
    /// StatsDirect closes and when it next starts.  A folder another running StatsDirect or an RGui still needs is not left over.
    /// </summary>
    internal static void DeleteLeftoverImports()
    {
        try
        {
            foreach (string folder in Directory.GetDirectories(Path.GetTempPath(), ImportFolderPrefix + "*"))
                if (!InUse(folder))
                    DeleteImportFolder(folder);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void WriteOwner(string folder, Process process) =>
        File.WriteAllText(Path.Combine(folder, OwnerName), process.Id.ToString(CultureInfo.InvariantCulture) + " "
            + process.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Whether another running process may still need the folder's files: the StatsDirect whose R window holds its data, or the RGui
    /// that R+ started on it.  This StatsDirect's own folders are left over when it closes.  A folder with no owner is being made by
    /// another StatsDirect at this moment, or was left by a build that wrote none, which only its age tells apart.
    /// </summary>
    internal static bool InUse(string folder)
    {
        string text;
        try { text = File.ReadAllText(Path.Combine(folder, OwnerName)); }
        catch (FileNotFoundException)
        {
            try { return Directory.GetCreationTimeUtc(folder) > DateTime.UtcNow.AddMinutes(-10); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return true; }
        }
        catch (DirectoryNotFoundException) { return false; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return true; }

        string[] parts = text.Split(' ');
        if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int id)
            || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long started))
            return false;
        if (id == Environment.ProcessId) return false;
        try
        {
            using var process = Process.GetProcessById(id);
            // The same process, not a later one given the same id.
            try { return Math.Abs(process.StartTime.ToUniversalTime().Ticks - started) < TimeSpan.TicksPerSecond; }
            catch (Exception e) when (e is InvalidOperationException or Win32Exception or NotSupportedException) { return true; }
        }
        catch (ArgumentException) { return false; }   // no such process is running
        catch (InvalidOperationException) { return false; }
    }

    internal static void DeleteImportFolder(string? folder)
    {
        if (folder == null) return;
        string path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        string temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        if (!string.Equals(Path.GetDirectoryName(path), temp, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(path).StartsWith(ImportFolderPrefix, StringComparison.Ordinal))
            return;

        foreach (string file in new[] { WorksheetRData.CsvFileName, ImportScriptName, ProfileName, OwnerName })
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
