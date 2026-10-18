"""
SDF + Sphere Tracing（球面追踪）演示
- 场景：地面平面 + 「平滑并集」的圆角方块与球（像一个铸件/机加工零件）
- 对比：球面追踪 vs 固定步长光线步进（fixed-step ray marching）
- 输出：渲染图、每像素步数热力图、2D 示意图（沿光线画「安全球」）
依赖：numpy, matplotlib
"""
import time
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

# ---------- 基本 SDF（公式来自 Inigo Quilez, distfunctions） ----------
def sd_sphere(p, r):
    return np.linalg.norm(p, axis=-1) - r

def sd_round_box(p, b, r):
    q = np.abs(p) - (np.asarray(b) - r)
    outside = np.linalg.norm(np.maximum(q, 0.0), axis=-1)
    inside = np.minimum(np.max(q, axis=-1), 0.0)
    return outside + inside - r

def sd_plane_y(p, h):          # 平面 y = h，法线 (0,1,0)
    return p[..., 1] - h

def op_smooth_union(a, b, k):  # iq 的二次 smooth min，k 单位是距离
    k = k * 4.0
    h = np.maximum(k - np.abs(a - b), 0.0)
    return np.minimum(a, b) - h * h * 0.25 / k

def scene_part(p):
    box = sd_round_box(p - np.array([-0.35, 0.0, 0.0]), [0.45, 0.45, 0.45], 0.08)
    ball = sd_sphere(p - np.array([0.45, 0.15, 0.0]), 0.42)
    return op_smooth_union(box, ball, 0.12)

def scene(p):
    return np.minimum(scene_part(p), sd_plane_y(p, -0.45))

# ---------- 球面追踪（向量化：所有像素同时走） ----------
def sphere_trace(ro, rd, t_max=20.0, eps=1e-3, max_steps=128):
    n = rd.shape[0]
    t = np.zeros(n)
    hit = np.zeros(n, bool)
    alive = np.ones(n, bool)
    steps = np.zeros(n, int)
    for _ in range(max_steps):
        if not alive.any():
            break
        idx = np.nonzero(alive)[0]
        d = scene(ro + rd[idx] * t[idx, None])
        steps[idx] += 1
        newly_hit = d < eps * np.maximum(t[idx], 1.0)   # 远处放宽：误差约一个像素量级
        hit[idx[newly_hit]] = True
        t[idx] += np.where(newly_hit, 0.0, d)            # 关键一行：步长 = 当前距离
        gone = newly_hit | (t[idx] > t_max)
        alive[idx[gone]] = False
    return t, hit, steps

# ---------- 固定步长步进（对照组） ----------
def fixed_march(ro, rd, dt, t_max=20.0):
    n = rd.shape[0]
    t = np.zeros(n)
    hit = np.zeros(n, bool)
    alive = np.ones(n, bool)
    steps = np.zeros(n, int)
    while alive.any():
        idx = np.nonzero(alive)[0]
        d = scene(ro + rd[idx] * t[idx, None])
        steps[idx] += 1
        inside = d < 0.0
        hit[idx[inside]] = True
        t[idx[~inside]] += dt
        gone = inside | (t[idx] > t_max)
        alive[idx[gone]] = False
    return t, hit, steps

def normal(p, h=1e-3):          # 中心差分求梯度 = 法线（真 SDF 的梯度模长为 1）
    ex = np.array([h, 0, 0]); ey = np.array([0, h, 0]); ez = np.array([0, 0, h])
    g = np.stack([scene(p + ex) - scene(p - ex),
                  scene(p + ey) - scene(p - ey),
                  scene(p + ez) - scene(p - ez)], axis=-1)
    return g / np.linalg.norm(g, axis=-1, keepdims=True)

def camera_rays(W, H):
    ro = np.array([0.6, 0.9, 3.2])
    target = np.array([0.0, 0.0, 0.0])
    fwd = target - ro; fwd /= np.linalg.norm(fwd)
    right = np.cross(fwd, [0, 1, 0]); right /= np.linalg.norm(right)
    up = np.cross(right, fwd)
    ys, xs = np.mgrid[0:H, 0:W]
    u = (2 * (xs + 0.5) / W - 1) * (W / H)
    v = 1 - 2 * (ys + 0.5) / H
    rd = u[..., None] * right + v[..., None] * up + 1.6 * fwd
    rd /= np.linalg.norm(rd, axis=-1, keepdims=True)
    return ro, rd.reshape(-1, 3)

def shade(ro, rd, t, hit):
    col = np.tile(np.array([0.75, 0.82, 0.92]), (rd.shape[0], 1))   # 天空
    p = ro + rd[hit] * t[hit, None]
    nrm = normal(p)
    light = np.array([0.6, 0.8, 0.4]); light /= np.linalg.norm(light)
    lam = np.clip(nrm @ light, 0, 1)
    base = np.where((sd_plane_y(p, -0.45) < scene_part(p))[:, None], [0.55, 0.55, 0.5], [0.85, 0.45, 0.2])
    col[hit] = base * (0.25 + 0.75 * lam[:, None])
    return np.clip(col, 0, 1)

if __name__ == "__main__":
    W, H = 320, 200
    ro, rd = camera_rays(W, H)

    t0 = time.perf_counter()
    t_st, hit_st, steps_st = sphere_trace(ro, rd)
    sec_st = time.perf_counter() - t0

    results = {}
    for dt in (0.05, 0.01):
        t0 = time.perf_counter()
        t_fx, hit_fx, steps_fx = fixed_march(ro, rd, dt)
        results[dt] = (t_fx, hit_fx, steps_fx, time.perf_counter() - t0)

    print(f"图像 {W}x{H} = {W*H} 条光线")
    print(f"[球面追踪] 命中 {hit_st.sum()}，平均步数 {steps_st.mean():.2f}，"
          f"中位 {np.median(steps_st):.0f}，最大 {steps_st.max()}，"
          f"总 SDF 求值 {steps_st.sum()}，耗时 {sec_st:.2f}s")
    for dt, (t_fx, hit_fx, steps_fx, sec) in results.items():
        both = hit_st & hit_fx
        err = np.abs(t_fx[both] - t_st[both])
        print(f"[固定步长 dt={dt}] 命中 {hit_fx.sum()}，平均步数 {steps_fx.mean():.2f}，"
              f"总 SDF 求值 {steps_fx.sum()}（是球面追踪的 {steps_fx.sum()/steps_st.sum():.1f} 倍），"
              f"深度平均误差 {err.mean():.4f}、最大 {err.max():.4f}，"
              f"命中判定不一致像素 {(hit_st != hit_fx).sum()}，耗时 {sec:.2f}s")

    # 法线模长检查：真 SDF 的 |∇d| 应接近 1；smooth union 混合区会小于 1（下界）
    p = ro + rd[hit_st] * t_st[hit_st, None]
    h = 1e-3
    g = np.stack([(scene(p + e) - scene(p - e)) / (2 * h)
                  for e in np.eye(3) * h], axis=-1)
    gn = np.linalg.norm(g, axis=-1)
    print(f"命中点 |∇d|：均值 {gn.mean():.4f}，最小 {gn.min():.4f}，最大 {gn.max():.4f}")

    # ---------- 图 1：渲染图 + 步数热力图 ----------
    img = shade(ro, rd, t_st, hit_st).reshape(H, W, 3)
    fig, ax = plt.subplots(1, 2, figsize=(12, 4))
    ax[0].imshow(img); ax[0].set_title("Sphere tracing render (320x200)"); ax[0].axis("off")
    im = ax[1].imshow(steps_st.reshape(H, W), cmap="magma")
    ax[1].set_title("Steps per pixel (edges = more steps)"); ax[1].axis("off")
    plt.colorbar(im, ax=ax[1], fraction=0.03)
    plt.tight_layout()
    plt.savefig("/workspace/algorithm-articles/code/sdf_sphere_tracing_render.png", dpi=110)

    # ---------- 图 2：2D 示意，沿一条光线画安全球 ----------
    def scene2d(x, y):
        c1 = np.hypot(x - 3.0, y - 0.3) - 0.8
        c2 = np.hypot(x - 5.2, y + 0.9) - 1.1
        return np.minimum(c1, c2)
    xs, ys = np.meshgrid(np.linspace(-0.5, 7, 400), np.linspace(-2.5, 2.5, 300))
    fig, axx = plt.subplots(figsize=(9, 5))
    axx.contourf(xs, ys, scene2d(xs, ys), levels=30, cmap="coolwarm", alpha=0.6)
    axx.contour(xs, ys, scene2d(xs, ys), levels=[0], colors="k", linewidths=2)
    o = np.array([0.0, -0.6]); d = np.array([1.0, 0.0]); d /= np.linalg.norm(d)
    tt = 0.0
    for i in range(30):
        q = o + d * tt
        r = scene2d(q[0], q[1])
        axx.add_patch(plt.Circle(q, abs(r), fill=False, ec="green", lw=1))
        axx.plot(*q, "go", ms=3)
        axx.annotate(str(i + 1), q + np.array([0.0, 0.12]), fontsize=7, color="darkgreen")
        if r < 1e-3:
            print(f"2D 示意：第 {i+1} 次求值命中，t={tt:.4f}")
            break
        tt += r
    axx.plot([o[0], o[0] + d[0] * 7], [o[1], o[1] + d[1] * 7], "g--", lw=0.8)
    axx.set_xlim(-0.5, 7); axx.set_ylim(-2.5, 2.5); axx.set_aspect("equal"); axx.set_title("Sphere tracing in 2D: each circle = safe step")
    plt.tight_layout()
    plt.savefig("/workspace/algorithm-articles/code/sdf_sphere_tracing_2d.png", dpi=110)
