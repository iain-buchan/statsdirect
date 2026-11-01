// Copyright (c) 2026 StatsDirect Limited. MIT licence, as the StatsDirect project.
#include "FocusApi.h"
#include <wchar.h>

namespace
{
    struct FocusState
    {
        PVOID volatile window;
        IBootstrapperEngine* engine;
        BOOTSTRAPPER_DISPLAY display;
        UINT restoreMessage;
        LONG volatile restorePending;
    };
    FocusState* active;

    bool Eligible(FocusState* state, HWND window)
    {
        return state->display >= BOOTSTRAPPER_DISPLAY_PASSIVE
            && IsWindow(window) && IsWindowVisible(window) && IsWindowEnabled(window) && !IsIconic(window);
    }

    void Log(FocusState* state, const wchar_t* text)
    {
        if (state->engine) state->engine->Log(BOOTSTRAPPER_LOG_LEVEL_STANDARD, text);
    }

    HRESULT WINAPI OnEvent(int message, const void* args, void* results, void* context)
    {
        auto state = static_cast<FocusState*>(context);
        if (!state || !args) return S_OK;
        if (message == SetupFocusThemeLoaded)
        {
            auto theme = static_cast<const SetupFocusThemeLoadedArgs*>(args);
            if (theme->cbSize >= sizeof(*theme))
            {
                InterlockedExchange(&state->restorePending, 0);
                InterlockedExchangePointer(&state->window, theme->hWnd);
            }
        }
        else if (message == BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATEBEGIN)
        {
            InterlockedExchange(&state->restorePending, 0);
        }
        else if (message == BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATECOMPLETE)
        {
            auto elevation = static_cast<const BA_ONELEVATECOMPLETE_ARGS*>(args);
            // WiX 7 still sends this event with API version 5; the engine's
            // version-7 interfaces do not change this callback contract.
            if (elevation->dwApiVersion != WIX_5_BOOTSTRAPPER_APPLICATION_API_VERSION) return S_OK;
            if (FAILED(elevation->hrStatus))
            {
                InterlockedExchange(&state->restorePending, 0);
                return S_OK;
            }
            HWND window = static_cast<HWND>(InterlockedCompareExchangePointer(&state->window, nullptr, nullptr));
            // This callback is not the window thread. Let the published
            // BAFunctions WndProc hook make the one activation request there.
            if (SUCCEEDED(elevation->hrStatus) && state->restoreMessage && Eligible(state, window)
                && InterlockedCompareExchange(&state->restorePending, 1, 0) == 0)
            {
                wchar_t text[192];
                if (PostMessageW(window, state->restoreMessage, 0, 0))
                {
                    swprintf(text, sizeof(text) / sizeof(text[0]),
                        L"StatsDirect setup: queued foreground handover hwnd=%p callbackTid=%lu uiTid=%lu.",
                        static_cast<void*>(window), GetCurrentThreadId(), GetWindowThreadProcessId(window, nullptr));
                    Log(state, text);
                }
                else
                {
                    InterlockedExchange(&state->restorePending, 0);
                    Log(state, L"StatsDirect setup: could not queue foreground handover.");
                }
            }
        }
        else if (message == SetupFocusWindowProc && results)
        {
            auto proc = static_cast<const SetupFocusWindowProcArgs*>(args);
            auto output = static_cast<SetupFocusWindowProcResults*>(results);
            HWND window = static_cast<HWND>(InterlockedCompareExchangePointer(&state->window, nullptr, nullptr));
            if (proc->cbSize < sizeof(*proc) || output->cbSize < sizeof(*output)
                || !state->restoreMessage || proc->uMsg != state->restoreMessage || proc->hWnd != window
                || proc->wParam != 0 || proc->lParam != 0
                || (IsWindow(window) && GetCurrentThreadId() != GetWindowThreadProcessId(window, nullptr))) return S_OK;
            output->fProcessed = TRUE;
            output->lResult = 0;
            if (InterlockedExchange(&state->restorePending, 0) == 1 && Eligible(state, window)
                && GetCurrentThreadId() == GetWindowThreadProcessId(window, nullptr))
            {
                HWND before = GetForegroundWindow();
                DWORD beforePid = 0, targetPid = 0;
                GetWindowThreadProcessId(before, &beforePid);
                GetWindowThreadProcessId(window, &targetPid);
                BOOL activated = SetForegroundWindow(window);
                HWND after = GetForegroundWindow();
                wchar_t text[320];
                swprintf(text, sizeof(text) / sizeof(text[0]),
                    L"StatsDirect setup: UI-thread foreground restore %ls; target=%p pid=%lu tid=%lu before=%p pid=%lu after=%p active=%u.",
                    activated ? L"accepted" : L"declined by Windows", static_cast<void*>(window),
                    targetPid, GetCurrentThreadId(), static_cast<void*>(before), beforePid, static_cast<void*>(after), after == window ? 1u : 0u);
                Log(state, text);
                // UAC can leave another process with the foreground lock. The
                // user still needs to see installation progress. Raise our own
                // window once without changing keyboard focus, immediately
                // returning it to the normal (non-topmost) window group.
                if ((!activated || after != window) && Eligible(state, window)
                    && !(GetWindowLongPtrW(window, GWL_EXSTYLE) & WS_EX_TOPMOST))
                {
                    const UINT flags = SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER;
                    BOOL raised = SetWindowPos(window, HWND_TOPMOST, 0, 0, 0, 0, flags);
                    SetWindowPos(window, HWND_NOTOPMOST, 0, 0, 0, 0, flags);
                    if (IsWindow(window) && (GetWindowLongPtrW(window, GWL_EXSTYLE) & WS_EX_TOPMOST))
                        SetWindowPos(window, HWND_NOTOPMOST, 0, 0, 0, 0, flags);
                    bool normal = !IsWindow(window) || !(GetWindowLongPtrW(window, GWL_EXSTYLE) & WS_EX_TOPMOST);
                    swprintf(text, sizeof(text) / sizeof(text[0]),
                        L"StatsDirect setup: visibility handover raised=%u normal=%u foreground=%p.",
                        raised ? 1u : 0u, normal ? 1u : 0u, static_cast<void*>(GetForegroundWindow()));
                    Log(state, text);
                }
            }
        }
        // Focus is best effort; it never changes Burn's install/cancel/results.
        return S_OK;
    }
}

extern "C" __declspec(dllexport) HRESULT WINAPI BAFunctionsCreate(const SetupFocusCreateArgs* args, SetupFocusCreateResults* results)
{
    if (!args || !results || args->cbSize < sizeof(*args) || results->cbSize < sizeof(*results)
        || !args->pCommand || args->pCommand->cbSize < sizeof(BOOTSTRAPPER_COMMAND)) return E_INVALIDARG;
    if (args->qwBAFunctionsAPIVersion != SetupFocusApiVersion)
    {
        if (args->pEngine) args->pEngine->Log(BOOTSTRAPPER_LOG_LEVEL_ERROR, L"StatsDirect setup: unsupported WixStdBA BAFunctions interface version.");
        return HRESULT_FROM_WIN32(ERROR_REVISION_MISMATCH);
    }
    if (active) return E_UNEXPECTED;
    auto state = static_cast<FocusState*>(HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(FocusState)));
    if (!state) return E_OUTOFMEMORY;
    state->engine = args->pEngine;
    if (state->engine) state->engine->AddRef();
    state->display = args->pCommand->display;
    state->restoreMessage = RegisterWindowMessageW(SetupFocusMessageName);
    if (!state->restoreMessage) Log(state, L"StatsDirect setup: could not register foreground handover message.");
    active = state;
    results->pfnBAFunctionsProc = OnEvent;
    results->pvBAFunctionsProcContext = state;
    return S_OK;
}

extern "C" __declspec(dllexport) void WINAPI BAFunctionsDestroy(const SetupFocusDestroyArgs*, SetupFocusDestroyResults*)
{
    if (!active) return;
    if (active->engine) active->engine->Release();
    HeapFree(GetProcessHeap(), 0, active);
    active = nullptr;
}
