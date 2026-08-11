using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A* 寻路核心：在 NavGrid2D 上做 8 方向寻路。
/// 额外代价通过 costAt 委托注入（区域外空气高代价、玩家高代价等），网格保持通用。
/// 路径点输出为世界坐标折线，供 BT 移动节点逐点跟随。
/// </summary>
public static class AStarPathfinder
{
    private struct Node
    {
        public Vector2Int Cell;
        public float G;
        public float F;
    }

    /// <summary>
    /// 在网格上寻找 start→end 的路径（8 方向）。
    /// </summary>
    /// <param name="grid">网格</param>
    /// <param name="start">起点世界坐标</param>
    /// <param name="end">终点世界坐标</param>
    /// <param name="costAt">额外代价函数（世界坐标→额外代价，如空气/玩家惩罚）。可为 null</param>
    /// <param name="outPath">输出路径点（世界坐标，含起点不含终点？含终点）。调用方负责 Clear</param>
    /// <returns>是否找到路径</returns>
    public static bool FindPath(NavGrid2D grid, Vector2 start, Vector2 end,
        System.Func<Vector2, float> costAt, List<Vector2> outPath)
    {
        outPath.Clear();

        Vector2Int startCell = ClampToFreeCell(grid, start);
        Vector2Int endCell = ClampToFreeCell(grid, end);

        if (grid.IsBlocked(startCell) || grid.IsBlocked(endCell))
            return false;

        // 起点终点同格：直接给终点
        if (startCell == endCell)
        {
            outPath.Add(ClampToBounds(grid, end));
            return true;
        }

        int width = grid.Width;
        int height = grid.Height;

        float[,] gScore = new float[width, height];
        bool[,] closed = new bool[width, height];
        Vector2Int[,] cameFrom = new Vector2Int[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                gScore[x, y] = float.PositiveInfinity;

        // 8 方向：水平/垂直 1.0，对角 sqrt2
        Vector2Int[] dirs =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };
        float[] dirCosts = { 1f, 1f, 1f, 1f, 1.4142f, 1.4142f, 1.4142f, 1.4142f };

        List<Node> open = new List<Node>();
        gScore[startCell.x, startCell.y] = 0f;
        open.Add(new Node { Cell = startCell, G = 0f, F = Heuristic(startCell, endCell) });

        const int maxIterations = 20000; // 安全上限，防止意外死循环
        int iterations = 0;

        while (open.Count > 0 && iterations++ < maxIterations)
        {
            // 取 F 最小的节点
            int bestIdx = 0;
            for (int i = 1; i < open.Count; i++)
                if (open[i].F < open[bestIdx].F) bestIdx = i;

            Node current = open[bestIdx];
            open.RemoveAt(bestIdx);

            if (current.Cell == endCell)
            {
                ReconstructPath(grid, cameFrom, startCell, endCell, outPath);
                return true;
            }

            if (closed[current.Cell.x, current.Cell.y])
                continue;
            closed[current.Cell.x, current.Cell.y] = true;

            for (int d = 0; d < dirs.Length; d++)
            {
                Vector2Int n = current.Cell + dirs[d];
                if (grid.IsBlocked(n))
                    continue;

                // 对角线穿角检查：斜向移动时相邻两直角格都须可通行，防止穿墙角
                if (dirs[d].x != 0 && dirs[d].y != 0)
                {
                    if (grid.IsBlocked(current.Cell.x + dirs[d].x, current.Cell.y) ||
                        grid.IsBlocked(current.Cell.x, current.Cell.y + dirs[d].y))
                        continue;
                }

                if (closed[n.x, n.y])
                    continue;

                // 额外代价（空气/玩家惩罚），中心点采样
                float extra = 0f;
                if (costAt != null)
                    extra = Mathf.Max(0f, costAt(grid.CellToWorld(n.x, n.y)));

                float tentativeG = current.G + dirCosts[d] + extra;
                if (tentativeG >= gScore[n.x, n.y])
                    continue;

                gScore[n.x, n.y] = tentativeG;
                cameFrom[n.x, n.y] = current.Cell;
                open.Add(new Node { Cell = n, G = tentativeG, F = tentativeG + Heuristic(n, endCell) });
            }
        }

        // 未找到路径：回退为直线（尽力而为），由上层决定是否放弃
        outPath.Add(ClampToBounds(grid, end));
        return false;
    }

    /// <summary>曼哈顿距离启发。</summary>
    private static float Heuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return dx + dy; // 曼哈顿，可接受启发（不高估）
    }

    /// <summary>把世界坐标钳制到网格边界内。</summary>
    private static Vector2 ClampToBounds(NavGrid2D grid, Vector2 p)
    {
        Vector2 o = grid.Origin;
        float maxX = o.x + grid.Width * grid.CellSize;
        float maxY = o.y + grid.Height * grid.CellSize;
        return new Vector2(Mathf.Clamp(p.x, o.x, maxX), Mathf.Clamp(p.y, o.y, maxY));
    }

    /// <summary>把世界坐标吸附到最近的可通行格；全阻塞时返回原格钳制。</summary>
    private static Vector2Int ClampToFreeCell(NavGrid2D grid, Vector2 p)
    {
        Vector2Int cell = grid.WorldToCell(ClampToBounds(grid, p));
        if (!grid.IsBlocked(cell))
            return cell;

        // 从中心向外螺旋找最近可通行格
        for (int r = 1; r < 8; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    Vector2Int c = cell + new Vector2Int(dx, dy);
                    if (!grid.IsBlocked(c))
                        return c;
                }
            }
        }
        return cell;
    }

    private static void ReconstructPath(NavGrid2D grid, Vector2Int[,] cameFrom,
        Vector2Int start, Vector2Int end, List<Vector2> outPath)
    {
        List<Vector2> reversed = new List<Vector2>();
        Vector2Int cur = end;
        int guard = 0;
        while (cur != start && guard++ < 10000)
        {
            reversed.Add(grid.CellToWorld(cur.x, cur.y));
            cur = cameFrom[cur.x, cur.y];
        }
        for (int i = reversed.Count - 1; i >= 0; i--)
            outPath.Add(reversed[i]);
    }
}
