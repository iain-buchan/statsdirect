/* StatsDirect interactive R console prototype. MIT, matching StatsDirect.
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
#include <Rversion.h>
#include <R_ext/RStartup.h>

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
static void (*r_starttime)(void), (*r_setup)(void), (*r_replinit)(void);
static int (*r_repldo1)(void);
static int (*r_toplevelexec)(void (*)(void *), void *);
static void (*r_processevents)(void), (*r_checkinterrupt)(void);
static void (*r_end)(int);
static int (*r_ga_initapp)(int, char **);
static void (*r_readconfig)(void);
static char *(*r_version)(void);
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

/* Copy only one source line. Incomplete long lines are supplied on subsequent
 * ReadConsole calls. The user's script never enters the reply queue. */
static int take_source(unsigned char *buffer, int length)
{
    size_t amount = 0;
    while (source_offset < source_length && amount + 1 < (size_t)length) {
        char c = source[source_offset++]; buffer[amount++] = (unsigned char)c;
        if (c == '\n') break;
    }
    buffer[amount] = 0;
    return amount != 0;
}

static void adopt_source(Message *m, int is_new)
{
    if (is_new) {
        has_command = 1; command_id = m->id;
        command_error = command_interrupted = 0;
    }
    free(source); source = m->text; m->text = NULL;
    source_length = m->length;
    if (!source_length || source[source_length - 1] != '\n') source[source_length++] = '\n';
    source[source_length] = 0; source_offset = 0;
}

static int read_console(const char *prompt, unsigned char *buffer, int length, int history)
{
    /* Busy=1 and/or history=0 describes nested input, including readline/menu.
     * Main-loop continuation comes from R_ReplDLLdo1, never matching prompt text. */
    int is_input = busy || !history;
    if (!is_input && has_command && source_offset < source_length)
        return take_source(buffer, length);
    if (!is_input && prompt_type == 1) completed();
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
                adopt_source(m, 1); free_message(m);
                return take_source(buffer, length);
            }
            if (m->opcode == 2 && m->id == request) {
                if (!is_input && prompt_type == 2) {
                    adopt_source(m, 0); free_message(m);
                    return take_source(buffer, length);
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

static void show_message(const char *text) { output(text, (int)strlen(text), 1); }
static void noop(void) { }
static int yes_no_cancel(const char *text)
{
    unsigned char answer[100];
    if (!read_console(text, answer, (int)sizeof(answer), 0)) return 0;
    return answer[0] == 'y' || answer[0] == 'Y' ? 1 : answer[0] == 'n' || answer[0] == 'N' ? -1 : 0;
}
static int repl_result;
static void iteration(void *unused) { (void)unused; repl_result = r_repldo1(); }

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
    if (!proc) { error_event(name); ExitProcess(2); }
    return proc;
}
#define LOAD(name, symbol) name = (void *)required(dll, symbol)

int wmain(int argc, wchar_t **argv)
{
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
    SetDllDirectoryW(bin_path);
    HMODULE dll = LoadLibraryW(dll_path);
    if (!dll) { error_event("Cannot load R.dll from the requested R installation."); return 2; }
    LOAD(r_defparams, "R_DefParamsEx"); LOAD(r_setparams, "R_SetParams");
    LOAD(r_setargs, "R_set_command_line_arguments"); LOAD(r_starttime, "R_setStartTime");
    LOAD(r_setup, "setup_Rmainloop"); LOAD(r_replinit, "R_ReplDLLinit"); LOAD(r_repldo1, "R_ReplDLLdo1");
    LOAD(r_toplevelexec, "R_ToplevelExec"); LOAD(r_processevents, "R_ProcessEvents");
    LOAD(r_checkinterrupt, "R_CheckUserInterrupt"); LOAD(r_end, "Rf_endEmbeddedR");
    HMODULE graphapp = GetModuleHandleW(L"Rgraphapp.dll");
    r_ga_initapp = (void *)required(graphapp, "GA_initapp");
    LOAD(r_readconfig, "readconsolecfg"); LOAD(r_version, "getDLLVersion");
    r_userbreak = (int *)required(dll, "UserBreak");
    char compiled_version[32]; snprintf(compiled_version, sizeof(compiled_version), "%s.%s", R_MAJOR, R_MINOR);
    /* The public startup struct must match the selected R headers/runtime. */
    if (strcmp(r_version(), compiled_version)) { error_event("R runtime does not match the native helper's build headers."); return 2; }
    structRstart params;
    r_starttime(); r_defparams(&params, RSTART_VERSION);
    char *home = utf8(rhome);
    const wchar_t *user = _wgetenv(L"USERPROFILE");
    char *user_home = utf8(user ? user : rhome);
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
    r_setup(); r_replinit();
    begin_event("ready"); number_field("protocol", 1); number_field("pid", GetCurrentProcessId());
    string_field("rVersion", r_version(), strlen(r_version())); end_event();
    while (!InterlockedCompareExchange(&shutdown_requested, 0, 0)) {
        if (!r_toplevelexec(iteration, NULL)) {
            command_error = !command_interrupted;
            source_offset = source_length; busy = 0; prompt_type = 1;
            *r_userbreak = 0; InterlockedExchange(&interrupted, 0);
            r_replinit(); completed();
        } else {
            if (repl_result < 0) break;
            prompt_type = repl_result;
        }
    }
    InterlockedExchange(&interrupted, 0); *r_userbreak = 0;
    r_end(0);
    begin_event("stopped"); end_event();
    return 0;
}
