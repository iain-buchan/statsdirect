#nullable enable
using StatsDirect.Configuration;
using StatsDirect.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace StatsDirect.R;

internal static class WorksheetRLauncher
{
    private const string ImportFolderPrefix = "StatsDirect-worksheet-r-";
    private const string ImportScriptName = "load-worksheet.r";
    private const string ProfileName = "profile.r";
    // The start-up file R is given through R_PROFILE_USER, in a folder of its own (also the R script window's RGui start-up file).
    internal const string StartupFileName = ProfileName;
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
                HandOver(importFolder, process);
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
                    # Keep worksheet headings in UTF-8 even with a non-Unicode R locale.  The connection is closed here, so that no R
                    # warns later about a connection left unused.
                    connection <- file(script, encoding = "native.enc")
                    lines <- tryCatch(readLines(connection, encoding = "UTF-8", warn = FALSE), finally = close(connection))
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
        // A StatsDirect start-up file is never the user's own: an R that an earlier StatsDirect started can pass one on to a StatsDirect
        // started from it, and the file would then run itself.
        if (IsStatsDirectStartupFile(own)) own = "";
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

    // The R window's start-up file for RGui (RController.OpenInRProfile), or an R+ start-up file in an import folder.
    private static bool IsStatsDirectStartupFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            string full = Path.GetFullPath(path);
            if (string.Equals(full, Path.GetFullPath(Path.Combine(SDConfiguration.MyStatsDirectRFolder, RController.OpenInRProfileName)), StringComparison.OrdinalIgnoreCase))
                return true;
            string? folder = Path.GetDirectoryName(full);
            return folder != null && Path.GetFileName(folder).StartsWith(ImportFolderPrefix, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetFileName(full), ProfileName, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    /// <summary>The folder's files now belong to the R that was started on them: they stay while it runs, whether or not this StatsDirect
    /// is still running, and go when it ends.</summary>
    internal static void HandOver(string folder, Process process)
    {
        try { WriteOwner(folder, process); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception) { }
        _ = DeleteImportAfterExitAsync(process, folder);
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
    internal static void DeleteLeftoverImports(bool atStart = false)
    {
        try
        {
            foreach (string folder in Directory.GetDirectories(Path.GetTempPath(), ImportFolderPrefix + "*"))
                if (!InUse(folder) && !(atStart && OwnedByThisProcess(folder)))
                    DeleteImportFolder(folder);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    // The owner record is written whole and then moved into place, so that a reader never sees part of it; a reader of the old one
    // shares deletion, so the move can replace it, and a few retries cover another process opening it at that moment.
    private static void WriteOwner(string folder, Process process)
    {
        string text = process.Id.ToString(CultureInfo.InvariantCulture) + " "
            + process.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture);
        string owner = Path.Combine(folder, OwnerName), written = owner + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(written, text);
        for (int attempt = 1; ; attempt++)
        {
            try { File.Move(written, owner, true); return; }
            catch (Exception e) when (attempt < 20 && e is IOException or UnauthorizedAccessException) { System.Threading.Thread.Sleep(25); }
            catch { try { File.Delete(written); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } throw; }
        }
    }

    /// <summary>
    /// Whether another running process may still need the folder's files: the StatsDirect whose R window holds its data, or the RGui
    /// that R+ started on it.  This StatsDirect's own folders are left over when it closes.  A folder with no owner is being made by
    /// another StatsDirect at this moment, or was left by a build that wrote none, which only its age tells apart.
    /// </summary>
    // Whether this very process made the folder: at start-up none is left over, though a script opened from the command line has one
    // already (the independent review of 0b9c2282).
    private static bool OwnedByThisProcess(string folder)
    {
        try
        {
            string[] parts = File.ReadAllText(Path.Combine(folder, OwnerName)).Split(' ');
            using var self = Process.GetCurrentProcess();
            return parts.Length == 2 && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int id) && id == Environment.ProcessId
                && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long started)
                && Math.Abs(self.StartTime.ToUniversalTime().Ticks - started) < TimeSpan.TicksPerSecond;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception) { return false; }
    }

    internal static bool InUse(string folder)
    {
        string text;
        try
        {
            using var stream = new FileStream(Path.Combine(folder, OwnerName), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            text = reader.ReadToEnd();
        }
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
            // The same process, not a later one given the same id.  A process this user cannot ask its start time is another user's or
            // the system's, so it holds the id of a finished owner, unless it is itself a StatsDirect or an RGui (one run as administrator).
            try { return Math.Abs(process.StartTime.ToUniversalTime().Ticks - started) < TimeSpan.TicksPerSecond; }
            catch (Exception e) when (e is InvalidOperationException or Win32Exception or NotSupportedException)
            {
                try { return process.ProcessName.Equals("StatsDirect", StringComparison.OrdinalIgnoreCase) || process.ProcessName.Equals("Rgui", StringComparison.OrdinalIgnoreCase); }
                catch (Exception f) when (f is InvalidOperationException or Win32Exception or NotSupportedException) { return true; }
            }
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

        var files = new List<string> { WorksheetRData.CsvFileName, ImportScriptName, ProfileName, OwnerName };
        try { files.AddRange(Directory.GetFiles(path, OwnerName + ".*.tmp").Select(file => Path.GetFileName(file))); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        foreach (string file in files)
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
