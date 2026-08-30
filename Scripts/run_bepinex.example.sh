#!/bin/sh
# Example settings for the BepInEx 5 unix/macos Mono pack.
#
# Copy these values into the run_bepinex.sh that ships in
# BepInEx_unix_5.4.x.zip / BepInEx_macos_x64_*.zip after extracting
# that zip into the Steam game root (next to thronefall.app).
#
# Do NOT use Thunderstore BepInExPack_Thronefall. That pack is Windows:
# winhttp.dll + Thronefall.exe. It will not inject this .app.

# LINUX: name of Unity executable
# MACOS: name of the .app directory (lowercase t, Steam layout)
executable_name="thronefall.app"

# BepInEx 5 Mono preloader (not IL2CPP / BepInEx 6)
target_assembly="BepInEx/core/BepInEx.Preloader.dll"

# Enable Doorstop? 0 is false, 1 is true
enabled="1"

# Rest of the file comes from the unix pack. Scripts/install_macos.sh
# patches executable_name in place and chmod/xattr the real script.
#
# Steam launch options (macOS needs an absolute path):
#   "/Users/<you>/Library/Application Support/Steam/steamapps/common/Thronefall/run_bepinex.sh" %command%
#
# Apple Silicon: the unix pack's script already re-passes
# DYLD_INSERT_LIBRARIES through `arch`. If doorstop still fails to
# inject, run under Rosetta as a diagnostic:
#   arch -x86_64 ./run_bepinex.sh
# This plugin does not use Harmony; a Harmony crash is not expected
# from ThronefallControl.dll itself.
