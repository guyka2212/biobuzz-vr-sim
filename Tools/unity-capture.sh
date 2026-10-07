#!/usr/bin/env bash
# Render review screenshots of the running scene to Logs/Captures (needs a GPU).
set -u
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY_EXE="${UNITY_EXE:-/c/Program Files/Unity/Hub/Editor/6000.5.8f1/Editor/Unity.exe}"
"$UNITY_EXE" -batchmode -projectPath "$ROOT" -runTests -testPlatform PlayMode \
  -testFilter "${1:-VrFsim.Tests.VisualCaptureTests.CaptureViews}" \
  -testResults "$ROOT/Logs/test-results-capture.xml" -logFile "$ROOT/Logs/capture.log"
ls -la "$ROOT/Logs/Captures"
