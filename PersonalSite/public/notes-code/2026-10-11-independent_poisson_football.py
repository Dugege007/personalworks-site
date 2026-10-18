#!/usr/bin/env python3
"""Maher 1982 Independent Poisson football score model — reproducible demo.

SEED=42. Uses only stdlib + numpy + matplotlib.

Parameterization (engineering form, aligned with Maher Model 2):
  λ_ij = τ · α_i · β_j · γ     # home expected goals
  μ_ij = τ · α_j · β_i         # away expected goals
where
  α  = attack strength (>1 stronger attack)
  β  = defensive weakness (>1 weaker defence → more goals conceded)
  γ  = common home-advantage factor
  τ  = league baseline expected goals (scale)

Score: P(X=x, Y=y) = Pois(x; λ) · Pois(y; μ)
1X2 from summing the truncated score matrix (0..max_goals).
"""

from __future__ import annotations

import math
from pathlib import Path

import matplotlib.pyplot as plt
import numpy as np
from matplotlib import font_manager

# Prefer Noto Sans CJK SC for Chinese labels in figures.
for _fp in (
    "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc",
    "/usr/share/fonts/opentype/noto/NotoSerifCJK-Regular.ttc",
):
    if Path(_fp).exists():
        font_manager.fontManager.addfont(_fp)
        _name = font_manager.FontProperties(fname=_fp).get_name()
        plt.rcParams["font.family"] = _name
        plt.rcParams["axes.unicode_minus"] = False
        break

SEED = 42
RNG = np.random.default_rng(SEED)
OUT_DIR = Path(__file__).resolve().parent
MAX_GOALS = 6


def poisson_pmf(k: np.ndarray | int, lam: float) -> np.ndarray | float:
    """P(K=k) for Poisson(lam). Vectorized over k."""
    k_arr = np.asarray(k, dtype=float)
    # use log form for stability then exp
    log_p = -lam + k_arr * math.log(lam) - np.vectorize(math.lgamma)(k_arr + 1.0)
    return np.exp(log_p)


def score_matrix(lam: float, mu: float, max_goals: int = MAX_GOALS) -> np.ndarray:
    """Independent Poisson joint PMF truncated to {0..max_goals}^2 (not renormalized)."""
    ks = np.arange(0, max_goals + 1)
    p_home = poisson_pmf(ks, lam)
    p_away = poisson_pmf(ks, mu)
    return np.outer(p_home, p_away)


def one_x_two(mat: np.ndarray) -> tuple[float, float, float]:
    """Home win / draw / away win from score matrix (rows=home goals, cols=away)."""
    # home win: x > y
    home = float(np.tril(mat, k=-1).sum())
    draw = float(np.trace(mat))
    away = float(np.triu(mat, k=1).sum())
    return home, draw, away


def expected_goals(
    alpha_home: float,
    beta_home: float,
    alpha_away: float,
    beta_away: float,
    gamma: float,
    tau: float,
) -> tuple[float, float]:
    lam = tau * alpha_home * beta_away * gamma
    mu = tau * alpha_away * beta_home
    return lam, mu


def fair_odds(p: float) -> float:
    return float("inf") if p <= 0 else 1.0 / p


def implied_probs_from_decimal(odds: dict[str, float]) -> dict[str, float]:
    """Remove overround by proportional normalization of 1/odds."""
    raw = {k: 1.0 / v for k, v in odds.items()}
    s = sum(raw.values())
    return {k: v / s for k, v in raw.items()}


def iterative_mle(
    matches: list[tuple[int, int, int, int]],
    n_teams: int,
    gamma_init: float = 1.3,
    tau: float = 1.0,
    n_iter: int = 80,
) -> tuple[np.ndarray, np.ndarray, float]:
    """Very small alternating MLE on toy data (attack / defence / home factor).

    Constraint: geometric mean of α = 1 and geometric mean of β = 1
    (equivalent to sum log α = 0, sum log β = 0). Matches are
    (home_idx, away_idx, home_goals, away_goals).
    """
    alpha = np.ones(n_teams)
    beta = np.ones(n_teams)
    gamma = gamma_init

    for _ in range(n_iter):
        # update attack: for each team, collect scored goals / current denom
        # home scoring: λ = τ α_h β_a γ  → α_h contribution from home goals
        # away scoring: μ = τ α_a β_h    → α_a contribution from away goals
        scored_sum = np.zeros(n_teams)
        denom = np.zeros(n_teams)
        for h, a, x, y in matches:
            scored_sum[h] += x
            denom[h] += tau * beta[a] * gamma
            scored_sum[a] += y
            denom[a] += tau * beta[h]
        alpha = np.where(denom > 0, scored_sum / denom, alpha)
        # renormalize geometric mean to 1
        alpha = alpha / np.exp(np.mean(np.log(np.maximum(alpha, 1e-12))))

        conceded_sum = np.zeros(n_teams)
        denom_b = np.zeros(n_teams)
        for h, a, x, y in matches:
            # home team concedes y under μ = τ α_a β_h
            conceded_sum[h] += y
            denom_b[h] += tau * alpha[a]
            # away team concedes x under λ = τ α_h β_a γ
            conceded_sum[a] += x
            denom_b[a] += tau * alpha[h] * gamma
        beta = np.where(denom_b > 0, conceded_sum / denom_b, beta)
        beta = beta / np.exp(np.mean(np.log(np.maximum(beta, 1e-12))))

        # update gamma from home goals only: x ~ Pois(τ α_h β_a γ)
        home_goals = 0.0
        home_denom = 0.0
        for h, a, x, y in matches:
            home_goals += x
            home_denom += tau * alpha[h] * beta[a]
        if home_denom > 0:
            gamma = home_goals / home_denom

    return alpha, beta, float(gamma)


def main() -> None:
    print("=" * 60)
    print("Maher 1982 Independent Poisson — demo (SEED=%d)" % SEED)
    print("=" * 60)

    # ---- Part A: fixed toy parameters (main worked example) ----
    teams = ["红杉", "青鸟", "白鹿", "玄铁"]
    # attack strength α, defensive weakness β
    alpha = np.array([1.35, 1.10, 0.95, 0.75])
    beta = np.array([0.85, 0.95, 1.05, 1.20])
    gamma = 1.25  # home advantage
    tau = 1.15  # league baseline

    print("\n[Part A] Fixed parameters")
    print("teams:", teams)
    print("alpha (attack):", np.round(alpha, 4).tolist())
    print("beta  (def weakness):", np.round(beta, 4).tolist())
    print("gamma (home adv):", gamma)
    print("tau   (baseline):", tau)

    # focal match: 红杉 (home) vs 白鹿 (away)
    i_home, i_away = 0, 2
    lam, mu = expected_goals(
        alpha[i_home], beta[i_home], alpha[i_away], beta[i_away], gamma, tau
    )
    print("\nFocal match: %s (home) vs %s (away)" % (teams[i_home], teams[i_away]))
    print("lambda (home xG): %.6f" % lam)
    print("mu     (away xG): %.6f" % mu)

    mat = score_matrix(lam, mu, MAX_GOALS)
    trunc_mass = float(mat.sum())
    print("truncated mass (0..%d)^2: %.6f" % (MAX_GOALS, trunc_mass))

    p10 = float(mat[1, 0])
    p00 = float(mat[0, 0])
    p11 = float(mat[1, 1])
    p21 = float(mat[2, 1])
    p01 = float(mat[0, 1])
    print("P(1-0)=%.6f  P(0-0)=%.6f  P(1-1)=%.6f  P(2-1)=%.6f  P(0-1)=%.6f"
          % (p10, p00, p11, p21, p01))

    # top scorelines
    flat_idx = np.dstack(np.unravel_index(np.argsort(mat.ravel())[::-1], mat.shape))[0]
    print("Top 5 scorelines:")
    for r, c in flat_idx[:5]:
        print("  %d-%d: %.6f" % (r, c, mat[r, c]))

    ph, pd, pa = one_x_two(mat)
    # residual mass beyond truncation treated as neither (report raw sums)
    print("1X2 (truncated, not renormalized):")
    print("  P(home)=%.6f  P(draw)=%.6f  P(away)=%.6f  sum=%.6f"
          % (ph, pd, pa, ph + pd + pa))
    # renormalized for market comparison
    s123 = ph + pd + pa
    ph_n, pd_n, pa_n = ph / s123, pd / s123, pa / s123
    print("1X2 (renormalized over truncated mass):")
    print("  P(home)=%.6f  P(draw)=%.6f  P(away)=%.6f"
          % (ph_n, pd_n, pa_n))
    print("fair decimal odds (renorm): H=%.3f  D=%.3f  A=%.3f"
          % (fair_odds(ph_n), fair_odds(pd_n), fair_odds(pa_n)))

    # step-by-step small calc for article: Pois(1;λ) and Pois(0;μ)
    p_h1 = math.exp(-lam) * lam / 1.0
    p_a0 = math.exp(-mu)
    print("\nStep detail for P(1-0):")
    print("  Pois(1;λ)=exp(-λ)·λ = %.6f" % p_h1)
    print("  Pois(0;μ)=exp(-μ)   = %.6f" % p_a0)
    print("  product             = %.6f" % (p_h1 * p_a0))

    # ---- Part B: synthetic matches + iterative MLE ----
    print("\n[Part B] Synthetic round-robin + alternating MLE")
    # generate matches from true params (double round-robin home/away)
    true_alpha, true_beta = alpha.copy(), beta.copy()
    true_gamma, true_tau = gamma, tau
    matches: list[tuple[int, int, int, int]] = []
    for h in range(4):
        for a in range(4):
            if h == a:
                continue
            for _rep in range(3):  # 3 seasons worth of each fixture direction
                lam_t, mu_t = expected_goals(
                    true_alpha[h], true_beta[h],
                    true_alpha[a], true_beta[a],
                    true_gamma, true_tau,
                )
                x = int(RNG.poisson(lam_t))
                y = int(RNG.poisson(mu_t))
                matches.append((h, a, x, y))
    print("n_matches:", len(matches))
    avg_home = np.mean([m[2] for m in matches])
    avg_away = np.mean([m[3] for m in matches])
    print("sample mean goals home/away: %.4f / %.4f" % (avg_home, avg_away))

    est_alpha, est_beta, est_gamma = iterative_mle(matches, n_teams=4, tau=true_tau)
    print("est alpha:", np.round(est_alpha, 4).tolist())
    print("est beta :", np.round(est_beta, 4).tolist())
    print("est gamma:", round(est_gamma, 4))
    print("true alpha:", np.round(true_alpha, 4).tolist())
    print("true beta :", np.round(true_beta, 4).tolist())
    print("true gamma:", true_gamma)

    # refit focal match with estimated params
    lam_e, mu_e = expected_goals(
        est_alpha[i_home], est_beta[i_home],
        est_alpha[i_away], est_beta[i_away],
        est_gamma, true_tau,
    )
    mat_e = score_matrix(lam_e, mu_e, MAX_GOALS)
    ph_e, pd_e, pa_e = one_x_two(mat_e)
    s_e = ph_e + pd_e + pa_e
    print("refit focal λ/μ: %.6f / %.6f" % (lam_e, mu_e))
    print("refit 1X2 renorm: H=%.6f D=%.6f A=%.6f"
          % (ph_e / s_e, pd_e / s_e, pa_e / s_e))

    # ---- Part C: model vs bookmaker implied (toy decimal odds) ----
    print("\n[Part C] Model vs toy bookmaker implied (illustration only)")
    # fictional decimal odds with ~5% overround
    book = {"H": 1.55, "D": 4.20, "A": 5.50}
    impl = implied_probs_from_decimal(book)
    overround = sum(1.0 / v for v in book.values()) - 1.0
    print("book decimal:", book)
    print("overround: %.4f" % overround)
    print("implied (norm): H=%.6f D=%.6f A=%.6f"
          % (impl["H"], impl["D"], impl["A"]))
    print("model (renorm): H=%.6f D=%.6f A=%.6f" % (ph_n, pd_n, pa_n))
    print("delta (model - implied): H=%+.4f D=%+.4f A=%+.4f"
          % (ph_n - impl["H"], pd_n - impl["D"], pa_n - impl["A"]))

    # ---- Figures ----
    fig, axes = plt.subplots(1, 2, figsize=(11.5, 4.8))

    # heatmap
    ax = axes[0]
    im = ax.imshow(mat, origin="lower", cmap="YlOrRd", vmin=0)
    ax.set_xticks(range(MAX_GOALS + 1))
    ax.set_yticks(range(MAX_GOALS + 1))
    ax.set_xlabel("客队进球 y")
    ax.set_ylabel("主队进球 x")
    ax.set_title(
        "比分概率热力图\n%s vs %s  (λ=%.3f, μ=%.3f)"
        % (teams[i_home], teams[i_away], lam, mu)
    )
    for r in range(MAX_GOALS + 1):
        for c in range(MAX_GOALS + 1):
            ax.text(
                c, r, "%.1f%%" % (100 * mat[r, c]),
                ha="center", va="center", fontsize=7,
                color="black" if mat[r, c] < mat.max() * 0.55 else "white",
            )
    fig.colorbar(im, ax=ax, fraction=0.046, pad=0.04, label="P(x,y)")

    # 1X2 bars: model vs implied
    ax2 = axes[1]
    labels = ["主胜", "平", "客胜"]
    model_p = [ph_n, pd_n, pa_n]
    impl_p = [impl["H"], impl["D"], impl["A"]]
    x = np.arange(3)
    w = 0.35
    b1 = ax2.bar(x - w / 2, model_p, w, label="独立泊松模型", color="#d62728")
    b2 = ax2.bar(x + w / 2, impl_p, w, label="假水位隐含(去抽水)", color="#1f77b4")
    ax2.set_xticks(x)
    ax2.set_xticklabels(labels)
    ax2.set_ylim(0, max(max(model_p), max(impl_p)) * 1.25)
    ax2.set_ylabel("概率")
    ax2.set_title("胜平负：模型 vs 假水位隐含概率（对照说明）")
    ax2.legend(loc="upper right", fontsize=9)
    for bars in (b1, b2):
        for bar in bars:
            h = bar.get_height()
            ax2.text(
                bar.get_x() + bar.get_width() / 2, h + 0.01,
                "%.1f%%" % (100 * h), ha="center", va="bottom", fontsize=8,
            )

    fig.suptitle(
        "Maher 独立泊松比分模型演示 (SEED=%d)" % SEED, fontsize=12, y=1.02
    )
    fig.tight_layout()
    out_png = OUT_DIR / "independent_poisson_football_demo.png"
    fig.savefig(out_png, dpi=140, bbox_inches="tight")
    plt.close(fig)
    print("\nSaved figure:", out_png)


if __name__ == "__main__":
    main()
