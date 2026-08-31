---
name: thronefall-play
description: 代操 Thronefall。战略判断局面，战术走已发布 HTTP / MCP。做选择时重点读 references/codex.md（含地图）。召夜前按 loadout 武器和今晚 mouths 告诉用户国王怎么打。用户说代操、排兵布阵、图鉴、play the match 或 /thronefall-play 时使用。
---

# thronefall-play

插件 HTTP 在 Unity 进程里。MCP 是 `Clients/mcp/server.py` 的 stdio 代理。默认 `http://127.0.0.1:17891`，本机对局常用 `http://127.0.0.1:17892`（`THRONEFALL_URL`）。

建筑、兵种、怪物、挑战、perk、武器、**地图**在 `references/codex.md`。费用以 live `nextUpgradeOrBuildCost` 为准；文案和 live `tooltip` / `nextUpgradeLabel` 冲突时听 live。

原则按**威胁怎么走、钱怎么复利、权能改了什么**来用，不按上一把的图名、夜号、坐标套。图鉴只提供这张图有什么经济、几夜、有没有 Boss；当晚口和费用只认 live。

# 战略

城堡倒了或国王死了就输。金币是后面所有选择的上限。收入建筑被拆，等于丢掉后面每一夜的复利。

每一天只判断三件事：今晚的威胁在哪、现有力量能不能活过今夜、活下来之后收入够不够撑后面的夜。和战术冲突时：先让今夜在没人操控国王时能活。

## 今晚能不能活

现有城堡、已派的兵、已经盖好的塔/墙。**不要把国王算进去**（用户可能挂机）。无兵契约是唯一必须把国王算进守力的情况，那种局更要早堆塔墙。

- **兵营/箭塔买了当天就有人。** 一级选兵立刻 +4，再升一级再 +4，当天就能派。不要按「还在训练」把军事推后。`training` 只表示死人补员（精英兵补得慢），不是新建要等一夜。

- **同时在场看 `interval` / `delay`，不看 `count`。** `mouths[].count` 是整晚配额。按总数去堆今晚战力会早爆兵、抽空收入，后期没钱。人少高伤（Commander / Elite 一类）打的是当前这一撮，不是配额全表。
- **类型对不上等于盖不住。** 近战打不到飞、猎人打人类没加成，人头再多也要先换兵种或换岗，再谈加人数。
- **经济优先不是零防。** 现有力量盖不住今晚这一撮，先买最小能活的（对口一座兵营、路障、该口一座塔），剩钱再给收入。城堡加固、国王向 perk 都不等于前几夜可以空城。
- **兵力够就投资，不够就出兵/防御/点塔。** 盖得住：钱给磨坊/田/房/矿/港。盖不住：先买对口的兵和该口上的防。不要为了花光去堆用不上的墙塔，也不要在类型已经对上、人数已经盖住时继续加兵。

## 活下来之后钱给谁

- **守得住才扩张。** 护不住的田、港、矿先别造。同一类收入建筑按「今晚会不会被拆」分别判断，不要把三座港或三座矿当一件事。房子通常最安全（靠城堡、两夜回本）。
- **磨坊是有田的图上 ROI 最高的一档。** 一级弱，改良犁升到 L2/L3 才是大头，田 1 金 +1 跟在后面。第一座磨坊升满、田扫完之后，下一座**今晚盖得住**的磨坊优先于再买塔、再升兵营。奥术塔/城堡加固改的是「最少要几座塔」，不是把钱从磨坊挪走。南口当夜有地面潮可以先开反方向那座，不要因此整晚只留一座。
- 矿前高后低，能摸到且今晚盖得住就早开。港口一级往往不值，升级后才是大头。
- **能换口的力量优先于焊死的力量。** 兵能换口，塔不能。墙会让敌人堆成团，没有更好选项再造。塔补已经有人仍盖不住的那一口，或护一块收入，不要每个新口种一座。
- **Loadout 是本局约束，必须读 catalog 全文。** 对 `asString` 每一项对着 `catalog[].description` 写成约束再买东西，不要只拿名字当标签。人少高伤 → 先经济后加兵；少收尸金 → 更早靠建筑收入；房子能扛/拆了续税 → 可更敢铺房；塔或城堡被加强 → 改的是它们够不够，不是默认再买一座兵营。契约直接删掉对应方案。Warrior / 国王向 perk 仍然按兵和建筑能活来布。
- 不要套固定建造链。轻的夜把经济或关键升级一次做到位。
- **够打也要把当前能买、回得来的收入扫完再停。** 「战力够了」不是停手理由。漏掉 1 金田/房等于后面每夜少收。能开的第二、第三座磨坊也算这里，不要用「先再补一座塔」把它挤掉。

## 开夜前把钱花完

买一轮 → `GET /state` 看 `balance` 和还能升级的格子 → 再买 → 再查，至少两轮。**默认花完。** 只有写得出战略理由才许留金（Interest、明天城堡档、明确的下一夜大件）。「好像够了」不是理由。没有 Interest 时剩 10+ 就继续扫田/房/**还能护住的磨坊升级**，塔只在今晚盖不住时再买。直到买不起下一档有用的东西。

**终夜不做投资。** `finalWaveComingUp` 或今晚是最后一波时，钱只给出兵、路障、墙、塔。不买房/田/磨坊/矿/港。循环花到买不起下一档防或兵。

# 战术

只守今晚 `mouths[]`。没有 `mouths[]` 不要派到猜的线上。很近的两条集结当一堆；分开的口各守一岗。按口分兵，不要全军倒最响那条。上一口差点爆，也不能把所有岗缩回家——上一口的恐惧不能盖住今晚的口。

落点按**这口的敌人怎么走**选，不按上一把同一坐标的记忆。`suggestedRally` 默认是地面堵口点；水上、`(0,0,0)`、或其它走不到的点作废，改用该路城堡侧陆地。

## 落点

先看这口敌人的行为，再落点：

| 行为 | 怎么认 | 近战 | 远程 |
|---|---|---|---|
| 沿路推进、会踩经济 | 地面步行、攻城、弓弩、人海 | 该口 `suggestedRally` 堵口，不要收到经济边上 | 近战身后、仍在该路上，不要贴城堡 |
| 绕开经济直取城堡 | `E Racer` 及同类 | 城堡接近路 / 隘口，不要跟到很远的 rally | 同左，仍在进城路上 |
| 拆经济、一只只出 | `E Flyer` / 黄蜂及同类 | 不用去堵 spawn | 散到今晚要护的收入旁（已开的港/矿/房），不要堆成一坨堵口 |
| 只追国王 | `E Hunterling` 及同类 | 兵不用去这口 | 同左 |
| Boss / 假口 | rally 无效或口名是 Boss | 收在城堡周围拦刷怪，别去 Boss 本体 | 清潮优先于磨 Boss |

远程站墙后、近战后。不要派到出兵折线上「用身体护墙」。本局 `"hold": false`，他们会追出去，出生点更要靠后。飞的不吃墙，对空远程站得能射到航线即可，不必站墙外。

home 被吸飞了，另挑**同一条行为对应的路**，不要统一收回城堡。

## 对口

对照图鉴「对口」和当晚 `mouths[].enemies[]`。认标签，不认上一把用过的兵种组合。

- `E Ogre` 是人类，猎人无效；骑士顶。
- 猎人打怪物/坐骑，不打人。
- 火焰弓手是溅射打攻城和抱团，不是只打飞。骑士打不到飞。
- 爆破者冲建筑：用肉塔/墙吸，不必全员堆一只上。
- 投石机优先拆；近战去砸，远程隔空不够。
- 敌方弓在经济口：近战必须顶在田/磨坊/港前面，不能用「保城堡」把岗收到家里。
- Boss 夜优先清潮。能打 Boss 的塔留最少量；刷怪点对着清潮塔，不要只围城堡。
- 墙挡地面，也帮对面堆成团。远程可隔墙射，敌方弓也能隔墙拆房子。幽灵穿墙，鼹鼠挖隧道，飞的不走门。

兵会自己找怪，也会自己送。远程没人挡就会贴脸；近战会追出岗位。Boss 夜预备队收在城堡后，别提前冲去自杀。

# 操作

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

`units/deploy` 按 `typeName`+`count` 会反复抽同一批人；分口必须 `selector.ids`。`GET /state?include=enemies` 的敌军是 `{count, units[]}`，坐标字段是 `pos`。

## 信息分析

开局读 loadout：`asString`、`catalog[]`、`quests[]`、`worth`、`perkPointsRemaining`。中途变 null 就重读。**权能没对着 catalog 全文写成约束，就先别买东西。** 对照图鉴「地图」看这张图有没有田/港/矿、几夜、有没有 Boss、开局金和前几夜是否 0 波金。

每天先 `thronefall_harvest`，直到 `economy.balance` 对上该收的钱。再 `thronefall_next_wave`。HTTP 看 `nextWave.available` / `nextWave.mouths[]`；MCP 还会抄到顶层。**只看 `mouths[]`**：`spawn`、`suggestedRally`、`mouths[].enemies[]`，以及每组的 `interval` / `delay`。不要用顶层 `enemies[]` 或扁的 `groups[]`。不要编口。`/state/spawns` 是全图线，不是今晚。`available=false`：城堡未建就先建再读；仍 false 则兵停城堡，不要问 HUD。

也读 `clock`（`wavenumber` / `waveCount` / `finalWaveComingUp` / `currentScore`）和 `training`（`hasKnockedOut`、`timeTillNextRespawn`；还在出的兵算进今夜力量）。格子看 `tooltip` / `nextUpgradeLabel` / `choices` / `goldIncome` / `canBeUpgraded` / `hp`。挡住就 `thronefall_path_toggle`。

一级塔造完会 `isWaitingForChoice`。`choices[]` 常残留错名，听 `nextUpgradeLabel`。L2：`Castle Tower` / `Sniper Tower` / `Armored Tower` / `Bunker Tower`。L3：`Archers Spire` / `Ballistic Spire` / `Fire Spire` / `Healing Spire`。`choice` 的 dryRun 不校验名字。

## 工作循环

1. 收税，读今晚 `mouths[]`、钱、建筑、兵、训练、**当前 loadout 每条权能的 catalog 全文**、地图图鉴里后面几晚的压力。权能没展开就先别决定买什么。看 `clock.finalWaveComingUp` / `waveNumber==waveCount`：终夜跳过所有收入建筑。
2. 按战略决定买什么：城堡是失败条件，先看它的 `hp` / `level` / 下一档。终夜只买兵和防。L1 兵营/箭塔/英雄营用 choice 英文名选兵（`Knights`、`Fire Archers`、`Hunters`…）。`needsChoice` 立刻 `POST /slots/{id}/choice`，选错了 cancel，不要留下未选。无 choice 可并行。类型对不上就换兵种或换岗。买完或升完兵营/箭塔，当天这 4 人算进今夜力量，立刻一并派出去。
3. **只在 `day` 派兵，按战术落点。** 多口、要点名：`include=units` 取 id，再 `thronefall_units_command`（`selector.ids` + `target` + `"hold": false`）。整类上一口：`thronefall_units_send_to_spawn`。按数量落点：`thronefall_units_deploy`（只用于单点；分口不要用 count）。分组用 `POST /units/groups`。`settings.resetUnitFormationEveryMorning` 为 true 时每个白天重派。
4. 写两三句国王建议（见下）。**先查余额并循环花钱**（见战略），再 `thronefall_night_call`。不要问用户要不要召夜。白天即可召。开夜后不要再派兵，夜里不要改 Home。夜里看场用 `include=enemies,units`。口只认当天的 `mouths[]`。
5. **夜结束后写 `.experience/`。** `phase` 回到 `day` 或变成 `end_screen` 时，把教训写到本 skill 目录的 `.experience/`（不要提交；已在仓库 `.gitignore`）。文件名用当地时间 `YYYY-MM-DD-HHMM.md`；同一把对局追加到该文件，换一把再开新文件。开局先扫该目录。只写以后换图还能用的判断：威胁类型、花钱对错、落点规则、接口坑。不要写对局流水账，不要把某张图的坐标写成下次默认岗。下一白天先扫一遍再决策。

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
- 今晚有飞：远程武器去打飞，近战别管
- 今晚有直取城堡的快怪：矛去隘口拦
- 今晚有精英 / 高血 / Boss：点名最高血
- `Challenge the Falcon God`：优先拦快的
- `Challenge the Elite God`：优先点精英
