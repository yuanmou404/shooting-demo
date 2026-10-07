using System.Collections.Generic;
using UnityEngine;

namespace PixelArena
{
    /// <summary>
    /// 体素世界的导航网格（2.5D）。
    ///
    /// 做法：按 (x,z) 分列，把每列能站人的所有表面（地面 / 平台 / 屋顶）抽成节点，
    /// 所以它是**多层**的 —— 房子里站在地板上不会被当成站在屋顶上。
    /// 相邻节点之间只允许"抬升 1 格"（MoveAABB 的自动上台阶就是这个高度）
    /// 或"下落不超过 MaxDrop 格"（直接走下去摔一下），于是敌人不会再沿着墙
    /// 一路蹭过去，而是绕开它走能走通的路。
    ///
    /// 查询用 A*，并用 binary heap 做开放集；数组一次性分配、靠 stamp 做版本标记，
    /// 所以运行期不产生 GC。（不可重入：同一帧内串行调用没问题。）
    /// </summary>
    public class NavGrid
    {
        private const int MaxLevels = 4;        // 每列最多记几层可站立面
        private const int MaxRise = 1;          // 一步最多抬升多少格
        private const int MaxDrop = 3;          // 一步最多下落多少格
        private const int MaxExpansions = 3600; // 单次寻路最多扩展节点数（兜底，防止卡帧）

        private readonly VoxelWorld world;
        private readonly int sx, sz;
        private readonly int[] lvlCount;        // 每列实际有几层
        private readonly int[] lvlY;            // 每层的站立高度（脚底 y）
        private readonly bool[] dirty;          // 待重建的列
        private int pending;

        // ---- A* 暂存 ----
        private readonly float[] gScore;
        private readonly int[] cameFrom;
        private readonly int[] seenStamp;
        private readonly int[] closedStamp;
        private int stamp;
        private readonly int[] heapNode;
        private readonly float[] heapKey;
        private int heapCount;
        private readonly int[] backBuf = new int[512];   // 路径回溯用的复用缓冲

        public NavGrid(VoxelWorld w)
        {
            world = w;
            sx = w.SX;
            sz = w.SZ;
            int cols = sx * sz;
            int nodes = cols * MaxLevels;

            lvlCount = new int[cols];
            lvlY = new int[nodes];
            dirty = new bool[cols];

            gScore = new float[nodes];
            cameFrom = new int[nodes];
            seenStamp = new int[nodes];
            closedStamp = new int[nodes];
            // 堆里会存在同一个节点的旧版本（g 变小后被重复压入），容量留宽一点
            heapNode = new int[nodes * 2 + 16];
            heapKey = new float[nodes * 2 + 16];
            stamp = 0;

            InvalidateAll();
        }

        // ------------------------------------------------------------------ 失效与重建

        /// <summary>整张地图都变了（换图 / 生成新地图）。</summary>
        public void InvalidateAll()
        {
            for (int i = 0; i < dirty.Length; i++) dirty[i] = true;
            pending = dirty.Length;
        }

        /// <summary>某处方块被挖掉/放上时调用（连带四周，因为净空判定会受影响）。</summary>
        public void MarkAround(int x, int z)
        {
            Mark(x, z);
            Mark(x + 1, z); Mark(x - 1, z);
            Mark(x, z + 1); Mark(x, z - 1);
        }

        private void Mark(int x, int z)
        {
            if (x < 0 || z < 0 || x >= sx || z >= sz) return;
            int c = x + z * sx;
            if (dirty[c]) return;
            dirty[c] = true;
            pending++;
        }

        private void EnsureFresh()
        {
            if (pending <= 0) return;
            for (int c = 0; c < dirty.Length; c++)
            {
                if (!dirty[c]) continue;
                dirty[c] = false;
                pending--;
                RebuildColumn(c % sx, c / sx, c);
            }
        }

        /// <summary>
        /// 扫一列：从下往上找所有"脚下有实心 + 身体两格净空"的可站立面。
        /// 保留最低的 MaxLevels 层（大多数列只有地面 1 层，楼房才会出现多层）。
        /// </summary>
        private void RebuildColumn(int x, int z, int c)
        {
            int n = 0;
            int baseIdx = c * MaxLevels;
            int maxY = Mathf.Min(world.SY - 3, 44);
            for (int y = 1; y <= maxY && n < MaxLevels; y++)
            {
                if (!world.IsSolidAt(x, y - 1, z)) continue;      // 脚下要有支撑
                if (world.IsSolidAt(x, y, z)) continue;           // 身体所在格必须是空的
                if (world.IsSolidAt(x, y + 1, z)) continue;       // 头顶还要有一格（身高 1.8 ≈ 2 格）
                lvlY[baseIdx + n] = y;
                n++;
            }
            lvlCount[c] = n;
        }

        // ------------------------------------------------------------------ 节点工具

        private int NodeOf(int x, int z, int lvl) => (x + z * sx) * MaxLevels + lvl;

        private void Decode(int node, out int x, out int z, out int y)
        {
            int lvl = node % MaxLevels;
            int col = node / MaxLevels;
            z = col / sx;
            x = col - z * sx;
            y = lvlY[node];
        }

        private Vector3 NodeWorld(int node)
        {
            int x, z, y;
            Decode(node, out x, out z, out y);
            return new Vector3(x + 0.5f, y + 0.02f, z + 0.5f);
        }

        /// <summary>找离某个世界坐标最近的可用节点（同高度优先，向外扩最多 3 圈）。</summary>
        private int FindNode(Vector3 pos)
        {
            int cx = Mathf.FloorToInt(pos.x);
            int cz = Mathf.FloorToInt(pos.z);
            int best = -1;
            float bestScore = float.MaxValue;

            for (int r = 0; r <= 3; r++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;   // 只走当前这一圈的边框
                        int x = cx + dx, z = cz + dz;
                        if (x < 0 || z < 0 || x >= sx || z >= sz) continue;
                        int c = x + z * sx;
                        int n = lvlCount[c];
                        for (int l = 0; l < n; l++)
                        {
                            int node = NodeOf(x, z, l);
                            float dy = Mathf.Abs(lvlY[node] - pos.y);
                            if (dy > MaxDrop + 1.5f) continue;      // 差了三四层那就不算这个人站的地方
                            float score = Mathf.Abs(dx) + Mathf.Abs(dz) + dy * 0.75f;
                            if (score < bestScore) { bestScore = score; best = node; }
                        }
                    }
                }
                if (best >= 0 && r >= 1) break;   // 找到近的就不往外扩了
            }
            return best;
        }

        // ------------------------------------------------------------------ 寻路

        /// <summary>
        /// 找一条从 start 到 goal 的路（世界坐标）。
        /// 返回值：true = 正好通到终点附近；false = 走不到（地图不通），
        /// 但 outPath 里仍然会填上"能走得最近的那个点"的路径 —— 调用方可以直接用，
        /// 敌人会尽量靠近目标而不是原地发呆。
        /// </summary>
        public bool FindPath(Vector3 start, Vector3 goal, List<Vector3> outPath)
        {
            outPath.Clear();
            if (world == null) return false;
            EnsureFresh();

            int startNode = FindNode(start);
            int goalNode = FindNode(goal);
            if (startNode < 0 || goalNode < 0) return false;

            if (startNode == goalNode)
            {
                outPath.Add(NodeWorld(goalNode));
                return true;
            }

            int goalX, goalZ, goalY;
            Decode(goalNode, out goalX, out goalZ, out goalY);

            stamp++;
            heapCount = 0;
            seenStamp[startNode] = stamp;
            gScore[startNode] = 0f;
            cameFrom[startNode] = -1;
            Push(startNode, HeuristicOf(startNode, goalX, goalZ, goalY));

            int bestNode = startNode;
            float bestH = HeuristicOf(startNode, goalX, goalZ, goalY);
            bool reached = false;
            int expansions = 0;

            while (heapCount > 0 && expansions < MaxExpansions)
            {
                int cur = Pop();
                if (closedStamp[cur] == stamp) continue;
                closedStamp[cur] = stamp;
                expansions++;

                if (cur == goalNode) { reached = true; bestNode = cur; break; }

                float h = HeuristicOf(cur, goalX, goalZ, goalY);
                if (h < bestH) { bestH = h; bestNode = cur; }

                Expand(cur, goalX, goalZ, goalY);
            }

            BuildPath(bestNode, outPath);
            if (!reached && outPath.Count == 0) return false;
            return reached;
        }

        private float HeuristicOf(int node, int gx, int gz, int gy)
        {
            int x, z, y;
            Decode(node, out x, out z, out y);
            float dx = Mathf.Abs(x - gx), dz = Mathf.Abs(z - gz);
            float oct = dx > dz ? (dx - dz) + 1.4142f * dz : (dz - dx) + 1.4142f * dx;
            return oct + Mathf.Abs(y - gy) * 0.2f;
        }

        /// <summary>展开八邻域（对角要求两侧的正交格子也能踩，避免贴着墙角穿过去）。</summary>
        private void Expand(int cur, int gx, int gz, int gy)
        {
            int cxx, czz, cyy;
            Decode(cur, out cxx, out czz, out cyy);

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = cxx + dx, nz = czz + dz;
                    if (nx < 0 || nz < 0 || nx >= sx || nz >= sz) continue;

                    bool diag = dx != 0 && dz != 0;
                    if (diag)
                    {
                        // 侧边两格至少要有一个能落脚的高度，否则就是穿过墙角了
                        if (!SideClear(cxx + dx, czz, cyy)) continue;
                        if (!SideClear(cxx, czz + dz, cyy)) continue;
                    }

                    int c2 = nx + nz * sx;
                    float stepCost = diag ? 1.4142f : 1f;   // 对角本来就是走 1 格的 √2 倍，别再乘一遍

                    for (int l = 0; l < lvlCount[c2]; l++)
                    {
                        int nb = NodeOf(nx, nz, l);
                        if (closedStamp[nb] == stamp) continue;
                        int ny = lvlY[nb];
                        int dh = ny - cyy;
                        if (dh > MaxRise) continue;
                        if (-dh > MaxDrop) continue;

                        // 上台阶比走平地累一点，往下跳几乎白送（但别太夸张）
                        float extra = dh > 0 ? 0.5f * dh : (dh < 0 ? 0.12f * (-dh) : 0f);
                        float ng = gScore[cur] + stepCost + extra;

                        if (seenStamp[nb] == stamp && ng >= gScore[nb]) continue;
                        seenStamp[nb] = stamp;
                        gScore[nb] = ng;
                        cameFrom[nb] = cur;
                        Push(nb, ng + HeuristicOf(nb, gx, gz, gy));
                    }
                }
            }
        }

        /// <summary>该列是否存在一个与 curY 高度差在允许范围内、可以落脚的层。</summary>
        private bool SideClear(int x, int z, int curY)
        {
            if (x < 0 || z < 0 || x >= sx || z >= sz) return false;
            int c = x + z * sx;
            for (int l = 0; l < lvlCount[c]; l++)
            {
                int dh = lvlY[NodeOf(x, z, l)] - curY;
                if (dh <= MaxRise && -dh <= MaxDrop) return true;
            }
            return false;
        }

        /// <summary>回溯路径，并做一次"把共线的中间点丢掉"的简化（保留高度变化点）。</summary>
        private void BuildPath(int endNode, List<Vector3> outPath)
        {
            if (endNode < 0) return;

            // 从终点回溯到起点（存进复用缓冲，避免每次寻路都 new 一个 List）
            int n = 0;
            int cur = endNode;
            int guard = 0;
            while (cur >= 0 && guard++ < 4096 && n < backBuf.Length)
            {
                backBuf[n++] = cur;
                if (cameFrom[cur] == -1) break;   // 到起点了
                cur = cameFrom[cur];
            }
            if (n == 0) return;

            // 反序 → 起点到终点，转成世界坐标并丢掉共线的冗余点
            Vector3 prevDir = Vector3.zero;
            bool hasDir = false;
            float prevY = float.MinValue;
            for (int i = n - 1; i >= 0; i--)
            {
                Vector3 p = NodeWorld(backBuf[i]);
                bool last = (i == 0);
                if (!last)
                {
                    Vector3 nextP = NodeWorld(backBuf[i - 1]);
                    Vector3 d = nextP - p;
                    d.y = 0f;
                    bool sameHeight = Mathf.Abs(nextP.y - p.y) < 0.01f;
                    if (d.sqrMagnitude > 1e-6f)
                    {
                        Vector3 nd = d.normalized;
                        // 下一段方向和上一段几乎一致、又没有爬升 → 中间这个点可以不要
                        if (hasDir && sameHeight && Mathf.Abs(nextP.y - prevY) < 0.01f && Vector3.Dot(nd, prevDir) > 0.985f)
                        {
                            prevY = p.y;
                            continue;
                        }
                        prevDir = nd;
                        hasDir = true;
                    }
                }
                prevY = p.y;
                outPath.Add(p);
            }
        }

        // ------------------------------------------------------------------ 二叉堆

        private void Push(int node, float key)
        {
            int i = ++heapCount;
            if (i >= heapNode.Length)
            {
                heapCount--;
                return;
            }
            heapNode[i] = node;
            heapKey[i] = key;
            while (i > 1)
            {
                int p = i >> 1;
                if (heapKey[p] <= heapKey[i]) break;
                Swap(p, i);
                i = p;
            }
        }

        private int Pop()
        {
            int top = heapNode[1];
            heapNode[1] = heapNode[heapCount];
            heapKey[1] = heapKey[heapCount];
            heapCount--;
            int i = 1;
            while (true)
            {
                int l = i << 1, r = l + 1;
                if (l > heapCount) break;
                int m = (r <= heapCount && heapKey[r] < heapKey[l]) ? r : l;
                if (heapKey[i] <= heapKey[m]) break;
                Swap(i, m);
                i = m;
            }
            return top;
        }

        private void Swap(int a, int b)
        {
            int tn = heapNode[a]; heapNode[a] = heapNode[b]; heapNode[b] = tn;
            float tk = heapKey[a]; heapKey[a] = heapKey[b]; heapKey[b] = tk;
        }
    }
}
