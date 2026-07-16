#!/usr/bin/env python3
"""Generate VibraForge haptic command files for each VR Doctor Fish mode.

Each output file is one line of JSON per command, in the exact format read by
Software_Design/Python_Server/Python_Play_Command.py from
https://github.com/pokemon9757/VibraForge:

    {"time": 2.0, "addr": 1, "mode": 1, "duty": 5, "freq": 6}

- time: seconds from playback start, ascending
- addr: vibration unit address (chain = addr // 16, unit = addr % 16)
- mode: 1 = start vibrating, 0 = stop (a unit keeps vibrating until stopped)
- duty: intensity level 0-15
- freq: frequency index 0-7 (see FREQ_HZ below)

Re-run this script after editing the constants below:

    python3 haptics/tools/generate_patterns.py

No dependencies beyond the Python 3 standard library. Randomised patterns use
a fixed seed so output is reproducible.
"""

import json
import random
from pathlib import Path

# ---------------------------------------------------------------------------
# Hardware layout - EDIT HERE when the actuator wiring is finalised
# ---------------------------------------------------------------------------

# Frequency index -> Hz, fixed by the VibraForge firmware (index = "freq" field).
FREQ_HZ = [123, 145, 170, 200, 235, 275, 322, 384]

# Per-leg layout. Confirmed so far: addresses are all even for one leg;
# 0, 2, 4, 16 (small) and 32 (big) are the current best guess.
LEFT_SMALL = [0, 2, 4, 16]
LEFT_BIG = 32

# Welcome-experience node map from the whiteboard sketch (one leg,
# front column A1/A2/A3, back column B1/B2). ASSUMED addresses: front
# nodes on chain 0 (0, 2, 4), back nodes on chain 1 (16, 18). Edit here
# when the wiring is final.
WELCOME_NODES = [
    # (node, addr, wave, start_s, stop_s)
    ("A1", 0, "noise", 0.0, 1.0),    # top front
    ("B1", 16, "sine", 1.0, 2.0),    # top back (gap)
    ("A2", 2, "sine", 2.0, 5.0),     # mid front
    ("B2", 18, "sine", 5.0, 7.0),    # low back (gap)
    ("A3", 4, "sine", 7.0, 10.0),    # bottom front
]

# ASSUMPTION: the other leg mirrors the even addresses at +1 (left/right
# pairing). Update these two lines if the team assigns different numbers.
RIGHT_SMALL = [a + 1 for a in LEFT_SMALL]
RIGHT_BIG = LEFT_BIG + 1

# Patterns are written straight into the Unity project so the engine and the
# standalone Python player always read the same files.
OUTPUT_DIR = (Path(__file__).resolve().parents[2]
              / "Assets" / "StreamingAssets" / "haptics")

# Max commands the player batches into one BLE packet per timestamp.
MAX_COMMANDS_PER_TIMESTAMP = 10


# ---------------------------------------------------------------------------
# Command list builder
# ---------------------------------------------------------------------------

class Pattern:
    """Collects timed start/stop commands and writes them as JSON lines."""

    def __init__(self, name):
        self.name = name
        self.commands = []

    def start(self, t, addr, duty, freq):
        self._add(t, addr, 1, duty, freq)

    def stop(self, t, addr):
        self._add(t, addr, 0, 0, 0)

    def pulse(self, t, addr, duration, duty, freq):
        """Vibrate one unit for `duration` seconds, then stop it."""
        self.start(t, addr, duty, freq)
        self.stop(t + duration, addr)
        return t + duration

    def ramp(self, t, addr, steps, step_duration, freq):
        """Re-trigger one unit with changing duty (the player's ramp idiom:
        repeated mode-1 commands, one final stop)."""
        for duty in steps:
            self.start(t, addr, duty, freq)
            t += step_duration
        self.stop(t, addr)
        return t

    def _add(self, t, addr, mode, duty, freq):
        if not 0 <= duty <= 15:
            raise ValueError(f"{self.name}: duty {duty} out of range 0-15")
        if not 0 <= freq <= 7:
            raise ValueError(f"{self.name}: freq {freq} out of range 0-7")
        self.commands.append(
            {"time": round(t, 3), "addr": addr, "mode": mode,
             "duty": duty, "freq": freq}
        )

    def finish(self):
        """Sort by time (stops before starts on ties) and stop every unit
        that is still running at the end, so nothing is left vibrating."""
        self.commands.sort(key=lambda c: (c["time"], c["mode"]))
        running = {}
        for c in self.commands:
            if c["mode"] == 1:
                running[c["addr"]] = True
            else:
                running.pop(c["addr"], None)
        if running:
            t_end = self.commands[-1]["time"] + 0.01
            for addr in sorted(running):
                self.stop(t_end, addr)
            self.commands.sort(key=lambda c: (c["time"], c["mode"]))
        self._validate()

    def _validate(self):
        per_timestamp = {}
        last_t = -1.0
        state = {}
        for c in self.commands:
            if c["time"] < last_t:
                raise ValueError(f"{self.name}: timestamps not ascending")
            last_t = c["time"]
            per_timestamp[c["time"]] = per_timestamp.get(c["time"], 0) + 1
            state[c["addr"]] = c["mode"]
        worst = max(per_timestamp.values())
        if worst > MAX_COMMANDS_PER_TIMESTAMP:
            raise ValueError(
                f"{self.name}: {worst} commands share one timestamp "
                f"(max {MAX_COMMANDS_PER_TIMESTAMP})"
            )
        stuck = [a for a, m in state.items() if m == 1]
        if stuck:
            raise ValueError(f"{self.name}: units left running: {stuck}")

    def write(self):
        OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
        path = OUTPUT_DIR / f"{self.name}.json"
        with open(path, "w") as f:
            for c in self.commands:
                f.write(json.dumps(c) + "\n")
        duration = self.commands[-1]["time"] if self.commands else 0.0
        print(f"wrote {path.name}: {len(self.commands)} commands, "
              f"{duration:.2f}s")


# ---------------------------------------------------------------------------
# Mode: idle water (ambient, loop by replaying the file)
# ---------------------------------------------------------------------------

IDLE_DURATION = 12.0       # seconds per loop
IDLE_WAVE_PERIOD = 3.0     # one swell sweeps a leg every N seconds
IDLE_SWELL_LENGTH = 1.2    # how long each unit stays on per swell
IDLE_STAGGER = 0.4         # delay between neighbouring units in a sweep
IDLE_DUTY = 2              # very gentle
IDLE_FREQ = 1              # 145 Hz


def build_idle_water():
    p = Pattern("idle_water")
    t = 0.0
    while t + IDLE_STAGGER * 3 + IDLE_SWELL_LENGTH < IDLE_DURATION:
        for i, (left, right) in enumerate(zip(LEFT_SMALL, RIGHT_SMALL)):
            onset = t + i * IDLE_STAGGER
            p.pulse(onset, left, IDLE_SWELL_LENGTH, IDLE_DUTY, IDLE_FREQ)
            # Right leg mirrors slightly later so the water feels alive.
            p.pulse(onset + 0.15, right, IDLE_SWELL_LENGTH, IDLE_DUTY,
                    IDLE_FREQ)
        t += IDLE_WAVE_PERIOD
    p.finish()
    return p


# ---------------------------------------------------------------------------
# Mode: small fish nibble (ticklish, loop by replaying the file)
# Audio: drfish_fish_se1.mp3 / drfish_fish_se2.mp3
# ---------------------------------------------------------------------------

NIBBLE_SEED = 20260715     # change for a different (still reproducible) take
NIBBLE_DURATION = 10.0     # seconds per loop
NIBBLE_DUTY_RANGE = (3, 6)
NIBBLE_FREQS = [6, 7]      # 322 / 384 Hz
NIBBLE_PULSE_RANGE = (0.04, 0.08)   # single tap length, seconds
NIBBLE_GAP_RANGE = (0.05, 0.12)     # gap between taps within a burst
NIBBLE_TAPS_RANGE = (2, 5)          # taps per burst
NIBBLE_REST_RANGE = (0.10, 0.60)    # pause before the next fish arrives


def build_small_fish_nibble():
    rng = random.Random(NIBBLE_SEED)
    p = Pattern("small_fish_nibble")
    all_small = LEFT_SMALL + RIGHT_SMALL
    t = 0.2
    while t < NIBBLE_DURATION - 0.5:
        addr = rng.choice(all_small)
        duty = rng.randint(*NIBBLE_DUTY_RANGE)
        freq = rng.choice(NIBBLE_FREQS)
        for _ in range(rng.randint(*NIBBLE_TAPS_RANGE)):
            t = p.pulse(t, addr, rng.uniform(*NIBBLE_PULSE_RANGE), duty, freq)
            t += rng.uniform(*NIBBLE_GAP_RANGE)
        t += rng.uniform(*NIBBLE_REST_RANGE)
    p.finish()
    return p


# ---------------------------------------------------------------------------
# Mode: big fish bite (one-shot, left leg; add +1 to addresses for right leg)
# Audio: drfish_big_se1.mp3
# ---------------------------------------------------------------------------

BITE_APPROACH_DUTY = 5
BITE_APPROACH_FREQ = 6         # 322 Hz, like a curious nudge
BITE_DUTY_RAMP = [15, 15, 15, 15, 15, 10, 6, 3]  # hold then decay
BITE_STEP = 0.12               # seconds per ramp step (~1s of bite)
BITE_FREQ = 0                  # 123 Hz, deep and heavy


def build_big_fish_bite():
    p = Pattern("big_fish_bite")
    # Two light approach nudges on the small actuators near the big one.
    p.pulse(0.0, LEFT_SMALL[0], 0.06, BITE_APPROACH_DUTY, BITE_APPROACH_FREQ)
    p.pulse(0.25, LEFT_SMALL[1], 0.06, BITE_APPROACH_DUTY + 1,
            BITE_APPROACH_FREQ)
    # The bite: strong hold on the big actuator, then a decaying release.
    p.ramp(0.60, LEFT_BIG, BITE_DUTY_RAMP, BITE_STEP, BITE_FREQ)
    p.finish()
    return p


# ---------------------------------------------------------------------------
# Mode: jellyfish sting (one-shot, left leg; add +1 to addresses for right leg)
# Audio: drfish_jelly_se1.mp3
# ---------------------------------------------------------------------------

STING_BUZZES = 6
STING_ON = 0.025               # seconds on
STING_OFF = 0.025              # seconds off
STING_DUTY = 15
STING_FREQ = 7                 # 384 Hz, sharp and electric


def build_jellyfish_sting():
    p = Pattern("jellyfish_sting")
    t = 0.0
    helpers = LEFT_SMALL[:2]   # nearby small units make it feel like a zap
    for i in range(STING_BUZZES):
        p.pulse(t, LEFT_BIG, STING_ON, STING_DUTY, STING_FREQ)
        p.pulse(t, helpers[i % len(helpers)], STING_ON, STING_DUTY,
                STING_FREQ)
        t += STING_ON + STING_OFF
    p.finish()
    return p


# ---------------------------------------------------------------------------
# Mode: welcome experience (continuing, one-shot at session start)
# A wave that travels down the leg: A1 (top front) -> B1 (top back) ->
# A2 (mid front) -> B2 (low back) -> A3 (bottom front) over 10 seconds,
# with linear crossfades between neighbouring nodes so the sensation
# flows continuously instead of switching abruptly (per whiteboard:
# "both are fine, linear is easier to do").
# Audio: drfish.mp3 (BGM starts with the experience)
# ---------------------------------------------------------------------------

WELCOME_SEED = 20260716        # jitter seed for the white-noise node
WELCOME_XFADE = 0.5            # seconds of linear crossfade at each handover
WELCOME_STEP = 0.1             # envelope update interval, seconds
WELCOME_PEAK_DUTY = 6          # gentle: this is a relaxing welcome
WELCOME_SINE_FREQ = 2          # 170 Hz, soft rolling feel
WELCOME_NOISE_FREQ = 4         # 235 Hz base for the noise/PWM node
WELCOME_NOISE_JITTER = (0.5, 1.0)  # random amplitude factor per step


def _welcome_envelope(t, start, stop, t_first, t_last):
    """Linear crossfade envelope: ramp in over WELCOME_XFADE centred on
    `start`, hold, ramp out centred on `stop`. The first node fades in
    from silence at t=0 and the last node fades out to silence."""
    fade_in_start = start - WELCOME_XFADE / 2
    fade_out_end = stop + WELCOME_XFADE / 2
    if start <= t_first:
        fade_in_start = start  # timeline begins already at level
    if t < fade_in_start or t >= fade_out_end:
        return 0.0
    if t < fade_in_start + WELCOME_XFADE:
        return min(1.0, (t - fade_in_start) / WELCOME_XFADE)
    if t > fade_out_end - WELCOME_XFADE:
        return max(0.0, (fade_out_end - t) / WELCOME_XFADE)
    return 1.0


def build_welcome_experience():
    rng = random.Random(WELCOME_SEED)
    p = Pattern("welcome_experience")
    t_first = WELCOME_NODES[0][3]
    t_last = max(stop for _, _, _, _, stop in WELCOME_NODES)
    last_duty = {addr: 0 for _, addr, _, _, _ in WELCOME_NODES}
    steps = int(round((t_last + WELCOME_XFADE) / WELCOME_STEP)) + 1
    for i in range(steps):
        t = round(i * WELCOME_STEP, 3)
        for _, addr, wave, start, stop in WELCOME_NODES:
            env = _welcome_envelope(t, start, stop, t_first, t_last)
            level = env
            freq = WELCOME_SINE_FREQ
            if wave == "noise":
                level = env * rng.uniform(*WELCOME_NOISE_JITTER)
                freq = WELCOME_NOISE_FREQ
            duty = round(WELCOME_PEAK_DUTY * level)
            if duty == last_duty[addr]:
                continue
            if duty == 0:
                p.stop(t, addr)
            else:
                p.start(t, addr, duty, freq)
            last_duty[addr] = duty
    p.finish()
    return p


# ---------------------------------------------------------------------------

def main():
    for build in (build_welcome_experience, build_idle_water,
                  build_small_fish_nibble, build_big_fish_bite,
                  build_jellyfish_sting):
        build().write()


if __name__ == "__main__":
    main()
