#!/usr/bin/env python3
"""
Tabular two-sided CUSUM for small persistent mean shifts.

1) Reproduces NIST Engineering Statistics Handbook 6.3.2.3 example.
2) Spindle-temperature simulation (SEED=42): +0.75σ mean step vs Shewhart 3σ.

References:
  Page (1954) Biometrika 41(1/2):100–115
  NIST handbook 6.3.2.3
  Montgomery rule of thumb (via NIST): k ≈ δ/2, h ≈ 4 or 5
"""

from __future__ import annotations

import os
import random
from dataclasses import dataclass, field
from typing import List, Optional, Sequence, Tuple

# ---------------------------------------------------------------------------
# Core CUSUM
# ---------------------------------------------------------------------------


@dataclass
class CusumResult:
    shi: List[float]
    slo: List[float]
    alarms: List[Tuple[int, str, float]] = field(default_factory=list)
    # each alarm: (0-based index, "hi"|"lo", statistic value at alarm)

    @property
    def first_alarm_index(self) -> Optional[int]:
        return self.alarms[0][0] if self.alarms else None


def tabular_cusum(
    xs: Sequence[float],
    mu0: float,
    k: float,
    h: float,
    reset_on_alarm: bool = False,
) -> CusumResult:
    """NIST tabular two-sided CUSUM (original data units).

    S_hi(i) = max(0, S_hi(i-1) + x_i - mu0 - k)
    S_lo(i) = max(0, S_lo(i-1) + mu0 - k - x_i)
    Alarm when either exceeds h. Optional reset to 0 after each alarm
    (common industrial practice so monitoring can continue).
    """
    shi: List[float] = []
    slo: List[float] = []
    alarms: List[Tuple[int, str, float]] = []
    s_hi = 0.0
    s_lo = 0.0

    for i, x in enumerate(xs):
        s_hi = max(0.0, s_hi + x - mu0 - k)
        s_lo = max(0.0, s_lo + mu0 - k - x)
        shi.append(s_hi)
        slo.append(s_lo)
        if s_hi > h:
            alarms.append((i, "hi", s_hi))
            if reset_on_alarm:
                s_hi = 0.0
                s_lo = 0.0
        elif s_lo > h:
            alarms.append((i, "lo", s_lo))
            if reset_on_alarm:
                s_hi = 0.0
                s_lo = 0.0

    return CusumResult(shi=shi, slo=slo, alarms=alarms)


def standardized_cusum(
    xs: Sequence[float],
    mu0: float,
    sigma: float,
    k_star: float,
    h_star: float,
    reset_on_alarm: bool = False,
) -> CusumResult:
    """z=(x-mu0)/sigma, then tabular CUSUM on z with parameters k*, h*."""
    if sigma <= 0:
        raise ValueError("sigma must be positive")
    zs = [(x - mu0) / sigma for x in xs]
    return tabular_cusum(zs, mu0=0.0, k=k_star, h=h_star, reset_on_alarm=reset_on_alarm)


def shewhart_alarms(
    xs: Sequence[float],
    mu0: float,
    sigma: float,
    L: float = 3.0,
) -> List[int]:
    """0-based indices where |x - mu0| > L * sigma."""
    return [i for i, x in enumerate(xs) if abs(x - mu0) > L * sigma]


# ---------------------------------------------------------------------------
# NIST textbook example
# ---------------------------------------------------------------------------

NIST_XBAR = [
    324.925,
    324.675,
    324.725,
    324.350,
    325.350,
    325.225,
    324.125,
    324.525,
    325.225,
    324.600,
    324.625,
    325.150,
    328.325,
    327.250,
    327.825,
    328.500,
    326.675,
    327.775,
    326.875,
    328.350,
]


def run_nist_example() -> CusumResult:
    """Reproduce NIST 6.3.2.3: mu0=325, k=0.3175, h=4.1959; first alarm at 14."""
    mu0 = 325.0
    k = 0.3175
    h = 4.1959
    result = tabular_cusum(NIST_XBAR, mu0=mu0, k=k, h=h, reset_on_alarm=False)

    assert result.first_alarm_index is not None, "NIST example should alarm"
    assert result.first_alarm_index + 1 == 14, (
        f"expected first alarm at sample 14 (1-based), got {result.first_alarm_index + 1}"
    )
    assert result.alarms[0][1] == "hi"
    assert abs(result.alarms[0][2] - 4.94) < 0.02, (
        f"expected S_hi≈4.94 at alarm, got {result.alarms[0][2]}"
    )

    print("=== NIST tabular CUSUM example (Handbook 6.3.2.3) ===")
    print(f"mu0={mu0}, k={k}, h={h}")
    print(
        f"first alarm: sample {result.first_alarm_index + 1} (1-based), "
        f"side={result.alarms[0][1]}, S_hi={result.alarms[0][2]:.4f} (NIST table ≈ 4.94)"
    )
    print(f"S_hi at sample 13 = {result.shi[12]:.4f}; at sample 14 = {result.shi[13]:.4f}")
    return result


# ---------------------------------------------------------------------------
# Production-line simulation
# ---------------------------------------------------------------------------


def simulate_spindle(
    n: int,
    mu0: float,
    sigma: float,
    shift_at: int,
    shift_delta_sigma: float,
    seed: int,
) -> List[float]:
    rng = random.Random(seed)
    out: List[float] = []
    for t in range(n):
        mean = mu0 if t < shift_at else mu0 + shift_delta_sigma * sigma
        out.append(rng.gauss(mean, sigma))
    return out


def estimate_arl0(
    mu0: float,
    sigma: float,
    k_star: float,
    h_star: float,
    n_runs: int,
    max_len: int,
    seed0: int,
) -> Tuple[float, float, float]:
    """Monte Carlo ARL0 for standardized two-sided CUSUM (no reset; time to first)."""
    lengths: List[int] = []
    for r in range(n_runs):
        xs = simulate_spindle(max_len, mu0, sigma, shift_at=max_len + 1, shift_delta_sigma=0.0, seed=seed0 + r)
        res = standardized_cusum(xs, mu0, sigma, k_star, h_star, reset_on_alarm=False)
        if res.first_alarm_index is None:
            lengths.append(max_len)
        else:
            lengths.append(res.first_alarm_index + 1)
    lengths_sorted = sorted(lengths)
    mean_arl = sum(lengths) / len(lengths)
    median_arl = lengths_sorted[len(lengths) // 2]
    censored = sum(1 for L in lengths if L >= max_len) / len(lengths)
    return mean_arl, median_arl, censored


def run_spindle_demo() -> dict:
    seed = 42
    mu0 = 55.0  # °C
    sigma = 0.40  # °C
    n = 1800
    shift_at = 900
    delta = 0.75  # small persistent mean step in σ units
    k_star = 0.5  # Montgomery / NIST rule of thumb (≈ δ/2 for δ≈1)
    h_star = 5.0

    xs = simulate_spindle(n, mu0, sigma, shift_at, delta, seed)
    # Reset after each alarm so pre-shift false alarms do not freeze monitoring.
    cusum = standardized_cusum(xs, mu0, sigma, k_star, h_star, reset_on_alarm=True)
    shew = shewhart_alarms(xs, mu0, sigma, L=3.0)

    pre = [a for a in cusum.alarms if a[0] < shift_at]
    post = [a for a in cusum.alarms if a[0] >= shift_at]
    shew_pre = [i for i in shew if i < shift_at]
    shew_post = [i for i in shew if i >= shift_at]

    cusum_detect = post[0][0] if post else None
    cusum_delay = (cusum_detect - shift_at) if cusum_detect is not None else None
    shew_detect = shew_post[0] if shew_post else None
    shew_delay = (shew_detect - shift_at) if shew_detect is not None else None

    # Pure in-control series (same length) with resets: false-alarm count
    xs_ic = simulate_spindle(n, mu0, sigma, shift_at=n + 1, shift_delta_sigma=0.0, seed=seed + 1)
    cusum_ic = standardized_cusum(xs_ic, mu0, sigma, k_star, h_star, reset_on_alarm=True)
    shew_ic = shewhart_alarms(xs_ic, mu0, sigma, L=3.0)

    mean_arl, median_arl, censored = estimate_arl0(
        mu0, sigma, k_star, h_star, n_runs=200, max_len=5000, seed0=1000
    )

    # Secondary: +1.0σ same seed
    xs_1 = simulate_spindle(n, mu0, sigma, shift_at, 1.0, seed)
    cusum_1 = standardized_cusum(xs_1, mu0, sigma, k_star, h_star, reset_on_alarm=True)
    shew_1 = shewhart_alarms(xs_1, mu0, sigma, L=3.0)
    post_1 = [a for a in cusum_1.alarms if a[0] >= shift_at]
    shew_1_post = [i for i in shew_1 if i >= shift_at]

    print()
    print("=== Spindle temperature mean-shift demo (SEED=42) ===")
    print(f"mu0={mu0} °C, sigma={sigma} °C, n={n}, shift_at={shift_at}s, delta={delta}σ")
    print(f"after-shift mean = {mu0 + delta * sigma:.3f} °C")
    print(f"standardized CUSUM: k*={k_star}, h*={h_star}, reset_on_alarm=True")
    print(f"CUSUM false alarms before shift: {len(pre)} at t={[a[0] for a in pre]}")
    if cusum_detect is not None:
        print(
            f"CUSUM first alarm after shift: t={cusum_detect}s, "
            f"side={post[0][1]}, value={post[0][2]:.4f}, delay={cusum_delay}s"
        )
    else:
        print("CUSUM: no alarm after shift")
    print(f"Shewhart 3σ before shift: {len(shew_pre)}; after shift total points: {len(shew_post)}")
    if shew_detect is not None:
        print(f"Shewhart first alarm after shift: t={shew_detect}s, delay={shew_delay}s")
    else:
        print("Shewhart: no alarm after shift")
    print(
        f"In-control series n={n} seed={seed + 1}: "
        f"CUSUM alarms={len(cusum_ic.alarms)}, Shewhart={len(shew_ic)}"
    )
    print(
        f"ARL0 Monte Carlo (200 runs, max_len=5000, k*=0.5, h*=5): "
        f"mean≈{mean_arl:.1f}, median={median_arl:.0f}, censored_frac={censored:.3f}"
    )

    print()
    print("--- same SEED, shift = +1.0σ (secondary) ---")
    if post_1:
        print(
            f"CUSUM first after shift: t={post_1[0][0]}s, delay={post_1[0][0] - shift_at}s"
        )
    else:
        print("CUSUM: no alarm after shift")
    if shew_1_post:
        print(
            f"Shewhart first after shift: t={shew_1_post[0]}s, "
            f"delay={shew_1_post[0] - shift_at}s"
        )
    else:
        print("Shewhart: no alarm after shift")

    return {
        "xs": xs,
        "cusum": cusum,
        "shew": shew,
        "mu0": mu0,
        "sigma": sigma,
        "n": n,
        "shift_at": shift_at,
        "delta": delta,
        "k_star": k_star,
        "h_star": h_star,
        "pre": pre,
        "cusum_detect": cusum_detect,
        "cusum_delay": cusum_delay,
        "shew_detect": shew_detect,
        "shew_delay": shew_delay,
        "shew_pre_count": len(shew_pre),
        "shew_post_count": len(shew_post),
        "ic_cusum_count": len(cusum_ic.alarms),
        "ic_shew_count": len(shew_ic),
        "arl0_mean": mean_arl,
        "arl0_median": median_arl,
        "post_1_delay": (post_1[0][0] - shift_at) if post_1 else None,
        "shew_1_delay": (shew_1_post[0] - shift_at) if shew_1_post else None,
        "post_alarm_value": post[0][2] if post else None,
        "post_side": post[0][1] if post else None,
    }


def make_plot(demo: dict, out_path: str) -> None:
    import matplotlib

    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    xs = demo["xs"]
    cusum = demo["cusum"]
    shew = demo["shew"]
    mu0 = demo["mu0"]
    sigma = demo["sigma"]
    shift_at = demo["shift_at"]
    h_star = demo["h_star"]
    n = demo["n"]
    t = list(range(n))

    fig, axes = plt.subplots(
        2, 1, figsize=(11, 7.2), sharex=True, gridspec_kw={"height_ratios": [1.15, 1]}
    )

    ax0 = axes[0]
    ax0.plot(t, xs, color="#4a6fa5", linewidth=0.65, alpha=0.85, label="temperature (°C)")
    ax0.axhline(mu0, color="#333333", linestyle="--", linewidth=1.0, label=f"μ₀={mu0}")
    ax0.axhline(mu0 + 3 * sigma, color="#c0392b", linestyle=":", linewidth=1.0, label="Shewhart ±3σ")
    ax0.axhline(mu0 - 3 * sigma, color="#c0392b", linestyle=":", linewidth=1.0)
    ax0.axvline(shift_at, color="#e67e22", linestyle="-.", linewidth=1.2, label=f"mean step t={shift_at}s")
    if demo["cusum_detect"] is not None:
        ax0.axvline(
            demo["cusum_detect"],
            color="#27ae60",
            linestyle="-.",
            linewidth=1.2,
            label=f"CUSUM after-step alarm t={demo['cusum_detect']}s",
        )
    for i in shew:
        ax0.plot(i, xs[i], "x", color="#c0392b", markersize=5)
    ax0.set_ylabel("temperature (°C)")
    ax0.set_title(
        f"Spindle temp: +{demo['delta']}σ mean step at t={shift_at}s  "
        f"(SEED=42, σ={sigma}, k*={demo['k_star']}, h*={h_star}, reset after alarm)"
    )
    ax0.legend(loc="upper left", fontsize=8, ncol=2)
    ax0.grid(True, alpha=0.3)

    ax1 = axes[1]
    ax1.plot(t, cusum.shi, color="#2980b9", linewidth=1.0, label="S_hi (standardized)")
    ax1.plot(t, cusum.slo, color="#8e44ad", linewidth=1.0, label="S_lo (standardized)")
    ax1.axhline(h_star, color="#c0392b", linestyle="--", linewidth=1.0, label=f"h*={h_star}")
    ax1.axvline(shift_at, color="#e67e22", linestyle="-.", linewidth=1.2)
    if demo["cusum_detect"] is not None:
        ax1.axvline(demo["cusum_detect"], color="#27ae60", linestyle="-.", linewidth=1.2)
        ax1.plot(
            demo["cusum_detect"],
            cusum.shi[demo["cusum_detect"]],
            "o",
            color="#27ae60",
            markersize=6,
        )
    # mark pre-shift false alarms lightly
    for a in demo["pre"]:
        ax1.axvline(a[0], color="#95a5a6", linestyle=":", linewidth=0.8, alpha=0.7)
    ax1.set_xlabel("time (s)")
    ax1.set_ylabel("CUSUM statistic")
    ax1.legend(loc="upper left", fontsize=8)
    ax1.grid(True, alpha=0.3)
    ax1.set_xlim(0, n - 1)

    fig.tight_layout()
    fig.savefig(out_path, dpi=140)
    plt.close(fig)
    print()
    print(f"figure saved: {out_path}")


def main() -> None:
    nist = run_nist_example()
    demo = run_spindle_demo()
    out_dir = os.path.dirname(os.path.abspath(__file__))
    out_path = os.path.join(out_dir, "cusum_demo.png")
    make_plot(demo, out_path)

    print()
    print("=== NIST S_hi / S_lo (selected rows for article) ===")
    for i in [0, 4, 11, 12, 13, 14, 19]:
        print(
            f"i={i + 1:2d}  x={NIST_XBAR[i]:8.3f}  "
            f"S_hi={nist.shi[i]:7.4f}  S_lo={nist.slo[i]:7.4f}"
        )

    # Expose key numbers as a final machine-readable block
    print()
    print("=== KEY NUMBERS ===")
    print(f"nist_first_alarm_1based=14")
    print(f"nist_S_hi_at_14={nist.shi[13]:.4f}")
    print(f"spindle_delta_sigma={demo['delta']}")
    print(f"spindle_cusum_delay_s={demo['cusum_delay']}")
    print(f"spindle_cusum_detect_t={demo['cusum_detect']}")
    print(f"spindle_shewhart_delay_s={demo['shew_delay']}")
    print(f"spindle_pre_cusum_fp={len(demo['pre'])}")
    print(f"spindle_shew_pre={demo['shew_pre_count']}")
    print(f"ic_cusum_alarms={demo['ic_cusum_count']}")
    print(f"ic_shewhart={demo['ic_shew_count']}")
    print(f"arl0_mean={demo['arl0_mean']:.1f}")
    print(f"arl0_median={demo['arl0_median']:.0f}")
    print(f"secondary_1sigma_cusum_delay={demo['post_1_delay']}")
    print(f"secondary_1sigma_shewhart_delay={demo['shew_1_delay']}")


if __name__ == "__main__":
    main()
