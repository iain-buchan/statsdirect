# Installer focus checks

These checks exercise the native helper used by the stock WiX bootstrapper after
the user approves elevation. They do not install StatsDirect, request elevation,
open another application's windows or change the installed application.

The linked-source cases replace only the four Windows window-state functions
with deterministic test functions. They send the real WiX callbacks through the
production helper: successful elevation may request foreground once for a valid,
visible, non-minimized installer; cancelled/failed elevation, hidden/minimized or
destroyed windows and unrelated callbacks must not. The checks also load the
actual compiled DLL and exercise its exported creation/callback/destruction ABI
with invalid window handles, so no desktop focus changes are made.

Run the script with the same WiX 7.0.1 native headers and compiler used to build
the helper. Its command-line parameters are documented at the top of `run.ps1`.
Build products are written below `bin` unless an output directory is supplied.

This verifies the callback and policy, not Windows' UAC desktop transition.
Final acceptance uses the signed bundle in a disposable Windows VM: launch from
Explorer, click Install, approve UAC and check the progress window is in front;
repeat for upgrade and for an in-application update. Cancelling UAC must leave
the ordinary installer error/cancellation flow intact. A minimized installer
must not be restored, and switching to another application after the one-time
handover must not cause repeated foreground activation.
