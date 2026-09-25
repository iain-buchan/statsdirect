# Update download regression checks

Run on Windows with the .NET 10 SDK:

```
dotnet run --project tests/UpdateDownloadRegression -c Release
```

Links the production downloader and progress dialog, with no commercial UI dependencies. Uses simulated HTTP responses to check complete and unknown-length downloads, Internet-zone metadata, progress, cancellation, broken transfers, invalid content, and cleanup. Two brief dialog checks exercise completion and closing during a download. It never runs an installer or changes the installed StatsDirect application.

`--preview` shows the progress dialog with simulated progress until Cancel or the close button is pressed.

`--download-only` fetches the real HTTPS installer using the production download code, then removes the test download without executing it.

For release acceptance, also use a disposable Windows VM to check the complete signed installer handoff: accept/cancel unsaved-work prompts, normal Windows security prompts, installer visibility, and successful upgrade. The automated tests do not install software.
