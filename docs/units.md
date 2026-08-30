# 单位派遣

夜间守线走 `POST /units/command`、`/units/deploy`、`/units/hold`、`/units/follow`、`/units/groups`、`/units/send-to-spawn`。合法 phase 只有 `day` / `night`；切场景时 409 `transition_in_progress`。

## command：WarpTo，响应仍报 `path=fallback`

`POST /units/command` 默认走 **WarpTo**（配置 `UseCommandUnitsSolver=false`）：

1. `WarpTo(GetNearestGroundPosition(target))`
2. `FollowPlayer(false)`
3. 按请求设 `HoldPosition`

响应里的 `path` 仍是 `"fallback"`，不是 `"warp"`。这不求解单位间距，但能把兵瞬移到世界坐标。请求里 `useSolver=true` 而配置仍为 false 时也走这条路，不会调用 `PlaceCommandedUnitsAndCalculateTargetPositions`。

配置打开 solver 后，会把单位填进 `CommandUnits.playerUnitsCommanding`（不调用 private `TryToSelectUnits`），把 `CommandUnits` 的 transform 移到目标，再调公开的 `PlaceCommandedUnitsAndCalculateTargetPositions(false)`。solver 成功则 `path=solver`，失败则回退 WarpTo 并仍报 `path=fallback`。

控制组 1/2/3 对应 `TagManager.ETag.Group1/2/3`。`send-to-spawn` 在 `EnemySpawnLine` 折线（transform 子节点）上取朝城堡最近的点；附近有 `ETag.Wall` 时沿城堡→出生线再外推 `WallBackOffset`（默认 3 m）。

## deploy：按 picks 瞬移

`POST /units/deploy` 已发布（HTTP；MCP 尚未单独包此工具）：

```json
{
  "clientRequestId": "d1",
  "picks": [{ "typeName": "P Knight", "count": 4 }],
  "target": { "x": 12.0, "y": 0.0, "z": -3.0 },
  "hold": true,
  "spacing": 2
}
```

- `picks`：按 `typeName`+`count` 或 `ids` 点名。
- `target`：世界坐标。
- `hold`：API 默认 `true`。
- `spacing`：默认 `2`，沿 X 排开后再 `WarpTo`。
- 成功时响应 `path=warp`。

`hold` 在 `/units/command`、`/units/deploy`、`/units/send-to-spawn` 上 **API 默认 `true`**。对局可以显式传 `"hold": false`（skill 对局就是这样做）。

## 早晨阵型重置

游戏设置 `Gameplay_ResetUnitFormationEveryMorning` / `SettingsManager.ResetUnitFormationEveryMorning`，状态字段是 `settings.resetUnitFormationEveryMorning`。插件只回传，不擅自改，也没有 `POST /settings/reset-units-morning`。

`PathfindMovementPlayerunit.OnDawn_BeforeSunrise`：若该设置为 true，黎明会把 `HomePosition` 还原成 `homePositionOriginal`，并 `HoldPosition=false`。**开启早晨重置时，Hold 不能把夜间岗位留过白天**，需要每天重新派遣，或关掉该设置。

关闭早晨重置时，夜间岗位可以留到白天；此时必须 `HoldPosition=true`，否则单位会 `FollowPlayer` 或离开 Homing 点。
