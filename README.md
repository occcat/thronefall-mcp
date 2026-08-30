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

### 3. Talk to a running game (once the plugin is installed)

```bash
curl -s http://127.0.0.1:17891/health
curl -s 'http://127.0.0.1:17891/state?include=slots,units,spawns'
curl -s -X POST http://127.0.0.1:17891/harvest \
  -H 'Content-Type: application/json' \
  -d '{"clientRequestId":"day-1-harvest"}'
```

Install steps for BepInEx on the macOS `.app` (not the Windows Thunderstore pack) live in the design doc and will land in `Scripts/` as they are verified on Apple Silicon.

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
| Design (macOS Mono, HTTP, IDs, night policies) | Done — [`docs/design.md`](docs/design.md) when present on the work branch |
| Repo conventions | Done — [`AGENTS.md`](AGENTS.md) |
| BepInEx plugin + HTTP | In progress |
| MCP stdio wrapper | Planned (same commands as HTTP) |
| Windows / Linux game builds | Not this milestone |

## Docs

- [`AGENTS.md`](AGENTS.md) — Conventional Commits (Chinese body, what changed + why)
- Design and test plan land next to the plugin implementation
- Game install (read-only): `~/Library/Application Support/Steam/steamapps/common/Thronefall/thronefall.app`
- Saves: `~/Library/Application Support/Grizzly Games/Thronefall/`

## Community

Open issues and pull requests on this repo. Labels map to plugin surfaces (`http`, `units`, `bepinex`, `macos`) so an agent can filter work the same way a human would.

## License

Released under the [MIT License](LICENSE). Thronefall itself is [Grizzly Games](https://store.steampowered.com/app/2239150/Thronefall/) on Steam and is not included.
