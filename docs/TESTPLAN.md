# Thronefall Control 测试计划

针对 `docs/design.md` 的 v1 HTTP 控制插件。目标游戏：Thronefall v2.13，地图优先 `Neuland(Tutorial)`，回归用 `Nordfels`。

分层：

| 层 | 是否需要游戏 | 命令 / 做法 |
| --- | --- | --- |
| 单元测试 | 否 | `dotnet test ThronefallControl.Tests.csproj` |
| 局内手工 / curl | 是 | 注入插件后打 loopback HTTP |
| 安装 / 卸载 | 是 | Steam 启动对照 |

单测失败即阻断。局内项在对应功能分支合并前必须在至少一张图上过一遍。

---

## 1. 无需游戏的单元测试

跑：

```bash
dotnet test ThronefallControl.Tests.csproj
```

仓库根或任意 worktree 均可。需要 .NET 8 SDK。这些测试编译 `Http/`、`Dto/`、`Config/`、`Game/MainThread.cs`、`Game/IdRegistry.cs`，**禁止**引用 `UnityEngine` / `Assembly-CSharp`。

### 1.1 HTTP Router

现有：`Tests/RouterTests.cs`。

- `GET /health` 由 `HealthModule` 注册，返回 200 且 `ok=true`，不入主线程队列。
- 未注册路径（例如骨架阶段的 `POST /harvest`）返回 404，`error=not_found`。
- `POST /slots/{id}/build` 这种模板能抽出 `id`。
- `?dryRun=true` 解析到 `RequestContext.DryRun`。
- 新功能只加 `Http/Modules/*.cs` 实现 `IRouteModule`，用 `Router.AddModule` / 程序集扫描注册，不改 `Router.cs` 的路由表。

后续每加一个端点：补一条「模块注册后 Dispatch 命中」的单测；非法 phase 的映射可以在模块里用假 `phase` 字符串测，不必起 Unity。

### 1.2 Auth

现有：`Tests/AuthTests.cs`。

- `AuthToken` 为空：任意请求通过。
- `AuthToken` 非空且缺少 `X-Thronefall-Token`：401 `unauthorized`。
- 头值错误：401。
- 头值匹配：通过。
- 鉴权发生在 Router 之前（见 `Tests/ServerTests.cs`）。

### 1.3 JSON 错误体

现有：`Tests/JsonErrorTests.cs`。

统一信封必须能反序列化为 `ErrorResponse`：

```json
{ "ok": false, "error": "stale_id", "message": "...", "phase": "day", "generation": 4 }
```

字段名 camelCase。覆盖码：`illegal_phase`、`stale_id`、`not_found`、`insufficient_gold`、`choice_required`、`transition_in_progress`、`cheat_disabled`、`dry_run`、`unity_exception`、`main_thread_timeout`、`unauthorized`、`unsupported_in_this_build`。骨架已测信封形状；各模块在返回这些码时补一条断言。

### 1.4 主线程队列（假 dispatcher）

现有：`Tests/MainThreadTests.cs`。`Game/MainThread.cs` 不得引用 Unity 类型。

- `Run` 入队，调用 `Pump` 后 Task 完成。
- 不 `Pump` 时在 `MainThreadTimeoutMs` 后抛 `MainThreadTimeoutException`（HTTP 层应映射 504 `main_thread_timeout`）。超时**不取消**已 `Execute` 的工作。
- `Execute` 内异常进入 TCS，不得逃出 `Pump`。
- 用 `Task.Run` 循环 `Pump` 充当假 dispatcher，验证 `await MainThread.Run` 能结束。
- `MaxWorkItemsPerFrame` 限制每拍处理条数。

HTTP 模块测 mutate 时：注入可 `Pump` 的 `MainThread` 实例，不要碰 `UnityEngine.Object`。

### 1.5 IdRegistry（假对象）

现有：`Tests/IdRegistryTests.cs`。目标存 `object`，测试用普通 CLR 对象代替 `GameObject`。

- `BeginScene` 递增 `SceneGeneration` 并清空表。
- `Register` 写回的 `generation` 等于当前世代。
- 世代不匹配 → `stale_id`。
- 世代匹配但 instanceId 不在表内 → `not_found`。

---

## 2. 局内手工 / curl

默认 `http://127.0.0.1:17891`。若配置了 `AuthToken`，每个请求加 `-H 'X-Thronefall-Token: ...'`.

建议顺序：菜单确认 `/health` → 选图 `Neuland(Tutorial)` 或 `Nordfels` → 白天经济 → 入夜派遣 → 黎明再观察。`Player.log` 与 `BepInEx/LogOutput.txt` 不得出现未捕获异常。

### 2.1 任意 phase（含 HTTP 线程）

| 步骤 | 命令 | 期望 |
| --- | --- | --- |
| 存活 | `curl -s http://127.0.0.1:17891/health` | 200，`ok=true`，`plugin=ThronefallControl`，`cheatsEnabled=false`。进程刚起来、尚在菜单也可以。 |
| 健康 ready | 同一 URL；实现若区分 alive/ready，ready 进主线程填 `phase` / `scene` / `generation` | 局内 `phase` 为 `menu` / `level_select` / `day` / `night` / `end_screen` / `transition` 之一。 |
| 状态 | `curl -s 'http://127.0.0.1:17891/state'` | 200，含 `generation`、`phase`、`economy`、`clock`、`king`。 |
| 裁剪 | `curl -s 'http://127.0.0.1:17891/state?include=slots,units,spawns'` | 未请求的大数组可空或缺省；主线程 < 50 ms（看日志）。 |
| OpenAPI | `curl -s http://127.0.0.1:17891/openapi.json` | 200，OpenAPI 3 文档。 |
| 幂等空闲 | 连续两次相同 `clientRequestId` 的 POST | 第二次重放第一次响应，不重复扣钱 / 不重复派遣。 |
| 过渡期 | 切场景瞬间任意 POST | 409 `transition_in_progress`，带当前 `generation`。 |

### 2.2 观察切片

在 `day` 或 `night`（loadout 另列）：

```bash
curl -s http://127.0.0.1:17891/state/slots
curl -s http://127.0.0.1:17891/state/units
curl -s http://127.0.0.1:17891/state/enemies
curl -s http://127.0.0.1:17891/state/spawns
curl -s http://127.0.0.1:17891/state/loadout
```

| 路径 | 合法 phase | 核对 |
| --- | --- | --- |
| `/state/slots` | day / night / end_screen | 每槽有 `id.instanceId`、`id.generation`、`buildingName`、`nextUpgradeOrBuildCost`、`position`。Nordfels 白天槽位数约几十。 |
| `/state/units` | day / night | `typeName`、`homePosition`、`holdPosition`、`tags` 与 `tagIds` 同时存在。 |
| `/state/enemies` | day / night | 白天 count 可为 0；夜间有波次时 count>0，无投射物字段。 |
| `/state/spawns` | day / night | `polyline` 非空；`suggestedRally` 为插件计算。 |
| `/state/loadout` | menu / level_select / day / night | `asString` 与当前 perk/武器一致。 |

菜单里打 `/state/slots` 应 `illegal_phase` 或空观察（以设计：合法 phase 外拒绝为准）。

过期 ID：记下某个 `instanceId`，退出到选图再进关，用旧 `generation` POST，必须 409 `stale_id`，不得打到新对象。

### 2.3 经济 / 建筑（仅 day）

dry-run 不得改金币、等级、库存。

```bash
# 全图收税（先 dry-run）
curl -s -X POST 'http://127.0.0.1:17891/harvest?dryRun=true' \
  -H 'Content-Type: application/json' \
  -d '{"clientRequestId":"h-dry-1"}'

curl -s -X POST http://127.0.0.1:17891/harvest \
  -H 'Content-Type: application/json' \
  -d '{"clientRequestId":"h-1"}'

# 单槽建造 / 升级（把 SLOT 换成 /state/slots 里的 instanceId）
curl -s -X POST "http://127.0.0.1:17891/slots/${SLOT}/build?dryRun=true" \
  -H 'Content-Type: application/json' \
  -d '{"clientRequestId":"b-dry-1"}'

curl -s -X POST "http://127.0.0.1:17891/slots/${SLOT}/build" \
  -H 'Content-Type: application/json' \
  -d '{"clientRequestId":"b-1","teleportKingNearby":false}'

curl -s -X POST "http://127.0.0.1:17891/slots/${SLOT}/upgrade" \
  -H 'Content-Type: application/json' \
  -d '{"clientRequestId":"u-1"}'
```

| 检查 | 期望 |
| --- | --- |
| dry-run 建造 | 200，`dryRun=true`，`would.cost` 等于 live `nextUpgradeOrBuildCost`，金币不变。 |
| 真建造 | 金币减少，slot `level` 或 `state` 变化；国王不必走进交互半径。 |
| 钱不够 | 409/400 `insufficient_gold`，建筑不变。 |
| `NextUpgradeIsChoice` | `/build` 返回 `needs_choice` / `choice_required` 和分支列表，不擅自选支。随后 `POST /slots/{id}/choice`。 |
| 夜晚 POST `/harvest` 或 `/build` | `illegal_phase`。 |
| `teleportKingNearby=true` | 纯视觉，建造仍应成功。 |

```bash
curl -s -X POST "http://127.0.0.1:17891/slots/${SLOT}/choice" \
  -H 'Content-Type: application/json' \
  -d '{"clientRequestId":"c-1","name":"<choice-name>"}'
```

### 2.4 日夜与开路

```bash
curl -s -X POST http://127.0.0.1:17891/night/call \
  -H 'Content-Type: application/json' \
  -d '{"clientRequestId":"n-1"}'

curl -s -X POST http://127.0.0.1:17891/path/toggle \
  -H 'Content-Type: application/json' \
  -d '{"clientRequestId":"p-1","id":{"instanceId":CUTTER,"generation":GEN}}'
```

| 检查 | 期望 |
| --- | --- |
| 白天且 `isFreeToCallNight=true` | 进入 night，`clock.timestate=Night`。 |
| 白天但未收完税 / 不能 call | 409，不入夜。 |
| 已是 night 再 call | `illegal_phase`。 |
| 默认不跳波 | 不得调用 `EnemySpawner.DebugSkipWave`。 |
| 开路器 | `pathOpened` 翻转，扣 `toggleCost`；`toogleOnlyAtDay` 的 cutter 夜晚拒绝。 |

### 2.5 单位派遣（day / night）

```bash
curl -s -X POST http://127.0.0.1:17891/units/command \
  -H 'Content-Type: application/json' \
  -d '{"clientRequestId":"u-1","selector":{"ids":[UID]},"target":{"x":12,"y":0,"z":-3},"hold":true,"useSolver":false}'

curl -s -X POST http://127.0.0.1:17891/units/hold \
  -H 'Content-Type: application/json' \
  -d '{"clientRequestId":"u-2","selector":{"ids":[UID]}}'

curl -s -X POST http://127.0.0.1:17891/units/follow \
  -H 'Content-Type: application/json' \
  -d '{"clientRequestId":"u-3","selector":{"typeName":"P Knight"}}'

curl -s -X POST http://127.0.0.1:17891/units/groups \
  -H 'Content-Type: application/json' \
  -d '{"clientRequestId":"u-4","group":1,"selector":{"ids":[UID]}}'

curl -s -X POST http://127.0.0.1:17891/units/send-to-spawn \
  -H 'Content-Type: application/json' \
  -d '{"clientRequestId":"u-5","typeName":"P Knight","spawnId":SPAWN,"hold":true}'
```

| 检查 | 期望 |
| --- | --- |
| fallback（`UseCommandUnitsSolver=false`） | 单位 `homePosition` 靠近目标，`holdPosition=true`，站在 navmesh 上。 |
| solver 关闭时 `useSolver=true` | 仍走 fallback 或明确返回未启用，不得崩。 |
| 过期 unit id | `stale_id`，其它单位仍可执行。 |
| send-to-spawn | 落在 spawn 折线朝城堡一侧、墙外（`WallBackOffset` 默认 3）。 |
| 控制组 1/2/3 | `/state/units` 出现对应 tag。 |
| 早晨重置 | 若 `resetUnitFormationEveryMorning=true`，非 hold 单位黎明可能回家；hold 行为与设置一致。 |

Neuland 白天用 2–3 个近战单位目视对照。

### 2.6 国王、夜间策略、Loadout、选图

```bash
curl -s -X POST http://127.0.0.1:17891/king/teleport \
  -H 'Content-Type: application/json' \
  -d '{"clientRequestId":"k-1","target":"castle"}'

curl -s -X POST http://127.0.0.1:17891/night/policy \
  -H 'Content-Type: application/json' \
  -d '{"clientRequestId":"np-1","policy":"human"}'

curl -s -X POST http://127.0.0.1:17891/loadout/select \
  -H 'Content-Type: application/json' \
  -d '{"clientRequestId":"l-1","name":"Royal Mint","kind":"perk"}'

curl -s -X POST http://127.0.0.1:17891/level/start \
  -H 'Content-Type: application/json' \
  -d '{"clientRequestId":"s-1","sceneName":"Nordfels"}'
```

| 路径 | 合法 phase | 核对 |
| --- | --- | --- |
| `POST /king/teleport` | day / night / level_select | `castle` / `start` / 坐标；默认不 `MakeInvulerable`。 |
| `POST /night/policy` | day / night | `human`：不传送、不改 hold。`afk_castle`：国王到城堡。`scripted_posts`：按 spawn rally 派遣。策略立即执行一次。 |
| `POST /loadout/select` | level_select | 未解锁装备拒绝；`/state/loadout` 更新。 |
| `POST /level/start` | level_select | 走 `LevelInteractor` + `PlayButtonPressed`，不要自己 `LoadScene`。随后 `/health.phase` 经 `transition` 到 `day`。 |

### 2.7 Debug 端点（必须全部 gated）

```bash
curl -s -X POST http://127.0.0.1:17891/debug/upgrade-max \
  -H 'Content-Type: application/json' -d '{"clientRequestId":"d-1"}'
curl -s -X POST http://127.0.0.1:17891/debug/skip-wave \
  -H 'Content-Type: application/json' -d '{"clientRequestId":"d-2"}'
curl -s -X POST http://127.0.0.1:17891/debug/invulnerable \
  -H 'Content-Type: application/json' -d '{"clientRequestId":"d-3"}'
curl -s -X POST http://127.0.0.1:17891/debug/save \
  -H 'Content-Type: application/json' -d '{"clientRequestId":"d-4"}'
```

默认配置下四条都要 `cheat_disabled`（save 可同码或等价拒绝），建筑不得被 max、波次不得被跳、国王不得无敌、不得写 `LocalMatchSaveLoad.Save`。

仅当显式打开对应开关后再测一次成功路径，测完立刻关，并避免 Steam Cloud 同步该存档。

---

## 3. 绑定失败不得崩游戏

1. 把 `HttpPort` 改成已被占用的端口，或把 `BindAddress` 设为 `0.0.0.0`（必须拒绝非 loopback）。
2. 经 Steam 启动游戏。
3. 游戏进入菜单、能开新局；`BepInEx` 日志有 bind 失败，**没有** Awake 未捕获异常。
4. `curl` 连不上是预期；`/health` 不存在可接受。
5. 改回 `127.0.0.1:17891` 后下一局应能监听。

单测侧：`Server.Start` 在 `0.0.0.0` 上不抛异常且 `IsListening=false`（`Tests/ServerTests.cs`）。

---

## 4. 卸载后原版游戏仍能启动

1. 清空 Steam launch options，或删掉 `BepInEx/plugins/ThronefallControl.dll`。
2. 完整卸载对照（可选）：删除游戏根目录下 `BepInEx/`、`libdoorstop.dylib`、`run_bepinex.sh`、`doorstop_config.ini`。
3. Steam 点 Play：主菜单出现，能进 `Neuland(Tutorial)` / `Nordfels`。
4. 不改 `ThroneSave.sav` / `{Map}.json`（除非测过显式 `/debug/save`）。
5. `curl 127.0.0.1:17891/health` 失败。

---

## 5. 作弊默认关闭

启动后读 `BepInEx/config/com.thronefall.control.cfg`（或等价）与 `/health`：

- `EnableDebugCheats=false`
- `EnableDebugUpgradeToMax=false`
- `AllowSaveApi=false`
- `UseCommandUnitsSolver=false`
- `DefaultNightPolicy=human`
- `/health.cheatsEnabled=false`

不得自动打开 `CheatMenuIMGUI`，不得注册 `CheatSystem` 项，不得默认 `Hp.MakeInvulerable` / `DEBUGUpgradeToMax` / `DebugSkipWave`。

单测：`Tests/ServerTests.Cheats_are_off_by_default`。

---

## 6. 通过标准

功能分支合并前：

1. `dotnet test ThronefallControl.Tests.csproj` 全绿。
2. 本分支新增端点在 `Neuland(Tutorial)` 或 `Nordfels` 上按上表 curl 过一遍。
3. `Player.log` / `LogOutput.txt` 无未捕获异常。
4. 插件 DLL 不在时游戏仍能从 Steam 原版启动。
5. 绑定失败不阻止进菜单。
6. 作弊相关配置保持默认关。
