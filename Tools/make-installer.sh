#!/usr/bin/env bash
# Package the Windows build into a one-file installer: Builds/VrFsim-Setup.exe
#   1. Build the game:   Tools/unity-batch.sh VrFsim.EditorTools.BuildTool.BuildWindows
#   2. Then run:         Tools/make-installer.sh
# Uses only what ships with Windows: PowerShell (zip) and the .NET Framework C# compiler.
set -eu
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BUILD="$ROOT/Builds/VrFsim"
WORK="$ROOT/Builds/installer"
OUT="$ROOT/Builds/VrFsim-Setup.exe"
CSC="/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe"

[ -f "$BUILD/VrFsim.exe" ] || { echo "INSTALLER: no build at $BUILD - build the game first"; exit 1; }
VERSION=$(grep -o 'Version = "[^"]*"' "$ROOT/Assets/VrFsim/Scripts/Editor/ProjectSetup.cs" | cut -d'"' -f2)
REPO="https://github.com/guyka2212/biobuzz-vr-sim"

# Stage the game without debug symbols, logs or Unity's do-not-ship folders.
rm -rf "${WORK:?}"
mkdir -p "$WORK/stage"
cp -r "$BUILD/." "$WORK/stage/"
rm -rf "$WORK/stage/"*_DoNotShip "$WORK/stage/"*.log "$WORK/stage/benchmark.txt"
find "$WORK/stage" -name "*.pdb" -delete

powershell.exe -NoProfile -Command "Add-Type -AssemblyName System.IO.Compression.FileSystem; [IO.Compression.ZipFile]::CreateFromDirectory('$(cygpath -w "$WORK/stage")', '$(cygpath -w "$WORK/payload.zip")', [IO.Compression.CompressionLevel]::Optimal, \$false)"

cat > "$WORK/BuildInfo.cs" <<CS
namespace VrFsimSetup { static class BuildInfo { public const string Version = "$VERSION"; public const string RepoUrl = "$REPO"; } }
CS

w() { cygpath -w "$1"; }
"$CSC" -nologo -target:winexe -optimize+ -platform:anycpu \
  -out:"$(w "$OUT")" \
  -win32icon:"$(w "$ROOT/Tools/installer/VrFsim.ico")" \
  -win32manifest:"$(w "$ROOT/Tools/installer/app.manifest")" \
  -resource:"$(w "$WORK/payload.zip")",payload.zip \
  -resource:"$(w "$ROOT/Assets/VrFsim/Resources/Branding/VrFsimLogo.png")",logo.png \
  -r:System.IO.Compression.dll -r:System.IO.Compression.FileSystem.dll -r:System.Windows.Forms.dll -r:System.Drawing.dll \
  "$(w "$ROOT/Tools/installer/VrFsimSetup.cs")" "$(w "$WORK/BuildInfo.cs")"

echo "INSTALLER: $OUT ($(du -h "$OUT" | cut -f1), version $VERSION)"
