# RosaryShare

A standalone companion mod for **Silksong Multiplayer Mod (XvX)** that lets players
**send rosaries (beads) and shell shards to each other** inside a Steam lobby.

> 🇷🇺 Подробная русская инструкция — в [MANUAL_RU.md](MANUAL_RU.md).

It is a **separate plugin**: it does not patch or modify the multiplayer mod.
RosaryShare rides the same Steam lobby and reuses the already-established P2P
session, but speaks its own protocol over the free **Steam P2P channel 1**
(the multiplayer mod uses channel 0 — they never collide).

## Features

- 🎁 Send beads or shell shards to anyone in your lobby — an **Sharing** tab inside the items/map inventory, or **F7**
  (or **LB+RB** on a gamepad).
- 🕯 **Silksong-styled menu**: dimmed background, carved panel with gold filigree,
  crimson silk highlights, serif caps — it belongs in Hallownest, not in a debug overlay.
- 🖱 **Its own mouse cursor** — the game hides the system one, so the menu draws a
  needle-and-bead pointer (the OS cursor can be used instead, see `Cursor Mode`).
- 🎮 **Full gamepad support**: open/close, navigate, pick a player and an amount
  and send without touching the keyboard. Button prompts (A/B/LB/RB) are shown
  in the menu and follow the controller layout (Xbox/PlayStation).
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
4. Every player exchanging resources needs RosaryShare 1.1.0 or newer installed (older versions cannot receive shell shards).

## Installation

1. Copy `RosaryShare.dll` into `<Silksong>\BepInEx\plugins\`
   (next to `SilksongMultiplayer.dll` or into its own `RosaryShare\` subfolder).
2. Launch the game.

Or build & auto-install: `build.bat --deploy`.

## Usage

1. Create or join a lobby via the multiplayer mod menu as usual.
2. Load a save file.
3. Press **F7** (mouse/keyboard) or hold **LB+RB** (gamepad): pick a player
   (Steam names), choose beads or shell shards and an amount (100 / 500 / 1000 / 5000 / All, the −/+ stepper
   or a custom value), press **Bestow**.
4. The recipient gets the selected resource instantly plus a toast;
   the sender gets a delivery confirmation.

### Controls

| | Mouse & keyboard | Gamepad |
|---|---|---|
| Open / close the menu | `F7`, `Esc` | hold `LB+RB`, `B`, `Start` |
| Move between items | arrow keys, mouse | left stick / D-pad |
| Confirm | `Enter`, click | `A` |
| Previous / next amount | click a preset | `LB` / `RB` |
| All beads | click **All** | `X` |
| Scroll the journal | mouse wheel | `LT` / `RT` |
| Quick send | `G` | `Quick Send Combo` (off by default) |

While the menu is open the game stops receiving input, so Hornet will not run,
jump or attack behind the window (`Block Game Input`).

If a transfer cannot be delivered (player left, timed out, declined, network
error), the amount is refunded automatically — with a notification.

## Configuration

`BepInEx/config/com.silksong.rosaryshare.cfg`:

| Setting | Default | Description |
|---|---|---|
| `[Interface] Language` | `Auto` | `Auto` / `English` / `Russian`. |
| `[Interface] Inventory Integration` | `true` | Show the Sharing tab while the native items/map inventory is open. |
| `[Interface] Show Toasts` | `true` | Toast notifications about transfers. |
| `[Interface] UI Scale` | `1.0` | Extra menu scale on top of the automatic one. |
| `[Interface] Serif Font` | `true` | Game-like serif system font. |
| `[Interface] Font Name` | *(empty)* | Preferred font, e.g. `Trajan Pro`, `Cinzel`. |
| `[Interface] Background Dim` | `0.78` | How dark the game gets behind the menu. |
| `[Controls] Menu Key` | `F7` | Open/close the sharing window. |
| `[Controls] Quick Send Key` | `G` | Quick-send hotkey. |
| `[Controls] Quick Send Amount` | `100` | Amount sent by the quick-send key. |
| `[Controls] Block Game Input` | `true` | Freeze game input while the menu is open. |
| `[Controls] Cursor Mode` | `Soft` | `Soft` (drawn needle), `System`, `Both`. |
| `[Controls] Cursor Scale` | `1.0` | Size of the drawn cursor. |
| `[Gamepad] Enable Gamepad` | `true` | Gamepad control of the menu. |
| `[Gamepad] Use Game Input Library` | `true` | Read the pad through the game's InControl. |
| `[Gamepad] Menu Combo` | `LB+RB` | Buttons that open the menu (empty — off). |
| `[Gamepad] Menu Combo Hold Seconds` | `0.3` | Hold time for that combo. |
| `[Gamepad] Quick Send Combo` | *(empty)* | e.g. `LB+Y` — quick send without the menu. |
| `[Gamepad] Stick Deadzone` | `0.45` | Stick deadzone for navigation. |
| `[Gamepad] Repeat Delay` / `Repeat Rate` | `0.35` / `0.11` | Held-direction repeat. |
| `[Gamepad] Swap Confirm And Cancel` | `false` | `B` confirms, `A` cancels. |
| `[Transfers] Allow Receive` | `true` | `false` auto-declines incoming transfers (sender is refunded). |
| `[Transfers] Max Send Amount` | `100000` | Per-transfer outgoing limit. |
| `[Transfers] Max Receive Amount` | `1000000` | Per-transfer incoming limit. |
| `[Transfers] Send Cooldown Seconds` | `1.5` | Delay between sends. |
| `[Transfers] Ack Timeout Seconds` | `6` | Delivery wait before auto-refund. |

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
