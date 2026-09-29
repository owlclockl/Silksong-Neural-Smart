#!/usr/bin/env bash
# =============================================================================
#  SILKSONG NEURAL SMART — сборка мода (Linux / macOS)
#  Аналог build.bat: собирает .dll, пакует мод и по желанию ставит его в игру.
#
#  Примеры:
#     ./build.sh                       собрать в dist/
#     ./build.sh --deploy              собрать и установить в игру
#     ./build.sh -g ~/Games/Silksong -d
#     ./build.sh --il2cpp              сборка под BepInEx 6 / IL2CPP
#     ./build.sh --clean --debug
# =============================================================================
set -u

PROJECT="SilksongNeuralSmart.csproj"
MODNAME="SilksongNeuralSmart"
CONFIG="Release"
TFM="net472"
RUNTIME_LABEL="BepInEx 5 / Mono"
DO_DEPLOY=0
DO_CLEAN=0
DO_ZIP=1
GAMEDIR="${SILKSONG_PATH:-}"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$ROOT" || exit 1

C_OK=$'\033[92m'; C_ERR=$'\033[91m'; C_INF=$'\033[96m'; C_WARN=$'\033[93m'; C_OFF=$'\033[0m'
ok()   { echo "${C_OK}$*${C_OFF}"; }
err()  { echo "${C_ERR}$*${C_OFF}"; }
inf()  { echo "${C_INF}$*${C_OFF}"; }
warn() { echo "${C_WARN}$*${C_OFF}"; }

show_help() {
    cat <<'EOF'
SILKSONG NEURAL SMART — сборка мода

  ./build.sh [ключи]

  --deploy, -d            установить собранный мод в BepInEx/plugins игры
  --game PATH, -g PATH    путь к папке игры Hollow Knight Silksong
  --il2cpp                собрать под BepInEx 6 / IL2CPP  [netstandard2.1]
  --debug                 конфигурация Debug вместо Release
  --clean                 очистить bin, obj, dist перед сборкой
  --no-zip                не создавать zip-архив
  --help, -h              эта справка

Путь к игре ищется так: --game -> $SILKSONG_PATH -> silksong.path.txt ->
типовые пути Steam (~/.steam, ~/.local/share/Steam, ~/Library/Application Support/Steam).
EOF
}

while [ $# -gt 0 ]; do
    case "$1" in
        --help|-h)   show_help; exit 0 ;;
        --deploy|-d) DO_DEPLOY=1; shift ;;
        --il2cpp)    TFM="netstandard2.1"; RUNTIME_LABEL="BepInEx 6 / IL2CPP"; shift ;;
        --clean)     DO_CLEAN=1; shift ;;
        --debug)     CONFIG="Debug"; shift ;;
        --no-zip)    DO_ZIP=0; shift ;;
        --no-pause)  shift ;;
        --game|-g)   GAMEDIR="${2:-}"; shift 2 ;;
        *)           err "Неизвестный ключ: $1"; show_help; exit 1 ;;
    esac
done

echo
echo "==============================================================="
echo "   SILKSONG NEURAL SMART — СБОРКА МОДА"
echo "   Конфигурация: $CONFIG   Платформа: $TFM   ($RUNTIME_LABEL)"
echo "==============================================================="
echo

# ------------------------------------------------------------ проверка dotnet
if ! command -v dotnet >/dev/null 2>&1; then
    err "[ОШИБКА] Не найден .NET SDK."
    echo "          Установите .NET SDK 6.0+ : https://dotnet.microsoft.com/download"
    exit 1
fi
ok "[OK] .NET SDK $(dotnet --version)"

# -------------------------------------------------------- поиск папки с игрой
if [ -z "$GAMEDIR" ] && [ -f "silksong.path.txt" ]; then
    GAMEDIR="$(head -n 1 silksong.path.txt | tr -d '\r')"
fi

if [ -z "$GAMEDIR" ]; then
    for candidate in \
        "$HOME/.steam/steam/steamapps/common/Hollow Knight Silksong" \
        "$HOME/.local/share/Steam/steamapps/common/Hollow Knight Silksong" \
        "$HOME/.steam/debian-installation/steamapps/common/Hollow Knight Silksong" \
        "$HOME/Library/Application Support/Steam/steamapps/common/Hollow Knight Silksong" \
        "/run/media/mmcblk0p1/steamapps/common/Hollow Knight Silksong"
    do
        [ -d "$candidate" ] && GAMEDIR="$candidate" && break
    done
fi

# библиотеки Steam из libraryfolders.vdf
if [ -z "$GAMEDIR" ]; then
    for vdf in "$HOME/.steam/steam/steamapps/libraryfolders.vdf" \
               "$HOME/.local/share/Steam/steamapps/libraryfolders.vdf" \
               "$HOME/Library/Application Support/Steam/steamapps/libraryfolders.vdf"
    do
        [ -f "$vdf" ] || continue
        while IFS= read -r lib; do
            [ -d "$lib/steamapps/common/Hollow Knight Silksong" ] && \
                GAMEDIR="$lib/steamapps/common/Hollow Knight Silksong" && break
        done < <(grep -oE '"path"[[:space:]]+"[^"]+"' "$vdf" | sed -E 's/.*"path"[[:space:]]+"([^"]+)".*/\1/')
        [ -n "$GAMEDIR" ] && break
    done
fi

MSBUILD_ARGS=()
if [ -n "$GAMEDIR" ] && [ -d "$GAMEDIR" ]; then
    ok "[OK] Игра найдена: $GAMEDIR"
    MSBUILD_ARGS+=("-p:SilksongPath=$GAMEDIR")
else
    warn "[!] Папка игры не найдена — сборка против NuGet-пакетов."
    echo "    Укажите путь вручную: ./build.sh --game \"/path/to/Hollow Knight Silksong\""
    GAMEDIR=""
fi

# ---------------------------------------------------------------- очистка
if [ "$DO_CLEAN" = "1" ]; then
    inf "[1/5] Очистка bin, obj, dist..."
    rm -rf bin obj dist
else
    inf "[1/5] Очистка пропущена (ключ --clean)"
fi

# ---------------------------------------------------------------- restore
inf "[2/5] Восстановление пакетов NuGet..."
if ! dotnet restore "$PROJECT" -p:TargetFramework="$TFM" "${MSBUILD_ARGS[@]+"${MSBUILD_ARGS[@]}"}" --nologo; then
    err "[ОШИБКА] Не удалось восстановить пакеты (проверьте интернет и NuGet.config)."
    exit 1
fi
ok "[OK] Пакеты восстановлены"

# ------------------------------------------------------------------ build
inf "[3/5] Компиляция $MODNAME.dll ($CONFIG, $TFM)..."
if ! dotnet build "$PROJECT" -c "$CONFIG" -p:TargetFramework="$TFM" \
        "${MSBUILD_ARGS[@]+"${MSBUILD_ARGS[@]}"}" --nologo --no-restore; then
    err "[ОШИБКА] Компиляция завершилась с ошибками."
    exit 1
fi

OUTDLL="bin/$CONFIG/$TFM/$MODNAME.dll"
[ -f "$OUTDLL" ] || OUTDLL="bin/$CONFIG/$MODNAME.dll"
if [ ! -f "$OUTDLL" ]; then
    err "[ОШИБКА] Не найден собранный файл $MODNAME.dll"
    exit 1
fi
ok "[OK] Собрано: $OUTDLL"

# ------------------------------------------------------------- пакет мода
inf "[4/5] Сборка структуры мода в dist/ ..."
PKG="dist/BepInEx/plugins/$MODNAME"
rm -rf dist
mkdir -p "$PKG/PretrainedBrains"
cp "$OUTDLL" "$PKG/"
[ -f "bin/$CONFIG/$TFM/$MODNAME.pdb" ] && cp "bin/$CONFIG/$TFM/$MODNAME.pdb" "$PKG/"
cp PretrainedBrains/*.json "$PKG/PretrainedBrains/" 2>/dev/null
cp README.md MANUAL_RU.md "$PKG/" 2>/dev/null
ok "[OK] Мод собран в dist/"

# -------------------------------------------------------------------- zip
if [ "$DO_ZIP" = "1" ]; then
    ZIPNAME="$MODNAME-$TFM-$CONFIG.zip"
    inf "[5/5] Упаковка $ZIPNAME ..."
    if command -v zip >/dev/null 2>&1; then
        (cd dist && zip -qr "../dist/$ZIPNAME" BepInEx) && ok "[OK] Архив: dist/$ZIPNAME"
    else
        warn "[!] Утилита zip не найдена — архив пропущен."
    fi
else
    inf "[5/5] Архив пропущен (ключ --no-zip)"
fi

# ----------------------------------------------------------------- deploy
if [ "$DO_DEPLOY" = "1" ]; then
    if [ -z "$GAMEDIR" ]; then
        err "[ОШИБКА] Папка игры неизвестна — установка невозможна."
        echo "          Укажите её: ./build.sh --deploy --game \"/path/to/Hollow Knight Silksong\""
        exit 1
    fi
    TARGET="$GAMEDIR/BepInEx/plugins/$MODNAME"
    inf "Установка мода в $TARGET ..."
    mkdir -p "$TARGET"
    cp -r "$PKG/." "$TARGET/"
    ok "[OK] Мод установлен. Запустите игру и нажмите F6 (Великая Арена) или F7 (додзё)."
else
    echo "Установка пропущена. Для автоустановки запустите: ./build.sh --deploy"
fi

echo
echo "==============================================================="
ok  "   ГОТОВО: $MODNAME $CONFIG / $TFM"
echo "   Папка результата: dist/"
echo "==============================================================="
echo
