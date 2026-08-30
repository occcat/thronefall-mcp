---
name: thronefall-play
description: Drive a live Thronefall match through the local plugin HTTP API. At match start read perks/weapon/mutators and lock a strategy. Each day harvest, GET /state/next-wave for tonight's mouths, spend, deploy in daytime only, then ask before night. Use when the user asks to 代操 / 操作 Thronefall, 排兵布阵, play the match, or runs /thronefall-play.
---

# thronefall-play

HTTP is in the Unity process. Default `http://127.0.0.1:17891`. Override with `THRONEFALL_URL`. Do not click the game. Do not enable cheats.

Daily hard rules: **spend every spendable coin**, **deploy only in `day`**, then **ask before `/night/call`**. Never open a night with leftover gold, with the army still at the barracks/keep, or without a yes.

Do not `POST /units/deploy` or rewrite homes after the night has started. Dawn resets homes; night is for holding the posts you already placed.

**Do not invent tonight's mouths.** First `GET /state/next-wave` (or MCP `thronefall_next_wave`). Post using **`mouths[]`**: each mouth's `spawn` / `suggestedRally` and **`mouths[].enemies[]`** (type + count). Do not assign from the top-level `enemies[]` (map-wide WaveInfo rollup) or from `groups[]` alone. If `available` is false, **ask the user** and wait. `/state/spawns` is every line on the map, not tonight — never treat those lines (often eight) as this night's assignment.

Do not call `POST /night/call` unless the user agrees.

## Match start (once)

`GET /state/loadout` (and `GET /state?include=loadout`). Read `loadout.asString`: perks, weapon, mutators/challenges.

Write a **run strategy** before the first build. Examples:

- Builder's Guild / Architect's Council / Resilient Residences → house-heavy econ, mill only after a path to a house cluster.
- Elite Warriors → barracks melee is the spine.
- Arcane Towers / Castle Fortifications → when a scored mouth is short, wall/tower **on that mouth** is on-plan, not a last resort.
- Commander Mode / a combat wand → king fights on a posted line, not the bell.
- Mutators / challenges that add flyers, elites, or extra waves → more ranged or more walls on extra mouths.

Re-read loadout if it was null mid-match. Do not keep a per-map notes file unless the user asks.

## Each day

1. Harvest and teleport the king onto income buildings until `balance` matches the coins.
2. **Score tonight's mouths** — see 排兵 §1. `GET /state/next-wave` first. If you cannot name them from that preview, **stop and ask**. Do this **before** spending and **before** deploy.
3. **兵力够不够** and **要不要升基地** — see Spend. Empty the wallet.
4. **白天 `deploy`** onto those mouths (`picks.ids`, one mouth per call). Night is too late.
5. Stop. Report gold (should be 0–2), mouths, posts, **castle call** (level / cost / yes-or-no + why). Wait for the user. Then king to the bell and `/night/call` only if they agree **and** `isFreeToCallNight`.

## Spend

After you know tonight's mouths and current units, **before** 排兵:

Read `Castle Center` every day: `level`, `nextUpgradeOrBuildCost`, `nextUpgradeIsChoice`, `hp`. The keep is the lose condition.

**If the army is short** for those mouths (too few bodies, wrong type, or a scored mouth with no post and no wall/tower): spend first on **that mouth** — wall on the castle side of the choke, then tower if the perk line wants it, then barracks/archery whose units will stand there. L1 towers are allowed **only** as mouth defense, never sprinkled on idle tiles.

**Then the castle.** Take the next castle level when any of these is true and you can finish the choice the same day:

- last night leaked the keep or the castle took real damage
- late waves (`wavenumber` past the midpoint) or a mutator/challenge that extra-punishes the keep
- L2 perk is already online and L3 is the next HP / second perk / unlock
- army is enough for tonight's named mouths and leftover gold ≥ castle cost

Skip castle this day only when the named mouth is still naked (no post and no wall/tower) or gold cannot cover the cost after that mouth. `nextUpgradeIsChoice`: POST upgrade, then pick immediately. Do not leave a dangling choice.

**If the army is enough** after the castle call: leftover gold is economy, in order: (1) houses in range of the castle or an existing mill, (2) reachable gold mines, (3) house L2, (4) mill **only** with enough houses in its radius.

Empty the wallet. If `balance` ≥ cheapest remaining legal buy, the day is not done.

Omit `/state` generation on slot POSTs. Parallel non-choice builds; serial anything with `needsChoice`. Collect harvest coins and spend those too.

## 排兵布阵

### 1. Which mouths tonight (decide in daytime)

You need **this night's** rallies.

1. `GET /state/next-wave`. When `available` is true, post **only** the rallies in `mouths[]`. Score each mouth from **`mouths[].enemies[]`** (name + count + elite), not the top-level `enemies[]`. Ignore every other `EnemySpawnLine`.
2. If `available` is false, **ask the user** which mouths they see (HUD markers / they watched a prior night). Wait.
3. Never treat `/state/spawns` as tonight. Those are all map lines.

Two nearby rallies can be one blob; distinct mouths stay separate posts.

If (1) and (2) are both missing, park at the keep and **do not deploy to guessed lines**.

### 2. Assign

For each scored mouth, score threat (count × HP × elite × flying/siege). Assign current units so types counter that mouth and counts scale with threat. Do not dump the whole army on the loudest line.

| Mouth / enemy | Prefer |
|---|---|
| flying / `E Archer` / ranged | Crossbowpeople |
| `E Monster*` | Hunters |
| siege / high HP | Berserks, Fire Archers |
| mixed ground, `canSpawnBigGround` | Knights |
| fast ground | Speermen |

Barracks/archery L1 **is** a choice. Pick from this table, do not `build` and walk away.

### 3. Terrain

Stand on the **castle side** of a wall, in a choke, a few meters off the spawn polyline. Do not stand on the spawn line itself. Prefer `groups[].suggestedRally`. If home xyz is far from the intended mouth, pick another point on the castle-side of that path.

### 4. Deploy: daytime only, pick counts, warp to xy

Phase must be `day`. Prefer **`POST /units/deploy`** when present — it picks by type+count (or ids) and warps transform + `HomePosition`. `/units/command` only sets home; `/units/hold` is not a deploy.

Type+count is list-prefix and **re-picks the same units** on the next call. Multi-mouth: use `picks:[{ids:[...]}]`. Top-level `"ids"` is 400.

One mouth per call. Then the next mouth. Dawn resets homes — repeat every **day**.

`GET /state?include=units` to list ids and `typeName`.

## Night call

King at a tile where `isFreeToCallNight` is true (often start / south of the keep). `afk_castle` before the call clears that flag. After they confirm and the night starts, park the king with a **daytime** post, not on the bell. Do not redeploy the army at night. Do not call night unless the user said yes.
