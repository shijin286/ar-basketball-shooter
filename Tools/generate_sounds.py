"""
AR Basketball Shooter - procedural sound effects (Stage 7).

Creates the 7 game sounds as 44.1 kHz, 16-bit, mono WAV files using only
Python's standard library. Every sound is built from sine waves, filtered
noise and decay envelopes, so the files are original and free to use in
your project and report.

Usage:   python generate_sounds.py [output_folder]
Default output folder: ../Assets/Audio (next to this Tools folder)
"""
import math
import os
import random
import struct
import sys
import wave

SR = 44100
TAU = 2 * math.pi


# ---------------------------------------------------------------- building blocks

def samples(duration):
    return int(duration * SR)


def noise(n, seed):
    rnd = random.Random(seed)
    return [rnd.uniform(-1.0, 1.0) for _ in range(n)]


def lowpass(signal, cutoff):
    a = 1.0 - math.exp(-TAU * cutoff / SR)
    y, out = 0.0, []
    for x in signal:
        y += a * (x - y)
        out.append(y)
    return out


def highpass(signal, cutoff):
    low = lowpass(signal, cutoff)
    return [x - l for x, l in zip(signal, low)]


def decay(t, tau):
    return math.exp(-t / tau)


def finish(signal, peak=0.89, fade_ms=8):
    """Normalise to the peak level and fade the end to avoid clicks."""
    top = max(abs(s) for s in signal) or 1.0
    out = [s * peak / top for s in signal]
    fade = samples(fade_ms / 1000)
    for i in range(min(fade, len(out))):
        out[-1 - i] *= i / fade
    return out


# ---------------------------------------------------------------- the sounds

def ball_bounce():
    """Rubber ball on a floor: low pitch-dropping thump + short slap + tiny air ring."""
    n = samples(0.35)
    slap = lowpass(noise(n, 1), 3000)
    out, phase = [], 0.0
    for i in range(n):
        t = i / SR
        phase += TAU * (95 + 60 * decay(t, 0.02)) / SR
        body = math.sin(phase) * decay(t, 0.07)
        ring = 0.25 * math.sin(TAU * 420 * t) * decay(t, 0.03)
        out.append(body + ring + 0.6 * slap[i] * decay(t, 0.006))
    return finish(out)


def rim_clank():
    """Steel rim: inharmonic metal partials with different decay times + a click."""
    n = samples(0.7)
    click = highpass(noise(n, 2), 2000)
    partials = [(523, 0.35, 1.0), (526, 0.35, 0.5), (1385, 0.22, 0.6),
                (2210, 0.12, 0.45), (3550, 0.07, 0.3), (4700, 0.05, 0.2)]
    out = []
    for i in range(n):
        t = i / SR
        metal = sum(a * math.sin(TAU * f * t) * decay(t, d) for f, d, a in partials)
        out.append(metal + 0.5 * click[i] * decay(t, 0.004))
    return finish(out)


def backboard_thud():
    """Hard board: mid thump + band-limited knock + a short plexiglass ring."""
    n = samples(0.3)
    knock = lowpass(highpass(noise(n, 3), 200), 1200)
    out, phase = [], 0.0
    for i in range(n):
        t = i / SR
        phase += TAU * (110 + 40 * decay(t, 0.03)) / SR
        out.append(math.sin(phase) * decay(t, 0.05)
                   + 0.5 * knock[i] * decay(t, 0.03)
                   + 0.15 * math.sin(TAU * 680 * t) * decay(t, 0.02))
    return finish(out)


def net_swish():
    """Ball through the net: bright filtered noise with a soft attack and string flutter."""
    n = samples(0.5)
    bright = lowpass(highpass(noise(n, 4), 2500), 8000)
    soft = lowpass(highpass(noise(n, 5), 600), 2500)
    out = []
    for i in range(n):
        t = i / SR
        env = t / 0.04 if t < 0.04 else decay(t - 0.04, 0.12)
        flutter = 1.0 + 0.3 * math.sin(TAU * 30 * t)
        out.append(env * flutter * (bright[i] + 0.4 * soft[i]))
    return finish(out, peak=0.8)


def shoot_whoosh():
    """Throw: low noise whose filter opens up, shaped like a gentle swell."""
    n = samples(0.35)
    src = noise(n, 6)
    out, y = [], 0.0
    for i in range(n):
        t = i / SR
        cutoff = 300 + 1500 * (t / 0.35)
        y += (1.0 - math.exp(-TAU * cutoff / SR)) * (src[i] - y)
        out.append(y * math.sin(math.pi * t / 0.35) ** 2)
    return finish(out, peak=0.7)


def button_click():
    """UI tap: very short high tick."""
    n = samples(0.05)
    tick = noise(n, 7)
    out = []
    for i in range(n):
        t = i / SR
        out.append(math.sin(TAU * 1800 * t) * decay(t, 0.008) + 0.3 * tick[i] * decay(t, 0.002))
    return finish(out, peak=0.6, fade_ms=3)


def score_chime():
    """Success: quick C6-E6-G6 bell arpeggio."""
    n = samples(0.9)
    notes = [(1046.5, 0.0), (1318.5, 0.08), (1568.0, 0.16)]
    out = [0.0] * n
    for freq, start in notes:
        for i in range(samples(start), n):
            t = i / SR - start
            attack = min(1.0, t / 0.003)
            bell = (math.sin(TAU * freq * t)
                    + 0.3 * math.sin(TAU * freq * 2.76 * t)
                    + 0.1 * math.sin(TAU * freq * 5.4 * t))
            out[i] += attack * bell * decay(t, 0.35)
    return finish(out, peak=0.75)


def countdown_tick():
    """Stage 9: short clean beep for the last seconds of the challenge."""
    n = samples(0.12)
    out = []
    for i in range(n):
        t = i / SR
        attack = min(1.0, t / 0.004)
        out.append(attack * (math.sin(TAU * 1320 * t) + 0.25 * math.sin(TAU * 2640 * t)) * decay(t, 0.045))
    return finish(out, peak=0.7, fade_ms=5)


def buzzer():
    """Stage 9: arena 'time up' buzzer - a buzzy square-like tone made of odd harmonics."""
    n = samples(0.8)
    out = []
    for i in range(n):
        t = i / SR
        env = min(1.0, t / 0.01) * (1.0 if t < 0.6 else decay(t - 0.6, 0.06))
        tone = sum(math.sin(TAU * 196 * k * t) / k for k in (1, 3, 5, 7, 9, 11))
        tone += 0.6 * sum(math.sin(TAU * 247 * k * t) / k for k in (1, 3, 5, 7))
        out.append(env * tone)
    return finish(out, peak=0.8)


SOUNDS = {
    "ball_bounce": ball_bounce,
    "rim_clank": rim_clank,
    "backboard_thud": backboard_thud,
    "net_swish": net_swish,
    "shoot_whoosh": shoot_whoosh,
    "button_click": button_click,
    "score_chime": score_chime,
    "countdown_tick": countdown_tick,
    "buzzer": buzzer,
}


def write_wav(path, signal):
    with wave.open(path, "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(SR)
        f.writeframes(b"".join(struct.pack("<h", int(max(-1.0, min(1.0, s)) * 32767)) for s in signal))


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    folder = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "..", "Assets", "Audio")
    os.makedirs(folder, exist_ok=True)
    for name, make in SOUNDS.items():
        signal = make()
        path = os.path.join(folder, name + ".wav")
        write_wav(path, signal)
        peak = max(abs(s) for s in signal)
        print(f"{name + '.wav':20} {len(signal) / SR:5.2f} s   peak {20 * math.log10(peak):5.1f} dBFS")


if __name__ == "__main__":
    main()
