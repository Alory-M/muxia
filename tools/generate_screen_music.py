"""Generate the project's original, seamless victory and defeat music loops.

Only Python's standard library is needed. Running this file recreates both WAV
assets with the same notes, envelopes and simple room echoes.
"""

import math
from pathlib import Path
import struct
import wave

RATE = 22050
MUSIC = Path(__file__).resolve().parents[1] / "music"


def compose(name, bpm, chords, melody, bright):
    beat = 60 / bpm
    length = round(len(chords) * 4 * beat * RATE)
    samples = [0.0] * length

    def note(at, duration, midi, amplitude, bell=False):
        start = round(at * RATE)
        count = round(duration * RATE)
        frequency = 440 * 2 ** ((midi - 69) / 12)
        for index in range(count):
            elapsed = index / RATE
            if bell:
                envelope = min(1, elapsed / 0.012) * math.exp(-3.6 * elapsed / duration)
                tone = math.sin(math.tau * frequency * elapsed)
                tone += 0.26 * math.sin(math.tau * frequency * 2 * elapsed) * math.exp(-5 * elapsed)
            else:
                envelope = min(1, elapsed / 0.12, (duration - elapsed) / 0.2)
                tone = math.sin(math.tau * frequency * elapsed)
                tone += 0.16 * math.sin(math.tau * frequency * 2 * elapsed)
            envelope *= min(1, (duration - elapsed) / 0.04)
            samples[(start + index) % length] += tone * envelope * amplitude

    for bar, chord in enumerate(chords):
        at = bar * 4 * beat
        for pitch in chord:
            note(at, 4.5 * beat, pitch, 0.055 if bright else 0.075)
        note(at, 3.8 * beat, chord[0] - 12, 0.10 if bright else 0.14)
        for step in range(8):
            pitch = melody[bar % len(melody)][step]
            if pitch:
                note(at + step * beat / 2, beat * (1.3 if bright else 2.1), pitch,
                     0.15 if bright else 0.09, bell=True)
        if bright:
            for step in range(4):
                note(at + step * beat, beat * 0.7, chord[step % 3] + 12, 0.045, bell=True)

    original = samples.copy()
    for seconds, gain in ((0.173, 0.14), (0.371, 0.09)):
        delay = round(seconds * RATE)
        for index, value in enumerate(original):
            samples[(index + delay) % length] += value * gain
    gain = 0.65 / max(abs(sample) for sample in samples)
    with wave.open(str(MUSIC / name), "wb") as output:
        output.setparams((1, 2, RATE, length, "NONE", "not compressed"))
        output.writeframes(b"".join(struct.pack("<h", round(sample * gain * 32767)) for sample in samples))


if __name__ == "__main__":
    compose("BGM_victory.wav", 96,
            [(48, 52, 55), (53, 57, 60), (57, 60, 64), (55, 59, 62)] * 2,
            [[64, 67, 72, 74, 76, 74, 72, 67], [65, 69, 72, 76, 77, 76, 72, 69],
             [69, 72, 76, 79, 76, 72, 74, 76], [67, 71, 74, 79, 74, 71, 67, 62]], True)
    compose("BGM_defeat.wav", 72,
            [(45, 48, 52), (41, 45, 48), (48, 52, 55), (43, 47, 50)],
            [[64, 0, 60, 0, 57, 0, 55, 0], [60, 0, 57, 0, 53, 0, 52, 0],
             [55, 0, 60, 0, 64, 0, 60, 0], [59, 0, 55, 0, 50, 0, 52, 0]], False)
