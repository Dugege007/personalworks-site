#!/usr/bin/env python3
"""
Platt scaling demo (SEED=42).

Constructs overconfident / optimistic binary scores, fits
P(y=1|f) = 1/(1+exp(A*f+B)) (Platt/sklearn sign) by MLE with Platt's
smoothed targets, then compares reliability (bucket frequency vs
mean prediction) and ECE before vs after calibration.

References:
  Platt 1999, Advances in Large Margin Classifiers
  Niculescu-Mizil & Caruana, ICML 2005
  Lin, Lin & Weng, Machine Learning 2007 (numerically safer Newton)
"""

from __future__ import annotations

import os

import matplotlib.pyplot as plt
import numpy as np
from matplotlib import font_manager
from scipy.optimize import minimize

# Prefer CJK font so reliability-diagram labels render.
for _fname in ("Noto Sans CJK SC", "Noto Serif CJK SC", "Noto Sans CJK JP"):
    try:
        font_manager.findfont(_fname, fallback_to_default=False)
        plt.rcParams["font.sans-serif"] = [_fname, "DejaVu Sans"]
        plt.rcParams["axes.unicode_minus"] = False
        break
    except Exception:
        continue

SEED = 42
N_CAL = 800
N_TEST = 2000
N_BINS = 10
OUT_FIG = os.path.join(
    os.path.dirname(os.path.abspath(__file__)),
    "..",
    "platt_scaling_reliability.png",
)


def logit(p: np.ndarray, eps: float = 1e-6) -> np.ndarray:
    p = np.clip(p, eps, 1.0 - eps)
    return np.log(p) - np.log(1.0 - p)


def sigmoid(z: np.ndarray) -> np.ndarray:
    # numerically stable
    z = np.asarray(z, dtype=float)
    out = np.empty_like(z)
    pos = z >= 0
    out[pos] = 1.0 / (1.0 + np.exp(-z[pos]))
    ez = np.exp(z[~pos])
    out[~pos] = ez / (1.0 + ez)
    return out


def platt_targets(y: np.ndarray) -> np.ndarray:
    """Platt 1999 smoothed labels: avoid hard 0/1."""
    y = np.asarray(y, dtype=float)
    n_pos = float(np.sum(y == 1))
    n_neg = float(np.sum(y == 0))
    t_pos = (n_pos + 1.0) / (n_pos + 2.0)
    t_neg = 1.0 / (n_neg + 2.0)
    t = np.where(y == 1, t_pos, t_neg)
    return t, t_pos, t_neg


def fit_platt(f: np.ndarray, y: np.ndarray) -> tuple[float, float]:
    """
    Minimize cross-entropy of sigmoid(A*f+B) vs smoothed targets.
    Returns (A, B).
    """
    f = np.asarray(f, dtype=float)
    y = np.asarray(y, dtype=float)
    t, _, _ = platt_targets(y)

    def nll(params: np.ndarray) -> float:
        # Platt 1999 / sklearn: P = 1/(1+exp(A f + B)) = sigmoid(-(A f + B))
        a, b = params
        p = sigmoid(-(a * f + b))
        p = np.clip(p, 1e-12, 1.0 - 1e-12)
        return float(-np.sum(t * np.log(p) + (1.0 - t) * np.log(1.0 - p)))

    def grad(params: np.ndarray) -> np.ndarray:
        a, b = params
        p = sigmoid(-(a * f + b))
        # dP/d(Af+B) = -p(1-p); dNLL/d(Af+B) = (p-t)*(-1) wait:
        # NLL = -t log p -(1-t)log(1-p); dNLL/dp = (p-t)/(p(1-p));
        # dp/dz = -p(1-p) for z=Af+B; so dNLL/dz = t-p.
        # Thus dNLL/dA = sum (t-p)*f, dNLL/dB = sum (t-p).
        diff = t - p
        ga = float(np.sum(diff * f))
        gb = float(np.sum(diff))
        return np.array([ga, gb])

    # Platt / Lin init: A=0, B=log((N-)/(N+)) so prior odds are roughly matched
    n_pos = max(float(np.sum(y == 1)), 1.0)
    n_neg = max(float(np.sum(y == 0)), 1.0)
    x0 = np.array([0.0, np.log(n_neg / n_pos)])

    res = minimize(
        nll,
        x0,
        jac=grad,
        method="L-BFGS-B",
        options={"maxiter": 200, "ftol": 1e-12},
    )
    if not res.success:
        raise RuntimeError(f"Platt fit failed: {res.message}")
    a, b = float(res.x[0]), float(res.x[1])
    return a, b


def apply_platt(f: np.ndarray, a: float, b: float) -> np.ndarray:
    # Platt / sklearn canonical form
    return sigmoid(-(a * f + b))


def reliability_table(
    probs: np.ndarray, y: np.ndarray, n_bins: int = N_BINS
) -> list[dict]:
    """Equal-width bins on [0,1]; return per-bin stats."""
    probs = np.asarray(probs, dtype=float)
    y = np.asarray(y, dtype=float)
    edges = np.linspace(0.0, 1.0, n_bins + 1)
    rows = []
    for i in range(n_bins):
        lo, hi = edges[i], edges[i + 1]
        if i == n_bins - 1:
            mask = (probs >= lo) & (probs <= hi)
        else:
            mask = (probs >= lo) & (probs < hi)
        n = int(mask.sum())
        if n == 0:
            rows.append(
                {
                    "bin": i,
                    "lo": lo,
                    "hi": hi,
                    "n": 0,
                    "mean_pred": float("nan"),
                    "emp_freq": float("nan"),
                    "gap": float("nan"),
                }
            )
            continue
        mean_pred = float(probs[mask].mean())
        emp_freq = float(y[mask].mean())
        rows.append(
            {
                "bin": i,
                "lo": lo,
                "hi": hi,
                "n": n,
                "mean_pred": mean_pred,
                "emp_freq": emp_freq,
                "gap": abs(mean_pred - emp_freq),
            }
        )
    return rows


def ece(probs: np.ndarray, y: np.ndarray, n_bins: int = N_BINS) -> float:
    rows = reliability_table(probs, y, n_bins)
    n = len(probs)
    total = 0.0
    for r in rows:
        if r["n"] == 0:
            continue
        total += (r["n"] / n) * r["gap"]
    return float(total)


def brier(probs: np.ndarray, y: np.ndarray) -> float:
    return float(np.mean((probs - y) ** 2))


def make_optimistic_dataset(
    n: int, rng: np.random.Generator
) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    """
    True risk p ~ Beta(2, 2) (spread around 0.5).
    Labels ~ Bernoulli(p).
    Uncalibrated score: steeper + positively biased logit
      f = 1.75 * logit(p) + 0.55
    then raw_prob = sigmoid(f) — overconfident and optimistic
    (predicted win/fault probs systematically too high in the mid range).
    """
    p_true = rng.beta(2.0, 2.0, size=n)
    y = rng.binomial(1, p_true).astype(float)
    f = 1.75 * logit(p_true) + 0.55
    raw_prob = sigmoid(f)
    return f, raw_prob, y


def print_table(title: str, rows: list[dict]) -> None:
    print(title)
    print(
        f"{'bin':>3} {'range':>13} {'n':>5} {'mean_pred':>10} "
        f"{'emp_freq':>10} {'|gap|':>8}"
    )
    for r in rows:
        if r["n"] == 0:
            print(
                f"{r['bin']:3d} [{r['lo']:.1f},{r['hi']:.1f}) "
                f"{0:5d} {'—':>10} {'—':>10} {'—':>8}"
            )
            continue
        print(
            f"{r['bin']:3d} [{r['lo']:.1f},{r['hi']:.1f}) "
            f"{r['n']:5d} {r['mean_pred']:10.4f} "
            f"{r['emp_freq']:10.4f} {r['gap']:8.4f}"
        )


def plot_reliability(
    raw: np.ndarray,
    cal: np.ndarray,
    y: np.ndarray,
    a: float,
    b: float,
    path: str,
) -> None:
    raw_rows = reliability_table(raw, y)
    cal_rows = reliability_table(cal, y)

    def xy(rows):
        xs, ys = [], []
        for r in rows:
            if r["n"] == 0:
                continue
            xs.append(r["mean_pred"])
            ys.append(r["emp_freq"])
        return np.array(xs), np.array(ys)

    fig, axes = plt.subplots(1, 2, figsize=(10.5, 4.8), dpi=120)

    for ax, probs, rows, name, ece_v, brier_v in [
        (axes[0], raw, raw_rows, "标定前（乐观分数）", ece(raw, y), brier(raw, y)),
        (axes[1], cal, cal_rows, "标定后（Platt）", ece(cal, y), brier(cal, y)),
    ]:
        xs, ys = xy(rows)
        ax.plot([0, 1], [0, 1], "k--", lw=1.2, label="理想对角线")
        ax.plot(xs, ys, "o-", color="#2563eb", lw=1.6, markersize=7, label="桶经验频率")
        ax.set_xlim(0, 1)
        ax.set_ylim(0, 1)
        ax.set_aspect("equal")
        ax.set_xlabel("桶内预测概率均值")
        ax.set_ylabel("桶内经验正类频率")
        ax.set_title(f"{name}\nECE={ece_v:.4f}  Brier={brier_v:.4f}")
        ax.grid(True, alpha=0.35)
        ax.legend(loc="upper left", fontsize=9)

    fig.suptitle(
        f"可靠性图（reliability diagram）  SEED={SEED}  "
        f"A={a:.4f}, B={b:.4f}",
        fontsize=12,
    )
    fig.tight_layout()
    abs_path = os.path.abspath(path)
    os.makedirs(os.path.dirname(abs_path), exist_ok=True)
    fig.savefig(abs_path, bbox_inches="tight")
    plt.close(fig)
    print(f"\nWrote figure: {abs_path}")


def main() -> None:
    rng = np.random.default_rng(SEED)
    f_all, raw_all, y_all = make_optimistic_dataset(N_CAL + N_TEST, rng)
    f_cal, y_cal = f_all[:N_CAL], y_all[:N_CAL]
    raw_cal = raw_all[:N_CAL]
    f_te, y_te = f_all[N_CAL:], y_all[N_CAL:]
    raw_te = raw_all[N_CAL:]

    t, t_pos, t_neg = platt_targets(y_cal)
    a, b = fit_platt(f_cal, y_cal)
    cal_te = apply_platt(f_te, a, b)

    print(f"SEED={SEED}")
    print(f"cal n={N_CAL}, test n={N_TEST}, bins={N_BINS}")
    print(f"cal positives={int(y_cal.sum())}, negatives={int((1-y_cal).sum())}")
    print(f"smoothed targets: t+={t_pos:.6f}, t-={t_neg:.6f}")
    print(f"fitted A={a:.6f}, B={b:.6f}")
    print(
        f"raw mean on test={raw_te.mean():.4f}, "
        f"cal mean={cal_te.mean():.4f}, "
        f"label rate={y_te.mean():.4f}"
    )
    print()
    print_table("=== TEST 标定前 ===", reliability_table(raw_te, y_te))
    print()
    print_table("=== TEST 标定后 ===", reliability_table(cal_te, y_te))
    print()
    print(f"ECE  before={ece(raw_te, y_te):.6f}  after={ece(cal_te, y_te):.6f}")
    print(f"Brier before={brier(raw_te, y_te):.6f}  after={brier(cal_te, y_te):.6f}")

    # tiny hand-check: one score through the sigmoid
    f_demo = 0.0
    p_demo = apply_platt(np.array([f_demo]), a, b)[0]
    print(
        f"\nHand check: f=0 -> P=1/(1+exp(B)) = {p_demo:.6f}  "
        f"(B={b:.6f}; A={a:.6f})"
    )

    plot_reliability(raw_te, cal_te, y_te, a, b, OUT_FIG)


if __name__ == "__main__":
    main()
