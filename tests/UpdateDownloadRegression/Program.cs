using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using StatsDirect.UI;

internal static class Program
{
    private static int passed;
    // The transfer checks use made-up bytes, which no publisher signed; they pass this in place of the updater's own check.
    private static readonly Action<string> NoCheck = _ => { };

    // The manifest of a release, as the release writes it: the checks of reading start from this one.
    private const string Address = "https://github.com/iain-buchan/statsdirect/releases/download/v5.3.17/StatsDirectSetup.exe";
    private const string Digest = "79dbc07d18397b55f6c9e7957c30e5242370610f25a106ef0eb648450c394359";

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Contains("--download-only"))
                return CheckManifest(UpdateOffer.ManifestUri.AbsoluteUri, null, download: true, Required(args)).GetAwaiter().GetResult();
            if (Value(args, "--verify") is string installer)
            {
                // The release check: would the updater of the version given start this installer?
                string path = Path.GetFullPath(installer);
                Version running = Value(args, "--running") is string given ? Version.Parse(given) : new Version(0, 0, 0);
                UpdateInstallerCheck.Verify(path, running);
                Console.WriteLine($"PASS: {path} is signed by {UpdateInstallerCheck.Signer(path)} and is a newer StatsDirect than {running}: the updater would start it. Nothing was launched.");
                return 0;
            }
            if (Value(args, "--manifest") is string signed)
                return WriteManifest(Path.GetFullPath(signed), Value(args, "--out"), Value(args, "--mirror"), args.Contains("--versioned-mirror"));
            if (Value(args, "--check-manifest") is string manifest)
                return CheckManifest(manifest, Value(args, "--running"), args.Contains("--download"), Required(args)).GetAwaiter().GetResult();
            if (args.Contains("--preview"))
            {
                ApplicationConfiguration.Initialize();
                using var preview = new frmDownloadUpdate(async (progress, token) =>
                {
                    for (int i = 0; ; i = (i + 1) % 100)
                    {
                        progress.Report(new UpdateDownloadProgress(i * 1048576L, 100 * 1048576L));
                        await Task.Delay(1000, token);
                    }
                }, UpdateOffer.Create(new Version(5, 3, 17), 100 * 1048576L, Digest));
                preview.ShowDialog();
                return 0;
            }

            RunManifests();
            RunManifestFetches().GetAwaiter().GetResult();
            RunDownloads().GetAwaiter().GetResult();
            RunRedirects().GetAwaiter().GetResult();
            RunMirrors().GetAwaiter().GetResult();
            RunLimits().GetAwaiter().GetResult();
            RunGates().GetAwaiter().GetResult();
            RunInstallerChecks();
            RunSignedDownloads().GetAwaiter().GetResult();
            ApplicationConfiguration.Initialize();
            RunDialogs();
            Console.WriteLine($"PASS: {passed} update manifest, download and dialog checks. No installer was launched.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    // ---- The release's tools ----

    // Writes the manifest of a signed installer, with a mirror on www.statsdirect.com if one is asked for: the versioned one,
    // which is what a release publishes, or an address given.  The installer must be one the updater would start, and what is
    // written must read back, through the updater's own reader, as the same offer.
    private static int WriteManifest(string installer, string output, string mirror, bool versionedMirror)
    {
        UpdateInstallerCheck.Verify(installer, new Version(0, 0, 0));
        if (versionedMirror && mirror != null)
            throw new ArgumentException("Give --versioned-mirror or --mirror <address>, not both.");
        Version version = FileVersion(installer);
        if (versionedMirror)
            mirror = UpdateOffer.VersionedMirrorAddress(version);
        string sha256;
        using (FileStream file = File.OpenRead(installer))
            sha256 = Convert.ToHexString(SHA256.HashData(file));
        UpdateOffer offer = UpdateOffer.Create(version, new FileInfo(installer).Length, sha256, mirror);
        string manifest = offer.ToManifest();
        if (UpdateOffer.Parse(manifest) != offer)
            throw new Exception("The manifest written does not read back as the offer.");
        if (output == null)
        {
            Console.Out.Write(manifest);
            return 0;
        }
        byte[] bytes = new UTF8Encoding(false).GetBytes(manifest);
        File.WriteAllBytes(Path.GetFullPath(output), bytes);
        Console.WriteLine($"PASS: wrote {Path.GetFullPath(output)} ({bytes.Length} bytes, SHA-256 {Convert.ToHexString(SHA256.HashData(bytes))}) for {installer}, signed by {UpdateInstallerCheck.Signer(installer)}.");
        Console.WriteLine("It offers " + offer);
        Console.WriteLine("Publish it only when the release's address serves this file (--check-manifest <file> --download --require-github)"
            + (offer.Mirror == null ? "." : " and the mirror serves it too (--check-manifest <file> --download --block-github)."));
        return 0;
    }

    // Where a check requires the installer to come from.
    private enum From { Either, GitHub, Mirror }

    private static From Required(string[] args) =>
        args.Contains("--require-github") && args.Contains("--block-github") ? throw new ArgumentException("--require-github and --block-github cannot go together.")
        : args.Contains("--require-github") ? From.GitHub
        : args.Contains("--block-github") ? From.Mirror
        : From.Either;

    // The download a check makes, over the network given.
    // GitHub: from the release's own address and no other, so that a mirror in good order cannot stand in for a file that is
    // missing or wrong at GitHub.
    // Mirror: as on a network that blocks GitHub, where the file has to come from the manifest's mirror with no help from GitHub.
    // Either: as the updater downloads.
    private static async Task<UpdateInstaller> DownloadFrom(HttpMessageHandler network, UpdateOffer offer, From source, Action<string> verify, UpdateDownloadLimits limits = null)
    {
        if (source == From.Mirror && offer.Mirror == null)
        {
            network.Dispose();
            throw new IOException("The manifest names no mirror.");
        }
        using var client = new HttpClient(source == From.Mirror ? new BlockGitHub(network) : network) { Timeout = Timeout.InfiniteTimeSpan };
        UpdateInstaller installer = await UpdateInstaller.DownloadAsync(client, source == From.GitHub ? offer.WithoutMirror() : offer, null, CancellationToken.None, verify, limits);
        Uri required = source switch { From.GitHub => offer.Url, From.Mirror => offer.Mirror, _ => installer.Source };
        if (installer.Source != required)
        {
            installer.Dispose();
            throw new IOException($"The file came from {installer.Source}, not from {required}.");
        }
        return installer;
    }

    // Reads a manifest, from a file or an https address, as the updater reads it; with download, also fetches its installer and
    // checks it as the updater of the version given would.  It starts nothing.
    private static async Task<int> CheckManifest(string source, string runningGiven, bool download, From from)
    {
        // With no version given, the updater's own check is made: against this program's version.
        Version running = runningGiven != null ? Version.Parse(runningGiven) : UpdateInstallerCheck.RunningVersion;
        UpdateOffer offer;
        if (source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            using var client = new HttpClient();
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(100));
            offer = await UpdateOffer.FetchAsync(client, new Uri(source), limit.Token);
        }
        else if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A manifest is read over https only.");
        else
            offer = UpdateOffer.Parse(File.ReadAllText(Path.GetFullPath(source), new UTF8Encoding(false, true)));
        Console.WriteLine($"Manifest read from {source}: {offer}");
        Console.WriteLine(offer.IsNewerThan(running)
            ? $"StatsDirect {running} would offer it as an update."
            : $"StatsDirect {running} would say that it has the latest version.");
        if (!download)
            return from == From.Either ? 0 : throw new ArgumentException("--require-github and --block-github go with --download.");
        Console.WriteLine(from switch
        {
            From.GitHub => "From the release's address at GitHub and no other: the manifest's mirror is left out of this check.",
            From.Mirror => "As on a network that blocks GitHub: every request to github.com and githubusercontent.com is refused here, so the file has to come from the mirror.",
            _ => "As the updater downloads: from the release's address, or from the mirror if that cannot be fetched."
        });
        using UpdateInstaller installer = await DownloadFrom(new HttpClientHandler { AllowAutoRedirect = false }, offer, from,
            path => UpdateInstallerCheck.Verify(path, running, offer.Version));
        Console.WriteLine($"PASS: downloaded {new FileInfo(installer.FilePath).Length:N0} bytes from {installer.Source}. Size and SHA-256 are the manifest's. It is the setup program, signed by {UpdateInstallerCheck.Signer(installer.FilePath)}, and is StatsDirect {offer.Version}, newer than {running}: the updater would start it. File has Internet-zone metadata. No installer launched; test download removed on exit.");
        return 0;
    }

    private static string Value(string[] args, string name)
    {
        int at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }

    // ---- Reading a manifest ----

    // A manifest whose fields are the raw JSON given (null leaves a field out), with anything extra after them.
    private static string Manifest(string schema = "1", string version = "\"5.3.17\"", string url = "\"" + Address + "\"",
        string size = "304535768", string sha256 = "\"" + Digest + "\"", string extra = null)
    {
        var fields = new List<string>();
        if (schema != null) fields.Add("\"schema\": " + schema);
        if (version != null) fields.Add("\"version\": " + version);
        if (url != null) fields.Add("\"url\": " + url);
        if (size != null) fields.Add("\"size\": " + size);
        if (sha256 != null) fields.Add("\"sha256\": " + sha256);
        if (extra != null) fields.Add(extra);
        return "{ " + string.Join(", ", fields) + " }";
    }

    private static void RunManifests()
    {
        UpdateOffer release = UpdateOffer.Create(new Version(5, 3, 17), 304535768, Digest.ToUpperInvariant());
        Check(release.Version == new Version(5, 3, 17) && release.Size == 304535768 && release.Sha256 == Digest && release.Url.AbsoluteUri == Address
                && UpdateOffer.InstallerAddress(new Version(5, 3, 17)) == Address,
            "An offer holds its version, size and SHA-256, and its address is the installer of that version's release at GitHub");
        Check(release.ToManifest() == "{\n  \"schema\": 1,\n  \"version\": \"5.3.17\",\n  \"url\": \"" + Address + "\",\n  \"size\": 304535768,\n  \"sha256\": \"" + Digest + "\"\n}\n",
            "The manifest a release writes is the published form, character for character");
        Check(UpdateOffer.Parse(release.ToManifest()) == release && UpdateOffer.Parse(Manifest()) == release,
            "The manifest a release writes reads back as the same offer");
        Check(UpdateOffer.Parse("\uFEFF" + release.ToManifest().Replace("\n", "\r\n")) == release
                && UpdateOffer.Parse(Manifest(sha256: "\"" + Digest.ToUpperInvariant() + "\"")) == release
                && UpdateOffer.Parse("{\"sha256\":\"" + Digest + "\",\"size\":304535768,\"url\":\"" + Address + "\",\"version\":\"5.3.17\",\"schema\":1}") == release,
            "A byte order mark, other line endings, capital hexadecimal digits and another order of fields change nothing");
        Check(UpdateOffer.Parse(Manifest(extra: "\"published\": \"2026-11-22\", \"msi\": { \"size\": 29163520, \"notes\": [1, 2] }, \"schema2\": 2")) == release,
            "Fields a reader does not know are passed over, so fields can be added later");

        Check(release.IsNewerThan(new Version(5, 3, 16)) && release.IsNewerThan(new Version(5, 3, 16, 9)) && release.IsNewerThan(new Version(4, 9))
                && !release.IsNewerThan(new Version(5, 3, 17)) && !release.IsNewerThan(new Version(5, 3, 17, 5)) && !release.IsNewerThan(new Version(5, 3, 18))
                && !release.IsNewerThan(new Version(6, 0, 0)),
            "An offer is an update only for an older version: the same version and a newer one are offered nothing");

        // What the check tells the user, and whether it offers anything.
        const string page = "www.statsdirect.com/download.aspx";
        UpdateCheckOutcome found = UpdateOffer.Evaluate(release.ToManifest(), null, new Version(5, 3, 16, 0));
        Check(found.Succeeded && found.Offer == release && found.Detail == null
                && found.Message == "You are presently running StatsDirect version 5.3.16. Version 5.3.17 is available. Would you like to close StatsDirect and install the new version?",
            "The check of an older version offers the update, whole, and names both versions");
        UpdateCheckOutcome current = UpdateOffer.Evaluate(release.ToManifest(), null, new Version(5, 3, 17)), ahead = UpdateOffer.Evaluate(release.ToManifest(), null, new Version(5, 4, 0));
        Check(current.Succeeded && current.Offer == null && current.Message == "You are presently running StatsDirect version 5.3.17. You have the latest version of StatsDirect."
                && ahead.Succeeded && ahead.Offer == null && ahead.Message == "You are presently running StatsDirect version 5.4.0. You have the latest version of StatsDirect.",
            "The check of the version published, and of a later one, offers nothing and says the version is the latest");
        foreach ((string manifest, Exception error, string says, string detail, string name) in new (string, Exception, string, string, string)[]
        {
            (Manifest(schema: "2"), null, "Could not read the StatsDirect update information", "later form", "a manifest in a later form"),
            (Manifest(url: "\"https://example.org/StatsDirectSetup.exe\""), null, "Could not read the StatsDirect update information", "address of StatsDirect", "a manifest with another address"),
            ("<html>Not found</html>", null, "Could not read the StatsDirect update information", "is not JSON", "a web page in the manifest's place"),
            (null, new UpdateManifestException("The update manifest is longer than a manifest can be."), "Could not read the StatsDirect update information", "longer than", "a manifest that could not be read"),
            (null, new HttpRequestException("Response status code does not indicate success: 404 (Not Found).", null, HttpStatusCode.NotFound), "Could not read the StatsDirect update information", "404", "a site that answers 404"),
            (null, new HttpRequestException("No such host is known."), "Could not check for StatsDirect updates. Please check your Internet connection", "No such host", "no connection"),
            (null, new TaskCanceledException("The request timed out."), "Could not check for StatsDirect updates. Please check your Internet connection", "timed out", "a fetch that timed out"),
            (null, null, "Could not check for StatsDirect updates. Please check your Internet connection", null, "nothing fetched")
        })
        {
            UpdateCheckOutcome outcome = UpdateOffer.Evaluate(manifest, error, new Version(5, 3, 16));
            Check(!outcome.Succeeded && outcome.Offer == null && outcome.Message.StartsWith(says, StringComparison.Ordinal) && outcome.Message.EndsWith(page, StringComparison.Ordinal)
                    && (detail == null ? outcome.Detail == null : outcome.Detail.Contains(detail, StringComparison.Ordinal)),
                $"The check offers nothing and sends the user to the download page: {name}");
        }

        // An escape that JSON's grammar allows but that is no text, one half of a surrogate pair, makes the manifest unreadable
        // wherever it is read: in a value, or in the name of any field.  (Codex's review of 11afcbbc: it threw past the manifest's
        // own error.)
        string half = "\\uD800";
        Unreadable(Manifest(version: "\"" + half + "\""), "cannot be read", "half a surrogate pair as the version");
        Unreadable(Manifest(version: "\"5.3.17" + half + "\""), "cannot be read", "half a surrogate pair after the version");
        Unreadable(Manifest(url: "\"" + Address + half + "\""), "cannot be read", "half a surrogate pair in the address");
        Unreadable(Manifest(sha256: "\"" + half + "\""), "cannot be read", "half a surrogate pair as the SHA-256");
        Unreadable(Manifest(extra: "\"mirror\": \"" + half + "\""), "cannot be read", "half a surrogate pair as the mirror");
        Unreadable(Manifest(extra: "\"note" + half + "\": 1"), "cannot be read", "half a surrogate pair in the name of a field that is not known");
        Unreadable("{ \"" + half + "\": 1 }", "cannot be read", "half a surrogate pair as the only field's name");
        Unreadable(Manifest(extra: "\"note\\uDC00\": 1"), "cannot be read", "the second half of a surrogate pair alone in a field's name");
        UpdateCheckOutcome garbled = UpdateOffer.Evaluate(Manifest(version: "\"" + half + "\""), null, new Version(5, 3, 16));
        Check(!garbled.Succeeded && garbled.Offer == null && garbled.Message.StartsWith("Could not read the StatsDirect update information", StringComparison.Ordinal)
                && garbled.Message.EndsWith(page, StringComparison.Ordinal) && garbled.Detail.Contains("cannot be read", StringComparison.Ordinal),
            "The check offers nothing and sends the user to the download page: a manifest with half a surrogate pair");
        // Escapes that stand for text are read as that text.
        Check(UpdateOffer.Parse(Manifest(version: "\"5.3.\\u0031\\u0037\"")) == release
                && UpdateOffer.Parse(Manifest(url: "\"" + Address.Replace("/", "\\/") + "\"")) == release
                && UpdateOffer.Parse(Manifest().Replace("\"version\"", "\"vers\\u0069on\"")) == release
                && UpdateOffer.Parse(Manifest(extra: "\"note\": \"\\uD83D\\uDE00 \\u00e9 \\\" \\\\\", \"n\\u006Fte\": 2")) == release,
            "Escapes that stand for text are read as the text: in a value, in a field's name, and a whole surrogate pair");

        Unreadable("", "is not JSON", "empty");
        Unreadable("   \n", "is not JSON", "white space only");
        Unreadable("<html><body>Not found</body></html>", "is not JSON", "a web page");
        Unreadable(Manifest() + " x", "is not JSON", "text after the object");
        Unreadable(Manifest() + Manifest(), "is not JSON", "two objects");
        Unreadable("{ \"schema\": 1, /* note */ \"version\": \"5.3.17\" }", "is not JSON", "a comment");
        Unreadable(Manifest().Replace(" }", ", }"), "is not JSON", "a trailing comma");
        Unreadable(Manifest(extra: "\"deep\": [[[[[[[[[1]]]]]]]]]"), "is not JSON", "nesting deeper than a manifest has");
        Unreadable("null", "is not a JSON object", "null");
        Unreadable("[" + Manifest() + "]", "is not a JSON object", "an array");
        Unreadable("\"5.3.17\"", "is not a JSON object", "a string");

        Unreadable("{}", "which form", "an empty object");
        Unreadable(Manifest(schema: null), "which form", "no schema");
        Unreadable(Manifest(schema: "\"1\""), "which form", "schema as text");
        Unreadable(Manifest(schema: "1.0"), "which form", "schema with a fraction");
        Unreadable(Manifest(schema: "1e0"), "which form", "schema with an exponent");
        Unreadable(Manifest(schema: "-1"), "which form", "a negative schema");
        Unreadable(Manifest(schema: "99999999999"), "which form", "a schema too large for a number");
        Unreadable(Manifest(schema: "true"), "which form", "schema as true");
        Unreadable(Manifest(schema: "0"), "does not read", "schema 0");
        Unreadable(Manifest(schema: "2"), "later form", "a later schema");
        Unreadable("{ \"schema\": 2, \"release\": { \"version\": \"5.4.0\" } }", "later form", "a later schema with other fields");

        Unreadable(Manifest(version: null), "three numbers", "no version");
        Unreadable(Manifest(version: "5.3"), "three numbers", "version as a number");
        Unreadable(Manifest(version: "null"), "three numbers", "version null");
        foreach (string text in new[] { "", "5", "5.3", "5.3.17.0", "v5.3.17", "05.3.17", "5.03.17", "5.3.017", " 5.3.17", "5.3.17 ", "5.3.17\\n", "5.3.x", "5.3.-1", "5,3,17", "+5.3.17",
            "5.3.1234567890", "\u0665.\u0663.\u0661\u0667", "5.3.17-beta", "5.3.17+build" })
            Unreadable(Manifest(version: "\"" + text + "\""), "three numbers", "version \"" + text + "\"");

        Unreadable(Manifest(url: null), "address of StatsDirect 5.3.17", "no address");
        Unreadable(Manifest(url: "5"), "address of StatsDirect 5.3.17", "address as a number");
        foreach (string text in new[]
        {
            "http://github.com/iain-buchan/statsdirect/releases/download/v5.3.17/StatsDirectSetup.exe",
            "https://github.com/someone-else/statsdirect/releases/download/v5.3.17/StatsDirectSetup.exe",
            "https://github.com/iain-buchan/statsdirect-mac/releases/download/v5.3.17/StatsDirectSetup.exe",
            "https://github.com/iain-buchan/statsdirect/releases/download/v5.3.17/StatsDirect-x64.msi",
            "https://github.com/iain-buchan/statsdirect/releases/download/v5.3.17/statsdirectsetup.exe",
            "https://github.com/iain-buchan/statsdirect/releases/download/v5.3.18/StatsDirectSetup.exe",
            "https://github.com/iain-buchan/statsdirect/releases/download/5.3.17/StatsDirectSetup.exe",
            "https://github.com/iain-buchan/statsdirect/releases/latest/download/StatsDirectSetup.exe",
            "https://GitHub.com/iain-buchan/statsdirect/releases/download/v5.3.17/StatsDirectSetup.exe",
            "https://github.com:443/iain-buchan/statsdirect/releases/download/v5.3.17/StatsDirectSetup.exe",
            "https://github.com.example.org/iain-buchan/statsdirect/releases/download/v5.3.17/StatsDirectSetup.exe",
            "https://user@github.com/iain-buchan/statsdirect/releases/download/v5.3.17/StatsDirectSetup.exe",
            "https://github.com/iain-buchan/statsdirect/releases/download/v5.3.17/../v5.3.17/StatsDirectSetup.exe",
            "https://github.com/iain-buchan/statsdirect/releases/download/v5.3.17/StatsDirectSetup%2Eexe",
            Address + "?download=1", Address + "#top", Address + "/", Address + " ", " " + Address,
            "https://www.statsdirect.com/download/StatsDirectSetup.exe",
            "https://example.org/StatsDirectSetup.exe",
            ""
        })
            Unreadable(Manifest(url: "\"" + text + "\""), "address of StatsDirect 5.3.17", "address \"" + text + "\"");

        Unreadable(Manifest(size: null), "size that an installer can have", "no size");
        foreach (string text in new[] { "0", "-1", "1.5", "304535768.0", "3e8", "\"304535768\"", "2147483649", "99999999999999999999", "null", "[304535768]" })
            Unreadable(Manifest(size: text), "size that an installer can have", "size " + text);
        Check(UpdateOffer.Parse(Manifest(size: UpdateOffer.MaximumInstallerBytes.ToString())).Size == UpdateOffer.MaximumInstallerBytes && UpdateOffer.Parse(Manifest(size: "1")).Size == 1,
            "The smallest and the largest size an installer can have are read");

        Unreadable(Manifest(sha256: null), "SHA-256 of 64", "no SHA-256");
        foreach (string text in new[] { "", Digest[..63], Digest + "0", "g" + Digest[1..], "sha256:" + Digest, " " + Digest[1..], Digest[..32] + " " + Digest[33..] })
            Unreadable(Manifest(sha256: "\"" + text + "\""), "SHA-256 of 64", "SHA-256 \"" + text + "\"");
        Unreadable(Manifest(sha256: "7"), "SHA-256 of 64", "SHA-256 as a number");

        // The mirror: a second address for the same file, on www.statsdirect.com only.
        const string site = "https://www.statsdirect.com/download/StatsDirectSetup.exe";
        UpdateOffer mirrored = UpdateOffer.Create(new Version(5, 3, 17), 304535768, Digest, site);
        Check(release.Mirror == null && release.MirrorAddress == null && mirrored.Mirror.AbsoluteUri == site && mirrored != release && mirrored.Url == release.Url,
            "An offer has a mirror only when it is given one, and its first address is the release's either way");
        Check(mirrored.ToManifest() == "{\n  \"schema\": 1,\n  \"version\": \"5.3.17\",\n  \"url\": \"" + Address + "\",\n  \"mirror\": \"" + site + "\",\n  \"size\": 304535768,\n  \"sha256\": \"" + Digest + "\"\n}\n"
                && UpdateOffer.Parse(mirrored.ToManifest()) == mirrored && UpdateOffer.Parse(Manifest(extra: "\"mirror\": \"" + site + "\"")) == mirrored,
            "The manifest of an offer with a mirror names it, and reads back as the same offer");
        foreach (string text in new[]
        {
            "https://www.statsdirect.com/download/v5.3.17/StatsDirectSetup.exe",
            "https://www.statsdirect.com/download/direct/StatsDirectSetup.exe",
            "https://www.statsdirect.com/download/a/b.c/d-e/f_g/StatsDirectSetup.exe"
        })
            Check(UpdateOffer.Parse(Manifest(extra: "\"mirror\": \"" + text + "\"")).MirrorAddress == text, "A mirror in a folder below the site's download folder is read: " + text);
        foreach (string text in new[]
        {
            "http://www.statsdirect.com/download/StatsDirectSetup.exe",
            "https://statsdirect.com/download/StatsDirectSetup.exe",
            "https://WWW.statsdirect.com/download/StatsDirectSetup.exe",
            "https://www.statsdirect.com.example.org/download/StatsDirectSetup.exe",
            "https://www.statsdirect.com:8443/download/StatsDirectSetup.exe",
            "https://user@www.statsdirect.com/download/StatsDirectSetup.exe",
            "https://www.statsdirect.com/StatsDirectSetup.exe",
            "https://www.statsdirect.com/Download/StatsDirectSetup.exe",
            "https://www.statsdirect.com/download/StatsDirect-x64.msi",
            "https://www.statsdirect.com/download/statsdirectsetup.exe",
            "https://www.statsdirect.com/download/../StatsDirectSetup.exe",
            "https://www.statsdirect.com/download/./StatsDirectSetup.exe",
            "https://www.statsdirect.com/download//StatsDirectSetup.exe",
            "https://www.statsdirect.com/download/a/b/c/d/e/StatsDirectSetup.exe",
            "https://www.statsdirect.com/download/a%2Fb/StatsDirectSetup.exe",
            "https://www.statsdirect.com/download/StatsDirectSetup.exe?v=5.3.17",
            "https://www.statsdirect.com/download/StatsDirectSetup.exe#x",
            "https://www.statsdirect.com/download/StatsDirectSetup.exe/",
            " https://www.statsdirect.com/download/StatsDirectSetup.exe",
            "https://example.org/download/StatsDirectSetup.exe",
            Address,
            ""
        })
            Unreadable(Manifest(extra: "\"mirror\": \"" + text + "\""), "second address", "mirror \"" + text + "\"");
        Unreadable(Manifest(extra: "\"mirror\": null"), "second address", "mirror null");
        Unreadable(Manifest(extra: "\"mirror\": 5"), "second address", "mirror as a number");
        Unreadable(Manifest(extra: "\"mirror\": [\"" + site + "\"]"), "second address", "mirror as a list");
        Unreadable(Manifest(extra: "\"mirror\": \"" + site + "\", \"mirror\": \"" + site + "\""), "twice", "a mirror given twice");

        foreach (string again in new[] { "\"schema\": 1", "\"version\": \"5.3.17\"", "\"url\": \"" + Address + "\"", "\"size\": 304535768", "\"sha256\": \"" + Digest + "\"",
            "\"vers\\u0069on\": \"5.3.18\"" })
            Unreadable(Manifest(extra: again), "twice", "a field given twice: " + again);

        foreach (Action wrong in new Action[]
        {
            () => UpdateOffer.Create(new Version(5, 3, 17), 0, Digest),
            () => UpdateOffer.Create(new Version(5, 3, 17), UpdateOffer.MaximumInstallerBytes + 1, Digest),
            () => UpdateOffer.Create(new Version(5, 3, 17), 10, Digest[1..]),
            () => UpdateOffer.Create(new Version(5, 3, 1_000_000_000), 10, Digest),
            () => UpdateOffer.Create(new Version(5, 3, 17), 10, Digest, "https://example.org/download/StatsDirectSetup.exe"),
            () => UpdateOffer.Create(new Version(5, 3, 17), 10, Digest, Address)
        })
        {
            bool refused = false;
            try { wrong(); }
            catch (ArgumentException) { refused = true; }
            if (!refused) throw new Exception("FAIL: an offer that cannot be published was made");
        }
        Check(UpdateOffer.Create(new Version(5, 3), 10, Digest).Version == new Version(5, 3, 0) && UpdateOffer.Create(new Version(5, 3, 17, 4), 10, Digest).Version == new Version(5, 3, 17),
            "An offer that cannot be published is not made, and an offer's version has three parts");
    }

    private static void Unreadable(string manifest, string because, string name)
    {
        try
        {
            UpdateOffer.Parse(manifest);
        }
        catch (UpdateManifestException ex) when (ex.Message.Contains(because, StringComparison.Ordinal))
        {
            passed++;
            Console.WriteLine("PASS: Manifest refused: " + name);
            return;
        }
        catch (UpdateManifestException ex)
        {
            throw new Exception($"FAIL: manifest refused for another reason ({ex.Message}): {name}");
        }
        throw new Exception("FAIL: manifest accepted: " + name);
    }

    private static async Task RunManifestFetches()
    {
        UpdateOffer release = UpdateOffer.Create(new Version(5, 3, 17), 304535768, Digest);
        Uri address = UpdateOffer.ManifestUri;
        Check(address.AbsoluteUri == "https://www.statsdirect.com/download/windows-latest.json", "The manifest is read from statsdirect.com over https");

        using (var client = new HttpClient(new Routes().On(address, _ => Ok(new StringContent(release.ToManifest())))))
            Check(await UpdateOffer.FetchAsync(client, address, CancellationToken.None) == release, "A manifest served by the site is read as its offer");

        // As long as a manifest can be, and one byte longer; with the length stated, and without.
        string padded = release.ToManifest().PadRight(UpdateOffer.MaximumManifestBytes);
        using (var client = new HttpClient(new Routes().On(address, _ => Ok(new StringContent(padded)))))
            Check(await UpdateOffer.FetchAsync(client, address, CancellationToken.None) == release, "A manifest of the greatest length is read");
        await NoManifest("A manifest longer than a manifest can be, its length stated", new Routes().On(address, _ => Ok(new StringContent(padded + " "))), "longer than a manifest can be");
        var endless = new NonSeekableStream(new byte[10 * 1024 * 1024]);
        await NoManifest("A manifest longer than a manifest can be, its length not stated", new Routes().On(address, _ => Ok(new TestContent(endless))), "longer than a manifest can be");
        Check(endless.BytesRead == UpdateOffer.MaximumManifestBytes + 1, "No more of an overlong answer is read than shows it to be too long");

        await NoManifest("The site answers 404", new Routes().On(address, _ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("Not found") }), null, HttpStatusCode.NotFound);
        await NoManifest("The site answers 500", new Routes().On(address, _ => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(release.ToManifest()) }), null, HttpStatusCode.InternalServerError);
        await NoManifest("A web page served with status 200", new Routes().On(address, _ => Ok(new StringContent("<html>Temporarily unavailable</html>"))), "is not JSON");
        await NoManifest("An empty answer", new Routes().On(address, _ => Ok(new ByteArrayContent([]))), "is not JSON");
        byte[] notUtf8 = Encoding.UTF8.GetBytes(release.ToManifest());
        notUtf8[10] = 0xFF;
        await NoManifest("Bytes that are not UTF-8", new Routes().On(address, _ => Ok(new ByteArrayContent(notUtf8))), "is not UTF-8");
        await NoManifest("A redirect that ends on an insecure address", new Routes().On(address, request =>
        {
            request.RequestUri = new Uri("http://www.statsdirect.com/download/windows-latest.json");
            return Ok(new StringContent(release.ToManifest()));
        }), "secure connection");

        using var alreadyCancelled = new CancellationTokenSource();
        alreadyCancelled.Cancel();
        await NoManifest("Cancellation before the manifest is fetched", new Routes().On(address, _ => Ok(new StringContent(release.ToManifest()))), null, null, alreadyCancelled.Token);
        using var duringRead = new CancellationTokenSource();
        duringRead.CancelAfter(150);
        await NoManifest("Cancellation while the manifest is read", new Routes().On(address, _ => Ok(new TestContent(new WaitingStream()))), null, null, duringRead.Token);
    }

    // The fetch gives no offer: it throws for the reason given, or with the status given, or because it was cancelled.
    private static async Task NoManifest(string name, Routes routes, string because, HttpStatusCode? status = null, CancellationToken token = default)
    {
        bool refused = false;
        using (var client = new HttpClient(routes))
        {
            try
            {
                await UpdateOffer.FetchAsync(client, UpdateOffer.ManifestUri, token);
            }
            catch (UpdateManifestException ex) { refused = because != null && ex.Message.Contains(because, StringComparison.Ordinal); }
            catch (HttpRequestException ex) { refused = status != null && ex.StatusCode == status; }
            catch (OperationCanceledException) { refused = because == null && status == null && token.IsCancellationRequested; }
        }
        Check(refused, name + ": no offer is made");
    }

    // ---- The transfer ----

    private static UpdateOffer Offer(byte[] bytes, string version = "9.9.9") =>
        UpdateOffer.Create(Version.Parse(version), bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));

    private static async Task RunDownloads()
    {
        byte[] bytes = new byte[200000];
        new Random(42).NextBytes(bytes);
        bytes[0] = (byte)'M'; bytes[1] = (byte)'Z';
        UpdateOffer offer = Offer(bytes);
        string savedPath;
        var progress = new RecordedProgress();
        using (var client = Client(offer, new ByteArrayContent(bytes)))
        using (UpdateInstaller installer = await UpdateInstaller.DownloadAsync(client, offer, progress, CancellationToken.None, NoCheck))
        {
            savedPath = installer.FilePath;
            Check(File.ReadAllBytes(savedPath).SequenceEqual(bytes), "Complete binary preserved byte for byte");
            Check(!File.Exists(savedPath + ".partial") && Path.GetFileName(savedPath) == "StatsDirectSetup.exe", "Only a completed installer is offered for launch");
            string zone = File.ReadAllText(savedPath + ":Zone.Identifier");
            Check(zone.Contains("ZoneId=3\r\n") && zone.Contains("HostUrl=" + offer.Url.AbsoluteUri + "\r\n"),
                "Downloaded file retains Internet zone and the address it came from");
            Check(progress.Values[0] == new UpdateDownloadProgress(0, bytes.Length, "github.com") && progress.Values[1] == new UpdateDownloadProgress(0, bytes.Length)
                    && progress.Values.Last() == new UpdateDownloadProgress(bytes.Length, bytes.Length) && progress.Values.Count(p => p.Source != null) == 1,
                "Progress names the host asked, then includes the start and the completed length, measured against the offer's size");
        }
        Check(!Directory.Exists(Path.GetDirectoryName(savedPath)), "Unlaunched download removed when a save/close is cancelled");

        var unstated = new RecordedProgress();
        using (var client = Client(offer, new TestContent(new NonSeekableStream(bytes))))
        using (UpdateInstaller installer = await UpdateInstaller.DownloadAsync(client, offer, unstated, CancellationToken.None, NoCheck))
            Check(File.ReadAllBytes(installer.FilePath).SequenceEqual(bytes) && unstated.Values.All(p => p.TotalBytes == bytes.Length),
                "Download works without Content-Length, and its progress still has a total");

        // With the updater's own check, a complete download that StatsDirect Limited did not sign is not kept.
        await Reject("Complete download not signed by StatsDirect Limited", Client(offer, new ByteArrayContent(bytes)), offer, "not signed by StatsDirect Limited", verify: null);
        await Reject("HTTP failure", Client(offer, new StringContent("Not found"), HttpStatusCode.NotFound), offer, "404");

        // The file must be the one offered: of its size...
        await Reject("HTML returned with status 200", Client(offer, new StringContent("<html>Temporarily unavailable</html>")), offer, "not the size published");
        await Reject("Empty response", Client(offer, new ByteArrayContent([])), offer, "not the size published");
        await Reject("One-byte response", Client(offer, new ByteArrayContent([(byte)'M'])), offer, "not the size published");
        var unread = new NonSeekableStream(bytes);
        var misstated = new TestContent(unread); misstated.Headers.ContentLength = bytes.Length + 500;
        await Reject("A stated length that is not the offer's", Client(offer, misstated), offer, "not the size published");
        Check(unread.BytesRead == 0, "Nothing is fetched of a file whose stated length is not the offer's");
        var truncated = new TestContent(new NonSeekableStream(bytes[..^500])); truncated.Headers.ContentLength = bytes.Length;
        await Reject("Truncated response", Client(offer, truncated), offer, "incomplete");
        await Reject("Truncated response, its length not stated", Client(offer, new TestContent(new NonSeekableStream(bytes[..^500]))), offer, "incomplete");
        byte[] longer = [.. bytes, .. new byte[300000]];
        var overlong = new NonSeekableStream(longer);
        await Reject("A file longer than the offer, its length not stated", Client(offer, new TestContent(overlong)), offer, "not the size published");
        Check(overlong.BytesRead > bytes.Length && overlong.BytesRead < longer.Length, "A file longer than the offer is given up before its end");

        // ...and of its SHA-256.
        byte[] altered = (byte[])bytes.Clone();
        altered[^1] ^= 1;
        await Reject("A file of the offer's size with one bit changed", Client(offer, new ByteArrayContent(altered)), offer, "not the installer published");
        byte[] page = Encoding.ASCII.GetBytes("<html>Temporarily unavailable</html>");
        await Reject("A file that is not an installer, though it is the one published", Client(Offer(page), new ByteArrayContent(page)), Offer(page), "not a Windows installer");

        await Reject("Read failure after partial transfer", Client(offer, new TestContent(new FailingStream(bytes))), offer, "Simulated connection loss");

        using var alreadyCancelled = new CancellationTokenSource();
        alreadyCancelled.Cancel();
        await Reject("Cancellation before download", Client(offer, new ByteArrayContent(bytes)), offer, null, alreadyCancelled.Token);

        using var duringRead = new CancellationTokenSource();
        duringRead.CancelAfter(150);
        await Reject("Cancellation during response body", Client(offer, new TestContent(new WaitingStream())), offer, null, duringRead.Token);

        using var afterRead = new CancellationTokenSource();
        var cancelOnEof = new TestContent(new CancelAtEndStream(bytes, afterRead));
        await Reject("Cancellation at end of response", Client(offer, cancelOnEof), offer, null, afterRead.Token);
    }

    // The installer's address is answered with redirects, as GitHub answers it.  Each is followed only to an https address.
    private static async Task RunRedirects()
    {
        byte[] bytes = new byte[50000];
        new Random(7).NextBytes(bytes);
        bytes[0] = (byte)'M'; bytes[1] = (byte)'Z';
        UpdateOffer offer = Offer(bytes);
        var first = new Uri("https://release-assets.example.net/file/1?token=abc");
        var second = new Uri("https://release-assets.example.net/file/2?token=def");

        var chain = new Routes()
            .On(offer.Url, _ => Redirect(first.AbsoluteUri))
            .On(first, _ => Redirect("/file/2?token=def", HttpStatusCode.TemporaryRedirect))
            .On(second, _ => Ok(new ByteArrayContent(bytes)));
        using (var client = new HttpClient(chain))
        using (UpdateInstaller installer = await UpdateInstaller.DownloadAsync(client, offer, null, CancellationToken.None, NoCheck))
        {
            Check(File.ReadAllBytes(installer.FilePath).SequenceEqual(bytes) && chain.Requested.SequenceEqual([offer.Url, first, second]),
                "Redirects are followed from the installer's address, an absolute one and a relative one, to the file");
            Check(File.ReadAllText(installer.FilePath + ":Zone.Identifier").Contains("HostUrl=" + offer.Url.AbsoluteUri + "\r\n"),
                "The file is marked with the installer's lasting address, not the one it was redirected to");
        }

        var insecure = new Routes().On(offer.Url, _ => Redirect("http://release-assets.example.net/file/1"));
        await Reject("Redirect to an insecure address", new HttpClient(insecure), offer, "secure connection");
        Check(insecure.Requested.SequenceEqual([offer.Url]), "An insecure address is never asked for");
        var later = new Routes().On(offer.Url, _ => Redirect(first.AbsoluteUri)).On(first, _ => Redirect("http://release-assets.example.net/file/2"));
        await Reject("Redirect to an insecure address at the second step", new HttpClient(later), offer, "secure connection");
        Check(later.Requested.SequenceEqual([offer.Url, first]), "An insecure address is never asked for, wherever it comes in the chain");
        await Reject("A client that follows redirects itself and ends on an insecure address", new HttpClient(new Routes().On(offer.Url, request =>
        {
            request.RequestUri = new Uri("http://release-assets.example.net/file/1");
            return Ok(new ByteArrayContent(bytes));
        })), offer, "secure connection");

        await Reject("Redirect without an address", new HttpClient(new Routes().On(offer.Url, _ => new HttpResponseMessage(HttpStatusCode.Found))), offer, "without an address");
        var loop = new Routes().On(offer.Url, _ => Redirect(first.AbsoluteUri)).On(first, _ => Redirect(offer.Url.AbsoluteUri));
        await Reject("Redirects without end", new HttpClient(loop), offer, "too many times");
        Check(loop.Requested.Count == 11, "A chain of redirects is given up after ten");
        await Reject("A redirect answered with another failure", new HttpClient(new Routes().On(offer.Url, _ => Redirect(first.AbsoluteUri))
            .On(first, _ => new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("Forbidden") })), offer, "403");
    }

    private static void RunDialogs()
    {
        UpdateOffer offer = UpdateOffer.Create(new Version(5, 3, 17), 4, Convert.ToHexString(SHA256.HashData([(byte)'M', (byte)'Z', 0, 0])));
        using (var dialog = new frmDownloadUpdate(async (progress, token) =>
        {
            using var client = Client(offer, new ByteArrayContent([(byte)'M', (byte)'Z', 0, 0]));
            return await UpdateInstaller.DownloadAsync(client, offer, progress, token, NoCheck);
        }, offer))
        {
            dialog.Shown += (_, _) =>
            {
                Control layout = dialog.Controls[0];
                Check(layout.Controls.Cast<Control>().All(c => layout.ClientRectangle.Contains(c.Bounds)),
                    $"Progress text and Cancel button fit the dialog at {dialog.DeviceDpi} DPI");
                Check(layout.Controls[0].Text.StartsWith("Downloading the StatsDirect 5.3.17 installer.", StringComparison.Ordinal),
                    "The dialog names the version it downloads");
            };
            Check(dialog.ShowDialog() == DialogResult.OK && dialog.Installer != null, "Dialog returns a completed download");
            using UpdateInstaller installer = dialog.Installer;
        }

        // A download that fails, as one whose file fails the updater's check does.  The dialog gives no installer, and StatsDirect
        // closes for an update only when the dialog gives one: so nothing is closed and nothing is started.  The user is told why,
        // and where to download the installer by hand.
        string told = null;
        int before = Directory.GetDirectories(Path.GetTempPath(), "StatsDirect-update-*").Length;
        byte[] unsigned = [(byte)'M', (byte)'Z', 0, 0];
        using (var dialog = new frmDownloadUpdate(async (progress, token) =>
        {
            using var client = Client(offer, new ByteArrayContent(unsigned));
            return await UpdateInstaller.DownloadAsync(client, offer, progress, token);
        }, offer, message => told = message))
            Check(dialog.ShowDialog() == DialogResult.Cancel && dialog.Installer == null && told != null
                    && told.StartsWith("The update could not be downloaded. StatsDirect will stay open.", StringComparison.Ordinal)
                    && told.Contains("not signed by StatsDirect Limited") && told.EndsWith("download the installer from https://www.statsdirect.com/download.aspx.", StringComparison.Ordinal)
                    && Directory.GetDirectories(Path.GetTempPath(), "StatsDirect-update-*").Length == before,
                "A download whose file fails the updater's check gives the dialog no installer and keeps nothing; the user is told why, and where to download it by hand");

        bool cancelled = false;
        using (var dialog = new frmDownloadUpdate(async (progress, token) =>
        {
            try { await Task.Delay(Timeout.Infinite, token); }
            catch (OperationCanceledException) { cancelled = true; throw; }
            return null;
        }))
        using (var timer = new System.Windows.Forms.Timer { Interval = 150 })
        {
            timer.Tick += (_, _) => { timer.Stop(); dialog.Close(); };
            timer.Start();
            Check(dialog.ShowDialog() == DialogResult.Cancel && dialog.Installer == null && cancelled,
                "Closing the dialog cancels and waits for the transfer");
        }
    }

    // An offer with a mirror: the file comes from the release's address while that can be fetched, and from the mirror on
    // www.statsdirect.com when it cannot, as on a network that blocks GitHub.  Either way it must be the file published.
    private static async Task RunMirrors()
    {
        byte[] bytes = new byte[120000];
        new Random(11).NextBytes(bytes);
        bytes[0] = (byte)'M'; bytes[1] = (byte)'Z';
        const string site = "https://www.statsdirect.com/download/StatsDirectSetup.exe";
        UpdateOffer offer = UpdateOffer.Create(new Version(9, 9, 9), bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)), site);
        Uri mirror = offer.Mirror;
        var brief = new UpdateDownloadLimits(TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(60));
        byte[] altered = (byte[])bytes.Clone();
        altered[^1] ^= 1;

        var both = new Routes().On(offer.Url, _ => Ok(new ByteArrayContent(bytes))).On(mirror, _ => Ok(new ByteArrayContent(bytes)));
        using (var client = new HttpClient(both))
        using (UpdateInstaller installer = await UpdateInstaller.DownloadAsync(client, offer, null, CancellationToken.None, NoCheck))
            Check(both.Requested.SequenceEqual([offer.Url]) && installer.Source == offer.Url
                    && File.ReadAllText(installer.FilePath + ":Zone.Identifier").Contains("HostUrl=" + offer.Url.AbsoluteUri + "\r\n"),
                "While the release's address serves the file, the mirror is not asked");

        // Each way the release's address can fail to give the file.
        foreach ((string name, Func<Routes, Routes> first) in new (string, Func<Routes, Routes>)[]
        {
            ("cannot be reached", r => r.OnAsync(offer.Url, (_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("No such host is known. (github.com:443)")))),
            ("is refused by the network", r => r.On(offer.Url, _ => new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("<html>Blocked by policy</html>") })),
            ("is answered with a page in place of the file", r => r.On(offer.Url, _ => Ok(new StringContent("<html>Blocked by policy</html>")))),
            ("is answered with a file of the right size and another SHA-256", r => r.On(offer.Url, _ => Ok(new ByteArrayContent(altered)))),
            ("is cut short", r => r.On(offer.Url, _ => Ok(new TestContent(new NonSeekableStream(bytes[..^500]))))),
            ("breaks off part of the way through", r => r.On(offer.Url, _ => Ok(new TestContent(new FailingStream(bytes))))),
            ("is redirected to an insecure address", r => r.On(offer.Url, _ => Redirect("http://release-assets.example.net/file/1"))),
            ("does not answer", r => r.OnAsync(offer.Url, async (_, token) => { await Task.Delay(Timeout.Infinite, token); return null; })),
            ("stops part of the way through", r => r.On(offer.Url, _ => Ok(new TestContent(new StallingStream(bytes, 30000))))),
            ("is redirected to an address that does not answer", r => r.On(offer.Url, _ => Redirect("https://release-assets.example.net/file/1"))
                .OnAsync(new Uri("https://release-assets.example.net/file/1"), async (_, token) => { await Task.Delay(Timeout.Infinite, token); return null; }))
        })
        {
            Routes routes = first(new Routes()).On(mirror, _ => Ok(new ByteArrayContent(bytes)));
            var progress = new RecordedProgress();
            using var client = new HttpClient(routes);
            using UpdateInstaller installer = await UpdateInstaller.DownloadAsync(client, offer, progress, CancellationToken.None, NoCheck, brief);
            Check(File.ReadAllBytes(installer.FilePath).SequenceEqual(bytes) && !File.Exists(installer.FilePath + ".partial")
                    && installer.Source == mirror && routes.Requested[0] == offer.Url && routes.Requested[^1] == mirror
                    && File.ReadAllText(installer.FilePath + ":Zone.Identifier").Contains("HostUrl=" + site + "\r\n")
                    && progress.Values.Where(p => p.Source != null).Select(p => p.Source).SequenceEqual(["github.com", "www.statsdirect.com"])
                    && progress.Values.Last() == new UpdateDownloadProgress(bytes.Length, bytes.Length),
                "The mirror gives the file when the release's address " + name);
        }

        // The mirror is the last address: it is given all the time there is, and so is an address that has no mirror.
        var patient = new Routes().On(offer.Url, _ => new HttpResponseMessage(HttpStatusCode.Forbidden))
            .OnAsync(mirror, async (_, token) => { await Task.Delay(700, token); return Ok(new TestContent(new SlowStream(bytes, 700))); });
        using (var client = new HttpClient(patient))
        using (UpdateInstaller installer = await UpdateInstaller.DownloadAsync(client, offer, null, CancellationToken.None, NoCheck, brief))
            Check(installer.Source == mirror && File.ReadAllBytes(installer.FilePath).SequenceEqual(bytes), "The mirror is waited on for longer than the release's address is");
        UpdateOffer alone = Offer(bytes);
        var lone = new Routes().OnAsync(alone.Url, async (_, token) => { await Task.Delay(700, token); return Ok(new TestContent(new SlowStream(bytes, 700))); });
        using (var client = new HttpClient(lone))
        using (UpdateInstaller installer = await UpdateInstaller.DownloadAsync(client, alone, null, CancellationToken.None, NoCheck, brief))
            Check(installer.Source == alone.Url && File.ReadAllBytes(installer.FilePath).SequenceEqual(bytes), "An address with no mirror after it is waited on as before");

        // The mirror is followed through https redirects too, and must give the same file.
        var forwarded = new Routes().On(offer.Url, _ => new HttpResponseMessage(HttpStatusCode.Forbidden))
            .On(mirror, _ => Redirect("https://files.example.net/StatsDirectSetup.exe")).On(new Uri("https://files.example.net/StatsDirectSetup.exe"), _ => Ok(new ByteArrayContent(bytes)));
        using (var client = new HttpClient(forwarded))
        using (UpdateInstaller installer = await UpdateInstaller.DownloadAsync(client, offer, null, CancellationToken.None, NoCheck))
            Check(installer.Source == mirror && forwarded.Requested.Count == 3 && File.ReadAllBytes(installer.FilePath).SequenceEqual(bytes), "A mirror that redirects to another https address is followed");
        await Reject("A mirror that redirects to an insecure address", new HttpClient(new Routes().On(offer.Url, _ => new HttpResponseMessage(HttpStatusCode.Forbidden))
            .On(mirror, _ => Redirect("http://files.example.net/StatsDirectSetup.exe"))), offer, "secure connection", verify: NoCheck);
        await Reject("A mirror whose file is not the one published", new HttpClient(new Routes().On(offer.Url, _ => new HttpResponseMessage(HttpStatusCode.Forbidden))
            .On(mirror, _ => Ok(new ByteArrayContent(altered)))), offer, "not the installer published", verify: NoCheck);
        await Reject("Neither address gives the file", new HttpClient(new Routes().On(offer.Url, _ => new HttpResponseMessage(HttpStatusCode.Forbidden))
            .On(mirror, _ => new HttpResponseMessage(HttpStatusCode.NotFound))), offer, "404", verify: NoCheck);

        // The mirror holds the same file, so a file that arrived whole and failed the updater's own check is not fetched again.
        var whole = new Routes().On(offer.Url, _ => Ok(new ByteArrayContent(bytes))).On(mirror, _ => Ok(new ByteArrayContent(bytes)));
        await Reject("A file that arrived whole from the release's address and is not signed by StatsDirect Limited", new HttpClient(whole), offer, "not signed by StatsDirect Limited");
        Check(whole.Requested.SequenceEqual([offer.Url]), "The mirror is not asked for a file that arrived whole and failed the check of its signature");

        // Cancelling stops the download: the mirror is not turned to.
        using var cancelled = new CancellationTokenSource();
        cancelled.CancelAfter(150);
        var waiting = new Routes().On(offer.Url, _ => Ok(new TestContent(new WaitingStream()))).On(mirror, _ => Ok(new ByteArrayContent(bytes)));
        await Reject("Cancellation while the release's address is being fetched", new HttpClient(waiting), offer, null, cancelled.Token, NoCheck);
        Check(waiting.Requested.SequenceEqual([offer.Url]), "After a cancellation the mirror is not asked");
        // A request made after the cancellation would not reach the address, so what the download announced is looked at too.
        using var cancelledAgain = new CancellationTokenSource();
        cancelledAgain.CancelAfter(150);
        var announced = new RecordedProgress();
        bool stopped = false;
        using (var client = new HttpClient(new Routes().On(offer.Url, _ => Ok(new TestContent(new WaitingStream()))).On(mirror, _ => Ok(new ByteArrayContent(bytes)))))
        {
            try { using UpdateInstaller unexpected = await UpdateInstaller.DownloadAsync(client, offer, announced, cancelledAgain.Token, NoCheck); }
            catch (OperationCanceledException) { stopped = true; }
        }
        Check(stopped && announced.Values.Where(p => p.Source != null).Select(p => p.Source).SequenceEqual(["github.com"]),
            "After a cancellation the download stops, and the mirror is not announced as the next address");
    }

    // The time a download has.  Each address has its own, so the release's address cannot use up the time the mirror needs.
    // (Codex's review of 11afcbbc: a first address that kept sending, a byte at a time, was waited on until the one limit over the
    // whole download ran out, and the mirror was never asked.)
    private static async Task RunLimits()
    {
        byte[] bytes = new byte[120000];
        new Random(23).NextBytes(bytes);
        bytes[0] = (byte)'M'; bytes[1] = (byte)'Z';
        var version = new Version(9, 9, 9);
        UpdateOffer offer = UpdateOffer.Create(version, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)), UpdateOffer.VersionedMirrorAddress(version));
        Uri mirror = offer.Mirror;
        var watch = Stopwatch.StartNew();

        // A release's address that never falls silent but sends too slowly to finish: given up once that is plain, well within its time.
        var judged = new UpdateDownloadLimits(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(60));
        var trickle = new TrickleStream(bytes, 50);
        var trickling = new Routes().On(offer.Url, _ => Ok(new TestContent(trickle))).On(mirror, _ => Ok(new ByteArrayContent(bytes)));
        using (var client = new HttpClient(trickling))
        using (UpdateInstaller installer = await UpdateInstaller.DownloadAsync(client, offer, null, CancellationToken.None, NoCheck, judged))
            Check(installer.Source == mirror && File.ReadAllBytes(installer.FilePath).SequenceEqual(bytes) && !File.Exists(installer.FilePath + ".partial")
                    && trickling.Requested.SequenceEqual([offer.Url, mirror]) && trickle.BytesRead is > 0 and < 1000 && watch.Elapsed < TimeSpan.FromSeconds(10),
                $"A release's address that sends a byte at a time is given up once it is plain that it cannot finish in its time, and the mirror gives the file ({trickle.BytesRead} bytes taken in {watch.Elapsed.TotalSeconds:F1} s of 20 s)");

        // The same address where the rate is not yet judged: its time still ends, and the mirror has its own time after it.
        var ended = new UpdateDownloadLimits(TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(700), TimeSpan.FromSeconds(60));
        watch.Restart();
        var held = new Routes().On(offer.Url, _ => Ok(new TestContent(new TrickleStream(bytes, 50)))).On(mirror, _ => Ok(new ByteArrayContent(bytes)));
        using (var client = new HttpClient(held))
        using (UpdateInstaller installer = await UpdateInstaller.DownloadAsync(client, offer, null, CancellationToken.None, NoCheck, ended))
            Check(installer.Source == mirror && File.ReadAllBytes(installer.FilePath).SequenceEqual(bytes) && watch.Elapsed >= TimeSpan.FromMilliseconds(600) && watch.Elapsed < TimeSpan.FromSeconds(10),
                $"A release's address that is still sending when its time ends is given up, and the mirror gives the file ({watch.Elapsed.TotalSeconds:F1} s)");

        // A chain of redirects, none of them slow to answer, that takes longer in all than the release's address has.
        var chain = new Routes();
        Uri at = offer.Url, last = null;
        for (int hop = 1; hop <= 8; hop++)
        {
            Uri next = new("https://release-assets.example.net/hop/" + hop);
            chain.OnAsync(at, async (_, token) => { await Task.Delay(250, token); return Redirect(next.AbsoluteUri); });
            at = last = next;
        }
        chain.On(last, _ => Ok(new ByteArrayContent(bytes))).On(mirror, _ => Ok(new ByteArrayContent(bytes)));
        var hurried = new UpdateDownloadLimits(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60));
        watch.Restart();
        using (var client = new HttpClient(chain))
        using (UpdateInstaller installer = await UpdateInstaller.DownloadAsync(client, offer, null, CancellationToken.None, NoCheck, hurried))
            Check(installer.Source == mirror && File.ReadAllBytes(installer.FilePath).SequenceEqual(bytes) && !chain.Requested.Contains(last) && chain.Requested[^1] == mirror
                    && watch.Elapsed < TimeSpan.FromSeconds(10),
                $"A chain of redirects that takes longer than the release's address has is given up part of the way along, and the mirror gives the file ({chain.Requested.Count - 1} of 9 addresses asked in {watch.Elapsed.TotalSeconds:F1} s)");

        // A release's address that is slow but on course is left to finish: a slower connection is not sent to the mirror for nothing.
        var onCourse = new UpdateDownloadLimits(TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(60));
        var steady = new Routes().On(offer.Url, _ => Ok(new TestContent(new SteadyStream(bytes, 10000, 40)))).On(mirror, _ => Ok(new ByteArrayContent(bytes)));
        using (var client = new HttpClient(steady))
        using (UpdateInstaller installer = await UpdateInstaller.DownloadAsync(client, offer, null, CancellationToken.None, NoCheck, onCourse))
            Check(installer.Source == offer.Url && File.ReadAllBytes(installer.FilePath).SequenceEqual(bytes) && steady.Requested.SequenceEqual([offer.Url]),
                "A release's address that sends slowly, but fast enough to finish in its time, is left to finish, and the mirror is not asked");

        // The last address has an end as well: the mirror, and an address with no mirror.
        var last600 = new UpdateDownloadLimits(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(600));
        UpdateOffer alone = Offer(bytes);
        await Reject("An only address that is still sending when the download's time ends", new HttpClient(new Routes().On(alone.Url, _ => Ok(new TestContent(new TrickleStream(bytes, 50))))),
            alone, "took too long", verify: NoCheck, limits: last600);
        await Reject("A mirror that is still sending when its time ends", new HttpClient(new Routes().On(offer.Url, _ => new HttpResponseMessage(HttpStatusCode.Forbidden))
            .On(mirror, _ => Ok(new TestContent(new TrickleStream(bytes, 50))))), offer, "took too long", verify: NoCheck, limits: last600);
        // An only address is judged neither on its silences nor on its rate, as before: it has the last address's time.
        var patient = new UpdateDownloadLimits(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(60));
        using (var client = new HttpClient(new Routes().On(alone.Url, _ => Ok(new TestContent(new SteadyStream(bytes, 30000, 250))))))
        using (UpdateInstaller installer = await UpdateInstaller.DownloadAsync(client, alone, null, CancellationToken.None, NoCheck, patient))
            Check(installer.Source == alone.Url && File.ReadAllBytes(installer.FilePath).SequenceEqual(bytes), "An only address is not held to the first address's limits");

        // The user's Cancel stops the download wherever it has got to, and nothing is kept.
        using var early = new CancellationTokenSource();
        early.CancelAfter(200);
        var sending = new Routes().On(offer.Url, _ => Ok(new TestContent(new TrickleStream(bytes, 50)))).On(mirror, _ => Ok(new ByteArrayContent(bytes)));
        await Reject("Cancellation while the release's address is sending", new HttpClient(sending), offer, null, early.Token, NoCheck, judged);
        Check(sending.Requested.SequenceEqual([offer.Url]), "After a cancellation while the release's address is sending, the mirror is not asked");

        // Between the two addresses: the release's address fails at the moment the user cancels.
        using var between = new CancellationTokenSource();
        var failing = new Routes().On(offer.Url, _ => { between.Cancel(); return new HttpResponseMessage(HttpStatusCode.Forbidden); }).On(mirror, _ => Ok(new ByteArrayContent(bytes)));
        var announced = new RecordedProgress();
        var before = Directory.GetDirectories(Path.GetTempPath(), "StatsDirect-update-*").ToHashSet();
        string outcome = "completed";
        using (var client = new HttpClient(failing))
        {
            try { using UpdateInstaller unexpected = await UpdateInstaller.DownloadAsync(client, offer, announced, between.Token, NoCheck); }
            catch (OperationCanceledException) { outcome = "cancelled"; }
            catch (Exception ex) when (ex is HttpRequestException or IOException) { outcome = "failed: " + ex.Message; }
        }
        Check(outcome == "cancelled" && failing.Requested.SequenceEqual([offer.Url]) && announced.Values.Where(p => p.Source != null).Select(p => p.Source).SequenceEqual(["github.com"])
                && !Directory.GetDirectories(Path.GetTempPath(), "StatsDirect-update-*").Except(before).Any(),
            $"A cancellation as the release's address fails ends the download as cancelled, not as failed, and the mirror is neither asked nor announced ({outcome})");

        // While the mirror is sending.
        using var late = new CancellationTokenSource();
        var onMirror = new Routes().On(offer.Url, _ => new HttpResponseMessage(HttpStatusCode.Forbidden))
            .On(mirror, _ => { late.CancelAfter(150); return Ok(new TestContent(new WaitingStream())); });
        await Reject("Cancellation while the mirror is sending", new HttpClient(onMirror), offer, null, late.Token, NoCheck);
        Check(onMirror.Requested.SequenceEqual([offer.Url, mirror]), "A cancellation while the mirror is sending stops the download there");
    }

    // The release's checks of where the file comes from.  (Codex's review of 11afcbbc: a check that may fall back to the mirror
    // says nothing about the file at GitHub, so a release could pass it with that file missing or wrong.)
    private static async Task RunGates()
    {
        byte[] bytes = new byte[90000];
        new Random(31).NextBytes(bytes);
        bytes[0] = (byte)'M'; bytes[1] = (byte)'Z';
        byte[] altered = (byte[])bytes.Clone();
        altered[^1] ^= 1;
        var version = new Version(9, 9, 9);
        string sha256 = Convert.ToHexString(SHA256.HashData(bytes));
        UpdateOffer offer = UpdateOffer.Create(version, bytes.Length, sha256, UpdateOffer.VersionedMirrorAddress(version));
        Uri mirror = offer.Mirror, held = new("https://release-assets.example.net/file/1?token=abc");
        Check(mirror.AbsoluteUri == "https://www.statsdirect.com/download/v9.9.9/StatsDirectSetup.exe" && offer.WithoutMirror().Mirror == null && offer.WithoutMirror().Url == offer.Url
                && UpdateOffer.Parse(offer.ToManifest()) == offer,
            "A release's mirror is the installer in the website's folder for that version, and the manifest that names it reads back");

        // The file is missing at GitHub, or wrong there, and the mirror is in good order.
        Routes Missing() => new Routes().On(offer.Url, _ => new HttpResponseMessage(HttpStatusCode.NotFound)).On(mirror, _ => Ok(new ByteArrayContent(bytes)));
        Routes Wrong() => new Routes().On(offer.Url, _ => Ok(new ByteArrayContent(altered))).On(mirror, _ => Ok(new ByteArrayContent(bytes)));
        using (UpdateInstaller either = await DownloadFrom(Missing(), offer, From.Either, NoCheck))
            Check(either.Source == mirror, "As the updater downloads, a file missing at GitHub is made good by the mirror: that download cannot be the check of GitHub");
        await NoGate("The check of the release's address fails when the file is missing at GitHub, though the mirror is in good order", Missing(), offer, From.GitHub, "404");
        await NoGate("The check of the release's address fails when the file at GitHub is not the one published, though the mirror is in good order", Wrong(), offer, From.GitHub, "not the installer published");

        // The right file at GitHub, sent on by GitHub to where it is held, with a mirror that would fail.
        var healthy = new Routes().On(offer.Url, _ => Redirect(held.AbsoluteUri)).On(held, _ => Ok(new ByteArrayContent(bytes))).On(mirror, _ => Ok(new ByteArrayContent(altered)));
        using (UpdateInstaller fromGitHub = await DownloadFrom(healthy, offer, From.GitHub, NoCheck))
            Check(fromGitHub.Source == offer.Url && healthy.Requested.SequenceEqual([offer.Url, held]) && File.ReadAllBytes(fromGitHub.FilePath).SequenceEqual(bytes),
                "The check of the release's address passes on the right file, redirected by GitHub to where it is held, and does not ask the mirror");

        // The mirror's own check: GitHub is refused, so nothing but the mirror can give the file.
        var both = new Routes().On(offer.Url, _ => Ok(new ByteArrayContent(bytes))).On(mirror, _ => Ok(new ByteArrayContent(bytes)));
        using (UpdateInstaller fromMirror = await DownloadFrom(both, offer, From.Mirror, NoCheck))
            Check(fromMirror.Source == mirror && both.Requested.SequenceEqual([mirror]) && File.ReadAllBytes(fromMirror.FilePath).SequenceEqual(bytes),
                "The check of the mirror takes the file from the mirror named in the manifest, and no request reaches GitHub");
        await NoGate("The check of the mirror fails when the manifest names no mirror", new Routes().On(offer.Url, _ => Ok(new ByteArrayContent(bytes))), offer.WithoutMirror(), From.Mirror, "names no mirror");
        await NoGate("The check of the mirror fails when the mirror's file is not the one published, though GitHub's is", new Routes().On(offer.Url, _ => Ok(new ByteArrayContent(bytes)))
            .On(mirror, _ => Ok(new ByteArrayContent(altered))), offer, From.Mirror, "not the installer published");
        await NoGate("The check of the mirror fails when the mirror has no file, though GitHub has", new Routes().On(offer.Url, _ => Ok(new ByteArrayContent(bytes)))
            .On(mirror, _ => new HttpResponseMessage(HttpStatusCode.NotFound)), offer, From.Mirror, "404");
        foreach (string onward in new[] { offer.Url.AbsoluteUri, "https://release-assets.githubusercontent.com/file/1", "https://objects.githubusercontent.com/file/1", "https://codeload.github.com/file/1" })
            await NoGate("The check of the mirror fails when the mirror sends the request on to " + new Uri(onward).Host, new Routes().On(mirror, _ => Redirect(onward))
                .On(new Uri(onward), _ => Ok(new ByteArrayContent(bytes))), offer, From.Mirror, "Refused for this check");
        var elsewhere = new Routes().On(mirror, _ => Redirect("https://files.example.net/v9.9.9/StatsDirectSetup.exe")).On(new Uri("https://files.example.net/v9.9.9/StatsDirectSetup.exe"), _ => Ok(new ByteArrayContent(bytes)));
        using (UpdateInstaller forwarded = await DownloadFrom(elsewhere, offer, From.Mirror, NoCheck))
            Check(forwarded.Source == mirror && elsewhere.Requested.Count == 2, "The check of the mirror passes when the mirror sends the request on to another https address that is not GitHub's");

        // An offer goes on finding its own installer at its versioned mirror after the website has moved on to the next release.
        byte[] next = new byte[bytes.Length];
        new Random(37).NextBytes(next);
        next[0] = (byte)'M'; next[1] = (byte)'Z';
        var legacy = new Uri("https://www.statsdirect.com/download/StatsDirectSetup.exe");
        var moved = new Routes().On(mirror, _ => Ok(new ByteArrayContent(bytes))).On(legacy, _ => Ok(new ByteArrayContent(next)));
        using (UpdateInstaller kept = await DownloadFrom(moved, offer, From.Mirror, NoCheck))
            Check(kept.Source == mirror && File.ReadAllBytes(kept.FilePath).SequenceEqual(bytes) && moved.Requested.SequenceEqual([mirror]),
                "After the website's own download has moved on to the next release, an offer still gets its own installer from its versioned mirror");
        UpdateOffer unversioned = UpdateOffer.Create(version, bytes.Length, sha256, legacy.AbsoluteUri);
        await NoGate("An offer whose mirror is the website's unversioned download gets the next release's file after the website has moved on, and refuses it",
            new Routes().On(legacy, _ => Ok(new ByteArrayContent(next))), unversioned, From.Mirror, "not the installer published");
    }

    // A check of where the file comes from fails, for the reason given, and keeps nothing.
    private static async Task NoGate(string name, Routes network, UpdateOffer offer, From source, string because)
    {
        var before = Directory.GetDirectories(Path.GetTempPath(), "StatsDirect-update-*").ToHashSet();
        string reason = null;
        try
        {
            using UpdateInstaller unexpected = await DownloadFrom(network, offer, source, NoCheck);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            reason = ex.Message;
        }
        if (reason != null && !reason.Contains(because, StringComparison.Ordinal))
            throw new Exception($"FAIL: {name}: failed for another reason: {reason}");
        Check(reason != null && !Directory.GetDirectories(Path.GetTempPath(), "StatsDirect-update-*").Except(before).Any(), name);
    }

    // ---- The check of the file ----

    private static string Dotnet => Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", "dotnet.exe"));
    private static string Installed => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "StatsDirect", "StatsDirect.exe");

    // A setup program signed by StatsDirect Limited, as a release publishes it: the one named by STATSDIRECT_SIGNED_SETUP, or the
    // one a release leaves in C:\temp.  Null where there is neither, as on a build machine: the checks that need it say SKIP.
    private static readonly Lazy<string> SignedSetup = new(() =>
    {
        foreach (string path in new[] { Environment.GetEnvironmentVariable("STATSDIRECT_SIGNED_SETUP"), @"C:\temp\StatsDirectSetup.exe" })
            if (!string.IsNullOrEmpty(path) && File.Exists(path) && UpdateInstallerCheck.Signer(path) == UpdateInstallerCheck.Publisher)
                return Path.GetFullPath(path);
        return null;
    });

    private static Version FileVersion(string path)
    {
        FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
        return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart);
    }

    // The updater's check itself, on real files: one with no signature, one validly signed by another publisher, the installed
    // program's own signed file and, where this machine has one, a published setup program.
    private static void RunInstallerChecks()
    {
        string own = Environment.ProcessPath;
        Check(UpdateInstallerCheck.Signer(own) == null && Refused(own, new Version(0, 0, 0), null, "not signed by StatsDirect Limited"), "A file with no signature is refused");
        if (File.Exists(Dotnet) && UpdateInstallerCheck.Signer(Dotnet) is string other)
            Check(other != UpdateInstallerCheck.Publisher && Refused(Dotnet, new Version(0, 0, 0), null, "not signed by StatsDirect Limited"), $"A file validly signed by another publisher ({other}) is refused");
        else
            Console.WriteLine("SKIP: no validly signed dotnet.exe beside the runtime to stand for another publisher");

        // The program's own file is signed by StatsDirect Limited and carries the same product and version, but it is not the
        // setup program.  (Codex's review of 11afcbbc: it was accepted, and --manifest wrote a manifest for it.)
        if (File.Exists(Installed) && UpdateInstallerCheck.Signer(Installed) == UpdateInstallerCheck.Publisher)
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(Installed);
            Check(info.ProductName == UpdateInstallerCheck.Product && info.OriginalFilename != UpdateInstallerCheck.SetupName
                    && Refused(Installed, new Version(0, 0, 0), null, "is not the StatsDirect installer") && Refused(Installed, new Version(0, 0, 0), FileVersion(Installed), "is not the StatsDirect installer"),
                $"The installed program's own file, signed by StatsDirect Limited, is refused: it is {info.OriginalFilename}, not the setup program");
            bool wrote = true;
            try { WriteManifest(Installed, Path.Combine(Path.GetTempPath(), "StatsDirect-update-check-not-written.json"), null, true); }
            catch (IOException ex) when (ex.Message.Contains("is not the StatsDirect installer")) { wrote = false; }
            Check(!wrote && !File.Exists(Path.Combine(Path.GetTempPath(), "StatsDirect-update-check-not-written.json")), "No manifest is written for the installed program's own file");
        }
        else
            Console.WriteLine("SKIP: no installed StatsDirect signed by StatsDirect Limited on this machine to stand for a file of ours that is not the setup program");

        if (SignedSetup.Value is string setup)
        {
            Version version = FileVersion(setup);
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(setup);
            UpdateInstallerCheck.Verify(setup, new Version(0, 0, 0));
            UpdateInstallerCheck.Verify(setup, new Version(0, 0, 0), version);
            UpdateInstallerCheck.Verify(setup, new Version(0, 0, 0), new Version(version.Major, version.Minor, version.Build, 7));
            passed++;
            Console.WriteLine($"PASS: The published setup program of StatsDirect {version} ({info.OriginalFilename}), signed by StatsDirect Limited, newer than the version running and of the version offered, is accepted");
            Check(Refused(setup, new Version(999, 0, 0), null, "not a newer version of StatsDirect") && Refused(setup, version, version, "not a newer version of StatsDirect"),
                "A signed setup program that is not newer than the version running is refused");
            Check(Refused(setup, new Version(0, 0, 0), new Version(version.Major, version.Minor, version.Build + 1), "not the version offered")
                    && Refused(setup, new Version(0, 0, 0), new Version(version.Major, version.Minor, version.Build > 0 ? version.Build - 1 : version.Build + 2), "not the version offered"),
                "A signed setup program that is newer, but not the version offered, is refused");

            // The release's own tool, on the published setup program: the manifest it writes names the versioned mirror and reads back.
            string written = Path.Combine(Path.GetTempPath(), "StatsDirect-update-check-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                int exit = WriteManifest(setup, written, null, true);
                UpdateOffer read = UpdateOffer.Parse(File.ReadAllText(written));
                Check(exit == 0 && read.Version == version && read.Size == new FileInfo(setup).Length && read.MirrorAddress == UpdateOffer.VersionedMirrorAddress(version)
                        && !File.ReadAllText(written).Contains('\r') && File.ReadAllBytes(written)[0] == (byte)'{',
                    $"The manifest written for the published setup program gives its version, size and SHA-256, and its mirror in the website's folder for {version}");
            }
            finally
            {
                File.Delete(written);
            }
        }
        else
            Console.WriteLine("SKIP: no setup program signed by StatsDirect Limited on this machine (STATSDIRECT_SIGNED_SETUP, or C:\\temp\\StatsDirectSetup.exe); a release is checked with --verify");
        Check(UpdateInstallerCheck.RunningVersion == UpdateOffer.ThreeParts(typeof(Program).Assembly.GetName().Version), "The version running is this program's, in three parts");
    }

    // The whole download with the updater's own check, on real signed files served as the installer.
    private static async Task RunSignedDownloads()
    {
        if (File.Exists(Dotnet) && UpdateInstallerCheck.Signer(Dotnet) is string other && other != UpdateInstallerCheck.Publisher)
        {
            byte[] bytes = File.ReadAllBytes(Dotnet);
            UpdateOffer offer = Offer(bytes, "999.0.0");
            await Reject($"A download that is the file published, but signed by another publisher ({other})", Client(offer, new ByteArrayContent(bytes)), offer, "not signed by StatsDirect Limited", verify: null);
        }
        else
            Console.WriteLine("SKIP: no validly signed dotnet.exe beside the runtime to serve as a download from another publisher");

        if (File.Exists(Installed) && UpdateInstallerCheck.Signer(Installed) == UpdateInstallerCheck.Publisher)
        {
            byte[] bytes = File.ReadAllBytes(Installed);
            Version version = FileVersion(Installed);
            UpdateOffer offer = Offer(bytes, (version > UpdateInstallerCheck.RunningVersion ? version : new Version(999, 0, 0)).ToString(3));
            await Reject("A download that is the file published and the program's own signed file, under the installer's name", Client(offer, new ByteArrayContent(bytes)), offer, "is not the StatsDirect installer", verify: null);
        }
        else
            Console.WriteLine("SKIP: no installed StatsDirect signed by StatsDirect Limited on this machine to serve as a download of ours that is not the setup program");

        if (SignedSetup.Value is string setup && FileVersion(setup) > UpdateInstallerCheck.RunningVersion)
        {
            // The published setup program itself, taken through the whole download from each of its two addresses in turn.
            Version version = FileVersion(setup);
            long length = new FileInfo(setup).Length;
            string sha256;
            await using (FileStream file = File.OpenRead(setup))
                sha256 = Convert.ToHexString(await SHA256.HashDataAsync(file));
            UpdateOffer offer = UpdateOffer.Create(version, length, sha256, UpdateOffer.VersionedMirrorAddress(version));

            var github = new Routes().On(offer.Url, _ => Ok(new StreamContent(File.OpenRead(setup))));
            using (UpdateInstaller installer = await DownloadFrom(github, offer, From.GitHub, null))
                Check(installer.Source == offer.Url && new FileInfo(installer.FilePath).Length == length
                        && File.ReadAllText(installer.FilePath + ":Zone.Identifier").Contains("HostUrl=" + offer.Url.AbsoluteUri + "\r\n"),
                    $"The published setup program of StatsDirect {version}, served as the release's file, passes the whole download with the updater's own check");

            var site = new Routes().On(offer.Mirror, _ => Ok(new StreamContent(File.OpenRead(setup))));
            using (UpdateInstaller installer = await DownloadFrom(site, offer, From.Mirror, null))
                Check(installer.Source == offer.Mirror && new FileInfo(installer.FilePath).Length == length && site.Requested.SequenceEqual([offer.Mirror])
                        && File.ReadAllText(installer.FilePath + ":Zone.Identifier").Contains("HostUrl=" + offer.Mirror.AbsoluteUri + "\r\n"),
                    $"The published setup program, served from its versioned mirror with GitHub blocked, passes the whole download with the updater's own check");

            UpdateOffer another = UpdateOffer.Create(new Version(version.Major, version.Minor, version.Build + 1), length, sha256);
            await Reject("The published setup program, offered as another version", new HttpClient(new Routes().On(another.Url, _ => Ok(new StreamContent(File.OpenRead(setup))))),
                another, "not the version offered", verify: null);
        }
        else
            Console.WriteLine("SKIP: no setup program signed by StatsDirect Limited on this machine to take through the whole download");
    }

    private static bool Refused(string path, Version running, Version offered, string reason)
    {
        try { UpdateInstallerCheck.Verify(path, running, offered); return false; }
        catch (IOException ex) { return ex.Message.Contains(reason); }
    }

    // The download is refused, for the reason given (or, with none given, because it was cancelled), and nothing of it is kept.
    // The updater's own check is made unless another is given, so a file that came through the transfer when it should not have
    // is still refused, for another reason than the one expected, and the check fails.
    private static async Task Reject(string name, HttpClient client, UpdateOffer offer, string because, CancellationToken token = default, Action<string> verify = null,
        UpdateDownloadLimits limits = null)
    {
        var before = Directory.GetDirectories(Path.GetTempPath(), "StatsDirect-update-*").ToHashSet();
        bool rejected = false;
        string reason = null;
        using (client)
        {
            try { using UpdateInstaller unexpected = await UpdateInstaller.DownloadAsync(client, offer, null, token, verify, limits); }
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            {
                reason = ex.Message;
                rejected = because != null && reason.Contains(because, StringComparison.Ordinal);
            }
            catch (OperationCanceledException) { rejected = because == null && token.IsCancellationRequested; }
        }
        if (!rejected && reason != null)
            throw new Exception($"FAIL: {name}: refused for another reason: {reason}");
        Check(rejected && !Directory.GetDirectories(Path.GetTempPath(), "StatsDirect-update-*").Except(before).Any(),
            name + ": no installer or partial files retained");
    }

    // ---- Simulated answers ----

    private static HttpClient Client(UpdateOffer offer, HttpContent content, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new Routes().On(offer.Url, _ => new HttpResponseMessage(status) { Content = content }));

    private static HttpResponseMessage Ok(HttpContent content) => new(HttpStatusCode.OK) { Content = content };

    private static HttpResponseMessage Redirect(string location, HttpStatusCode status = HttpStatusCode.Found)
    {
        var response = new HttpResponseMessage(status);
        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return response;
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        passed++;
        Console.WriteLine("PASS: " + name);
    }

    // Answers each address it has been given, and no other: a request to any other address fails the run.
    private sealed class Routes : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> answers = new(StringComparer.Ordinal);
        internal readonly List<Uri> Requested = [];

        internal Routes On(Uri address, Func<HttpRequestMessage, HttpResponseMessage> answer) =>
            OnAsync(address, (request, _) => Task.FromResult(answer(request)));

        // An answer that takes time, fails as a connection fails, or never comes.
        internal Routes OnAsync(Uri address, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer)
        {
            answers[address.AbsoluteUri] = answer;
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requested.Add(request.RequestUri);
            if (!answers.TryGetValue(request.RequestUri.AbsoluteUri, out var answer))
                throw new Exception("FAIL: an address that was not expected was asked for: " + request.RequestUri);
            HttpResponseMessage response = await answer(request, cancellationToken);
            response.RequestMessage = request;
            return response;
        }
    }

    // A network that blocks GitHub, for the release's check of the mirror: requests to GitHub and to the hosts that hold its
    // files fail as a blocked connection does, and the rest go on to the network given.
    private sealed class BlockGitHub(HttpMessageHandler network) : DelegatingHandler(network)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string host = request.RequestUri.Host;
            return host == "github.com" || host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase)
                ? Task.FromException<HttpResponseMessage>(new HttpRequestException("Refused for this check, as a network that blocks GitHub would: " + host))
                : base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class RecordedProgress : IProgress<UpdateDownloadProgress>
    {
        internal readonly List<UpdateDownloadProgress> Values = [];
        public void Report(UpdateDownloadProgress value) => Values.Add(value);
    }

    private sealed class TestContent(Stream source) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) => source.CopyToAsync(stream);
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult(source);
        protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
    }

    private class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        // Counted here, since the answer closes its stream when the download gives it up.
        internal long BytesRead { get; private set; }
        public override bool CanSeek => false;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int count = await base.ReadAsync(buffer, cancellationToken);
            BytesRead += count;
            return count;
        }
    }

    private sealed class FailingStream(byte[] bytes) : NonSeekableStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Position > 0 ? ValueTask.FromException<int>(new IOException("Simulated connection loss")) : base.ReadAsync(buffer, cancellationToken);
    }

    // Sends the file a byte at a time, never falling silent for long.
    private sealed class TrickleStream(byte[] bytes, int milliseconds) : NonSeekableStream(bytes)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(milliseconds, cancellationToken);
            return await base.ReadAsync(buffer[..1], cancellationToken);
        }
    }

    // Sends the file in parts of a size, with a wait before each.
    private sealed class SteadyStream(byte[] bytes, int part, int milliseconds) : NonSeekableStream(bytes)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(milliseconds, cancellationToken);
            return await base.ReadAsync(buffer[..Math.Min(buffer.Length, part)], cancellationToken);
        }
    }

    // Gives the first part of the file and then nothing more.
    private sealed class StallingStream(byte[] bytes, int given) : NonSeekableStream(bytes)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position >= given)
                await Task.Delay(Timeout.Infinite, cancellationToken);
            return await base.ReadAsync(buffer[..(int)Math.Min(buffer.Length, given - Position)], cancellationToken);
        }
    }

    // Gives the whole file, after a wait before its first part.
    private sealed class SlowStream(byte[] bytes, int milliseconds) : NonSeekableStream(bytes)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position == 0)
                await Task.Delay(milliseconds, cancellationToken);
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }

    private sealed class WaitingStream() : NonSeekableStream([])
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }

    private sealed class CancelAtEndStream(byte[] bytes, CancellationTokenSource cancellation) : NonSeekableStream(bytes)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int count = await base.ReadAsync(buffer, cancellationToken);
            if (count == 0) cancellation.Cancel();
            return count;
        }
    }
}
