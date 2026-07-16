# VR Doctor Fish

**Experience a relaxing, virtual fish therapy from the comfort of your home.**

VR Doctor Fish recreates a doctor fish foot spa as a multi-sensory VR experience. The user sits with their feet in a real bucket of water while a Meta Quest 3 renders a virtual pool around their legs. Immersive visuals, spatial audio and localised vibrotactile feedback from VibraForge actuators worn on the legs combine to simulate gentle water flow, small fish nibbling, a big fish bite and jellyfish stings, before the water calms again and the session ends in relaxation.

## Project context

Developed at the **IVE 2026 Winter School: Haptics x XR**
13 – 17 July 2026 | Mawson Lakes, Adelaide University, Kaurna Land

## Team

- Mimi Yoshii-Podger
- Cerella YueRu Chen
- Anneysha Sarkar

## The experience

Real-world setup: sit on a chair, remove your shoes, put your feet into a real bucket of water and put on the Meta Quest 3. The experience then moves through a staged sequence, with sight, sound and touch coordinated at every stage.

| Stage | You see | You hear | You feel |
| --- | --- | --- | --- |
| 1. Welcome | Clear water flowing around your legs | Gentle water flow and soft music | A gentle wave of vibration rising slowly along both legs |
| 2. Small fish | Small fish swim toward your feet | Tiny nibbling sounds | Soft, ticklish vibrations at random points on your legs |
| 3. Big fish | A big fish approaches and bites | A strong splash and water movement | A strong, quick vibration |
| 4. Jellyfish | Jellyfish glide by your legs | Whooshing and electric sounds | Sharp, high-frequency vibrations like tiny stings |
| 5. Calm | The water becomes calm again | Gentle water flow and relaxing music | Vibrations fade away |

At the end the user takes off the headset and lifts their feet out of the bucket, ideally with a relaxed body and a happy mind.

## Getting started

1. Open the repository root in **Unity 6000.3.19f1** (the project uses URP, OpenXR and the Meta XR SDK; the Package Manager restores everything on first open).
2. Open the scene `Assets/Scenes/DoctorFish.unity`.
3. For haptics, start the VibraForge Python server from the [VibraForge](https://github.com/pokemon9757/VibraForge) repo (`Software_Design/Python_Server/`) so it bridges TCP to the BLE control unit. The Unity `TcpSender` connects to `localhost:9051` on play. Without the server the experience still runs with visuals and audio only.
4. Press Play. With a Meta Quest 3 connected over Link the camera is head tracked; without a headset, hold the right mouse button and drag to look around.

Debug keys: `N` skips to the next stage, `R` restarts the session.

The whole scene (tub, water, legs, fish, jellyfish, lighting) is built at runtime by `DoctorFishBootstrap` from primitives and the base materials in `Assets/Resources/DoctorFish`, so there are no model imports and the scene file stays tiny. Layout and stage durations are tunable in the inspector on the `DoctorFish` object.

## System architecture

The Unity application on the host computer runs the VR environment (virtual body, bucket and water, small fish, large fish, jellyfish, lighting and effects) and an **experience state manager** that steps through the stages above:

```
Water (gentle flow)
  -> Small fish  (low frequency, long duration)
  -> Large fish  (high frequency, high amplitude, short duration)
  -> Jellyfish   (very high frequency, high amplitude, short duration)
  -> Ending      (relaxation)
```

Each state drives three controllers in parallel (all in [`Assets/Scripts/DoctorFish/`](Assets/Scripts/DoctorFish/)):

- **Visual controller** (`VisualController.cs`) – per-stage lighting, fog and water moods plus the creature choreography (`SmallFishSchool`, `BigFishController`, `JellyfishSwarm`, `WaterSurface`)
- **Audio controller** (`AudioController.cs`) – water flow, fish, jellyfish and background music (see [`Assets/Resources/Audio/`](Assets/Resources/Audio/README.md))
- **Haptic controller** (`HapticController.cs`) – plays the pattern files from `Assets/StreamingAssets/haptics/` through the VibraForge Unity API as `SendCommand(address, start_stop, intensity, frequency)` calls (see [`haptics/`](haptics/README.md))

Creature contact events flow back the other way: a big fish bite or a jellyfish sting reported by the visual layer triggers the matching sound and one-shot vibration on the leg that was touched, so all three senses stay in sync.

### Haptics hardware pipeline

```
Unity (host computer)
  -> BLE -> ESP32 control unit   (receives and validates commands, converts to UART, manages chains)
  -> UART -> 2 chains x 5 vibration units
  -> PIC16F18313 MCU per unit    (receives UART command, drives H-bridge, controls actuator)
  -> DRV8837 driver + LRA/VCA actuator on the user's legs
```

The headset provides head tracking, the VR display and spatial audio; all haptic output runs through the [VibraForge](https://github.com/pokemon9757/VibraForge) toolkit.

### Haptic design

Two actuator types are combined with contrasting textures so each creature feels distinct:

- **Weak actuators, low intensity, high frequency** – the ticklish nibble of small fish
- **Strong actuators, high intensity, low frequency** – the big fish bite
- **Strong actuators, very high frequency, short bursts** – jellyfish stings

The 10-second welcome pattern sweeps a continuous sensation down the leg through five actuator nodes with linear crossfades between neighbours. The full node timeline, pattern files and generator are documented in [`haptics/README.md`](haptics/README.md).

## Repository structure

The repository root is the Unity project.

| Folder | Contents |
| --- | --- |
| `Assets/Scenes/DoctorFish.unity` | The experience scene (plus the original haptics starter sample scenes) |
| `Assets/Scripts/DoctorFish/` | Experience state manager, visual, audio and haptic controllers, procedural creatures and scene bootstrap |
| `Assets/Scripts/VibraForge/` | VibraForge Unity API (TCP sender and command wrapper) from the toolkit |
| `Assets/StreamingAssets/haptics/` | Generated VibraForge vibration patterns (JSON command files) |
| `Assets/Resources/Audio/` | Background music and sound effects, with usage notes per file |
| `Assets/Resources/DoctorFish/` | Base URP materials for water, creatures, jellyfish and bubbles |
| `haptics/` | Haptic design docs, the welcome-experience CSV spec and the Python pattern generator |

## Credits

- Sound effects: CC0, sourced via Pixabay and edited pitch via Audacity
- Background music: Sound motifs were generated via ACE Music and Gemini, edited and composed via GarageBand
- Haptics hardware and Unity plugin: [VibraForge](https://github.com/pokemon9757/VibraForge)

## License

MIT – see [LICENSE](LICENSE).
