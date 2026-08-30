#!/usr/bin/env python3
"""Stdio MCP proxy for the Thronefall loopback HTTP plugin.

Talks JSON-RPC on stdin/stdout and forwards tools to
http://127.0.0.1:17891. The process starts even when the game is down;
tool calls then return {"error":"game_not_running"}.
"""

from __future__ import annotations

import json
import os
import socket
import sys
import uuid
import urllib.error
import urllib.parse
import urllib.request
from typing import Any

SERVER_NAME = "thronefall-mcp"
SERVER_VERSION = "0.1.0"
DEFAULT_URL = "http://127.0.0.1:17891"
DEFAULT_TIMEOUT = "2.5"
TOKEN_HEADER = "X-Thronefall-Token"

_use_content_length = False


def log(message: str) -> None:
    sys.stderr.write(message + "\n")
    sys.stderr.flush()


def base_url() -> str:
    return os.environ.get("THRONEFALL_URL", DEFAULT_URL).rstrip("/")


def request_timeout() -> float:
    raw = os.environ.get("THRONEFALL_TIMEOUT", DEFAULT_TIMEOUT)
    try:
        return max(0.1, float(raw))
    except ValueError:
        return float(DEFAULT_TIMEOUT)


def auth_token() -> str:
    return os.environ.get("THRONEFALL_TOKEN", "")


def game_not_running(reason: object) -> dict[str, Any]:
    return {
        "ok": False,
        "error": "game_not_running",
        "message": f"cannot reach {base_url()}: {reason}",
    }


def decode_body(raw: str, status: int) -> dict[str, Any]:
    text = (raw or "").strip()
    if not text:
        return {"ok": status < 400, "status": status}
    try:
        parsed = json.loads(text)
    except json.JSONDecodeError:
        return {"ok": False, "error": "invalid_json", "message": text, "status": status}
    if isinstance(parsed, dict):
        parsed.setdefault("status", status)
        return parsed
    return {"ok": status < 400, "status": status, "body": parsed}


def http_json(
    method: str,
    path: str,
    query: dict[str, Any] | None = None,
    body: dict[str, Any] | None = None,
) -> dict[str, Any]:
    if not path.startswith("/"):
        path = "/" + path
    url = base_url() + path
    if query:
        encoded = urllib.parse.urlencode(
            {k: v for k, v in query.items() if v is not None and v != ""}
        )
        if encoded:
            url = url + "?" + encoded

    headers = {"Accept": "application/json"}
    data = None
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"
    token = auth_token()
    if token:
        headers[TOKEN_HEADER] = token

    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=request_timeout()) as resp:
            raw = resp.read().decode("utf-8", "replace")
            return decode_body(raw, getattr(resp, "status", 200))
    except urllib.error.HTTPError as exc:
        raw = exc.read().decode("utf-8", "replace")
        return decode_body(raw, exc.code)
    except urllib.error.URLError as exc:
        return game_not_running(exc.reason)
    except (TimeoutError, socket.timeout) as exc:
        return game_not_running(exc)
    except OSError as exc:
        return game_not_running(exc)


def with_request_id(arguments: dict[str, Any]) -> dict[str, Any]:
    body = dict(arguments)
    if not body.get("clientRequestId"):
        body["clientRequestId"] = str(uuid.uuid4())
    return body


def proxy_post(path: str, arguments: dict[str, Any]) -> dict[str, Any]:
    body = with_request_id(arguments)
    dry = body.pop("dryRun", False)
    query = {"dryRun": "true"} if dry in (True, "true", "1", 1) else None
    return http_json("POST", path, query=query, body=body)


def tool_health(arguments: dict[str, Any]) -> dict[str, Any]:
    if arguments.get("ready"):
        return http_json("GET", "/health/ready")
    return http_json("GET", "/health")


def tool_state(arguments: dict[str, Any]) -> dict[str, Any]:
    include = arguments.get("include")
    query: dict[str, Any] | None = None
    if include:
        if isinstance(include, (list, tuple)):
            include = ",".join(str(part) for part in include)
        query = {"include": include}
    return http_json("GET", "/state", query=query)


def tool_harvest(arguments: dict[str, Any]) -> dict[str, Any]:
    return proxy_post("/harvest", arguments)


def tool_slot_upgrade(arguments: dict[str, Any]) -> dict[str, Any]:
    body = dict(arguments)
    slot_id = body.pop("id", None)
    if slot_id is None:
        slot_id = body.pop("slotId", None)
    if slot_id is None:
        return {"ok": False, "error": "invalid_arguments", "message": "id is required"}
    if isinstance(slot_id, dict):
        slot_id = slot_id.get("instanceId")
    if slot_id is None or slot_id == "":
        return {"ok": False, "error": "invalid_arguments", "message": "id is required"}
    return proxy_post(f"/slots/{slot_id}/upgrade", body)


def tool_night_call(arguments: dict[str, Any]) -> dict[str, Any]:
    return proxy_post("/night/call", arguments)


def tool_units_command(arguments: dict[str, Any]) -> dict[str, Any]:
    return proxy_post("/units/command", arguments)


def tool_units_send_to_spawn(arguments: dict[str, Any]) -> dict[str, Any]:
    return proxy_post("/units/send-to-spawn", arguments)


def tool_path_toggle(arguments: dict[str, Any]) -> dict[str, Any]:
    return proxy_post("/path/toggle", arguments)


def tool_king_teleport(arguments: dict[str, Any]) -> dict[str, Any]:
    return proxy_post("/king/teleport", arguments)


def tool_loadout_select(arguments: dict[str, Any]) -> dict[str, Any]:
    return proxy_post("/loadout/select", arguments)


def tool_level_start(arguments: dict[str, Any]) -> dict[str, Any]:
    return proxy_post("/level/start", arguments)


CLIENT_REQUEST = {
    "type": "string",
    "description": "Idempotency key. Generated if omitted.",
}
DRY_RUN = {
    "type": "boolean",
    "description": "If true, append ?dryRun=true and do not mutate.",
}

TOOLS: list[dict[str, Any]] = [
    {
        "name": "thronefall_health",
        "description": "GET /health. Alive check that does not wait on the Unity main thread.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "ready": {
                    "type": "boolean",
                    "description": "If true, call GET /health/ready and wait for a Unity frame.",
                }
            },
            "additionalProperties": False,
        },
        "handler": tool_health,
    },
    {
        "name": "thronefall_state",
        "description": "GET /state snapshot. Use include to request large arrays.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "include": {
                    "description": "Comma-separated slices: slots,units,enemies,spawns,loadout.",
                    "anyOf": [
                        {"type": "string"},
                        {"type": "array", "items": {"type": "string"}},
                    ],
                }
            },
            "additionalProperties": False,
        },
        "handler": tool_state,
    },
    {
        "name": "thronefall_harvest",
        "description": "POST /harvest. Collect gold from all harvestable slots, or one slotId.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "clientRequestId": CLIENT_REQUEST,
                "dryRun": DRY_RUN,
                "slotId": {"description": "Optional slot instance id. Omit to harvest all."},
            },
            "additionalProperties": True,
        },
        "handler": tool_harvest,
    },
    {
        "name": "thronefall_slot_upgrade",
        "description": "POST /slots/{id}/upgrade. Build or upgrade a BuildSlot.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "id": {"description": "Slot instance id, or {instanceId, generation}."},
                "slotId": {"description": "Alias of id."},
                "clientRequestId": CLIENT_REQUEST,
                "dryRun": DRY_RUN,
                "teleportKingNearby": {"type": "boolean"},
            },
            "required": ["id"],
            "additionalProperties": True,
        },
        "handler": tool_slot_upgrade,
    },
    {
        "name": "thronefall_night_call",
        "description": "POST /night/call. Switch to night when IsFreeToCallNight.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "clientRequestId": CLIENT_REQUEST,
                "dryRun": DRY_RUN,
            },
            "additionalProperties": True,
        },
        "handler": tool_night_call,
    },
    {
        "name": "thronefall_units_command",
        "description": "POST /units/command. Send units to a world point.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "clientRequestId": CLIENT_REQUEST,
                "dryRun": DRY_RUN,
                "selector": {
                    "type": "object",
                    "description": "One of ids, typeName, or group (1/2/3).",
                },
                "target": {
                    "type": "object",
                    "properties": {
                        "x": {"type": "number"},
                        "y": {"type": "number"},
                        "z": {"type": "number"},
                    },
                },
                "hold": {"type": "boolean"},
                "useSolver": {"type": "boolean"},
            },
            "additionalProperties": True,
        },
        "handler": tool_units_command,
    },
    {
        "name": "thronefall_units_send_to_spawn",
        "description": "POST /units/send-to-spawn. Post a unit type onto a spawn-line rally.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "clientRequestId": CLIENT_REQUEST,
                "dryRun": DRY_RUN,
                "typeName": {"type": "string"},
                "spawnId": {"description": "Spawn line instance id."},
                "hold": {"type": "boolean"},
            },
            "additionalProperties": True,
        },
        "handler": tool_units_send_to_spawn,
    },
    {
        "name": "thronefall_path_toggle",
        "description": "POST /path/toggle. Open or close a path cutter.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "clientRequestId": CLIENT_REQUEST,
                "dryRun": DRY_RUN,
                "id": {
                    "type": "object",
                    "properties": {
                        "instanceId": {"type": "integer"},
                        "generation": {"type": "integer"},
                    },
                },
            },
            "additionalProperties": True,
        },
        "handler": tool_path_toggle,
    },
    {
        "name": "thronefall_king_teleport",
        "description": "POST /king/teleport. target is castle, start, or a coordinate.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "clientRequestId": CLIENT_REQUEST,
                "dryRun": DRY_RUN,
                "target": {"description": "castle | start | {x,y,z}"},
            },
            "additionalProperties": True,
        },
        "handler": tool_king_teleport,
    },
    {
        "name": "thronefall_loadout_select",
        "description": "POST /loadout/select. Equip a perk, weapon, or mutator.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "clientRequestId": CLIENT_REQUEST,
                "dryRun": DRY_RUN,
                "name": {"type": "string"},
                "kind": {"type": "string", "description": "perk | weapon | mutator"},
            },
            "additionalProperties": True,
        },
        "handler": tool_loadout_select,
    },
    {
        "name": "thronefall_level_start",
        "description": "POST /level/start. Start a level from level_select.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "clientRequestId": CLIENT_REQUEST,
                "dryRun": DRY_RUN,
                "sceneName": {"type": "string", "description": "e.g. Nordfels or Neuland(Tutorial)"},
            },
            "additionalProperties": True,
        },
        "handler": tool_level_start,
    },
]

HANDLERS = {tool["name"]: tool["handler"] for tool in TOOLS}


def public_tools() -> list[dict[str, Any]]:
    return [
        {
            "name": tool["name"],
            "description": tool["description"],
            "inputSchema": tool["inputSchema"],
        }
        for tool in TOOLS
    ]


def read_message() -> dict[str, Any] | None:
    global _use_content_length
    first = sys.stdin.buffer.readline()
    if not first:
        return None
    if first.lower().startswith(b"content-length:"):
        _use_content_length = True
        length = int(first.split(b":", 1)[1].strip())
        while True:
            line = sys.stdin.buffer.readline()
            if line in (b"", b"\r\n", b"\n"):
                break
            if line.lower().startswith(b"content-length:"):
                length = int(line.split(b":", 1)[1].strip())
        body = sys.stdin.buffer.read(length)
        if not body:
            return None
        return json.loads(body.decode("utf-8"))

    line = first.strip()
    if not line:
        return read_message()
    return json.loads(line.decode("utf-8"))


def write_message(payload: dict[str, Any]) -> None:
    data = json.dumps(payload, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    if _use_content_length:
        sys.stdout.buffer.write(f"Content-Length: {len(data)}\r\n\r\n".encode("ascii"))
        sys.stdout.buffer.write(data)
    else:
        sys.stdout.buffer.write(data)
        sys.stdout.buffer.write(b"\n")
    sys.stdout.buffer.flush()


def initialize_result(params: dict[str, Any], msg_id: Any) -> dict[str, Any]:
    requested = str((params or {}).get("protocolVersion") or "2024-11-05")
    if not (requested.startswith("2024-") or requested.startswith("2025-")):
        requested = "2024-11-05"
    return {
        "jsonrpc": "2.0",
        "id": msg_id,
        "result": {
            "protocolVersion": requested,
            "capabilities": {"tools": {"listChanged": False}},
            "serverInfo": {"name": SERVER_NAME, "version": SERVER_VERSION},
            "instructions": (
                "Local Thronefall HTTP proxy. The BepInEx plugin must listen on "
                "127.0.0.1:17891. If the game is closed, tools return game_not_running."
            ),
        },
    }


def call_tool(params: dict[str, Any], msg_id: Any) -> dict[str, Any]:
    name = (params or {}).get("name")
    arguments = (params or {}).get("arguments") or {}
    if not isinstance(arguments, dict):
        arguments = {}
    handler = HANDLERS.get(name)
    if handler is None:
        return {
            "jsonrpc": "2.0",
            "id": msg_id,
            "error": {"code": -32602, "message": f"unknown tool: {name}"},
        }
    try:
        result = handler(arguments)
    except Exception as exc:  # noqa: BLE001 — tool errors must not kill stdio
        result = {"ok": False, "error": "proxy_exception", "message": str(exc)}
        text = json.dumps(result, separators=(",", ":"), ensure_ascii=False)
        return {
            "jsonrpc": "2.0",
            "id": msg_id,
            "result": {
                "content": [{"type": "text", "text": text}],
                "isError": True,
            },
        }

    text = json.dumps(result, separators=(",", ":"), ensure_ascii=False)
    return {
        "jsonrpc": "2.0",
        "id": msg_id,
        "result": {
            "content": [{"type": "text", "text": text}],
            "isError": False,
        },
    }


def dispatch(message: dict[str, Any]) -> dict[str, Any] | None:
    method = message.get("method")
    has_id = "id" in message
    msg_id = message.get("id") if has_id else None
    params = message.get("params") or {}
    if not isinstance(params, dict):
        params = {}

    if method == "initialize":
        return initialize_result(params, msg_id)
    if not has_id:
        return None
    if method == "ping":
        return {"jsonrpc": "2.0", "id": msg_id, "result": {}}
    if method == "tools/list":
        return {"jsonrpc": "2.0", "id": msg_id, "result": {"tools": public_tools()}}
    if method == "tools/call":
        return call_tool(params, msg_id)
    return {
        "jsonrpc": "2.0",
        "id": msg_id,
        "error": {"code": -32601, "message": f"method not found: {method}"},
    }


def main() -> int:
    log(f"{SERVER_NAME} stdio proxy -> {base_url()}")
    while True:
        try:
            message = read_message()
        except json.JSONDecodeError as exc:
            write_message(
                {
                    "jsonrpc": "2.0",
                    "id": None,
                    "error": {"code": -32700, "message": f"parse error: {exc}"},
                }
            )
            continue
        except Exception as exc:  # noqa: BLE001
            log(f"read failed: {exc}")
            return 1
        if message is None:
            return 0
        if not isinstance(message, dict):
            continue
        response = dispatch(message)
        if response is not None:
            write_message(response)


if __name__ == "__main__":
    raise SystemExit(main())
