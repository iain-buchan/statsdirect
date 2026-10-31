/* StatsDirect interactive R console worker. MIT, matching StatsDirect.
 * R is loaded only into this disposable sidecar, never into the application.
 * The transport thread never calls R. All callbacks and R APIs use main(). */
#define WIN32_LEAN_AND_MEAN
#define Win32
#define _CRT_SECURE_NO_WARNINGS
#include <windows.h>
#include <process.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <io.h>
#include <fcntl.h>
#define R_NO_REMAP
#include <Rversion.h>
#include <R_ext/RStartup.h>
#include <Rinternals.h>
#include <R_ext/Parse.h>

#define MAX_FRAME (16u * 1024u * 1024u)
typedef struct Message {
    struct Message *next;
    unsigned char opcode;
    uint32_t id;
    char *text;
    size_t length;
} Message;

static HANDLE control_in, event_out, input_available;
static CRITICAL_SECTION queue_lock, output_lock;
static Message *queue_first, *queue_last;
static volatile LONG interrupted, shutdown_requested, accepting_submit;
static int command_interrupted, command_error, busy, prompt_type = 1;
static uint32_t command_id, next_request_id;
static int has_command;
static char *source;
static size_t source_length, source_offset;

static int (*r_defparams)(Rstart, int);
static void (*r_setparams)(Rstart);
static void (*r_setargs)(int, char **);
static void (*r_starttime)(void), (*r_setup)(void);
static int (*r_toplevelexec)(void (*)(void *), void *);
static SEXP (*r_protect)(SEXP), (*r_utf8char)(const char *, cetype_t), (*r_scalarstring)(SEXP);
static void (*r_unprotect)(int), (*r_print)(SEXP), (*r_definevar)(SEXP, SEXP, SEXP);
static void (*r_printwarnings)(void);
static void (*r_toplevelhandlers)(SEXP, SEXP, Rboolean, Rboolean);
static Rboolean (*r_bindinglocked)(SEXP, SEXP);
static void (*r_unlockbinding)(SEXP, SEXP), (*r_lockbinding)(SEXP, SEXP);
static SEXP (*r_withcleanup)(SEXP (*)(void *), void *, void (*)(void *), void *);
static SEXP (*r_parse)(SEXP, int, ParseStatus *, SEXP), (*r_eval)(SEXP, SEXP);
static SEXP (*r_getattrib)(SEXP, SEXP), (*r_install)(const char *), (*r_vectorat)(SEXP, R_xlen_t);
static SEXP (*r_lang3)(SEXP, SEXP, SEXP), (*r_getoption)(SEXP), (*r_aschar)(SEXP);
static R_xlen_t (*r_length)(SEXP);
static int *(*r_integer)(SEXP);
static const char *(*r_utf8)(SEXP);
static void (*r_error)(const char *, ...);
static SEXP *r_nil, *r_global, *r_base;
static Rboolean *r_visible;
static const char *r_parse_error;
#ifdef STATSDIRECT_R_PROTECTION_CHECKS
static int *r_protection_top;
#endif
static void (*r_processevents)(void), (*r_checkinterrupt)(void);
static void (*r_end)(int);
static int (*r_ga_initapp)(int, char **);
static void (*r_readconfig)(void);
static char *(*r_version)(void);
static char *(*r_getruser)(void);
static int *r_userbreak;

static void write_bytes(const char *s, size_t n)
{
    while (n) {
        DWORD done;
        if (!WriteFile(event_out, s, (DWORD)n, &done, NULL) || !done)
            ExitProcess(3);
        s += done; n -= done;
    }
}

static void json_string(const char *s, size_t n)
{
    static const char hex[] = "0123456789abcdef";
    size_t start = 0;
    write_bytes("\"", 1);
    for (size_t i = 0; i < n; ++i) {
        unsigned char c = (unsigned char)s[i];
        if (c == '"' || c == '\\' || c < 32) {
            if (i > start) write_bytes(s + start, i - start);
            if (c == '"' || c == '\\') {
                char escaped[2] = { '\\', (char)c };
                write_bytes(escaped, 2);
            } else {
                char escaped[6] = { '\\', 'u', '0', '0', hex[c >> 4], hex[c & 15] };
                write_bytes(escaped, 6);
            }
            start = i + 1;
        }
    }
    if (n > start) write_bytes(s + start, n - start);
    write_bytes("\"", 1);
}

static void begin_event(const char *kind)
{
    EnterCriticalSection(&output_lock);
    write_bytes("{\"type\":", 8);
    json_string(kind, strlen(kind));
}

static void number_field(const char *name, uint32_t value)
{
    char field[100];
    int n = snprintf(field, sizeof(field), ",\"%s\":%u", name, (unsigned)value);
    write_bytes(field, (size_t)n);
}

static void string_field(const char *name, const char *value, size_t length)
{
    write_bytes(",", 1); json_string(name, strlen(name)); write_bytes(":", 1);
    json_string(value, length);
}

static void end_event(void)
{
    write_bytes("}\n", 2);
    LeaveCriticalSection(&output_lock);
}

static void error_event(const char *message)
{
    begin_event("error"); string_field("message", message, strlen(message)); end_event();
}

static void completed(void)
{
    if (!has_command) return;
    const char *status = command_interrupted ? "interrupted" : command_error ? "error" : "ok";
    begin_event("completed"); number_field("commandId", command_id);
    string_field("status", status, strlen(status)); end_event();
    has_command = 0; command_id = 0;
    free(source); source = NULL; source_length = source_offset = 0;
    command_interrupted = command_error = 0;
}

static void output(const char *text, int length, int type)
{
    if (length <= 0) return;
    begin_event("output"); number_field("commandId", command_id);
    const char *stream = type ? "stderr" : "stdout";
    string_field("stream", stream, strlen(stream));
    string_field("text", text, (size_t)length); end_event();
}

static void set_busy(int value)
{
    busy = value;
    begin_event("busy"); number_field("commandId", command_id);
    write_bytes(value ? ",\"value\":true" : ",\"value\":false", value ? 13 : 14);
    end_event();
}

static void process_control(void)
{
    if (InterlockedExchange(&interrupted, 0)) {
        command_interrupted = 1;
        *r_userbreak = 1;
        r_checkinterrupt(); /* R main thread only; unwinds to R_ToplevelExec. */
    }
}

static void free_message(Message *m)
{
    if (m) { free(m->text); free(m); }
}

static Message *dequeue(void)
{
    EnterCriticalSection(&queue_lock);
    Message *m = queue_first;
    if (m) {
        queue_first = m->next;
        if (!queue_first) queue_last = NULL;
    }
    LeaveCriticalSection(&queue_lock);
    return m;
}

static int read_exact(HANDLE handle, void *buffer, DWORD size)
{
    unsigned char *p = buffer;
    while (size) {
        DWORD got;
        if (!ReadFile(handle, p, size, &got, NULL) || !got) return 0;
        p += got; size -= got;
    }
    return 1;
}

static uint32_t u32(const unsigned char *b)
{
    return (uint32_t)b[0] | (uint32_t)b[1] << 8 | (uint32_t)b[2] << 16 | (uint32_t)b[3] << 24;
}

static unsigned __stdcall read_control(void *unused)
{
    (void)unused;
    for (;;) {
        unsigned char header[4];
        if (!read_exact(control_in, header, 4)) break;
        uint32_t length = u32(header);
        if (length < 1 || length > MAX_FRAME) { error_event("Invalid control frame length."); break; }
        unsigned char *frame = malloc((size_t)length + 1);
        if (!frame) break;
        if (!read_exact(control_in, frame, length)) { free(frame); break; }
        frame[length] = 0;
        unsigned char opcode = frame[0];
        if ((opcode == 3 || opcode == 4) && length == 1) {
            if (opcode == 4) InterlockedExchange(&shutdown_requested, 1);
            InterlockedExchange(&interrupted, 1);
            SetEvent(input_available); free(frame);
            if (opcode == 4) return 0;
            continue;
        }
        if ((opcode != 1 && opcode != 2) || length < 5 || memchr(frame + 5, 0, length - 5)) {
            error_event("Invalid control command."); free(frame); continue;
        }
        if (length > 5 && !MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS,
                                             (const char *)frame + 5, (int)length - 5, NULL, 0)) {
            error_event("Control text is not valid UTF-8."); free(frame); continue;
        }
        if (opcode == 1 && InterlockedCompareExchange(&accepting_submit, 0, 1) != 1) {
            error_event("A submission is already active; wait for the next top prompt.");
            free(frame); continue;
        }
        Message *m = calloc(1, sizeof(*m));
        if (!m) { free(frame); break; }
        m->opcode = opcode; m->id = u32(frame + 1); m->length = length - 5;
        m->text = malloc(m->length + 2);
        if (!m->text) { free(m); free(frame); break; }
        memcpy(m->text, frame + 5, m->length); m->text[m->length] = 0;
        free(frame);
        EnterCriticalSection(&queue_lock);
        if (queue_last) queue_last->next = m; else queue_first = m;
        queue_last = m;
        LeaveCriticalSection(&queue_lock);
        SetEvent(input_available);
    }
    InterlockedExchange(&shutdown_requested, 1);
    InterlockedExchange(&interrupted, 1);
    SetEvent(input_available);
    return 0;
}

static void prompt_event(const char *kind, const char *prompt, uint32_t request, int buffer_length)
{
    begin_event("prompt"); number_field("commandId", command_id);
    number_field("requestId", request);
    string_field("kind", kind, strlen(kind));
    if (!strcmp(kind, "input")) number_field("maxBytes", (uint32_t)(buffer_length > 2 ? buffer_length - 2 : 0));
    string_field("text", prompt, strlen(prompt)); end_event();
}

static int adopt_source(Message *m, int is_new)
{
    size_t pending = is_new ? 0 : source_length - source_offset;
    if (pending > MAX_FRAME - m->length - 2) {
        error_event("The incomplete R expression exceeds the command limit."); return 0;
    }
    char *combined = malloc(pending + m->length + 2);
    if (!combined) { error_event("Cannot allocate R source."); return 0; }
    if (pending) memcpy(combined, source + source_offset, pending);
    if (is_new) {
        has_command = 1; command_id = m->id;
        command_error = command_interrupted = 0;
    }
    free(source); source = combined;
    /* Console buffers contain logical lines. Windows-authored .R files retain
     * their original text in the editor, but R's parser needs LF here. */
    source_length = pending;
    for (size_t i = 0; i < m->length; ++i) {
        char c = m->text[i];
        if (c == '\r') {
            if (i + 1 < m->length && m->text[i + 1] == '\n') ++i;
            c = '\n';
        }
        source[source_length++] = c;
    }
    if (!source_length || source[source_length - 1] != '\n') source[source_length++] = '\n';
    source[source_length] = 0; source_offset = 0;
    return 1;
}

static int read_request(const char *prompt, unsigned char *buffer, int length, int is_input)
{
    uint32_t request = ++next_request_id;
    const char *kind = is_input ? "input" : prompt_type == 2 ? "continuation" : "top";
    InterlockedExchange(&accepting_submit, !is_input && prompt_type == 1);
    prompt_event(kind, prompt, request, length);
    for (;;) {
        if (InterlockedCompareExchange(&shutdown_requested, 0, 0)) {
            /* EOF is appropriate only at an idle top prompt. During execution
             * it could answer readline and allow following code to run. */
            if (has_command || is_input || prompt_type == 2) process_control();
            return 0;
        }
        process_control();
        Message *m = dequeue();
        if (m) {
            if (!is_input && prompt_type == 1 && m->opcode == 1) {
                int accepted = adopt_source(m, 1); free_message(m);
                if (accepted) return 1;
                InterlockedExchange(&accepting_submit, 1); continue;
            }
            if (m->opcode == 2 && m->id == request) {
                if (!is_input && prompt_type == 2) {
                    int accepted = adopt_source(m, 0); free_message(m);
                    if (accepted) return 1;
                    continue;
                }
                if (is_input) {
                    if (m->length + 2 > (size_t)length) {
                        error_event("Input reply exceeds R's console buffer."); free_message(m); continue;
                    }
                    memcpy(buffer, m->text, m->length);
                    buffer[m->length] = '\n'; buffer[m->length + 1] = 0;
                    free_message(m); return 1;
                }
            }
            error_event("Command is not valid for the current prompt, or its requestId is stale.");
            free_message(m); continue;
        }
        WaitForSingleObject(input_available, 50);
        r_processevents();
    }
}

static int read_console(const char *prompt, unsigned char *buffer, int length, int history)
{
    (void)history;
    /* Only R's nested input uses this callback. Top and continuation requests
     * come from our parser's status, even when their visible prompts match. */
    return read_request(prompt, buffer, length, 1);
}

static void show_message(const char *text) { output(text, (int)strlen(text), 1); }
static void noop(void) { }
static int yes_no_cancel(const char *text)
{
    unsigned char answer[100];
    if (!read_console(text, answer, (int)sizeof(answer), 0)) return 0;
    return answer[0] == 'y' || answer[0] == 'Y' ? 1 : answer[0] == 'n' || answer[0] == 'N' ? -1 : 0;
}
static const char *console_prompt(const char *name, const char *fallback)
{
    SEXP value = r_getoption(r_install(name));
    return value == *r_nil ? fallback : r_utf8(r_aschar(value));
}

typedef struct LastValueUpdate { SEXP symbol, value; int locked; } LastValueUpdate;
static SEXP assign_last_value(void *data)
{
    LastValueUpdate *update = data;
    if (update->locked) r_unlockbinding(update->symbol, *r_base);
    r_definevar(update->symbol, update->value, *r_base);
    return *r_nil;
}
static void restore_last_value_lock(void *data)
{
    LastValueUpdate *update = data;
    if (update->locked) r_lockbinding(update->symbol, *r_base);
}

/* Parse one expression at a time, letting R tell us exactly which bytes it
 * consumed, including the delimiter and any trailing comment. This preserves
 * completed expressions before a later syntax error or incomplete expression.
 * No R syntax or prompt text is interpreted by the host. */
static size_t consumed_source(SEXP expressions, const char *text, size_t length)
{
    SEXP ref = r_getattrib(expressions, r_install("wholeSrcref"));
    if (r_length(ref) < 8) r_error("R did not return a source position.");
    const int *position = r_integer(ref);
    if (position[7] < 1 || position[5] < 0) r_error("R returned an invalid source position.");
    size_t offset = 0;
    /* R's logical line can be changed by #line. Its physical line and UTF-8
     * column remain usable; its byte counter can overcount multibyte text.
     * Match R's documented column counting, including eight-column tabs. */
    for (int line = 1; line < position[7] && offset < length; ++line) {
        while (offset < length && text[offset++] != '\n') { }
    }
    int column = 0;
    while (offset < length && column < position[5]) {
        unsigned char c = (unsigned char)text[offset++];
        if (c == '\n') r_error("R returned a source column beyond its line.");
        if (c == '\t') column = (column + 8) & ~7;
        else if (c < 0x80 || c >= 0xc0) ++column;
    }
    while (offset < length && ((unsigned char)text[offset] & 0xc0) == 0x80) ++offset;
    if (!offset) r_error("R did not consume the parsed expression.");
    return offset;
}

/* Global calling handlers that a command installs live in the toplevel context R_ToplevelExec made for that command and
 * discarded with it, while R's own record of them persists: they are re-armed from that record before the next command,
 * as R's REPL keeps them.  Not under try(): globalCallingHandlers() refuses to run with a handler on the stack, and try()
 * puts one there; the stack is empty at the start of a command, so nothing here can fail. */
static void rearm_global_handlers(void)
{
    static const char code[] =
        "local({ h <- globalCallingHandlers(); if (length(h)) { globalCallingHandlers(NULL); globalCallingHandlers(h) } })";
    SEXP text = r_protect(r_scalarstring(r_utf8char(code, CE_UTF8)));
    ParseStatus status;
    SEXP expressions = r_protect(r_parse(text, -1, &status, *r_nil));
    if (status == PARSE_OK && r_length(expressions)) r_eval(r_vectorat(expressions, 0), *r_base);
    r_unprotect(2);
}

static void evaluate_command(void *unused)
{
    (void)unused;
    prompt_type = 1;
    if (!read_request(console_prompt("prompt", "> "), NULL, 0, 0)) return;
    rearm_global_handlers();
    int incremental = 0;
    while (source_offset < source_length) {
        process_control();
        SEXP chars = r_protect(r_utf8char(source + source_offset, CE_UTF8));
        SEXP text = r_protect(r_scalarstring(chars));
        SEXP filename = r_protect(r_scalarstring(r_utf8char("<console>", CE_UTF8)));
        SEXP call = r_protect(r_lang3(r_install("srcfilecopy"), filename, text));
        SEXP srcfile = r_protect(r_eval(call, *r_base));
        ParseStatus status;
        /* The whole remaining text is parsed once, as source() parses a file: work in proportion to the text, and one
         * source reference shared by the expressions.  Only text that does not parse whole goes one expression at a time,
         * so that the complete expressions before a syntax error or an unfinished one still run first. */
        SEXP expressions = r_protect(r_parse(text, incremental ? 1 : -1, &status, srcfile));
        int whole = !incremental && (status == PARSE_OK || status == PARSE_NULL || status == PARSE_EOF);
        if (!incremental && !whole) {
            incremental = 1;
            r_unprotect(1);
            expressions = r_protect(r_parse(text, 1, &status, srcfile));
        }
        if (status == PARSE_INCOMPLETE) {
            r_unprotect(6);
            prompt_type = 2;
            if (!read_request(console_prompt("continue", "+ "), NULL, 0, 0)) return;
            incremental = 0;   /* the continuation may complete the text */
            continue;
        }
        if (status != PARSE_OK && status != PARSE_NULL && status != PARSE_EOF)
            r_error("%s", *r_parse_error ? r_parse_error : "Invalid R syntax.");
        R_xlen_t count = r_length(expressions);
        if (count) {
            if (whole) source_offset = source_length;
            else source_offset += consumed_source(expressions, source + source_offset, source_length - source_offset);
            for (R_xlen_t i = 0; i < count && !command_interrupted; ++i) {
                if (i) process_control();
                set_busy(1);
                SEXP value = r_protect(r_eval(r_vectorat(expressions, i), *r_global));
                /* R's REPL updates this base binding directly. Use public binding
                 * APIs and restore its original lock even if an R error unwinds. */
                LastValueUpdate update = { r_install(".Last.value"), value, 0 };
                update.locked = r_bindinglocked(update.symbol, *r_base);
                r_withcleanup(assign_last_value, &update, restore_last_value_lock, &update);
                Rboolean visible = *r_visible;
                if (visible) r_print(value);
                r_printwarnings();
                r_toplevelhandlers(r_vectorat(expressions, i), value, TRUE, visible);
                r_unprotect(1);
                set_busy(0);
            }
        } else source_offset = source_length;
        r_unprotect(6);
        /* A user's interrupt handler can return normally. Stop still cancels
         * the remaining expressions of this submitted command. */
        if (command_interrupted) { source_offset = source_length; break; }
    }
    prompt_type = 1;
    completed();
}

/* The oldest R the worker takes: 4.2.0, the first R for Windows that is 64-bit only, has UTF-8 as its native encoding and
 * versions its start-up structure, whose 64-bit layout is unchanged through the headers this worker is built with (the field
 * R 4.4 added fills what was padding).  Every R entry point is resolved by name at run time, so a later R serves while it keeps
 * those entry points and accepts that structure version, which R confirms at start-up. */
#define MINIMUM_R_MAJOR 4
#define MINIMUM_R_MINOR 2
static int version_at_least(const char *version, int major, int minor)
{
    int v_major = 0, v_minor = 0;
    if (sscanf(version, "%d.%d", &v_major, &v_minor) != 2) return 0;
    return v_major > major || (v_major == major && v_minor >= minor);
}

static char *utf8(const wchar_t *value)
{
    int n = WideCharToMultiByte(CP_UTF8, 0, value, -1, NULL, 0, NULL, NULL);
    char *result = malloc((size_t)n);
    if (result) WideCharToMultiByte(CP_UTF8, 0, value, -1, result, n, NULL, NULL);
    return result;
}

static FARPROC required(HMODULE dll, const char *name)
{
    FARPROC proc = GetProcAddress(dll, name);
    if (!proc) {
        char message[200];
        snprintf(message, sizeof(message), "This R does not provide %s; the interactive console needs 64-bit R %d.%d or later.", name, MINIMUM_R_MAJOR, MINIMUM_R_MINOR);
        error_event(message); ExitProcess(2);
    }
    return proc;
}
#define LOAD(name, symbol) name = (void *)required(dll, symbol)

int wmain(int argc, wchar_t **argv)
{
    if (argc == 2 && !wcscmp(argv[1], L"--version")) {
        /* the host asks for the oldest R series the worker takes, to choose among the installed Rs */
        printf("%d.%d\n", MINIMUM_R_MAJOR, MINIMUM_R_MINOR); return 0;
    }
    const wchar_t *rhome = NULL, *working = NULL;
    for (int i = 1; i + 1 < argc; i += 2) {
        if (!wcscmp(argv[i], L"--r-home")) rhome = argv[i + 1];
        else if (!wcscmp(argv[i], L"--working-directory")) working = argv[i + 1];
    }
    InitializeCriticalSection(&queue_lock); InitializeCriticalSection(&output_lock);
    input_available = CreateEventW(NULL, FALSE, FALSE, NULL);
    HANDLE self = GetCurrentProcess();
    if (!DuplicateHandle(self, GetStdHandle(STD_OUTPUT_HANDLE), self, &event_out, 0, FALSE, DUPLICATE_SAME_ACCESS) ||
        !DuplicateHandle(self, GetStdHandle(STD_INPUT_HANDLE), self, &control_in, 0, FALSE, DUPLICATE_SAME_ACCESS)) return 2;
    _setmode(_fileno(stdout), _O_BINARY); _setmode(_fileno(stdin), _O_BINARY);
    /* User/native reads and writes cannot consume or forge transport frames. */
    _dup2(_fileno(stderr), _fileno(stdout));
    SetStdHandle(STD_OUTPUT_HANDLE, GetStdHandle(STD_ERROR_HANDLE));
    FILE *null_input; freopen_s(&null_input, "NUL", "rb", stdin);
    SetStdHandle(STD_INPUT_HANDLE, (HANDLE)_get_osfhandle(_fileno(stdin)));
    if (!rhome) { error_event("--r-home is required."); return 2; }
    if (working && !SetCurrentDirectoryW(working)) { error_event("Cannot open working directory."); return 2; }
    _wputenv_s(L"R_HOME", rhome);
    wchar_t dll_path[32768], bin_path[32768];
    swprintf_s(bin_path, 32768, L"%s\\bin\\x64", rhome);
    swprintf_s(dll_path, 32768, L"%s\\R.dll", bin_path);
    /* R's package loader temporarily changes its DLL directory. Keep R's
     * BLAS/LAPACK and other package dependencies discoverable afterwards too.
     * This PATH change belongs only to this isolated helper and its children. */
    const wchar_t *inherited_path = _wgetenv(L"PATH");
    size_t path_size = wcslen(bin_path) + (inherited_path ? wcslen(inherited_path) : 0) + 2;
    wchar_t *r_path = malloc(path_size * sizeof(wchar_t));
    if (!r_path) { error_event("Cannot allocate the R process environment."); return 2; }
    swprintf_s(r_path, path_size, L"%s;%s", bin_path, inherited_path ? inherited_path : L"");
    if (_wputenv_s(L"PATH", r_path) || !SetEnvironmentVariableW(L"PATH", r_path)) {
        free(r_path); error_event("Cannot prepare the R process PATH."); return 2;
    }
    free(r_path);
    SetDllDirectoryW(bin_path);
    HMODULE dll = LoadLibraryW(dll_path);
    if (!dll) { error_event("Cannot load the 64-bit R.dll of the requested R installation; the interactive console needs 64-bit R."); return 2; }
    LOAD(r_defparams, "R_DefParamsEx"); LOAD(r_setparams, "R_SetParams");
    LOAD(r_setargs, "R_set_command_line_arguments"); LOAD(r_starttime, "R_setStartTime");
    LOAD(r_setup, "setup_Rmainloop");
    LOAD(r_toplevelexec, "R_ToplevelExec"); LOAD(r_processevents, "R_ProcessEvents");
    LOAD(r_protect, "Rf_protect"); LOAD(r_unprotect, "Rf_unprotect");
    LOAD(r_utf8char, "Rf_mkCharCE"); LOAD(r_scalarstring, "Rf_ScalarString");
    LOAD(r_parse, "R_ParseVector"); LOAD(r_eval, "Rf_eval"); LOAD(r_print, "Rf_PrintValue");
    LOAD(r_printwarnings, "Rf_PrintWarnings"); LOAD(r_toplevelhandlers, "Rf_callToplevelHandlers");
    LOAD(r_getattrib, "Rf_getAttrib"); LOAD(r_install, "Rf_install"); LOAD(r_vectorat, "VECTOR_ELT");
    LOAD(r_length, "Rf_xlength"); LOAD(r_integer, "INTEGER"); LOAD(r_lang3, "Rf_lang3");
    LOAD(r_getoption, "Rf_GetOption1"); LOAD(r_aschar, "Rf_asChar"); LOAD(r_utf8, "Rf_translateCharUTF8");
    LOAD(r_error, "Rf_error"); LOAD(r_definevar, "Rf_defineVar");
    LOAD(r_bindinglocked, "R_BindingIsLocked"); LOAD(r_unlockbinding, "R_unLockBinding");
    LOAD(r_lockbinding, "R_LockBinding"); LOAD(r_withcleanup, "R_ExecWithCleanup");
    r_nil = (SEXP *)required(dll, "R_NilValue");
    r_global = (SEXP *)required(dll, "R_GlobalEnv"); r_base = (SEXP *)required(dll, "R_BaseEnv");
    r_visible = (Rboolean *)required(dll, "R_Visible"); r_parse_error = (const char *)required(dll, "R_ParseErrorMsg");
#ifdef STATSDIRECT_R_PROTECTION_CHECKS
    r_protection_top = (int *)required(dll, "R_PPStackTop");
#endif
    LOAD(r_checkinterrupt, "R_CheckUserInterrupt"); LOAD(r_end, "Rf_endEmbeddedR");
    HMODULE graphapp = GetModuleHandleW(L"Rgraphapp.dll");
    r_ga_initapp = (void *)required(graphapp, "GA_initapp");
    LOAD(r_readconfig, "readconsolecfg"); LOAD(r_version, "getDLLVersion"); LOAD(r_getruser, "getRUser");
    r_userbreak = (int *)required(dll, "UserBreak");
    const char *runtime_version = r_version();
    if (!version_at_least(runtime_version, MINIMUM_R_MAJOR, MINIMUM_R_MINOR)) {
        char message[200];
        snprintf(message, sizeof(message), "The interactive console needs 64-bit R %d.%d or later; this R installation is %s.", MINIMUM_R_MAJOR, MINIMUM_R_MINOR, runtime_version);
        error_event(message); return 2;
    }
    structRstart params;
    memset(&params, 0, sizeof(params));
    r_starttime();
    /* R fills the structure for the version asked for and refuses a version it does not know: a later R that dropped this one
     * is refused here, before any field of the structure is used */
    if (r_defparams(&params, RSTART_VERSION)) {
        char message[200];
        snprintf(message, sizeof(message), "R %s does not accept the interactive console's start-up structure (version %d).", runtime_version, RSTART_VERSION);
        error_event(message); return 2;
    }
    char *home = utf8(rhome);
    /* The user's home as R's own front ends take it (R_USER, HOME, else the Documents folder), so that ~ means here
     * what it means in Rscript and Rgui; the string lives as long as R does. */
    char *user_home = r_getruser();
    if (!user_home) { const wchar_t *user = _wgetenv(L"USERPROFILE"); user_home = utf8(user ? user : rhome); }
    params.rhome = home; params.home = user_home; params.CharacterMode = LinkDLL;
    params.R_Quiet = 1; params.R_Interactive = 1; params.R_NoEcho = 0;
    params.LoadSiteFile = params.LoadInitFile = 0; params.NoRenviron = 1;
    params.RestoreAction = SA_NORESTORE; params.SaveAction = SA_NOSAVE;
    params.ReadConsole = read_console; params.WriteConsole = NULL; params.WriteConsoleEx = output;
    params.CallBack = process_control; params.Busy = set_busy; params.ShowMessage = show_message;
    params.YesNoCancel = yes_no_cancel; params.ClearerrConsole = noop;
    params.FlushConsole = noop; params.ResetConsole = noop; params.EmitEmbeddedUTF8 = 0;
    r_setparams(&params);
    char *r_args[] = { "StatsDirect.RConsole.Native", "--vanilla", "--quiet", NULL };
    r_setargs(3, r_args); r_ga_initapp(0, NULL); r_readconfig();
    uintptr_t reader = _beginthreadex(NULL, 0, read_control, NULL, 0, NULL);
    if (!reader) { error_event("Cannot start the control reader."); return 2; }
    r_setup();
    begin_event("ready"); number_field("protocol", 1); number_field("pid", GetCurrentProcessId());
    string_field("rVersion", r_version(), strlen(r_version())); end_event();
    while (!InterlockedCompareExchange(&shutdown_requested, 0, 0)) {
#ifdef STATSDIRECT_R_PROTECTION_CHECKS
        int expected_top = *r_protection_top;
        if (expected_top < 0) { error_event("R protection stack is negative before evaluation."); return 70; }
#endif
        /* Unlike R_ReplDLLdo1, our callback keeps R_ToplevelExec's protected
         * roots intact. R owns the longjmp boundary for errors and Stop. */
        if (!r_toplevelexec(evaluate_command, NULL)) {
            command_error = !command_interrupted;
            source_offset = source_length; busy = 0; prompt_type = 1;
            *r_userbreak = 0; InterlockedExchange(&interrupted, 0);
            completed();
        }
#ifdef STATSDIRECT_R_PROTECTION_CHECKS
        if (*r_protection_top != expected_top) { error_event("R protection stack changed across evaluation."); return 70; }
        fprintf(stderr, "STATSDIRECT_PROTECTION_CHECK_OK top=%d\n", *r_protection_top); fflush(stderr);
#endif
    }
    InterlockedExchange(&interrupted, 0); *r_userbreak = 0;
    r_end(0);
    begin_event("stopped"); end_event();
    return 0;
}
