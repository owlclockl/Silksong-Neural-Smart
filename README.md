# RosaryShare

A standalone companion mod for **Silksong Multiplayer Mod (XvX)** that lets players
**send rosaries (beads) to each other** inside a Steam lobby.

> 🇷🇺 Подробная русская инструкция — в [MANUAL_RU.md](MANUAL_RU.md).

It is a **separate plugin**: it does not patch or modify the multiplayer mod.
RosaryShare rides the same Steam lobby and reuses the already-established P2P
session, but speaks its own protocol over the free **Steam P2P channel 1**
(the multiplayer mod uses channel 0 — they never collide).

## Features

- 🎁 Send any amount of beads to anyone in your lobby — window on **F7**.
- ⚡ Quick-send a fixed amount (default 100) with **G**, no window needed.
- 🛡 **No lost beads**: the recipient acknowledges every transfer. Player left the
  lobby, is out of game, disabled receiving, or never answers — the sender is
  automatically refunded.
- 📜 In-window history of all transfers (sent / delivered / received / refunded).
- 🌍 Russian & English UI (auto-detected or forced in config).
- 🔢 Amount limits and a send cooldown against typos and double-clicks.

## Requirements

1. Hollow Knight: Silksong (Steam version).
2. BepInEx 5 (x64).
3. **Silksong Multiplayer Mod (XvX)** — the one with Create Lobby / Invite Players
   buttons (tested against 0.11.4 and 0.12.x).
4. Every player exchanging beads needs RosaryShare installed.

## Installation

1. Copy `RosaryShare.dll` into `<Silksong>\BepInEx\plugins\`
   (next to `SilksongMultiplayer.dll` or into its own `RosaryShare\` subfolder).
2. Launch the game.

Or build & auto-install: `build.bat --deploy`.

## Usage

1. Create or join a lobby via the multiplayer mod menu as usual.
2. Load a save file.
3. Press **F7**: pick a player (Steam names), choose an amount
   (100 / 500 / 1000 / 5000 / All or a custom value), press **Send**.
4. The recipient gets beads on their account instantly plus a toast;
   the sender gets a delivery confirmation.

If a transfer cannot be delivered (player left, timed out, declined, network
error), the amount is refunded automatically — with a notification.

## Configuration

`BepInEx/config/com.silksong.rosaryshare.cfg`:

| Setting | Default | Description |
|---|---|---|
| Language | `Auto` | `Auto` / `English` / `Russian`. |
| Show Toasts | `true` | Toast notifications about transfers. |
| Menu Key | `F7` | Open/close the sharing window. |
| Quick Send Key | `G` | Quick-send hotkey. |
| Quick Send Amount | `100` | Amount sent by the quick-send key. |
| Allow Receive | `true` | `false` auto-declines incoming transfers (sender is refunded). |
| Max Send Amount | `100000` | Per-transfer outgoing limit. |
| Max Receive Amount | `1000000` | Per-transfer incoming limit. |
| Send Cooldown Seconds | `1.5` | Delay between sends. |
| Ack Timeout Seconds | `6` | Delivery wait before auto-refund. |

## How it works

- Finds the multiplayer mod's `LobbyManager(Clone)` object and reads `currentRoomID`
  off its `RoomManager` component via reflection — no compile-time dependency on
  the XvX mod and no patches.
- Lobby members and names come straight from Steam (`SteamMatchmaking`,
  `SteamFriends`).
- Packets (`RSWP1`: transfer / ack / reject / hello) travel over Steam P2P
  **channel 1** with reliable delivery.
- Beads are `PlayerData.geo`; credit/debit goes through the game's own
  `CurrencyManager.AddGeo/TakeGeo`, so wallet caps and the HUD counter animation
  behave exactly like vanilla.

Only Steam is supported (same as the multiplayer mod).

## Build

Requires **.NET SDK 8.0+**.

- Windows: `build.bat` (see `build.bat --help`) → `dist\`
- Linux/macOS: `./build.sh` → `dist/`

If the game is found (Steam default paths, `--game PATH`, `SILKSONG_PATH`, or
`silksong.path.txt`), compilation uses the real game assemblies offline.
Otherwise it falls back to NuGet packages plus the reference stubs in `refs/`
(matching signatures of `Assembly-CSharp` and `com.rlabrecque.steamworks.net`;
stubs are never shipped inside the mod).

GitHub Actions also builds every push and uploads the ready `RosaryShare.dll`
as an artifact.

## Credits

- Team Cherry — for Hollow Knight: Silksong.
- XvX (233XvX233) — for Silksong Multiplayer Mod.
- nek5s — SilklessCoop/SilklessLib, inspiration for the architecture.
- BepInEx, rlabrecque (Steamworks.NET).
