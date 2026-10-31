# Isolated native R console helper

Prototype only. This executable loads the selected installation's `R.dll`; the
StatsDirect process does not load R. Its C entry point includes the production
`RConsoleWorker/console.c`, so the archived prototype shares the corrected evaluator.
Build with `build-native.ps1`. The default
output is `bin/StatsDirect.RConsole.Native.exe`. The headers and selected R
runtime must use the same major/minor series, currently R 4.6.x. No R source is incorporated.

Run with `--r-home <R installation> --working-directory <session directory>`.
If Visual Studio has no C++ workload, run `fetch-native-tools.ps1` first to fetch
the pinned, SHA256-verified portable Zig compiler from ziglang.org into ignored
`tools/`. This does not install software or modify the global PATH. Either build
uses the Windows Universal C Runtime (UCRT).

Startup disables profiles, environment files, workspace restoration and saving.
The UTF-8 Windows manifest and a 16 MiB main stack are part of the build.
R's `bin/x64` directory is prefixed to this helper's process-local PATH before
loading R so default packages and their BLAS/LAPACK dependencies load normally.
The parent process and system PATH are unchanged. Submitted source and
continuations normalize CRLF/lone CR to LF within the helper; editor text remains
unchanged.

## Native transport, version 1

The host redirects stdin/stdout/stderr. On startup the helper privately duplicates
stdin for control and stdout for events. It redirects **both** CRT stdin and the
OS standard-input handle to NUL; ordinary `stdin()` or child-process input cannot
consume protocol bytes. It redirects both user stdout handles to stderr, keeping
native-package `printf` and child output separate from the event stream. The host
must drain stderr continuously as literal output. Raw native output ordering
relative to JSON console events is best effort.

Input frames use a four-byte unsigned little-endian payload length, followed by
one payload. Maximum payload: 16 MiB. Text must be valid UTF-8, without NUL bytes.

| Opcode | Remaining payload |
| --- | --- |
| 1, submit | `uint32 commandId`, then UTF-8 source |
| 2, reply | `uint32 requestId`, then UTF-8 answer or continuation |
| 3, interrupt | none |
| 4, shutdown | none |

All uint32 IDs are permitted, including zero. Only one submission may be active.
A submission is accepted after a `top` prompt; input/continuation require `reply`
with the current request ID. Source is consumed only by the top-level evaluator.
An internal `readline()` cannot consume the remaining lines of a submitted script.

Output is newline-delimited JSON, with a `type` discriminator:

- `ready`: `protocol`, `pid`, `rVersion`; emitted before the first prompt.
- `prompt`: `kind` (`top`, `continuation`, `input`), `text`, `requestId`, `commandId`.
  An `input` prompt also supplies `maxBytes`, the maximum UTF-8 reply byte count
  after reserving space for a newline and NUL. Reject larger answers in the host
  before changing pending-input state. A native rejection preserves the request.
- `output`: `stream` (`stdout`, `stderr`), `text`, `commandId`.
- `busy`: `value` boolean, `commandId`.
- `completed`: `commandId`, `status` (`ok`, `error`, `interrupted`).
- `error`: transport/host error `message` (R diagnostics use `output`).
- `stopped`: graceful termination.

`completed` precedes the following `top` prompt, after all queued source was
consumed. An incomplete expression retains its command ID until a continuation
reply completes it or Stop cancels it. Prompt kind is derived from the R evaluator
and console callback state, not by matching visible prompt text.

## Interruption and limits

The transport reader only enqueues messages and sets flags/events. **Every R API
call runs on the main thread.** The main-thread console callback wakes regularly
and processes interruption through R's ordinary unwind, including while awaiting
a `readline` or menu answer. Stop never supplies blank input or EOF. A per-iteration
R top-level context catches errors/interrupts and resets the parser, preserving
the process and variables already assigned. This is not transaction rollback.

Native packages that never check R interrupts can still block. The host's explicit
Restart may terminate the process in that case. Native/child OS stdin readers see
NUL/EOF rather than the console callback; they are not interactive. R's console
connection, including `readLines(stdin())`, routes through `ReadConsole` and is
interactive without exposing the control stream.

The prototype does not assign R to a Windows Job Object. Arbitrary background
processes started by user code may outlive a graceful session shutdown; the host
must manage descendants explicitly before treating this as production behavior.

The host configures graphics and snapshots through internal submissions. The
helper does not close graphics devices after each command, so follow-on commands
such as `abline()` can modify an existing plot.
