// Copyright (c) 2026 StatsDirect Limited. MIT licence.
#include "../../SDBootstrapper/Focus/FocusApi.h"
#include <cstdio>
#include <cwchar>
#include <cstring>

// Send real BAFunctions events through the production source without moving any
// real desktop window. The actual DLL's ABI is checked separately below.
static HWND testWindow = reinterpret_cast<HWND>(0x1234);
static bool validWindow = true, visibleWindow = true, minimizedWindow = false;
static BOOL allowForeground = TRUE;
static int foregroundCalls;
static HWND requestedWindow;
static BOOL WINAPI TestIsWindow(HWND window) { return validWindow && window == testWindow; }
static BOOL WINAPI TestIsWindowVisible(HWND window) { return visibleWindow && window == testWindow; }
static BOOL WINAPI TestIsIconic(HWND window) { return minimizedWindow && window == testWindow; }
static BOOL WINAPI TestSetForegroundWindow(HWND window)
{
    ++foregroundCalls; requestedWindow = window; return allowForeground;
}
#define IsWindow TestIsWindow
#define IsWindowVisible TestIsWindowVisible
#define IsIconic TestIsIconic
#define SetForegroundWindow TestSetForegroundWindow
#include "../../SDBootstrapper/Focus/Focus.cpp"
#undef IsWindow
#undef IsWindowVisible
#undef IsIconic
#undef SetForegroundWindow

class TestEngine final : public IBootstrapperEngine
{
public:
    ULONG references = 1;
    int logs = 0;
    bool rejectLog = false;
    wchar_t lastLog[256]{};
    HRESULT WINAPI QueryInterface(REFIID, void**) noexcept override { return E_NOINTERFACE; }
    ULONG WINAPI AddRef() noexcept override { return ++references; }
    ULONG WINAPI Release() noexcept override { return --references; }
    HRESULT WINAPI Log(BOOTSTRAPPER_LOG_LEVEL, LPCWSTR message) noexcept override
    {
        ++logs; std::wcsncpy(lastLog, message, 255); return rejectLog ? E_FAIL : S_OK;
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

// WixStdBA v7.0.1 LoadBAFunctions supplies MAKEQWORDVERSION(2024, 1, 1, 0).
// Keep this independent of the helper's constant so both the source and actual
// DLL checks catch a guard accidentally changed to the WiX package/core version.
constexpr DWORD64 Wix701BAFunctionsApiVersion = 0x07e8000100010000ULL;

static SetupFocusCreateArgs CreateArgs(TestEngine& engine, BOOTSTRAPPER_COMMAND& command)
{
    return {sizeof(SetupFocusCreateArgs), Wix701BAFunctionsApiVersion, &engine, &command};
}

static bool PolicyChecks()
{
    TestEngine engine;
    BOOTSTRAPPER_COMMAND command{};
    command.cbSize = sizeof(command); command.display = BOOTSTRAPPER_DISPLAY_FULL;
    auto args = CreateArgs(engine, command);
    SetupFocusCreateResults result{}; result.cbSize = sizeof(result);
    REQUIRE(BAFunctionsCreate(nullptr, &result) == E_INVALIDARG && BAFunctionsCreate(&args, nullptr) == E_INVALIDARG,
        "null create arguments are rejected without acquiring an engine reference");
    auto shortArgs = args; shortArgs.cbSize--;
    REQUIRE(BAFunctionsCreate(&shortArgs, &result) == E_INVALIDARG && engine.references == 1,
        "an undersized create structure is rejected");
    auto wrongVersion = args; ++wrongVersion.qwBAFunctionsAPIVersion;
    REQUIRE(BAFunctionsCreate(&wrongVersion, &result) == HRESULT_FROM_WIN32(ERROR_REVISION_MISMATCH) && engine.references == 1,
        "a different BAFunctions interface version is rejected without leaking a reference");
    wrongVersion.qwBAFunctionsAPIVersion = WIX_7_BOOTSTRAPPER_APPLICATION_API_VERSION;
    REQUIRE(BAFunctionsCreate(&wrongVersion, &result) == HRESULT_FROM_WIN32(ERROR_REVISION_MISMATCH) && engine.references == 1,
        "the WiX 7 core API number is not accepted as the BAFunctions interface version");
    REQUIRE(BAFunctionsCreate(&args, &result) == S_OK && result.pfnBAFunctionsProc && result.pvBAFunctionsProcContext && engine.references == 2,
        "the helper creates its callback and holds the engine until destruction");
    SetupFocusCreateResults duplicate{}; duplicate.cbSize = sizeof(duplicate);
    REQUIRE(BAFunctionsCreate(&args, &duplicate) == E_UNEXPECTED && engine.references == 2,
        "a duplicate create does not replace the live context or leak an engine reference");
    auto callback = result.pfnBAFunctionsProc;
    auto context = result.pvBAFunctionsProcContext;
    BA_ONELEVATECOMPLETE_ARGS elevation{WIX_5_BOOTSTRAPPER_APPLICATION_API_VERSION, S_OK};
    DWORD untouched = 0xabcdef01;
    REQUIRE(callback(BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATECOMPLETE, nullptr, &untouched, context) == S_OK
        && callback(BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATECOMPLETE, &elevation, &untouched, nullptr) == S_OK && foregroundCalls == 0,
        "an absent callback argument or context has no foreground side effect");
    REQUIRE(callback(BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATECOMPLETE, &elevation, &untouched, context) == S_OK && foregroundCalls == 0,
        "elevation before a theme window is available requests no activation");
    SetupFocusThemeLoadedArgs theme{sizeof(theme), testWindow};
    auto shortTheme = theme; --shortTheme.cbSize;
    callback(SetupFocusThemeLoaded, &shortTheme, &untouched, context);
    callback(BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATECOMPLETE, &elevation, &untouched, context);
    REQUIRE(foregroundCalls == 0, "an undersized theme callback cannot supply a window handle");
    callback(SetupFocusThemeLoaded, &theme, &untouched, context);
    REQUIRE(callback(BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATECOMPLETE, &elevation, &untouched, context) == S_OK
        && foregroundCalls == 1 && requestedWindow == testWindow && untouched == 0xabcdef01,
        "successful elevation requests the captured visible window once and leaves Burn results unchanged");
    REQUIRE(std::wcsstr(engine.lastLog, L"accepted") != nullptr, "the native helper records the foreground outcome");
    int previous = foregroundCalls;
    callback(BOOTSTRAPPER_APPLICATION_MESSAGE_ONPROGRESS, &elevation, &untouched, context);
    callback(BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATEBEGIN, &elevation, &untouched, context);
    REQUIRE(foregroundCalls == previous, "ordinary progress and elevation-begin events never request activation");
    elevation.hrStatus = HRESULT_FROM_WIN32(ERROR_CANCELLED);
    callback(BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATECOMPLETE, &elevation, &untouched, context);
    elevation.hrStatus = E_FAIL;
    callback(BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATECOMPLETE, &elevation, &untouched, context);
    REQUIRE(foregroundCalls == previous, "cancelled and failed elevation leave focus alone");
    elevation.hrStatus = S_OK;
    visibleWindow = false;
    callback(BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATECOMPLETE, &elevation, &untouched, context);
    visibleWindow = true; minimizedWindow = true;
    callback(BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATECOMPLETE, &elevation, &untouched, context);
    minimizedWindow = false; validWindow = false;
    callback(BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATECOMPLETE, &elevation, &untouched, context);
    REQUIRE(foregroundCalls == previous, "hidden, minimized and destroyed windows are never activated or restored");
    validWindow = true;
    // WiX v7.0.1 BACallbackOnElevateComplete still sends the version-5 structure.
    // Version 7 exists for other events but must not replace this event's guard.
    auto unknownEventVersion = elevation; unknownEventVersion.dwApiVersion = WIX_7_BOOTSTRAPPER_APPLICATION_API_VERSION;
    callback(BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATECOMPLETE, &unknownEventVersion, &untouched, context);
    REQUIRE(foregroundCalls == previous, "the core version-7 marker is rejected for the version-5 elevation event");
    allowForeground = FALSE; engine.rejectLog = true;
    REQUIRE(callback(BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATECOMPLETE, &elevation, &untouched, context) == S_OK
        && foregroundCalls == previous + 1 && untouched == 0xabcdef01,
        "Windows declining focus and a failed log do not fail the installation callback");
    REQUIRE(std::wcsstr(engine.lastLog, L"declined") != nullptr, "a declined foreground request is distinguishable in the log");
    allowForeground = TRUE; engine.rejectLog = false;
    BAFunctionsDestroy(nullptr, nullptr);
    BAFunctionsDestroy(nullptr, nullptr);
    REQUIRE(engine.references == 1, "destruction releases the engine exactly once and tolerates repeated destruction");

    const BOOTSTRAPPER_DISPLAY modes[]{BOOTSTRAPPER_DISPLAY_NONE, BOOTSTRAPPER_DISPLAY_EMBEDDED, BOOTSTRAPPER_DISPLAY_PASSIVE};
    for (auto display : modes)
    {
        command.display = display; result = {}; result.cbSize = sizeof(result);
        REQUIRE(BAFunctionsCreate(&args, &result) == S_OK, "a new display mode receives a fresh callback context");
        previous = foregroundCalls;
        callback = result.pfnBAFunctionsProc; context = result.pvBAFunctionsProcContext;
        callback(BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATECOMPLETE, &elevation, nullptr, context);
        REQUIRE(foregroundCalls == previous, "a fresh context does not retain the previous theme window");
        callback(SetupFocusThemeLoaded, &theme, nullptr, context);
        callback(BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATECOMPLETE, &elevation, nullptr, context);
        REQUIRE(foregroundCalls == previous + (display == BOOTSTRAPPER_DISPLAY_PASSIVE ? 1 : 0),
            "silent and embedded modes stay silent; visible passive installation uses the same handover");
        BAFunctionsDestroy(nullptr, nullptr);
    }
    REQUIRE(engine.references == 1, "all display-mode contexts release their engine references");
    return true;
}

static bool BinaryChecks(const wchar_t* path)
{
    HMODULE module = LoadLibraryW(path);
    REQUIRE(module != nullptr, "the actual x64 native DLL loads successfully");
    CreateFunction create{}; auto createAddress = GetProcAddress(module, "BAFunctionsCreate");
    static_assert(sizeof(create) == sizeof(createAddress)); std::memcpy(&create, &createAddress, sizeof(create));
    DestroyFunction destroy{}; auto destroyAddress = GetProcAddress(module, "BAFunctionsDestroy");
    static_assert(sizeof(destroy) == sizeof(destroyAddress)); std::memcpy(&destroy, &destroyAddress, sizeof(destroy));
    REQUIRE(create && destroy, "the actual DLL exports the two names loaded by WixStdBA");
    TestEngine engine;
    BOOTSTRAPPER_COMMAND command{};
    command.cbSize = sizeof(command); command.display = BOOTSTRAPPER_DISPLAY_FULL;
    auto args = CreateArgs(engine, command);
    SetupFocusCreateResults result{}; result.cbSize = sizeof(result);
    auto wrongVersion = args; wrongVersion.qwBAFunctionsAPIVersion = WIX_7_BOOTSTRAPPER_APPLICATION_API_VERSION;
    REQUIRE(create(&wrongVersion, &result) == HRESULT_FROM_WIN32(ERROR_REVISION_MISMATCH) && engine.references == 1,
        "the actual DLL distinguishes the BAFunctions contract from the WiX 7 core API number");
    REQUIRE(create(&args, &result) == S_OK && result.pfnBAFunctionsProc && result.pvBAFunctionsProcContext && engine.references == 2,
        "the compiled DLL accepts the WiX 7.0.1 loader's exact BAFunctions version and owns one engine reference");
    // An invalid HWND is deliberate: the actual WinAPI is used, but no real
    // desktop window can be activated by this binary smoke check.
    SetupFocusThemeLoadedArgs theme{sizeof(theme), nullptr};
    BA_ONELEVATECOMPLETE_ARGS elevation{WIX_5_BOOTSTRAPPER_APPLICATION_API_VERSION, S_OK};
    DWORD untouched = 0xfedcba98;
    REQUIRE(result.pfnBAFunctionsProc(SetupFocusThemeLoaded, &theme, &untouched, result.pvBAFunctionsProcContext) == S_OK
        && result.pfnBAFunctionsProc(BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATECOMPLETE, &elevation, &untouched, result.pvBAFunctionsProcContext) == S_OK
        && untouched == 0xfedcba98, "the compiled callback accepts native WiX events without mutating Burn results");
    destroy(nullptr, nullptr);
    REQUIRE(engine.references == 1 && FreeLibrary(module), "the actual DLL releases its engine and unloads cleanly");
    return true;
}

int wmain(int argc, wchar_t** argv)
{
    if (argc != 2) { std::fprintf(stderr, "Usage: InstallerFocusTests.exe <StatsDirect.Setup.Focus.dll>\n"); return 2; }
    if (!PolicyChecks() || !BinaryChecks(argv[1])) return 1;
    std::printf("PASS %d installer-focus checks; no windows activated and no installer launched.\n", checks);
    return 0;
}
