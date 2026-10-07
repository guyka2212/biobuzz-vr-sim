# VrFsim — BIOBUZZ VR driving simulator

VrFsim is a PC VR practice simulator for the *FIRST* Tech Challenge 2026–27 game, **BIOBUZZ**.
You wear a PC VR headset, stand at the driver station, and drive a simulated FTC robot on a
full-size field with a gamepad, just like a real match.

> Work in progress. See [Docs/biobuzz-facts.md](Docs/biobuzz-facts.md) for the verified game
> rules this project implements and [Docs/dsim-settings-inventory.md](Docs/dsim-settings-inventory.md)
> for the settings it supports. Full setup and build instructions will be added as the build progresses.

## Status

- [x] Project cleaned, OpenXR (PC) configured
- [ ] Field, robot, scoring, match flow, VR menu, models, performance pass

## Credits

- Feature and settings reference: [dsim](https://github.com/genius0412/dsim) by Dohun Kim.
  No dsim code is used; this is an independent C# implementation.
- Game rules and field dimensions: *FIRST* Tech Challenge BIOBUZZ Competition Manual and
  Event Field Setup Guide.

This is an independent, fan-made simulator. It is not affiliated with, authorized, or endorsed by
*FIRST*, *FIRST* Tech Challenge, or RTX. "FIRST", "FIRST Tech Challenge", "FTC", and "BIOBUZZ" are
trademarks of their respective owners, used here only to describe the game being simulated.
