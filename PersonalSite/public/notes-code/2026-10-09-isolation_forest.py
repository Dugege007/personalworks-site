#!/usr/bin/env python3
"""Mini Isolation Forest demo (Liu et al., ICDM 2008) — numpy only.

SEED=42. Synthetic 3-feature production-line samples:
  vibration (mm/s), current (A), temperature (°C).
Normal cloud + sparse outliers. Prints scores and writes a figure.

Usage:
  MPLBACKEND=Agg python3 2026-10-09-isolation_forest.py
"""

from __future__ import annotations

import math
from dataclasses import dataclass
from pathlib import Path
from typing import List, Optional, Tuple

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np

SEED = 42
N_NORMAL = 200
N_OUTLIER = 8
N_TREES = 100
MAX_SAMPLES = 256  # ψ in the paper; we use min(ψ, n)
CONTAMINATION = 0.04  # expected outlier fraction → score threshold
EULER_GAMMA = 0.5772156649

OUT_DIR = Path(__file__).resolve().parent
FIG_PATH = OUT_DIR / "isolation_forest_demo.png"


def c_factor(n: float) -> float:
    """Average path length of unsuccessful BST search / iTree external termination."""
    if n <= 1:
        return 0.0
    if n == 2:
        return 1.0
    return 2.0 * (math.log(n - 1.0) + EULER_GAMMA) - 2.0 * (n - 1.0) / n


@dataclass
class Node:
    size: int
    left: Optional["Node"] = None
    right: Optional["Node"] = None
    split_att: Optional[int] = None
    split_value: Optional[float] = None

    @property
    def is_external(self) -> bool:
        return self.left is None


def _build_tree(
    X: np.ndarray, rng: np.random.Generator, depth: int, height_limit: int
) -> Node:
    n, d = X.shape
    if n <= 1 or depth >= height_limit or np.allclose(X, X[0]):
        return Node(size=n)
    q = int(rng.integers(0, d))
    col = X[:, q]
    lo, hi = float(col.min()), float(col.max())
    if lo == hi:
        return Node(size=n)
    p = float(rng.uniform(lo, hi))
    left_mask = col < p
    if not left_mask.any() or left_mask.all():
        return Node(size=n)
    return Node(
        size=n,
        left=_build_tree(X[left_mask], rng, depth + 1, height_limit),
        right=_build_tree(X[~left_mask], rng, depth + 1, height_limit),
        split_att=q,
        split_value=p,
    )


def _path_length(x: np.ndarray, node: Node, e: int = 0) -> float:
    while not node.is_external:
        e += 1
        if x[node.split_att] < node.split_value:  # type: ignore[index]
            node = node.left  # type: ignore[assignment]
        else:
            node = node.right  # type: ignore[assignment]
    return e + c_factor(node.size)


class MiniIsolationForest:
    def __init__(
        self,
        n_estimators: int = 100,
        max_samples: int = 256,
        contamination: float = 0.1,
        random_state: int = 0,
    ):
        self.n_estimators = n_estimators
        self.max_samples = max_samples
        self.contamination = contamination
        self.random_state = random_state
        self.trees: List[Node] = []
        self.psi: int = 0
        self.threshold_: Optional[float] = None

    def fit(self, X: np.ndarray) -> "MiniIsolationForest":
        rng = np.random.default_rng(self.random_state)
        n = X.shape[0]
        self.psi = int(min(self.max_samples, n))
        height_limit = int(math.ceil(math.log2(max(self.psi, 2))))
        self.trees = []
        for _ in range(self.n_estimators):
            if self.psi < n:
                idx = rng.choice(n, size=self.psi, replace=False)
                sample = X[idx]
            else:
                sample = X
            self.trees.append(_build_tree(sample, rng, 0, height_limit))
        scores = self.anomaly_scores(X)
        # Higher s → more anomalous; flag roughly contamination fraction.
        k = max(1, int(math.ceil(self.contamination * n)))
        self.threshold_ = float(np.sort(scores)[::-1][k - 1])
        return self

    def expected_path_lengths(self, X: np.ndarray) -> np.ndarray:
        out = np.empty(X.shape[0], dtype=float)
        for i in range(X.shape[0]):
            x = X[i]
            total = 0.0
            for tree in self.trees:
                total += _path_length(x, tree)
            out[i] = total / len(self.trees)
        return out

    def anomaly_scores(self, X: np.ndarray) -> np.ndarray:
        """Paper score s(x, ψ) = 2^(-E[h(x)] / c(ψ)); closer to 1 → more anomalous."""
        eh = self.expected_path_lengths(X)
        return np.power(2.0, -eh / c_factor(self.psi))

    def predict(self, X: np.ndarray, scores: Optional[np.ndarray] = None) -> np.ndarray:
        """+1 inlier, -1 outlier (sklearn-style labels)."""
        assert self.threshold_ is not None
        if scores is None:
            scores = self.anomaly_scores(X)
        return np.where(scores >= self.threshold_, -1, 1)


def make_line_data(rng: np.random.Generator) -> Tuple[np.ndarray, np.ndarray]:
    """Columns: vibration, current, temperature."""
    normal = np.column_stack(
        [
            rng.normal(2.0, 0.35, N_NORMAL),
            rng.normal(12.0, 0.8, N_NORMAL),
            rng.normal(55.0, 2.5, N_NORMAL),
        ]
    )
    outliers = np.array(
        [
            [5.8, 12.2, 56.0],
            [2.1, 18.5, 54.0],
            [1.9, 11.8, 78.0],
            [6.2, 17.0, 72.0],
            [0.2, 5.0, 40.0],
            [4.5, 15.5, 68.0],
            [7.0, 11.5, 53.0],
            [2.0, 12.0, 90.0],
        ],
        dtype=float,
    )
    assert outliers.shape[0] == N_OUTLIER
    X = np.vstack([normal, outliers])
    y_true = np.array([0] * N_NORMAL + [1] * N_OUTLIER)
    return X, y_true


def tiny_hand_example() -> None:
    X_toy = np.array(
        [
            [0.0, 0.1],
            [0.2, -0.1],
            [-0.1, 0.2],
            [0.1, 0.0],
            [-0.2, -0.1],
            [8.0, 8.0],
        ],
        dtype=float,
    )
    forest = MiniIsolationForest(
        n_estimators=50, max_samples=6, contamination=1 / 6, random_state=7
    )
    forest.fit(X_toy)
    s = forest.anomaly_scores(X_toy)
    eh = forest.expected_path_lengths(X_toy)
    print("--- Tiny hand example (2-D, 5 normal + 1 far point) ---")
    print(f"psi={forest.psi}, c(psi)={c_factor(forest.psi):.4f}")
    for i, (pt, e, sc) in enumerate(zip(X_toy, eh, s)):
        print(f"  pt{i} {pt.tolist()}: E[h]={e:.3f}, s={sc:.4f}")
    print()


def main() -> None:
    tiny_hand_example()

    rng = np.random.default_rng(SEED)
    X, y_true = make_line_data(rng)
    forest = MiniIsolationForest(
        n_estimators=N_TREES,
        max_samples=MAX_SAMPLES,
        contamination=CONTAMINATION,
        random_state=SEED,
    )
    forest.fit(X)
    scores = forest.anomaly_scores(X)
    labels = forest.predict(X, scores)
    eh = forest.expected_path_lengths(X)

    pred_anom = labels == -1
    planted = y_true == 1
    tp = int(np.sum(pred_anom & planted))
    fp = int(np.sum(pred_anom & ~planted))
    fn = int(np.sum(~pred_anom & planted))

    print("--- Production-line demo (3 features) ---")
    print(f"SEED={SEED}, n={X.shape[0]}, d={X.shape[1]}")
    print(f"n_trees={N_TREES}, psi={forest.psi}, c(psi)={c_factor(forest.psi):.4f}")
    print(f"contamination={CONTAMINATION}, score_threshold={forest.threshold_:.4f}")
    print(
        f"score: min={scores.min():.4f}, max={scores.max():.4f}, mean={scores.mean():.4f}"
    )
    print(
        f"E[h]: normal mean={eh[~planted].mean():.3f}, "
        f"anomaly mean={eh[planted].mean():.3f}"
    )
    print(f"detected anomalies (pred=-1): {int(pred_anom.sum())}")
    print(f"planted hit: TP={tp}/{N_OUTLIER}, FP={fp}, FN={fn}")
    print("Planted outlier scores (last 8 rows):")
    for i in range(N_NORMAL, N_NORMAL + N_OUTLIER):
        print(
            f"  idx={i} vib={X[i,0]:.2f} I={X[i,1]:.2f} T={X[i,2]:.2f} "
            f"s={scores[i]:.4f} E[h]={eh[i]:.3f} pred={labels[i]}"
        )
    top_idx = np.argsort(scores)[::-1][:8]
    print("Top-8 by anomaly score s:")
    for rank, i in enumerate(top_idx, 1):
        tag = "planted" if planted[i] else "normal"
        print(f"  #{rank} idx={i} ({tag}) s={scores[i]:.4f}")

    fig, axes = plt.subplots(1, 2, figsize=(11.5, 5.0))
    ax0 = axes[0]
    sc0 = ax0.scatter(
        X[~planted, 0],
        X[~planted, 1],
        c=scores[~planted],
        cmap="viridis",
        s=28,
        alpha=0.85,
        vmin=scores.min(),
        vmax=scores.max(),
        label="normal cloud",
    )
    ax0.scatter(
        X[planted, 0],
        X[planted, 1],
        c=scores[planted],
        cmap="viridis",
        s=90,
        edgecolors="red",
        linewidths=1.4,
        vmin=scores.min(),
        vmax=scores.max(),
        marker="D",
        label="planted outliers",
    )
    if fp:
        ax0.scatter(
            X[pred_anom & ~planted, 0],
            X[pred_anom & ~planted, 1],
            s=160,
            facecolors="none",
            edgecolors="orange",
            linewidths=1.6,
            label=f"FP (n={fp})",
        )
    if fn:
        ax0.scatter(
            X[~pred_anom & planted, 0],
            X[~pred_anom & planted, 1],
            s=200,
            facecolors="none",
            edgecolors="magenta",
            linewidths=1.8,
            label=f"FN (n={fn})",
        )
    ax0.set_xlabel("vibration (mm/s)")
    ax0.set_ylabel("current (A)")
    ax0.set_title("Isolation Forest scores (vib × current)")
    ax0.legend(loc="upper left", fontsize=8)
    cb0 = fig.colorbar(sc0, ax=ax0, fraction=0.046, pad=0.04)
    cb0.set_label("anomaly score s (↑ more anomalous)")

    ax1 = axes[1]
    order = np.argsort(scores)
    colors = np.where(planted[order], "#d62728", "#1f77b4")
    ax1.barh(np.arange(len(order)), scores[order], color=colors, height=1.0)
    ax1.axvline(
        forest.threshold_,
        color="black",
        linestyle="--",
        linewidth=1.2,
        label=f"threshold s≥{forest.threshold_:.3f}",
    )
    ax1.set_xlabel("anomaly score s")
    ax1.set_ylabel("samples (sorted by s)")
    ax1.set_title("Score ranking (red = planted outliers)")
    ax1.legend(loc="lower right", fontsize=8)
    ax1.set_ylim(-1, len(order))

    fig.suptitle(
        f"Mini Isolation Forest demo  |  SEED={SEED}, t={N_TREES}, ψ={forest.psi}, "
        f"contamination={CONTAMINATION}",
        fontsize=11,
    )
    fig.tight_layout()
    fig.savefig(FIG_PATH, dpi=140)
    print(f"\nWrote figure: {FIG_PATH}")


if __name__ == "__main__":
    main()
