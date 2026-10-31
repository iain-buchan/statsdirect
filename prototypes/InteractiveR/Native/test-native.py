"""Focused real-R transport and interrupt tests; never loads R into Python."""
import json
import pathlib
import queue
import struct
import subprocess
import sys
import tempfile
import threading
import time

ROOT = pathlib.Path(__file__).resolve().parent
RHOME = sys.argv[1] if len(sys.argv) > 1 else r"C:\Program Files\R\R-4.6.1"
checks = 0


def check(value, description):
    global checks
    assert value, description
    checks += 1
    print("PASS", description, flush=True)


with tempfile.TemporaryDirectory(prefix="native-r-", dir=ROOT / "bin") as working:
    process = subprocess.Popen(
        [str(ROOT / "bin" / "StatsDirect.RConsole.Native.exe"), "--r-home", RHOME,
         "--working-directory", working],
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
        creationflags=subprocess.CREATE_NO_WINDOW,
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

    try:
        seen = top()
        check(any(event["type"] == "ready" and event["protocol"] == 1 for event in seen), "ready precedes initial top prompt")
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
        send(2, request["requestId"], "saved <- saved + 1\n}")
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
        print(f"{checks} native checks passed", flush=True)
    finally:
        if process.poll() is None:
            process.kill()
            process.wait(timeout=5)
