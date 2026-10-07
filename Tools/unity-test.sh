#!/usr/bin/env bash
# Run a test suite headless: Tools/unity-test.sh [EditMode|PlayMode]
# The editor must not already have the project open.
set -u
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY_EXE="${UNITY_EXE:-/c/Program Files/Unity/Hub/Editor/6000.5.8f1/Editor/Unity.exe}"
PLATFORM="${1:-EditMode}"
RESULTS="$ROOT/Logs/test-results-$PLATFORM.xml"
rm -f "$RESULTS"
"$UNITY_EXE" -batchmode -nographics -projectPath "$ROOT" -runTests -testPlatform "$PLATFORM" \
  -testResults "$RESULTS" -logFile "$ROOT/Logs/test.log"
code=$?
if [ $code -ne 0 ] && grep -q "Multiple Unity instances cannot open the same project" "$ROOT/Logs/test.log" 2>/dev/null; then
  echo "UNITY: the project is open in the Unity Editor - close it to run batch jobs"; exit 2
fi
if [ ! -f "$RESULTS" ]; then
  grep -E "error CS[0-9]+" "$ROOT/Logs/test.log" | sort -u
  echo "UNITY TESTS: no results (exit $code) - see Logs/test.log"; exit 1
fi
python - "$RESULTS" <<'PY'
import sys, xml.etree.ElementTree as ET
r = ET.parse(sys.argv[1]).getroot()
print(f"UNITY TESTS: {r.get('result')} - total {r.get('total')}, passed {r.get('passed')}, failed {r.get('failed')}")
for tc in r.iter('test-case'):
    if tc.get('result') == 'Failed':
        msg = tc.find('.//message')
        print(' FAIL', tc.get('fullname'), '-', (msg.text or '').strip()[:300] if msg is not None else '')
PY
exit $code
