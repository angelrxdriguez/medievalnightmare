"""Sintetiza los sonidos placeholder de combate en assets/audio/.

Uso y tirar, como todo tools/: se ejecuta una vez (python tools/make_placeholder_audio.py)
y deja WAVs de 22050 Hz mono. La frecuencia es baja a proposito: es la del
audio de la epoca que imita el juego, y de paso disimula que son sintesis
barata. La semilla es fija para que regenerar no cambie ningun sonido.

Nada de dependencias: wave + math + random, que vienen con Python.
"""

import math
import random
import struct
import wave
from pathlib import Path

RATE = 22050
OUT = Path(__file__).resolve().parent.parent / "assets" / "audio"

random.seed(7)


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def mix(base, extra, at=0.0):
    """Suma extra sobre base a partir del segundo at, alargando si hace falta."""
    start = int(RATE * at)
    end = start + len(extra)
    if end > len(base):
        base.extend([0.0] * (end - len(base)))
    for i, sample in enumerate(extra):
        base[start + i] += sample
    return base


def damped_sine(freq, tau, seconds, amp=1.0, freq_end=None):
    """Un parcial que suena y se apaga. Es el atomo de todo lo metalico y oseo."""
    n = int(RATE * seconds)
    out = []
    phase = 0.0
    for i in range(n):
        t = i / RATE
        f = freq if freq_end is None else freq + (freq_end - freq) * (t / seconds)
        phase += 2.0 * math.pi * f / RATE
        out.append(amp * math.exp(-t / tau) * math.sin(phase))
    return out


def noise_burst(tau, seconds, amp=1.0, lowpass=0.0):
    """Ruido que se apaga. lowpass entre 0 (crudo) y 1 (sordo del todo)."""
    n = int(RATE * seconds)
    out = []
    last = 0.0
    for i in range(n):
        t = i / RATE
        sample = random.uniform(-1.0, 1.0) * amp * math.exp(-t / tau)
        last += (sample - last) * (1.0 - lowpass)
        out.append(last)
    return out


def whoosh(seconds, brightness, amp=1.0):
    """Aire cortado: ruido con un filtro que abre y cierra siguiendo el gesto.

    El pico va al 60 % del recorrido, que es donde el brazo va mas rapido; un
    pico centrado suena a viento, no a golpe.
    """
    n = int(RATE * seconds)
    out = []
    last = 0.0
    for i in range(n):
        t = i / n
        swing = math.sin(min(t / 0.6, 1.0) * math.pi * 0.5) if t < 0.6 else math.cos((t - 0.6) / 0.4 * math.pi * 0.5)
        swing = max(swing, 0.0) ** 1.5
        alpha = 0.02 + brightness * 0.3 * swing
        sample = random.uniform(-1.0, 1.0)
        last += (sample - last) * alpha
        out.append(last * amp * swing)
    return out


def clack(freq, amp=1.0):
    """Un choque de hueso: click de ruido + un parcial seco."""
    out = noise_burst(0.004, 0.02, amp)
    mix(out, damped_sine(freq, 0.014, 0.06, amp * 0.8))
    mix(out, damped_sine(freq * 2.7, 0.006, 0.03, amp * 0.35))
    return out


def normalize(samples, peak):
    top = max(1e-6, max(abs(s) for s in samples))
    return [s / top * peak for s in samples]


def save(name, samples, peak=0.85):
    samples = normalize(samples, peak)
    OUT.mkdir(parents=True, exist_ok=True)
    with wave.open(str(OUT / name), "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(b"".join(
            struct.pack("<h", int(max(-1.0, min(1.0, s)) * 32767)) for s in samples))
    print(name)


# --- Jugador -----------------------------------------------------------------

save("whoosh_light.wav", whoosh(0.16, 0.9), peak=0.55)
save("whoosh_heavy.wav", whoosh(0.34, 0.45, amp=1.2), peak=0.7)

# El golpe que conecta: click, dos clacks de hueso y un cuerpo grave que es el
# que hace que "pese". El grave cae de tono porque un golpe que sube suena a
# pregunta.
hit = clack(640, 1.0)
mix(hit, clack(1180, 0.5), at=0.008)
mix(hit, damped_sine(95, 0.09, 0.3, 0.9, freq_end=55), at=0.0)
save("hit_bone.wav", hit, peak=0.9)

# Bloquear: metal inharmonico. Los parciales no son multiplos a proposito,
# que es lo que separa una campana de una nota.
block = noise_burst(0.005, 0.02, 0.9)
for freq, tau, amp in ((523, 0.10, 0.9), (947, 0.08, 0.7), (1621, 0.06, 0.5), (2489, 0.045, 0.35), (3733, 0.03, 0.2)):
    mix(block, damped_sine(freq, tau, 0.4, amp))
save("hit_block.wav", block, peak=0.7)

# La guardia al romperse: el mismo metal pero desafinado por pares —bate— y con
# un cuerpo grave largo. Tiene que sonar a "esto se ha acabado", no a otro clang.
broke = noise_burst(0.02, 0.08, 0.9, lowpass=0.5)
for freq, tau, amp in ((496, 0.22, 0.9), (541, 0.20, 0.8), (1120, 0.15, 0.5), (1201, 0.13, 0.45)):
    mix(broke, damped_sine(freq, tau, 0.7, amp))
mix(broke, damped_sine(68, 0.2, 0.6, 1.0, freq_end=48))
save("guard_break.wav", broke, peak=0.85)

# Recibir: sordo y feo, sin nada de brillo. El brillo es de los golpes que das.
taken = noise_burst(0.06, 0.25, 0.8, lowpass=0.92)
mix(taken, damped_sine(82, 0.12, 0.3, 1.0, freq_end=50))
save("hit_taken.wav", taken, peak=0.8)

# Esquiva: aire suave y corto, sin cuerpo. Es un desplazamiento, no un ataque.
save("dash.wav", whoosh(0.26, 0.25, amp=0.8), peak=0.35)

# Ballesta: cuerda que restalla y virote que sale. El tono cae rapidisimo
# porque la cuerda se destensa, que es lo que la hace cuerda y no nota.
shot = clack(1900, 0.6)
mix(shot, damped_sine(210, 0.05, 0.2, 1.0, freq_end=90))
mix(shot, whoosh(0.12, 0.8, amp=0.5), at=0.01)
save("bolt_shot.wav", shot, peak=0.8)

# --- Esqueleto ---------------------------------------------------------------

# La anticipacion: huesos que crujen acelerando. Es telegrafia sonora, asi que
# el ritmo IMPORTA: acelera hacia el golpe igual que el brazo.
windup = silence(0.05)
t = 0.0
for i in range(9):
    gap = 0.075 - i * 0.005
    t += max(gap, 0.03)
    mix(windup, clack(random.uniform(850, 1650), 0.25 + i * 0.08), at=t)
save("skel_windup.wav", windup, peak=0.5)

save("skel_swing.wav", whoosh(0.2, 0.55), peak=0.55)

# El derrumbe: una cascada de clacks que se van apagando y espaciando, mas un
# rumor grave de monton que cae. El final espaciado es el que cuenta "ya esta".
fall = damped_sine(55, 0.25, 0.8, 0.5)
t = 0.0
for i in range(15):
    t += random.uniform(0.02, 0.05) * (1.0 + i * 0.15)
    mix(fall, clack(random.uniform(700, 1900), 1.0 - i * 0.055), at=t)
save("bones_collapse.wav", fall, peak=0.7)
