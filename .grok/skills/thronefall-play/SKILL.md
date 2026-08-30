---
name: thronefall-play
description: 代操 Thronefall。战略判断局面，战术走已发布 HTTP / MCP。做选择时重点读 references/codex.md（含地图）。召夜前按 loadout 武器和今晚 mouths 告诉用户国王怎么打。用户说代操、排兵布阵、图鉴、play the match 或 /thronefall-play 时使用。
---

# thronefall-play

插件 HTTP 在 Unity 进程里。MCP 是 `Clients/mcp/server.py` 的 stdio 代理。默认 `http://127.0.0.1:17891`，本机对局常用 `http://127.0.0.1:17892`（`THRONEFALL_URL`）。

建筑、兵种、怪物、挑战、perk、武器、**地图**在 `references/codex.md`。费用以 live `nextUpgradeOrBuildCost` 为准；文案和 live `tooltip` / `nextUpgradeLabel` 冲突时听 live。

# 战略

城堡倒了或国王死了就输。金币是后面所有选择的上限。收入建筑被拆，等于丢掉后面每一夜的复利。

每一天只判断三件事：今晚的威胁在哪、现有力量能不能活过今夜、活下来之后收入够不够撑后面的夜。答案从当前对局状态和 `references/codex.md`（先地图，再对口/建筑/兵种）里读。

社区和 wiki 的共识，用来想，不拿来当购物清单：

- **守得住才扩张经济。** 护不住的田、港、矿先别造，攒到能护再造。房子通常最安全（靠城堡、两夜回本）。磨坊/港口要算回本期，一级往往不值，升级后才是大头。矿前高后低，能摸到就早开。**不要把「国王去扛前几夜」算进守得住**——用户可能挂机，见常识。
- **能换口的力量优先于焊死的力量。** 兵能换口，塔不能。墙会让敌人堆成团，没有更好选项再造。塔补已经有人仍盖不住的那一口，或护一块收入，不要每个新口种一座。
- **Loadout 是本局约束。** Commander 必须靠兵和塔独自赢。Warrior / 国王向 perk 仍然按兵和建筑能活来布，不当作用户会输出。契约（无墙/无塔/无兵/和平）直接删掉对应方案。蛇神少收尸金，更靠建筑收入。
- 不要套固定建造链，不要为了花光而花。轻的夜用来把经济或关键升级一次做到位。自己决定白天什么时候结束。

# 战术

不要点游戏，不要开作弊，不要打 `/debug/*`。不要瞬移国王（不调 `thronefall_king_teleport` / `/king/teleport`，不用 `afk_castle`，建造不带 `teleportKingNearby`）。`POST /night/policy` 只用 `human`。Slot POST 不要带 `/state` 的 generation。不要写对局流水账，除非用户要。

优先走 MCP。没有对应工具的路径用 HTTP。

| 工具 | HTTP | 用途 |
|---|---|---|
| `thronefall_health` | `GET /health` | 进程是否活着 |
| `thronefall_state` | `GET /state?include=` | 快照 |
| `thronefall_next_wave` | `GET /state/next-wave` | 今晚每口 |
| `thronefall_harvest` | `POST /harvest` | 收税 |
| `thronefall_slot_upgrade` | `POST /slots/{id}/upgrade` | 建造/升级 |
| `thronefall_slot_choice_cancel` | `POST /slots/choice/cancel` | 取消未完成的选择 |
| `thronefall_night_call` | `POST /night/call` | 召夜 |
| `thronefall_units_command` | `POST /units/command` | 单位到世界点 |
| `thronefall_units_send_to_spawn` | `POST /units/send-to-spawn` | 按类型派到 spawn 集结 |
| `thronefall_units_deploy` | `POST /units/deploy` | 按种类/数量到世界点 |
| `thronefall_path_toggle` | `POST /path/toggle` | 开路器 |
| `thronefall_loadout_select` | `POST /loadout/select` | 选装备 |
| `thronefall_level_start` | `POST /level/start` | 选图开局 |

不用：`thronefall_king_teleport`。HTTP 另有 `POST /slots/{id}/choice`、`POST /units/groups`。单位命令一律显式 `"hold": false`。不要 `/units/hold`，不要 `/units/follow`。

## 信息分析

开局读 loadout：`asString`、`catalog[]`、`quests[]`、`worth`、`perkPointsRemaining`。中途变 null 就重读。对照图鉴「地图」看这张图有没有田/港/矿、几夜、有没有 Boss、开局金和前几夜是否 0 波金。

每天先 `thronefall_harvest`，直到 `economy.balance` 对上该收的钱。再 `thronefall_next_wave`。HTTP 看 `nextWave.available` / `nextWave.mouths[]`；MCP 还会抄到顶层。**只看 `mouths[]`**：`spawn`、`suggestedRally`、`mouths[].enemies[]`。不要用顶层 `enemies[]` 或扁的 `groups[]`。不要编口。`/state/spawns` 是全图线，不是今晚。`available=false`：城堡未建就先建再读；仍 false 则兵停城堡，不要问 HUD。

也读 `clock`（`wavenumber` / `waveCount` / `finalWaveComingUp` / `currentScore`）和 `training`（`hasKnockedOut`、`timeTillNextRespawn`；还在出的兵算进今夜力量）。格子看 `tooltip` / `nextUpgradeLabel` / `choices` / `goldIncome` / `canBeUpgraded` / `hp`。挡住就 `thronefall_path_toggle`。

对照图鉴「对口」和当晚 `mouths[].enemies[]` 判断类型是否对得上。`E Ogre` 是人类。飞兵常见 `E Flyer`。火焰弓手是溅射打攻城，不是只打飞。

## 工作循环

1. 收税，读今晚 `mouths[]`、钱、建筑、兵、训练、地图图鉴里后面几晚的压力。
2. 按战略决定买什么：城堡是失败条件，先看它的 `hp` / `level` / 下一档。L1 兵营/箭塔/英雄营用 choice 英文名选兵（`Knights`、`Fire Archers`、`Hunters`…）。`needsChoice` 立刻 `POST /slots/{id}/choice`，选错了 cancel，不要留下未选。无 choice 可并行。类型对不上就换兵种或换岗。
3. **只在 `day` 派兵，只守 `mouths[]`。** 很近的两条集结当一堆；分开的口各守一岗。没有 `mouths[]` 不要派到猜的线上。近战站墙的城堡一侧、隘口里，用该口 `suggestedRally`；远程再往后、墙后或近战身后（见常识）。不要站在出兵线上。home 吸飞了就另挑该路城堡侧。按口分兵，不要全军倒最响那条。
   - 多口、要点名：`include=units` 取 id，再 `thronefall_units_command`（`selector.ids` + `target` + `"hold": false`）。
   - 整类上一口：`thronefall_units_send_to_spawn`（`typeName` + `spawnId` + `"hold": false`）。
   - 按数量落点：`thronefall_units_deploy`（`picks` + `target` + `"hold": false`）。
   - 分组用 `POST /units/groups`。`settings.resetUnitFormationEveryMorning` 为 true 时每个白天重派。
4. 写两三句国王建议（见下），立刻 `thronefall_night_call`。不要问用户要不要召夜。白天即可召。开夜后不要再派兵，夜里不要改 Home。夜里看场用 `include=enemies,units`（敌军坐标是 `pos`）。口只认当天的 `mouths[]`。

## 国王建议

国王由用户操控。不要替用户走、砍、放技能，也不要瞬移国王。召夜前仍按武器 / perk / `mouths[]` 说两三句，立刻召夜。**建议是给可能在看的人听的，布阵和花钱必须当他挂机。** 细则看图鉴「武器」。

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

有就改建议：

- `Commander Mode`：国王别当主力，去补漏或站 Power Tower 旁边
- `Warrior Mode`：国王去今晚最响的口输出，兵只挡
- `Pacifist Pact`：今晚不要攻击
- `Glass Canon` / `Risk Taker`：别硬换血
- `Heavy Armor` / `Ring of Resurection`：可以去挡一下关键口
- `Power Tower`：站在要强化的那座塔旁边
- `War Horse`：可以撞，会自伤
- 今晚有投石机：近战武器去拆，弓别隔空射
- 今晚有 `E Flyer` / 飞：远程武器去打飞，近战别管
- 今晚有 `E Racer` / 快：矛去隘口拦
- 今晚有精英 / `E Ogre` / Boss：点名最高血
- `Challenge the Falcon God`：优先拦快的
- `Challenge the Elite God`：优先点精英

# 常识

从社区攻略和游戏机制里抽出的、每天都用的判断。和战略冲突时：先让今夜在没人操控国王时能活。

- **远程站墙后、近战后。** 墙和近战挡路，弓弩从后面射。Steam 和论坛里最常见的翻车是远程自己冲出城门/墙外去「用身体护墙」。落点放在城堡一侧、墙后或近战身后，不要派到出兵折线上。本局 `"hold": false`，他们会追出去，所以出生点更要靠后。飞兵不吃墙，对空远程仍要站得能射到飞的航线，但不必站到墙外。
- **用户可能挂机，不要指望他。** 国王建议可以写，但不能当输出、挡枪、拆投石机、站 Power Tower 或风筝。判断「今晚能不能活」只数建筑、已派的兵和还在训练的人。Commander 本来就靠军队；Warrior、重甲、战马也不要当成有人在打。无兵契约才是唯一必须把国王算进去的情况，那种局更要早堆塔墙。
- **兵会自己找怪，也会自己送。** 自动索敌。远程没人挡就会贴脸；近战会追出岗位。Boss 夜把预备队收在城堡后，别让他们提前冲去自杀（Frostsee 水中之影攻略）。
- **飞速怪只认城堡。** `E Racer` 会绕开房子和矿直取城堡。城堡前或隘口必须有人，不能只守经济。
- **墙挡地面，也帮对面堆成团。** 远程可以隔墙射；敌方弓弩也能隔墙拆房子，墙后经济不是绝对安全。幽灵穿墙。鼹鼠挖隧道。飞的不走门。
- **一种建筑护不住所有口。** 塔不能换口。轻的夜把钱留给后面，重的夜先保证类型对得上。
