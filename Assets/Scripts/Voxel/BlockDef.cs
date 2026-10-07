using UnityEngine;

namespace PixelArena
{
    /// <summary>
    /// 方块类型。数值直接存进体素数组（byte）。
    /// </summary>
    public enum BlockId : byte
    {
        Air = 0,
        Grass = 1,
        Dirt = 2,
        Stone = 3,
        Sand = 4,
        Wood = 5,
        Leaves = 6,
        Brick = 7,
        Metal = 8,
        Crate = 9,
        Concrete = 10,
        Glass = 11,      // 玻璃幕墙（办公楼）
        Carpet = 12,     // 办公地毯
        Tiles = 13,      // 瓷砖（走廊 / 隔断）
        Sandstone = 14   // 砂岩（沙漠村庄 / 沙2）
    }

    /// <summary>
    /// 方块属性表 + 程序化生成的像素纹理图集（不依赖任何外部图片资源）。
    /// </summary>
    public static class BlockDef
    {
        public const int Tile = 16;          // 每个方块贴图 16x16 像素（像素风）
        public const int Cols = 4;           // 图集 4x4 = 16 格
        public const int AtlasSize = Tile * Cols;

        // 图集格子索引
        public const int T_GrassTop = 0;
        public const int T_GrassSide = 1;
        public const int T_Dirt = 2;
        public const int T_Stone = 3;
        public const int T_Sand = 4;
        public const int T_WoodSide = 5;
        public const int T_WoodTop = 6;
        public const int T_Leaves = 7;
        public const int T_Brick = 8;
        public const int T_Metal = 9;
        public const int T_Crate = 10;
        public const int T_Concrete = 11;
        public const int T_Glass = 12;
        public const int T_Carpet = 13;
        public const int T_Tiles = 14;
        public const int T_Sandstone = 15;

        public static bool IsSolid(byte id) => id != (byte)BlockId.Air;
        public static bool IsOpaque(byte id) => id != (byte)BlockId.Air;

        /// <summary>顶/侧/底 使用的图集格子。</summary>
        public static int TileTop(byte id)
        {
            switch ((BlockId)id)
            {
                case BlockId.Grass: return T_GrassTop;
                case BlockId.Wood: return T_WoodTop;
                case BlockId.Crate: return T_Crate;
                case BlockId.Tiles: return T_Tiles;
                default: return TileSide(id);
            }
        }

        public static int TileSide(byte id)
        {
            switch ((BlockId)id)
            {
                case BlockId.Grass: return T_GrassSide;
                case BlockId.Dirt: return T_Dirt;
                case BlockId.Stone: return T_Stone;
                case BlockId.Sand: return T_Sand;
                case BlockId.Wood: return T_WoodSide;
                case BlockId.Leaves: return T_Leaves;
                case BlockId.Brick: return T_Brick;
                case BlockId.Metal: return T_Metal;
                case BlockId.Crate: return T_Crate;
                case BlockId.Concrete: return T_Concrete;
                case BlockId.Glass: return T_Glass;
                case BlockId.Carpet: return T_Carpet;
                case BlockId.Tiles: return T_Tiles;
                case BlockId.Sandstone: return T_Sandstone;
                default: return T_Stone;
            }
        }

        public static int TileBottom(byte id)
        {
            switch ((BlockId)id)
            {
                case BlockId.Grass: return T_Dirt;
                case BlockId.Wood: return T_WoodTop;
                case BlockId.Carpet: return T_Concrete;
                default: return TileSide(id);
            }
        }

        /// <summary>破坏所需伤害总量（子弹伤害 / 霰弹单颗伤害）。</summary>
        public static float Hardness(byte id)
        {
            switch ((BlockId)id)
            {
                case BlockId.Grass: return 26f;
                case BlockId.Dirt: return 24f;
                case BlockId.Stone: return 62f;
                case BlockId.Sand: return 20f;
                case BlockId.Wood: return 40f;
                case BlockId.Leaves: return 8f;
                case BlockId.Brick: return 56f;
                case BlockId.Metal: return 130f;
                case BlockId.Crate: return 22f;
                case BlockId.Concrete: return 48f;
                case BlockId.Glass: return 10f;      // 玻璃一打就碎
                case BlockId.Carpet: return 16f;
                case BlockId.Tiles: return 40f;
                case BlockId.Sandstone: return 44f;
                default: return 1f;
            }
        }

        /// <summary>破坏时碎片粒子的颜色。</summary>
        public static Color DebrisColor(byte id)
        {
            switch ((BlockId)id)
            {
                case BlockId.Grass: return new Color(0.36f, 0.62f, 0.24f);
                case BlockId.Dirt: return new Color(0.47f, 0.34f, 0.22f);
                case BlockId.Stone: return new Color(0.50f, 0.51f, 0.53f);
                case BlockId.Sand: return new Color(0.85f, 0.79f, 0.55f);
                case BlockId.Wood: return new Color(0.45f, 0.31f, 0.18f);
                case BlockId.Leaves: return new Color(0.24f, 0.52f, 0.20f);
                case BlockId.Brick: return new Color(0.63f, 0.26f, 0.20f);
                case BlockId.Metal: return new Color(0.58f, 0.60f, 0.64f);
                case BlockId.Crate: return new Color(0.55f, 0.40f, 0.22f);
                case BlockId.Concrete: return new Color(0.72f, 0.72f, 0.70f);
                case BlockId.Glass: return new Color(0.62f, 0.82f, 0.92f);
                case BlockId.Carpet: return new Color(0.20f, 0.22f, 0.30f);
                case BlockId.Tiles: return new Color(0.86f, 0.88f, 0.90f);
                case BlockId.Sandstone: return new Color(0.80f, 0.72f, 0.52f);
                default: return Color.gray;
            }
        }

        // ---------------------------------------------------------------- 纹理

        private static uint Hash(uint x, uint y, uint s)
        {
            uint h = x * 374761393u + y * 668265263u + s * 2246822519u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }

        /// <summary>0..1 白噪声。</summary>
        private static float N(int x, int y, int s)
        {
            return (Hash((uint)x, (uint)y, (uint)s) & 0xFF) / 255f;
        }

        private static Color32 C(float r, float g, float b)
        {
            return new Color32((byte)Mathf.Clamp(r * 255f, 0, 255), (byte)Mathf.Clamp(g * 255f, 0, 255), (byte)Mathf.Clamp(b * 255f, 0, 255), 255);
        }

        /// <summary>
        /// 生成 4x4 像素图集。注意：y=0 对应方块贴图的"下方"（与 UV 一致）。
        /// </summary>
        public static Texture2D CreateAtlas()
        {
            var tex = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false, true);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.anisoLevel = 1;
            var px = new Color32[AtlasSize * AtlasSize];
            for (int i = 0; i < px.Length; i++) px[i] = C(1f, 0f, 1f);

            WriteTile(px, T_GrassTop, GrassTop);
            WriteTile(px, T_GrassSide, GrassSide);
            WriteTile(px, T_Dirt, Dirt);
            WriteTile(px, T_Stone, Stone);
            WriteTile(px, T_Sand, Sand);
            WriteTile(px, T_WoodSide, WoodSide);
            WriteTile(px, T_WoodTop, WoodTop);
            WriteTile(px, T_Leaves, Leaves);
            WriteTile(px, T_Brick, Brick);
            WriteTile(px, T_Metal, Metal);
            WriteTile(px, T_Crate, Crate);
            WriteTile(px, T_Concrete, Concrete);
            WriteTile(px, T_Glass, Glass);
            WriteTile(px, T_Carpet, Carpet);
            WriteTile(px, T_Tiles, Tiles);
            WriteTile(px, T_Sandstone, Sandstone);

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        private static void WriteTile(Color32[] px, int tile, System.Func<int, int, Color32> f)
        {
            int col = tile % Cols;
            int row = tile / Cols;
            int ox = col * Tile;
            int oy = row * Tile;
            for (int y = 0; y < Tile; y++)
            {
                int baseIdx = (oy + y) * AtlasSize + ox;
                for (int x = 0; x < Tile; x++) px[baseIdx + x] = f(x, y);
            }
        }

        // --- 各种图案（x 向右，y 向上，y=0 是底部） ---

        private static Color32 GrassTop(int x, int y)
        {
            float n = N(x, y, 11);
            float blotch = N(x / 3, y / 3, 5);
            float g = 0.30f + n * 0.16f + blotch * 0.12f;
            return C(0.22f + n * 0.10f, g, 0.16f + n * 0.06f);
        }

        private static Color32 GrassSide(int x, int y)
        {
            int edge = 11 + (int)(N(x, 0, 7) * 4); // 草皮厚度不均
            float n = N(x, y, 3);
            if (y >= edge)
            {
                float g = 0.30f + n * 0.16f;
                return C(0.22f + n * 0.10f, g, 0.16f + n * 0.06f);
            }
            float dn = N(x, y, 17);
            return C(0.44f + dn * 0.10f, 0.32f + dn * 0.08f, 0.20f + dn * 0.06f);
        }

        private static Color32 Dirt(int x, int y)
        {
            float n = N(x, y, 23);
            float peb = N(x / 4, y / 4, 9) > 0.75f ? -0.12f : 0f;
            return C(0.46f + n * 0.12f + peb, 0.33f + n * 0.10f + peb, 0.21f + n * 0.08f + peb);
        }

        private static Color32 Stone(int x, int y)
        {
            float n = N(x, y, 31);
            float low = N(x / 4, y / 4, 41);
            float v = 0.44f + n * 0.12f + low * 0.14f;
            return C(v, v * 1.01f, v * 1.06f);
        }

        private static Color32 Sand(int x, int y)
        {
            float n = N(x, y, 47);
            float v = 0.78f + n * 0.14f;
            return C(v, v * 0.92f, v * 0.62f);
        }

        private static Color32 WoodSide(int x, int y)
        {
            float n = N(x, y, 53);
            float grain = (x % 5 == 0) ? -0.10f : 0f;
            float knot = N(x / 6, y / 5, 61) > 0.85f ? -0.12f : 0f;
            return C(0.46f + n * 0.06f + grain + knot, 0.31f + n * 0.05f + grain, 0.17f + n * 0.04f + grain);
        }

        private static Color32 WoodTop(int x, int y)
        {
            float dx = x - 7.5f, dy = y - 7.5f;
            int ring = (int)Mathf.Floor(Mathf.Sqrt(dx * dx + dy * dy));
            float n = N(x, y, 67);
            float dark = (ring % 3 == 0) ? -0.09f : 0f;
            return C(0.50f + n * 0.05f + dark, 0.35f + n * 0.04f + dark, 0.20f + n * 0.03f + dark);
        }

        private static Color32 Leaves(int x, int y)
        {
            float n = N(x, y, 71);
            float hole = N(x / 2, y / 2, 73) > 0.82f ? -0.16f : 0f;
            return C(0.16f + n * 0.14f + hole, 0.44f + n * 0.18f + hole, 0.16f + n * 0.10f + hole);
        }

        private static Color32 Brick(int x, int y)
        {
            int row = y / 4;
            int offset = (row % 2) * 8;
            int bx = (x + offset) % 16;
            bool mortar = (y % 4 == 0) || (bx % 8 == 0);
            float n = N(x, y, 79);
            if (mortar) return C(0.62f + n * 0.08f, 0.60f + n * 0.08f, 0.57f + n * 0.08f);
            return C(0.60f + n * 0.14f, 0.25f + n * 0.10f, 0.19f + n * 0.08f);
        }

        private static Color32 Metal(int x, int y)
        {
            float n = N(x, y, 83);
            bool border = x < 2 || x > 13 || y < 2 || y > 13;
            bool rivet = (Mathf.Abs(x - 3) <= 1 || Mathf.Abs(x - 12) <= 1) && (Mathf.Abs(y - 3) <= 1 || Mathf.Abs(y - 12) <= 1);
            float v = 0.50f + n * 0.08f;
            if (border) v += 0.10f;
            if (rivet) v += 0.14f;
            return C(v, v * 1.02f, v * 1.08f);
        }

        private static Color32 Crate(int x, int y)
        {
            float n = N(x, y, 89);
            bool border = x < 2 || x > 13 || y < 2 || y > 13;
            bool diag = Mathf.Abs(x - y) <= 1 || Mathf.Abs(x + y - 15) <= 1;
            float v = 0.52f + n * 0.07f;
            if (border || diag) v -= 0.14f;
            return C(v, v * 0.72f, v * 0.42f);
        }

        private static Color32 Concrete(int x, int y)
        {
            float n = N(x, y, 97);
            float crack = N(x / 5, y / 5, 101) > 0.88f ? -0.10f : 0f;
            float v = 0.70f + n * 0.10f + crack;
            return C(v, v, v * 0.98f);
        }

        /// <summary>玻璃幕墙：深色窗框 + 淡蓝反光。</summary>
        private static Color32 Glass(int x, int y)
        {
            float n = N(x, y, 103);
            bool frame = x < 2 || x > 13 || y < 2 || y > 13;
            if (frame) return C(0.26f + n * 0.06f, 0.29f + n * 0.06f, 0.34f + n * 0.06f);
            float v = 0.60f + n * 0.12f;
            float streak = N(x / 8, y, 109) > 0.7f ? 0.10f : 0f;   // 一道反光
            return C((v + streak) * 0.70f, (v + streak) * 0.92f, (v + streak) * 1.06f);
        }

        /// <summary>办公地毯：深蓝灰 + 细密绒面噪点。</summary>
        private static Color32 Carpet(int x, int y)
        {
            float n = N(x, y, 107);
            float fleck = N(x / 2, y / 2, 111) > 0.85f ? 0.06f : 0f;
            float v = 0.15f + n * 0.06f + fleck;
            return C(v, v * 1.10f, v * 1.32f);
        }

        /// <summary>瓷砖：8 像素一块 + 灰缝。</summary>
        private static Color32 Tiles(int x, int y)
        {
            float n = N(x, y, 113);
            bool grout = (x % 8 == 0) || (y % 8 == 0);
            if (grout) return C(0.52f + n * 0.06f, 0.54f + n * 0.06f, 0.56f + n * 0.06f);
            return C(0.85f + n * 0.08f, 0.87f + n * 0.08f, 0.90f + n * 0.08f);
        }

        /// <summary>砂岩：土黄 + 横向层理。</summary>
        private static Color32 Sandstone(int x, int y)
        {
            float n = N(x, y, 127);
            float band = (y % 6 == 0) ? -0.09f : 0f;
            float v = 0.74f + n * 0.10f + band;
            return C(v, v * 0.90f, v * 0.64f);
        }
    }
}
