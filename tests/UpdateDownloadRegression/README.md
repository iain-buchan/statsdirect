# Update download regression checks

Run on Windows with the .NET 10 SDK:

```
dotnet run --project tests/UpdateDownloadRegression -c Release
```

Links the production updater: the manifest reader (`UpdateOffer`), the downloader (`UpdateInstaller`), the installer check (`UpdateInstallerCheck`) and the progress dialog, with no commercial UI dependencies. It never runs an installer or changes the installed StatsDirect application.

What the updater does, and what is checked:

- **The manifest.** The check for a new version reads `https://www.statsdirect.com/download/windows-latest.json`: the version published, the address of its installer, and the installer's size and SHA-256. The checks read the manifest a release writes, and refuse every other form: a missing or mistyped field, another address than the installer of that version's GitHub release, a later schema, a field given twice, text that is not JSON, an answer that is too long, not UTF-8, not a success, or that ended on an insecure address. They also check what the user is told, and that an update is offered only to an older version.
- **The transfer.** Simulated HTTP responses check complete and unknown-length downloads, Internet-zone metadata, progress, cancellation, broken transfers and cleanup. The file kept must be the one offered: a stated or actual length that is not the offer's, and a file of the right length with another SHA-256, are refused. Redirects are followed one at a time, and only to https addresses.
- **The mirror.** A manifest may name a second address for the same file, on `www.statsdirect.com` only. The checks take each way the release's address can fail (no connection, a refusal, a page in the file's place, another SHA-256, a transfer cut short or broken off, an insecure redirect, no answer, a stall) and expect the file from the mirror, checked as before. They also expect the mirror not to be asked while the release's address serves the file, after a cancellation, or for a file that arrived whole and failed the check of its signature.
- **The time each address has.** The release's address has its own time when a mirror follows it, so that it cannot use up the mirror's. The checks take an address that sends a byte at a time, one that is still sending when its time ends, and a chain of redirects that takes too long, and expect the mirror to give the file; an address that is slow but on course is left to finish. The last address has an end too. A cancellation while either address is sending, or between the two, stops the download as cancelled and keeps nothing.
- **Where the file comes from.** The release's two checks are checked themselves: the check of the release's address fails on a file missing or wrong at GitHub though the mirror is in good order, and passes through GitHub's own redirect; the check of the mirror refuses every request to GitHub, fails without the mirror named in the manifest, and fails if the mirror sends the request on to GitHub. An offer still finds its installer at its versioned mirror after the website's unversioned download has moved on to the next release.
- **The file.** The updater starts nothing that is not ours: the completed file must carry a valid signature made by StatsDirect Limited, be StatsDirect's setup program, be newer than the version running, and be the version offered (`UpdateInstallerCheck`). The transfer checks use made-up bytes and pass a stand-in for that check; some use the real check and expect the unsigned bytes to be refused. The check itself is then run on real files:
  - this program's own unsigned executable, refused;
  - `dotnet.exe`, validly signed by another publisher, refused;
  - where the machine has an installed StatsDirect, its own signed file, refused because it is not the setup program, and no manifest is written for it;
  - where the machine has a published setup program (the file named by `STATSDIRECT_SIGNED_SETUP`, or `C:\temp\StatsDirectSetup.exe`), that file, accepted.

  These files are also served as downloads, to take the whole route with the updater's own check: the published setup program from the release's address and from its versioned mirror with GitHub blocked. Where a file is absent the checks that need it say SKIP.
- **The dialog.** Two brief dialog checks exercise completion and closing during a download.

Release tools (none of them starts an installer):

- `--verify <installer> [--running <version>]` runs the updater's check on an installer file. Use it on a release's signed `StatsDirectSetup.exe` with the previous version, to prove that installed copies will accept the update.
- `--manifest <installer> [--out <file>] [--versioned-mirror | --mirror <address>]` writes the update manifest of a signed installer, with the updater's own code, and reads it back. The installer must be one the updater would start: the setup program, not another signed file. `--versioned-mirror` names the mirror a release publishes, the installer in the website's folder for its version; `--mirror` names another address on the website.
- `--check-manifest <file or https address> [--running <version>] [--download [--require-github | --block-github]]` reads a manifest as the updater does and says whether that version would be offered it. With `--download` it also fetches the installer with the production downloader and checks it as the updater of that version would: size, SHA-256, signature, setup program and version.
  - `--require-github` takes the file from the release's address and no other. It is the release's check of GitHub: a mirror in good order cannot make it pass.
  - `--block-github` refuses every request to GitHub, as on a network that blocks it, so the file has to come from the manifest's mirror. It is the release's check of the mirror.
  - With neither, the download is made as the updater makes it, and may come from either address.
- `--download-only` does the same from the live manifest address, with this program standing for the installed StatsDirect.
- `--preview` shows the progress dialog with simulated progress until Cancel or the close button is pressed.

`BUILDING.md` ("Updates: the manifest and the installer's address") has the manifest's form and the release steps.

For release acceptance, also use a disposable Windows VM to check the complete signed installer handoff: accept/cancel unsaved-work prompts, normal Windows security prompts, installer visibility, and successful upgrade. The automated tests do not install software.
