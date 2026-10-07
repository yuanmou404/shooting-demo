using System;
using UnityEngine;

namespace PixelArena
{
    /// <summary>
    /// 体素世界：固定大小的竞技场地图数据 + 射线检测 + AABB 碰撞解算（完全不依赖 Unity 物理）。
    /// 坐标：方块 (x,y,z) 占据空间 [x, x+1) × [y, y+1) × [z, z+1)。
    /// </summary>
    public class VoxelWorld
    {
        public readonly int SX, SY, SZ;
        private readonly byte[] blocks;
        private readonly float[] damage;

        /// <summary>
        /// 导航网格（敌人寻路用）。跟随世界数据增量更新：
        /// 方块被破坏 / 放置 / 重新生成地图时调用 <see cref="MarkNavDirty"/> 或 <see cref="InvalidateNav"/>。
        /// </summary>
        public NavGrid Nav { get; private set; }

        public VoxelWorld(int sx, int sy, int sz)
        {
            SX = sx; SY = sy; SZ = sz;
            blocks = new byte[sx * sy * sz];
            damage = new float[sx * sy * sz];
            Nav = new NavGrid(this);
        }

        /// <summary>某处方块变了（连带四周，因为净空判定会受影响）。</summary>
        public void MarkNavDirty(int x, int z)
        {
            if (Nav != null) Nav.MarkAround(x, z);
        }

        /// <summary>整张地图都换了。</summary>
        private void InvalidateNav()
        {
            if (Nav != null) Nav.InvalidateAll();
        }

        private int Idx(int x, int y, int z) => x + z * SX + y * SX * SZ;

        public bool InBounds(int x, int y, int z) => x >= 0 && y >= 0 && z >= 0 && x < SX && y < SY && z < SZ;

        public byte Get(int x, int y, int z)
        {
            if (x < 0 || z < 0 || x >= SX || z >= SZ) return (byte)BlockId.Stone; // 地图外围视为实心墙
            if (y < 0) return (byte)BlockId.Stone;
            if (y >= SY) return (byte)BlockId.Air;
            return blocks[Idx(x, y, z)];
        }

        public void Set(int x, int y, int z, byte id)
        {
            if (!InBounds(x, y, z)) return;
            int i = Idx(x, y, z);
            blocks[i] = id;
            damage[i] = 0f;
        }

        public bool IsSolidAt(int x, int y, int z) => BlockDef.IsSolid(Get(x, y, z));
        public bool IsOpaqueAt(int x, int y, int z) => BlockDef.IsOpaque(Get(x, y, z));

        /// <summary>对方块造成伤害，返回 true 表示已破坏。</summary>
        public bool Damage(int x, int y, int z, float amount, out byte brokenId)
        {
            brokenId = 0;
            if (!InBounds(x, y, z)) return false;
            int i = Idx(x, y, z);
            byte id = blocks[i];
            if (id == (byte)BlockId.Air) return false;
            damage[i] += amount;
            if (damage[i] >= BlockDef.Hardness(id))
            {
                blocks[i] = (byte)BlockId.Air;
                damage[i] = 0f;
                brokenId = id;
                return true;
            }
            return false;
        }

        public float DamageRatio(int x, int y, int z)
        {
            if (!InBounds(x, y, z)) return 0f;
            int i = Idx(x, y, z);
            byte id = blocks[i];
            if (id == (byte)BlockId.Air) return 0f;
            return Mathf.Clamp01(damage[i] / BlockDef.Hardness(id));
        }

        // ------------------------------------------------------------ 射线

        /// <summary>
        /// 体素 DDA 射线步进。命中实心方块返回 true。
        /// </summary>
        public bool RaycastBlocks(Vector3 origin, Vector3 dir, float maxDist, out Vector3 point, out Vector3Int cell, out Vector3Int normal)
        {
            point = origin; cell = Vector3Int.zero; normal = Vector3Int.zero;
            dir.Normalize();

            int x = Mathf.FloorToInt(origin.x);
            int y = Mathf.FloorToInt(origin.y);
            int z = Mathf.FloorToInt(origin.z);

            int stepX = dir.x > 0 ? 1 : (dir.x < 0 ? -1 : 0);
            int stepY = dir.y > 0 ? 1 : (dir.y < 0 ? -1 : 0);
            int stepZ = dir.z > 0 ? 1 : (dir.z < 0 ? -1 : 0);

            float tDeltaX = stepX != 0 ? Mathf.Abs(1f / dir.x) : float.MaxValue;
            float tDeltaY = stepY != 0 ? Mathf.Abs(1f / dir.y) : float.MaxValue;
            float tDeltaZ = stepZ != 0 ? Mathf.Abs(1f / dir.z) : float.MaxValue;

            float tMaxX = stepX > 0 ? (x + 1 - origin.x) * tDeltaX : (stepX < 0 ? (origin.x - x) * tDeltaX : float.MaxValue);
            float tMaxY = stepY > 0 ? (y + 1 - origin.y) * tDeltaY : (stepY < 0 ? (origin.y - y) * tDeltaY : float.MaxValue);
            float tMaxZ = stepZ > 0 ? (z + 1 - origin.z) * tDeltaZ : (stepZ < 0 ? (origin.z - z) * tDeltaZ : float.MaxValue);

            float t = 0f;
            Vector3Int n = Vector3Int.zero;
            int guard = 0;
            while (guard++ < 1024)
            {
                if (IsSolidAt(x, y, z))
                {
                    cell = new Vector3Int(x, y, z);
                    normal = n;
                    point = origin + dir * t;
                    return true;
                }
                if (tMaxX < tMaxY && tMaxX < tMaxZ)
                {
                    x += stepX; t = tMaxX; tMaxX += tDeltaX; n = new Vector3Int(-stepX, 0, 0);
                }
                else if (tMaxY < tMaxZ)
                {
                    y += stepY; t = tMaxY; tMaxY += tDeltaY; n = new Vector3Int(0, -stepY, 0);
                }
                else
                {
                    z += stepZ; t = tMaxZ; tMaxZ += tDeltaZ; n = new Vector3Int(0, 0, -stepZ);
                }
                if (t > maxDist) return false;
                if (y < -1 || y > SY + 1) return false;
            }
            return false;
        }

        /// <summary>两点之间是否被方块挡住（敌人视线检测）。</summary>
        public bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            if (dist < 0.001f) return true;
            Vector3Int c, n; Vector3 p;
            if (!RaycastBlocks(from, dir, dist, out p, out c, out n)) return true;
            return Vector3.Distance(from, p) >= dist - 0.35f;
        }

        // ------------------------------------------------- AABB 碰撞（pos = 脚底中心）

        /// <summary>AABB 是否与实心方块重叠。</summary>
        public bool Overlaps(float px, float py, float pz, float halfXZ, float height)
        {
            int x0 = Mathf.FloorToInt(px - halfXZ);
            int x1 = Mathf.FloorToInt(px + halfXZ - 1e-4f);
            int z0 = Mathf.FloorToInt(pz - halfXZ);
            int z1 = Mathf.FloorToInt(pz + halfXZ - 1e-4f);
            int y0 = Mathf.FloorToInt(py + 1e-4f);
            int y1 = Mathf.FloorToInt(py + height - 1e-4f);
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                        if (IsSolidAt(x, y, z)) return true;
            return false;
        }

        /// <summary>
        /// 带碰撞的移动解算（分轴 + 自动上一格台阶）。
        /// </summary>
        /// <summary>
        /// 从 pos 沿 horiz 方向往前探一格：脚下这块障碍有多高（连续实心方块数）。
        /// 0 = 前面没东西，1 = 一格台阶（能跨上去），2 及以上 = 高墙（只能绕）。
        /// </summary>
        public int WallHeightAhead(Vector3 pos, Vector3 horiz, float halfXZ)
        {
            Vector3 flat = new Vector3(horiz.x, 0f, horiz.z);
            if (flat.sqrMagnitude < 1e-6f) return 0;
            flat.Normalize();
            float reach = halfXZ + 0.30f;
            int ax = Mathf.FloorToInt(pos.x + flat.x * reach);
            int az = Mathf.FloorToInt(pos.z + flat.z * reach);
            int fy = Mathf.FloorToInt(pos.y + 0.05f);
            int h = 0;
            for (int k = 0; k < 5; k++)
            {
                if (!IsSolidAt(ax, fy + k, az)) break;
                h++;
            }
            return h;
        }

        /// <param name="allowStep">false = 完全不允许上台阶（坦克这种重型单位：撞墙就停，绝不爬）</param>
        public Vector3 MoveAABB(Vector3 pos, float halfXZ, float height, Vector3 delta, out bool grounded, out bool hitWall,
            bool allowStep = true)
        {
            grounded = false; hitWall = false;
            const float eps = 0.001f;
            float dx0 = delta.x, dz0 = delta.z;

            // 起始是否站在地面上：只有在地面上才允许"自动上一格台阶"。
            // 否则跳跃时会先跳 h 格、再在空中触发台阶抬升，循环下去就能爬上任意高的墙。
            bool wasGrounded = Overlaps(pos.x, pos.y - 0.06f, pos.z, halfXZ, height);

            // ---- Y 轴
            float ny = pos.y + delta.y;
            if (Overlaps(pos.x, ny, pos.z, halfXZ, height))
            {
                if (delta.y <= 0f)
                {
                    ny = Mathf.Floor(ny) + 1f; // 落到方块顶面
                    grounded = true;
                }
                else
                {
                    ny = Mathf.Floor(ny + height) - height - eps; // 头顶撞到方块底面
                }
                delta = new Vector3(delta.x, 0f, delta.z);
            }
            pos.y = ny;

            // ---- X 轴
            float nx = pos.x + delta.x;
            if (Overlaps(nx, pos.y, pos.z, halfXZ, height))
            {
                if (delta.x > 0f) nx = Mathf.Floor(nx + halfXZ) - halfXZ - eps;
                else if (delta.x < 0f) nx = Mathf.Floor(nx - halfXZ) + 1f + halfXZ + eps;
                hitWall = true;
                delta = new Vector3(0f, delta.y, delta.z);
            }
            pos.x = nx;

            // ---- Z 轴
            float nz = pos.z + delta.z;
            if (Overlaps(pos.x, pos.y, nz, halfXZ, height))
            {
                if (delta.z > 0f) nz = Mathf.Floor(nz + halfXZ) - halfXZ - eps;
                else if (delta.z < 0f) nz = Mathf.Floor(nz - halfXZ) + 1f + halfXZ + eps;
                hitWall = true;
                delta = new Vector3(delta.x, delta.y, 0f);
            }
            pos.z = nz;

            // ---- 自动上 1 格台阶（用未被清零的原始水平位移）
            //
            // 以前这里只判断"抬高一格后身体放不放得下"就无条件把 y 抬上去，
            // 结果贴着 2 格以上的墙时会发生：身体悬空被抬高 1 格 → 下一帧脚下没有支撑
            // → 判定为离地 → 掉回地面 → 再抬 → …… 看起来就是"对着高墙原地反复起跳"。
            // 现在必须同时满足：① 抬高后身体放得下 ② 横向真的前进了 ③ 新位置脚下有支撑。
            Vector3 horiz = new Vector3(dx0, 0f, dz0);
            if (allowStep && hitWall && wasGrounded && delta.y <= 0f && horiz.sqrMagnitude > 1e-6f)
            {
                float stepY = Mathf.Floor(pos.y + eps) + 1f + eps;
                if (!Overlaps(pos.x, stepY, pos.z, halfXZ, height))
                {
                    float tx = pos.x, tz = pos.z;
                    float wantX = pos.x + horiz.x;
                    if (!Overlaps(wantX, stepY, pos.z, halfXZ, height)) tx = wantX;
                    float wantZ = pos.z + horiz.z;
                    if (!Overlaps(tx, stepY, wantZ, halfXZ, height)) tz = wantZ;

                    bool moved = Mathf.Abs(tx - pos.x) > 1e-4f || Mathf.Abs(tz - pos.z) > 1e-4f;
                    bool supported = Overlaps(tx, stepY - 0.08f, tz, halfXZ, height);   // 脚下得踩得到东西
                    if (moved && supported)
                    {
                        pos.x = tx; pos.z = tz; pos.y = stepY;
                        hitWall = false;
                        grounded = true;
                    }
                }
            }

            // 落地检测（脚下一丁点）
            if (!grounded && Overlaps(pos.x, pos.y - 0.06f, pos.z, halfXZ, height))
                grounded = true;

            return pos;
        }

        /// <summary>某列的最高实心方块顶面高度（可站立高度）。</summary>
        public int GroundHeight(int x, int z, int maxY)
        {
            for (int y = Mathf.Min(maxY, SY - 1); y >= 0; y--)
                if (IsSolidAt(x, y, z)) return y + 1;
            return 0;
        }

        // ------------------------------------------------------------ 地图主题

        /// <summary>地图主题。每次开局随机抽一张，地形 / 建筑 / 配色都不同。</summary>
        public enum MapTheme { Grassland, DesertVillage, Rainforest, Office, Dust2 }

        /// <summary>当前主题。</summary>
        public MapTheme Theme = MapTheme.Grassland;
        /// <summary>地图名（HUD 显示）。</summary>
        public string MapName = "草原林场";
        /// <summary>主题配色：换图时天空 / 雾 / 环境光 / 阳光一起换。</summary>
        public Color SkyColor = new Color(0.62f, 0.76f, 0.92f);
        public Color FogColor = new Color(0.66f, 0.78f, 0.92f);
        public Color AmbientColor = new Color(0.42f, 0.45f, 0.52f);
        public Color SunColor = new Color(1.05f, 1.00f, 0.92f);

        /// <summary>玩家出生点（生成结束时确定，避开建筑与树）。</summary>
        public Vector3 SpawnPoint = Vector3.zero;

        /// <summary>地面顶层方块的 y（站在 y = GroundY + 1）。</summary>
        public const int GroundY = 4;

        public static int ThemeCount => 5;

        public static string ThemeLabel(MapTheme t)
        {
            switch (t)
            {
                case MapTheme.DesertVillage: return "沙漠村庄 DESERT VILLAGE";
                case MapTheme.Rainforest: return "热带雨林 RAINFOREST";
                case MapTheme.Office: return "办公楼 OFFICE BLOCK";
                case MapTheme.Dust2: return "沙2 DUST II";
                default: return "草原林场 GRASSLAND";
            }
        }

        // ------------------------------------------------------------ 生成入口

        /// <summary>按 seed 随机抽主题（每局都换图）。</summary>
        public void GenerateArena(int seed)
        {
            GenerateArena(seed, (MapTheme)new System.Random(seed).Next(0, ThemeCount));
        }

        public void GenerateArena(int seed, MapTheme theme)
        {
            var rnd = new System.Random(seed);
            Theme = theme;
            MapName = ThemeLabel(theme);
            SpawnPoint = Vector3.zero;
            InvalidateNav();
            for (int i = 0; i < blocks.Length; i++) { blocks[i] = (byte)BlockId.Air; damage[i] = 0f; }

            try
            {
                switch (theme)
                {
                    case MapTheme.DesertVillage: GenDesertVillage(rnd); break;
                    case MapTheme.Rainforest: GenRainforest(rnd); break;
                    case MapTheme.Office: GenOffice(rnd); break;
                    case MapTheme.Dust2: GenDust2(rnd); break;
                    default: GenGrassland(rnd); break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[PixelArena] 地图「" + MapName + "」生成失败，回退到草原林场：" + e);
                for (int i = 0; i < blocks.Length; i++) { blocks[i] = (byte)BlockId.Air; damage[i] = 0f; }
                Theme = MapTheme.Grassland; MapName = ThemeLabel(Theme);
                GenGrassland(rnd);
            }

            // 同一主题也要有点随机：色温 / 明暗轻微浮动，每张图看上去都不一样
            float mul = 0.94f + (float)rnd.NextDouble() * 0.12f;
            float warm = (float)rnd.NextDouble();
            SkyColor = new Color(Mathf.Clamp01(SkyColor.r * mul * (1f + warm * 0.05f)),
                                 Mathf.Clamp01(SkyColor.g * mul),
                                 Mathf.Clamp01(SkyColor.b * mul * (1f - warm * 0.06f)));
            FogColor = new Color(Mathf.Clamp01(FogColor.r * mul), Mathf.Clamp01(FogColor.g * mul),
                                 Mathf.Clamp01(FogColor.b * mul));
            AmbientColor *= 0.92f + (float)rnd.NextDouble() * 0.16f;
            SunColor *= 0.96f + (float)rnd.NextDouble() * 0.10f;

            if (SpawnPoint.y <= 0.01f) SpawnPoint = FallbackSpawn();
            // 地形全变了：导航网格重算一遍（写在最后，保证所有方块都已经写完）
            InvalidateNav();
        }

        // ------------------------------------------------------------ 通用填充

        private void Fill(int x0, int y0, int z0, int x1, int y1, int z1, byte id)
        {
            int ax = Mathf.Min(x0, x1), bx = Mathf.Max(x0, x1);
            int ay = Mathf.Min(y0, y1), by = Mathf.Max(y0, y1);
            int az = Mathf.Min(z0, z1), bz = Mathf.Max(z0, z1);
            for (int y = ay; y <= by; y++)
                for (int z = az; z <= bz; z++)
                    for (int x = ax; x <= bx; x++)
                        SetSafe(x, y, z, id);
        }

        /// <summary>整平一块地：底下实体、地表 top、上方清 5 格。</summary>
        private void Flatten(int x0, int z0, int x1, int z1, byte top, byte sub)
        {
            Fill(x0, 0, z0, x1, GroundY - 1, z1, sub);
            Fill(x0, GroundY, z0, x1, GroundY, z1, top);
            Fill(x0, GroundY + 1, z0, x1, GroundY + 5, z1, (byte)BlockId.Air);
        }

        private void Flatten(int x0, int z0, int x1, int z1, byte top)
        {
            Flatten(x0, z0, x1, z1, top, (byte)BlockId.Stone);
        }

        /// <summary>外围围墙（两格厚）。</summary>
        private void Perimeter(int topY, byte id)
        {
            for (int y = GroundY; y < topY; y++)
                for (int i = 0; i < SX; i++)
                    for (int t = 0; t < 2; t++)
                    {
                        SetSafe(i, y, t, id);
                        SetSafe(i, y, SZ - 1 - t, id);
                        SetSafe(t, y, i, id);
                        SetSafe(SX - 1 - t, y, i, id);
                    }
        }

        /// <summary>带玻璃窗带的外墙（办公楼）。</summary>
        private void PerimeterWindows(int topY, byte wall, byte glass)
        {
            for (int y = GroundY; y < topY; y++)
            {
                int band = (y - GroundY) % 5;
                byte id = (band == 1 || band == 2) ? glass : wall;
                for (int i = 0; i < SX; i++)
                    for (int t = 0; t < 2; t++)
                    {
                        SetSafe(i, y, t, id);
                        SetSafe(i, y, SZ - 1 - t, id);
                        SetSafe(t, y, i, id);
                        SetSafe(SX - 1 - t, y, i, id);
                    }
            }
        }

        private void BaseGround(byte top, byte patch, float patchChance, System.Random rnd)
        {
            Fill(0, 0, 0, SX - 1, GroundY - 1, SZ - 1, (byte)BlockId.Stone);
            Fill(0, GroundY, 0, SX - 1, GroundY, SZ - 1, top);
            if (patchChance <= 0f) return;
            for (int z = 2; z < SZ - 2; z++)
                for (int x = 2; x < SX - 2; x++)
                    if (rnd.NextDouble() < patchChance) SetSafe(x, GroundY, z, patch);
        }

        /// <summary>清出一块 6×6 的空地当出生点。</summary>
        private Vector3 MakeSpawn(int x0, int z0, byte top)
        {
            const int w = 6;
            Flatten(x0, z0, x0 + w - 1, z0 + w - 1, top);
            return new Vector3(x0 + w * 0.5f, GroundY + 1f, z0 + w * 0.5f);
        }

        private Vector3 FallbackSpawn()
        {
            for (int z = 8; z < SZ - 10; z += 2)
                for (int x = 8; x < SX - 10; x += 2)
                {
                    int y = GroundHeight(x, z, SY - 1);
                    if (y < 1 || y > 10) continue;
                    if (Overlaps(x + 0.5f, y + 0.05f, z + 0.5f, 0.40f, 1.8f)) continue;
                    return new Vector3(x + 0.5f, y + 0.02f, z + 0.5f);
                }
            return new Vector3(SX * 0.5f, GroundY + 1f, SZ * 0.5f);
        }

        private static int ClampI(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

        // ------------------------------------------------------------ 草原林场

        private void GenGrassland(System.Random rnd)
        {
            SkyColor = new Color(0.62f, 0.76f, 0.92f);
            FogColor = new Color(0.66f, 0.78f, 0.92f);
            AmbientColor = new Color(0.42f, 0.45f, 0.52f);
            SunColor = new Color(1.05f, 1.00f, 0.92f);

            BaseGround((byte)BlockId.Grass, (byte)BlockId.Sand, 0.06f, rnd);
            // 十字水泥路
            for (int i = 2; i < SX - 2; i++)
                for (int w = -1; w <= 1; w++)
                {
                    SetSafe(i, GroundY, SZ / 2 + w, (byte)BlockId.Concrete);
                    SetSafe(SX / 2 + w, GroundY, i, (byte)BlockId.Concrete);
                }
            Perimeter(14, (byte)BlockId.Stone);

            int cx = SX / 2, cz = SZ / 2;

            // 四角砖塔（高度随机）
            BuildTower(6, GroundY, 6, 8, rnd.Next(9, 13), rnd);
            BuildTower(SX - 15, GroundY, 6, 8, rnd.Next(9, 13), rnd);
            BuildTower(6, GroundY, SZ - 15, 8, rnd.Next(9, 13), rnd);
            BuildTower(SX - 15, GroundY, SZ - 15, 8, rnd.Next(9, 13), rnd);

            // 中央大树 + 树下掩体
            BigTree(cx, GroundY + 1, cz, rnd.Next(10, 13), rnd);
            for (int k = 0; k < 4; k++)
                CratePile(cx + (k % 2 == 0 ? -7 : 6), GroundY + 1, cz + (k < 2 ? -7 : 6), rnd.Next(2, 4), rnd);

            // 散布掩体 / 岩石堆
            int covers = rnd.Next(14, 22);
            for (int k = 0; k < covers; k++)
            {
                int x = rnd.Next(6, SX - 10), z = rnd.Next(6, SZ - 10);
                if (Mathf.Abs(x - cx) < 11 && Mathf.Abs(z - cz) < 11) continue;
                int type = rnd.Next(0, 4);
                if (type == 0) Wall(x, GroundY + 1, z, rnd.Next(3, 7), rnd.Next(2, 4), (byte)BlockId.Brick);
                else if (type == 1) CratePile(x, GroundY + 1, z, rnd.Next(1, 3), rnd);
                else if (type == 2) Wall(x, GroundY + 1, z, rnd.Next(2, 5), rnd.Next(3, 5), (byte)BlockId.Metal);
                else StoneMound(x, z, rnd.Next(2, 4), rnd);
            }
            int trees = rnd.Next(10, 18);
            for (int k = 0; k < trees; k++)
            {
                int x = rnd.Next(5, SX - 5), z = rnd.Next(5, SZ - 5);
                if (Mathf.Abs(x - cx) < 11 && Mathf.Abs(z - cz) < 11) continue;
                Tree(x, GroundY + 1, z, rnd.Next(4, 7));
            }

            // 出生点最后再清：否则树 / 掩体会直接长在出生点上，把玩家卡在方块里
            SpawnPoint = MakeSpawn(cx - 12, cz - 12, (byte)BlockId.Grass);
        }

        private void StoneMound(int x, int z, int r, System.Random rnd)
        {
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d > r) continue;
                    int h = d < r * 0.5f ? 3 : (d < r ? 2 : 1);
                    if (rnd.NextDouble() < 0.15f) continue;
                    for (int y = 1; y <= h; y++) SetSafe(x + dx, GroundY + y, z + dz, (byte)BlockId.Stone);
                }
        }

        // ------------------------------------------------------------ 沙漠村庄

        private void GenDesertVillage(System.Random rnd)
        {
            SkyColor = new Color(0.88f, 0.80f, 0.60f);
            FogColor = new Color(0.90f, 0.82f, 0.64f);
            AmbientColor = new Color(0.54f, 0.49f, 0.40f);
            SunColor = new Color(1.18f, 1.06f, 0.86f);

            BaseGround((byte)BlockId.Sand, (byte)BlockId.Sandstone, 0.10f, rnd);
            Perimeter(12, (byte)BlockId.Sandstone);

            int cx = SX / 2, cz = SZ / 2;

            // 中央广场：硬化地面 + 水井 + 集市摊位
            Flatten(cx - 10, cz - 10, cx + 10, cz + 10, (byte)BlockId.Concrete);
            Well(cx - 3, cz - 3);
            int stalls = rnd.Next(3, 6);
            for (int k = 0; k < stalls; k++)
                Stall(cx + rnd.Next(-8, 6), cz + rnd.Next(-8, 6), rnd);

            // 环形排列的土坯房（不会互相叠在一起）
            int houses = rnd.Next(4, 7);
            float baseA = (float)rnd.NextDouble() * Mathf.PI * 2f;
            for (int i = 0; i < houses; i++)
            {
                float a = baseA + i * (Mathf.PI * 2f / houses) + (float)(rnd.NextDouble() - 0.5) * 0.35f;
                int hx = ClampI(Mathf.RoundToInt(cx + Mathf.Cos(a) * 21f), 5, SX - 18);
                int hz = ClampI(Mathf.RoundToInt(cz + Mathf.Sin(a) * 21f), 5, SZ - 18);
                House(hx, hz, rnd.Next(6, 10), rnd.Next(6, 10), rnd.Next(4, 6),
                    (byte)BlockId.Sandstone, (byte)BlockId.Wood, rnd, true);
            }

            // 棕榈树
            int palms = rnd.Next(6, 12);
            for (int k = 0; k < palms; k++)
            {
                int x = rnd.Next(5, SX - 5), z = rnd.Next(5, SZ - 5);
                if (Mathf.Abs(x - cx) < 12 && Mathf.Abs(z - cz) < 12) continue;
                PalmTree(x, z, rnd.Next(4, 7));
            }
            // 矮墙 / 货箱掩体
            int walls = rnd.Next(6, 11);
            for (int k = 0; k < walls; k++)
                Wall(rnd.Next(6, SX - 10), GroundY + 1, rnd.Next(6, SZ - 10),
                    rnd.Next(3, 7), rnd.Next(2, 4), (byte)BlockId.Sandstone);
            int crates = rnd.Next(6, 12);
            for (int k = 0; k < crates; k++)
                CratePile(rnd.Next(6, SX - 10), GroundY + 1, rnd.Next(6, SZ - 10), rnd.Next(1, 3), rnd);

            // 沙丘放在最后：只往空地上堆，不会把房子埋掉
            int dunes = rnd.Next(6, 11);
            for (int k = 0; k < dunes; k++)
                Dune(rnd.Next(6, SX - 6), rnd.Next(6, SZ - 6), rnd.Next(2, 5), rnd);

            SpawnPoint = MakeSpawn(7, 7, (byte)BlockId.Sand);
        }

        private void Dune(int x, int z, int r, System.Random rnd)
        {
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d > r) continue;
                    int h = d < r * 0.45f ? 2 : 1;
                    for (int y = 1; y <= h; y++)
                        if (!IsSolidAt(x + dx, GroundY + y, z + dz))
                            SetSafe(x + dx, GroundY + y, z + dz, (byte)BlockId.Sand);
                }
        }

        /// <summary>平顶房子：四壁 + 屋顶 + 门 + 窗（沙漠村庄 / 办公楼通用）。</summary>
        private void House(int x0, int z0, int w, int d, int h, byte wall, byte roof, System.Random rnd, bool parapet)
        {
            int y0 = GroundY + 1;
            Flatten(x0 - 1, z0 - 1, x0 + w, z0 + d, wall);

            for (int x = x0; x < x0 + w; x++)
                for (int z = z0; z < z0 + d; z++)
                {
                    bool edge = (x == x0 || x == x0 + w - 1 || z == z0 || z == z0 + d - 1);
                    if (!edge) continue;
                    for (int y = y0; y < y0 + h; y++) SetSafe(x, y, z, wall);
                }
            Fill(x0, y0 + h, z0, x0 + w - 1, y0 + h, z0 + d - 1, roof);
            if (parapet)
            {
                for (int x = x0; x < x0 + w; x++)
                {
                    SetSafe(x, y0 + h + 1, z0, wall);
                    SetSafe(x, y0 + h + 1, z0 + d - 1, wall);
                }
                for (int z = z0; z < z0 + d; z++)
                {
                    SetSafe(x0, y0 + h + 1, z, wall);
                    SetSafe(x0 + w - 1, y0 + h + 1, z, wall);
                }
            }

            // 门：随机一侧居中，2 格宽 2 格高
            int side = rnd.Next(0, 4);
            int dx = x0 + w / 2, dz = z0 + d / 2;
            if (side == 0) dz = z0;
            else if (side == 1) dz = z0 + d - 1;
            else if (side == 2) dx = x0;
            else dx = x0 + w - 1;
            int ox = side <= 1 ? 1 : 0, oz = side >= 2 ? 1 : 0;
            for (int y = y0; y < y0 + 2; y++)
            {
                SetSafe(dx, y, dz, (byte)BlockId.Air);
                SetSafe(dx + ox, y, dz + oz, (byte)BlockId.Air);
            }

            // 窗：每面墙随机开洞（离地 1 格，留窗台）
            for (int s = 0; s < 4; s++)
            {
                if (rnd.NextDouble() < 0.25f) continue;
                int wy = y0 + 1 + rnd.Next(0, Mathf.Max(1, h - 2));
                if (s <= 1)
                {
                    int wx = x0 + 1 + rnd.Next(0, Mathf.Max(1, w - 3));
                    int wz = s == 0 ? z0 : z0 + d - 1;
                    SetSafe(wx, wy, wz, (byte)BlockId.Air);
                    SetSafe(wx + 1, wy, wz, (byte)BlockId.Air);
                }
                else
                {
                    int wz = z0 + 1 + rnd.Next(0, Mathf.Max(1, d - 3));
                    int wx = s == 2 ? x0 : x0 + w - 1;
                    SetSafe(wx, wy, wz, (byte)BlockId.Air);
                    SetSafe(wx, wy, wz + 1, (byte)BlockId.Air);
                }
            }
        }

        private void PalmTree(int x, int z, int h)
        {
            int y0 = GroundY + 1;
            for (int i = 0; i < h; i++) SetSafe(x, y0 + i, z, (byte)BlockId.Wood);
            int top = y0 + h;
            for (int k = 0; k < 4; k++)
            {
                int sx = k == 0 ? 1 : (k == 1 ? -1 : 0);
                int sz = k == 2 ? 1 : (k == 3 ? -1 : 0);
                for (int i = 1; i <= 3; i++)
                    SetSafe(x + sx * i, top - (i >= 3 ? 1 : 0), z + sz * i, (byte)BlockId.Leaves);
            }
            Fill(x - 1, top + 1, z - 1, x + 1, top + 1, z + 1, (byte)BlockId.Leaves);
            SetSafe(x, top + 2, z, (byte)BlockId.Leaves);
        }

        private void Well(int x, int z)
        {
            for (int dz = 0; dz < 4; dz++)
                for (int dx = 0; dx < 4; dx++)
                {
                    bool edge = dx == 0 || dx == 3 || dz == 0 || dz == 3;
                    if (!edge) continue;
                    for (int y = GroundY + 1; y <= GroundY + 2; y++) SetSafe(x + dx, y, z + dz, (byte)BlockId.Stone);
                }
            int[] px = { x, x + 3, x, x + 3 };
            int[] pz = { z, z, z + 3, z + 3 };
            for (int i = 0; i < 4; i++) SetSafe(px[i], GroundY + 3, pz[i], (byte)BlockId.Wood);
            Fill(x, GroundY + 4, z, x + 3, GroundY + 4, z + 3, (byte)BlockId.Wood);
        }

        private void Stall(int x, int z, System.Random rnd)
        {
            for (int i = 0; i < 4; i++)
            {
                int px = x + (i % 2) * 3, pz = z + (i / 2) * 3;
                SetSafe(px, GroundY + 1, pz, (byte)BlockId.Wood);
                SetSafe(px, GroundY + 2, pz, (byte)BlockId.Wood);
            }
            Fill(x, GroundY + 3, z, x + 3, GroundY + 3, z + 3,
                rnd.Next(0, 2) == 0 ? (byte)BlockId.Crate : (byte)BlockId.Wood);
            CratePile(x, GroundY + 1, z, 2, 1);
        }

        // ------------------------------------------------------------ 热带雨林

        private void GenRainforest(System.Random rnd)
        {
            SkyColor = new Color(0.52f, 0.66f, 0.60f);
            FogColor = new Color(0.44f, 0.58f, 0.52f);
            AmbientColor = new Color(0.32f, 0.40f, 0.36f);
            SunColor = new Color(0.86f, 0.96f, 0.80f);

            BaseGround((byte)BlockId.Grass, (byte)BlockId.Dirt, 0.20f, rnd);
            Perimeter(16, (byte)BlockId.Stone);
            // 崖顶挂一层树叶：像被丛林吞掉的围墙
            for (int i = 0; i < SX; i++)
                for (int t = 0; t < 2; t++)
                {
                    SetSafe(i, 16, t, (byte)BlockId.Leaves);
                    SetSafe(i, 16, SZ - 1 - t, (byte)BlockId.Leaves);
                    SetSafe(t, 16, i, (byte)BlockId.Leaves);
                    SetSafe(SX - 1 - t, 16, i, (byte)BlockId.Leaves);
                }

            int sx = rnd.Next(8, 16), sz = rnd.Next(8, 16);

            int trees = rnd.Next(24, 34);
            for (int k = 0; k < trees; k++)
            {
                int x = rnd.Next(4, SX - 4), z = rnd.Next(4, SZ - 4);
                if (Mathf.Abs(x - sx) < 9 && Mathf.Abs(z - sz) < 9) continue;   // 出生点周围留空地
                JungleTree(x, z, rnd.Next(8, 14), rnd);
            }
            int bushes = rnd.Next(30, 48);
            for (int k = 0; k < bushes; k++)
                Bush(rnd.Next(4, SX - 4), rnd.Next(4, SZ - 4), rnd);

            // 木栈道（横跨场地的高架路）
            if (rnd.Next(0, 2) == 0) Walkway(8, SZ / 2 - 1, SX - 10, SZ / 2 - 1, 3, true);
            else Walkway(SX / 2 - 1, 8, SX / 2 - 1, SZ - 10, 3, false);

            // 石遗迹
            int ruins = rnd.Next(2, 5);
            for (int k = 0; k < ruins; k++)
                Ruin(rnd.Next(8, SX - 16), rnd.Next(8, SZ - 16), rnd.Next(6, 11), rnd);

            // 空地上的补给箱掩体
            int crates = rnd.Next(6, 11);
            for (int k = 0; k < crates; k++)
                CratePile(rnd.Next(6, SX - 10), GroundY + 1, rnd.Next(6, SZ - 10), rnd.Next(1, 3), rnd);

            // 出生点留到最后清空（树 / 灌木 / 箱子都长完了再开这块地）
            SpawnPoint = MakeSpawn(sx, sz, (byte)BlockId.Grass);
        }

        private void JungleTree(int x, int z, int h, System.Random rnd)
        {
            int y0 = GroundY + 1;
            for (int i = 0; i < h; i++) SetSafe(x, y0 + i, z, (byte)BlockId.Wood);
            // 板根
            for (int k = 0; k < 4; k++)
            {
                int bx = k == 0 ? 1 : (k == 1 ? -1 : 0);
                int bz = k == 2 ? 1 : (k == 3 ? -1 : 0);
                SetSafe(x + bx, y0, z + bz, (byte)BlockId.Wood);
                SetSafe(x + bx, y0 + 1, z + bz, (byte)BlockId.Wood);
            }
            int top = y0 + h;
            for (int dy = -2; dy <= 2; dy++)
            {
                int r = dy <= 0 ? 4 : (dy == 1 ? 3 : 2);
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (dx * dx + dz * dz > r * r + 1) continue;
                        if (dx == 0 && dz == 0 && dy < 2) continue;
                        if (rnd.NextDouble() < 0.12f) continue;   // 树冠上开点洞
                        SetSafe(x + dx, top + dy, z + dz, (byte)BlockId.Leaves);
                    }
            }
        }

        private void Bush(int x, int z, System.Random rnd)
        {
            int w = rnd.Next(2, 4);
            for (int dz = 0; dz < w; dz++)
                for (int dx = 0; dx < w; dx++)
                {
                    if (rnd.NextDouble() < 0.25f) continue;
                    SetSafe(x + dx, GroundY + 1, z + dz, (byte)BlockId.Leaves);
                    if (rnd.NextDouble() < 0.5f) SetSafe(x + dx, GroundY + 2, z + dz, (byte)BlockId.Leaves);
                }
        }

        /// <summary>架空木栈道：路面在 GroundY+1（走上去自动上一格），下面有木桩。</summary>
        private void Walkway(int x0, int z0, int x1, int z1, int w, bool alongX)
        {
            int y = GroundY + 1;
            int ax = Mathf.Min(x0, x1), bx = Mathf.Max(x0, x1);
            int az = Mathf.Min(z0, z1), bz = Mathf.Max(z0, z1);
            if (alongX)
            {
                Fill(ax, y, z0, bx, y, z0 + w - 1, (byte)BlockId.Wood);
                for (int x = ax; x <= bx; x += 4)
                    for (int k = 0; k < w; k++) SetSafe(x, GroundY, z0 + k, (byte)BlockId.Wood);
            }
            else
            {
                Fill(x0, y, az, x0 + w - 1, y, bz, (byte)BlockId.Wood);
                for (int z = az; z <= bz; z += 4)
                    for (int k = 0; k < w; k++) SetSafe(x0 + k, GroundY, z, (byte)BlockId.Wood);
            }
        }

        private void Ruin(int x, int z, int size, System.Random rnd)
        {
            int y0 = GroundY + 1;
            int h = rnd.Next(3, 6);
            for (int dx = 0; dx < size; dx++)
                for (int dz = 0; dz < size; dz++)
                {
                    bool edge = dx == 0 || dx == size - 1 || dz == 0 || dz == size - 1;
                    if (!edge) continue;
                    if (rnd.NextDouble() < 0.25f) continue;                 // 塌了一角
                    int hh = rnd.Next(1, h + 1);
                    for (int y = y0; y < y0 + hh; y++) SetSafe(x + dx, y, z + dz, (byte)BlockId.Stone);
                    if (rnd.NextDouble() < 0.45f) SetSafe(x + dx, y0 + hh, z + dz, (byte)BlockId.Leaves);  // 苔藓
                }
        }

        // ------------------------------------------------------------ 办公楼

        private void GenOffice(System.Random rnd)
        {
            SkyColor = new Color(0.70f, 0.74f, 0.82f);
            FogColor = new Color(0.60f, 0.63f, 0.70f);
            AmbientColor = new Color(0.46f, 0.47f, 0.52f);
            SunColor = new Color(1.00f, 1.00f, 1.02f);

            BaseGround((byte)BlockId.Concrete, (byte)BlockId.Concrete, 0f, rnd);
            // 十字走廊铺瓷砖
            for (int i = 2; i < SX - 2; i++)
                for (int w = -2; w <= 2; w++)
                {
                    SetSafe(i, GroundY, SZ / 2 + w, (byte)BlockId.Tiles);
                    SetSafe(SX / 2 + w, GroundY, i, (byte)BlockId.Tiles);
                }
            // 四个象限铺地毯（办公区）
            for (int q = 0; q < 4; q++)
            {
                int qx = (q % 2 == 0) ? 8 : SX / 2 + 6;
                int qz = (q < 2) ? 8 : SZ / 2 + 6;
                Fill(qx, GroundY, qz, qx + 18, GroundY, qz + 18, (byte)BlockId.Carpet);
            }
            // 楼体外墙：砖墙 + 玻璃窗带
            PerimeterWindows(14, (byte)BlockId.Brick, (byte)BlockId.Glass);

            // 承重柱
            for (int px = 10; px < SX - 8; px += 16)
                for (int pz = 10; pz < SZ - 8; pz += 16)
                    Fill(px, GroundY + 1, pz, px + 1, GroundY + 13, pz + 1, (byte)BlockId.Concrete);

            // 工位 / 会议室（避开十字路口与出生大堂）
            for (int gx = 0; gx < 3; gx++)
                for (int gz = 0; gz < 3; gz++)
                {
                    int px = 9 + gx * 15, pz = 9 + gz * 15;
                    if (px > SX - 14 || pz > SZ - 14) continue;
                    if (Mathf.Abs(px + 3 - SX / 2) < 9 && Mathf.Abs(pz + 3 - SZ / 2) < 9) continue;
                    if (gx == 0 && gz == 0) continue;                        // 出生大堂留空
                    if (rnd.NextDouble() < 0.28f) { MeetingRoom(px, pz, rnd); continue; }
                    Cubicle(px, pz, rnd);
                    if (rnd.NextDouble() < 0.5f) Cubicle(px + 8, pz, rnd);
                    if (rnd.NextDouble() < 0.5f) Cubicle(px, pz + 8, rnd);
                }

            // 顶部横梁（室内感，又不封死天空）
            for (int x = 6; x < SX - 6; x += 8)
                Fill(x, GroundY + 10, 6, x, GroundY + 10, SZ - 7, (byte)BlockId.Wood);

            SpawnPoint = MakeSpawn(9, 9, (byte)BlockId.Tiles);

            // 大堂前台
            Fill(SX / 2 - 3, GroundY + 1, SZ / 2 - 8, SX / 2 + 2, GroundY + 1, SZ / 2 - 7, (byte)BlockId.Wood);
            SetSafe(SX / 2 - 3, GroundY, SZ / 2 - 8, (byte)BlockId.Crate);
            SetSafe(SX / 2 + 2, GroundY, SZ / 2 - 8, (byte)BlockId.Crate);
        }

        private void Cubicle(int x0, int z0, System.Random rnd)
        {
            byte part = rnd.Next(0, 2) == 0 ? (byte)BlockId.Concrete : (byte)BlockId.Tiles;
            int w = 6, d = 6, h = 2;                 // 隔断只有 2 格高：能挡身位，但视线可以越过
            int y0 = GroundY + 1;
            for (int i = 0; i < w; i++)
                for (int y = y0; y < y0 + h; y++)
                {
                    SetSafe(x0 + i, y, z0, part);
                    SetSafe(x0 + i, y, z0 + d - 1, part);
                }
            for (int j = 0; j < d; j++)
                for (int y = y0; y < y0 + h; y++)
                {
                    SetSafe(x0, y, z0 + j, part);
                    SetSafe(x0 + w - 1, y, z0 + j, part);
                }
            // 开口（2 格宽）
            int side = rnd.Next(0, 4);
            for (int k = 0; k < 2; k++)
                for (int y = y0; y < y0 + h; y++)
                {
                    if (side == 0) SetSafe(x0 + 2 + k, y, z0, (byte)BlockId.Air);
                    else if (side == 1) SetSafe(x0 + 2 + k, y, z0 + d - 1, (byte)BlockId.Air);
                    else if (side == 2) SetSafe(x0, y, z0 + 2 + k, (byte)BlockId.Air);
                    else SetSafe(x0 + w - 1, y, z0 + 2 + k, (byte)BlockId.Air);
                }
            Desk(x0 + 1, z0 + 1, rnd);
        }

        private void Desk(int x, int z, System.Random rnd)
        {
            int y0 = GroundY + 1;
            SetSafe(x, y0, z, (byte)BlockId.Crate);                 // 桌腿
            SetSafe(x + 2, y0, z, (byte)BlockId.Crate);
            Fill(x, y0 + 1, z, x + 2, y0 + 1, z, (byte)BlockId.Wood);   // 桌面
            SetSafe(x + 1, y0 + 2, z, (byte)BlockId.Metal);            // 显示器
            SetSafe(x + 1, y0, z + 2, (byte)BlockId.Crate);            // 椅子
            if (rnd.NextDouble() < 0.5f) SetSafe(x + 4, y0, z + 1, (byte)BlockId.Crate);  // 文件柜
        }

        private void MeetingRoom(int x0, int z0, System.Random rnd)
        {
            int w = 9, d = 8, h = 3;
            int y0 = GroundY + 1;
            for (int x = x0; x < x0 + w; x++)
                for (int z = z0; z < z0 + d; z++)
                {
                    bool edge = x == x0 || x == x0 + w - 1 || z == z0 || z == z0 + d - 1;
                    if (!edge) continue;
                    for (int y = y0; y < y0 + h; y++) SetSafe(x, y, z, (byte)BlockId.Glass);
                }
            // 门
            for (int y = y0; y < y0 + 2; y++)
            {
                SetSafe(x0 + w / 2, y, z0 + d - 1, (byte)BlockId.Air);
                SetSafe(x0 + w / 2 + 1, y, z0 + d - 1, (byte)BlockId.Air);
            }
            // 会议桌 + 椅子
            Fill(x0 + 2, y0 + 1, z0 + 2, x0 + w - 3, y0 + 1, z0 + d - 3, (byte)BlockId.Wood);
            for (int i = 0; i < 4; i++)
            {
                int px = x0 + 2 + (i % 2) * (w - 5);
                int pz = z0 + 2 + (i / 2) * (d - 5);
                SetSafe(px, y0, pz, (byte)BlockId.Crate);
            }
        }

        // ------------------------------------------------------------ 沙2 DUST II

        private void GenDust2(System.Random rnd)
        {
            SkyColor = new Color(0.84f, 0.76f, 0.58f);
            FogColor = new Color(0.86f, 0.78f, 0.62f);
            AmbientColor = new Color(0.52f, 0.47f, 0.38f);
            SunColor = new Color(1.12f, 1.02f, 0.82f);

            BaseGround((byte)BlockId.Sand, (byte)BlockId.Concrete, 0.06f, rnd);
            Perimeter(13, (byte)BlockId.Sandstone);

            // ---- T 出生平台（左侧中间，1 格高，敌人能直接走上来）
            Platform(5, 26, 16, 38, GroundY + 1, (byte)BlockId.Concrete);
            for (int z = 28; z <= 36; z += 4) CratePile(7, GroundY + 2, z, 2, rnd);

            // ---- A 点（右上）+ A 大坑
            Platform(46, 5, 58, 18, GroundY + 1, (byte)BlockId.Concrete);
            CratePile(48, GroundY + 2, 8, 2, 2);
            CratePile(53, GroundY + 2, 12, 2, 2);
            CratePile(48, GroundY + 2, 15, 2, 1);

            // ---- A 长廊：两道 5 格高的平行墙夹出一条 5 宽的通道
            Fill(18, GroundY + 1, 6, 42, GroundY + 5, 6, (byte)BlockId.Sandstone);
            Fill(18, GroundY + 1, 12, 42, GroundY + 5, 12, (byte)BlockId.Sandstone);
            Trench(26, 7, 34, 11, 2);                    // 长廊中间的下沉坑
            CratePile(22, GroundY + 1, 9, 2, 1);
            CratePile(37, GroundY + 1, 8, 2, 2);

            // ---- 中门：两块金属箱夹一条 3 宽的缝
            Fill(SX / 2 - 6, GroundY + 1, SZ / 2 - 1, SX / 2 - 2, GroundY + 4, SZ / 2 + 1, (byte)BlockId.Metal);
            Fill(SX / 2 + 2, GroundY + 1, SZ / 2 - 1, SX / 2 + 6, GroundY + 4, SZ / 2 + 1, (byte)BlockId.Metal);

            // ---- B 点（右下）+ 通往 B 的下沉地道
            Platform(46, 44, 58, 58, GroundY + 1, (byte)BlockId.Concrete);
            CratePile(48, GroundY + 2, 47, 2, 2);
            CratePile(53, GroundY + 2, 51, 2, 2);
            Trench(18, 42, 34, 46, 3);

            // ---- 零散掩体 + 油桶
            int crates = rnd.Next(8, 14);
            for (int k = 0; k < crates; k++)
                CratePile(rnd.Next(8, SX - 12), GroundY + 1, rnd.Next(8, SZ - 12), rnd.Next(1, 3), rnd);
            int barrels = rnd.Next(6, 12);
            for (int k = 0; k < barrels; k++)
            {
                int x = rnd.Next(8, SX - 10), z = rnd.Next(8, SZ - 10);
                int y = GroundHeight(x, z, SY - 1);
                if (y < 1 || y > GroundY + 3) continue;
                Fill(x, y, z, x, y + 1, z, (byte)BlockId.Metal);
            }

            // 出生在 T 平台上
            SpawnPoint = new Vector3(10.5f, GroundY + 2f, 32.5f);
        }

        /// <summary>实心平台：从地面上方填到 topY（站在 topY+1）。</summary>
        private void Platform(int x0, int z0, int x1, int z1, int topY, byte id)
        {
            Fill(x0, GroundY + 1, z0, x1, topY, z1, id);
        }

        /// <summary>下沉通道：挖空一条沟，两端各铺逐级台阶（不会变成死坑）。</summary>
        private void Trench(int x0, int z0, int x1, int z1, int depth)
        {
            Fill(x0, GroundY - depth + 1, z0, x1, GroundY, z1, (byte)BlockId.Air);
            for (int i = 0; i < depth; i++)
            {
                int topY = GroundY - depth + 1 + i;
                int px = x0 - 1 - i, qx = x1 + 1 + i;
                if (px < 2 || qx >= SX - 2) continue;
                Fill(px, GroundY - depth + 1, z0, px, topY, z1, (byte)BlockId.Sand);
                Fill(qx, GroundY - depth + 1, z0, qx, topY, z1, (byte)BlockId.Sand);
            }
        }

        // ------------------------------------------------------------ 基础构件

        private void SetSafe(int x, int y, int z, byte id)
        {
            if (InBounds(x, y, z)) Set(x, y, z, id);
        }

        private void ClearArea(int x0, int y0, int z0, int w, int h)
        {
            for (int z = z0; z < z0 + w; z++)
                for (int x = x0; x < x0 + w; x++)
                    for (int y = y0; y < y0 + h; y++)
                        SetSafe(x, y, z, (byte)BlockId.Air);
        }

        private void Wall(int x, int y, int z, int len, int height, byte id)
        {
            bool horiz = (x + z) % 2 == 0;
            for (int i = 0; i < len; i++)
                for (int yy = 0; yy < height; yy++)
                {
                    int px = horiz ? x + i : x;
                    int pz = horiz ? z : z + i;
                    SetSafe(px, y + yy, pz, id);
                }
        }

        private void CratePile(int x, int y, int z, int w, int h)
        {
            for (int i = 0; i < w; i++)
                for (int j = 0; j < w; j++)
                    for (int k = 0; k < h; k++)
                        SetSafe(x + i, y + k, z + j, (byte)BlockId.Crate);
        }

        private void CratePile(int x, int y, int z, int w, System.Random rnd)
        {
            int h = rnd.Next(1, 4);
            CratePile(x, y, z, w, h);
        }

        private void Tree(int x, int y, int z, int h)
        {
            for (int i = 0; i < h; i++) SetSafe(x, y + i, z, (byte)BlockId.Wood);
            int top = y + h;
            for (int dy = -1; dy <= 2; dy++)
            {
                int r = dy >= 1 ? 1 : 2;
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (dx == 0 && dz == 0 && dy < 2) continue;
                        SetSafe(x + dx, top + dy, z + dz, (byte)BlockId.Leaves);
                    }
            }
        }

        /// <summary>
        /// 中央的大树：2×2 粗壮树干 + 几根侧枝 + 分层树冠（纯方块堆积的轮廓）。
        /// </summary>
        private void BigTree(int x, int y, int z, int h, System.Random rnd)
        {
            const byte wood = (byte)BlockId.Wood;
            const byte leaf = (byte)BlockId.Leaves;

            // 树干（2×2）
            for (int i = 0; i < h; i++)
                for (int dx = 0; dx <= 1; dx++)
                    for (int dz = 0; dz <= 1; dz++)
                        SetSafe(x + dx, y + i, z + dz, wood);

            // 板根：底部一圈加粗，看起来像真正的大树
            for (int dy = 0; dy < 3; dy++)
                for (int dx = -1; dx <= 2; dx++)
                    for (int dz = -1; dz <= 2; dz++)
                    {
                        bool core = dx >= 0 && dx <= 1 && dz >= 0 && dz <= 1;
                        if (core) continue;
                        if (rnd.Next(0, 100) < 65 - dy * 15) SetSafe(x + dx, y + dy, z + dz, wood);
                    }

            int top = y + h;

            // 侧枝：从主干斜向外伸出四根短枝
            for (int k = 0; k < 4; k++)
            {
                int sx = (k == 0 ? 2 : (k == 1 ? -3 : 0));
                int sz = (k == 2 ? 2 : (k == 3 ? -3 : 0));
                int by = top - 4 - (k % 2);
                for (int i = 0; i < 3; i++)
                {
                    SetSafe(x + sx + (k == 0 ? i : (k == 1 ? -i : 0)), by + i,
                            z + sz + (k == 2 ? i : (k == 3 ? -i : 0)), wood);
                }
            }

            // 树冠：分层方块汇聚成的伞形
            for (int dy = -2; dy <= 5; dy++)
            {
                int yy = top + dy;
                int r = dy <= 0 ? 4 : (dy <= 2 ? 3 : (dy == 3 ? 2 : 1));
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int d2 = dx * dx + dz * dz;
                        if (d2 > r * r + 1) continue;
                        bool trunkCore = dx >= 0 && dx <= 1 && dz >= 0 && dz <= 1 && dy <= 2;
                        if (trunkCore) continue;                      // 别把树干替换成叶子
                        if (Mathf.Abs(dx) == r && Mathf.Abs(dz) == r && d2 > r * r && rnd.Next(0, 100) < 45) continue; // 削掉尖角
                        SetSafe(x + dx, yy, z + dz, leaf);
                    }
            }
        }

        private void BuildTower(int x0, int y0, int z0, int size, int height, System.Random rnd)
        {
            byte wall = rnd.Next(0, 2) == 0 ? (byte)BlockId.Brick : (byte)BlockId.Wood;
            for (int z = z0; z < z0 + size; z++)
                for (int x = x0; x < x0 + size; x++)
                    for (int y = y0; y < y0 + height; y++)
                    {
                        bool edge = (x == x0 || x == x0 + size - 1 || z == z0 || z == z0 + size - 1);
                        if (edge) SetSafe(x, y, z, wall);
                        if (y == y0 + height - 1) SetSafe(x, y, z, (byte)BlockId.Wood); // 屋顶
                    }
            // 挖门
            for (int y = y0; y < y0 + 3; y++)
            {
                SetSafe(x0 + size / 2, y, z0, (byte)BlockId.Air);
                SetSafe(x0 + size / 2 - 1, y, z0, (byte)BlockId.Air);
            }
            // 内部通往顶层的木楼梯（沿墙螺旋）
            for (int i = 0; i < height - 1; i++)
            {
                int y = y0 + i;
                int x = x0 + 1 + (i % (size - 3));
                int z = z0 + 1 + (i / (size - 3));
                if (z >= z0 + size - 1) z = z0 + size - 2;
                SetSafe(x, y, z, (byte)BlockId.Wood);
            }
        }
    }

    /// <summary>射线 vs AABB（slab 法），用于命中敌人判定。</summary>
    public static class RayMath
    {
        public static bool RayAABB(Vector3 origin, Vector3 dir, Vector3 min, Vector3 max, out float dist)
        {
            dist = float.MaxValue;
            float tmin = 0f, tmax = float.MaxValue;
            for (int a = 0; a < 3; a++)
            {
                float o = origin[a], d = dir[a];
                if (Mathf.Abs(d) < 1e-8f)
                {
                    if (o < min[a] || o > max[a]) return false;
                }
                else
                {
                    float inv = 1f / d;
                    float t1 = (min[a] - o) * inv;
                    float t2 = (max[a] - o) * inv;
                    if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
                    if (t1 > tmin) tmin = t1;
                    if (t2 < tmax) tmax = t2;
                    if (tmin > tmax) return false;
                }
            }
            dist = tmin;
            return true;
        }
    }
}
