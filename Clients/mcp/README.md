# Thronefall MCP stdio proxy

A Python 3 **stdlib** MCP server. It speaks JSON-RPC on stdin/stdout and
forwards tool calls to the BepInEx plugin at `http://127.0.0.1:17891`.

The MCP process **starts even if Thronefall is not running**. Tool calls then
return structured JSON instead of crashing the session:

```json
{"ok":false,"error":"game_not_running","message":"cannot reach http://127.0.0.1:17891: ..."}
```

No pip packages. Tools map to published HTTP routes. How to play a match (what to buy, where to stand, `hold: false`, no king teleport) is [`.grok/skills/thronefall-play/SKILL.md`](../../.grok/skills/thronefall-play/SKILL.md).

## Run

```bash
python3 Clients/mcp/server.py
```

| Environment | Default | Meaning |
| --- | --- | --- |
| `THRONEFALL_URL` | `http://127.0.0.1:17891` | Plugin base URL. Set this if `HttpPort` is not 17891. |
| `THRONEFALL_TOKEN` | empty | Sent as `X-Thronefall-Token` |
| `THRONEFALL_TIMEOUT` | `2.5` | HTTP timeout in seconds |

Logs go to stderr. stdout is JSON-RPC only.

## Grok `config.toml`

User-wide: `~/.grok/config.toml`. Project-wide: `.grok/config.toml`.

```toml
[mcp_servers.thronefall]
command = "python3"
args = ["/ABS/PATH/to/thronefall-mcp/Clients/mcp/server.py"]
env = { THRONEFALL_URL = "http://127.0.0.1:17891" }
startup_timeout_sec = 10
tool_timeout_sec = 30
```

If the plugin config has `AuthToken` set:

```toml
[mcp_servers.thronefall]
command = "python3"
args = ["/ABS/PATH/to/thronefall-mcp/Clients/mcp/server.py"]
env = { THRONEFALL_URL = "http://127.0.0.1:17891", THRONEFALL_TOKEN = "secret" }
startup_timeout_sec = 10
tool_timeout_sec = 30
```

Equivalent CLI:

```bash
grok mcp add thronefall -- python3 /ABS/PATH/to/thronefall-mcp/Clients/mcp/server.py
```

Replace `/ABS/PATH/to/thronefall-mcp` with the clone root. Then
`grok mcp doctor thronefall`.

## Tools

14 tools (see `TOOLS` in `server.py`):

| Tool | HTTP |
| --- | --- |
| `thronefall_health` | `GET /health` (`ready=true` → `GET /health/ready`) |
| `thronefall_state` | `GET /state` (`include` query) |
| `thronefall_next_wave` | `GET /state/next-wave` |
| `thronefall_harvest` | `POST /harvest` |
| `thronefall_slot_upgrade` | `POST /slots/{id}/upgrade` |
| `thronefall_slot_choice_cancel` | `POST /slots/choice/cancel` |
| `thronefall_night_call` | `POST /night/call` |
| `thronefall_units_command` | `POST /units/command` |
| `thronefall_units_send_to_spawn` | `POST /units/send-to-spawn` |
| `thronefall_units_deploy` | `POST /units/deploy` |
| `thronefall_path_toggle` | `POST /path/toggle` |
| `thronefall_king_teleport` | `POST /king/teleport` |
| `thronefall_loadout_select` | `POST /loadout/select` |
| `thronefall_level_start` | `POST /level/start` |

`dryRun=true` is forwarded as `?dryRun=true`. Missing `clientRequestId` is filled in.

`thronefall_units_deploy` maps to `POST /units/deploy` (picks / target / hold / spacing=2). `typeName`+`count` re-picks the same squad; split mouths with `ids`.

`hold` on command / deploy / send-to-spawn defaults to `true` in the HTTP DTO. A play loop must send `"hold": false`.

`thronefall_king_teleport` is on the wire. The play skill does not call it.
