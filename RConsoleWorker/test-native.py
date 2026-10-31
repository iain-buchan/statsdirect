"""Focused real-R transport and interrupt tests; never loads R into Python."""
import json
import os
import pathlib
import queue
import re
import struct
import subprocess
import sys
import tempfile
import threading
import time

ROOT = pathlib.Path(__file__).resolve().parent
RHOME = sys.argv[1] if len(sys.argv) > 1 else r"C:\Program Files\R\R-4.6.1"
REQUIRE_PROTECTION_CHECKS = "--require-protection-checks" in sys.argv[2:]
helper_args = [arg for arg in sys.argv[2:] if arg != "--require-protection-checks"]
if len(helper_args) > 1:
    raise SystemExit("Usage: test-native.py [R-home] [native-helper] [--require-protection-checks]")
HELPER = pathlib.Path(helper_args[0]).resolve() if helper_args else ROOT / "bin" / "StatsDirect.RConsole.Native.exe"
checks = 0


def check(value, description):
    global checks
    if not value:
        print("Recent native events:", json.dumps(all_events[-20:], ensure_ascii=False), flush=True)
        print("Native stderr:", "".join(raw[-30:]), flush=True)
    assert value, description
    checks += 1
    print("PASS", description, flush=True)


with tempfile.TemporaryDirectory(prefix="native-r-", dir=ROOT / "bin") as working:
    # Match the production adapter: Unix UTF-8 locale names inherited by the
    # test runner are unsupported by Windows R and can invalidate re-encoding.
    child_environment = os.environ.copy()
    child_environment.pop("LC_ALL", None)
    child_environment.pop("LC_CTYPE", None)
    process = subprocess.Popen(
        [str(HELPER), "--r-home", RHOME,
         "--working-directory", working],
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
        creationflags=subprocess.CREATE_NO_WINDOW,
        env=child_environment,
    )
    events = queue.Queue()
    raw = []
    all_events = []

    def read_events():
        try:
            for line in iter(process.stdout.readline, b""):
                event = json.loads(line.decode("utf-8"))
                all_events.append(event)
                events.put(event)
        except Exception as error:
            events.put({"type": "transportFailure", "message": str(error)})
        events.put({"type": "eof"})

    def read_raw():
        for line in iter(process.stderr.readline, b""):
            raw.append(line.decode("utf-8", errors="replace"))

    threading.Thread(target=read_events, daemon=True).start()
    threading.Thread(target=read_raw, daemon=True).start()

    def send(opcode, ident=None, text=""):
        payload = bytes([opcode])
        if ident is not None:
            payload += struct.pack("<I", ident) + text.encode("utf-8")
        process.stdin.write(struct.pack("<I", len(payload)) + payload)
        process.stdin.flush()

    def until(predicate, timeout=10):
        end = time.monotonic() + timeout
        seen = []
        while time.monotonic() < end:
            try:
                event = events.get(timeout=max(0.001, end - time.monotonic()))
            except queue.Empty:
                break
            seen.append(event)
            if event["type"] in ("transportFailure", "eof"):
                raise AssertionError((seen, raw, process.poll()))
            if predicate(event):
                return seen
        raise AssertionError(("timeout", seen, raw, process.poll()))

    def top(timeout=10):
        return until(lambda event: event["type"] == "prompt" and event["kind"] == "top", timeout)

    def output(seen):
        return "".join(event.get("text", "") for event in seen if event["type"] == "output")

    def submit(ident, source):
        send(1, ident, source)
        seen = top()
        completed = [event for event in seen if event["type"] == "completed"]
        check(len(completed) == 1 and completed[0]["commandId"] == ident, "exactly one completed event for command " + str(ident))
        return seen

    def stop_at_prompt(ident, source, kind):
        send(1, ident, source)
        seen = until(lambda event: event["type"] == "prompt" and event["kind"] == kind)
        check(seen[-1]["commandId"] == ident, "prompt retains command identity " + str(ident))
        send(3)
        seen = top()
        check(any(event["type"] == "completed" and event["status"] == "interrupted" for event in seen), "Stop cancels " + kind + " without a reply")

    def parse_eval_regressions():
        # A complete prefix must run before a following incomplete expression
        # asks for continuation, as in R's ordinary interactive console.
        prefix_path = pathlib.Path(working) / "prefix-before-continuation.txt"
        send(1, 100, 'cat("prefix ran\\n", file="prefix-before-continuation.txt", append=TRUE)\nif (TRUE) {')
        request = until(lambda e: e["type"] == "prompt" and e["kind"] == "continuation")[-1]
        check(prefix_path.read_text().strip() == "prefix ran", "a complete batch prefix executes before an incomplete suffix requests continuation")
        send(2, request["requestId"], 'cat("CONTINUED-PREFIX\\n")\n}')
        seen = top()
        check("CONTINUED-PREFIX" in output(seen) and prefix_path.read_text().splitlines() == ["prefix ran"]
              and sum(e["type"] == "completed" and e["commandId"] == 100 and e["status"] == "ok" for e in seen) == 1,
              "continuing the suffix finishes the original batch once without replaying its completed prefix")

        seen = submit(101, 'syntax_prefix <- 17L\nsyntax_bad <- )\nsyntax_following <- TRUE')
        check(any(e["type"] == "completed" and e["status"] == "error" for e in seen), "a syntax error after a valid prefix reports error completion")
        seen = submit(102, 'cat("SYNTAX-PREFIX", syntax_prefix, exists("syntax_following"), "\\n")')
        check("SYNTAX-PREFIX 17 FALSE" in output(seen), "syntax errors preserve completed prefix assignments and discard following statements")

        # Straddle the old 4096-byte callback boundary with multibyte UTF-8,
        # including a four-byte character, on one physical source line. A tab
        # distinguishes physical byte offsets from displayed source columns.
        prefix = '\tlong_utf8 <- "'
        for ident, boundary in enumerate((4094, 4095, 4096), 103):
            value = "x" * (boundary - len(prefix.encode("utf-8"))) + "α😀" + "y" * 200
            source = prefix + value + '"; stopifnot(nchar(long_utf8,type="bytes")==%dL, grepl("α😀",long_utf8,fixed=TRUE)); cat("LONG-UTF8-%d\\n")' % (len(value.encode("utf-8")), boundary)
            seen = submit(ident, source)
            check("LONG-UTF8-%d" % boundary in output(seen) and any(e["type"] == "completed" and e["status"] == "ok" for e in seen),
                  "one long UTF-8 line survives a multibyte character at byte " + str(boundary))

        # Prompt kind is semantic: users may give top/continuation/readline
        # exactly the same visible text.
        submit(106, 'old_prompts <- options(prompt="SAME> ", continue="SAME> ")')
        send(1, 107, 'if (TRUE) {')
        request = until(lambda e: e["type"] == "prompt" and e["kind"] == "continuation")[-1]
        check(request["text"] == "SAME> ", "equal custom top and continuation text still yields a typed continuation prompt")
        send(2, request["requestId"], 'cat("CUSTOM-CONTINUATION\\n")\n}')
        seen = top()
        check(seen[-1]["text"] == "SAME> " and "CUSTOM-CONTINUATION" in output(seen), "the completed custom continuation returns to the typed top prompt")
        send(1, 108, 'same_answer <- readline("SAME> "); cat("SAME-ANSWER", same_answer, "\\n")')
        request = until(lambda e: e["type"] == "prompt" and e["kind"] == "input")[-1]
        check(request["text"] == "SAME> ", "readline remains typed input even when its text equals the top prompt")
        send(2, request["requestId"], "chosen answer")
        check("SAME-ANSWER chosen answer" in output(top()), "the equal-text readline receives only its explicit reply")
        submit(109, 'options(old_prompts)')

        seen = submit(110, 'hidden_assignment <- "ASSIGNMENT-NOT-PRINTED"; invisible("INVISIBLE-NOT-PRINTED"); "VISIBLE-AUTO-PRINT"; cat("LAST-VALUE=", identical(.Last.value, "VISIBLE-AUTO-PRINT"), "\\n", sep=""); cat("LAST-BINDING=", bindingIsLocked(".Last.value", baseenv()) && !exists(".Last.value", envir=.GlobalEnv, inherits=FALSE), "\\n", sep="")')
        check(re.search(r'\[1\]\s+"VISIBLE-AUTO-PRINT"', output(seen)) is not None
              and "INVISIBLE-NOT-PRINTED" not in output(seen) and "ASSIGNMENT-NOT-PRINTED" not in output(seen)
              and "LAST-VALUE=TRUE" in output(seen) and "LAST-BINDING=TRUE" in output(seen),
              "interactive auto-print preserves visibility and the locked base last-value binding without global pollution")

        # Force frequent collection while evaluating closures and returned lists.
        # The existing error/Stop cases separately verify that later statements
        # in a failed batch are discarded and prior workspace values survive.
        seen = submit(111, 'local({on.exit(gctorture(FALSE)); gctorture2(10L); x <- lapply(1:12, function(i) list(i=i, value=as.character(i))); stopifnot(identical(x[[12L]]$i, 12L))}); cat("GC-TORTURE-SAFE\\n")')
        check("GC-TORTURE-SAFE" in output(seen) and any(e["type"] == "completed" and e["status"] == "ok" for e in seen),
              "frequent garbage collection preserves parsed expressions and evaluated objects")

    def console_compatibility_regressions():
        seen = submit(112, 'options(warn=0); {warning("DEFERRED-NATIVE-WARNING"); cat("WARN0-BODY\\n")}')
        text = output(seen)
        check(text.count("DEFERRED-NATIVE-WARNING") == 1 and "WARN0-BODY" in text
              and text.index("WARN0-BODY") < text.index("DEFERRED-NATIVE-WARNING"),
              "warn=0 displays the deferred warning once after its expression finishes")
        seen = submit(113, 'options(warn=1); {warning("IMMEDIATE-NATIVE-WARNING"); cat("WARN1-BODY\\n")}')
        text = output(seen)
        check(text.count("IMMEDIATE-NATIVE-WARNING") == 1 and "WARN1-BODY" in text
              and text.index("IMMEDIATE-NATIVE-WARNING") < text.index("WARN1-BODY"),
              "warn=1 displays its immediate warning once without a duplicate deferred warning")
        seen = submit(114, 'options(warn=-1); warning("SILENT-NATIVE-WARNING"); cat("WARN-SILENT-BODY\\n")')
        check("SILENT-NATIVE-WARNING" not in output(seen) and "WARN-SILENT-BODY" in output(seen),
              "warn=-1 suppresses warning text while continuing execution")
        seen = submit(115, 'options(warn=2); warning("ERROR-NATIVE-WARNING"); after_warning_error <- TRUE')
        check(any(e["type"] == "completed" and e["status"] == "error" for e in seen),
              "warn=2 converts its warning to typed error completion")
        seen = submit(116, 'options(warn=0); cat("WARN2-SAFE", exists("after_warning_error"), bindingIsLocked(".Last.value",baseenv()), "\\n")')
        check("WARN2-SAFE FALSE TRUE" in output(seen),
              "a warning converted to an error discards its tail and restores the locked last-value binding")

        seen = submit(117, '''task_calls <- 0L
addTaskCallback(function(expr, value, ok, visible) {
  if (identical(expr, quote(731L + 29L))) {
    task_calls <<- task_calls + 1L
    cat("TASK-CALLBACK", task_calls, ok, visible, identical(value, 760L), "\\n")
    return(FALSE)
  }
  TRUE
}, name="native-console-regression")
731L + 29L
cat("TASK-AFTER", task_calls, "\\n")''')
        check(output(seen).count("TASK-CALLBACK 1 TRUE TRUE TRUE") == 1 and "TASK-AFTER 1" in output(seen),
              "a top-level task callback receives its expression, value and visibility exactly once")

        send(1, 118, 'tryCatch(repeat {}, interrupt=function(e) cat("INTERRUPT-CAUGHT\\n"))\nafter_caught_interrupt <- TRUE')
        until(lambda e: e["type"] == "busy" and e["value"])
        time.sleep(0.15)
        send(3)
        seen = top(5)
        check("INTERRUPT-CAUGHT" in output(seen)
              and sum(e["type"] == "completed" and e["commandId"] == 118 and e["status"] == "interrupted" for e in seen) == 1,
              "Stop remains typed interruption even when R code catches the interrupt condition")
        seen = submit(119, 'cat("CAUGHT-SAFE", exists("after_caught_interrupt"), bindingIsLocked(".Last.value",baseenv()), syntax_prefix, "\\n")')
        check("CAUGHT-SAFE FALSE TRUE 17" in output(seen),
              "a caught interrupt discards later expressions, restores the binding and retains earlier variables")

    try:
        seen = top()
        check(any(event["type"] == "ready" and event["protocol"] == 1 for event in seen), "ready precedes initial top prompt")
        check("package 'stats'" not in output(seen) and 'package "stats"' not in output(seen), "stats package has no startup load warning")
        seen = submit(18, '''stopifnot(all(paste0("package:", getOption("defaultPackages")) %in% search()))
tt <- t.test(c(1, 2, 4, 5), mu=0)
pw <- power.t.test(n=20, delta=1, sd=1)
mat <- crossprod(matrix(c(1, 2, 3, 4, 6, 8), nrow=3))
ch <- chol(mat)
ss <- svd(mat)
stopifnot(is.finite(tt$p.value), is.finite(pw$power),
          max(abs(crossprod(ch) - mat)) < 1e-10,
          max(abs(ss$u %*% diag(ss$d) %*% t(ss$v) - mat)) < 1e-10)
cat("DEFAULT-PACKAGES-AND-LAPACK-READY\\n")''')
        check("DEFAULT-PACKAGES-AND-LAPACK-READY" in output(seen), "all default packages, t.test, power.t.test, chol and SVD work immediately")
        seen = submit(21, 'svg("native-startup.svg"); plot(1:10); abline(a=0, b=1); dev.off(); stopifnot(file.info("native-startup.svg")$size > 0); cat("SVG-STARTUP-READY\\n")')
        check("SVG-STARTUP-READY" in output(seen), "graphics and Cairo SVG dependencies work in a fresh session")
        seen = submit(19, 'crlf <- 1:3\r\ncat("CRLF", sum(crlf), "\\n")\r\n')
        check("CRLF 6" in output(seen), "Windows CRLF source runs unchanged at the editor boundary")
        seen = submit(20, 'cronly <- 1:4\rcat("CRONLY", sum(cronly), "\\n")\r')
        check("CRONLY 10" in output(seen), "lone CR source normalizes to logical lines")
        parse_eval_regressions()
        console_compatibility_regressions()
        seen = submit(1, 'saved <- 42; cat("UTF8: café α 😀\\n")')
        check("UTF8: café α 😀" in output(seen), "UTF-8 console output survives")

        send(1, 2, 'answer <- readline("Choose: ")\ncat("ANSWER:", answer, "\\n")')
        request = until(lambda e: e["type"] == "prompt" and e["kind"] == "input")[-1]
        check(request["maxBytes"] > 0, "input prompt advertises actual UTF-8 byte limit")
        send(2, request["requestId"], "x" * (request["maxBytes"] + 1))
        check("exceeds" in until(lambda e: e["type"] == "error")[-1]["message"], "oversized input rejected without consuming request")
        send(2, request["requestId"] + 100, "stale")
        check(until(lambda e: e["type"] == "error")[-1]["type"] == "error", "stale reply rejected")
        send(2, request["requestId"], "naïve α")
        seen = top()
        check("ANSWER: naïve α" in output(seen), "genuine readline uses its reply, not following source")

        stop_at_prompt(3, 'blocked <- readline("Stop: "); after_blocked <- TRUE', "input")
        seen = submit(4, 'cat("SAFE", exists("blocked"), exists("after_blocked"), saved, "\\n")')
        check("SAFE FALSE FALSE 42" in output(seen), "Stop does not answer readline or run following code; variables survive")

        stop_at_prompt(5, 'choice <- menu(c("One", "Two"), graphics=FALSE); after_menu <- TRUE', "input")
        seen = submit(6, 'cat("MENU", exists("choice"), exists("after_menu"), saved, "\\n")')
        check("MENU FALSE FALSE 42" in output(seen), "Stop cancels genuine menu without selecting anything")

        send(1, 7, "if (TRUE) {")
        request = until(lambda e: e["type"] == "prompt" and e["kind"] == "continuation")[-1]
        send(2, request["requestId"], "saved <- saved + 1\r\n}")
        seen = top()
        check(any(e["type"] == "completed" and e["commandId"] == 7 for e in seen), "multiline continuation completes original command")
        stop_at_prompt(8, "if (TRUE) {", "continuation")

        for ident, source in [(9, "Sys.sleep(60); after_sleep <- TRUE"), (10, "repeat {}")]:
            send(1, ident, source)
            until(lambda e: e["type"] == "busy" and e["value"])
            time.sleep(0.15)
            start = time.monotonic()
            send(3)
            seen = top(5)
            check(time.monotonic() - start < 3 and any(e["type"] == "completed" and e["status"] == "interrupted" for e in seen), "Stop interrupts active R command " + str(ident))
        seen = submit(11, 'cat("PRESERVED", saved, exists("after_sleep"), "\\n")')
        check("PRESERVED 43 FALSE" in output(seen), "state preserved across continuation, sleep and CPU interruption")

        seen = submit(12, 'stop("expected test error"); after_error <- TRUE')
        check(any(e["type"] == "completed" and e["status"] == "error" for e in seen), "R error returns typed error completion")
        seen = submit(13, 'cat("ONCE", saved, exists("after_error"), "\\n")')
        check(output(seen).count("ONCE 43 FALSE") == 1, "next command runs once after R error")

        seen = submit(14, 'cat(\'{"type":"ready","protocol":999}\\n\'); system("cmd /c echo NATIVE-OUTPUT"); system("cmd /c set /p X="); cat("AFTER-NATIVE\\n")')
        check('"protocol":999' in output(seen) and "AFTER-NATIVE" in output(seen), "console JSON cannot forge transport; raw child stdin cannot consume protocol")
        time.sleep(0.1)
        check(any("NATIVE-OUTPUT" in line for line in raw), "native child output uses separately drained stderr")
        check(sum(e["type"] == "ready" for e in all_events) == 1, "exactly one actual ready event")
        send(1, 15, 'line <- readLines(stdin(), n=1); cat("CONSOLE-STDIN", line, "\\n")')
        request = until(lambda e: e["type"] == "prompt" and e["kind"] == "input")[-1]
        send(2, request["requestId"], "typed answer")
        check("CONSOLE-STDIN typed answer" in output(top()), "R console stdin routes through a genuine input callback")
        submit(16, "")
        send(1, 17, 'shutdown_answer <- readline("Closing: "); writeLines("BAD", "shutdown-followon.txt")')
        until(lambda e: e["type"] == "prompt" and e["kind"] == "input")
        send(4)
        until(lambda e: e["type"] == "stopped")
        check(process.wait(timeout=5) == 0, "graceful shutdown")
        check(not (pathlib.Path(working) / "shutdown-followon.txt").exists(), "shutdown during input does not answer a question or run following source")
        if REQUIRE_PROTECTION_CHECKS:
            markers = re.findall(r"STATSDIRECT_PROTECTION_CHECK_OK top=(-?\d+)", "".join(raw))
            check(markers and all(int(top) == 0 for top in markers),
                  "the diagnostic worker reports balanced, nonnegative protection-stack boundaries throughout the session")
            print(f"Protection boundaries checked: {len(markers)}; all top=0", flush=True)
        print(f"{checks} native checks passed", flush=True)
    finally:
        if process.poll() is None:
            process.kill()
            process.wait(timeout=5)
