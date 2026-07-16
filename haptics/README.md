# Haptic Patterns

Vibration "scores" for each interaction mode of the VR Doctor Fish experience,
written for the [pokemon9757/VibraForge](https://github.com/pokemon9757/VibraForge)
toolkit. The pattern files live in
[`Assets/StreamingAssets/haptics/`](../Assets/StreamingAssets/haptics/) so the
Unity `HapticController` loads them at runtime, and each file can also be
played directly on the hardware with the toolkit's command player:

```
python Python_Play_Command.py -file Assets/StreamingAssets/haptics/big_fish_bite.json
```

(`Python_Play_Command.py` lives in `Software_Design/Python_Server/` of the
VibraForge repo. Pass `-uuid` / `-name` if your control unit differs from the
defaults.)

## Modes

| File | Interaction | Feel | Length | Matching audio |
|---|---|---|---|---|
| `welcome_experience.json` | Entering the water at session start | A continuous wave that travels down the leg, node to node, with linear crossfades | 10 s, one-shot | `drfish.mp3` (BGM starts here) |
| `idle_water.json` | Ambient water while feet are in the pool | Very gentle staggered swells across the small actuators | ~12 s, replay to loop | `drfish.mp3` (BGM) |
| `small_fish_nibble.json` | Small fish nibbling the feet | Light, ticklish, irregular taps at random spots | ~9 s, replay to loop | `drfish_fish_se1.mp3` / `drfish_fish_se2.mp3` |
| `big_fish_bite.json` | Large fish bite | Two curious nudges, then a deep strong bite that decays | ~1.6 s, one-shot | `drfish_big_se1.mp3` |
| `jellyfish_sting.json` | Jellyfish sting / electric zap | Sharp rapid max-intensity buzz | ~0.3 s, one-shot | `drfish_jelly_se1.mp3` |

The one-shot patterns (`welcome_experience`, `big_fish_bite`,
`jellyfish_sting`) are authored for the **left leg** (even addresses). For the
right leg, add +1 to every `addr`, or change the constants in the generator
(see below). In Unity the `HapticController` does this remap at playback time:
pass `HapticLeg.Right` or `HapticLeg.Both` to `PlayOneShot`.

### Welcome experience (continuing experience)

Per the whiteboard design, the 10-second welcome sweeps a sensation down the
leg through five nodes, overlapping neighbours with a **0.5 s linear
crossfade** ("both are fine, linear is easier to do") so the feeling is
continuous rather than a series of separate buzzes:

| Node | Physical position | Wave | Start | Stop |
|---|---|---|---|---|
| A1 | top front | white noise / PWM | 0 s | 1 s |
| B1 | top back (gap) | sine (rolling) | 1 s | 2 s |
| A2 | mid front | sine (rolling) | 2 s | 5 s |
| B2 | low back (gap) | sine (rolling) | 5 s | 7 s |
| A3 | bottom front | sine (rolling) | 7 s | 10 s |

Two artefacts implement this design:

- `Assets/StreamingAssets/haptics/welcome_experience.json` — playable command
  file. The crossfade is approximated by re-sending `mode: 1` with a stepped
  duty every 100 ms (rising on the incoming node while falling on the
  outgoing one); the noise node adds random duty jitter per step.
- `welcome_experience.csv` (this folder) — the pre-authored node-level spec
  (node, position, addresses, wave, start/stop, crossfade), kept as the
  design source of truth for the timeline.

## Command format

One JSON object per line, exactly what `Python_Play_Command.py` reads:

```json
{"time": 2.0, "addr": 1, "mode": 1, "duty": 5, "freq": 6}
```

| Field | Meaning | Range |
|---|---|---|
| `time` | Seconds from playback start (ascending; max 10 commands may share a timestamp) | ≥ 0 |
| `addr` | Vibration unit address (chain = `addr // 16`, unit in chain = `addr % 16`) | 0–127 |
| `mode` | 1 = start vibrating, 0 = stop. **A unit keeps vibrating until it receives a stop** | 0 / 1 |
| `duty` | Intensity level | 0–15 |
| `freq` | Frequency index | 0–7 |

Frequency index to Hz: `0=123, 1=145, 2=170, 3=200, 4=235, 5=275, 6=322, 7=384`.

Re-sending `mode: 1` to a running unit with a new `duty` changes its intensity
without stopping it — that is how the bite's decay ramp works.

In Unity, one line corresponds to one call of
`VibraForge.SendCommand(addr, mode, duty, freq)` (see the toolkit's
`Unity_Engine_API`). `Assets/Scripts/DoctorFish/HapticController.cs` parses
these files at runtime and replays them with exactly that mapping, so the
Python player and the engine share one source of truth.

## Actuator layout

Per leg: small (weak) actuators + 1 big (strong) actuator. Addresses on one
leg are all even; the welcome sketch names five nodes per leg (A1/A2/A3 front,
B1/B2 back).

| Unit | Left leg (even addresses, pending final wiring) | Right leg (ASSUMED: left + 1) |
|---|---|---|
| A1/A2/A3 (front, small) | 0, 2, 4 | 1, 3, 5 |
| B1/B2 (back, small) | 16, 18 (18 is assumed) | 17, 19 |
| Big | 32 | 33 |

The right-leg addresses and the exact small-actuator positions are still
assumptions. When the wiring is final, edit `LEFT_SMALL` / `LEFT_BIG` /
`RIGHT_SMALL` / `RIGHT_BIG` at the top of `tools/generate_patterns.py` and
regenerate.

In Unity this layout is mirrored visually: `DoctorFishBootstrap` places an
anchor per address on the virtual leg (`HapticNodeLayout`) and
`HapticNodeGlow` lights a small glow at that anchor for every command the
`HapticController` issues, so the on-screen position always matches the
vibrating unit. If the wiring changes, update `HapticNodeLayout` alongside
the generator constants.

## Tuning the patterns

Do not edit the JSON files by hand — they are generated. Instead:

1. Open `tools/generate_patterns.py`
2. Adjust the constants at the top of each mode section (duty, frequency
   index, pulse lengths, gaps, ramp shape, random seed)
3. Regenerate all files: `python3 haptics/tools/generate_patterns.py`

The generator validates every file: timestamps ascending, at most 10 commands
per timestamp, duty/freq in range, and no unit left vibrating at the end.

## Notes

- The generated pattern files live in `Assets/StreamingAssets/haptics/` so
  they ship inside the Unity build; this folder keeps the design docs, the
  CSV spec and the generator (`tools/generate_patterns.py`).
- Randomised patterns use a fixed seed, so regeneration is reproducible.
