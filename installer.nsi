!include "MUI2.nsh"

; ── App metadata ─────────────────────────────────────────────────────────────
!define APP_NAME        "AstroPlanner"
!define APP_VERSION     "1.0"
!define APP_PUBLISHER   "tankhardrive"
!define APP_EXE         "AstroPlanner.exe"
!define REG_KEY         "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"

!define PUBLISH_DIR     "AstroPlanner\bin\Release\net10.0\win-x64\publish"
!define ICON            "AstroPlanner\Assets\astroplanner.ico"

; ── Output ───────────────────────────────────────────────────────────────────
Name            "${APP_NAME} ${APP_VERSION}"
OutFile         "AstroPlanner-${APP_VERSION}-Setup.exe"
InstallDir      "$PROGRAMFILES64\${APP_NAME}"
InstallDirRegKey HKLM "Software\${APP_NAME}" "Install_Dir"
RequestExecutionLevel admin
Unicode True

; ── MUI settings ─────────────────────────────────────────────────────────────
!define MUI_ICON                        "${ICON}"
!define MUI_UNICON                      "${ICON}"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN              "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT         "Launch ${APP_NAME}"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

; ── Install ───────────────────────────────────────────────────────────────────
Section "Install"
    SetOutPath "$INSTDIR"

    File "${PUBLISH_DIR}\${APP_EXE}"
    File "${PUBLISH_DIR}\av_libglesv2.dll"
    File "${PUBLISH_DIR}\libHarfBuzzSharp.dll"
    File "${PUBLISH_DIR}\libSkiaSharp.dll"

    ; Desktop shortcut
    CreateShortcut "$DESKTOP\${APP_NAME}.lnk" \
        "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0

    ; Start Menu shortcut
    CreateDirectory "$SMPROGRAMS\${APP_NAME}"
    CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" \
        "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
    CreateShortcut "$SMPROGRAMS\${APP_NAME}\Uninstall.lnk" \
        "$INSTDIR\Uninstall.exe"

    ; Write uninstaller
    WriteUninstaller "$INSTDIR\Uninstall.exe"

    ; Add/Remove Programs entry
    WriteRegStr   HKLM "${REG_KEY}" "DisplayName"      "${APP_NAME}"
    WriteRegStr   HKLM "${REG_KEY}" "DisplayVersion"   "${APP_VERSION}"
    WriteRegStr   HKLM "${REG_KEY}" "Publisher"        "${APP_PUBLISHER}"
    WriteRegStr   HKLM "${REG_KEY}" "UninstallString"  "$INSTDIR\Uninstall.exe"
    WriteRegStr   HKLM "${REG_KEY}" "InstallLocation"  "$INSTDIR"
    WriteRegStr   HKLM "${REG_KEY}" "DisplayIcon"      "$INSTDIR\${APP_EXE}"
    WriteRegDWORD HKLM "${REG_KEY}" "NoModify"         1
    WriteRegDWORD HKLM "${REG_KEY}" "NoRepair"         1
SectionEnd

; ── Uninstall ─────────────────────────────────────────────────────────────────
Section "Uninstall"
    Delete "$INSTDIR\${APP_EXE}"
    Delete "$INSTDIR\av_libglesv2.dll"
    Delete "$INSTDIR\libHarfBuzzSharp.dll"
    Delete "$INSTDIR\libSkiaSharp.dll"
    Delete "$INSTDIR\Uninstall.exe"

    Delete "$DESKTOP\${APP_NAME}.lnk"
    Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
    Delete "$SMPROGRAMS\${APP_NAME}\Uninstall.lnk"
    RMDir  "$SMPROGRAMS\${APP_NAME}"
    RMDir  "$INSTDIR"

    DeleteRegKey HKLM "${REG_KEY}"
    DeleteRegKey HKLM "Software\${APP_NAME}"
SectionEnd
