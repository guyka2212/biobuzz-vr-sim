#!/usr/bin/env bash
# Run the Unity editor headless against this project and report compile errors.
#   Tools/unity-batch.sh                      # import + compile only
#   Tools/unity-batch.sh Some.Class.Method    # also run an editor method
# Set UNITY_EXE to override the editor path. The editor must not already have the project open.
set -u
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY_EXE="${UNITY_EXE:-/c/Program Files/Unity/Hub/Editor/6000.5.8f1/Editor/Unity.exe}"
LOG="$ROOT/Logs/batch.log"
mkdir -p "$ROOT/Logs"

args=(-batchmode -quit -projectPath "$ROOT" -logFile "$LOG")
[ "${NOGRAPHICS:-1}" = "1" ] && args+=(-nographics)
[ $# -ge 1 ] && args+=(-executeMethod "$1")

"$UNITY_EXE" "${args[@]}"
code=$?

errors=$(grep -E "error CS[0-9]+|Scripts have compiler errors|executeMethod.*(failed|could not)" "$LOG" | sort -u)
if [ -n "$errors" ]; then
  echo "$errors"
  echo "UNITY BATCH: FAILED (exit $code) — see $LOG"
  exit 1
fi
echo "UNITY BATCH: OK (exit $code)"
exit $code
