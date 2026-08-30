<div align="center">

# thronefall-mcp

**A local control plane so you and your AI agent can play Thronefall together**

<p>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-3DA639?style=for-the-badge" alt="License MIT" /></a>
  <a href="https://store.steampowered.com/app/2239150/Thronefall/"><img src="https://img.shields.io/badge/Game-Thronefall-171a21?style=for-the-badge&logo=steam&logoColor=white" alt="Thronefall on Steam" /></a>
  <a href="#quick-start"><img src="https://img.shields.io/badge/Platform-macOS-000000?style=for-the-badge&logo=apple&logoColor=white" alt="macOS" /></a>
  <a href="#status"><img src="https://img.shields.io/badge/Status-early-1E90FF?style=for-the-badge" alt="Early status" /></a>
</p>

</div>

thronefall-mcp is a loopback HTTP API (and upcoming MCP server) in front of a BepInEx plugin. You keep the mouse. The agent reads gold, slots, units, and spawn lines, then calls the same C# methods the game already uses to harvest, upgrade, call night, and post units.

Existing approaches fight you for the same window. Cheat tables poke memory. Screenshot bots click the wrong tower. thronefall-mcp injects into the running Unity player and talks JSON on `127.0.0.1` only. No cloud, no overlay war, no walking the king to a building just to press upgrade.

> 白天建筑是槽位选择，夜晚把兵派到世界坐标。插件做观察器和执行器；经济怎么滚、兵往哪站，留给你的 agent。

## Quick Start

macOS + Steam Thronefall **v2.13** (Mono, not IL2CPP) today. The HTTP surface is designed; the plugin is landing on `main` in slices.

### 1. Clone

```bash
git clone https://github.com/occcat/thronefall-mcp.git
cd thronefall-mcp
```

### 2. Let your agent read the design

Paste this into your agent:

```
Set up thronefall-mcp for me: https://github.com/occcat/thronefall-mcp

Read README.md and docs/design.md. Do not click the game. Drive it through the local HTTP API on 127.0.0.1:17891.
```

### 3. Install BepInEx 5 unix doorstop (macOS)

The game is a Mono `.app`. Do **not** unpack Thunderstore `BepInExPack_Thronefall` (that pack is `winhttp.dll` + `Thronefall.exe`).

1. Download a **BepInEx 5 unix/macos x64 Mono** zip from [BepInEx releases](https://github.com/BepInEx/BepInEx/releases) (`BepInEx_unix_5.4.x.zip` / `BepInEx_macos_x64_*.zip`).
2. From this repo:

```bash
chmod +x Scripts/install_macos.sh
./Scripts/install_macos.sh --zip ~/Downloads/BepInEx_unix_5.4.23.3.zip
```

The script extracts next to `thronefall.app`, sets `executable_name="thronefall.app"` (see `Scripts/run_bepinex.example.sh`), `chmod`/`xattr` doorstop, and copies `ThronefallControl.dll` if you already built it.

3. Steam → Thronefall → Properties → Launch Options (absolute path required on macOS):

```
"/Users/<you>/Library/Application Support/Steam/steamapps/common/Thronefall/run_bepinex.sh" %command%
```

4. Play once via Steam. `BepInEx/LogOutput.txt` should show `Chainloader startup complete`. Then:

```bash
curl -s http://127.0.0.1:17891/health
```

Night policy defaults to **`human`**: the plugin does not teleport the king, rewrite HoldPosition, or call `MakeInvulerable`. Cheats (`DEBUGUpgradeToMax`, skip wave, god mode, save API) stay behind flags, off by default.

Apple Silicon: this plugin does not use Harmony. If doorstop still fails to inject a universal binary, the unix `run_bepinex.sh` already re-passes `DYLD_INSERT_LIBRARIES` through `arch`; as a diagnostic, `arch -x86_64 ./run_bepinex.sh` from the game root.

### 4. Talk to a running game

```bash
curl -s http://127.0.0.1:17891/health
curl -s http://127.0.0.1:17891/openapi.json
curl -s 'http://127.0.0.1:17891/state?include=slots,units,spawns'
curl -s -X POST http://127.0.0.1:17891/harvest \
  -H 'Content-Type: application/json' \
  -d '{"clientRequestId":"day-1-harvest"}'
```

Python: `Clients/thronefall_control.py` (`Thronefall().health()`, `.select_loadout(...)`, `.night_policy("human")`, `.start_level("Nordfels")`).

## Highlight

| Feature | What it does |
| --- | --- |
| **In-process calls, not fake clicks** | Harvest, build, and upgrade go through `BuildSlot.TryToBuildOrUpgradeAndPay` and `BuildingInteractor.Harvest`. The king does not have to walk into range. |
| **A snapshot the model can actually plan on** | `GET /state` returns gold, day/night, every slot's level and next cost, unit HP/home/hold, and enemy spawn lines — live values, not wiki tables. |
| **Units go to coordinates, buildings stay on slots** | `POST /units/command` writes `HomePosition` / Hold, or the game's own `PlaceCommandedUnitsAndCalculateTargetPositions` solver. `POST /units/send-to-spawn` maps a unit type onto an `EnemySpawnLine`. |
| **You keep the night, or you don't** | Night policy is `human` (default), `afk_castle`, or `scripted_posts`. Combat micro is out of scope. |
| **Loopback only** | `HttpListener` binds `127.0.0.1:17891`. Optional `X-Thronefall-Token`. Cheats (`DEBUGUpgradeToMax`, skip wave, god mode) stay behind flags, off by default. |
| **Any agent that can HTTP** | Grok, Claude Code, Codex, curl, or a thin Python client. Strategy stays outside the plugin so the same actuator can serve all of them. |

## thronefall-mcp vs existing products

Most tools can "play a game." The real questions are whether the agent sees structured state, whether you can keep playing, and whether a game update wipes a pixel bot.

| Capability | thronefall-mcp | Screenshot + click | Cheat Engine table | Manual play |
| --- | :---: | :---: | :---: | :---: |
| Structured state for the agent | ✓ | — | partial | — |
| Build without walking the king | ✓ | — | — | — |
| Send units to spawn lines | ✓ | — | — | ✓ |
| You and the agent in parallel | ✓ | — | ✓ | — |
| Survives UI skin changes | ✓ | — | fragile | ✓ |
| Default-off cheats | ✓ | n/a | — | ✓ |
| Data stays on the machine | ✓ | ✓ | ✓ | ✓ |
| Free | ✓ | ✓ | ✓ | ✓ |

Screenshot bots re-click HUD every patch. Memory tables break when Unity moves a field. thronefall-mcp calls public (and a small cached-reflection private) API on `Assembly-CSharp.dll` from the version you actually have installed.

## Status

| Area | State |
| --- | --- |
| Design (macOS Mono, HTTP, IDs, night policies) | Done — [`docs/design.md`](docs/design.md) |
| Repo conventions | Done — [`AGENTS.md`](AGENTS.md) |
| BepInEx plugin + HTTP | Skeleton on `thoxvi/local-http-control`; workers add `Http/Modules/` |
| MCP stdio wrapper | Planned (same commands as HTTP) |
| Windows / Linux game builds | Not this milestone |

Plugin GUID: `com.thronefall.control`. Default bind: `127.0.0.1:17891`. Default night policy: `human`.

## MCP

HTTP on `127.0.0.1:17891` is the source of truth. A stdio MCP wrapper is intended at [`Clients/mcp`](Clients/mcp) (same commands as this API). If that folder is not in your checkout yet, do not invent a server file — drive the game with curl or `Clients/thronefall_control.py`, then add MCP once the wrapper lands:

```bash
grok mcp add thronefall -- python3 Clients/mcp
```

## Unit tests (no game)

Needs the .NET 8 SDK. `Http/` and `Dto/` have no `UnityEngine` references.

```bash
dotnet test ThronefallControl.Tests.csproj
```

Covers the HTTP router, token auth, JSON error envelope, main-thread queue with a fake dispatcher, and `IdRegistry` generation / stale ids. In-game curl checks are in [`docs/TESTPLAN.md`](docs/TESTPLAN.md).

## Docs

- [`AGENTS.md`](AGENTS.md) — Conventional Commits (Chinese body, what changed + why)
- [`docs/design.md`](docs/design.md) — BepInEx plugin HTTP design
- [`docs/TESTPLAN.md`](docs/TESTPLAN.md) — unit tests and Neuland / Nordfels curl checklist
- [`Scripts/install_macos.sh`](Scripts/install_macos.sh) — BepInEx 5 unix doorstop into the Steam `.app` root
- [`Scripts/run_bepinex.example.sh`](Scripts/run_bepinex.example.sh) — `executable_name=thronefall.app` (not Windows Thunderstore)
- [`Clients/thronefall_control.py`](Clients/thronefall_control.py) — stdlib HTTP client
- Game install (read-only): `~/Library/Application Support/Steam/steamapps/common/Thronefall/thronefall.app`
- Saves: `~/Library/Application Support/Grizzly Games/Thronefall/`

## Community

Open issues and pull requests on this repo. Labels map to plugin surfaces (`http`, `units`, `bepinex`, `macos`) so an agent can filter work the same way a human would.

## License

Released under the [MIT License](LICENSE). Thronefall itself is [Grizzly Games](https://store.steampowered.com/app/2239150/Thronefall/) on Steam and is not included.
