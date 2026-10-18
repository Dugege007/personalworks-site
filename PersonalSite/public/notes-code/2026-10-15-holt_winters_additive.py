#!/usr/bin/env python3
"""Additive Holt-Winters (triple exponential smoothing) demo.

Formulas follow Hyndman & Athanasopoulos, Forecasting: Principles and Practice
(3rd ed) §8.3 additive method. SEED=42, non-interactive.
"""

from __future__ import annotations

import math
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib import font_manager
import numpy as np

# Prefer Noto Sans CJK SC for Chinese labels in the demo figure.
for _fname in ("Noto Sans CJK SC", "Noto Sans CJK JP", "WenQuanYi Micro Hei"):
    try:
        font_manager.findfont(_fname, fallback_to_default=False)
        plt.rcParams["font.sans-serif"] = [_fname, "DejaVu Sans"]
        plt.rcParams["axes.unicode_minus"] = False
        break
    except Exception:
        continue

SEED = 42
OUT_DIR = Path(__file__).resolve().parent
FIG_PATH = OUT_DIR / "holt_winters_demo.png"


def mae(a: np.ndarray, b: np.ndarray) -> float:
    return float(np.mean(np.abs(a - b)))


def rmse(a: np.ndarray, b: np.ndarray) -> float:
    return float(np.sqrt(np.mean((a - b) ** 2)))


def init_additive_hw(y: np.ndarray, m: int) -> tuple[float, float, np.ndarray]:
    """Simple reproducible init (clearly documented).

    - Initial level ℓ_0: mean of first m observations.
    - Initial trend b_0: (mean of season 2 − mean of season 1) / m,
      requiring at least 2m points.
    - Initial seasonal s_{-m+1..0}: first m observations minus ℓ_0.
    """
    y = np.asarray(y, dtype=float)
    if len(y) < 2 * m:
        raise ValueError(f"need at least 2m={2*m} points for init, got {len(y)}")
    level0 = float(np.mean(y[:m]))
    mean1 = float(np.mean(y[:m]))
    mean2 = float(np.mean(y[m : 2 * m]))
    trend0 = (mean2 - mean1) / m
    season0 = y[:m] - level0  # length m; index 0 ↔ season of day 1
    return level0, trend0, season0.astype(float)


def additive_holt_winters(
    y: np.ndarray,
    m: int,
    alpha: float,
    beta_star: float,
    gamma: float,
    level0: float | None = None,
    trend0: float | None = None,
    season0: np.ndarray | None = None,
) -> dict:
    """Fit additive HW; return states, one-step fitted, and final components.

    Indexing: at step t (1-based in formulas), we use Python index t-1 for y.
    Seasonal buffer holds the last m seasonal indices; after processing t,
    season[t % m] is overwritten with s_t (same weekday slot).
    """
    y = np.asarray(y, dtype=float)
    n = len(y)
    if not (0.0 <= alpha <= 1.0 and 0.0 <= beta_star <= 1.0 and 0.0 <= gamma <= 1.0 - alpha + 1e-12):
        # allow tiny float slack on gamma <= 1-alpha
        if gamma > 1.0 - alpha + 1e-9:
            raise ValueError(f"require 0≤γ≤1−α; got α={alpha}, γ={gamma}")

    if level0 is None or trend0 is None or season0 is None:
        level0, trend0, season0 = init_additive_hw(y, m)
    else:
        season0 = np.asarray(season0, dtype=float).copy()

    # season_buf[j] = seasonal index for weekday j (0..m-1), initially from season0
    season_buf = season0.copy()
    # For t=1..m we need s_{t-m}; season0[t-1] is s_{t-m} for first cycle.
    # Keep a full history of seasonal values for forecasting from any t if needed.
    season_hist = np.zeros(n)  # season_hist[t] = s_{t+1} after step? store s_t at index t
    levels = np.zeros(n)
    trends = np.zeros(n)
    fitted = np.full(n, np.nan)  # one-step: ŷ_{t|t-1}

    level = float(level0)
    trend = float(trend0)

    for t in range(n):  # t is 0-based; formula time = t+1
        # seasonal index from m periods ago: for first m steps use init season0
        s_lag = season_buf[t % m]
        # one-step forecast before updating
        fitted[t] = level + trend + s_lag

        y_t = y[t]
        level_new = alpha * (y_t - s_lag) + (1.0 - alpha) * (level + trend)
        trend_new = beta_star * (level_new - level) + (1.0 - beta_star) * trend
        # seasonal: γ(y_t − ℓ_{t−1} − b_{t−1}) + (1−γ) s_{t−m}
        s_new = gamma * (y_t - level - trend) + (1.0 - gamma) * s_lag

        level, trend = level_new, trend_new
        season_buf[t % m] = s_new
        levels[t] = level
        trends[t] = trend
        season_hist[t] = s_new

    return {
        "level0": level0,
        "trend0": trend0,
        "season0": season0,
        "levels": levels,
        "trends": trends,
        "season_hist": season_hist,
        "season_buf": season_buf.copy(),
        "fitted": fitted,
        "level_final": level,
        "trend_final": trend,
        "alpha": alpha,
        "beta_star": beta_star,
        "gamma": gamma,
        "m": m,
    }


def hw_forecast(fit: dict, h: int) -> np.ndarray:
    """h-step ahead forecasts from end of fit: ŷ_{T+h|T} = ℓ_T + h b_T + s_{T+h−m(k+1)}."""
    m = fit["m"]
    level = fit["level_final"]
    trend = fit["trend_final"]
    season_buf = fit["season_buf"]
    n = len(fit["levels"])
    out = np.zeros(h)
    for hi in range(1, h + 1):
        k = (hi - 1) // m
        # index of seasonal: T+h - m(k+1); weekday of forecast step
        # After processing n observations (indices 0..n-1), season_buf[j] holds
        # the most recent seasonal for weekday j = (n-1) % m for that slot...
        # Actually season_buf[j] was last written when t%m == j, i.e. last time
        # t ≡ j (mod m). For forecast at T+h (1-based T=n), season index needed
        # is s_{n+h - m(k+1)}. The weekday of time n+h (1-based) is (n+h-1)%m.
        # Equivalently weekday of forecast horizon hi from end: (n + hi - 1) % m
        season_idx = (n + hi - 1) % m
        out[hi - 1] = level + hi * trend + season_buf[season_idx]
    return out


def simple_exponential_smoothing(
    y: np.ndarray, alpha: float, level0: float | None = None
) -> dict:
    """SES: ℓ_t = α y_t + (1−α) ℓ_{t−1}; ŷ_{t+h|t} = ℓ_t (flat)."""
    y = np.asarray(y, dtype=float)
    n = len(y)
    if level0 is None:
        level0 = float(y[0])
    level = float(level0)
    levels = np.zeros(n)
    fitted = np.full(n, np.nan)
    for t in range(n):
        fitted[t] = level  # ŷ_{t|t-1} = ℓ_{t-1}
        level = alpha * y[t] + (1.0 - alpha) * level
        levels[t] = level
    return {
        "levels": levels,
        "fitted": fitted,
        "level_final": level,
        "alpha": alpha,
        "level0": level0,
    }


def ses_forecast(fit: dict, h: int) -> np.ndarray:
    return np.full(h, fit["level_final"])


def simulate_daily_output(n: int, m: int, rng: np.random.Generator) -> tuple[np.ndarray, dict]:
    """Daily production: slow upward trend + weekend dip + noise. SEED via rng."""
    t = np.arange(n)
    # base level ~ 100 units/day, trend +0.15/day, weekend (Sat=5,Sun=6) lower
    level = 100.0 + 0.15 * t
    # additive seasonal pattern (Mon..Sun), roughly zero-sum
    season_true = np.array([8.0, 10.0, 6.0, 4.0, 2.0, -18.0, -12.0])  # m=7
    season_true = season_true - season_true.mean()  # enforce sum≈0
    seasonal = season_true[t % m]
    noise = rng.normal(0.0, 3.5, size=n)
    y = level + seasonal + noise
    return y, {"season_true": season_true, "level_trend": level, "noise_sigma": 3.5}


def simulate_intraday_price_volume(
    n: int, m: int, rng: np.random.Generator
) -> tuple[np.ndarray, dict]:
    """Synthetic 'weekly pattern' series resembling scaled volume or detrended price."""
    t = np.arange(n)
    base = 50.0 + 0.05 * t
    # mid-week peak, Fri dip style pattern
    season_true = np.array([-4.0, -1.0, 3.0, 5.0, 2.0, -2.0, -3.0])
    season_true = season_true - season_true.mean()
    seasonal = season_true[t % m]
    noise = rng.normal(0.0, 1.8, size=n)
    y = base + seasonal + noise
    return y, {"season_true": season_true}


def grid_search_hw(
    y_train: np.ndarray, m: int, alphas, betas, gammas
) -> tuple[float, float, float, float]:
    """Minimize one-step RMSE on train (skip first m fitted which use cold start)."""
    best = (math.inf, 0.2, 0.1, 0.1)
    for a in alphas:
        for b in betas:
            for g in gammas:
                if g > 1.0 - a + 1e-12:
                    continue
                fit = additive_holt_winters(y_train, m, a, b, g)
                # evaluate from index m onward (after one full season of updates)
                start = m
                f = fit["fitted"][start:]
                yt = y_train[start:]
                err = rmse(yt, f)
                if err < best[0]:
                    best = (err, a, b, g)
    return best  # rmse, alpha, beta_star, gamma


def grid_search_ses(y_train: np.ndarray, alphas) -> tuple[float, float]:
    best = (math.inf, 0.2)
    for a in alphas:
        fit = simple_exponential_smoothing(y_train, a)
        start = 1
        err = rmse(y_train[start:], fit["fitted"][start:])
        if err < best[0]:
            best = (err, a)
    return best


def main() -> None:
    rng = np.random.default_rng(SEED)
    m = 7
    n_train = 70  # 10 weeks
    n_test = 14  # 2 weeks ahead
    n = n_train + n_test

    y, meta = simulate_daily_output(n, m, rng)
    y_train = y[:n_train]
    y_test = y[n_train:]

    # parameter grids (coarse but reproducible)
    alphas = [0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.8]
    betas = [0.05, 0.1, 0.2, 0.3, 0.4]
    gammas = [0.05, 0.1, 0.2, 0.3, 0.4, 0.5]

    hw_rmse_cv, alpha, beta_star, gamma = grid_search_hw(y_train, m, alphas, betas, gammas)
    ses_rmse_cv, alpha_ses = grid_search_ses(y_train, alphas)

    hw = additive_holt_winters(y_train, m, alpha, beta_star, gamma)
    ses = simple_exponential_smoothing(y_train, alpha_ses)

    start = m  # skip cold start for fair one-step metrics
    hw_fit_mae = mae(y_train[start:], hw["fitted"][start:])
    hw_fit_rmse = rmse(y_train[start:], hw["fitted"][start:])
    ses_fit_mae = mae(y_train[start:], ses["fitted"][start:])
    ses_fit_rmse = rmse(y_train[start:], ses["fitted"][start:])

    hw_fc = hw_forecast(hw, n_test)
    ses_fc = ses_forecast(ses, n_test)
    hw_fc_mae = mae(y_test, hw_fc)
    hw_fc_rmse = rmse(y_test, hw_fc)
    ses_fc_mae = mae(y_test, ses_fc)
    ses_fc_rmse = rmse(y_test, ses_fc)

    # second series: weekly pattern "price/volume-like"
    rng2 = np.random.default_rng(SEED + 1)
    y2, meta2 = simulate_intraday_price_volume(n, m, rng2)
    y2_train = y2[:n_train]
    _, a2, b2, g2 = grid_search_hw(y2_train, m, alphas, betas, gammas)
    hw2 = additive_holt_winters(y2_train, m, a2, b2, g2)
    # recover last-cycle seasonal for display (season_buf after fit)
    season_est = hw2["season_buf"].copy()

    # ---- prints ----
    print("=== Additive Holt-Winters demo (SEED=42) ===")
    print(f"m={m}, n_train={n_train}, n_test={n_test}")
    print(f"true weekly season (zero-mean): {np.round(meta['season_true'], 3)}")
    print()
    print("--- Main: daily production ---")
    print(f"HW params: alpha={alpha}, beta_star={beta_star}, gamma={gamma}")
    print(f"SES alpha={alpha_ses}")
    print(f"grid-search train one-step RMSE (from t>m): HW={hw_rmse_cv:.4f}")
    print(f"train one-step MAE  HW={hw_fit_mae:.4f}  SES={ses_fit_mae:.4f}")
    print(f"train one-step RMSE HW={hw_fit_rmse:.4f} SES={ses_fit_rmse:.4f}")
    print(f"forecast {n_test}d MAE  HW={hw_fc_mae:.4f}  SES={ses_fc_mae:.4f}")
    print(f"forecast {n_test}d RMSE HW={hw_fc_rmse:.4f} SES={ses_fc_rmse:.4f}")
    print(f"final level ell_T={hw['level_final']:.4f}, trend b_T={hw['trend_final']:.4f}")
    print(f"final seasonal s (Mon..Sun slots by weekday index 0..6): {np.round(hw['season_buf'], 4)}")
    print(f"HW forecast 14d: {np.round(hw_fc, 2)}")
    print(f"SES forecast 14d (flat): {np.round(ses_fc, 2)}")
    print(f"test truth 14d: {np.round(y_test, 2)}")
    print()
    print("--- Secondary: weekly-pattern series ---")
    print(f"HW2 params: alpha={a2}, beta_star={b2}, gamma={g2}")
    print(f"true season: {np.round(meta2['season_true'], 3)}")
    print(f"estimated season_buf: {np.round(season_est, 4)}")
    print(f"final ell={hw2['level_final']:.4f}, b={hw2['trend_final']:.4f}")

    # ---- figure ----
    fig, axes = plt.subplots(3, 1, figsize=(11, 10), constrained_layout=True)

    ax = axes[0]
    t_all = np.arange(n)
    ax.plot(t_all[:n_train], y_train, "o-", color="#333333", ms=3, lw=1, label="训练观测")
    ax.plot(t_all[n_train:], y_test, "o-", color="#888888", ms=3, lw=1, label="测试真值")
    ax.plot(t_all[:n_train], hw["fitted"], "-", color="#D55E00", lw=1.5, label="HW 一步拟合")
    ax.plot(
        t_all[n_train:],
        hw_fc,
        "s-",
        color="#0072B2",
        ms=4,
        lw=1.5,
        label="HW 14 日预报",
    )
    ax.plot(
        t_all[n_train:],
        ses_fc,
        "--",
        color="#009E73",
        lw=1.5,
        label="SES 预报（平坦）",
    )
    ax.axvline(n_train - 0.5, color="gray", ls=":", lw=1)
    ax.set_title("主实例：产线日产量（周季节 m=7 + 缓升趋势）")
    ax.set_xlabel("日")
    ax.set_ylabel("产量")
    ax.legend(loc="upper left", fontsize=8)
    ax.grid(True, alpha=0.3)

    ax = axes[1]
    # seasonal comparison for main series
    days = np.arange(m)
    labels = ["一", "二", "三", "四", "五", "六", "日"]
    ax.bar(days - 0.2, meta["season_true"], width=0.4, label="真季节", color="#56B4E9")
    ax.bar(days + 0.2, hw["season_buf"], width=0.4, label="HW 估计季节", color="#E69F00")
    ax.set_xticks(days)
    ax.set_xticklabels(labels)
    ax.axhline(0, color="k", lw=0.5)
    ax.set_title("主实例：周季节分量（加法，单位与产量相同）")
    ax.set_ylabel("季节指数")
    ax.legend(fontsize=8)
    ax.grid(True, alpha=0.3, axis="y")

    ax = axes[2]
    t2 = np.arange(n_train)
    ax.plot(t2, y2_train, "o-", color="#333333", ms=2, lw=1, label="合成周内模式序列")
    ax.plot(t2, hw2["fitted"], "-", color="#D55E00", lw=1.2, label="HW 一步拟合")
    # overlay estimated seasonal repeated
    seas_rep = np.tile(season_est, n_train // m + 1)[:n_train]
    ax.plot(t2, hw2["levels"] + seas_rep, ":", color="#0072B2", lw=1, label="水平+季节（示意）")
    ax.set_title("副实例：合成「周内模式」序列（价格/成交量风格，非交易策略）")
    ax.set_xlabel("日")
    ax.set_ylabel("数值")
    ax.legend(loc="upper left", fontsize=8)
    ax.grid(True, alpha=0.3)

    fig.savefig(FIG_PATH, dpi=140)
    print()
    print(f"figure saved: {FIG_PATH}")


if __name__ == "__main__":
    main()
