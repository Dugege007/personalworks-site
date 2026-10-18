#!/usr/bin/env python3
"""Little's Law + M/M/1 demo for 2026-10-12 algorithm article.

Deterministic M/M/1 formulas for a production-line station, rho sweep plot,
and optional discrete-event simulation check of L ≈ λ̂·Ŵ (SEED=42).
"""

from __future__ import annotations

import math
from pathlib import Path

import matplotlib.pyplot as plt
import numpy as np

SEED = 42
OUT_DIR = Path(__file__).resolve().parent
FIG_PATH = OUT_DIR / "littles_law_mm1_demo.png"


def mm1_metrics(lam: float, mu: float) -> dict[str, float]:
    """Steady-state M/M/1 metrics. Requires rho < 1."""
    if mu <= 0:
        raise ValueError("mu must be positive")
    rho = lam / mu
    if rho >= 1:
        raise ValueError(f"unstable: rho={rho} >= 1")
    W = 1.0 / (mu - lam)
    L = lam * W  # = rho / (1 - rho)
    W_q = rho / (mu - lam)
    L_q = lam * W_q  # = rho**2 / (1 - rho)
    return {
        "lam": lam,
        "mu": mu,
        "rho": rho,
        "W": W,
        "L": L,
        "W_q": W_q,
        "L_q": L_q,
        "W_min": W * 60.0,
        "W_q_min": W_q * 60.0,
    }


def print_station_cases() -> None:
    print("=" * 60)
    print("M/M/1 production-station cases (deterministic)")
    print("=" * 60)

    base = mm1_metrics(lam=8.0, mu=10.0)
    print("\n[Case A] λ=8 /h, μ=10 /h")
    print(f"  ρ = λ/μ = {base['rho']:.10g}")
    print(f"  W = 1/(μ-λ) = {base['W']:.10g} h = {base['W_min']:.10g} min")
    print(f"  L = λ·W = {base['L']:.10g} pieces  (also ρ/(1-ρ) = {base['rho']/(1-base['rho']):.10g})")
    print(f"  W_q = ρ/(μ-λ) = {base['W_q']:.10g} h = {base['W_q_min']:.10g} min")
    print(f"  L_q = λ·W_q = {base['L_q']:.10g} pieces  (also ρ²/(1-ρ) = {base['rho']**2/(1-base['rho']):.10g})")

    boosted = mm1_metrics(lam=8.0, mu=12.0)
    print("\n[Case B] λ=8 /h, μ=12 /h  (capacity bump)")
    print(f"  ρ = {boosted['rho']:.10g} = {boosted['rho']:.10g} (2/3={2/3:.10g})")
    print(f"  W = {boosted['W']:.10g} h = {boosted['W_min']:.10g} min")
    print(f"  L = {boosted['L']:.10g} pieces")
    print(f"  W_q = {boosted['W_q']:.10g} h = {boosted['W_q_min']:.10g} min")
    print(f"  L_q = {boosted['L_q']:.10g} pieces")

    print("\n[Delta A→B]")
    print(f"  ρ: {base['rho']:.6g} → {boosted['rho']:.6g}")
    print(f"  W: {base['W_min']:.6g} min → {boosted['W_min']:.6g} min  ({100*(boosted['W']/base['W']-1):+.2f}%)")
    print(f"  L: {base['L']:.6g} → {boosted['L']:.6g}  ({100*(boosted['L']/base['L']-1):+.2f}%)")

    # Geometric distribution sanity: P(n)=(1-ρ)ρ^n for case A
    rho = base["rho"]
    print("\n[Case A] P(n)=(1-ρ)ρ^n for n=0..5")
    for n in range(6):
        pn = (1 - rho) * (rho**n)
        print(f"  P({n}) = {pn:.10g}")


def rho_sweep(mu: float = 10.0) -> list[dict[str, float]]:
    rhos = [0.5, 0.6, 0.7, 0.8, 0.9, 0.95]
    rows: list[dict[str, float]] = []
    print("\n" + "=" * 60)
    print(f"ρ sweep with fixed μ={mu} /h")
    print("=" * 60)
    print(f"{'ρ':>8} {'λ':>8} {'L':>12} {'W(h)':>12} {'W(min)':>10} {'L_q':>12}")
    for rho in rhos:
        lam = rho * mu
        m = mm1_metrics(lam, mu)
        rows.append(m)
        print(
            f"{m['rho']:8.2f} {m['lam']:8.2f} {m['L']:12.6f} "
            f"{m['W']:12.6f} {m['W_min']:10.4f} {m['L_q']:12.6f}"
        )
    return rows


def simulate_mm1(
    lam: float,
    mu: float,
    n_customers: int = 50000,
    seed: int = SEED,
    warmup: int = 5000,
) -> dict[str, float]:
    """FCFS single-server discrete-event simulation with exponential interarrival/service.

    Estimates time-average L and customer-average W over the post-warmup window,
    then checks Little: L ≈ λ_eff · W.
    """
    rng = np.random.default_rng(seed)
    interarrivals = rng.exponential(1.0 / lam, size=n_customers)
    services = rng.exponential(1.0 / mu, size=n_customers)
    arrival_times = np.cumsum(interarrivals)

    departure = 0.0
    sojourn: list[float] = []
    # For time-average L: track busy intervals via departure / arrival events
    # Use area under queue length via departure-time bookkeeping.
    # Simpler: Lindley's recursion for waiting + service; area via start/end.

    start_service = np.empty(n_customers)
    end_service = np.empty(n_customers)
    for i in range(n_customers):
        start_service[i] = max(arrival_times[i], departure)
        end_service[i] = start_service[i] + services[i]
        departure = end_service[i]
        sojourn.append(end_service[i] - arrival_times[i])

    sojourn_arr = np.asarray(sojourn)

    # Observation window: after warmup departures, until last departure of kept customers
    # Use customers [warmup, n_customers)
    idx0 = warmup
    t0 = arrival_times[idx0]  # start observing when warmup-th customer arrives
    # Better: start at departure of warmup-1 so system has run
    t0 = end_service[idx0 - 1]
    t1 = end_service[-1]
    window = t1 - t0
    if window <= 0:
        raise RuntimeError("empty observation window")

    # Time-average number in system over [t0, t1]:
    # For each customer i, contribution is length of [max(arr,t0), min(dep,t1)] ∩ sojourn
    area = 0.0
    arrivals_in_window = 0
    completed_sojourns: list[float] = []
    for i in range(n_customers):
        a, d = arrival_times[i], end_service[i]
        overlap_start = max(a, t0)
        overlap_end = min(d, t1)
        if overlap_end > overlap_start:
            area += overlap_end - overlap_start
        # Effective arrivals that enter during (t0, t1]
        if t0 < a <= t1:
            arrivals_in_window += 1
        # Sojourn of customers who both arrive and depart in window (or arrive after t0 and finish by t1)
        if a >= t0 and d <= t1:
            completed_sojourns.append(d - a)

    L_hat = area / window
    lam_hat = arrivals_in_window / window
    W_hat = float(np.mean(completed_sojourns)) if completed_sojourns else float("nan")
    L_from_little = lam_hat * W_hat
    rel_err = abs(L_hat - L_from_little) / L_hat if L_hat > 0 else float("nan")

    # Also customer-average W of post-warmup customers (all who arrived after warmup index)
    W_cust = float(np.mean(sojourn_arr[idx0:]))
    # Theoretical
    theory = mm1_metrics(lam, mu)

    return {
        "n_customers": float(n_customers),
        "warmup": float(warmup),
        "t0": float(t0),
        "t1": float(t1),
        "window_h": float(window),
        "L_hat": float(L_hat),
        "lam_hat": float(lam_hat),
        "W_hat": float(W_hat),
        "L_from_little": float(L_from_little),
        "rel_err": float(rel_err),
        "W_cust": W_cust,
        "n_completed_in_window": float(len(completed_sojourns)),
        "L_theory": theory["L"],
        "W_theory": theory["W"],
        "lam": lam,
        "mu": mu,
        "rho": theory["rho"],
    }


def print_simulation() -> dict[str, float]:
    print("\n" + "=" * 60)
    print("Discrete-event M/M/1 check of Little's Law (SEED=42)")
    print("=" * 60)
    sim = simulate_mm1(lam=8.0, mu=10.0, n_customers=50000, seed=SEED, warmup=5000)
    print(f"  λ={sim['lam']}, μ={sim['mu']}, ρ={sim['rho']}")
    print(f"  n_customers={int(sim['n_customers'])}, warmup={int(sim['warmup'])}")
    print(f"  observation window = {sim['window_h']:.6f} h")
    print(f"  L_hat (time avg)     = {sim['L_hat']:.6f}")
    print(f"  λ_hat (arrivals/win) = {sim['lam_hat']:.6f}")
    print(f"  W_hat (completed)    = {sim['W_hat']:.6f} h  (n={int(sim['n_completed_in_window'])})")
    print(f"  λ_hat · W_hat        = {sim['L_from_little']:.6f}")
    print(f"  |L_hat - λ̂Ŵ|/L_hat  = {sim['rel_err']:.6%} relative")
    print(f"  theory L={sim['L_theory']:.6f}, W={sim['W_theory']:.6f}")
    print(f"  post-warmup mean sojourn W_cust = {sim['W_cust']:.6f} h")
    return sim


def make_figure(rows: list[dict[str, float]], case_a: dict, case_b: dict) -> None:
    fig, axes = plt.subplots(1, 2, figsize=(11.5, 4.6))

    # Left: rho sweep L and W
    ax = axes[0]
    rhos = [r["rho"] for r in rows]
    Ls = [r["L"] for r in rows]
    Ws_min = [r["W_min"] for r in rows]

    color_l = "#1f77b4"
    color_w = "#d62728"
    ax.plot(rhos, Ls, "o-", color=color_l, lw=2, markersize=7, label=r"$L=\rho/(1-\rho)$")
    ax.set_xlabel(r"utilization $\rho=\lambda/\mu$ (fixed $\mu=10$/h)", fontsize=11)
    ax.set_ylabel(r"mean pieces in system $L$", color=color_l, fontsize=11)
    ax.tick_params(axis="y", labelcolor=color_l)
    ax.grid(True, alpha=0.35)
    ax.set_xlim(0.45, 1.0)

    ax2 = ax.twinx()
    ax2.plot(rhos, Ws_min, "s--", color=color_w, lw=2, markersize=6, label=r"$W$ (min)")
    ax2.set_ylabel(r"mean sojourn $W$ (minutes)", color=color_w, fontsize=11)
    ax2.tick_params(axis="y", labelcolor=color_w)

    # annotate rho=0.8
    ax.axvline(0.8, color="gray", ls=":", lw=1.2, alpha=0.8)
    ax.annotate(
        r"$\rho=0.8$: $L=4$, $W=30$ min",
        xy=(0.8, 4.0),
        xytext=(0.55, 12),
        fontsize=9,
        arrowprops=dict(arrowstyle="->", color="gray"),
        color="0.25",
    )

    lines1, labels1 = ax.get_legend_handles_labels()
    lines2, labels2 = ax2.get_legend_handles_labels()
    ax.legend(lines1 + lines2, labels1 + labels2, loc="upper left", fontsize=9)
    ax.set_title(r"$L(\rho)$ and $W(\rho)$ blow up as $\rho\to 1$", fontsize=12)

    # Right: bar comparison case A vs B
    axb = axes[1]
    labels = ["ρ", "L (pieces)", "W (min)", "W_q (min)", "L_q (pieces)"]
    vals_a = [
        case_a["rho"],
        case_a["L"],
        case_a["W_min"],
        case_a["W_q_min"],
        case_a["L_q"],
    ]
    vals_b = [
        case_b["rho"],
        case_b["L"],
        case_b["W_min"],
        case_b["W_q_min"],
        case_b["L_q"],
    ]
    x = np.arange(len(labels))
    width = 0.36
    bars_a = axb.bar(x - width / 2, vals_a, width, label=r"A: μ=10 (ρ=0.8)", color="#4c78a8")
    bars_b = axb.bar(x + width / 2, vals_b, width, label=r"B: μ=12 (ρ=2/3)", color="#f58518")
    axb.set_xticks(x)
    axb.set_xticklabels(labels, fontsize=9)
    axb.set_ylabel("value")
    axb.set_title(r"Same λ=8/h: bump μ 10→12 cuts congestion nonlinearly", fontsize=11)
    axb.legend(fontsize=9)
    axb.grid(True, axis="y", alpha=0.35)

    for bars in (bars_a, bars_b):
        for bar in bars:
            h = bar.get_height()
            axb.annotate(
                f"{h:.2f}" if h < 10 else f"{h:.1f}",
                xy=(bar.get_x() + bar.get_width() / 2, h),
                xytext=(0, 3),
                textcoords="offset points",
                ha="center",
                va="bottom",
                fontsize=8,
            )

    fig.suptitle(
        "Little's Law & M/M/1 — production station congestion (SEED=42 for sim)",
        fontsize=13,
        y=1.02,
    )
    fig.tight_layout()
    fig.savefig(FIG_PATH, dpi=140, bbox_inches="tight")
    plt.close(fig)
    print(f"\nFigure saved: {FIG_PATH}")


def main() -> None:
    print(f"SEED={SEED} (used only by discrete-event simulation)")
    print_station_cases()
    rows = rho_sweep(mu=10.0)
    case_a = mm1_metrics(8.0, 10.0)
    case_b = mm1_metrics(8.0, 12.0)
    make_figure(rows, case_a, case_b)
    print_simulation()
    print("\nDone.")


if __name__ == "__main__":
    main()
