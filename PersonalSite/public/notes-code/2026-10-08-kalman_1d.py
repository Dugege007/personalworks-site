#!/usr/bin/env python3
"""一维卡尔曼滤波演示：传送带/AGV 位置估计与一步预测。

模型（标量）：
  预测:  x^- = x + v*dt ,  P^- = P + Q
  更新:  K = P^- / (P^- + R)
         x = x^- + K (z - x^-)
         P = (1 - K) P^-

过程噪声 Q、观测噪声 R 控制「信模型 vs 信观测」。
"""

from __future__ import annotations

import math
from pathlib import Path

import numpy as np

# ---------------------------------------------------------------------------
# 参数（与文中迷你数值例子一致）
# ---------------------------------------------------------------------------
DT = 1.0          # 采样周期 s
V_CMD = 1.0       # 指令速度 m/s（已知控制输入）
Q = 0.25          # 过程噪声方差 (m^2)，约 0.5 m 标准差
R = 4.0           # 观测噪声方差 (m^2)，约 2.0 m 标准差
X0_HAT = 0.0      # 初始位置估计
P0 = 9.0          # 初始估计方差
N_STEPS = 20
SEED = 42
TRUE_X0 = 0.0


def kalman_step(
    x: float,
    p: float,
    z: float,
    v: float = V_CMD,
    dt: float = DT,
    q: float = Q,
    r: float = R,
) -> dict:
    """执行一步预测+更新，返回中间量便于打印。"""
    # --- 预测 ---
    x_pred = x + v * dt
    p_pred = p + q
    # --- 更新 ---
    gain = p_pred / (p_pred + r)
    innov = z - x_pred
    x_post = x_pred + gain * innov
    p_post = (1.0 - gain) * p_pred
    return {
        "x_pred": x_pred,
        "p_pred": p_pred,
        "gain": gain,
        "innov": innov,
        "x_post": x_post,
        "p_post": p_post,
    }


def main() -> None:
    rng = np.random.default_rng(SEED)

    true_x = TRUE_X0
    x_hat = X0_HAT
    p = P0

    rows: list[dict] = []
    print("一维卡尔曼：AGV/传送带位置（指令速度 v=1 m/s, dt=1 s）")
    print(f"Q={Q}, R={R}, 初值 x̂={X0_HAT}, P={P0}")
    print("-" * 88)
    hdr = (
        f"{'t':>3} {'true':>8} {'z':>8} {'x^-':>8} {'P^-':>8} "
        f"{'K':>7} {'x_hat':>8} {'P':>8} {'pred+1':>8}"
    )
    print(hdr)
    print("-" * 88)

    for t in range(1, N_STEPS + 1):
        # 真值按指令速度前进（外加一点点未建模扰动，仅用于合成真值）
        true_x = true_x + V_CMD * DT + rng.normal(0.0, math.sqrt(0.05))
        z = true_x + rng.normal(0.0, math.sqrt(R))

        step = kalman_step(x_hat, p, z)
        x_hat = step["x_post"]
        p = step["p_post"]
        # 一步预测（还没有下一拍观测时的先验位置）
        x_one_step = x_hat + V_CMD * DT

        row = {
            "t": t,
            "true": true_x,
            "z": z,
            **step,
            "pred1": x_one_step,
        }
        rows.append(row)
        print(
            f"{t:3d} {true_x:8.3f} {z:8.3f} {step['x_pred']:8.3f} {step['p_pred']:8.3f} "
            f"{step['gain']:7.4f} {x_hat:8.3f} {p:8.3f} {x_one_step:8.3f}"
        )

    # 详细打印前两步，供文章「走一遍数字」引用
    print("\n=== 前两步拆解（文中引用）===")
    for row in rows[:2]:
        print(
            f"t={row['t']}: 预测 x^-={row['x_pred']:.4f}, P^-={row['p_pred']:.4f}; "
            f"K={row['gain']:.4f}; 更新 x̂={row['x_post']:.4f}, P={row['p_post']:.4f}; "
            f"z={row['z']:.4f}, true={row['true']:.4f}"
        )

    # 稳态增益直觉：P 收敛后 K 接近常数，行为类似固定增益指数平滑
    p_ss = rows[-1]["p_post"]
    k_ss = (p_ss + Q) / ((p_ss + Q) + R)
    print(f"\n末步后验 P≈{p_ss:.4f}；下一拍预期 K≈{k_ss:.4f}（稳态时接近固定增益平滑）")

    mse_raw = float(np.mean([(r["z"] - r["true"]) ** 2 for r in rows]))
    mse_filt = float(np.mean([(r["x_post"] - r["true"]) ** 2 for r in rows]))
    print(f"观测 MSE={mse_raw:.3f}，滤波后 MSE={mse_filt:.3f}")

    # 可选：保存轨迹图（无 GUI；标签用英文避免缺字）
    try:
        import matplotlib

        matplotlib.use("Agg")
        import matplotlib.pyplot as plt

        ts = [r["t"] for r in rows]
        fig, ax = plt.subplots(figsize=(8, 4.5))
        ax.plot(ts, [r["true"] for r in rows], "g-", label="true x", linewidth=2)
        ax.plot(ts, [r["z"] for r in rows], "bx", label="noisy z", markersize=6)
        ax.plot(ts, [r["x_post"] for r in rows], "r-", label="filtered x_hat", linewidth=1.5)
        ax.plot(ts, [r["pred1"] for r in rows], "m--", label="1-step pred", alpha=0.7)
        ax.set_xlabel("t")
        ax.set_ylabel("position (m)")
        ax.set_title("1D Kalman: AGV position + 1-step prediction")
        ax.legend(loc="best")
        ax.grid(True, alpha=0.3)
        out = Path(__file__).resolve().parent / "2026-10-08-kalman_1d.png"
        fig.tight_layout()
        fig.savefig(out, dpi=120)
        plt.close(fig)
        print(f"\n图已保存: {out}")
    except Exception as exc:  # noqa: BLE001
        print(f"\n跳过绑图（{exc}）")


if __name__ == "__main__":
    main()
