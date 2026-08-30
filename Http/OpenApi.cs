using System.Collections.Generic;
using ThronefallControl.Config;

namespace ThronefallControl.Http;

public static class OpenApi
{
    public const string Version = "3.0.3";

    public static object Document => Build();

    public static string ToJson() => Json.Serialize(Build());

    public static HttpResponse Response() =>
        new()
        {
            Status = 200,
            ContentType = "application/json; charset=utf-8",
            Body = ToJson()
        };

    static object Build()
    {
        return new Dictionary<string, object>
        {
            ["openapi"] = Version,
            ["info"] = new Dictionary<string, object>
            {
                ["title"] = "Thronefall Control",
                ["version"] = ThronefallControl.PluginInfo.Version,
                ["description"] = "Loopback HTTP actuator for Thronefall. Mutate routes are phase-gated; debug routes stay behind flags."
            },
            ["servers"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["url"] = $"http://{PluginConfig.BindAddress}:{PluginConfig.HttpPort}"
                }
            },
            ["paths"] = Paths(),
            ["components"] = new Dictionary<string, object>
            {
                ["schemas"] = new Dictionary<string, object>
                {
                    ["ErrorResponse"] = Schema(
                        "object",
                        new Dictionary<string, object>
                        {
                            ["ok"] = Schema("boolean"),
                            ["error"] = Schema("string"),
                            ["message"] = Schema("string"),
                            ["phase"] = Schema("string"),
                            ["generation"] = Schema("integer")
                        }),
                    ["ClientRequest"] = Schema(
                        "object",
                        new Dictionary<string, object>
                        {
                            ["clientRequestId"] = Schema("string"),
                            ["dryRun"] = Schema("boolean")
                        })
                },
                ["securitySchemes"] = new Dictionary<string, object>
                {
                    ["token"] = new Dictionary<string, object>
                    {
                        ["type"] = "apiKey",
                        ["in"] = "header",
                        ["name"] = "X-Thronefall-Token"
                    }
                }
            }
        };
    }

    static Dictionary<string, object> Paths()
    {
        return new Dictionary<string, object>
        {
            ["/health"] = new Dictionary<string, object>
            {
                ["get"] = Op("Health", "Process-alive health. Does not wait on the Unity main thread.")
            },
            ["/state"] = new Dictionary<string, object>
            {
                ["get"] = Op("GetState", "Full snapshot. Query include=slots,units,enemies,spawns,loadout,nextWave to clip arrays.")
            },
            ["/state/slots"] = new Dictionary<string, object> { ["get"] = Op("GetSlots", "Build slots. Legal in day/night/end_screen.") },
            ["/state/units"] = new Dictionary<string, object> { ["get"] = Op("GetUnits", "Player units. Legal in day/night.") },
            ["/state/enemies"] = new Dictionary<string, object> { ["get"] = Op("GetEnemies", "Enemy summary. Legal in day/night.") },
            ["/state/spawns"] = new Dictionary<string, object> { ["get"] = Op("GetSpawns", "All map spawn lines and suggested rallies. Not tonight's wave.") },
            ["/state/next-wave"] = new Dictionary<string, object> { ["get"] = Op("GetNextWave", "Tonight's wave preview. Use nextWave.mouths[].enemies for per-mouth types and counts. Legal in day/night. Read-only; does not place HUD markers.") },
            ["/state/loadout"] = new Dictionary<string, object> { ["get"] = Op("GetLoadout", "Current perk/weapon string. Legal in menu/level_select/day/night.") },
            ["/openapi.json"] = new Dictionary<string, object>
            {
                ["get"] = Op("GetOpenApi", "OpenAPI 3 document. Served on the HTTP thread.")
            },
            ["/harvest"] = Post("Harvest", "Harvest all or one slot. Legal in day."),
            ["/slots/{id}/build"] = Post("BuildSlot", "TryToBuildOrUpgradeAndPay. Legal in day."),
            ["/slots/{id}/upgrade"] = Post("UpgradeSlot", "Alias of build. Legal in day."),
            ["/slots/{id}/choice"] = Post("ChooseUpgrade", "Complete an upgrade branch. Legal in day."),
            ["/night/call"] = Post("CallNight", "SwitchToNight when IsFreeToCallNight. Legal in day. Does not skip waves."),
            ["/night/policy"] = Post("SetNightPolicy", "Set and immediately run a night policy. human: no combat. afk_castle: teleport king to castle. scripted_posts: intent-only until the units worker owns dispatch — this call does not post units. Legal in day/night."),
            ["/units/command"] = Post("CommandUnits", "Send units to a world point. Legal in day/night."),
            ["/units/hold"] = Post("HoldUnits", "Hold position. Legal in day/night."),
            ["/units/follow"] = Post("FollowKing", "FollowPlayer. Legal in day/night."),
            ["/units/groups"] = Post("SetControlGroup", "Assign group 1/2/3. Legal in day/night."),
            ["/units/send-to-spawn"] = Post("SendToSpawn", "Type T onto spawn line L. Legal in day/night."),
            ["/path/toggle"] = Post("TogglePath", "Toggle a path cutter."),
            ["/king/teleport"] = Post("TeleportKing", "Teleport to castle, start, or coordinates. Never MakeInvulerable. Legal in day/night/level_select."),
            ["/loadout/select"] = Post("SelectLoadout", "Pick a live TFUIEquippable by Data.displayName and kind. Legal in level_select."),
            ["/level/start"] = Post("StartLevel", "LevelInteractor.InteractionBegin(PlayerInteraction.instance) then PlayButtonPressed. Legal in level_select. Does not LoadScene."),
            ["/debug/upgrade-max"] = Post("DebugUpgradeMax", "DEBUGUpgradeToMax. Requires EnableDebugUpgradeToMax."),
            ["/debug/skip-wave"] = Post("DebugSkipWave", "EnemySpawner.DebugSkipWave. Requires EnableDebugCheats."),
            ["/debug/invulnerable"] = Post("DebugInvulnerable", "Hp.MakeInvulerable. Requires EnableDebugCheats."),
            ["/debug/save"] = Post("DebugSave", "LocalMatchSaveLoad.Save. Requires AllowSaveApi.")
        };
    }

    static Dictionary<string, object> Post(string operationId, string summary) =>
        new() { ["post"] = Op(operationId, summary, write: true) };

    static Dictionary<string, object> Op(string operationId, string summary, bool write = false)
    {
        var op = new Dictionary<string, object>
        {
            ["operationId"] = operationId,
            ["summary"] = summary,
            ["responses"] = new Dictionary<string, object>
            {
                ["200"] = new Dictionary<string, object> { ["description"] = "OK" },
                ["401"] = new Dictionary<string, object> { ["description"] = "unauthorized" },
                ["403"] = new Dictionary<string, object> { ["description"] = "cheat_disabled" },
                ["404"] = new Dictionary<string, object> { ["description"] = "not_found" },
                ["409"] = new Dictionary<string, object> { ["description"] = "illegal_phase / stale_id / transition_in_progress" }
            }
        };
        if (write)
        {
            op["requestBody"] = new Dictionary<string, object>
            {
                ["required"] = false,
                ["content"] = new Dictionary<string, object>
                {
                    ["application/json"] = new Dictionary<string, object>
                    {
                        ["schema"] = new Dictionary<string, object>
                        {
                            ["$ref"] = "#/components/schemas/ClientRequest"
                        }
                    }
                }
            };
        }

        return op;
    }

    static Dictionary<string, object> Schema(string type, Dictionary<string, object>? properties = null)
    {
        var schema = new Dictionary<string, object> { ["type"] = type };
        if (properties != null)
            schema["properties"] = properties;
        return schema;
    }
}