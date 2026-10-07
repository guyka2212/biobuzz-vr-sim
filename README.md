# VrFsim — BIOBUZZ VR driving simulator

VrFsim is a PC VR practice simulator for the *FIRST* Tech Challenge 2026–27 game, **BIOBUZZ**.
You put on a PC VR headset and stand at the driver station in the ALLIANCE AREA, as a real
DRIVER does. Then you drive a simulated FTC robot on a full-size field with an ordinary gamepad.

- Full 144 in BIOBUZZ field with the HIVE structure (two tipping HIVES), four FLOWERS, LOADING
  ZONES, GARDENS and all 56 scoring elements, staged per the Competition Manual.
- Real match flow: 0:30 AUTO, 0:08 transition, 2:00 TELEOP. FLOWERS open at 1:00. Field audio cues.
  Scoring follows Table 10-2, including at-rest assessment and ranking points.
- Physically simulated robot built from your settings: mecanum, tank, swerve, X-drive or
  butterfly drivetrain, with motor and traction physics derived from goBILDA hardware.
- Intakes, single or double turret, dumper, Box Tube, passing, human-player NECTAR, automatic fouls.
- Every setting is adjustable in an in-VR menu and saved between sessions.

> Independent, fan-made project. Not affiliated with, authorized, or endorsed by *FIRST*,
> *FIRST* Tech Challenge, or RTX. See [Credits](#credits).

## Requirements

| | Minimum |
|---|---|
| OS | Windows 10/11, 64-bit |
| GPU | VR-ready (GTX 1070 / RX 5700 class or better) |
| Headset | Any PC VR headset with an **OpenXR** runtime: Meta Quest via Link / Air Link / Steam Link, Valve Index, HTC Vive, Windows Mixed Reality, Pico, Varjo… |
| Controller | Any gamepad Windows sees as a gamepad: Xbox, PlayStation DualShock 4 / DualSense, Switch Pro, Logitech F310/F710 (switch on **X**) |
| To build | Unity **6000.5.8f1** (Unity 6.5) with Windows Build Support; Git LFS |

No headset? The game also runs on a monitor. Hold the right mouse button to look around.

## Setting up the headset

The game uses OpenXR, so it runs on whichever OpenXR runtime is **active** on your PC.

1. Install your headset's PC software:
   - **Meta Quest:** the Meta Quest Link app. Connect with Link (cable) or Air Link.
   - **SteamVR headsets** (Index, Vive, Pico, Bigscreen…): Steam and SteamVR.
   - **Windows Mixed Reality:** Mixed Reality Portal, plus the OpenXR Tools for WMR.
2. Make that software the active OpenXR runtime:
   - **Meta Quest Link app:** Settings → General → *OpenXR Runtime* → *Set Meta Quest Link as active*.
   - **SteamVR:** Settings → OpenXR → *Set SteamVR as OpenXR Runtime*.
3. Start the headset software, put the headset on, then launch `VrFsim.exe`.
4. Stand where you want to drive and press **Recenter** (D-pad Left) to face the field.

The refresh rate (72/90/120 Hz) is set in your VR runtime, not in the game. VrFsim targets 90 fps.

## Setting up the controller

Plug in the gamepad, or pair it over Bluetooth, **before** starting the game. Nothing else is needed.

- **Logitech F310 / F710:** set the switch on the back or top to **X** (XInput). The D (DirectInput)
  position is not a standard gamepad. FTC teams usually run their F310s in X mode too.
- **PlayStation controllers** work over USB or Bluetooth. Steam Input is not required.
- Every button can be rebound in **Menu → Controls**, and the drive stick (left or right),
  deadzone, response curve and trigger threshold are in **Menu → Driving**.

### Default controls

| Action | Gamepad | Keyboard |
|---|---|---|
| Drive / strafe | Left stick | W A S D |
| Turn | Right stick X | ← → |
| Tank control (left / right side) | Left Y / Right Y | W S / ↑ ↓ |
| Intake (hold) | LT / L2 | Left Shift |
| Fire launcher (hold) | RT / R2 | Space |
| Outtake | B / ○ | O |
| Box Tube: place POLLEN | LB / L1 | C |
| Box Tube: place NECTAR | D-pad Up | X |
| Human player: enter NECTAR | D-pad Down | N |
| Slow mode (hold) | X / □ | P |
| Flip robot front | Y / △ | F |
| Butterfly shift (mecanum ↔ traction) | RB / R1 | B |
| Ramp intake toggle | Right stick click | Z |
| Pass to target | Left stick click | V |
| Start match / next match | A / ✕ | Enter |
| Reset match | Back / Share | R |
| Menu (pauses) | Start / Options | Esc |
| Next camera view | D-pad Right | L |
| Recenter view | D-pad Left | Home |

Everything works from the gamepad alone. A keyboard is only a fallback.
- **Menu:** D-pad or stick to move, Left/Right to change a value, **A** to select, **B** to go
  back, **Start** to close. Text such as the robot name is typed on an on-screen keyboard with the
  D-pad and A (a physical keyboard also types into it). You can also point a VR controller at
  the menu and pull its trigger.
- **Free camera (Menu → Camera → View → Free):** the left stick flies, the right stick turns, and
  the triggers raise and lower the camera. The robot holds still until you switch back to a
  driving view (D-pad Right).

## Playing

1. You start at the red driver station. A match is staged and waiting. Press **A** to start it.
2. **AUTO (0:30):** your robot runs the AUTO routine picked in Menu → Match, as an OpMode
   would (G401: no driver input).
3. **Transition (0:08):** controls are locked (G403). Watch for "PICK UP CONTROLLERS".
4. **TELEOP (2:00):** drive. Launch POLLEN and NECTAR into your HIVE's up-CELL to TIP it
   (+20). Each TIP lets your human player enter one NECTAR. Once 1:00 remains, the FLOWERS open
   and the human player may enter every remaining NECTAR. End in your LOADING ZONE to PARK.
5. After 0:00 the field comes to rest, then final scores and ranking points appear on the
   driver display.

**Free drive** (Menu → Match → Mode) removes the clock, so you can practise without a timer.

## Settings

All settings live in the in-VR menu and are saved to
`%USERPROFILE%\AppData\LocalLow\VrFsim\VrFsim\settings.json`.

- **Match:** mode, alliance, start position (four G304-legal positions or a custom one),
  AUTO routine, practice robots (partner and two opponents), event log, penalties.
- **Robot:** five presets, name, team number, drivetrain, length, width, deployed and stowed
  height, mass, wheel RPM, butterfly traction RPM, colours, and a saved-robot library.
- **Mechanisms:** intake type (sweeper, side rollers, ramp), reach and mount. Launcher (turret,
  double turret, dumper) and its mount, NECTAR turret mount, dumper hood angle. Box Tube and its
  mount. Storage capacity (1–4, G407) and pass target.
- **Driving:** field-centric, aim assist, auto intake, auto fire, tank control, slow-mode speed,
  drive stick, deadzone, response curve, trigger threshold.
- **Controls:** rebind every action on gamepad and keyboard.
- **Rules:** values FIRST does not publish, such as the HIVE tip model, swing time, element
  mass and PARK zone (see below).
- **Audio:** per-category volume. **Graphics:** quality preset, render scale, MSAA, shadows,
  venue, comfort options, minimap, performance overlay. **Camera:** driver station (default),
  overhead, chase, robot POV and free-fly views; station position, eye height and chase offsets.

The settings mirror the range offered by [dsim](https://github.com/genius0412/dsim);
[Docs/dsim-settings-inventory.md](Docs/dsim-settings-inventory.md) maps each one.

## Rules accuracy

Game facts come from the **BIOBUZZ Competition Manual (TU03)** and the **Event Field Setup
Guide**. They are summarised, with sources, in [Docs/biobuzz-facts.md](Docs/biobuzz-facts.md).
FIRST does not publish everything a simulator needs, so these values are labelled
assumptions and can be changed in **Menu → Rules**:

- HIVE tip threshold beyond the two official calibration points (8 POLLEN; 3 POLLEN + 3 NECTAR)
- POLLEN and NECTAR mass
- HIVE swing time
- Whether PARK requires your *own* LOADING ZONE
- The 1:00 FLOWER audio cue (TBD in the manual)

Enforced automatically: G402, G403/G404, G407, G408, G410, G421 and G426/G427.
Rules that need a referee's judgement of intent (STRATEGIC, cards) are not modelled.

## Building from source

```bash
git lfs install
git clone https://github.com/guyka2212/biobuzz-vr-sim.git
```

1. Open the `biobuzz-vr-sim` folder with **Unity 6000.5.8f1** from Unity Hub.
2. Open `Assets/VrFsim/Scenes/Main.unity` and press Play. The game runs in the editor with
   or without a headset.
3. Build: menu **VrFsim → Build → Windows** (output `Builds/VrFsim/VrFsim.exe`), or headless:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.5.8f1/Editor/Unity.exe" -batchmode -quit -projectPath . -executeMethod VrFsim.EditorTools.BuildTool.BuildWindows -logFile Logs/build.log
```

Other editor tools under **VrFsim → Setup**: *Configure Project* (OpenXR, controller
profiles, URP settings), *Build Scene* (regenerates the Main scene from code), and *Bake
Lighting*.

### Tests

```bash
Tools/unity-test.sh EditMode   # rules, scoring, drive and ballistics maths
Tools/unity-test.sh PlayMode   # boots the scene: staging, HIVE tips, driving, launchers, Box Tube, a full match
Tools/unity-capture.sh         # renders review screenshots to Logs/Captures
```

## Project layout

```
Assets/VrFsim/
  Scripts/Runtime/  Core (units, bootstrap, materials) · Game (field spec, rules, field, HIVE, FLOWER)
                    Robot (drive physics, builder, controller, ballistics) · Match (flow, fouls, AUTO, human player)
                    Input · VR (XR rig, views) · UI (driver display, scoreboard, settings menu) · Audio
  Scripts/Editor/   project setup, scene builder, FBX import rules, build tool
  Art/Models/       Blender FBX models          Materials/  shared materials
  Resources/        input actions, material library, art slots
  Tests/            EditMode and PlayMode tests
Tools/              batch scripts (compile, test, capture) and Blender model scripts
Docs/               verified game facts and the dsim settings inventory
```

## Performance

The game is built for a stable 90 fps on a mid-range VR PC:
- URP with single-pass instanced stereo, baked static lighting (Subtractive mode), and no post-processing
- one realtime shadow cascade, for moving robots and elements only
- a handful of shared materials (SRP batcher) and low-polygon models (about 37k triangles in a busy scene)
- box and capsule colliders only, no mesh colliders
- procedurally generated audio, so the build carries no sound files
- build size about 77 MB, almost all of which is the Unity runtime

Measure it on your own PC with the built-in benchmark. It runs free drive with three practice
robots and a busy scripted drive for 30 s, then writes `benchmark.txt` next to the executable:

```bash
Builds/VrFsim/VrFsim.exe -benchmark
```

Reference result: GTX 1650 + i5-10400, desktop mode at 2560×1241 with the frame rate uncapped.
Average 1.22 ms, 99th percentile 1.95 ms, against the 11.1 ms budget for 90 fps. A headset
roughly doubles the pixel count and adds stereo overhead, so this leaves a large margin. With a
headset connected, the benchmark measures the real VR frame time instead.

## 3D models

All models are original and were made in Blender from published dimensions. The Python
scripts in `Tools/blender/` rebuild every model and export it to `Assets/VrFsim/Art/Models`:

| Script | Models |
|---|---|
| `field_base.py` | perimeter, tile floor and its interlocking-tile texture |
| `hive_frame.py`, `hive_tray.py` | HIVE frame, red and blue HIVE trays |
| `flower.py` | FLOWER |
| `elements.py` | POLLEN / NECTAR mesh and the perforation texture |
| `robot_chassis.py`, `robot_parts.py` | chassis, mecanum/traction/omni wheels, swerve module, turret, dumper, Box Tube, intake roller |

Run a script in Blender (Scripting tab, or through the Blender MCP bridge), then in Unity run
**VrFsim → Setup → Assign Art**, **Build Scene** and **Bake Lighting**. Models only supply
visuals; every collider comes from `FieldSpec`, so physics never depends on the art.

## Credits

- Settings and feature reference: [dsim](https://github.com/genius0412/dsim) by Dohun Kim
  (PolyForm Noncommercial). No dsim code or assets are used; VrFsim is an independent C# implementation.
- Game rules and field dimensions: *FIRST* Tech Challenge BIOBUZZ Competition Manual and Event
  Field Setup Guide. Field models are original Blender models built from published dimensions.
- "FIRST", "FIRST Tech Challenge", "FTC", "BIOBUZZ" and "RTX" are trademarks of their respective
  owners, used only to describe the game this simulator practises for.
