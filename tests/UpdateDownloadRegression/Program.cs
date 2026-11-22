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
                return CheckManifest(UpdateOffer.ManifestUri.AbsoluteUri, null, download: true).GetAwaiter().GetResult();
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
                return WriteManifest(Path.GetFullPath(signed), Value(args, "--out"));
            if (Value(args, "--check-manifest") is string manifest)
                return CheckManifest(manifest, Value(args, "--running"), args.Contains("--download")).GetAwaiter().GetResult();
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

    // Writes the manifest of a signed installer.  The installer must be one the updater would start, and what is written must
    // read back, through the updater's own reader, as the same offer.
    private static int WriteManifest(string installer, string output)
    {
        UpdateInstallerCheck.Verify(installer, new Version(0, 0, 0));
        FileVersionInfo info = FileVersionInfo.GetVersionInfo(installer);
        string sha256;
        using (FileStream file = File.OpenRead(installer))
            sha256 = Convert.ToHexString(SHA256.HashData(file));
        UpdateOffer offer = UpdateOffer.Create(new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart), new FileInfo(installer).Length, sha256);
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
        Console.WriteLine("Publish it only when that address serves this file: check with --check-manifest <file> --download.");
        return 0;
    }

    // Reads a manifest, from a file or an https address, as the updater reads it; with download, also fetches its installer and
    // checks it as the updater of the version given would.  It starts nothing.
    private static async Task<int> CheckManifest(string source, string runningGiven, bool download)
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
            return 0;
        using HttpClient downloader = UpdateInstaller.CreateClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(30));
        using UpdateInstaller installer = await UpdateInstaller.DownloadAsync(downloader, offer, null, timeout.Token,
            path => UpdateInstallerCheck.Verify(path, running, offer.Version));
        Console.WriteLine($"PASS: downloaded {new FileInfo(installer.FilePath).Length:N0} bytes from {offer.Url}. Size and SHA-256 are the manifest's. It is signed by {UpdateInstallerCheck.Signer(installer.FilePath)} and is StatsDirect {offer.Version}, newer than {running}: the updater would start it. File has Internet-zone metadata. No installer launched; test download removed on exit.");
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
        Check(UpdateOffer.Parse("﻿" + release.ToManifest().Replace("\n", "\r\n")) == release
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
            "5.3.1234567890", "٥.٣.١٧", "5.3.17-beta", "5.3.17+build" })
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

        foreach (string again in new[] { "\"schema\": 1", "\"version\": \"5.3.17\"", "\"url\": \"" + Address + "\"", "\"size\": 304535768", "\"sha256\": \"" + Digest + "\"",
            "\"vers\\u0069on\": \"5.3.18\"" })
            Unreadable(Manifest(extra: again), "twice", "a field given twice: " + again);

        foreach (Action wrong in new Action[]
        {
            () => UpdateOffer.Create(new Version(5, 3, 17), 0, Digest),
            () => UpdateOffer.Create(new Version(5, 3, 17), UpdateOffer.MaximumInstallerBytes + 1, Digest),
            () => UpdateOffer.Create(new Version(5, 3, 17), 10, Digest[1..]),
            () => UpdateOffer.Create(new Version(5, 3, 1_000_000_000), 10, Digest)
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
            Check(progress.Values.First() == new UpdateDownloadProgress(0, bytes.Length) && progress.Values.Last() == new UpdateDownloadProgress(bytes.Length, bytes.Length),
                "Progress includes the start and the completed length, measured against the offer's size");
        }
        Check(!Directory.Exists(Path.GetDirectoryName(savedPath)), "Unlaunched download removed when a save/close is cancelled");

        var unstated = new RecordedProgress();
        using (var client = Client(offer, new TestContent(new NonSeekableStream(bytes))))
        using (UpdateInstaller installer = await UpdateInstaller.DownloadAsync(client, offer, unstated, CancellationToken.None, NoCheck))
            Check(File.ReadAllBytes(installer.FilePath).SequenceEqual(bytes) && unstated.Values.First().TotalBytes == bytes.Length,
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

    // ---- The check of the file ----

    private static string Dotnet => Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", "dotnet.exe"));
    private static string Installed => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "StatsDirect", "StatsDirect.exe");

    private static Version FileVersion(string path)
    {
        FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
        return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart);
    }

    // The updater's check itself, on real files: one with no signature, one validly signed by another publisher and, where
    // this machine has an installed StatsDirect signed by StatsDirect Limited, one of ours.
    private static void RunInstallerChecks()
    {
        string own = Environment.ProcessPath;
        Check(UpdateInstallerCheck.Signer(own) == null && Refused(own, new Version(0, 0, 0), null, "not signed by StatsDirect Limited"), "A file with no signature is refused");
        if (File.Exists(Dotnet) && UpdateInstallerCheck.Signer(Dotnet) is string other)
            Check(other != UpdateInstallerCheck.Publisher && Refused(Dotnet, new Version(0, 0, 0), null, "not signed by StatsDirect Limited"), $"A file validly signed by another publisher ({other}) is refused");
        else
            Console.WriteLine("SKIP: no validly signed dotnet.exe beside the runtime to stand for another publisher");
        if (File.Exists(Installed) && UpdateInstallerCheck.Signer(Installed) == UpdateInstallerCheck.Publisher)
        {
            Version version = FileVersion(Installed);
            UpdateInstallerCheck.Verify(Installed, new Version(0, 0, 0));
            UpdateInstallerCheck.Verify(Installed, new Version(0, 0, 0), version);
            UpdateInstallerCheck.Verify(Installed, new Version(0, 0, 0), new Version(version.Major, version.Minor, version.Build, 7));
            passed++;
            Console.WriteLine("PASS: A StatsDirect file signed by StatsDirect Limited, newer than the version running and of the version offered, is accepted");
            Check(Refused(Installed, new Version(999, 0, 0), null, "not a newer version of StatsDirect") && Refused(Installed, version, version, "not a newer version of StatsDirect"),
                "A signed StatsDirect file that is not newer than the version running is refused");
            Check(Refused(Installed, new Version(0, 0, 0), new Version(version.Major, version.Minor, version.Build + 1), "not the version offered")
                    && Refused(Installed, new Version(0, 0, 0), new Version(version.Major, version.Minor, version.Build > 0 ? version.Build - 1 : version.Build + 2), "not the version offered"),
                "A signed StatsDirect file that is newer, but not the version offered, is refused");
        }
        else
            Console.WriteLine("SKIP: no installed StatsDirect signed by StatsDirect Limited on this machine; a release is checked with --verify");
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

        if (File.Exists(Installed) && UpdateInstallerCheck.Signer(Installed) == UpdateInstallerCheck.Publisher && FileVersion(Installed) > UpdateInstallerCheck.RunningVersion)
        {
            byte[] bytes = File.ReadAllBytes(Installed);
            Version version = FileVersion(Installed);
            UpdateOffer offer = Offer(bytes, version.ToString(3));
            using (var client = Client(offer, new ByteArrayContent(bytes)))
            using (UpdateInstaller installer = await UpdateInstaller.DownloadAsync(client, offer, null, CancellationToken.None))
                Check(File.ReadAllBytes(installer.FilePath).SequenceEqual(bytes),
                    $"A download that is the file published, signed by StatsDirect Limited and StatsDirect {version} as offered, passes the updater's own check");
            UpdateOffer another = Offer(bytes, new Version(version.Major, version.Minor, version.Build + 1).ToString(3));
            await Reject("A download signed by StatsDirect Limited that is not the version offered", Client(another, new ByteArrayContent(bytes)), another, "not the version offered", verify: null);
        }
        else
            Console.WriteLine("SKIP: no installed StatsDirect signed by StatsDirect Limited on this machine to serve as a download of ours");
    }

    private static bool Refused(string path, Version running, Version offered, string reason)
    {
        try { UpdateInstallerCheck.Verify(path, running, offered); return false; }
        catch (IOException ex) { return ex.Message.Contains(reason); }
    }

    // The download is refused, for the reason given (or, with none given, because it was cancelled), and nothing of it is kept.
    // The updater's own check is made unless another is given, so a file that came through the transfer when it should not have
    // is still refused, for another reason than the one expected, and the check fails.
    private static async Task Reject(string name, HttpClient client, UpdateOffer offer, string because, CancellationToken token = default, Action<string> verify = null)
    {
        var before = Directory.GetDirectories(Path.GetTempPath(), "StatsDirect-update-*").ToHashSet();
        bool rejected = false;
        string reason = null;
        using (client)
        {
            try { using UpdateInstaller unexpected = await UpdateInstaller.DownloadAsync(client, offer, null, token, verify); }
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
        private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> answers = new(StringComparer.Ordinal);
        internal readonly List<Uri> Requested = [];

        internal Routes On(Uri address, Func<HttpRequestMessage, HttpResponseMessage> answer)
        {
            answers[address.AbsoluteUri] = answer;
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requested.Add(request.RequestUri);
            if (!answers.TryGetValue(request.RequestUri.AbsoluteUri, out var answer))
                throw new Exception("FAIL: an address that was not expected was asked for: " + request.RequestUri);
            HttpResponseMessage response = answer(request);
            response.RequestMessage = request;
            return Task.FromResult(response);
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
