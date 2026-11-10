using System.Text.Json;
using System.Text.RegularExpressions;

// Checks that each bundle the program ships with DOMPurify inside it holds exactly the version that its package manifest, its lock
// file and its third-party notice state.  Nothing else ties them together: the bundles are built on a developer's PC and committed,
// and esbuild bundles whatever is installed.  5.3.9 shipped the report editor's three bundles with DOMPurify 3.4.16 from an install
// that had not been refreshed, while the manifest, the lock file, the notice and the changelog all said 3.4.17.
static class BundledLibraries
{
    // Each bundle that carries DOMPurify, the folder of its package manifest and lock file, and its third-party notice.
    private static readonly (string bundle, string package, string notice)[] Bundles =
    {
        ("StatsDirectUI/Assets/ReportEditor/vendor/editor.js", "ReportEditor/upstream/Report", "StatsDirectUI/Assets/ReportEditor/vendor/THIRD-PARTY-LICENSES.txt"),
        ("StatsDirectUI/Assets/ReportEditor/vendor/export.js", "ReportEditor/upstream/Report", "StatsDirectUI/Assets/ReportEditor/vendor/THIRD-PARTY-LICENSES.txt"),
        ("StatsDirectUI/Assets/ReportEditor/vendor/import.js", "ReportEditor/upstream/Report", "StatsDirectUI/Assets/ReportEditor/vendor/THIRD-PARTY-LICENSES.txt"),
        ("StatsDirectUI/Assets/Learn/app.js", "Learning", "StatsDirectUI/Assets/Learn/THIRD-PARTY-LICENSES.txt"),
        ("StatsDirectUI/Assets/Tutor/conversation.js", "Learning", "StatsDirectUI/Assets/Learn/THIRD-PARTY-LICENSES.txt"),
    };

    private static readonly Regex Banner = new(@"@license DOMPurify (\S+)");
    private static readonly Regex BannerLink = new(@"cure53/DOMPurify/blob/([^/\s]+)/LICENSE");
    // DOMPurify.version = "3.4.17"; DOMPurify.removed = [];  whatever the minifier has made of the name and the spacing.
    private static readonly Regex Code = new(@"\.version\s*=\s*""([^""]*)""\s*[,;]\s*[\w$]+\.removed\s*=\s*\[\]");
    private static readonly Regex LockImporter = new(@"(?m)^ +dompurify:\r?\n +specifier: (\S+)\r?\n +version: (\S+)");
    private static readonly Regex LockPackage = new(@"(?m)^ +dompurify@([^:\s]+):");
    private static readonly Regex Notice = new(@"(?m)^(?:--- )?dompurify (\S+?)(?: ---)?\r?$");

    // The number of failures; one line is written for each.
    public static int Check(string repository)
    {
        int failures = 0;
        void Fail(string message)
        {
            failures++;
            Console.WriteLine("FAIL  " + message);
        }
        string Text(string relative)
        {
            string path = Path.Combine(repository, relative);
            if (File.Exists(path))
                return File.ReadAllText(path);
            Fail(relative + " is not there");
            return "";
        }
        static List<string> All(Regex pattern, string text) =>
            pattern.Matches(text).SelectMany(m => m.Groups.Values.Skip(1)).Select(g => g.Value).ToList();

        foreach ((string bundle, string package, string notice) in Bundles)
        {
            string text = Text(bundle);
            List<string> banner = All(Banner, text), code = All(Code, text);
            if (banner.Count != 1 || code.Count != 1)
            {
                Fail($"{bundle}: one copy of DOMPurify is expected, and it has {banner.Count} licence banners of it and {code.Count} places that set its version");
                continue;
            }
            string held = code[0];
            // What each document says, and where.
            List<(string where, string version)> stated = new();
            stated.AddRange(banner.Select(v => ("the bundle's own licence banner", v)));
            stated.AddRange(All(BannerLink, text).Select(v => ("the licence link of the bundle's own banner", v)));
            string manifest = Text(package + "/package.json");
            if (manifest.Length > 0)
            {
                using JsonDocument json = JsonDocument.Parse(manifest);
                List<string> pinned = new[] { "dependencies", "devDependencies" }
                    .Where(group => json.RootElement.TryGetProperty(group, out JsonElement packages) && packages.TryGetProperty("dompurify", out _))
                    .Select(group => json.RootElement.GetProperty(group).GetProperty("dompurify").GetString())
                    .ToList();
                if (pinned.Count == 0)
                    Fail($"{bundle}: {package}/package.json does not name dompurify");
                stated.AddRange(pinned.Select(v => (package + "/package.json", v)));
            }
            string lockFile = Text(package + "/pnpm-lock.yaml");
            List<string> importer = All(LockImporter, lockFile), locked = All(LockPackage, lockFile);
            if (lockFile.Length > 0 && (importer.Count != 2 || locked.Count == 0))
                Fail($"{bundle}: {package}/pnpm-lock.yaml does not state one dompurify for the package and its resolution");
            stated.AddRange(importer.Concat(locked).Select(v => (package + "/pnpm-lock.yaml", v)));
            List<string> noticed = All(Notice, Text(notice));
            if (noticed.Count == 0)
                Fail($"{bundle}: {notice} does not name dompurify with its version");
            stated.AddRange(noticed.Select(v => (notice, v)));
            foreach ((string where, string version) in stated.Distinct().Where(s => s.version != held))
                Fail($"{bundle} holds DOMPurify {held}, but {where} states {version}");
        }

        // A bundle that is not in the list above would go unchecked, so the list must be all of them.
        string assets = Path.Combine(repository, "StatsDirectUI", "Assets");
        if (Directory.Exists(assets))
            foreach (string file in Directory.GetFiles(assets, "*.js", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                string relative = Path.GetRelativePath(repository, file).Replace(Path.DirectorySeparatorChar, '/');
                if (!Bundles.Any(b => b.bundle == relative) && Banner.IsMatch(File.ReadAllText(file)))
                    Fail($"{relative} carries DOMPurify and is not among the bundles whose version is checked (tests/Definitions/BundledLibraries.cs)");
            }
        else
            Fail("the folder of the shipped assets is not there: " + assets);

        Console.WriteLine($"ok    {Bundles.Length} bundles that carry DOMPurify compared with their package manifest, lock file and third-party notice");
        return failures;
    }
}
