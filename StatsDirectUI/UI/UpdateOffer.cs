using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace StatsDirect.UI
{
    /// <summary>The update manifest could not be read, or does not describe an installer of StatsDirect.</summary>
    internal sealed class UpdateManifestException : IOException
    {
        internal UpdateManifestException(string message, Exception inner = null) : base(message, inner) { }
    }

    /// <summary>
    /// What a check for a newer StatsDirect found: whether the check could be made, the update to offer (null when there is none
    /// to offer), what to tell the user, and for the diagnostic log why a check could not be made.
    /// </summary>
    internal readonly record struct UpdateCheckOutcome(bool Succeeded, UpdateOffer Offer, string Message, string Detail = null);

    /// <summary>
    /// A release of StatsDirect offered as an update: its version, the address of its installer, and the installer's size and
    /// SHA-256.  The four are published together in a small file on statsdirect.com, the update manifest, which is written at
    /// each release from the signed installer.  They stay together from the check to the completed download, so that the
    /// version announced is the file that is started.
    /// <para>The installer is held with its release at GitHub.  Only that address, for that version, is accepted: the manifest
    /// cannot send the download anywhere else.</para>
    /// </summary>
    internal sealed record UpdateOffer
    {
        internal static readonly Uri ManifestUri = new("https://www.statsdirect.com/download/windows-latest.json");
        /// <summary>Where a user is sent to download the installer by hand.</summary>
        internal const string DownloadPage = "www.statsdirect.com/download.aspx";
        internal const string InstallerName = "StatsDirectSetup.exe";
        /// <summary>
        /// The form of the manifest that this version reads.  Fields may be added to it without a new number: a reader takes no
        /// notice of a name it does not know.  A change that an older reader would misread needs a new number, and a new address
        /// for the file, so that the versions that read this form still find it at <see cref="ManifestUri"/>.
        /// </summary>
        internal const int Schema = 1;
        internal const int MaximumManifestBytes = 16 * 1024;
        internal const long MaximumInstallerBytes = 2L * 1024 * 1024 * 1024;

        private const string ReleaseDownloads = "https://github.com/iain-buchan/statsdirect/releases/download/";
        private const int MaximumVersionPart = 999_999_999;
        private static readonly Regex VersionForm = new(@"\A(0|[1-9][0-9]{0,8})\.(0|[1-9][0-9]{0,8})\.(0|[1-9][0-9]{0,8})\z", RegexOptions.CultureInvariant);
        private static readonly Regex WholeNumber = new(@"\A(0|[1-9][0-9]{0,17})\z", RegexOptions.CultureInvariant);
        private static readonly Regex Sha256Form = new(@"\A[0-9A-Fa-f]{64}\z", RegexOptions.CultureInvariant);
        private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        private UpdateOffer(Version version, long size, string sha256)
        {
            Version = version;
            Size = size;
            Sha256 = sha256;
        }

        /// <summary>The version offered: major, minor and build.</summary>
        internal Version Version { get; }
        /// <summary>The installer's length in bytes.</summary>
        internal long Size { get; }
        /// <summary>The installer's SHA-256, as 64 lower-case hexadecimal digits.</summary>
        internal string Sha256 { get; }
        /// <summary>The installer's address: the file of this version's release at GitHub.</summary>
        internal Uri Url => new(InstallerAddress(Version));

        public override string ToString() =>
            $"StatsDirect {Version}, {Size.ToString("N0", CultureInfo.InvariantCulture)} bytes, SHA-256 {Sha256}, at {InstallerAddress(Version)}";

        /// <summary>The one address at which the installer of a version is accepted.</summary>
        internal static string InstallerAddress(Version version) => ReleaseDownloads + "v" + version.ToString(3) + "/" + InstallerName;

        /// <summary>Whether this is an update for a program of the given version.</summary>
        internal bool IsNewerThan(Version running) => Version > ThreeParts(running);

        /// <summary>An offer of the installer of a version, as the release makes it; throws ArgumentException when a value cannot be published.</summary>
        internal static UpdateOffer Create(Version version, long size, string sha256)
        {
            ArgumentNullException.ThrowIfNull(version);
            ArgumentNullException.ThrowIfNull(sha256);
            Version three = ThreeParts(version);
            if (three.Major > MaximumVersionPart || three.Minor > MaximumVersionPart || three.Build > MaximumVersionPart)
                throw new ArgumentException("The version has a part too large to publish.", nameof(version));
            if (size <= 0 || size > MaximumInstallerBytes)
                throw new ArgumentException("The size is not that of an installer.", nameof(size));
            if (!Sha256Form.IsMatch(sha256))
                throw new ArgumentException("The SHA-256 is not 64 hexadecimal digits.", nameof(sha256));
            return new UpdateOffer(three, size, sha256.ToLowerInvariant());
        }

        /// <summary>The manifest that publishes this offer, as the release writes it and <see cref="Parse"/> reads it.</summary>
        internal string ToManifest() =>
            "{\n" +
            "  \"schema\": " + Schema.ToString(CultureInfo.InvariantCulture) + ",\n" +
            "  \"version\": \"" + Version.ToString(3) + "\",\n" +
            "  \"url\": \"" + InstallerAddress(Version) + "\",\n" +
            "  \"size\": " + Size.ToString(CultureInfo.InvariantCulture) + ",\n" +
            "  \"sha256\": \"" + Sha256 + "\"\n" +
            "}\n";

        /// <summary>
        /// Reads a manifest.  Everything in it must be as a release writes it: a version of three numbers, the address that
        /// belongs to that version, a size an installer can have and a SHA-256.  Anything else throws
        /// <see cref="UpdateManifestException"/>: a manifest that is not understood offers nothing.
        /// </summary>
        internal static UpdateOffer Parse(string manifest)
        {
            if (manifest == null)
                throw new UpdateManifestException("The update manifest is missing.");
            if (manifest.Length > 0 && manifest[0] == '﻿')
                manifest = manifest[1..];
            JsonDocument document;
            try
            {
                // Comments and trailing commas are refused, as is anything after the object.
                document = JsonDocument.Parse(manifest, new JsonDocumentOptions { MaxDepth = 8 });
            }
            catch (JsonException ex)
            {
                throw new UpdateManifestException("The update manifest is not JSON.", ex);
            }
            using (document)
            {
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    throw new UpdateManifestException("The update manifest is not a JSON object.");
                JsonElement? schema = null, version = null, url = null, size = null, sha256 = null;
                foreach (JsonProperty property in root.EnumerateObject())
                {
                    switch (property.Name)
                    {
                        case "schema": Take(ref schema, property); break;
                        case "version": Take(ref version, property); break;
                        case "url": Take(ref url, property); break;
                        case "size": Take(ref size, property); break;
                        case "sha256": Take(ref sha256, property); break;
                    }
                }

                // The form first: a later form may hold the rest differently.
                if (schema is not { ValueKind: JsonValueKind.Number } schemaNumber || !WholeNumber.IsMatch(schemaNumber.GetRawText())
                    || !int.TryParse(schemaNumber.GetRawText(), NumberStyles.None, CultureInfo.InvariantCulture, out int form))
                    throw new UpdateManifestException("The update manifest does not say which form it is in (\"schema\").");
                if (form != Schema)
                    throw new UpdateManifestException(form > Schema
                        ? $"The update manifest is in a later form ({form}) than this version of StatsDirect reads ({Schema})."
                        : $"The update manifest is in a form ({form}) that StatsDirect does not read.");

                if (version is not { ValueKind: JsonValueKind.String } versionText || VersionForm.Match(versionText.GetString()) is not { Success: true } parts)
                    throw new UpdateManifestException("The update manifest does not give a version of three numbers (\"version\").");
                var offered = new Version(int.Parse(parts.Groups[1].Value, CultureInfo.InvariantCulture),
                    int.Parse(parts.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(parts.Groups[3].Value, CultureInfo.InvariantCulture));

                // The address is compared as text, character for character: no other spelling of it is accepted.
                if (url is not { ValueKind: JsonValueKind.String } address || !string.Equals(address.GetString(), InstallerAddress(offered), StringComparison.Ordinal))
                    throw new UpdateManifestException($"The update manifest does not give the address of StatsDirect {offered}'s installer (\"url\").");

                if (size is not { ValueKind: JsonValueKind.Number } sizeNumber || !WholeNumber.IsMatch(sizeNumber.GetRawText())
                    || !long.TryParse(sizeNumber.GetRawText(), NumberStyles.None, CultureInfo.InvariantCulture, out long bytes)
                    || bytes <= 0 || bytes > MaximumInstallerBytes)
                    throw new UpdateManifestException("The update manifest does not give a size that an installer can have (\"size\").");

                if (sha256 is not { ValueKind: JsonValueKind.String } digest || !Sha256Form.IsMatch(digest.GetString()))
                    throw new UpdateManifestException("The update manifest does not give a SHA-256 of 64 hexadecimal digits (\"sha256\").");

                return new UpdateOffer(offered, bytes, digest.GetString().ToLowerInvariant());
            }
        }

        // A name given twice is refused: readers of JSON differ on which of the two counts.
        private static void Take(ref JsonElement? field, JsonProperty property)
        {
            if (field.HasValue)
                throw new UpdateManifestException($"The update manifest gives \"{property.Name}\" twice.");
            field = property.Value;
        }

        /// <summary>
        /// Fetches a manifest's text: over https to the end, no longer than a manifest can be, and UTF-8.  An answer that is
        /// not a success throws HttpRequestException with its status.
        /// </summary>
        internal static async Task<string> ReadManifestAsync(HttpClient client, Uri address, CancellationToken cancellationToken)
        {
            using HttpResponseMessage response = await client.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            // A client that follows redirects reports the address it ended on.
            if (response.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps)
                throw new UpdateManifestException("The update manifest did not come over a secure connection.");
            const string tooLong = "The update manifest is longer than a manifest can be.";
            if (response.Content.Headers.ContentLength > MaximumManifestBytes)
                throw new UpdateManifestException(tooLong);
            byte[] buffer = new byte[MaximumManifestBytes + 1];
            int length = 0;
            await using (Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            {
                int count;
                while (length < buffer.Length
                    && (count = await source.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false)) != 0)
                    length += count;
            }
            if (length > MaximumManifestBytes)
                throw new UpdateManifestException(tooLong);
            try
            {
                return StrictUtf8.GetString(buffer, 0, length);
            }
            catch (DecoderFallbackException ex)
            {
                throw new UpdateManifestException("The update manifest is not UTF-8 text.", ex);
            }
        }

        /// <summary>Fetches and reads a manifest: what the check for a new version does.</summary>
        internal static async Task<UpdateOffer> FetchAsync(HttpClient client, Uri address, CancellationToken cancellationToken) =>
            Parse(await ReadManifestAsync(client, address, cancellationToken).ConfigureAwait(false));

        /// <summary>
        /// What the check makes of what it fetched: the manifest's text, or the error that came in its place, for a program of the
        /// version given.  An update is offered only by a manifest that is understood in full and names a newer version.  When the
        /// check cannot be made the user is sent to the download page, and StatsDirect offers nothing.
        /// </summary>
        internal static UpdateCheckOutcome Evaluate(string manifest, Exception error, Version running)
        {
            const string unreadable = "Could not read the StatsDirect update information. Please check manually at " + DownloadPage;
            // The site answered, but not with a manifest: telling the user to check the connection would mislead.
            if (error is UpdateManifestException || error is HttpRequestException { StatusCode: not null })
                return new UpdateCheckOutcome(false, null, unreadable, error.Message);
            if (error != null || manifest == null)
                return new UpdateCheckOutcome(false, null, "Could not check for StatsDirect updates. Please check your Internet connection, or look at " + DownloadPage, error?.Message);
            UpdateOffer published;
            try
            {
                published = Parse(manifest);
            }
            catch (UpdateManifestException ex)
            {
                return new UpdateCheckOutcome(false, null, unreadable, ex.Message);
            }
            Version installed = ThreeParts(running);
            return published.IsNewerThan(installed)
                ? new UpdateCheckOutcome(true, published, $"You are presently running StatsDirect version {installed}. Version {published.Version} is available. Would you like to close StatsDirect and install the new version?")
                : new UpdateCheckOutcome(true, null, $"You are presently running StatsDirect version {installed}. You have the latest version of StatsDirect.");
        }

        /// <summary>A version as the updater compares it: major, minor and build, with an absent part as zero.</summary>
        internal static Version ThreeParts(Version version) =>
            new(Math.Max(version.Major, 0), Math.Max(version.Minor, 0), Math.Max(version.Build, 0));
    }
}
