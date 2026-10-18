"""
2026-10-13 每日算法分享：单调双端队列求滑动窗口最大值／最小值
参考：LeetCode 239 Sliding Window Maximum；
Daniel Lemire (2006), Streaming Maximum-Minimum Filter Using No More than
Three Comparisons per Element, Nordic Journal of Computing 13(4):328-339.
"""
from collections import deque
import random, time
import numpy as np

def sliding_max(nums, k):
    """返回每个长度为 k 的窗口的最大值。队列里存下标，对应的值从队头到队尾严格递减。"""
    dq, out = deque(), []
    for i, x in enumerate(nums):
        # 1) 新来的 x 比队尾大：队尾永远不可能再当最大值了，弹掉
        while dq and nums[dq[-1]] <= x:
            dq.pop()
        dq.append(i)
        # 2) 队头已经滑出窗口 [i-k+1, i]：弹掉
        if dq[0] <= i - k:
            dq.popleft()
        # 3) 窗口凑满后，队头就是当前窗口最大值
        if i >= k - 1:
            out.append(nums[dq[0]])
    return out

def sliding_min(nums, k):
    return [-v for v in sliding_max([-x for x in nums], k)]

def trace(nums, k):
    dq = deque()
    print(f"手算演示 nums={nums}, k={k}")
    for i, x in enumerate(nums):
        popped = []
        while dq and nums[dq[-1]] <= x:
            popped.append(nums[dq.pop()])
        dq.append(i)
        expired = None
        if dq[0] <= i - k:
            expired = nums[dq.popleft()]
        q = [nums[j] for j in dq]
        ans = nums[dq[0]] if i >= k - 1 else "-"
        print(f"  i={i} x={x:>2}  从队尾弹出={popped}  队头过期={expired}  队列值={q}  窗口最大={ans}")

def brute(nums, k):
    return [max(nums[i:i + k]) for i in range(len(nums) - k + 1)]

if __name__ == "__main__":
    trace([1, 3, -1, -3, 5, 3, 6, 7], 3)
    print("结果:", sliding_max([1, 3, -1, -3, 5, 3, 6, 7], 3))

    # 正确性：随机对拍
    random.seed(42)
    for _ in range(2000):
        n = random.randint(1, 60); k = random.randint(1, n)
        a = [random.randint(-20, 20) for _ in range(n)]
        assert sliding_max(a, k) == brute(a, k)
    print("随机对拍 2000 组：全部一致")

    # 速度：n=200000，k=600
    rng = np.random.default_rng(42)
    big = rng.normal(size=200_000).tolist(); k = 600
    t0 = time.perf_counter(); r1 = sliding_max(big, k); t1 = time.perf_counter()
    r2 = brute(big, k); t2 = time.perf_counter()
    assert r1 == r2
    print(f"n=200000, k=600：单调队列 {t1-t0:.3f}s，暴力 {t2-t1:.3f}s，倍数≈{(t2-t1)/(t1-t0):.1f}")

    # 产线场景：主轴温度每秒一个点，窗口 60 秒，峰峰值（最大-最小）超过阈值报警
    rng = np.random.default_rng(42)
    T = 1800
    temp = 55 + 0.6 * np.sin(np.arange(T) / 90) + rng.normal(0, 0.25, T)
    temp[1200:1260] += np.linspace(0, 3.0, 60)   # 1200s 起冷却异常，60 秒内升温 3℃
    temp[1260:1320] += 3.0
    temp = temp.tolist()
    W, THR = 60, 2.0
    mx = sliding_max(temp, W); mn = sliding_min(temp, W)
    rng_pp = [a - b for a, b in zip(mx, mn)]
    first = next((j for j, v in enumerate(rng_pp) if v > THR), None)
    t_alarm = first + W - 1 if first is not None else None
    normal_max = max(rng_pp[:1200 - W + 1])
    print(f"产线演示：正常段 60 秒峰峰值最大≈{normal_max:.3f}℃；阈值 {THR}℃；首次报警在第 {t_alarm} 秒（异常从第 1200 秒开始）")

    try:
        import matplotlib; matplotlib.use("Agg")
        import matplotlib.pyplot as plt
        plt.rcParams["font.sans-serif"] = ["Noto Sans CJK SC", "WenQuanYi Zen Hei", "DejaVu Sans"]
        plt.rcParams["axes.unicode_minus"] = False
        x = np.arange(W - 1, T)
        fig, ax = plt.subplots(2, 1, figsize=(10, 6), sharex=True)
        ax[0].plot(temp, lw=0.7, color="gray", label="主轴温度（每秒）")
        ax[0].plot(x, mx, color="tab:red", label="近 60 秒最大值")
        ax[0].plot(x, mn, color="tab:blue", label="近 60 秒最小值")
        ax[0].legend(loc="upper left"); ax[0].set_ylabel("℃")
        ax[1].plot(x, rng_pp, color="tab:purple", label="峰峰值 = 最大 − 最小")
        ax[1].axhline(THR, ls="--", color="k", label=f"阈值 {THR}℃")
        if t_alarm: ax[1].axvline(t_alarm, color="tab:orange", label=f"首次报警 t={t_alarm}s")
        ax[1].legend(loc="upper left"); ax[1].set_xlabel("秒"); ax[1].set_ylabel("℃")
        fig.tight_layout()
        fig.savefig("/workspace/algorithm-articles/code/monotonic_deque_demo.png", dpi=120)
        print("图已保存")
    except Exception as e:
        print("画图失败：", e)
