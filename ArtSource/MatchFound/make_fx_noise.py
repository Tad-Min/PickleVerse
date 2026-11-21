"""Seamless (tileable) fractal noise for the match-found smoke and wave shaders.

python make_fx_noise.py <out.png>
R, G, B hold three independent fBm fields; FFT filtering of white noise makes them wrap perfectly.
"""
import sys
import numpy as np
from PIL import Image

N = 256


def fbm(seed, beta):
    rng = np.random.default_rng(seed)
    white = rng.standard_normal((N, N))
    f = np.fft.fftfreq(N)
    k = np.sqrt(f[:, None] ** 2 + f[None, :] ** 2)
    k[0, 0] = 1.0
    spectrum = np.fft.fft2(white) / k ** beta
    spectrum[0, 0] = 0
    field = np.real(np.fft.ifft2(spectrum))
    lo, hi = np.percentile(field, [1, 99])
    return np.clip((field - lo) / (hi - lo), 0, 1)


rgb = np.stack([fbm(11, 1.9), fbm(23, 1.6), fbm(37, 1.25)], axis=-1)
Image.fromarray((rgb * 255).astype(np.uint8), "RGB").save(sys.argv[1], optimize=True)
print("noise", sys.argv[1])
