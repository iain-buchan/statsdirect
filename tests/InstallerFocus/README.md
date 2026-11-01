# Installer focus checks

These checks exercise the native helper used by the stock WiX bootstrapper after
the user approves elevation. They do not install StatsDirect, request elevation,
open another application's windows or change the installed application.

The linked-source cases replace the Windows window-state, message, thread and
foreground functions with deterministic test functions. Successful elevation
must queue one private message without activating from the callback thread.
The published BAFunctions WndProc callback must consume it once on the window's
owning UI thread. Duplicate, unsolicited, unrelated, malformed and wrong-thread
messages must not cause activation or swallow the legitimate queued handover.
The checks cover eligibility both before posting and at dispatch, cancelled or
failed elevation, pending-request invalidation, registration/posting failures,
Windows declining focus, logging failure, silent modes and context lifetime.

The visibility fallback has separate checks for a declined foreground request
and an accepted request whose foreground window is still different. Its one
TOPMOST/NOTOPMOST sequence must run on the UI thread with the exact flags that
preserve position, size, keyboard focus and owner order. Tests cover immediate
cleanup, one bounded cleanup retry, native failures, destruction, eligibility
changes and a pre-existing topmost window. A failed cleanup is logged accurately;
the checks do not assume Windows grants either the raise or the demotion.

The checks also load the actual DLL and exercise its exported creation,
callback and destruction ABI, including the published x64 WndProc structures,
using invalid window handles so no desktop focus changes are made. The loader's
BAFunctions version is checked independently of the production constant; WiX 7
still sends elevation-complete events with the version-5 contract.

Run the script with the same WiX 7.0.1 native headers and compiler used to build
the helper. Its command-line parameters are documented at the top of `run.ps1`.
Build products are written below `bin` unless an output directory is supplied.

This verifies the callback and policy, not Windows' UAC desktop transition or
permission to bring the installer forward. Posting successfully is not evidence
that foreground activation will succeed; both accepted and declined outcomes
are tested and logged separately.
Final acceptance uses the signed bundle in a disposable Windows VM: launch from
Explorer, click Install, approve UAC and check the progress window is in front;
repeat for upgrade and for an in-application update. Cancelling UAC must leave
the ordinary installer error/cancellation flow intact. A minimized installer
must not be restored, and switching to another application after the one-time
handover must not cause repeated foreground activation.
