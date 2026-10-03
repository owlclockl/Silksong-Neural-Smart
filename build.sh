#!/usr/bin/env bash
# =============================================================================
#  ROSARYSHARE — сборка мода (Linux / macOS)
#  Аналог build.bat: собирает .dll, пакует мод и по желанию ставит его в игру.
#
#  Примеры:
#     ./build.sh                       собрать в dist/
#     ./build.sh --deploy              собрать и установить в игру
#     ./build.sh -g ~/Games/Silksong -d
#     ./build.sh --clean --debug
# =============================================================================
set -u

PROJECT="RosaryShare.csproj"
MODNAME="RosaryShare"
CONFIG="Release"
TFM="netstandard2.1"
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
ROSARYSHARE — сборка мода

  ./build.sh [ключи]

  --deploy, -d            установить собранный мод в BepInEx/plugins игры
  --game PATH, -g PATH    путь к папке игры Hollow Knight Silksong
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
        --deploy|-d)  DO_DEPLOY=1; shift ;;
        --game|-g)    GAMEDIR="${2:-}"; shift 2 ;;
        --debug)      CONFIG="Debug"; shift ;;
        --clean)      DO_CLEAN=1; shift ;;
        --no-zip)     DO_ZIP=0; shift ;;
        --help|-h)    show_help; exit 0 ;;
        *) err "Неизвестный параметр: $1"; show_help; exit 1 ;;
    esac
done

inf ""
inf "=========================================================="
inf "  ROSARYSHARE — СБОРКА МОДА"
inf "  Передача бусин между игроками (Silksong Multiplayer Mod)"
inf "=========================================================="
echo "  Конфигурация : $CONFIG"
echo "  Платформа    : $TFM  [BepInEx 5 / Unity Mono]"

# ------------------------------------------------------------- dotnet
if ! command -v dotnet >/dev/null 2>&1; then
    err "[ОШИБКА] Не найден .NET SDK."
    err "         Установите .NET SDK 8.0: https://dotnet.microsoft.com/download"
    exit 1
fi
echo "  .NET SDK     : $(dotnet --version 2>/dev/null || echo '?')"

# ------------------------------------------------------------- папка игры
if [ -z "$GAMEDIR" ] && [ -f "$ROOT/silksong.path.txt" ]; then
    GAMEDIR="$(head -n 1 "$ROOT/silksong.path.txt" | tr -d '\r')"
fi

if [ -z "$GAMEDIR" ]; then
    for c in \
        "$HOME/.steam/steam/steamapps/common/Hollow Knight Silksong" \
        "$HOME/.local/share/Steam/steamapps/common/Hollow Knight Silksong" \
        "$HOME/.var/app/com.valvesoftware.Steam/data/Steam/steamapps/common/Hollow Knight Silksong" \
        "$HOME/Library/Application Support/Steam/steamapps/common/Hollow Knight Silksong"
    do
        if [ -d "$c/Hollow Knight Silksong_Data" ]; then GAMEDIR="$c"; break; fi
    done
fi

if [ -n "$GAMEDIR" ] && [ ! -d "$GAMEDIR/Hollow Knight Silksong_Data" ]; then
    warn "  [!] По пути \"$GAMEDIR\" игра не найдена, путь игнорируется."
    GAMEDIR=""
fi

if [ -n "$GAMEDIR" ]; then
    echo "  Игра         : $GAMEDIR"
else
    echo "  Игра         : не найдена (сборка по NuGet и стабам)"
fi
echo ""

# ------------------------------------------------------------- очистка
if [ "$DO_CLEAN" -eq 1 ]; then
    echo "[1/4] Очистка bin, obj, dist..."
    rm -rf "$ROOT/bin" "$ROOT/obj" "$ROOT/dist" \
        "$ROOT/refs/GameStubs/bin" "$ROOT/refs/GameStubs/obj" \
        "$ROOT/refs/SteamworksStubs/bin" "$ROOT/refs/SteamworksStubs/obj"
else
    echo "[1/4] Очистка пропущена (ключ --clean)"
fi

# ------------------------------------------------------------- restore
echo "[2/4] Восстановление зависимостей..."
MSBUILD_ARGS=()
if [ -n "$GAMEDIR" ]; then MSBUILD_ARGS+=("-p:SilksongPath=$GAMEDIR"); fi

if ! dotnet restore "$PROJECT" "${MSBUILD_ARGS[@]}" --nologo; then
    err "[ОШИБКА] Не удалось восстановить пакеты."
    err "         Проверьте интернет либо укажите путь к игре: ./build.sh --game PATH"
    exit 1
fi

# ------------------------------------------------------------- build
echo "[3/4] Компиляция плагина..."
if ! dotnet build "$PROJECT" -c "$CONFIG" "${MSBUILD_ARGS[@]}" --nologo --no-restore; then
    err "[ОШИБКА] Компиляция завершилась с ошибками."
    exit 1
fi

OUTDLL="$ROOT/bin/$CONFIG/$TFM/$MODNAME.dll"
[ -f "$OUTDLL" ] || OUTDLL="$ROOT/bin/$CONFIG/$MODNAME.dll"
if [ ! -f "$OUTDLL" ]; then
    err "[ОШИБКА] Собранный файл $MODNAME.dll не найден."
    exit 1
fi

# ------------------------------------------------------------- пакет
echo "[4/4] Сборка пакета мода..."
PKGROOT="$ROOT/dist/BepInEx/plugins/$MODNAME"
mkdir -p "$PKGROOT"
cp -f "$OUTDLL" "$PKGROOT/$MODNAME.dll"
[ -f "$ROOT/bin/$CONFIG/$TFM/$MODNAME.pdb" ] && cp -f "$ROOT/bin/$CONFIG/$TFM/$MODNAME.pdb" "$PKGROOT/" 2>/dev/null || true
for doc in README.md MANUAL_RU.md INSTALL.txt; do
    [ -f "$ROOT/$doc" ] && cp -f "$ROOT/$doc" "$PKGROOT/" 2>/dev/null || true
done
echo "      Плагин   : $PKGROOT/$MODNAME.dll  [$(stat -c%s "$PKGROOT/$MODNAME.dll" 2>/dev/null || echo '?') байт]"

if [ "$DO_ZIP" -eq 1 ]; then
    ZIPFILE="$ROOT/dist/$MODNAME-$TFM-$CONFIG.zip"
    rm -f "$ZIPFILE"
    if command -v zip >/dev/null 2>&1; then
        (cd "$ROOT/dist" && zip -qr "$ZIPFILE" BepInEx)
        echo "      Архив    : $ZIPFILE"
    else
        warn "      [!] zip не найден, папка dist готова без архива."
    fi
fi

# ------------------------------------------------------------- deploy
if [ "$DO_DEPLOY" -eq 1 ]; then
    if [ -z "$GAMEDIR" ]; then
        warn "      [!] Папка игры не найдена — установка невозможна (--game PATH)."
    elif [ ! -d "$GAMEDIR/BepInEx" ]; then
        warn "      [!] В папке игры нет BepInEx. Установите BepInEx 5 и запустите игру один раз."
    else
        TARGET="$GAMEDIR/BepInEx/plugins/$MODNAME"
        mkdir -p "$TARGET"
        cp -rf "$PKGROOT/." "$TARGET/"
        ok "      Установлено: $TARGET"
    fi
else
    echo "      Установка в игру пропущена (ключ --deploy)."
fi

echo ""
ok "=========================================================="
ok "  ГОТОВО! Мод собран."
echo ""
echo "  Установка вручную:"
echo "    1. Скопируйте папку dist/BepInEx/plugins/$MODNAME"
echo "       в <Silksong>/BepInEx/plugins/"
echo "    2. Запустите игру и войдите в лобби мультиплеерного мода."
echo "    3. В игре нажмите F7 — откроется окно обмена бусинами."
echo ""
echo "  Настройки: BepInEx/config/com.silksong.rosaryshare.cfg"
ok "=========================================================="
