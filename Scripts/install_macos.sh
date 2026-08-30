#!/usr/bin/env bash
# Install BepInEx 5 unix doorstop + optional ThronefallControl.dll into
# the macOS Steam Thronefall .app layout.
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: Scripts/install_macos.sh --zip /path/to/BepInEx_unix_5.4.x.zip [options]

Installs the BepInEx 5 unix/macos Mono pack next to thronefall.app and
points run_bepinex.sh at that bundle. Does not download anything.

Options:
  --zip PATH       BepInEx unix/macos zip you already downloaded (required)
  --game PATH      Game root containing thronefall.app
  --plugin PATH    ThronefallControl.dll to copy into BepInEx/plugins
  -h, --help       Show this help

Default game root:
  ~/Library/Application Support/Steam/steamapps/common/Thronefall

Refuse: Windows Thunderstore packs (winhttp.dll / Thronefall.exe) and
IL2CPP BepInEx 6 zips. This game is Unity Mono on macOS.
EOF
}

GAME_ROOT="${THRONEFALL_ROOT:-$HOME/Library/Application Support/Steam/steamapps/common/Thronefall}"
ZIP=""
PLUGIN=""
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

while [ $# -gt 0 ]; do
  case "$1" in
    --zip) ZIP="${2:-}"; shift 2 ;;
    --game) GAME_ROOT="${2:-}"; shift 2 ;;
    --plugin) PLUGIN="${2:-}"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *)
      if [ -z "$ZIP" ] && [ -f "$1" ]; then
        ZIP="$1"
        shift
      else
        echo "unknown argument: $1" >&2
        usage >&2
        exit 2
      fi
      ;;
  esac
done

if [ -z "$ZIP" ]; then
  usage >&2
  exit 2
fi
if [ ! -f "$ZIP" ]; then
  echo "zip not found: $ZIP" >&2
  exit 1
fi
if [ ! -d "$GAME_ROOT/thronefall.app" ]; then
  echo "thronefall.app not found under: $GAME_ROOT" >&2
  echo "Pass --game pointing at the Steam folder that contains thronefall.app." >&2
  exit 1
fi

list_zip() {
  unzip -Z1 "$ZIP" 2>/dev/null || unzip -l "$ZIP"
}

if list_zip | grep -qiE 'winhttp\.dll|Thronefall\.exe'; then
  echo "This zip looks like the Windows Thunderstore BepInExPack_Thronefall." >&2
  echo "Use BepInEx 5 unix/macos Mono (BepInEx_unix_5.4.x.zip or BepInEx_macos_x64_*.zip)." >&2
  exit 1
fi
if list_zip | grep -qiE 'IL2CPP|BepInEx\.Unity\.IL2CPP'; then
  echo "This zip looks like an IL2CPP / BepInEx 6 pack. Thronefall v2.13 is Mono." >&2
  exit 1
fi

mkdir -p "$GAME_ROOT"
echo "Extracting $(basename "$ZIP") -> $GAME_ROOT"
unzip -qo "$ZIP" -d "$GAME_ROOT"

RUN="$GAME_ROOT/run_bepinex.sh"
if [ ! -f "$RUN" ]; then
  echo "run_bepinex.sh missing after extract. Is this the unix/macos pack?" >&2
  exit 1
fi

if grep -q '^executable_name=""' "$RUN"; then
  # portable in-place replace; macOS sed -i needs a suffix
  tmp="$RUN.tmp.$$"
  sed 's/^executable_name=""/executable_name="thronefall.app"/' "$RUN" >"$tmp"
  mv "$tmp" "$RUN"
elif grep -q '^executable_name=' "$RUN"; then
  tmp="$RUN.tmp.$$"
  sed 's/^executable_name=.*/executable_name="thronefall.app"/' "$RUN" >"$tmp"
  mv "$tmp" "$RUN"
else
  echo "warning: could not patch executable_name in $RUN" >&2
fi

if grep -q '^target_assembly=' "$RUN" && ! grep -q 'BepInEx.Preloader.dll' "$RUN"; then
  echo "warning: target_assembly is not BepInEx.Preloader.dll; leaving as-is." >&2
fi

chmod u+x "$RUN"
if [ -f "$GAME_ROOT/libdoorstop.dylib" ]; then
  chmod u+x "$GAME_ROOT/libdoorstop.dylib" || true
  xattr -d com.apple.quarantine "$GAME_ROOT/libdoorstop.dylib" 2>/dev/null || true
fi
xattr -d com.apple.quarantine "$RUN" 2>/dev/null || true

if [ -z "$PLUGIN" ]; then
  for cand in \
    "$REPO_ROOT/bin/ThronefallControl/Release/ThronefallControl.dll" \
    "$REPO_ROOT/bin/ThronefallControl/Debug/ThronefallControl.dll"
  do
    if [ -f "$cand" ]; then
      PLUGIN="$cand"
      break
    fi
  done
fi

if [ -n "$PLUGIN" ]; then
  if [ ! -f "$PLUGIN" ]; then
    echo "plugin dll not found: $PLUGIN" >&2
    exit 1
  fi
  mkdir -p "$GAME_ROOT/BepInEx/plugins"
  cp "$PLUGIN" "$GAME_ROOT/BepInEx/plugins/ThronefallControl.dll"
  echo "Installed plugin: $GAME_ROOT/BepInEx/plugins/ThronefallControl.dll"
else
  echo "No ThronefallControl.dll given; BepInEx is installed without the plugin."
  echo "Build then re-run with --plugin path/to/ThronefallControl.dll"
fi

echo
echo "Steam launch options (paste exactly, absolute path required on macOS):"
echo "  \"$GAME_ROOT/run_bepinex.sh\" %command%"
echo
echo "Launch once via Steam, then:"
echo "  curl -s http://127.0.0.1:17891/health"
echo
echo "Logs:"
echo "  $GAME_ROOT/BepInEx/LogOutput.txt"
echo "  $HOME/Library/Logs/Grizzly Games/Thronefall/Player.log"
echo
echo "Uninstall (does not touch saves):"
echo "  rm -rf \"$GAME_ROOT/BepInEx\" \"$GAME_ROOT/libdoorstop.dylib\" \\"
echo "         \"$GAME_ROOT/run_bepinex.sh\" \"$GAME_ROOT/doorstop_config.ini\" \\"
echo "         \"$GAME_ROOT/changelog.txt\""
echo "  and clear Steam launch options."
