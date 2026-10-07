# BIOBUZZ — verified game facts

Sources (downloaded 2026-10-07):

- **Competition Manual, Version TU03** — <https://ftc-resources.firstinspires.org/ftc/game/manual> (173 pp.)
- **Event Field Setup Guide V1.0** — <https://ftc-resources.firstinspires.org/ftc/field/eventfieldguide>

Field frame used in code: origin at field centre, +X = audience's right, +Z = away from the
audience (Unity is Y-up), 1 Unity unit = 1 m. Red alliance is on the audience's LEFT (§9.5).

## Match timing (§10.1, §10.4, Table 9-1)

| period | length |
|---|---|
| AUTO | 0:30 (no driver input) |
| AUTO→TELEOP transition | 0:08 (no powered movement, G403) |
| TELEOP | 2:00 |
| FLOWER ownership unlocked / NECTAR may enter FLOWERS | at 1:00 remaining (G410) |
| "Final 20 seconds" cue | 0:20 (audio only, no rule attached) |

Audio cues: start "Cavalry Charge", AUTO end "Buzzer ×3", transition "Drivers, pick up your
controllers, 3-2-1", TELEOP "3 Bells", 1:00 **[TBD in the manual]**, 0:20 "Train Whistle",
end "3-second Buzzer".

## Field (§9.2–9.7)

- ~144 × 144 in inside the walls, 36 soft tiles (~24 in, 0.59 in thick). Official field CAD
  puts real tile pitch at 23.53 in and the wall inner faces at ±70.67 in.
- Perimeter wall ~11 in tall (CAD).
- LOADING ZONE ~23 × 11 in, tape included; red on the left wall in tile row 5 (rear half), blue
  on the right wall in row 2. Point-symmetric layout.
- GARDEN ~23 × 2 in (outside edge of tape); red in the audience-left corner along the audience
  wall, blue in the rear-right corner along the rear wall.
- ALLIANCE AREA ~97 × 54 in outside the field, centred on the side wall.
- HIVE Structure at field centre: frame 49.46 in wide × 38.95 in deep, pivots 43.95 in up.
  Two HIVES (red at x < 0, blue at x > 0, 25.5 in centre to centre), each a bar with two CELLS,
  bi-stable, tilted 30° each way. CELL opening ~20 × 14 × 12 in. Top of up-CELL opening 65.6 in,
  bottom 53.5 in. Staged: red's audience-side cell up, blue's rear-side cell up (Fig 10-2, EFG §11.1).
- FLOWER ×4, attached at perimeter panel joints: left wall y ≈ −24, rear wall x ≈ −24, right wall
  y ≈ +24, audience wall x ≈ +24 (Fig 9-2; EFG §10). Top ring 4 in bore at 21.5 in; backstop
  1.25 in; retrieval opening 3.55 in tall; lower ring 0.4 in with 2.79 in hole.
- AprilTags 36h11, 3.25 in, four per CELL underside: red rear 30–33, red audience 34–37,
  blue audience 38–41, blue rear 42–45.

## Scoring elements (§9.8, §10.3.1)

- POLLEN: ~2.8 in yellow, 40 total. NECTAR: ~3.6 in, 8 red + 8 blue. Perforated plastic balls.
- Staging: 4 POLLEN in each FLOWER, 4 in each GARDEN, 4 preloaded per robot; 3 NECTAR in each
  up-CELL of its colour, 5 NECTAR in each ALLIANCE AREA.

## Points (Table 10-2) and RP (Table 10-3)

| achievement | AUTO | TELEOP |
|---|---|---|
| LEAVE (no longer touching the perimeter wall) | 3 | — |
| PARK (at least partly in LOADING ZONE) | 5 | 5 |
| HIVE TIP | 20 | 20 |
| POLLEN/NECTAR remaining in up-CELL at end | — | 2 each |
| Bottom NECTAR Bonus (per FLOWER) | — | 5 |
| POLLEN/NECTAR in an owned FLOWER | — | 2 each |
| POLLEN/NECTAR in GARDEN (credited to garden colour) | — | 1 each |

FLOWER owner = alliance with the top-most NECTAR of its colour in the scoring volume.
RP: SWARM (LEAVE+PARK ≥ 16), POLLINATOR 1 (≥ 4 TIPS), POLLINATOR 2 (≥ 7 TIPS), WIN 3, TIE 1.
Fouls: MINOR = 5, MAJOR = 20 to the opponent.

## HIVE tip threshold (EFG §12)

Each HIVE is calibrated to tip at **8 POLLEN + 0 NECTAR** and at **3 POLLEN + 3 NECTAR**
(7th POLLEN tossed in must NOT tip; 2nd POLLEN with 3 NECTAR must NOT tip).

## Rules the sim enforces

G304 start position · G402 AUTO side · G403/G404 no movement in transition/after end ·
G407 control ≤ 4 · G408 no opponent NECTAR · G410 NECTAR into FLOWER before 1:00 = MAJOR per
NECTAR · G421 pin > 3 s = MAJOR + MAJOR per 3 s · G426/G427 human NECTAR entry (one per own TIP,
all remaining at ≤ 1:00, via own LOADING ZONE) · R102 18 in start cube · R105 18 × 24 × 29 in
expanded.

## NOT published by FIRST — sim assumptions (user-adjustable where possible)

1. **Tip threshold for 1, 2, 4, 5 NECTAR.** Only the two calibration points above are official.
   The sim interpolates a torque model through them; the counts are exposed as settings.
2. **Element mass.** Not published. Defaults: POLLEN 30 g, NECTAR 55 g (adjustable).
3. **FLOWER scoring-volume bottom.** Manual says "between the top ring and the middle ring" and
   points to CAD. The sim uses the CAD's middle-ring height (~3.9–5.3 in).
4. **PARK in own LOADING ZONE only?** The rule says "the LOADING ZONE"; the zone is defined as
   alliance-specific. The sim uses own zone, with a setting to allow either.
5. **Exact FLOWER coordinates** come from the field CAD/figures, not manual text.
6. **HIVE swing time** (~4 s) is an estimate.
7. Championship/Regional RP thresholds are TBA; the 1:00 audio cue is TBA.
