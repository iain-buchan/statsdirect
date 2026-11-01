// Copyright (c) 2026 StatsDirect Limited. MIT licence, as the StatsDirect project.
#pragma once
#include <windows.h>
#include <unknwn.h>
#include <msi.h>
#include <BootstrapperApplicationTypes.h>
#if defined(__MINGW32__)
// WiX's SDK uses the older SAL spellings; these annotations do not change ABI.
#define __in
#define __in_opt
#define __in_z
#define __in_z_opt
#define __inout
#define __out
#define __out_ecount_opt(n)
#endif
#include <IBootstrapperEngine.h>
#if defined(__MINGW32__)
#undef __in
#undef __in_opt
#undef __in_z
#undef __in_z_opt
#undef __inout
#undef __out
#undef __out_ecount_opt
#endif

// The small part of the published WixStdBA BAFunctions ABI used here. WiX ships
// the core event types above in BootstrapperApplicationApi, but not BAFunctions.h:
// https://github.com/wixtoolset/wix/blob/v7.0.1/src/ext/Bal/wixstdfn/inc/BAFunctions.h
// Keep the interface layout/version tied to the WiX package used by this bundle.
using SetupFocusCallback = HRESULT (WINAPI*)(int, const void*, void*, void*);
constexpr DWORD64 SetupFocusApiVersion = (DWORD64{2024} << 48) | (DWORD64{1} << 32) | (DWORD64{1} << 16);
constexpr int SetupFocusThemeLoaded = 1024;

struct SetupFocusCreateArgs
{
    DWORD cbSize;
    DWORD64 qwBAFunctionsAPIVersion;
    IBootstrapperEngine* pEngine;
    BOOTSTRAPPER_COMMAND* pCommand;
};
struct SetupFocusCreateResults
{
    DWORD cbSize;
    SetupFocusCallback pfnBAFunctionsProc;
    void* pvBAFunctionsProcContext;
};
struct SetupFocusDestroyArgs { DWORD cbSize; BOOL fReload; };
struct SetupFocusDestroyResults { DWORD cbSize; BOOL fDisableUnloading; };
struct SetupFocusThemeLoadedArgs { DWORD cbSize; HWND hWnd; };

static_assert(sizeof(void*) == 8, "This bundle uses the x64 WixStdBA.");
static_assert(sizeof(SetupFocusCreateArgs) == 32 && sizeof(SetupFocusCreateResults) == 24 && sizeof(SetupFocusThemeLoadedArgs) == 16,
    "The x64 BAFunctions interface layout changed.");
