internal static partial class Program
{
    private static void CheckTemporaryCleanupIsolation()
    {
        string owned = Path.Combine(TemporaryRoot, "owned-cleanup-probe");
        string other = Path.Combine(Path.GetTempPath(), "StatsDirect-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(owned);
        Directory.CreateDirectory(other);
        string sentinel = Path.Combine(other, "download-sentinel.txt");
        File.WriteAllText(sentinel, "Unrelated installer download or another session's work");
        try
        {
            File.WriteAllText(Path.Combine(owned, "temporary.txt"), "owned");
            RemoveOwnedTemporaryFolder(owned);
            Check(!Directory.Exists(owned) && File.Exists(sentinel), "R replay cleanup deletes only its own run artifacts");
            bool refused = false;
            try { RemoveOwnedTemporaryFolder(other); }
            catch (InvalidOperationException) { refused = true; }
            Check(refused && File.ReadAllText(sentinel).StartsWith("Unrelated"), "R replay cleanup refuses an unrelated update or test-session folder");
        }
        finally
        {
            // This exact sentinel directory was created by this check, not found
            // by a prefix/date search of the user's temporary folder.
            File.Delete(sentinel);
            Directory.Delete(other, false);
        }
    }
}
