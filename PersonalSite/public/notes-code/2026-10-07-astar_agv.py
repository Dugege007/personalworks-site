import heapq, math

# 车间栅格：. 普通通道(代价1)  ~ 拥堵通道(代价3)  # 设备/货架(不可走)
GRID = [
    "S.........#.........",
    "..........#.........",
    "..####....#....###..",
    "..#..#....#....#....",
    "..#..#~~~~~~~~~#....",
    "..####~~~~~~~~~#....",
    "......~~~~~~~~~.....",
    "..........#.........",
    "..........#......###",
    "..........#........G",
]
COST = {'.': 1.0, '~': 3.0, 'S': 1.0, 'G': 1.0}
H, W = len(GRID), len(GRID[0])
find = lambda ch: next((r, c) for r in range(H) for c in range(W) if GRID[r][c] == ch)
START, GOAL = find('S'), find('G')

def neighbors(p):  # 8 邻接；斜走不许“切角”穿过设备
    r, c = p
    for dr in (-1, 0, 1):
        for dc in (-1, 0, 1):
            if dr == dc == 0: continue
            nr, nc = r + dr, c + dc
            if not (0 <= nr < H and 0 <= nc < W) or GRID[nr][nc] == '#': continue
            if dr and dc and (GRID[r][nc] == '#' or GRID[nr][c] == '#'): continue
            step = math.sqrt(2) if dr and dc else 1.0
            yield (nr, nc), step * COST[GRID[nr][nc]]   # 距离 × 区域代价

def octile(a, b):  # 8 方向网格的标准启发：不会高估（最小单格代价=1）
    dx, dy = abs(a[0] - b[0]), abs(a[1] - b[1])
    return (dx + dy) + (math.sqrt(2) - 2) * min(dx, dy)

def astar(start, goal, h, w=1.0):
    frontier = [(0.0, start)]
    g = {start: 0.0}; came = {start: None}; expanded = 0
    while frontier:
        _, cur = heapq.heappop(frontier)
        expanded += 1
        if cur == goal: break
        for nxt, c in neighbors(cur):
            ng = g[cur] + c
            if nxt not in g or ng < g[nxt]:
                g[nxt] = ng; came[nxt] = cur
                heapq.heappush(frontier, (ng + w * h(nxt, goal), nxt))
    path, p = [], goal
    while p is not None: path.append(p); p = came[p]
    return path[::-1], g[goal], expanded

if __name__ == "__main__":
    for name, h, w in [("Dijkstra (h=0)", lambda a, b: 0, 1.0),
                       ("A*  octile", octile, 1.0),
                       ("加权A* w=2", octile, 2.0)]:
        path, cost, n = astar(START, GOAL, h, w)
        print(f"{name:16s} 路径代价={cost:6.2f}  弹出节点数={n}")
    path, _, _ = astar(START, GOAL, octile)
    rows = [list(r) for r in GRID]
    for r, c in path[1:-1]: rows[r][c] = '*'
    print("\n".join("".join(r) for r in rows))
