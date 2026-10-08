#!/usr/bin/env bash
# Build VrFsim Quest Setup (installs the game on a USB-connected Meta Quest): Builds/VrFsim-Quest-Setup.exe
#   1. Build the Quest game:  Tools/unity-batch.sh VrFsim.EditorTools.BuildTool.BuildQuest
#   2. Then run:              Tools/make-quest-setup.sh
# One exe with the Quest build inside. Uses only the .NET Framework C# compiler that ships with Windows.
set -eu
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APK="$ROOT/Builds/VrFsim-Quest.apk"
WORK="$ROOT/Builds/quest-setup"
OUT="$ROOT/Builds/VrFsim-Quest-Setup.exe"
CSC="/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe"

[ -f "$APK" ] || { echo "QUEST SETUP: no Quest build at $APK - build it first"; exit 1; }
VERSION=$(grep -o 'Version = "[^"]*"' "$ROOT/Assets/VrFsim/Scripts/Editor/ProjectSetup.cs" | cut -d'"' -f2)
mkdir -p "$WORK"
cat > "$WORK/BuildInfo.cs" <<CS
namespace VrFsimQuestSetup { static class BuildInfo { public const string Version = "$VERSION"; } }
CS

w() { cygpath -w "$1"; }
"$CSC" -nologo -target:winexe -optimize+ -platform:anycpu \
  -out:"$(w "$OUT")" \
  -win32icon:"$(w "$ROOT/Tools/installer/VrFsim.ico")" \
  -win32manifest:"$(w "$ROOT/Tools/installer/app.manifest")" \
  -resource:"$(w "$APK")",VrFsim-Quest.apk \
  -resource:"$(w "$ROOT/Assets/VrFsim/Resources/Branding/VrFsimLogo.png")",logo.png \
  -r:System.IO.Compression.dll -r:System.IO.Compression.FileSystem.dll -r:System.Windows.Forms.dll -r:System.Drawing.dll \
  "$(w "$ROOT/Tools/quest-setup/QuestSetup.cs")" "$(w "$ROOT/Tools/quest-setup/Adb.cs")" "$(w "$WORK/BuildInfo.cs")"

echo "QUEST SETUP: $OUT ($(du -h "$OUT" | cut -f1), version $VERSION)"
