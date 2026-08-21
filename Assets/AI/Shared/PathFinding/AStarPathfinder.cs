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
        public int Index;   // 扁平化格子索引（y * width + x）
        public float G;
        public float F;
    }

    // 池化扁平数组：避免每次寻路 new 三块 width×height 大数组（蜜蜂等每 0.5s 重算一次路径，GC 压力大）。
    // Unity 主线程顺序调用、无并发，静态复用安全；网格变大时自动扩容。
    private static float[] _gScore;
    private static bool[] _closed;
    private static int[] _cameFrom;

    // 8 方向与代价：静态缓存，避免每次寻路重复分配小数组
    private static readonly Vector2Int[] _dirs =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
    };
    private static readonly float[] _dirCosts = { 1f, 1f, 1f, 1f, 1.4142f, 1.4142f, 1.4142f, 1.4142f };

    // 复用开放列表（二叉堆）与路径回溯列表，避免每次寻路分配 List 造成 GC。
    private static readonly List<Node> _open = new List<Node>();
    private static readonly List<Vector2> _reversed = new List<Vector2>();

    /// <summary>
    /// 在网格上寻找 start→end 的路径（8 方向）。
    /// </summary>
    /// <param name="grid">网格</param>
    /// <param name="start">起点世界坐标</param>
    /// <param name="end">终点世界坐标</param>
    /// <param name="costAt">额外代价函数（世界坐标→额外代价，如空气/玩家惩罚）。可为 null</param>
    /// <param name="outPath">输出路径点（世界坐标）。调用方负责 Clear</param>
    /// <param name="ignoreGroundSupport">飞行单位寻路用：忽略悬空格，只避物理障碍（如蜜蜂）</param>
    /// <returns>是否找到路径</returns>
    public static bool FindPath(NavGrid2D grid, Vector2 start, Vector2 end,
        System.Func<Vector2, float> costAt, List<Vector2> outPath,
        bool ignoreGroundSupport = false)
    {
        outPath.Clear();

        Vector2Int startCell = ClampToFreeCell(grid, start, ignoreGroundSupport);
        Vector2Int endCell = ClampToFreeCell(grid, end, ignoreGroundSupport);

        if (grid.IsBlockedFor(startCell.x, startCell.y, ignoreGroundSupport) ||
            grid.IsBlockedFor(endCell.x, endCell.y, ignoreGroundSupport))
            return false;

        // 起点终点同格：直接给终点
        if (startCell == endCell)
        {
            outPath.Add(ClampToBounds(grid, end));
            return true;
        }

        int width = grid.Width;
        int height = grid.Height;
        int cellCount = width * height;

        EnsureArrays(cellCount);
        for (int i = 0; i < cellCount; i++)
            _gScore[i] = float.PositiveInfinity;
        System.Array.Clear(_closed, 0, cellCount);

        int startIdx = startCell.y * width + startCell.x;
        int endIdx = endCell.y * width + endCell.x;

        _open.Clear();
        _gScore[startIdx] = 0f;
        HeapPush(new Node { Index = startIdx, G = 0f, F = Heuristic(startCell, endCell) });

        const int maxIterations = 20000; // 安全上限，防止意外死循环
        int iterations = 0;

        while (_open.Count > 0 && iterations++ < maxIterations)
        {
            Node current = HeapPop();

            if (current.Index == endIdx)
            {
                ReconstructPath(grid, startIdx, endIdx, width, outPath);
                return true;
            }

            if (_closed[current.Index])
                continue;
            _closed[current.Index] = true;

            int cx = current.Index % width;
            int cy = current.Index / width;

            for (int d = 0; d < _dirs.Length; d++)
            {
                int nx = cx + _dirs[d].x;
                int ny = cy + _dirs[d].y;
                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                    continue;
                if (grid.IsBlockedFor(nx, ny, ignoreGroundSupport))
                    continue;

                // 对角线穿角检查：斜向移动时相邻两直角格都须可通行，防止穿墙角
                if (_dirs[d].x != 0 && _dirs[d].y != 0)
                {
                    if (grid.IsBlockedFor(cx + _dirs[d].x, cy, ignoreGroundSupport) ||
                        grid.IsBlockedFor(cx, cy + _dirs[d].y, ignoreGroundSupport))
                        continue;
                }

                int nIdx = ny * width + nx;
                if (_closed[nIdx])
                    continue;

                // 额外代价（空气/玩家惩罚），中心点采样
                float extra = 0f;
                if (costAt != null)
                    extra = Mathf.Max(0f, costAt(grid.CellToWorld(nx, ny)));

                float tentativeG = current.G + _dirCosts[d] + extra;
                if (tentativeG >= _gScore[nIdx])
                    continue;

                _gScore[nIdx] = tentativeG;
                _cameFrom[nIdx] = current.Index;
                HeapPush(new Node { Index = nIdx, G = tentativeG, F = tentativeG + Heuristic(new Vector2Int(nx, ny), endCell) });
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
    private static Vector2Int ClampToFreeCell(NavGrid2D grid, Vector2 p, bool ignoreGroundSupport)
    {
        Vector2Int cell = grid.WorldToCell(ClampToBounds(grid, p));
        if (!grid.IsBlockedFor(cell.x, cell.y, ignoreGroundSupport))
            return cell;

        // 从中心向外螺旋找最近可通行格（半径放宽到 24，覆盖起终点贴近大障碍的场景，
        // 如蜜蜂在 Square/地形上方悬空区出生、附近全是障碍时仍能吸附到网格内的自由格）
        for (int r = 1; r < 24; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    Vector2Int c = cell + new Vector2Int(dx, dy);
                    if (!grid.IsBlockedFor(c.x, c.y, ignoreGroundSupport))
                        return c;
                }
            }
        }
        return cell;
    }

    private static void ReconstructPath(NavGrid2D grid, int startIdx, int endIdx, int width, List<Vector2> outPath)
    {
        _reversed.Clear();
        int cur = endIdx;
        int guard = 0;
        while (cur != startIdx && guard++ < 10000)
        {
            int x = cur % width;
            int y = cur / width;
            _reversed.Add(grid.CellToWorld(x, y));
            cur = _cameFrom[cur];
        }
        for (int i = _reversed.Count - 1; i >= 0; i--)
            outPath.Add(_reversed[i]);
    }

    /// <summary>确保池化数组容量足够当前网格大小（网格变大时自动扩容，避免每次寻路重复分配）。</summary>
    private static void EnsureArrays(int cellCount)
    {
        if (_gScore == null || _gScore.Length < cellCount)
        {
            _gScore = new float[cellCount];
            _closed = new bool[cellCount];
            _cameFrom = new int[cellCount];
        }
    }

    /// <summary>小顶堆入堆（按 F 值升序）。</summary>
    private static void HeapPush(Node node)
    {
        _open.Add(node);
        int i = _open.Count - 1;
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (_open[parent].F <= _open[i].F) break;
            Node tmp = _open[parent];
            _open[parent] = _open[i];
            _open[i] = tmp;
            i = parent;
        }
    }

    /// <summary>小顶堆出堆：弹出 F 最小的节点。</summary>
    private static Node HeapPop()
    {
        Node top = _open[0];
        int last = _open.Count - 1;
        _open[0] = _open[last];
        _open.RemoveAt(last);

        int i = 0;
        int n = _open.Count;
        while (true)
        {
            int left = i * 2 + 1;
            int right = left + 1;
            int smallest = i;
            if (left < n && _open[left].F < _open[smallest].F) smallest = left;
            if (right < n && _open[right].F < _open[smallest].F) smallest = right;
            if (smallest == i) break;
            Node tmp = _open[smallest];
            _open[smallest] = _open[i];
            _open[i] = tmp;
            i = smallest;
        }
        return top;
    }
}
