# Prototype validation, 31 October 2026

Windows standalone prototype, based on `a7836b49`, using installed 64-bit R 4.6.1 and WebView2. No changes to the released application, version, engine, release tags or installer projects.

- **34 native helper checks passed.** Genuine interactive console input, Unicode, source/input separation, stale input replies, Stop during `readline`, menus, incomplete input, sleep and loops, retained variables, error recovery, native/child output isolation, input byte limits and shutdown while awaiting an answer.
- **24 adapter integration checks passed.** Includes `on.exit` cleanup during interruption, validation before state changes, Restart during a prompt without answering it, public request IDs remaining unique across Restart, two independent sessions, source and Console sharing variables, no repeated chart after non-graphical commands, SVG follow-on edits, immutable earlier image assets and two plots in one command.
- **8 shared UI model checks passed.** Exact shared Web assets from Mac prototype `25d03419e9c02ff6ddb4bd19f45775f5481fbfbf`; only the Node test import path differs for the Windows directory layout.
- **16 actual WebView2 UI checks passed.** Real Run/Stop/Restart controls and input fields exercise the native R session. Includes prompt answers excluded from executable history, rejected-answer retry, invalid-source recovery, multiline input, history recall/draft restoration, Script retention after Restart, escaped output, visible SVG revisions and resolution of the original earlier plot asset.

Two initial apparent plot failures were test assertions that did not allow spaces in SVG `rgb(100%, 0%, 0%)` color notation. The SVGs contained the expected red line; assertions were corrected and the complete suite rerun.

Real issues found and fixed during validation: native GraphApp export lookup, stale request-ID collision after Restart, rejected input leaving the console busy, late-generation lifecycle updates, repeated unchanged plot previews, WebView2 event delivery, numeric/string request-ID mismatch, loss of wrapped-command history navigation and revision-ambiguous chart actions. The shared interface fixes were coordinated with the Mac prototype.

Stop is cooperative: this evidence does not claim interruption of arbitrary native code that never checks R interrupts. Restart explicitly replaces the process and loses its workspace. Completed assignments and file writes are retained after Stop. Native interactive graphics such as `locator()` and arbitrary terminal programs are outside this prototype's supported interface.

The native and integration test programs retain their actual JSON/event evidence in their designated output folders. `--ui-smoke` writes a JSON result and PNG screenshot. A separate prototype executable is produced; no installer is built or overwritten.
