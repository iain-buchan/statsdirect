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
    };
    FocusState* active;

    HRESULT WINAPI OnEvent(int message, const void* args, void*, void* context)
    {
        auto state = static_cast<FocusState*>(context);
        if (!state || !args) return S_OK;
        if (message == SetupFocusThemeLoaded)
        {
            auto theme = static_cast<const SetupFocusThemeLoadedArgs*>(args);
            if (theme->cbSize >= sizeof(*theme)) InterlockedExchangePointer(&state->window, theme->hWnd);
        }
        else if (message == BOOTSTRAPPER_APPLICATION_MESSAGE_ONELEVATECOMPLETE)
        {
            auto elevation = static_cast<const BA_ONELEVATECOMPLETE_ARGS*>(args);
            // WiX 7 still sends this event with API version 5; the engine's
            // version-7 interfaces do not change this callback contract.
            if (elevation->dwApiVersion != WIX_5_BOOTSTRAPPER_APPLICATION_API_VERSION) return S_OK;
            HWND window = static_cast<HWND>(InterlockedCompareExchangePointer(&state->window, nullptr, nullptr));
            // Restore the user's installation window once after successful UAC.
            // This is the policy of upstream WiX issue 9317 / PR 728, adapted to
            // the supported WixStdBA callback. Never unminimise or make topmost.
            if (SUCCEEDED(elevation->hrStatus) && state->display >= BOOTSTRAPPER_DISPLAY_PASSIVE
                && IsWindow(window) && IsWindowVisible(window) && !IsIconic(window))
            {
                BOOL activated = SetForegroundWindow(window);
                wchar_t text[192];
                swprintf(text, sizeof(text) / sizeof(text[0]),
                    L"StatsDirect setup: elevation 0x%08lX; foreground restore %ls.",
                    static_cast<unsigned long>(elevation->hrStatus), activated ? L"accepted" : L"declined by Windows");
                if (state->engine) state->engine->Log(BOOTSTRAPPER_LOG_LEVEL_STANDARD, text);
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
