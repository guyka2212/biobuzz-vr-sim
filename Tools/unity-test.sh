#!/usr/bin/env bash
# Run the EditMode test suite headless. The editor must not already have the project open.
set -u
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY_EXE="${UNITY_EXE:-/c/Program Files/Unity/Hub/Editor/6000.5.8f1/Editor/Unity.exe}"
RESULTS="$ROOT/Logs/test-results.xml"
rm -f "$RESULTS"
"$UNITY_EXE" -batchmode -nographics -projectPath "$ROOT" -runTests -testPlatform EditMode \
  -testResults "$RESULTS" -logFile "$ROOT/Logs/test.log"
code=$?
if [ ! -f "$RESULTS" ]; then
  grep -E "error CS[0-9]+" "$ROOT/Logs/test.log" | sort -u
  echo "UNITY TESTS: no results (exit $code) - see Logs/test.log"; exit 1
fi
python - "$RESULTS" <<'PY'
import sys, xml.etree.ElementTree as ET
r = ET.parse(sys.argv[1]).getroot()
print(f"UNITY TESTS: {r.get('result')} - total {r.get('total')}, passed {r.get('passed')}, failed {r.get('failed')}")
for tc in r.iter('test-case'):
    if tc.get('result') != 'Passed':
        msg = tc.find('.//message')
        print(' FAIL', tc.get('fullname'), '-', (msg.text or '').strip()[:300] if msg is not None else '')
PY
exit $code
