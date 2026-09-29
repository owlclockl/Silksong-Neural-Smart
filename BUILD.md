# 🛠 Сборка Silksong Neural Smart / Building the Mod

Репозиторий содержит исходники BepInEx-мода на C# и интерактивный веб-симулятор обучения.
Собрать **весь мод целиком** можно одной командой.

---

## 🔨 Быстрый старт (Windows) — `build.bat`

```bat
:: 1. Просто собрать мод (результат в dist\)
build.bat

:: 2. Собрать И сразу установить в игру — путь к Steam ищется автоматически
build.bat --deploy

:: 3. Игра в нестандартном месте
build.bat --deploy --game "D:\Games\Hollow Knight Silksong"

:: 4. Сборка под BepInEx 6 IL2CPP
build.bat --il2cpp --deploy

:: 5. Полная пересборка с нуля в конфигурации Debug
build.bat --clean --debug
```

На Linux / macOS — тот же скрипт в виде `./build.sh` с теми же ключами.

### Ключи

| Ключ | Действие |
|---|---|
| `--deploy`, `-d` | Скопировать готовый мод в `<Игра>\BepInEx\plugins\SilksongNeuralSmart\` |
| `--game PATH`, `-g PATH` | Явно указать папку с игрой |
| `--il2cpp` | Целевая платформа `netstandard2.1` (BepInEx 6 IL2CPP) вместо `net472` |
| `--debug` | Конфигурация `Debug` вместо `Release` |
| `--clean` | Удалить `bin`, `obj`, `dist` перед сборкой |
| `--no-zip` | Не создавать `.zip` |
| `--no-pause` | Не ждать нажатия клавиши в конце (для CI) |
| `--help`, `-h` | Справка |

### Что делает скрипт

1. **Проверяет .NET SDK** (`dotnet --version`), при отсутствии — даёт ссылку на установку.
2. **Ищет папку игры** по цепочке:
   `--game` → переменная окружения `SILKSONG_PATH` → файл `silksong.path.txt` рядом со скриптом →
   реестр Steam (`HKCU\Software\Valve\Steam` → `SteamPath`) → все библиотеки из `libraryfolders.vdf` →
   перебор типовых путей на дисках `C:`–`G:`
   (на Linux/macOS — `~/.steam`, `~/.local/share/Steam`, `~/Library/Application Support/Steam`).
3. **Выбирает режим ссылок**:
   - игра найдена → компиляция **напрямую против сборок игры и BepInEx**
     (`Hollow Knight Silksong_Data\Managed\*.dll`, `BepInEx\core\*.dll`) — самый точный вариант;
   - игра не найдена → компиляция против NuGet-пакетов (см. ниже), мод всё равно соберётся.
4. `dotnet restore` → `dotnet build -c Release`.
5. **Собирает структуру мода** и **пакует `.zip`**.
6. При `--deploy` копирует мод в `BepInEx\plugins` игры.

### Результат

```
dist\
├── BepInEx\
│   └── plugins\
│       └── SilksongNeuralSmart\
│           ├── SilksongNeuralSmart.dll
│           ├── SilksongNeuralSmart.pdb          (только Debug)
│           ├── PretrainedBrains\*.brain.json
│           ├── README.md
│           └── MANUAL_RU.md
└── SilksongNeuralSmart-net472-Release.zip       <- распаковывается в корень игры
```

### Чтобы не указывать путь каждый раз

Создайте рядом с `build.bat` файл `silksong.path.txt` с одной строкой:

```
D:\Games\Steam\steamapps\common\Hollow Knight Silksong
```

Либо задайте переменную окружения `SILKSONG_PATH`.

---

## 📦 Требования

- [.NET SDK 6.0 / 7.0 / 8.0](https://dotnet.microsoft.com/download) (только SDK, Visual Studio не нужна).
- *Hollow Knight: Silksong* с установленным
  [BepInEx 5.4.23+ (Mono)](https://github.com/BepInEx/BepInEx/releases) — рекомендуется,
  либо [BepInEx 6 IL2CPP](https://builds.bepinex.dev/projects/bepinex_be).
- Интернет при первой сборке — для восстановления NuGet-пакетов.

> **Важно:** перед установкой мода **один раз запустите игру с BepInEx**, чтобы он создал
> папки `BepInEx\core`, `BepInEx\config`, `BepInEx\plugins`.

---

## 🧩 Ручная сборка через dotnet CLI

```bash
# Сборка против сборок игры (наиболее точная)
dotnet build SilksongNeuralSmart.csproj -c Release \
  -p:SilksongPath="D:\Games\Steam\steamapps\common\Hollow Knight Silksong"

# Сборка без игры (по NuGet-пакетам)
dotnet build SilksongNeuralSmart.csproj -c Release

# Под BepInEx 6 IL2CPP
dotnet build SilksongNeuralSmart.csproj -c Release -p:TargetFramework=netstandard2.1
```

Готовый файл: `bin/Release/net472/SilksongNeuralSmart.dll`
(или `bin/Release/netstandard2.1/...` для IL2CPP).

В начале сборки проект печатает, какой режим выбран:

```
[SilksongNeuralSmart] Сборка против локальных сборок игры: D:\...\Hollow Knight Silksong
[SilksongNeuralSmart] Игра не найдена — сборка против NuGet-пакетов (UnityEngine.Modules + BepInEx).
```

### Используемые пакеты (режим без игры)

| Пакет | Версия | Когда |
|---|---|---|
| `UnityEngine.Modules` | 2021.3.33 | всегда |
| `BepInEx.Core` | 5.4.21 | `net472` (BepInEx 5 / Mono) |
| `BepInEx.Unity.IL2CPP` | 6.0.0-be.725 | `netstandard2.1` (BepInEx 6) |
| `HarmonyX` | 2.14.0 | всегда |
| `Microsoft.NETFramework.ReferenceAssemblies` | 1.0.3 | `net472` без установленного targeting pack |

Пакеты BepInEx живут в собственном фиде — он уже прописан в `NuGet.config` репозитория:

```xml
<add key="BepInEx" value="https://nuget.bepinex.dev/v3/index.json" />
```

---

## 💻 Сборка в IDE

1. Откройте `SilksongNeuralSmart.csproj` в Visual Studio 2022 / JetBrains Rider / VS Code.
2. При необходимости задайте свойство `SilksongPath` (Properties → Build, либо
   переменную окружения `SILKSONG_PATH`), чтобы ссылаться на сборки игры.
3. Конфигурация `Release` → **Build**.
4. Скопируйте `SilksongNeuralSmart.dll` и папку `PretrainedBrains/`
   в `<Игра>/BepInEx/plugins/SilksongNeuralSmart/`.

---

## 🌐 Запуск веб-симулятора (без игры и без .NET)

```bash
node web/server.js
```

Откройте `http://localhost:3000`. Зависимостей нет — `npm install` не требуется.
В симуляторе доступны те же режимы, что и в моде:

- **🏟 Великая Арена** — мобы и Хорнет обучаются одновременно на одной большой арене,
  3 слота сохранений (в `localStorage` браузера), система волн, экспорт мозгов в `.json`;
- **🥋 Классическое додзё 1 на 1** — дуэли Хорнет против одного моба в 3 комнатах.

---

## ❓ Частые проблемы

| Симптом | Решение |
|---|---|
| `dotnet` не найден | Установите .NET SDK и перезапустите терминал |
| `Unable to load the service index for source https://nuget.bepinex.dev/...` | Нет интернета или фид недоступен: соберите с указанием игры (`--game`), тогда пакеты BepInEx не нужны |
| `MSB3644: Reference assemblies for .NETFramework,Version=v4.7.2 were not found` | Восстановите пакеты (`--clean`), пакет `Microsoft.NETFramework.ReferenceAssemblies` подтянется сам |
| Мод собрался, но игра его не видит | Проверьте, что DLL лежит в `BepInEx/plugins/SilksongNeuralSmart/`, а в `BepInEx/LogOutput.log` есть строка загрузки плагина |
| Нет кнопки «Великая Арена» в главном меню | Нажмите **F6**; либо включите `AlwaysShowMainMenuButton = true` в `BepInEx/config/com.silksong.neural.smart.cfg` |
| Ошибки типов Unity при сборке по NuGet | Соберите против сборок игры: `build.bat --game "<путь к игре>"` |
