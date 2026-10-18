#!/usr/bin/env python3
"""MAPF 教学演示：两台 AGV 独立 A* 相撞，再用最小 CBS 解冲突。

仅用标准库。低层 = 时空 A*（可停留）；高层 = 冲突树（顶点约束）。
"""
from __future__ import annotations

import heapq
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Set, Tuple

# ---------------------------------------------------------------------------
# 小车间：5×7。中间两堵货架把上行通道收成窄廊，A/B 对向换位必撞。
# ---------------------------------------------------------------------------
GRID = [
    ".A...B.",  # 行 0：A 与 B 的最短路都走这一行
    "..#.#..",
    ".......",
    "..#.#..",
    ".......",
]
H, W = len(GRID), len(GRID[0])
Cell = Tuple[int, int]
Path = List[Cell]  # path[t] = t 时刻所在格；到达终点后原地停留

# 四邻 + 停留
MOVES = [(-1, 0), (1, 0), (0, -1), (0, 1), (0, 0)]


def walkable(r: int, c: int) -> bool:
    return 0 <= r < H and 0 <= c < W and GRID[r][c] != "#"


def find_agents() -> Dict[str, Tuple[Cell, Cell]]:
    """A 从左往右换到 B 起点，B 反之。"""
    a = next((r, c) for r in range(H) for c in range(W) if GRID[r][c] == "A")
    b = next((r, c) for r in range(H) for c in range(W) if GRID[r][c] == "B")
    return {"A": (a, b), "B": (b, a)}


AGENTS = find_agents()


# ---------------------------------------------------------------------------
# 单机空间 A*（忽略其他车）——用来展示“独立规划会撞”
# ---------------------------------------------------------------------------
def manhattan(a: Cell, b: Cell) -> int:
    return abs(a[0] - b[0]) + abs(a[1] - b[1])


def spatial_astar(start: Cell, goal: Cell) -> Path:
    frontier: List[Tuple[int, Cell]] = [(0, start)]
    g: Dict[Cell, int] = {start: 0}
    came: Dict[Cell, Optional[Cell]] = {start: None}
    while frontier:
        _, cur = heapq.heappop(frontier)
        if cur == goal:
            break
        for dr, dc in MOVES[:4]:  # 不含停留
            nxt = (cur[0] + dr, cur[1] + dc)
            if not walkable(*nxt):
                continue
            ng = g[cur] + 1
            if nxt not in g or ng < g[nxt]:
                g[nxt] = ng
                came[nxt] = cur
                heapq.heappush(frontier, (ng + manhattan(nxt, goal), nxt))
    path: Path = []
    p: Optional[Cell] = goal
    while p is not None:
        path.append(p)
        p = came[p]
    return path[::-1]


# ---------------------------------------------------------------------------
# 约束：顶点 (agent, cell, t)；边 (agent, from, to, t) 表示 t-1→t 的穿越
# ---------------------------------------------------------------------------
VertexConstraint = Tuple[str, Cell, int]
EdgeConstraint = Tuple[str, Cell, Cell, int]


@dataclass(order=True)
class STNode:
    f: int
    g: int = field(compare=False)
    cell: Cell = field(compare=False)
    t: int = field(compare=False)
    parent: Optional["STNode"] = field(default=None, compare=False)


def space_time_astar(
    agent: str,
    start: Cell,
    goal: Cell,
    vertex_cons: Set[VertexConstraint],
    edge_cons: Set[EdgeConstraint],
    t_max: int,
) -> Optional[Path]:
    """时空 A*：状态 (格, 时间)。到终点后继续展开到 t_max，方便检测终点停留冲突。"""
    start_ok = (agent, start, 0) not in vertex_cons
    if not start_ok:
        return None

    frontier: List[STNode] = []
    heapq.heappush(frontier, STNode(manhattan(start, goal), 0, start, 0, None))
    # 已正式展开过的 (cell, t)
    closed: Set[Tuple[Cell, int]] = set()
    best_g: Dict[Tuple[Cell, int], int] = {(start, 0): 0}
    goal_node: Optional[STNode] = None

    while frontier:
        node = heapq.heappop(frontier)
        state = (node.cell, node.t)
        if state in closed:
            continue
        closed.add(state)

        # 到达终点：若之后到 t_max 都不受顶点约束，可收工
        if node.cell == goal:
            safe = True
            for tt in range(node.t, t_max + 1):
                if (agent, goal, tt) in vertex_cons:
                    safe = False
                    break
            if safe:
                goal_node = node
                break

        if node.t >= t_max:
            continue

        for dr, dc in MOVES:
            nxt = (node.cell[0] + dr, node.cell[1] + dc)
            nt = node.t + 1
            if not walkable(*nxt):
                continue
            if (agent, nxt, nt) in vertex_cons:
                continue
            if (agent, node.cell, nxt, nt) in edge_cons:
                continue
            ng = node.g + 1
            key = (nxt, nt)
            if key in best_g and best_g[key] <= ng:
                continue
            best_g[key] = ng
            # 已在终点则启发为 0，否则曼哈顿
            h = 0 if nxt == goal else manhattan(nxt, goal)
            heapq.heappush(frontier, STNode(ng + h, ng, nxt, nt, node))

    if goal_node is None:
        return None

    # 回溯到起点，再把终点停留补到与其他路径对齐所需长度（调用方会 pad）
    cells: Path = []
    n: Optional[STNode] = goal_node
    while n is not None:
        cells.append(n.cell)
        n = n.parent
    return cells[::-1]


def pad_paths(paths: Dict[str, Path]) -> Dict[str, Path]:
    """短路径在终点原地补齐到同一长度。"""
    T = max(len(p) for p in paths.values())
    out: Dict[str, Path] = {}
    for aid, p in paths.items():
        out[aid] = p + [p[-1]] * (T - len(p))
    return out


def path_cost(path: Path) -> int:
    """个体代价：首次到达终点的时刻（步数）。"""
    goal = path[-1]
    for t, c in enumerate(path):
        if c == goal and all(path[tt] == goal for tt in range(t, len(path))):
            # 找真正第一次到达
            break
    first = 0
    for t, c in enumerate(path):
        if c == goal:
            first = t
            break
    # 更稳：从后往前找离开终点前的最后一段
    t = len(path) - 1
    while t > 0 and path[t] == path[-1]:
        t -= 1
    if path[t] == path[-1]:
        return 0
    return t + 1


def first_arrival(path: Path, goal: Cell) -> int:
    for t, c in enumerate(path):
        if c == goal:
            return t
    return len(path) - 1


# ---------------------------------------------------------------------------
# 冲突检测：顶点 / 边（对向交换）
# ---------------------------------------------------------------------------
@dataclass
class Conflict:
    kind: str  # "vertex" | "edge"
    a1: str
    a2: str
    t: int
    cell: Optional[Cell] = None
    cell_a: Optional[Cell] = None  # edge: a1 在 t-1 的格
    cell_b: Optional[Cell] = None  # edge: a2 在 t-1 的格

    def describe(self) -> str:
        if self.kind == "vertex":
            return (
                f"顶点冲突：{self.a1} 与 {self.a2} 在 t={self.t} "
                f"同时占据 {self.cell}"
            )
        return (
            f"边冲突（对向换位）：t={self.t-1}→{self.t} 时 "
            f"{self.a1} {self.cell_a}↔{self.cell_b} {self.a2}"
        )


def detect_conflict(paths: Dict[str, Path]) -> Optional[Conflict]:
    padded = pad_paths(paths)
    ids = list(padded.keys())
    T = len(next(iter(padded.values())))
    for t in range(T):
        # 顶点
        occ: Dict[Cell, str] = {}
        for aid in ids:
            cell = padded[aid][t]
            if cell in occ:
                return Conflict("vertex", occ[cell], aid, t, cell=cell)
            occ[cell] = aid
        # 边（交换）
        if t == 0:
            continue
        for i, a1 in enumerate(ids):
            for a2 in ids[i + 1 :]:
                c1_prev, c1 = padded[a1][t - 1], padded[a1][t]
                c2_prev, c2 = padded[a2][t - 1], padded[a2][t]
                if c1_prev == c2 and c2_prev == c1 and c1 != c1_prev:
                    return Conflict(
                        "edge", a1, a2, t, cell_a=c1_prev, cell_b=c2_prev
                    )
    return None


# ---------------------------------------------------------------------------
# CBS 高层
# ---------------------------------------------------------------------------
@dataclass(order=True)
class CTNode:
    cost: int
    node_id: int
    constraints_v: Set[VertexConstraint] = field(default_factory=set, compare=False)
    constraints_e: Set[EdgeConstraint] = field(default_factory=set, compare=False)
    paths: Dict[str, Path] = field(default_factory=dict, compare=False)


def sic(paths: Dict[str, Path]) -> int:
    total = 0
    for aid, p in paths.items():
        goal = AGENTS[aid][1]
        total += first_arrival(p, goal)
    return total


def replan_agent(
    agent: str,
    vcons: Set[VertexConstraint],
    econs: Set[EdgeConstraint],
    t_max: int,
) -> Optional[Path]:
    start, goal = AGENTS[agent]
    return space_time_astar(agent, start, goal, vcons, econs, t_max)


def cbs(t_max: int = 30) -> Tuple[Optional[Dict[str, Path]], Optional[Conflict], int]:
    """返回 (解, 根节点上发现的首个冲突, 高层展开节点数)。"""
    # 根：无约束，各自时空 A*（等价于可停留的最短路）
    root_paths: Dict[str, Path] = {}
    for aid in AGENTS:
        p = replan_agent(aid, set(), set(), t_max)
        if p is None:
            return None, None, 0
        root_paths[aid] = p

    first_conflict = detect_conflict(root_paths)
    counter = 0
    open_q: List[CTNode] = [
        CTNode(sic(root_paths), counter, set(), set(), root_paths)
    ]
    expanded = 0

    while open_q:
        node = heapq.heappop(open_q)
        expanded += 1
        conflict = detect_conflict(node.paths)
        if conflict is None:
            return pad_paths(node.paths), first_conflict, expanded

        # 两个分支：禁止 a1 或禁止 a2
        branches = []
        if conflict.kind == "vertex":
            assert conflict.cell is not None
            branches = [
                (conflict.a1, conflict.cell, conflict.t, "vertex"),
                (conflict.a2, conflict.cell, conflict.t, "vertex"),
            ]
        else:
            # 边冲突：禁止 a1 走 cell_a→cell_b，禁止 a2 走 cell_b→cell_a
            assert conflict.cell_a is not None and conflict.cell_b is not None
            branches = [
                (conflict.a1, (conflict.cell_a, conflict.cell_b), conflict.t, "edge"),
                (conflict.a2, (conflict.cell_b, conflict.cell_a), conflict.t, "edge"),
            ]

        for agent, payload, t, kind in branches:
            nv = set(node.constraints_v)
            ne = set(node.constraints_e)
            if kind == "vertex":
                nv.add((agent, payload, t))  # type: ignore[arg-type]
            else:
                frm, to = payload  # type: ignore[misc]
                ne.add((agent, frm, to, t))

            new_paths = dict(node.paths)
            replanned = replan_agent(agent, nv, ne, t_max)
            if replanned is None:
                continue
            new_paths[agent] = replanned
            # 若新路径更短，pad 时由 detect 处理；代价用 SIC
            counter += 1
            heapq.heappush(
                open_q, CTNode(sic(new_paths), counter, nv, ne, new_paths)
            )

    return None, first_conflict, expanded


# ---------------------------------------------------------------------------
# 打印
# ---------------------------------------------------------------------------
def print_grid_static() -> None:
    print("地图（A/B 为起点，# 为货架）：")
    for row in GRID:
        print("  " + row)


def path_str(aid: str, path: Path) -> str:
    goal = AGENTS[aid][1]
    arr = first_arrival(path, goal)
    coords = " → ".join(f"({r},{c})" for r, c in path[: arr + 1])
    return f"  {aid}: {coords}  （首次到达 t={arr}，代价={arr}）"


def print_frames(paths: Dict[str, Path]) -> None:
    padded = pad_paths(paths)
    T = len(next(iter(padded.values())))
    print(f"\n逐帧占位（t=0..{T-1}）：")
    for t in range(T):
        rows = [list(row.replace("A", ".").replace("B", ".")) for row in GRID]
        for aid, p in padded.items():
            r, c = p[t]
            ch = rows[r][c]
            rows[r][c] = aid if ch == "." else "X"  # X = 仍撞（不应出现）
        print(f"  t={t}")
        for row in rows:
            print("    " + "".join(row))


def main() -> int:
    print("=" * 56)
    print("MAPF 教学演示：独立 A* 相撞 → CBS 解冲突")
    print("=" * 56)
    print_grid_static()
    print(f"任务：A {AGENTS['A'][0]} → {AGENTS['A'][1]}；"
          f"B {AGENTS['B'][0]} → {AGENTS['B'][1]}（对向换位）")

    # --- 1. 独立空间 A* ---
    print("\n【1】各自跑空间 A*（互相看不见）")
    indep: Dict[str, Path] = {
        aid: spatial_astar(*AGENTS[aid]) for aid in AGENTS
    }
    for aid in ("A", "B"):
        print(path_str(aid, indep[aid]))
    print(f"  SIC（代价和）= {sic(indep)}")

    conf = detect_conflict(indep)
    if conf:
        print(f"\n【2】冲突：{conf.describe()}")
    else:
        print("\n【2】意外：独立路径无冲突（地图需调整）")
        return 1

    # --- 2. CBS ---
    print("\n【3】CBS 求解（低层=时空 A*，高层=冲突树）")
    solution, root_conf, n_exp = cbs(t_max=24)
    if solution is None:
        print("  CBS 未找到解（可增大 t_max）")
        return 1
    print(f"  高层展开节点数 = {n_exp}")
    if root_conf:
        print(f"  根节点冲突（与上同）: {root_conf.describe()}")
    for aid in ("A", "B"):
        print(path_str(aid, solution[aid]))
    print(f"  SIC（代价和）= {sic(solution)}")

    conf2 = detect_conflict(solution)
    if conf2:
        print(f"  仍有冲突：{conf2.describe()}")
        return 1
    print("  校验：无顶点/边冲突 ✓")

    print_frames(solution)

    print("\n说明：本演示处理顶点约束 + 边（对向换位）约束；")
    print("      未实现跟随冲突的专门分支、也未做 ICBS/旁观者启发等加速。")
    print("=" * 56)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
