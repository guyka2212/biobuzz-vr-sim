# dsim settings inventory → VR sim mapping

Reference: <https://github.com/genius0412/dsim> (read 2026-10-07, default branch).
dsim is licensed **PolyForm Noncommercial 1.0.0**. No dsim code is copied into this project;
the C# here is an independent implementation. dsim was used only as a checklist of which
settings exist. Game facts come from FIRST's own documents (see `biobuzz-facts.md`).

Legend: **Carry** = implemented in the VR sim · **Adapt** = same intent, VR-specific form ·
**N/A** = belongs to dsim's web/online platform, not to an offline single-player VR sim.

## Robot build (`RobotSpec`, BIOBUZZ arm)

| dsim setting | values / range in dsim | VR sim |
|---|---|---|
| name, teamName, teamNumber | text / int | Carry |
| length, width (chassis) | 12–18 in, 0.5 in steps; swerve min width 13.5 | Carry |
| height deployed (`heightIn`) | 12–18 in (R105 cap 29 in) | Carry |
| stow height (`stowHeightIn`) | 12–deployed height | Carry |
| mass (`massLb`) | per drivetrain: mecanum/xdrive 18–42, tank 22–42, swerve 21.5–40, butterfly 24–42; floor rises with mechanisms | Carry |
| drivetrain | mecanum, tank, swerve, x-drive, butterfly | Carry |
| drive wheel RPM (`driveRpm`) | 200–600 (tank ≤560, swerve ≤500) | Carry |
| butterfly traction-mode RPM (`tankRpm`) | 200–560 | Carry |
| intake reach style (`intake`) | sloped, vector, triangle | Carry |
| intake archetype | sweeper, side-rollers, ramp | Carry |
| intake mount | front, back, side, front+back | Carry |
| launcher type | single turret, double turret, dumper (mandatory) | Carry |
| launcher mount | 3×3 grid: front/back/left/right/4 corners/center | Carry |
| second turret mount (double turret) | non-adjacent cell | Carry |
| hood angle (turretless launcher) | 70–85°, default 75 | Carry |
| lift (Box Tube placer) | none / box tube, 8 perimeter mounts | Carry |
| element storage (`ballStorage`) | 1–4 (G407) | Carry |
| pass target / pass preset | field point | Carry |
| chassis colour, accent colour, decal, plate | colours / text | Carry (colours + number plate) |
| saved robots library | list | Carry |
| robot presets | Pollinator (mecanum, turret + box tube), Forager (butterfly, dumper), Skimmer (x-drive, double turret), Sniper (swerve, single turret) | Adapt: four presets with the same archetypes, own tuning |

## Driver assists (`AssistConfig`)

| setting | VR sim |
|---|---|
| field-centric drive | Carry |
| aim assist (turret auto-aim at own up-CELL) | Carry |
| auto intake | Carry |
| auto fire | Carry |
| tank control mode: traditional / normal (arcade) | Carry |
| park speed % (slow-mode multiplier, default 30) | Carry |

## Controls (`ControlBindings`)

Actions: drive fwd/back/left/right, rotate CW/CCW, tank right-side fwd/back, intake, fire,
place POLLEN (Box Tube), place NECTAR, human-player NECTAR entry, ramp toggle, pass,
drive-mode toggle (butterfly shift), flip front, park (slow mode), start, restart,
view toggle, camera cycle, eye height up/down.

| setting | VR sim |
|---|---|
| keyboard bindings per action | Carry (keyboard is a secondary device) |
| gamepad button bindings per action (multi-button) | Carry via Input System rebinding |
| drive stick: left or right | Carry |
| stick deadzone (default 0.12) | Carry |
| response curve (exponent, default 1) | Carry |
| trigger threshold (default 0.35) | Carry |
| button chords/combos, chord grace ms | Carry (Input System composite bindings) |
| menu button, pad menu navigation | Carry |
| per-game binding overrides | N/A (one game) |

## Match / field

| setting | VR sim |
|---|---|
| mode: full match / free drive | Carry |
| alliance: red / blue | Carry |
| start position: preset anchors (close/far) + custom pose, saved poses | Carry |
| physics: 2D / 3D | Adapt: 3D only (PhysX) |
| practice robots: per seat none / dummy / AI, AI tier | Carry none + dummy; AI opponents in a later stage |
| autonomous path editor + enable | Adapt: selectable pre-built auto routines (a VR path editor is out of scope for v1) |
| show event log (foul/score feed) | Carry |

## Audio

master, game, shoot, intake, gate, beep, alert, voice volumes; sounds on/off; voice cues on/off → **Carry** (field audio cues follow Table 9-1).

## Graphics / camera (`GraphicsSettings`, camera prefs)

| setting | VR sim |
|---|---|
| render scale | Carry (XR eye texture scale) |
| max FPS | Adapt: headset refresh rate is fixed by the runtime; option for 72/80/90/120 where supported |
| anti-aliasing off / MSAA 2× / 4× | Carry |
| shadows off/low/high/soft, element shadows | Carry (baked lighting + optional realtime robot shadow) |
| ambient occlusion | N/A (no post-processing, perf requirement) |
| anisotropy, mesh detail, element detail | Carry (quality tiers) |
| environment (room, arena, gym, …) | Carry: 2–3 lightweight venues |
| reflections, effects level, camera motion | Carry (reduced motion = comfort option) |
| horizontal FOV | N/A in headset (fixed by optics) |
| minimap | Carry (wrist / HUD minimap) |
| perf overlay off / fps / full | Carry |
| graphics presets auto/low/medium/high/ultra | Carry |
| camera: driver station / overhead / chase / orbit / free | Carry: driver station (default), overhead, chase, robot POV, free-fly |
| driver eye: TOP/BOTTOM station slot, eye height | Carry |
| free-cam control presets (Onshape/Blender/…) | N/A (mouse-orbit schemes; VR uses head + stick) |

## Not carried (dsim platform features, not game settings)

Online multiplayer, accounts, ranked/Glicko, leaderboards, friends, LAN hosting, replays/video
export, ads/supporter cosmetics, mobile touch layout, analytics, in-browser tutorial. These are
web-service features rather than simulator settings; they can be revisited later if wanted.
