---
name: thronefall-play
description: 代操 Thronefall。战略判断局面，战术走已发布 HTTP / MCP。做选择时读 references/codex.md。召夜前按 loadout 武器和今晚 mouths 告诉用户国王怎么打。用户说代操、排兵布阵、图鉴、play the match 或 /thronefall-play 时使用。
---

# thronefall-play

插件 HTTP 在 Unity 进程里。MCP 是 `Clients/mcp/server.py` 的 stdio 代理。默认 `http://127.0.0.1:17891`，本机对局常用 `http://127.0.0.1:17892`（`THRONEFALL_URL`）。

建筑、兵种、怪物、挑战、perk、武器在 `references/codex.md`。费用以 live `nextUpgradeOrBuildCost` 为准；文案和 live `tooltip` / `nextUpgradeLabel` 冲突时听 live。

## 战略

城堡倒了或国王死了就输。金币是后面所有选择的上限。

每一天只判断三件事：今晚的威胁在哪、现有力量能不能活过今夜、活下来之后收入够不够撑后面的夜。答案从当前对局状态和 `references/codex.md` 里读。

不要套固定建造链，不要按口种建筑，不要为了花光而花。Loadout 是本局约束，不是购物清单。自己决定白天什么时候结束。

## 硬约束

- 不要点游戏。不要开作弊，不要打 `/debug/*`。
- **不要瞬移国王。** 不调 `thronefall_king_teleport` / `/king/teleport`，不用 `afk_castle`，建造不带 `teleportKingNearby`。收税用 `thronefall_harvest`。
- **小兵不要固守。** `/units/command`、`/units/send-to-spawn`、`/units/deploy` 的 API 默认 `hold=true`；本局一律显式传 `"hold": false`。不要 `/units/hold`，不要 `/units/follow` 把整队绑到国王。
- HTTP 看 `nextWave.available` / `nextWave.mouths[]`；MCP 还会抄到顶层 `available` / `mouths`。只看 `mouths[]`：`spawn`、`suggestedRally`、`mouths[].enemies[]`。不要用顶层 `enemies[]` 或扁的 `groups[]`。不要编口。`/state/spawns` 是全图线，不是今晚。
- `POST /night/policy` 只用 `human`。`scripted_posts` 只记 intent，不派兵。不要 `afk_castle`。
- 不要问用户要不要召夜。白天即可召，不要求 `isFreeToCallNight`。召夜前必须按当晚武器、perk 和 `mouths[]` 用两三句告诉用户国王怎么打，然后立刻 `thronefall_night_call`。开夜后不要再派兵。夜里不要改 Home。
- Slot POST 不要带 `/state` 的 generation。`needsChoice` 立刻 `POST /slots/{id}/choice`，选错了用 `thronefall_slot_choice_cancel`，不要留下未选。
- `settings.resetUnitFormationEveryMorning` 为 true 时黎明会清 Home。不要写对局流水账文件，除非用户要。

## 工具

优先走 MCP。没有对应工具的路径用 HTTP。

| 工具                               | HTTP                                     | 用途                                                                                       |
| -------------------------------- | ---------------------------------------- | ---------------------------------------------------------------------------------------- |
| `thronefall_health`              | `GET /health`（`ready` → `/health/ready`） | 进程是否活着                                                                                   |
| `thronefall_state`               | `GET /state?include=`                    | 快照。include：`slots,units,training,enemies,spawns,nextWave,loadout,cutters`。空 include = 全要 |
| `thronefall_next_wave`           | `GET /state/next-wave`                   | 今晚每口。顶层带 `available` 与 `mouths`                                                          |
| `thronefall_harvest`             | `POST /harvest`                          | 收全部或一个 `slotId`                                                                          |
| `thronefall_slot_upgrade`        | `POST /slots/{id}/upgrade`               | 建造/升级                                                                                    |
| `thronefall_slot_choice_cancel`  | `POST /slots/choice/cancel`              | 取消进行中的升级选择。无选择 → 409 `not_found`                                                         |
| `thronefall_night_call`          | `POST /night/call`                       | 召夜。不跳波                                                                                   |
| `thronefall_units_command`       | `POST /units/command`                    | `WarpTo` 到世界点；响应仍报 `path=fallback`                                                       |
| `thronefall_units_send_to_spawn` | `POST /units/send-to-spawn`              | 按类型派到某条 spawn 线的集结                                                                       |
| `thronefall_units_deploy`        | `POST /units/deploy`                     | 按种类/数量或 id 瞬移到世界点                                                                       |
| `thronefall_path_toggle`         | `POST /path/toggle`                      | 开/关开路器                                                                                   |
| `thronefall_loadout_select`      | `POST /loadout/select`                   | 选图装备 perk/武器/突变                                                                          |
| `thronefall_level_start`         | `POST /level/start`                      | 选图开局                                                                                     |


有工具但本局不用：`thronefall_king_teleport`。

HTTP 有、MCP 未单独包的：`POST /slots/{id}/build`（与 upgrade 同义）、`POST /slots/{id}/choice`、`POST /units/groups`、`GET /state/training`、`GET /state/slots|units|enemies|spawns|loadout`。MCP 有 `thronefall_units_deploy`（`POST /units/deploy`：`picks` / `target` / `hold` / `spacing=2`）。

## 战术

### 读局

开局读 loadout：`asString`、`catalog[]`、`quests[]`、`worth`、`perkPointsRemaining`。中途变 null 就重读。

每天先 `thronefall_harvest`，直到 `economy.balance` 对上该收的钱。再 `thronefall_next_wave`。`available=false`：城堡未建就先建再读；仍 false 则兵停城堡，不要问 HUD。也读 `clock`（`wavenumber` / `waveCount` / `finalWaveComingUp` / `currentScore`）和 `GET /state/training`（`hasKnockedOut`、`timeTillNextRespawn`；还在出的兵算进今夜力量）。

格子看 `tooltip` / `nextUpgradeLabel` / `choices` / `goldIncome` / `canBeUpgraded`。挡住就 `thronefall_path_toggle`。无 choice 可并行；`needsChoice` 串行。

### 花钱

城堡是失败条件，先看它的 `hp`、`level`、下一档造价和分支。

该买什么由战略和图鉴定，不按固定顺序清仓。L1 兵营/箭塔/英雄营就是一次兵种选择，名字用 choice 里的英文（`Knights`、`Fire Archers`、`Hunters`…）。类型对不上就换兵种或换岗，不要假装现有兵能打他们够不着的目标。

### 排兵

`available` 时 **只守** `mouths[]`。威胁看 `mouths[].enemies[]`（`name` × `count` × `elite`）。很近的两条集结当一堆；分开的口各守一岗。没有 `mouths[]` 不要派到猜的线上。

站在墙的 **城堡一侧**、隘口里，离出兵折线几米。用该口 `suggestedRally`，不要站在线上。home 吸飞了就另挑该路城堡侧。对策和克制看图鉴「对策速查」，按口分现有兵，不要把全军倒在最响的那条。

phase 必须 `day`：

- 多口、要点名到人：`include=units` 取 `id.instanceId` / `typeName`，再 `thronefall_units_command`（`selector.ids` + `target` + `"hold": false`）。一口一次。command 会 `WarpTo`，响应仍报 `path=fallback`。
- 整类上一口：`thronefall_units_send_to_spawn`（`typeName` + `spawnId` = `mouths[].spawn.instanceId`，`"hold": false`）。
- 按种类/数量瞬移：`thronefall_units_deploy`（`picks` + `target` + `"hold": false` + `spacing` 默认 2）。

需要分组再用 `POST /units/groups`。`settings.resetUnitFormationEveryMorning` 为 true 时黎明会清 Home，**每个白天重派**。

### 当晚国王建议

国王由用户操控。不要替用户走、砍、放技能，也不要瞬移国王。召夜前用两三句告诉用户今晚怎么打，然后立刻 `thronefall_night_call`。

拼法：读 `loadout.asString` 里的武器 + 会改国王定位的 perk/挑战，再读今晚 `mouths[].enemies[]`。武器细则看图鉴「武器」。

武器默认：


| 武器 | 告诉用户 |
|---|---|
| Heavy Sword（重剑） | 站进人堆里砍，等人挤在一起再放主动 |
| Light Spear（轻矛） | 去拦快速的和 `E Racer`。残血再开主动（回血+攻速） |
| Long Bow（长弓） | 站后排射人。攻城贴上去用匕首，残血补刀刷冷却 |
| Lightning Wand（闪电法杖） | 去打散开的和飞的，别站进人堆正中 |
| Battle Axe（战斧） | 贴攻城和地面群；飞的别管。要挨打时开盾 |
| Falchion and Trap（弯刀与陷阱） | 点名怪物和高血；陷阱放必经之路 |
| Potion Vials（药水瓶） | 站在己方兵堆里奶。只对地 |
| Blood Wand（血之杖） | 盯一个打，站住叠层。主动会自残 |
| Cursed Blowpipe（诅咒吹箭） | 先给几个人上诅咒，主动清诅咒并砸伤害。飞的用别的 |


再叠约束（有就改建议，没有就按武器）：

- `Commander Mode`：国王别当主力，去补漏或站 `Power Tower` 旁边
- `Warrior Mode`：国王去今晚最响的口输出，兵只挡
- `Pacifist Pact`：今晚不要攻击
- `Glass Canon` / `Risk Taker`：别硬换血
- `Heavy Armor` / `Ring of Resurection`：可以去挡一下关键口
- `Power Tower`：站在要强化的那座塔旁边
- `War Horse`：可以撞，会自伤
- 今晚有投石机：近战武器去拆，弓别隔空射
- 今晚有 `E Flyer` / 飞：远程武器去打飞，近战别管
- 今晚有 `E Racer` / 快：矛去隘口拦
- 今晚有精英 / `E Ogre` / Boss：点名最高血，别在小兵堆里空转
- `Challenge the Falcon God`：优先拦快的
- `Challenge the Elite God`：优先点精英

### 召夜

白天判断、派兵、国王建议说完就 `thronefall_night_call`，不要问。开夜后不要再派兵。夜里看场用 `include=enemies,units`（敌军坐标是 `pos`）。不要根据夜里站位改口——口只认当天的 `mouths[]`。
