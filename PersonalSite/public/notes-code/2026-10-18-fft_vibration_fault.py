#!/usr/bin/env python3
"""
FFT vibration / spectral fault diagnosis demo (2026-10-18).

Simulates a motor + rolling-element bearing vibration signal:
  shaft 1X + harmonics (healthy baseline) + inner-race fault (BPFI) + noise.
Detects the injected fault frequency via one-sided rFFT with a Hann window.

SEED=42 for reproducibility.
"""

from __future__ import annotations

import math
from pathlib import Path

import matplotlib.pyplot as plt
import numpy as np

SEED = 42
OUT_PNG = Path(__file__).resolve().parent / "fft_vibration_fault_demo.png"


def bearing_fault_freqs(
    shaft_hz: float,
    n_balls: int,
    ball_d: float,
    pitch_d: float,
    contact_angle_deg: float = 0.0,
) -> dict[str, float]:
    """Classic kinematic bearing defect frequencies (Hz).

    BPFO = ball pass frequency outer race
    BPFI = ball pass frequency inner race
    BSF  = ball spin frequency
    FTF  = fundamental train (cage) frequency
    """
    g = (ball_d / pitch_d) * math.cos(math.radians(contact_angle_deg))
    fs = shaft_hz
    return {
        "shaft_1X": fs,
        "FTF": 0.5 * fs * (1.0 - g),
        "BPFO": 0.5 * n_balls * fs * (1.0 - g),
        "BPFI": 0.5 * n_balls * fs * (1.0 + g),
        "BSF": 0.5 * (pitch_d / ball_d) * fs * (1.0 - g * g),
    }


def synthesize_vibration(
    t: np.ndarray,
    shaft_hz: float,
    bpfi_hz: float,
    rng: np.random.Generator,
) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    """Return (healthy, with_fault, noise_only_component).

    Healthy: shaft 1X + 2X + 3X (imbalance / misalignment-like baseline).
    Fault: add a tone at BPFI (simplified inner-race defect line).
    """
    # Amplitudes in arbitrary acceleration units
    healthy = (
        1.00 * np.sin(2 * np.pi * shaft_hz * t)
        + 0.35 * np.sin(2 * np.pi * 2 * shaft_hz * t + 0.4)
        + 0.15 * np.sin(2 * np.pi * 3 * shaft_hz * t + 1.1)
    )
    # Small random phase jitter on fault tone (still a clear spectral line)
    fault_amp = 0.22
    fault = fault_amp * np.sin(2 * np.pi * bpfi_hz * t + 0.7)
    noise = 0.18 * rng.standard_normal(t.shape[0])
    with_fault = healthy + fault + noise
    healthy_noisy = healthy + noise
    return healthy_noisy, with_fault, fault


def one_sided_amplitude_spectrum(
    x: np.ndarray, fs: float, window: np.ndarray | None
) -> tuple[np.ndarray, np.ndarray]:
    """Hann- or rectangular-windowed one-sided amplitude spectrum via rfft.

    Scaling: 2 / sum(w) so a pure sinusoid of amplitude A peaks near A
    (DC and Nyquist not doubled in the usual sense; we still use 2/sum(w)
    for all positive bins which is the common engineering display scale).
    """
    n = x.shape[0]
    if window is None:
        w = np.ones(n)
    else:
        w = window
    xw = x * w
    # coherent gain of the window
    cg = np.sum(w)
    y = np.fft.rfft(xw)
    freqs = np.fft.rfftfreq(n, d=1.0 / fs)
    # one-sided amplitude (exclude DC special-case doubling for simplicity
    # on positive bins only: double all except DC)
    amp = (2.0 / cg) * np.abs(y)
    amp[0] *= 0.5  # undo double for DC
    if n % 2 == 0:
        amp[-1] *= 0.5  # Nyquist
    return freqs, amp


def find_peak_near(
    freqs: np.ndarray, amp: np.ndarray, f0: float, half_width_hz: float
) -> tuple[float, float]:
    mask = (freqs >= f0 - half_width_hz) & (freqs <= f0 + half_width_hz)
    if not np.any(mask):
        return float("nan"), float("nan")
    idx_local = np.argmax(amp[mask])
    idx = np.flatnonzero(mask)[idx_local]
    return float(freqs[idx]), float(amp[idx])


def main() -> None:
    rng = np.random.default_rng(SEED)

    # --- machine / bearing geometry (deep-groove ball bearing, illustrative) ---
    rpm = 1800.0
    shaft_hz = rpm / 60.0  # 30 Hz
    n_balls = 9
    ball_d = 7.5  # mm
    pitch_d = 39.0  # mm
    contact_angle_deg = 0.0
    freqs_kin = bearing_fault_freqs(
        shaft_hz, n_balls, ball_d, pitch_d, contact_angle_deg
    )
    bpfi = freqs_kin["BPFI"]
    bpfo = freqs_kin["BPFO"]

    # --- sampling ---
    fs = 5120.0  # Hz; Nyquist = 2560 Hz
    n = 8192
    duration = n / fs
    df = fs / n
    t = np.arange(n) / fs

    healthy, with_fault, _fault_tone = synthesize_vibration(t, shaft_hz, bpfi, rng)

    # --- spectra: rectangular vs Hann ---
    hann = np.hanning(n)
    f_rect, a_rect = one_sided_amplitude_spectrum(with_fault, fs, None)
    f_hann, a_hann = one_sided_amplitude_spectrum(with_fault, fs, hann)
    # assert same freq axis
    assert np.allclose(f_rect, f_hann)
    freqs = f_hann

    # peak search around injected BPFI and around shaft 1X
    peak_bpfi_f, peak_bpfi_a = find_peak_near(freqs, a_hann, bpfi, 3 * df)
    peak_1x_f, peak_1x_a = find_peak_near(freqs, a_hann, shaft_hz, 3 * df)
    peak_bpfo_f, peak_bpfo_a = find_peak_near(freqs, a_hann, bpfo, 3 * df)

    # leakage comparison: energy in bins adjacent to BPFI (±1..3 bins), exclude peak bin
    def far_sidelobe_energy(amp: np.ndarray, f0: float, lo: int = 8, hi: int = 25) -> float:
        """Energy in bins at distance [lo, hi] from the peak bin.

        Near bins (1–3) mostly capture main-lobe width: Hann is wider there
        by design. Far bins show the classic leakage skirts that Hann suppresses.
        """
        i0 = int(np.argmin(np.abs(freqs - f0)))
        idxs = [i for i in range(len(amp)) if lo <= abs(i - i0) <= hi]
        return float(np.sum(amp[idxs] ** 2))

    leak_rect = far_sidelobe_energy(a_rect, bpfi)
    leak_hann = far_sidelobe_energy(a_hann, bpfi)

    # time-domain RMS: healthy vs fault — often too close for a simple threshold
    rms_healthy = float(np.sqrt(np.mean(healthy**2)))
    rms_fault = float(np.sqrt(np.mean(with_fault**2)))
    rms_delta_pct = 100.0 * (rms_fault - rms_healthy) / rms_healthy

    # SNR-ish: fault peak vs median noise floor in 140–180 Hz band excluding peak
    band = (freqs >= 140.0) & (freqs <= 180.0)
    i_peak = int(np.argmin(np.abs(freqs - peak_bpfi_f)))
    floor_mask = band.copy()
    floor_mask[max(0, i_peak - 2) : i_peak + 3] = False
    noise_floor = float(np.median(a_hann[floor_mask]))
    peak_to_floor = peak_bpfi_a / noise_floor if noise_floor > 0 else float("inf")

    print("=== FFT vibration fault diagnosis demo ===")
    print(f"SEED                  = {SEED}")
    print(f"shaft RPM / 1X        = {rpm:.1f} / {shaft_hz:.4f} Hz")
    print(
        f"bearing geometry      = N={n_balls}, d={ball_d} mm, D={pitch_d} mm, "
        f"theta={contact_angle_deg} deg"
    )
    print(f"BPFO (kinematic)      = {bpfo:.4f} Hz")
    print(f"BPFI (kinematic)      = {bpfi:.4f} Hz  <-- injected fault line")
    print(f"FTF / BSF             = {freqs_kin['FTF']:.4f} / {freqs_kin['BSF']:.4f} Hz")
    print(f"fs / N / duration     = {fs:.1f} Hz / {n} / {duration:.4f} s")
    print(f"Delta f = fs/N        = {df:.6f} Hz")
    print(f"Nyquist               = {fs/2:.1f} Hz")
    print("--- peak pick (Hann) ---")
    print(
        f"detected BPFI peak    = {peak_bpfi_f:.4f} Hz, amp={peak_bpfi_a:.6f}"
    )
    print(
        f"abs error vs true BPFI= {abs(peak_bpfi_f - bpfi):.6f} Hz "
        f"({abs(peak_bpfi_f - bpfi)/df:.3f} bins)"
    )
    print(f"detected 1X peak      = {peak_1x_f:.4f} Hz, amp={peak_1x_a:.6f}")
    print(
        f"amp near BPFO (no inj)= {peak_bpfo_f:.4f} Hz, amp={peak_bpfo_a:.6f} "
        f"(should be near noise)"
    )
    print("--- window leakage (far sidelobe energy, bins 8–25 from peak) ---")
    print(f"rectangular window    = {leak_rect:.6e}")
    print(f"Hann window           = {leak_hann:.6e}")
    print(f"leakage ratio R/H     = {leak_rect / leak_hann:.3f}x  (higher = more leakage)")
    print("--- time-domain RMS ---")
    print(f"RMS healthy           = {rms_healthy:.6f}")
    print(f"RMS with fault        = {rms_fault:.6f}")
    print(f"RMS relative change   = {rms_delta_pct:.3f} %")
    print(f"peak/floor @ BPFI band= {peak_to_floor:.2f}x")
    print(f"figure saved          = {OUT_PNG}")

    # --- figure ---
    fig, axes = plt.subplots(2, 2, figsize=(11.5, 7.2), constrained_layout=True)

    # time domain (first 0.12 s)
    n_show = int(0.12 * fs)
    ax = axes[0, 0]
    ax.plot(t[:n_show], with_fault[:n_show], color="#1f4e79", lw=0.8)
    ax.set_xlabel("time (s)")
    ax.set_ylabel("acceleration (a.u.)")
    ax.set_title("time domain (first 0.12 s) — fault hard to see by eye")
    ax.grid(True, alpha=0.3)

    # spectrum Hann, 0–200 Hz
    ax = axes[0, 1]
    m = freqs <= 200.0
    ax.plot(freqs[m], a_hann[m], color="#1f4e79", lw=1.0, label="Hann spectrum")
    ax.axvline(bpfi, color="#c0392b", ls="--", lw=1.2, label=f"true BPFI={bpfi:.2f} Hz")
    ax.axvline(shaft_hz, color="#27ae60", ls=":", lw=1.2, label=f"1X={shaft_hz:.0f} Hz")
    ax.axvline(2 * shaft_hz, color="#27ae60", ls=":", lw=0.8, alpha=0.7)
    ax.axvline(3 * shaft_hz, color="#27ae60", ls=":", lw=0.8, alpha=0.5)
    ax.plot([peak_bpfi_f], [peak_bpfi_a], "o", color="#c0392b", ms=6)
    ax.set_xlabel("frequency (Hz)")
    ax.set_ylabel("amplitude (a.u.)")
    ax.set_title("one-sided spectrum (Hann) — fault line pops out")
    ax.legend(fontsize=8, loc="upper right")
    ax.grid(True, alpha=0.3)

    # zoom around BPFI: rect vs Hann
    ax = axes[1, 0]
    m2 = (freqs >= bpfi - 15) & (freqs <= bpfi + 15)
    ax.plot(freqs[m2], a_rect[m2], color="#7f8c8d", lw=1.0, label="rectangular (leak)")
    ax.plot(freqs[m2], a_hann[m2], color="#1f4e79", lw=1.2, label="Hann")
    ax.axvline(bpfi, color="#c0392b", ls="--", lw=1.0)
    ax.set_xlabel("frequency (Hz)")
    ax.set_ylabel("amplitude (a.u.)")
    ax.set_title("zoom @ BPFI: spectral leakage without window")
    ax.legend(fontsize=8)
    ax.grid(True, alpha=0.3)

    # bar: RMS vs spectral peak visibility
    ax = axes[1, 1]
    labels = ["RMS\nhealthy", "RMS\n+fault", "amp @\nBPFI peak", "noise floor\n(median)"]
    values = [rms_healthy, rms_fault, peak_bpfi_a, noise_floor]
    colors = ["#95a5a6", "#95a5a6", "#c0392b", "#7f8c8d"]
    bars = ax.bar(labels, values, color=colors, width=0.65)
    ax.set_ylabel("value (a.u.)")
    ax.set_title(
        f"RMS change {rms_delta_pct:.2f}% vs peak/floor {peak_to_floor:.1f}×"
    )
    ax.grid(True, axis="y", alpha=0.3)
    for b, v in zip(bars, values):
        ax.text(
            b.get_x() + b.get_width() / 2,
            v,
            f"{v:.3f}",
            ha="center",
            va="bottom",
            fontsize=8,
        )

    fig.suptitle(
        f"FFT bearing fault demo — inject BPFI={bpfi:.2f} Hz @ {rpm:.0f} RPM, "
        f"fs={fs:.0f} Hz, N={n}, Δf={df:.3f} Hz",
        fontsize=11,
    )
    fig.savefig(OUT_PNG, dpi=140)
    plt.close(fig)


if __name__ == "__main__":
    main()
