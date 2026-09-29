# Silksong Neural Smart (BepInEx Mod)

<div align="center">

![Version](https://img.shields.io/badge/Version-1.0.0-blue.svg)
![Target](https://img.shields.io/badge/Platform-Hollow%20Knight%3A%20Silksong-red.svg)
![Modloader](https://img.shields.io/badge/ModLoader-BepInEx%205%20%26%206-green.svg)
![Engine](https://img.shields.io/badge/Neural_Engine-Pure%20C%23%20SIMD-orange.svg)
![Latency](https://img.shields.io/badge/Inference_Latency-%3C0.02ms-brightgreen.svg)

**Самописный автономный искусственный интеллект на нейронной сети для всех врагов и Хорнет в Hollow Knight: Silksong с тренировочным додзё и балансировкой.**

[**📖 Полное руководство на русском (MANUAL_RU.md)**](MANUAL_RU.md) | [**🛠 Инструкция по сборке (BUILD.md)**](BUILD.md)

</div>

---

## 🌟 Ключевые возможности (Key Features)

- 🧠 **Собственная сверхбыстрая нейросеть (Pure C# Neural Engine)**:
  - Нулевая зависимость от тяжелых внешних C++ библиотек (PyTorch / ONNX).
  - Инференс занимает **менее 0.02 мс** (свыше 50,000 решений в секунду), гарантируя гладкий геймплей без просадок FPS.
  - Входной вектор из **24 сенсоров** (пространственные лучи, дистанции, скорости, тайминги, хитбоксы, ловушки, статус шелка/выносливости).
  - Выходной вектор из **10 действий** (перемещение, прыжки, рывки, удары, пого-отскоки, парирование, лечение).

- ⚖️ **Интеллектуальная система баланса (Fair-Play Balancing Engine)**:
  - Имитация естественной реакции человека (**110–140 мс**) через кольцевой буфер восприятия.
  - Ограничение выносливости (Stamina budget) и телеграфирование атак противников.
  - Настраиваемый процент тактических ошибок (Mistake margin).
  - Динамическая подстройка сложности (DDA) — бои ощущаются живыми, тактическими и честными!

- 🥋 **Тренировочный режим «Silk Dojo» (The Training Arena)**:
  - Отдельный режим обучения по горячей клавише **F7** с изолированными тренировочными комнатами.
  - **3 уникальные комнаты**:
    1. *Арена Мха (Moss Flat Dojo)* — базовый зонинг, размен ударами и контроль дистанции.
    2. *Вертикальный Разлом (Vertical Chasm with Spikes)* — яма с шипами, обучение Пого-прыжкам и лазанию по стенам.
    3. *Испытание Цитадели (Citadel Trial)* — колонны, узкие проходы и динамические препятствия.

- 👑 **Обучение Хорнет (Autonomous Hornet Agent)**:
  - Режим самообучения (Self-play) для Хорнет с адаптивным расходом катушек шелка, выбором моментов для лечения и воздушными Пого-комбо.
  - Возможность тренировки **AI vs AI** (наблюдение) или **Player vs AI** (игрок лично сражается против обучаемой нейросети).

- 📊 **Визуализация и статистика в реальном времени**:
  - Live HUD с графиком нейросети, весами синапсов, вероятностями действий и кривой эволюции (Fitness Curve).
  - Ускорение симуляции от **1x** до **50x** для быстрой эволюции десятков поколений за минуты.
  - Экспорт и импорт весов в формате `.brain.json`.

- 🌐 **Интерактивный веб-симулятор (Web Live Simulator)**:
  - Полноценная 2D боевая арена с физикой Silksong в браузере на порту `3000` для визуального тестирования ИИ прямо сейчас!

---

## 🏗 Архитектура проекта

```
Silksong-Neural-Smart/
├── src/                               # Исходный код C# BepInEx плагина
│   ├── SilksongNeuralSmartPlugin.cs   # Точка входа BaseUnityPlugin
│   ├── PluginInfo.cs                  # Метаданные плагина
│   ├── Config/
│   │   └── ModConfig.cs               # BepInEx настройки и горячие клавиши
│   ├── Core/
│   │   ├── NeuralNetwork.cs           # Самописная нейросеть (LeakyReLU, Softmax, He Init)
│   │   ├── GeneticTrainer.cs          # Нейроэволюция и генетический алгоритм
│   │   ├── ObservationSensor.cs       # 24-мерный энкодер сенсоров и рейкастов
│   │   ├── ActionDecoders.cs          # Декодер 10 боевых действий Silksong
│   │   ├── BalancingEngine.cs         # Буфер задержки реакции, выносливость и баланс
│   │   └── ExperienceReplay.cs        # Буфер опыта для RL
│   ├── Agents/
│   │   ├── MobAgent.cs                # Базовый класс нейро-врага
│   │   ├── HornetAgent.cs             # Агент обучения Хорнет
│   │   └── Archetypes/
│   │       ├── ScoutGruntAgent.cs     # Разведчик ближнего боя
│   │       ├── FlyingHunterAgent.cs   # Летающий охотник с пике
│   │       ├── ShieldKnightAgent.cs   # Рыцарь с глухим блоком щита
│   │       └── AssassinWeaverAgent.cs # Быстрый ассасин с блинками
│   ├── Training/
│   │   ├── TrainingArenaManager.cs    # Менеджер арены, раундов и скорости
│   │   ├── RoomLayouts.cs             # Геометрия 3 тренировочных комнат
│   │   └── FitnessFunctions.cs        # Награды и штрафы за урон, пого, уклонения
│   ├── Patches/
│   │   ├── EnemyFSMOverridePatch.cs   # Harmony хуки для врагов
│   │   ├── HeroControllerPatch.cs     # Harmony хуки для управления Хорнет
│   │   └── CombatHooks.cs             # Хуки урона, хитбоксов и парирования
│   └── UI/
│       └── TrainingHUD.cs             # Внутриигровой Unity GUI оверлей и визуализатор
├── PretrainedBrains/                  # Предобученные веса нейросетей (.brain.json)
│   ├── hornet_master.brain.json
│   ├── scout_grunt_smart.brain.json
│   ├── flying_hunter_smart.brain.json
│   ├── shield_knight_smart.brain.json
│   └── assassin_weaver_smart.brain.json
├── web/                               # Интерактивный Live Веб-симулятор (Node.js)
│   ├── server.js                      # Сервер симулятора на порту 3000
│   └── public/
│       ├── index.html                 # Интерфейс Silk Dojo
│       ├── styles.css                 # Стилизация в атмосфере Pharloom
│       ├── neural-engine.js           # JS-движок нейросети (100% совместим с C#)
│       ├── arena-physics.js           # 2D боевая физика Silksong
│       ├── visualizer.js              # Отрисовка лучей, графа сети и графиков
│       └── app.js                     # Логика управления и событий
├── MANUAL_RU.md                       # Подробнейшая документация на русском языке
├── BUILD.md                           # Руководство по сборке .dll
└── SilksongNeuralSmart.csproj         # Проект .NET / BepInEx
```

---

## 🎮 Горячие клавиши в игре

| Клавиша | Функция |
|---|---|
| `F7` | Показать / скрыть интерфейс обучения (Neural HUD) |
| `F8` | Переключить автономный ИИ для Хорнет (AI / Player) |
| `F9` | Мгновенно перезапустить текущий раунд дуэли |
| `Page Up` | Увеличить скорость симуляции (1x → 2x → 5x → 10x → 25x → 50x) |
| `Page Down` | Уменьшить скорость симуляции |

---

## 🚀 Установка

1. Скачайте и установите **BepInEx 5** или **BepInEx 6** для *Hollow Knight: Silksong*.
2. Скопируйте файл `SilksongNeuralSmart.dll` в папку `Silksong/BepInEx/plugins/`.
3. (Опционально) Скопируйте папку `PretrainedBrains/` в `Silksong/BepInEx/plugins/SilksongNeuralSmart/`.
4. Запустите игру. Нажмите **F7** для открытия меню нейро-арены!
