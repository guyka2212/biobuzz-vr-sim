#!/usr/bin/env bash
# Install the standalone Meta Quest build on a headset connected by USB, then start it.
#   1. Build it:   Tools/unity-batch.sh VrFsim.EditorTools.BuildTool.BuildQuest
#   2. Connect the Quest by USB (developer mode on), accept "Allow USB debugging" in the headset.
#   3. Run:        Tools/quest-install.sh
# After this the game is in the headset's Library under "Unknown Sources" and runs without the PC.
set -u
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ADB="${ADB:-/c/Program Files/Unity/Hub/Editor/6000.5.8f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe}"
APK="$ROOT/Builds/VrFsim-Quest.apk"
PKG="com.vrfsim.biobuzz"

[ -f "$APK" ] || { echo "QUEST: no build at $APK - build it first"; exit 1; }
devices=$("$ADB" devices | tail -n +2 | grep -v '^\s*$')
if [ -z "$devices" ]; then
  echo "QUEST: no headset found. Connect it by USB-C and make sure developer mode is on."; exit 2
fi
if echo "$devices" | grep -q unauthorized; then
  echo "QUEST: put the headset on and accept 'Allow USB debugging' (tick 'Always allow'), then run this again."; exit 3
fi

echo "QUEST: installing $(du -h "$APK" | cut -f1) ..."
"$ADB" install -r -g "$APK" || { echo "QUEST: install failed"; exit 1; }
"$ADB" shell am start -n "$PKG/com.unity3d.player.UnityPlayerActivity" >/dev/null && echo "QUEST: installed and started - put the headset on."
