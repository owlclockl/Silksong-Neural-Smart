@echo off
rem =====================================================================
rem  SILKSONG NEURAL SMART - сборка мода в готовый .dll и пакет BepInEx
rem  Собирает весь исходный код C# (включая режим "Великая Арена")
rem  в один плагин: dist\BepInEx\plugins\SilksongNeuralSmart\
rem
rem  ВАЖНО: этот файл должен быть сохранён с переносами строк CRLF
rem         и в кодировке UTF-8 с BOM, иначе cmd.exe закроется молча.
rem
rem  Использование:
rem    build.bat                     обычная сборка Release
rem    build.bat --deploy            собрать и установить в игру
rem    build.bat --game "D:\Games\Hollow Knight Silksong" --deploy
rem    build.bat --il2cpp            сборка под BepInEx 6 / IL2CPP
rem    build.bat --clean             очистить bin, obj, dist перед сборкой
rem    build.bat --debug             конфигурация Debug
rem    build.bat --no-zip            не создавать zip-архив
rem    build.bat --no-pause          не ждать нажатия клавиши в конце
rem    build.bat --log               писать полный лог в build.log
rem    build.bat --help              справка
rem =====================================================================

setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul 2>&1
title Silksong Neural Smart - сборка мода

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"
cd /d "%ROOT%"

set "PROJECT=SilksongNeuralSmart.csproj"
set "ASSEMBLY=SilksongNeuralSmart"
set "CONFIG=Release"
set "TFM=net472"
set "RUNTIME_LABEL=BepInEx 5 / Unity Mono"
set "DO_DEPLOY=0"
set "DO_ZIP=1"
set "DO_CLEAN=0"
set "DO_PAUSE=1"
set "DO_LOG=0"
set "GAMEDIR="
set "FAILED=0"

rem ---------------------------------------------------------------- args
:parse_args
if "%~1"=="" goto args_done
if /i "%~1"=="--help"     goto show_help
if /i "%~1"=="-h"         goto show_help
if /i "%~1"=="/?"         goto show_help
if /i "%~1"=="--deploy"   set "DO_DEPLOY=1" & shift & goto parse_args
if /i "%~1"=="-d"         set "DO_DEPLOY=1" & shift & goto parse_args
if /i "%~1"=="--il2cpp"   set "TFM=netstandard2.1" & set "RUNTIME_LABEL=BepInEx 6 / IL2CPP" & shift & goto parse_args
if /i "%~1"=="--clean"    set "DO_CLEAN=1" & shift & goto parse_args
if /i "%~1"=="--debug"    set "CONFIG=Debug" & shift & goto parse_args
if /i "%~1"=="--no-zip"   set "DO_ZIP=0" & shift & goto parse_args
if /i "%~1"=="--no-pause" set "DO_PAUSE=0" & shift & goto parse_args
if /i "%~1"=="--log"      set "DO_LOG=1" & shift & goto parse_args
if /i "%~1"=="--game"     set "GAMEDIR=%~2" & shift & shift & goto parse_args
if /i "%~1"=="-g"         set "GAMEDIR=%~2" & shift & shift & goto parse_args
echo [!] Неизвестный параметр: %~1
goto show_help

:args_done

set "LOGFILE=%ROOT%\build.log"
if "%DO_LOG%"=="1" (
    if exist "%LOGFILE%" del /q "%LOGFILE%" >nul 2>&1
)

echo.
echo ==========================================================
echo   SILKSONG NEURAL SMART - СБОРКА МОДА
echo   Нейро-ИИ мобов и Хорнет + режим "ВЕЛИКАЯ АРЕНА"
echo ==========================================================
echo   Конфигурация : %CONFIG%
echo   Платформа    : %TFM%  [%RUNTIME_LABEL%]
echo.

rem ------------------------------------------------------- проверка dotnet
set "DOTNET=dotnet"
where dotnet >nul 2>&1
if errorlevel 1 (
    if exist "%ProgramFiles%\dotnet\dotnet.exe" (
        set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
    ) else (
        if exist "%ProgramW6432%\dotnet\dotnet.exe" (
            set "DOTNET=%ProgramW6432%\dotnet\dotnet.exe"
        ) else (
            echo [ОШИБКА] Не найден .NET SDK.
            echo          Установите .NET SDK 8.0: https://dotnet.microsoft.com/download
            echo          После установки закройте и заново откройте окно.
            set "FAILED=1"
            goto finish
        )
    )
)

set "DOTNET_VER="
for /f "tokens=*" %%V in ('"!DOTNET!" --version 2^>nul') do set "DOTNET_VER=%%V"
if not defined DOTNET_VER (
    echo [ОШИБКА] .NET найден, но "dotnet --version" не отвечает.
    echo          Переустановите .NET SDK: https://dotnet.microsoft.com/download
    set "FAILED=1"
    goto finish
)
echo   .NET SDK     : !DOTNET_VER!

if not exist "%ROOT%\%PROJECT%" (
    echo [ОШИБКА] Не найден файл проекта: %ROOT%\%PROJECT%
    echo          Запускайте build.bat из папки с исходниками мода.
    set "FAILED=1"
    goto finish
)

rem ------------------------------------------------- поиск папки с игрой
if not defined GAMEDIR if defined SILKSONG_PATH set "GAMEDIR=%SILKSONG_PATH%"
if not defined GAMEDIR if exist "%ROOT%\silksong.path.txt" (
    for /f "usebackq delims=" %%P in ("%ROOT%\silksong.path.txt") do if not defined GAMEDIR set "GAMEDIR=%%P"
)
if not defined GAMEDIR call :find_game
if defined GAMEDIR (
    if not exist "!GAMEDIR!\Hollow Knight Silksong_Data" (
        echo   [!] По пути "!GAMEDIR!" игра не найдена, путь игнорируется.
        set "GAMEDIR="
    )
)

if defined GAMEDIR (
    echo   Игра         : !GAMEDIR!
) else (
    echo   Игра         : не найдена ^(сборка по NuGet-пакетам^)
)
echo.

rem ------------------------------------------------------------- очистка
if "%DO_CLEAN%"=="1" (
    echo [1/5] Очистка bin, obj, dist...
    if exist "%ROOT%\bin"  rmdir /s /q "%ROOT%\bin"
    if exist "%ROOT%\obj"  rmdir /s /q "%ROOT%\obj"
    if exist "%ROOT%\dist" rmdir /s /q "%ROOT%\dist"
) else (
    echo [1/5] Очистка пропущена ^(ключ --clean^)
)

rem ----------------------------------------------------------- restore
echo [2/5] Восстановление зависимостей NuGet...
set "MSBUILD_ARGS=-p:TargetFramework=%TFM%"
if defined GAMEDIR set "MSBUILD_ARGS=!MSBUILD_ARGS! "-p:SilksongPath=!GAMEDIR!""

if "%DO_LOG%"=="1" (
    "!DOTNET!" restore "%PROJECT%" !MSBUILD_ARGS! --nologo >>"%LOGFILE%" 2>&1
) else (
    "!DOTNET!" restore "%PROJECT%" !MSBUILD_ARGS! --nologo
)
if errorlevel 1 (
    echo.
    echo [ОШИБКА] Не удалось восстановить пакеты.
    echo          Проверьте интернет-соединение либо укажите путь к игре:
    echo          build.bat --game "D:\Games\Hollow Knight Silksong"
    set "FAILED=1"
    goto finish
)

rem ------------------------------------------------------------- build
echo [3/5] Компиляция плагина...
if "%DO_LOG%"=="1" (
    "!DOTNET!" build "%PROJECT%" -c %CONFIG% !MSBUILD_ARGS! --nologo --no-restore >>"%LOGFILE%" 2>&1
) else (
    "!DOTNET!" build "%PROJECT%" -c %CONFIG% !MSBUILD_ARGS! --nologo --no-restore
)
if errorlevel 1 (
    echo.
    echo [ОШИБКА] Компиляция завершилась с ошибками.
    if "%DO_LOG%"=="1" echo          Полный лог: %LOGFILE%
    if not "%DO_LOG%"=="1" echo          Повторите с ключом --log, чтобы сохранить лог в build.log
    set "FAILED=1"
    goto finish
)

set "OUTDLL=%ROOT%\bin\%CONFIG%\%TFM%\%ASSEMBLY%.dll"
if not exist "!OUTDLL!" set "OUTDLL=%ROOT%\bin\%CONFIG%\%ASSEMBLY%.dll"
if not exist "!OUTDLL!" (
    echo [ОШИБКА] Собранный файл %ASSEMBLY%.dll не найден.
    set "FAILED=1"
    goto finish
)

rem ------------------------------------------------------- пакет мода
echo [4/5] Сборка пакета мода...
set "PKGROOT=%ROOT%\dist\BepInEx\plugins\%ASSEMBLY%"
if not exist "!PKGROOT!" mkdir "!PKGROOT!" >nul 2>&1
if not exist "!PKGROOT!\PretrainedBrains" mkdir "!PKGROOT!\PretrainedBrains" >nul 2>&1

copy /y "!OUTDLL!" "!PKGROOT!\%ASSEMBLY%.dll" >nul
if exist "%ROOT%\bin\%CONFIG%\%TFM%\%ASSEMBLY%.pdb" copy /y "%ROOT%\bin\%CONFIG%\%TFM%\%ASSEMBLY%.pdb" "!PKGROOT!\" >nul
if exist "%ROOT%\PretrainedBrains\*.json" copy /y "%ROOT%\PretrainedBrains\*.json" "!PKGROOT!\PretrainedBrains\" >nul
if exist "%ROOT%\README.md"    copy /y "%ROOT%\README.md"    "!PKGROOT!\" >nul
if exist "%ROOT%\MANUAL_RU.md" copy /y "%ROOT%\MANUAL_RU.md" "!PKGROOT!\" >nul

set "DLLSIZE=0"
for %%F in ("!PKGROOT!\%ASSEMBLY%.dll") do set "DLLSIZE=%%~zF"
echo       Плагин   : !PKGROOT!\%ASSEMBLY%.dll  [!DLLSIZE! байт]

rem ---------------------------------------------------------------- zip
if "%DO_ZIP%"=="1" (
    set "ZIPFILE=%ROOT%\dist\%ASSEMBLY%-%TFM%-%CONFIG%.zip"
    if exist "!ZIPFILE!" del /q "!ZIPFILE!"
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%ROOT%\dist\BepInEx' -DestinationPath '!ZIPFILE!' -Force" >nul 2>&1
    if exist "!ZIPFILE!" (
        echo       Архив    : !ZIPFILE!
    ) else (
        echo       [!] Не удалось создать zip ^(PowerShell недоступен^), папка dist готова.
    )
)

rem ------------------------------------------------------------ deploy
echo [5/5] Установка в игру...
if "%DO_DEPLOY%"=="0" (
    echo       Пропущено. Для автоустановки запустите: build.bat --deploy
    goto finish
)

if not defined GAMEDIR (
    echo       [!] Папка игры не найдена — установка невозможна.
    echo           Укажите её: build.bat --deploy --game "D:\Games\Hollow Knight Silksong"
    goto finish
)

if not exist "!GAMEDIR!\BepInEx" (
    echo       [!] В папке игры нет BepInEx. Установите BepInEx 5 и запустите игру один раз.
    goto finish
)

set "TARGET=!GAMEDIR!\BepInEx\plugins\%ASSEMBLY%"
if not exist "!TARGET!" mkdir "!TARGET!" >nul 2>&1
xcopy /y /e /i /q "!PKGROOT!\*" "!TARGET!\" >nul
if errorlevel 1 (
    echo       [!] Копирование не удалось. Закройте игру и повторите.
) else (
    echo       Установлено: !TARGET!
)

:finish
echo.
if "%FAILED%"=="1" (
    echo ==========================================================
    echo   СБОРКА НЕ УДАЛАСЬ. Смотрите сообщения выше.
    echo ==========================================================
) else (
    echo ==========================================================
    echo   ГОТОВО! Мод собран.
    echo.
    echo   Установка вручную:
    echo     1. Скопируйте папку dist\BepInEx\plugins\%ASSEMBLY%
    echo        в ^<Silksong^>\BepInEx\plugins\
    echo     2. Запустите игру.
    echo     3. В ГЛАВНОМ МЕНЮ нажмите кнопку "НЕЙРО-АРЕНА:
    echo        ВЕЛИКАЯ ТРЕНИРОВКА" либо клавишу F6.
    echo.
    echo   Сохранения режима: BepInEx\config\SilksongNeuralSmart\GrandArena\
    echo ==========================================================
)
echo.
if "%DO_PAUSE%"=="1" pause
if "%FAILED%"=="1" exit /b 1
exit /b 0

rem =====================================================================
rem  Поиск установленной игры: реестр Steam, библиотеки Steam, типовые пути
rem =====================================================================
:find_game
set "STEAMROOT="
for /f "tokens=2,*" %%A in ('reg query "HKCU\Software\Valve\Steam" /v SteamPath 2^>nul ^| findstr /i "SteamPath"') do set "STEAMROOT=%%B"
if defined STEAMROOT set "STEAMROOT=!STEAMROOT:/=\!"

if defined STEAMROOT (
    if exist "!STEAMROOT!\steamapps\common\Hollow Knight Silksong\Hollow Knight Silksong_Data" (
        set "GAMEDIR=!STEAMROOT!\steamapps\common\Hollow Knight Silksong"
        goto :eof
    )
    set "VDF=!STEAMROOT!\steamapps\libraryfolders.vdf"
    if exist "!VDF!" call :scan_vdf "!VDF!"
    if defined GAMEDIR goto :eof
)

for %%D in (C D E F G) do (
    for %%L in ("Program Files (x86)\Steam" "Program Files\Steam" "Steam" "SteamLibrary" "Games\Steam" "Games\SteamLibrary") do (
        if exist "%%D:\%%~L\steamapps\common\Hollow Knight Silksong\Hollow Knight Silksong_Data" (
            if not defined GAMEDIR set "GAMEDIR=%%D:\%%~L\steamapps\common\Hollow Knight Silksong"
        )
    )
    if exist "%%D:\Games\Hollow Knight Silksong\Hollow Knight Silksong_Data" (
        if not defined GAMEDIR set "GAMEDIR=%%D:\Games\Hollow Knight Silksong"
    )
)
goto :eof

rem Разбор libraryfolders.vdf: строки вида   "path"   "D:\\SteamLibrary"
:scan_vdf
for /f usebackq^ tokens^=4^ delims^=^" %%L in (`findstr /i /c:"\"path\"" "%~1"`) do (
    set "LIB=%%L"
    set "LIB=!LIB:\\=\!"
    if exist "!LIB!\steamapps\common\Hollow Knight Silksong\Hollow Knight Silksong_Data" (
        if not defined GAMEDIR set "GAMEDIR=!LIB!\steamapps\common\Hollow Knight Silksong"
    )
)
goto :eof

rem =====================================================================
:show_help
echo.
echo SILKSONG NEURAL SMART - сборка мода
echo.
echo   build.bat [параметры]
echo.
echo   --deploy, -d            установить собранный мод в BepInEx\plugins игры
echo   --game PATH, -g PATH    путь к папке игры Hollow Knight Silksong
echo   --il2cpp                собрать под BepInEx 6 / IL2CPP  [netstandard2.1]
echo   --debug                 конфигурация Debug вместо Release
echo   --clean                 очистить bin, obj, dist перед сборкой
echo   --no-zip                не создавать zip-архив
echo   --no-pause              не ждать нажатия клавиши в конце
echo   --log                   писать полный вывод сборки в build.log
echo   --help, -h              эта справка
echo.
echo   Путь к игре можно также задать переменной SILKSONG_PATH
echo   или файлом silksong.path.txt рядом с build.bat.
echo.
if "%DO_PAUSE%"=="1" pause
endlocal
exit /b 0
