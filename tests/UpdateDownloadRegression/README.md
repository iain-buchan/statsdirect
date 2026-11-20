# Update download regression checks

Run on Windows with the .NET 10 SDK:

```
dotnet run --project tests/UpdateDownloadRegression -c Release
```

Links the production downloader and progress dialog, with no commercial UI dependencies. Uses simulated HTTP responses to check complete and unknown-length downloads, Internet-zone metadata, progress, cancellation, broken transfers, invalid content, and cleanup. Two brief dialog checks exercise completion and closing during a download. It never runs an installer or changes the installed StatsDirect application.

The updater starts nothing that is not ours: the completed file must carry a valid signature made by StatsDirect Limited and be a newer StatsDirect than the one running (`UpdateInstallerCheck`). The transfer checks use made-up bytes and pass a stand-in for that check; one of them uses the real check and expects the unsigned bytes to be refused. The check itself is then run on real files: this program's own unsigned executable, `dotnet.exe` as a file validly signed by another publisher and, where the machine has an installed StatsDirect signed by StatsDirect Limited, that file (accepted against an older running version, refused against a newer one).

`--verify <installer> [--running <version>]` runs the updater's check on an installer file and starts nothing. Use it on a release's signed `StatsDirectSetup.exe` with the previous version, to prove that installed copies will accept the update.

`--preview` shows the progress dialog with simulated progress until Cancel or the close button is pressed.

`--download-only` fetches the real HTTPS installer using the production download code, puts it through the updater's own check, then removes the test download without executing it.

For release acceptance, also use a disposable Windows VM to check the complete signed installer handoff: accept/cancel unsaved-work prompts, normal Windows security prompts, installer visibility, and successful upgrade. The automated tests do not install software.
