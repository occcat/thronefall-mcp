---
name: thronefall-play
description: 通过已发布的 HTTP / MCP 代操 Thronefall。开局读 loadout。每天收税、用 thronefall_next_wave 看今晚每口兵种和数量、花钱、白天派兵，开夜前询问。用户说代操、排兵布阵、play the match 或 /thronefall-play 时使用。
---

# thronefall-play

插件 HTTP 在 Unity 进程里。MCP 是 `Clients/mcp/server.py` 的 stdio 代理。默认 `http://127.0.0.1:17891`，本机对局常用 `http://127.0.0.1:17892`（`THRONEFALL_URL`）。不要点游戏。不要开作弊，不要打 `/debug/*`。

每天：**能花的金币花完**，**只在 `day` 派兵**，**问过再召夜**。钱包有余钱、兵还堆在兵营/城堡、或用户没点头，都不要 `/night/call`。夜里不要改 Home。黎明会重置阵型。

**不要自己编今晚的口。** 先 `thronefall_next_wave`（`GET /state/next-wave`）。只看 **`mouths[]`**：`spawn`、`suggestedRally`、**`mouths[].enemies[]`（种类 + 数量 + elite）**。不要用顶层 `enemies[]`（全图汇总）或只看扁的 `groups[]`。`available=false` 就问用户并等待。`/state/spawns` 是地图全部线，不是今晚。

## 本局约定

- **不要瞬移国王。** 不调 `thronefall_king_teleport` / `/king/teleport`，不用 `afk_castle`，建造不带 `teleportKingNearby`。收税用 `thronefall_harvest`。
- **小兵不要固守。** `/units/command` 与 `/units/send-to-spawn` 一律 `"hold": false`。不要 `/units/hold`，不要 `/units/follow` 把整队绑到国王。
- 用户点头后立刻 `thronefall_night_call`。不要让用户去点游戏召夜。白天即可召，不再要求 `isFreeToCallNight`。不要自己传国王。

## MCP 工具（已发布）

优先走 MCP。没有对应工具的路径用 HTTP。

| 工具 | HTTP | 用途 |
|---|---|---|
| `thronefall_health` | `GET /health`（`ready` → `/health/ready`） | 进程是否活着 |
| `thronefall_state` | `GET /state?include=` | 快照。include：`slots,units,training,enemies,spawns,nextWave,loadout,cutters`。空 include = 全要 |
| `thronefall_next_wave` | `GET /state/next-wave` | 今晚每口。顶层带 `available` 与 `mouths` |
| `thronefall_harvest` | `POST /harvest` | 收全部或一个 `slotId` |
| `thronefall_slot_upgrade` | `POST /slots/{id}/upgrade` | 建造/升级 |
| `thronefall_slot_choice_cancel` | `POST /slots/choice/cancel` | 取消进行中的升级选择。无选择 → 409 `not_found` |
| `thronefall_night_call` | `POST /night/call` | 召夜。不跳波 |
| `thronefall_units_command` | `POST /units/command` | 把单位 Home 写到世界点 |
| `thronefall_units_send_to_spawn` | `POST /units/send-to-spawn` | 按类型派到某条 spawn 线的集结 |
| `thronefall_path_toggle` | `POST /path/toggle` | 开/关开路器 |
| `thronefall_loadout_select` | `POST /loadout/select` | 选图装备 perk/武器/突变 |
| `thronefall_level_start` | `POST /level/start` | 选图开局 |

有工具但本局不用：`thronefall_king_teleport`。

HTTP 有、MCP 未单独包的：`POST /slots/{id}/build`（与 upgrade 同义）、`POST /slots/{id}/choice`、`POST /units/groups`、`GET /state/training`、`GET /state/slots|units|enemies|spawns|loadout`。`POST /night/policy` 存在；本局只用 `human`，不要 `afk_castle`。

## 开局（一次）

`thronefall_state` `include=loadout`（或 `GET /state/loadout`）。读：

- `asString`：已装备名
- `catalog[]`：`name` / `kind`（perk\|weapon\|mutator）/ `locked` / `unlocked` / `description`
- `quests[]`：`statement` / `complete`
- `worth`、`perkPointsRemaining`

先写本局策略再造：

- Builder's Guild / Architect's Council / Resilient Residences → 城堡附近先房子。磨坊要田：先填满已有磨坊的 `Field`（1g → +1/夜），再开下一座。
- Elite Warriors → 兵营近战是骨架。
- Arcane Towers / Castle Fortifications → 点名的口缺人时，在 **那个口** 上墙/塔是计划内。
- Commander Mode / 战斗法杖 → 国王自己走，在已布置的线上打；仍不瞬移。
- 飞兵/精英/额外波 → 多口加远程或墙。

loadout 中途变 null 就重读。不要写对局流水账文件，除非用户要。

## 每天

1. `thronefall_harvest`，直到 `economy.balance` 对上该收的钱。
2. **`thronefall_next_wave`。** `available=true` 才评口；否则停下来问。先评口，再花钱，再派兵。
3. 兵力够不够、要不要升基地 — 见花钱。钱包花空。
4. **白天** 把兵派到 `mouths[].suggestedRally` 的城堡侧（见排兵）。夜里来不及。`"hold": false`。
5. 停下。汇报金币（应 0–2）、口、`mouths[].enemies`、岗位、升基地结论。等用户。点头立刻召夜。

## 花钱

读 `Castle Center`：`level`、`nextUpgradeOrBuildCost`、`nextUpgradeIsChoice`、`tooltip`、`nextUpgradeLabel`、`unlockPreview`、`hp`。城堡是失败条件。

**兵力不够**（人少、类型不对、点名的口没岗也没墙/塔）：先砸 **那个口** — 隘口城堡侧先墙，perk 要塔再塔，再造会站在那里的兵营/箭塔。看 `tooltip` / `nextUpgradeLabel` / `choices`；L1 兵营/箭塔就是一次选择。L1 塔只许当口上防御。

看 `GET /state/training`（或 include=`training`）：`hasKnockedOut`、`timeTillNextRespawn`。还在出的兵算进「够不够」。

**然后升基地。** 昨夜漏城堡、城堡吃了实伤、`clock.finalWaveComingUp` / `wavenumber` 过半、或点名口已有岗且剩钱够，就升。`nextUpgradeIsChoice`：upgrade 后立刻 `POST /slots/{id}/choice`。选错了用 `thronefall_slot_choice_cancel`。不要留下未选。

**之后剩钱做经济：**

1. 已有磨坊的 `Field`（通常 1g）。先把现有磨坊周围蓝图田造完，再开新磨坊。
2. 升当前磨坊：只在它自己的田满了之后。
3. 新磨坊：每座已有磨坊的田都满之后；开完先填 **这座** 的田。
4. 城堡或已有磨坊附近的房子；够得着的矿；房子 L2 最后。

格子挡住就 `thronefall_path_toggle`。Slot POST 不要带 `/state` 的 generation。无 choice 可并行；`needsChoice` 串行。

`balance` ≥ 还剩的最便宜合法购买，这一天没完。

## 排兵

### 1. 今晚哪些口

1. `thronefall_next_wave`。`available` 时 **只守 `mouths[]`**。威胁看 `mouths[].enemies[]`（`name`×`count`×`elite`）。
2. `available=false`：问用户看见哪些 HUD 口，等回答。
3. 不要把 `/state/spawns` 的线当今晚有兵。很近的两条集结是一堆；分开的口各守一岗。
4. 两步都没有：兵停城堡，**不要派到猜的线上**。

也读 `clock`：`wavenumber` / `waveCount` / `finalWaveComingUp` / `currentScore`。

### 2. 分配

按口威胁分现有兵，不要把全军倒在最响的那条。

| 口上的敌人 | 优先 |
|---|---|
| 飞兵 / `E Archer` / 远程 | 弩手 |
| `E Monster*` | 猎人 |
| 攻城 / 高血 / `E Ogre` | 狂战、火弓、骑士 |
| 混合地面 | 骑士 |
| 快地走 / `E Racer` | 矛兵、猎人 |

### 3. 落点

站在墙的 **城堡一侧**、隘口里，离出兵折线几米。用该口 `suggestedRally`，不要站在线上。home 吸飞了就另挑该路城堡侧。守磨坊站磨坊边。

### 4. 白天派出

phase 必须 `day`。已发布接口：

- 多口、要点名到人：`GET /state?include=units` 取 `id.instanceId` / `typeName`，再 `thronefall_units_command`（`selector.ids` + `target` + `"hold": false`）。一口一次。
- 整类上一口：`thronefall_units_send_to_spawn`（`typeName` + `spawnId` = `mouths[].spawn.instanceId`，`"hold": false`）。

`/units/command` 只改 Home，不是固守。需要分组再用 HTTP `POST /units/groups`。黎明重置 Home，**每个白天重派**。

## 召夜

用户点头 → `thronefall_night_call`。开夜后不要再派兵。夜里需要看场就 `include=enemies,units`（敌军坐标是 `pos`）。不要根据夜里站位改口——口只认当天的 `mouths[]`。
