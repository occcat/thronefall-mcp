#!/usr/bin/env python3
"""Minimal loopback client for the Thronefall Control HTTP API."""

from __future__ import annotations

import json
import urllib.error
import urllib.request


class Thronefall:
    def __init__(self, base="http://127.0.0.1:17891", token=None, timeout=2.0):
        self.base, self.token = base.rstrip("/"), token
        self.timeout = timeout
        self._n = 0

    def _rid(self):
        self._n += 1
        return f"py-{self._n}"

    def _req(self, method, path, body=None, dry=False):
        sep = "&" if "?" in path else "?"
        url = self.base + path + (f"{sep}dryRun=true" if dry else "")
        data = None if body is None else json.dumps(body).encode()
        headers = {"Content-Type": "application/json"}
        if self.token:
            headers["X-Thronefall-Token"] = self.token
        req = urllib.request.Request(url, data=data, headers=headers, method=method)
        try:
            with urllib.request.urlopen(req, timeout=self.timeout) as r:
                return json.loads(r.read().decode())
        except urllib.error.HTTPError as e:
            raw = e.read().decode()
            try:
                return json.loads(raw)
            except json.JSONDecodeError:
                return {
                    "ok": False,
                    "error": "http_error",
                    "message": raw or str(e),
                    "status": e.code,
                }

    def health(self):
        return self._req("GET", "/health")

    def openapi(self):
        return self._req("GET", "/openapi.json")

    def state(self, include=None):
        q = "/state" + (f"?include={include}" if include else "")
        return self._req("GET", q)

    def next_wave(self):
        return self._req("GET", "/state/next-wave")

    def harvest_all(self, dry=False):
        return self._req("POST", "/harvest", {"clientRequestId": self._rid()}, dry)

    def build(self, slot_id, dry=False, teleport_king_nearby=False):
        return self._req(
            "POST",
            f"/slots/{slot_id}/build",
            {"clientRequestId": self._rid(), "teleportKingNearby": teleport_king_nearby},
            dry,
        )

    def cancel_choice(self):
        return self._req("POST", "/slots/choice/cancel", {"clientRequestId": self._rid()})

    def call_night(self):
        return self._req("POST", "/night/call", {"clientRequestId": self._rid()})

    def command_units(self, ids, x, z, hold=True):
        return self._req(
            "POST",
            "/units/command",
            {
                "clientRequestId": self._rid(),
                "selector": {"ids": ids},
                "target": {"x": x, "y": 0, "z": z},
                "hold": hold,
            },
        )

    def deploy(self, picks, target, hold=True, spacing=2, dry=False):
        return self._req(
            "POST",
            "/units/deploy",
            {
                "clientRequestId": self._rid(),
                "picks": picks,
                "target": target,
                "hold": hold,
                "spacing": spacing,
            },
            dry,
        )

    def send_to_spawn(self, type_name, spawn_id, hold=True):
        return self._req(
            "POST",
            "/units/send-to-spawn",
            {
                "clientRequestId": self._rid(),
                "typeName": type_name,
                "spawnId": spawn_id,
                "hold": hold,
            },
        )

    def teleport_king(self, target="castle", position=None):
        body = {"clientRequestId": self._rid(), "target": target}
        if position is not None:
            body["position"] = position
        return self._req("POST", "/king/teleport", body)

    def night_policy(self, policy="human"):
        return self._req(
            "POST",
            "/night/policy",
            {"clientRequestId": self._rid(), "policy": policy},
        )

    def select_loadout(self, name, kind="perk"):
        return self._req(
            "POST",
            "/loadout/select",
            {"clientRequestId": self._rid(), "name": name, "kind": kind},
        )

    def start_level(self, scene_name):
        return self._req(
            "POST",
            "/level/start",
            {"clientRequestId": self._rid(), "sceneName": scene_name},
        )


if __name__ == "__main__":
    tf = Thronefall()
    print(json.dumps(tf.health(), indent=2))
