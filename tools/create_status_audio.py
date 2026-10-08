"""Rebuild original short status cues (PCM mono, no loops, no external samples)."""
from pathlib import Path
import math
import random
import struct
import wave

RATE = 24000
OUT = Path(__file__).resolve().parents[1] / 'music'


def write(name, seconds, sample):
    values = [sample(i / RATE) for i in range(round(seconds * RATE))]
    peak = max(abs(value) for value in values) or 1
    data = b''.join(struct.pack('<h', round(value / peak * 0.65 * 32767)) for value in values)
    with wave.open(str(OUT / name), 'wb') as audio:
        audio.setnchannels(1)
        audio.setsampwidth(2)
        audio.setframerate(RATE)
        audio.writeframes(data)


def bleed(t):
    # Two soft low thumps, each with a tiny fluid click.
    result = 0
    for delay, gain in ((0, 1), (0.14, 0.65)):
        age = t - delay
        if age >= 0:
            envelope = (1 - math.exp(-age * 500)) * math.exp(-age * 28)
            result += gain * envelope * (math.sin(2 * math.pi * 72 * age) +
                0.12 * math.sin(2 * math.pi * 510 * age) * math.exp(-age * 70))
    return result


rng = random.Random(8026)

def poison(t):
    # Brief damped bubbling notes with a low, breath-like noise tail.
    result = 0
    for delay, pitch in ((0, 540), (0.09, 380), (0.18, 650)):
        age = t - delay
        if age >= 0:
            envelope = (1 - math.exp(-age * 300)) * math.exp(-age * 25)
            result += envelope * math.sin(2 * math.pi * (pitch * age - 350 * age * age)) * 0.55
    result += rng.uniform(-1, 1) * 0.035 * math.sin(math.pi * min(1, t / 0.4))
    return result


write('status_bleed.wav', 0.38, bleed)
write('status_poison.wav', 0.4, poison)
