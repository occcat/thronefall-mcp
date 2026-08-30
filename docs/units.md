# 单位派遣

夜间守线走 `POST /units/command`、`/units/hold`、`/units/follow`、`/units/groups`、`/units/send-to-spawn`。合法 phase 只有 `day` / `night`；切场景时 409 `transition_in_progress`。

## 双路径

默认 **HomePosition fallback**（配置 `UseCommandUnitsSolver=false`）：

1. `HomePosition = GetNearestGroundPosition(target)`
2. `SnapToNavmesh()`
3. `FollowPlayer(false)`，再按请求设 `HoldPosition`

这不求解单位间距，但能把兵派到世界坐标并站住。请求里 `useSolver=true` 而配置仍为 false 时也走这条路，不会调用 `PlaceCommandedUnitsAndCalculateTargetPositions`。

配置打开 solver 后，会把单位填进 `CommandUnits.playerUnitsCommanding`（不调用 private `TryToSelectUnits`），把 `CommandUnits` 的 transform 移到目标，再调公开的 `PlaceCommandedUnitsAndCalculateTargetPositions(false)`。`toBePlaced` 在游戏里是待放置单位列表，不是目标坐标。solver 失败则回退 fallback。

控制组 1/2/3 对应 `TagManager.ETag.Group1/2/3`。`send-to-spawn` 在 `EnemySpawnLine` 折线（transform 子节点）上取朝城堡最近的点；附近有 `ETag.Wall` 时沿城堡→出生线再外推 `WallBackOffset`（默认 3 m）。

## 早晨阵型重置

游戏设置 `Gameplay_ResetUnitFormationEveryMorning` / `SettingsManager.ResetUnitFormationEveryMorning`。插件不擅自改它。

`PathfindMovementPlayerunit.OnDawn_BeforeSunrise`：若该设置为 true，黎明会把 `HomePosition` 还原成 `homePositionOriginal`，并 `HoldPosition=false`。**开启早晨重置时，Hold 不能把夜间岗位留过白天**，需要每天重新派遣，或关掉该设置。

关闭早晨重置时，夜间岗位可以留到白天；此时必须 `HoldPosition=true`，否则单位会 `FollowPlayer` 或离开 Homing 点。派遣守线时始终带 `"hold": true`。
