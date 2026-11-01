// Copyright (c) 2026 StatsDirect Limited. MIT licence.
#include "../../SDBootstrapper/Focus/FocusApi.h"
#include <cstdio>
#include <cwchar>
#include <cstring>
#include <cstddef>

// Exercise the production callbacks without posting to or activating any real
// desktop window. The compiled DLL's exported ABI is checked separately below.
static HWND testWindow = reinterpret_cast<HWND>(0x1234);
static HWND otherWindow = reinterpret_cast<HWND>(0x5678);
constexpr UINT TestRestoreMessage = 0xc123;
constexpr DWORD CallbackThread = 500, WindowThread = 600;
static bool validWindow, visibleWindow, enabledWindow, minimizedWindow;
static BOOL allowForeground, allowPost, allowRaise;
static bool foregroundActuallyChanges, destroyDuringRaise;
static int demotionFailures, windowChangeDuringForeground;
static LONG_PTR extendedStyle;
static UINT registeredMessage;
static DWORD currentThread, foregroundThread;
static int foregroundCalls, postCalls, registrationCalls;
static HWND requestedWindow, postedWindow, foregroundWindow;
static UINT postedMessage;
static WPARAM postedWParam;
static LPARAM postedLParam;
static wchar_t registeredName[128];
struct PositionCall { HWND window, insertAfter; int x, y, width, height; UINT flags; DWORD thread; };
static PositionCall positionCalls[8];
static int positionCount;
static void ResetMocks()
{
    validWindow = visibleWindow = enabledWindow = true; minimizedWindow = false;
    allowForeground = allowPost = TRUE; registeredMessage = TestRestoreMessage;
    currentThread = CallbackThread; foregroundThread = 0;
    foregroundCalls = postCalls = registrationCalls = 0;
    requestedWindow = postedWindow = nullptr; foregroundWindow = otherWindow;
    postedMessage = 0; postedWParam = 1; postedLParam = 1; registeredName[0] = 0;
    allowRaise = TRUE; foregroundActuallyChanges = true; destroyDuringRaise = false;
    demotionFailures = windowChangeDuringForeground = positionCount = 0;
    extendedStyle = WS_EX_APPWINDOW;
}
static BOOL WINAPI TestIsWindow(HWND window) { return validWindow && window == testWindow; }
static BOOL WINAPI TestIsWindowVisible(HWND window) { return visibleWindow && window == testWindow; }
static BOOL WINAPI TestIsWindowEnabled(HWND window) { return enabledWindow && window == testWindow; }
static BOOL WINAPI TestIsIconic(HWND window) { return minimizedWindow && window == testWindow; }
static DWORD WINAPI TestGetCurrentThreadId() { return currentThread; }
static DWORD WINAPI TestGetWindowThreadProcessId(HWND window, LPDWORD process)
{
    if (process) *process = window == testWindow ? 700 : window == otherWindow ? 900 : 0;
    return window == testWindow ? WindowThread : window == otherWindow ? 800 : 0;
}
static HWND WINAPI TestGetForegroundWindow() { return foregroundWindow; }
static BOOL WINAPI TestSetForegroundWindow(HWND window)
{
    ++foregroundCalls; requestedWindow = window; foregroundThread = currentThread;
    if (allowForeground && foregroundActuallyChanges) foregroundWindow = window;
    if (windowChangeDuringForeground == 1) visibleWindow = false;
    if (windowChangeDuringForeground == 2) enabledWindow = false;
    if (windowChangeDuringForeground == 3) minimizedWindow = true;
    if (windowChangeDuringForeground == 4) validWindow = false;
    return allowForeground;
}
static LONG_PTR WINAPI TestGetWindowLongPtrW(HWND window, int index)
{
    return validWindow && window == testWindow && index == GWL_EXSTYLE ? extendedStyle : 0;
}
static BOOL WINAPI TestSetWindowPos(HWND window, HWND insertAfter, int x, int y, int width, int height, UINT flags)
{
    if (positionCount < 8) positionCalls[positionCount] = {window, insertAfter, x, y, width, height, flags, currentThread};
    ++positionCount;
    if (!validWindow || window != testWindow) return FALSE;
    if (insertAfter == HWND_TOPMOST)
    {
        if (!allowRaise) return FALSE;
        extendedStyle |= WS_EX_TOPMOST;
        if (destroyDuringRaise) validWindow = false;
        return TRUE;
    }
    if (insertAfter == HWND_NOTOPMOST)
    {
        if (demotionFailures > 0) { --demotionFailures; return FALSE; }
        extendedStyle &= ~static_cast<LONG_PTR>(WS_EX_TOPMOST); return TRUE;
    }
    return FALSE;
}
static BOOL WINAPI TestPostMessageW(HWND window, UINT message, WPARAM wParam, LPARAM lParam)
{
    ++postCalls; postedWindow = window; postedMessage = message;
    postedWParam = wParam; postedLParam = lParam; return allowPost;
}
static UINT WINAPI TestRegisterWindowMessageW(LPCWSTR name)
{
    ++registrationCalls; std::wcsncpy(registeredName, name, 127); return registeredMessage;
}
#define IsWindow TestIsWindow
#define IsWindowVisible TestIsWindowVisible
#define IsWindowEnabled TestIsWindowEnabled
#define IsIconic TestIsIconic
#define GetCurrentThreadId TestGetCurrentThreadId
#define GetWindowThreadProcessId TestGetWindowThreadProcessId
#define GetForegroundWindow TestGetForegroundWindow
#define SetForegroundWindow TestSetForegroundWindow
#define GetWindowLongPtrW TestGetWindowLongPtrW
#define SetWindowPos TestSetWindowPos
#define PostMessageW TestPostMessageW
#define RegisterWindowMessageW TestRegisterWindowMessageW
#include "../../SDBootstrapper/Focus/Focus.cpp"
#undef IsWindow
#undef IsWindowVisible
#undef IsWindowEnabled
#undef IsIconic
#undef GetCurrentThreadId
#undef GetWindowThreadProcessId
#undef GetForegroundWindow
#undef SetForegroundWindow
#undef GetWindowLongPtrW
#undef SetWindowPos
#undef PostMessageW
#undef RegisterWindowMessageW

class TestEngine final : public IBootstrapperEngine
{
public:
    ULONG references = 1;
    int logs = 0;
    bool rejectLog = false;
    wchar_t lastLog[512]{};
    wchar_t foregroundLog[512]{};
    HRESULT WINAPI QueryInterface(REFIID, void**) noexcept override { return E_NOINTERFACE; }
    ULONG WINAPI AddRef() noexcept override { return ++references; }
    ULONG WINAPI Release() noexcept override { return --references; }
    HRESULT WINAPI Log(BOOTSTRAPPER_LOG_LEVEL, LPCWSTR message) noexcept override
    {
        ++logs; std::wcsncpy(lastLog, message, 511);
        if (std::wcsstr(message, L"UI-thread foreground restore")) std::wcsncpy(foregroundLog, message, 511);
        return rejectLog ? E_FAIL : S_OK;
    }
#define UNUSED_ENGINE_METHOD(name, ...) HRESULT WINAPI name(__VA_ARGS__) noexcept override { return E_NOTIMPL; }
    UNUSED_ENGINE_METHOD(GetPackageCount, DWORD*)
    UNUSED_ENGINE_METHOD(GetVariableNumeric, LPCWSTR, LONGLONG*)
    UNUSED_ENGINE_METHOD(GetVariableString, LPCWSTR, LPWSTR, SIZE_T*)
    UNUSED_ENGINE_METHOD(GetVariableVersion, LPCWSTR, LPWSTR, SIZE_T*)
    UNUSED_ENGINE_METHOD(FormatString, LPCWSTR, LPWSTR, SIZE_T*)
    UNUSED_ENGINE_METHOD(EscapeString, LPCWSTR, LPWSTR, SIZE_T*)
    UNUSED_ENGINE_METHOD(EvaluateCondition, LPCWSTR, BOOL*)
    UNUSED_ENGINE_METHOD(SendEmbeddedError, DWORD, LPCWSTR, DWORD, int*)
    UNUSED_ENGINE_METHOD(SendEmbeddedProgress, DWORD, DWORD, int*)
    UNUSED_ENGINE_METHOD(SetUpdate, LPCWSTR, LPCWSTR, DWORD64, BOOTSTRAPPER_UPDATE_HASH_TYPE, LPCWSTR, LPCWSTR)
    UNUSED_ENGINE_METHOD(SetLocalSource, LPCWSTR, LPCWSTR, LPCWSTR)
    UNUSED_ENGINE_METHOD(SetDownloadSource, LPCWSTR, LPCWSTR, LPCWSTR, LPCWSTR, LPCWSTR, LPCWSTR)
    UNUSED_ENGINE_METHOD(SetVariableNumeric, LPCWSTR, LONGLONG)
    UNUSED_ENGINE_METHOD(SetVariableString, LPCWSTR, LPCWSTR, BOOL)
    UNUSED_ENGINE_METHOD(SetVariableVersion, LPCWSTR, LPCWSTR)
    UNUSED_ENGINE_METHOD(CloseSplashScreen)
    UNUSED_ENGINE_METHOD(Detect, HWND)
    UNUSED_ENGINE_METHOD(Plan, BOOTSTRAPPER_ACTION, BOOTSTRAPPER_SCOPE)
    UNUSED_ENGINE_METHOD(Elevate, HWND)
    UNUSED_ENGINE_METHOD(Apply, HWND)
    UNUSED_ENGINE_METHOD(Quit, DWORD)
    UNUSED_ENGINE_METHOD(LaunchApprovedExe, HWND, LPCWSTR, LPCWSTR, DWORD)
    UNUSED_ENGINE_METHOD(SetUpdateSource, LPCWSTR, LPCWSTR)
    UNUSED_ENGINE_METHOD(CompareVersions, LPCWSTR, LPCWSTR, int*)
    UNUSED_ENGINE_METHOD(GetRelatedBundleVariable, LPCWSTR, LPCWSTR, LPWSTR, SIZE_T*)
#undef UNUSED_ENGINE_METHOD
};

static int checks;
static bool Check(bool passed, const char* description)
{
    std::printf("%s %s\n", passed ? "PASS" : "FAIL", description);
    if (passed) ++checks;
    return passed;
}
#define REQUIRE(condition, description) do { if (!Check((condition), description)) return false; } while (false)
using CreateFunction = HRESULT (WINAPI*)(const SetupFocusCreateArgs*, SetupFocusCreateResults*);
using DestroyFunction = void (WINAPI*)(const SetupFocusDestroyArgs*, SetupFocusDestroyResults*);

// Independent published WiX 7.0.1 loader values: not the engine/package version.
constexpr DWORD64 Wix701BAFunctionsApiVersion = 0x07e8000100010000ULL;
constexpr int Wix701ThemeLoaded = 1024, Wix701WindowProc = 1025;
constexpr LRESULT ResultSentinel = 0xabcdef01;
// BAFunctions.h's published x64 WndProc layout, including alignment padding.
static_assert(offsetof(SetupFocusWindowProcArgs, hWnd) == 8 && offsetof(SetupFocusWindowProcArgs, uMsg) == 16
    && offsetof(SetupFocusWindowProcArgs, wParam) == 24 && offsetof(SetupFocusWindowProcArgs, lParam) == 32);
static_assert(offsetof(SetupFocusWindowProcResults, fProcessed) == 4 && offsetof(SetupFocusWindowProcResults, lResult) == 8);

static SetupFocusCreateArgs CreateArgs(TestEngine& engine, BOOTSTRAPPER_COMMAND& command)
{
    return {sizeof(SetupFocusCreateArgs), Wix701BAFunctionsApiVersion, &engine, &command};
}
static SetupFocusWindowProcArgs ProcArgs() { return {sizeof(SetupFocusWindowProcArgs), testWindow, TestRestoreMessage, 0, 0}; }
static SetupFocusWindowProcResults ProcResults() { return {sizeof(SetupFocusWindowProcResults), FALSE, ResultSentinel}; }
struct Fixture
{
    TestEngine engine;
    BOOTSTRAPPER_COMMAND command{};
    SetupFocusCreateArgs args;
    SetupFocusCreateResults result{};
    HRESULT created;
    DWORD burnResult = 0xfedcba98;
    Fixture(BOOTSTRAPPER_DISPLAY display = BOOTSTRAPPER_DISPLAY_FULL) : args(CreateArgs(engine, command))
    {
        command.cbSize = sizeof(command); command.display = display; result.cbSize = sizeof(result);
        created = BAFunctionsCreate(&args, &result);
    }
    ~Fixture() { BAFunctionsDestroy(nullptr, nullptr); }
    HRESULT Event(int message, const void* args, void* results = nullptr)
    {
        return result.pfnBAFunctionsProc(message, args, results, result.pvBAFunctionsProcContext);
    }
    HRESULT Theme(HWND window = testWindow)
    {
        SetupFocusThemeLoadedArgs theme{sizeof(theme), window}; return Event(Wix701ThemeLoaded, &theme);
    }
    HRESULT Complete(HRESULT status = S_OK, DWORD version = WIX_5_BOOTSTRAPPER_APPLICATION_API_VERSION)
    {
        BA_ONELEVATECOMPLETE_ARGS elevation{version, status};
        return Event(BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATECOMPLETE, &elevation, &burnResult);
    }
    HRESULT Dispatch(SetupFocusWindowProcResults& output)
    {
        auto proc = ProcArgs(); currentThread = WindowThread; return Event(Wix701WindowProc, &proc, &output);
    }
};

static bool CreationChecks()
{
    ResetMocks(); TestEngine engine; BOOTSTRAPPER_COMMAND command{};
    command.cbSize = sizeof(command); command.display = BOOTSTRAPPER_DISPLAY_FULL;
    auto args = CreateArgs(engine, command); SetupFocusCreateResults result{}; result.cbSize = sizeof(result);
    REQUIRE(BAFunctionsCreate(nullptr, &result) == E_INVALIDARG && BAFunctionsCreate(&args, nullptr) == E_INVALIDARG,
        "null create arguments are rejected without acquiring an engine reference");
    auto shortArgs = args; --shortArgs.cbSize;
    auto shortResults = result; --shortResults.cbSize;
    REQUIRE(BAFunctionsCreate(&shortArgs, &result) == E_INVALIDARG && BAFunctionsCreate(&args, &shortResults) == E_INVALIDARG
        && engine.references == 1, "undersized create arguments and results are rejected");
    auto noCommand = args; noCommand.pCommand = nullptr;
    --command.cbSize;
    REQUIRE(BAFunctionsCreate(&noCommand, &result) == E_INVALIDARG && BAFunctionsCreate(&args, &result) == E_INVALIDARG
        && engine.references == 1, "missing and undersized bootstrapper commands are rejected");
    ++command.cbSize;
    auto wrongVersion = args; ++wrongVersion.qwBAFunctionsAPIVersion;
    REQUIRE(BAFunctionsCreate(&wrongVersion, &result) == HRESULT_FROM_WIN32(ERROR_REVISION_MISMATCH) && engine.references == 1,
        "a different BAFunctions interface version is rejected without leaking a reference");
    wrongVersion.qwBAFunctionsAPIVersion = WIX_7_BOOTSTRAPPER_APPLICATION_API_VERSION;
    REQUIRE(BAFunctionsCreate(&wrongVersion, &result) == HRESULT_FROM_WIN32(ERROR_REVISION_MISMATCH) && engine.references == 1,
        "the WiX 7 core API number is not accepted as the BAFunctions interface version");
    REQUIRE(BAFunctionsCreate(&args, &result) == S_OK && result.pfnBAFunctionsProc && result.pvBAFunctionsProcContext && engine.references == 2,
        "the helper accepts the exact published loader version and holds one engine reference");
    SetupFocusCreateResults duplicate{}; duplicate.cbSize = sizeof(duplicate);
    REQUIRE(BAFunctionsCreate(&args, &duplicate) == E_UNEXPECTED && engine.references == 2,
        "a duplicate create does not replace the live callback or leak a reference");
    BAFunctionsDestroy(nullptr, nullptr); BAFunctionsDestroy(nullptr, nullptr);
    REQUIRE(engine.references == 1, "repeated destruction releases the engine exactly once");
    args.pEngine = nullptr;
    REQUIRE(BAFunctionsCreate(&args, &result) == S_OK, "an optional absent log engine does not prevent creation");
    BAFunctionsDestroy(nullptr, nullptr); return true;
}

static bool HandoverChecks()
{
    ResetMocks(); Fixture fixture;
    REQUIRE(fixture.created == S_OK && registrationCalls == 1 && std::wcscmp(registeredName, SetupFocusMessageName) == 0,
        "creation registers the private handover message once");
    BA_ONELEVATECOMPLETE_ARGS elevation{WIX_5_BOOTSTRAPPER_APPLICATION_API_VERSION, S_OK};
    REQUIRE(fixture.Event(BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATECOMPLETE, nullptr) == S_OK
        && fixture.result.pfnBAFunctionsProc(BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATECOMPLETE, &elevation, nullptr, nullptr) == S_OK
        && postCalls == 0 && foregroundCalls == 0, "missing callback arguments or context have no window side effect");
    fixture.Complete();
    REQUIRE(postCalls == 0 && foregroundCalls == 0, "elevation before a theme window is available does not queue a handover");
    SetupFocusThemeLoadedArgs shortTheme{sizeof(SetupFocusThemeLoadedArgs) - 1, testWindow};
    fixture.Event(Wix701ThemeLoaded, &shortTheme); fixture.Complete();
    REQUIRE(postCalls == 0, "an undersized theme callback cannot supply a window handle");
    fixture.Theme(); currentThread = WindowThread; auto output = ProcResults(); fixture.Dispatch(output);
    REQUIRE(foregroundCalls == 0, "an unsolicited registered message cannot activate a window without pending elevation");
    currentThread = CallbackThread;
    REQUIRE(fixture.Complete() == S_OK && postCalls == 1 && foregroundCalls == 0 && postedWindow == testWindow
        && postedMessage == TestRestoreMessage && postedWParam == 0 && postedLParam == 0 && fixture.burnResult == 0xfedcba98,
        "successful elevation queues the exact message and zero payload without activation on the callback thread");
    fixture.Complete();
    fixture.Event(BOOTSTRAPPER_APPLICATION_MESSAGE_ONPROGRESS, &elevation);
    REQUIRE(postCalls == 1 && foregroundCalls == 0, "duplicate pending elevation and ordinary progress never queue another request");
    output = ProcResults();
    REQUIRE(fixture.Dispatch(output) == S_OK && output.fProcessed == TRUE && output.lResult == 0
        && foregroundCalls == 1 && requestedWindow == testWindow && foregroundThread == WindowThread && positionCount == 0,
        "the published WndProc event consumes the pending request and activates only on the owning UI thread");
    REQUIRE(std::wcsstr(fixture.engine.lastLog, L"accepted") && std::wcsstr(fixture.engine.lastLog, L"active=1"),
        "the UI-thread log distinguishes the API outcome and observed foreground window");
    output = ProcResults(); fixture.Dispatch(output);
    REQUIRE(foregroundCalls == 1 && positionCount == 0,
        "confirmed foreground success needs no visibility fallback and duplicate messages do not activate again");
    return true;
}

static bool InvalidDispatchChecks()
{
    const char* cases[]{"short WndProc arguments", "short WndProc results", "null WndProc arguments", "null WndProc results",
        "null WndProc context", "unrelated BAFunctions event", "unrelated Windows message", "different target window",
        "nonzero wParam", "nonzero lParam", "wrong dispatch thread"};
    for (int index = 0; index < 11; ++index)
    {
        ResetMocks(); Fixture fixture; REQUIRE(fixture.created == S_OK, "an invalid-dispatch fixture creates successfully");
        fixture.Theme(); fixture.Complete(); auto proc = ProcArgs(); auto output = ProcResults();
        int message = Wix701WindowProc; const void* input = &proc; void* results = &output; void* context = fixture.result.pvBAFunctionsProcContext;
        currentThread = WindowThread;
        switch (index)
        {
        case 0: --proc.cbSize; break; case 1: --output.cbSize; break;
        case 2: input = nullptr; break; case 3: results = nullptr; break; case 4: context = nullptr; break;
        case 5: ++message; break; case 6: proc.uMsg = WM_APP + 20; break; case 7: proc.hWnd = otherWindow; break;
        case 8: proc.wParam = 1; break; case 9: proc.lParam = 1; break; case 10: currentThread = CallbackThread; break;
        }
        REQUIRE(fixture.result.pfnBAFunctionsProc(message, input, results, context) == S_OK
            && foregroundCalls == 0 && positionCount == 0 && postCalls == 1
            && output.fProcessed == FALSE && output.lResult == ResultSentinel, cases[index]);
        output = ProcResults(); fixture.Dispatch(output);
        REQUIRE(foregroundCalls == 1 && output.fProcessed == TRUE,
            "an ignored dispatch preserves the pending request for the legitimate owner-thread message");
    }
    return true;
}

static void DisqualifyWindow(int condition)
{
    if (condition == 0) enabledWindow = false;
    if (condition == 1) visibleWindow = false;
    if (condition == 2) minimizedWindow = true;
    if (condition == 3) validWindow = false;
}
static bool EligibilityChecks()
{
    const char* conditions[]{"disabled window", "hidden window", "minimized window", "destroyed window"};
    for (int condition = 0; condition < 4; ++condition)
    {
        ResetMocks(); Fixture fixture; REQUIRE(fixture.created == S_OK, "a window-state fixture creates successfully"); fixture.Theme();
        DisqualifyWindow(condition); fixture.Complete();
        REQUIRE(postCalls == 0 && foregroundCalls == 0, conditions[condition]);
        validWindow = visibleWindow = enabledWindow = true; minimizedWindow = false;
        fixture.Complete(); REQUIRE(postCalls == 1, "an eligible window queues one handover");
        DisqualifyWindow(condition); auto output = ProcResults(); fixture.Dispatch(output);
        REQUIRE(foregroundCalls == 0 && positionCount == 0,
            "a window becoming ineligible between queue and dispatch is not activated or raised");
        validWindow = visibleWindow = enabledWindow = true; minimizedWindow = false;
        output = ProcResults(); fixture.Dispatch(output);
        REQUIRE(foregroundCalls == 0, "a discarded request cannot activate later when the window becomes eligible again");
    }
    return true;
}

static bool FailureAndInvalidationChecks()
{
    for (int condition = 0; condition < 4; ++condition)
    {
        ResetMocks(); Fixture fixture; REQUIRE(fixture.created == S_OK, "an invalidation fixture creates successfully");
        fixture.Theme(); fixture.Complete();
        if (condition == 0) fixture.Complete(HRESULT_FROM_WIN32(ERROR_CANCELLED));
        if (condition == 1) fixture.Complete(E_FAIL);
        if (condition == 2) { BA_ONELEVATEBEGIN_ARGS begin{WIX_5_BOOTSTRAPPER_APPLICATION_API_VERSION}; fixture.Event(BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATEBEGIN, &begin); }
        if (condition == 3) fixture.Theme();
        auto output = ProcResults(); fixture.Dispatch(output);
        REQUIRE(postCalls == 1 && foregroundCalls == 0,
            "cancelled/failed elevation, a new elevation or a replacement theme invalidates the queued request");
    }
    {
        ResetMocks(); Fixture fixture; REQUIRE(fixture.created == S_OK, "a failed-elevation fixture creates successfully"); fixture.Theme();
        fixture.Complete(E_FAIL); fixture.Complete(HRESULT_FROM_WIN32(ERROR_CANCELLED));
        fixture.Complete(S_OK, WIX_7_BOOTSTRAPPER_APPLICATION_API_VERSION);
        REQUIRE(postCalls == 0 && foregroundCalls == 0 && fixture.burnResult == 0xfedcba98,
            "failed/cancelled elevation and the incorrect core-version-7 event never queue activation or change Burn results");
    }
    {
        ResetMocks(); registeredMessage = 0; Fixture fixture;
        REQUIRE(fixture.created == S_OK && std::wcsstr(fixture.engine.lastLog, L"could not register"),
            "message-registration failure is logged without failing helper creation");
        fixture.Theme(); fixture.Complete(); auto output = ProcResults(); fixture.Dispatch(output);
        REQUIRE(postCalls == 0 && foregroundCalls == 0 && output.fProcessed == FALSE && output.lResult == ResultSentinel,
            "a failed registration disables the handover without consuming unrelated messages");
    }
    {
        ResetMocks(); allowPost = FALSE; Fixture fixture; REQUIRE(fixture.created == S_OK, "a queue-failure fixture creates successfully");
        fixture.Theme(); REQUIRE(fixture.Complete() == S_OK && postCalls == 1 && std::wcsstr(fixture.engine.lastLog, L"could not queue"),
            "posting failure is logged without failing the elevation callback");
        auto output = ProcResults(); fixture.Dispatch(output);
        REQUIRE(foregroundCalls == 0, "a failed post leaves no activatable pending request");
        allowPost = TRUE; currentThread = CallbackThread; fixture.Complete(); output = ProcResults(); fixture.Dispatch(output);
        REQUIRE(postCalls == 2 && foregroundCalls == 1, "a later elevation can queue after a previous posting failure");
    }
    {
        ResetMocks(); allowForeground = FALSE; Fixture fixture; REQUIRE(fixture.created == S_OK, "a declined-focus fixture creates successfully");
        fixture.engine.rejectLog = true; fixture.Theme(); fixture.Complete(); auto output = ProcResults();
        REQUIRE(fixture.Dispatch(output) == S_OK && foregroundCalls == 1 && output.fProcessed == TRUE && output.lResult == 0
            && fixture.burnResult == 0xfedcba98 && foregroundWindow == otherWindow,
            "Windows declining foreground and log failure do not fail callbacks or change Burn results");
        REQUIRE(std::wcsstr(fixture.engine.foregroundLog, L"declined by Windows") && std::wcsstr(fixture.engine.foregroundLog, L"active=0"),
            "posting is not mistaken for foreground success; the dispatch logs a declined request and actual foreground state");
        output = ProcResults(); fixture.Dispatch(output);
        REQUIRE(foregroundCalls == 1, "a declined request is not repeatedly retried by duplicate messages");
    }
    return true;
}

static bool PositionSequence(int expectedCount)
{
    constexpr UINT expectedFlags = SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER;
    if (positionCount != expectedCount) return false;
    for (int index = 0; index < expectedCount; ++index)
    {
        const auto& call = positionCalls[index];
        if (call.window != testWindow || call.insertAfter != (index == 0 ? HWND_TOPMOST : HWND_NOTOPMOST)
            || call.x != 0 || call.y != 0 || call.width != 0 || call.height != 0
            || call.flags != expectedFlags || call.thread != WindowThread) return false;
    }
    return true;
}

static bool VisibilityFallbackChecks()
{
    for (int reportedSuccess = 0; reportedSuccess < 2; ++reportedSuccess)
    {
        ResetMocks(); allowForeground = reportedSuccess; foregroundActuallyChanges = false;
        Fixture fixture; REQUIRE(fixture.created == S_OK, "a visibility-fallback fixture creates successfully");
        fixture.Theme(); fixture.Complete();
        REQUIRE(positionCount == 0 && foregroundCalls == 0, "the elevation callback never raises or activates a desktop window directly");
        auto output = ProcResults(); fixture.Dispatch(output);
        REQUIRE(PositionSequence(2) && foregroundCalls == 1 && output.fProcessed == TRUE && output.lResult == 0,
            "declined or ineffective foreground requests use TOPMOST then NOTOPMOST once, on the UI thread, with exact non-activating flags");
        REQUIRE(extendedStyle == WS_EX_APPWINDOW && foregroundWindow == otherWindow && fixture.burnResult == 0xfedcba98
            && std::wcsstr(fixture.engine.lastLog, L"raised=1 normal=1"),
            "successful visibility cleanup restores the original non-topmost style without changing keyboard foreground or Burn results");
        BA_ONPROGRESS_ARGS progress{WIX_5_BOOTSTRAPPER_APPLICATION_API_VERSION, 10, 10};
        fixture.Event(BOOTSTRAPPER_APPLICATION_MESSAGE_ONPROGRESS, &progress);
        output = ProcResults(); fixture.Dispatch(output);
        REQUIRE(positionCount == 2 && foregroundCalls == 1,
            "ordinary progress and duplicate messages do not repeat the visibility fallback or foreground request");
    }
    {
        ResetMocks(); allowForeground = FALSE; allowRaise = FALSE;
        Fixture fixture; REQUIRE(fixture.created == S_OK, "a failed-raise fixture creates successfully");
        fixture.Theme(); fixture.Complete(); auto output = ProcResults(); fixture.Dispatch(output);
        REQUIRE(PositionSequence(2) && extendedStyle == WS_EX_APPWINDOW
            && std::wcsstr(fixture.engine.lastLog, L"raised=0 normal=1"),
            "a failed raise still performs immediate NOTOPMOST cleanup and logs its failure separately from normal final state");
    }
    for (int failedDemotions = 1; failedDemotions <= 2; ++failedDemotions)
    {
        ResetMocks(); allowForeground = FALSE; demotionFailures = failedDemotions;
        Fixture fixture; REQUIRE(fixture.created == S_OK, "a cleanup-failure fixture creates successfully");
        fixture.Theme(); fixture.Complete(); auto output = ProcResults(); fixture.Dispatch(output);
        REQUIRE(PositionSequence(3) && foregroundCalls == 1,
            "a remaining topmost style triggers exactly one additional NOTOPMOST cleanup with the same safe flags");
        bool normal = (extendedStyle & WS_EX_TOPMOST) == 0;
        REQUIRE(normal == (failedDemotions == 1)
            && std::wcsstr(fixture.engine.lastLog, normal ? L"raised=1 normal=1" : L"raised=1 normal=0"),
            "cleanup success and persistent native cleanup failure are reported honestly; normal state is not assumed");
        output = ProcResults(); fixture.Dispatch(output);
        REQUIRE(positionCount == 3 && foregroundCalls == 1,
            "persistent cleanup failure does not start an unbounded retry or activate repeatedly");
    }
    {
        ResetMocks(); allowForeground = FALSE; extendedStyle |= WS_EX_TOPMOST;
        Fixture fixture; REQUIRE(fixture.created == S_OK, "a pre-existing-topmost fixture creates successfully");
        fixture.Theme(); fixture.Complete(); auto output = ProcResults(); fixture.Dispatch(output);
        REQUIRE(positionCount == 0 && extendedStyle == (WS_EX_APPWINDOW | WS_EX_TOPMOST) && foregroundCalls == 1,
            "the fallback never changes a window that was already topmost");
    }
    for (int condition = 1; condition <= 4; ++condition)
    {
        ResetMocks(); allowForeground = FALSE; windowChangeDuringForeground = condition;
        Fixture fixture; REQUIRE(fixture.created == S_OK, "a changed-window fallback fixture creates successfully");
        fixture.Theme(); fixture.Complete(); auto output = ProcResults(); fixture.Dispatch(output);
        REQUIRE(foregroundCalls == 1 && positionCount == 0,
            "hidden, disabled, minimized or destroyed windows are rechecked after the foreground request and never raised");
    }
    {
        ResetMocks(); allowForeground = FALSE; destroyDuringRaise = true;
        Fixture fixture; REQUIRE(fixture.created == S_OK, "a destroyed-during-raise fixture creates successfully");
        fixture.Theme(); fixture.Complete(); auto output = ProcResults(); fixture.Dispatch(output);
        REQUIRE(PositionSequence(2) && !validWindow && foregroundCalls == 1
            && std::wcsstr(fixture.engine.lastLog, L"raised=1 normal=1"),
            "destruction during the raise still attempts immediate cleanup and never schedules another activation");
    }
    return true;
}

static bool ModeAndLifetimeChecks()
{
    const BOOTSTRAPPER_DISPLAY modes[]{BOOTSTRAPPER_DISPLAY_NONE, BOOTSTRAPPER_DISPLAY_EMBEDDED, BOOTSTRAPPER_DISPLAY_PASSIVE, BOOTSTRAPPER_DISPLAY_FULL};
    for (auto display : modes)
    {
        ResetMocks(); Fixture fixture(display); REQUIRE(fixture.created == S_OK, "a display-mode fixture creates successfully");
        fixture.Complete(); REQUIRE(postCalls == 0, "a new context never retains a previous theme window or pending message");
        fixture.Theme(); fixture.Complete(); auto output = ProcResults(); fixture.Dispatch(output);
        int expected = display >= BOOTSTRAPPER_DISPLAY_PASSIVE ? 1 : 0;
        REQUIRE(postCalls == expected && foregroundCalls == expected && positionCount == 0,
            "silent/embedded modes stay silent; passive/full modes use the UI-thread handover without an unnecessary raise");
        BAFunctionsDestroy(nullptr, nullptr); BAFunctionsDestroy(nullptr, nullptr);
        REQUIRE(fixture.engine.references == 1, "each context releases its engine once, including repeated destruction");
    }
    return true;
}

static bool BinaryChecks(const wchar_t* path)
{
    HMODULE module = LoadLibraryW(path); REQUIRE(module != nullptr, "the actual x64 native DLL loads successfully");
    CreateFunction create{}; auto createAddress = GetProcAddress(module, "BAFunctionsCreate");
    static_assert(sizeof(create) == sizeof(createAddress)); std::memcpy(&create, &createAddress, sizeof(create));
    DestroyFunction destroy{}; auto destroyAddress = GetProcAddress(module, "BAFunctionsDestroy");
    static_assert(sizeof(destroy) == sizeof(destroyAddress)); std::memcpy(&destroy, &destroyAddress, sizeof(destroy));
    REQUIRE(create && destroy, "the actual DLL exports both names loaded by WixStdBA");
    TestEngine engine; BOOTSTRAPPER_COMMAND command{}; command.cbSize = sizeof(command); command.display = BOOTSTRAPPER_DISPLAY_FULL;
    auto args = CreateArgs(engine, command); SetupFocusCreateResults result{}; result.cbSize = sizeof(result);
    auto wrongVersion = args; wrongVersion.qwBAFunctionsAPIVersion = WIX_7_BOOTSTRAPPER_APPLICATION_API_VERSION;
    REQUIRE(create(&wrongVersion, &result) == HRESULT_FROM_WIN32(ERROR_REVISION_MISMATCH) && engine.references == 1,
        "the actual DLL distinguishes the BAFunctions contract from the WiX 7 core API number");
    REQUIRE(create(&args, &result) == S_OK && result.pfnBAFunctionsProc && result.pvBAFunctionsProcContext && engine.references == 2,
        "the compiled DLL accepts the exact WiX 7.0.1 loader version and owns one engine reference");
    // Invalid HWNDs are deliberate. No real post or foreground operation can be
    // triggered; RegisterWindowMessage only obtains the same message identifier.
    SetupFocusThemeLoadedArgs theme{sizeof(theme), nullptr}; BA_ONELEVATECOMPLETE_ARGS elevation{WIX_5_BOOTSTRAPPER_APPLICATION_API_VERSION, S_OK};
    DWORD untouched = 0xfedcba98; auto callback = result.pfnBAFunctionsProc; auto context = result.pvBAFunctionsProcContext;
    REQUIRE(callback(Wix701ThemeLoaded, &theme, &untouched, context) == S_OK
        && callback(BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATECOMPLETE, &elevation, &untouched, context) == S_OK
        && untouched == 0xfedcba98, "the actual callback accepts native WiX events without changing Burn results");
    UINT nativeMessage = RegisterWindowMessageW(SetupFocusMessageName);
    REQUIRE(nativeMessage != 0, "the actual registered message can be resolved without creating a desktop window");
    SetupFocusWindowProcArgs proc{sizeof(proc), nullptr, nativeMessage, 0, 0}; auto output = ProcResults();
    REQUIRE(callback(Wix701WindowProc, &proc, &output, context) == S_OK && output.fProcessed == TRUE && output.lResult == 0,
        "the actual DLL accepts the published 40-byte/16-byte WndProc ABI without a pending activation");
    --proc.cbSize; output = ProcResults();
    REQUIRE(callback(Wix701WindowProc, &proc, &output, context) == S_OK && output.fProcessed == FALSE && output.lResult == ResultSentinel,
        "the actual DLL rejects an undersized WndProc argument without changing results");
    ++proc.cbSize; --output.cbSize;
    REQUIRE(callback(Wix701WindowProc, &proc, &output, context) == S_OK && output.fProcessed == FALSE && output.lResult == ResultSentinel,
        "the actual DLL rejects an undersized WndProc result without changing it");
    destroy(nullptr, nullptr);
    REQUIRE(engine.references == 1 && FreeLibrary(module), "the actual DLL releases its engine and unloads cleanly");
    return true;
}

int wmain(int argc, wchar_t** argv)
{
    if (argc != 2) { std::fprintf(stderr, "Usage: InstallerFocusTests.exe <StatsDirect.Setup.Focus.dll>\n"); return 2; }
    if (!CreationChecks() || !HandoverChecks() || !InvalidDispatchChecks() || !EligibilityChecks()
        || !FailureAndInvalidationChecks() || !VisibilityFallbackChecks()
        || !ModeAndLifetimeChecks() || !BinaryChecks(argv[1])) return 1;
    std::printf("PASS %d installer-focus checks; no windows activated and no installer launched.\n", checks);
    return 0;
}
