# First-use ChatGPT runtime checks

Builds the production downloader and setup dialog directly, without SpreadsheetGear
or an account. Tests use a unique temporary installation root and never change the
user's real runtime or credentials. The production application integration is also
covered by `tests/Learning` (missing runtime does not download on account refresh).

Prepare the optional official archive fixture once, then run:

```powershell
./Learning/prepare-runtime.ps1
# Optional: also exercise the real GitHub HTTPS redirect, download and installation.
$env:STATSDIRECT_TEST_RUNTIME_LIVE = '1'
# Optional: capture the native progress dialog at the current display DPI.
$env:STATSDIRECT_RUNTIME_ARTIFACTS = 'C:\path\to\artifacts'
dotnet run --project tests/TutorRuntime -c Release
```

Covers wrong/truncated downloads, untrusted redirects, hash tampering, archive
traversal/symlinks/duplicates/wrong lengths, cancellation during download and
extraction, cancellation while another process owns the lock, staged cleanup,
official publisher verification, installed file hashes, offline reuse, corrupt
installation repair, account-data preservation and real WinForms progress,
cancel/retry/error/close/modal completion and high-DPI button visibility.

No model calls, account sign-in, installer build or release is performed.
