using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelArena
{
    /// <summary>
    /// 运行时程序化绘制触控按钮图标。
    /// 本工程不引入任何美术贴图（零资源），图标全部用像素画法画在 Texture2D 上再转 UGUI Sprite；
    /// 统一画成白色，实际颜色由 Image.color 相乘得到（和项目"一切程序化生成"的做法一致）。
    /// </summary>
    public static class IconSprites
    {
        private const int S = 128;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Fire()   { return Get("fire", DrawFire); }
        public static Sprite Reload() { return Get("reload", DrawReload); }
        public static Sprite Jump()   { return Get("jump", DrawJump); }
        public static Sprite Aim()    { return Get("aim", DrawAim); }
        public static Sprite View()   { return Get("view", DrawView); }
        public static Sprite Squad()  { return Get("squad", DrawSquad); }
        public static Sprite Ride()   { return Get("ride", DrawRide); }

        private static Sprite Get(string key, Action<Canvas2> draw)
        {
            Sprite sp;
            if (Cache.TryGetValue(key, out sp) && sp != null) return sp;
            var c = new Canvas2(S);
            draw(c);
            var tex = c.ToTexture();
            sp = Sprite.Create(tex, new Rect(0f, 0f, S, S), new Vector2(0.5f, 0.5f), 100f);
            sp.name = "Icon_" + key;
            Cache[key] = sp;
            return sp;
        }

        // ------------------------------------------------------------------ 各图标画法

        /// <summary>开火：中心实心圆 + 四向放射短棒。</summary>
        private static void DrawFire(Canvas2 c)
        {
            Color w = Color.white;
            c.Disc(64f, 64f, 20f, w);
            c.Line(64f, 96f, 64f, 116f, 14f, w);
            c.Line(64f, 32f, 64f, 12f, 14f, w);
            c.Line(96f, 64f, 116f, 64f, 14f, w);
            c.Line(32f, 64f, 12f, 64f, 14f, w);
        }

        /// <summary>换弹：3/4 环形箭头 + 箭头尖。</summary>
        private static void DrawReload(Canvas2 c)
        {
            Color w = Color.white;
            float r = 44f;
            c.Arc(64f, 64f, r, 13f, 40f, 320f, w);
            // 起点 40° 处的箭头尖（朝逆时针切线方向）
            float a = 40f * Mathf.Deg2Rad;
            float px = 64f + Mathf.Cos(a) * r;
            float py = 64f + Mathf.Sin(a) * r;
            float tx = -Mathf.Sin(a), ty = Mathf.Cos(a);   // 逆时针切线
            float nx = Mathf.Cos(a), ny = Mathf.Sin(a);    // 径向
            c.Tri(
                px + tx * 20f, py + ty * 20f,
                px - nx * 15f + tx * 2f, py - ny * 15f + ty * 2f,
                px + nx * 15f + tx * 2f, py + ny * 15f + ty * 2f, w);
        }

        /// <summary>跳跃：向上箭头（三角头 + 竖杆）。</summary>
        private static void DrawJump(Canvas2 c)
        {
            Color w = Color.white;
            c.Tri(64f, 114f, 26f, 70f, 102f, 70f, w);
            c.Rect(54, 16, 74, 74, w);
        }

        /// <summary>开镜：细圆环 + 四段准线 + 中心点。</summary>
        private static void DrawAim(Canvas2 c)
        {
            Color w = Color.white;
            c.Ring(64f, 64f, 52f, 8f, w);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f * Mathf.Deg2Rad;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                c.Line(64f + ca * 10f, 64f + sa * 10f, 64f + ca * 48f, 64f + sa * 48f, 7f, w);
            }
            c.Disc(64f, 64f, 7f, w);
        }

        /// <summary>切换视角：眼睛（椭圆环 + 瞳孔）。</summary>
        private static void DrawView(Canvas2 c)
        {
            Color w = Color.white;
            for (int i = 0; i < 320; i++)
            {
                float t = i / 319f * Mathf.PI * 2f;
                float x = 64f + Mathf.Cos(t) * 54f;
                float y = 64f + Mathf.Sin(t) * 31f;
                c.Disc(x, y, 5.5f, w);
            }
            c.Disc(64f, 64f, 17f, w);
        }

        /// <summary>小队召唤：两个人形（头 + 身）。</summary>
        private static void DrawSquad(Canvas2 c)
        {
            Color w = Color.white;
            c.Disc(42f, 92f, 12f, w);
            c.Rect(28, 48, 56, 78, w);
            c.Disc(88f, 92f, 12f, w);
            c.Rect(74, 48, 102, 78, w);
            c.Rect(20, 24, 110, 38, w);
        }

        /// <summary>上下坦克：坦克侧视（炮管 / 炮塔 / 车体 / 履带）。</summary>
        private static void DrawRide(Canvas2 c)
        {
            Color w = Color.white;
            c.Rect(78, 74, 116, 86, w);      // 炮管
            c.Rect(52, 60, 84, 82, w);       // 炮塔
            c.Rect(30, 44, 100, 62, w);      // 车体
            c.Rect(20, 26, 110, 40, w);      // 履带
        }

        // ------------------------------------------------------------------ 像素画板

        private sealed class Canvas2
        {
            private readonly int size;
            private readonly Color[] buf;

            public Canvas2(int s)
            {
                size = s;
                buf = new Color[s * s];
            }

            public void Set(int x, int y, Color c)
            {
                if ((uint)x >= (uint)size || (uint)y >= (uint)size) return;
                buf[y * size + x] = c;
            }

            public void Rect(int x0, int y0, int x1, int y1, Color c)
            {
                if (x1 < x0) { int t = x0; x0 = x1; x1 = t; }
                if (y1 < y0) { int t = y0; y0 = y1; y1 = t; }
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++) Set(x, y, c);
            }

            public void Disc(float cx, float cy, float r, Color c)
            {
                int x0 = Mathf.FloorToInt(cx - r - 1f), x1 = Mathf.CeilToInt(cx + r + 1f);
                int y0 = Mathf.FloorToInt(cy - r - 1f), y1 = Mathf.CeilToInt(cy + r + 1f);
                float r2 = r * r;
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                        if (dx * dx + dy * dy <= r2) Set(x, y, c);
                    }
            }

            public void Ring(float cx, float cy, float r, float w, Color c)
            {
                float inner = r - w * 0.5f, outer = r + w * 0.5f;
                int x0 = Mathf.FloorToInt(cx - outer - 1f), x1 = Mathf.CeilToInt(cx + outer + 1f);
                int y0 = Mathf.FloorToInt(cy - outer - 1f), y1 = Mathf.CeilToInt(cy + outer + 1f);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d >= inner && d <= outer) Set(x, y, c);
                    }
            }

            /// <summary>圆弧（角度制，0° = +x 方向，逆时针为正）。</summary>
            public void Arc(float cx, float cy, float r, float w, float a0, float a1, Color c)
            {
                if (a1 < a0) a1 += 360f;
                int steps = Mathf.CeilToInt((a1 - a0) * 0.9f) + 8;
                for (int i = 0; i <= steps; i++)
                {
                    float a = Mathf.Lerp(a0, a1, i / (float)steps) * Mathf.Deg2Rad;
                    Disc(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r, w * 0.5f, c);
                }
            }

            public void Line(float x0, float y0, float x1, float y1, float w, Color c)
            {
                float dx = x1 - x0, dy = y1 - y0;
                float len = Mathf.Sqrt(dx * dx + dy * dy);
                int steps = Mathf.CeilToInt(len) + 1;
                if (steps < 1) steps = 1;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    Disc(x0 + dx * t, y0 + dy * t, w * 0.5f, c);
                }
            }

            /// <summary>实心三角形（按扫描线填充，参数为三个顶点的 x, y）。</summary>
            public void Tri(float ax, float ay, float bx, float by, float cx, float cy, Color c)
            {
                int yBot = Mathf.FloorToInt(Mathf.Min(ay, Mathf.Min(by, cy)));
                int yTop = Mathf.CeilToInt(Mathf.Max(ay, Mathf.Max(by, cy)));
                for (int y = yBot; y <= yTop; y++)
                {
                    float lo = float.MaxValue, hi = float.MinValue;
                    ScanEdge(ax, ay, bx, by, y, ref lo, ref hi);
                    ScanEdge(bx, by, cx, cy, y, ref lo, ref hi);
                    ScanEdge(cx, cy, ax, ay, y, ref lo, ref hi);
                    if (lo > hi) continue;
                    Rect(Mathf.CeilToInt(lo), y, Mathf.FloorToInt(hi), y, c);
                }
            }

            private static void ScanEdge(float px, float py, float qx, float qy, int y, ref float lo, ref float hi)
            {
                if (!((py <= y && qy > y) || (qy <= y && py > y))) return;
                float t = (y - py) / (qy - py);
                float x = px + (qx - px) * t;
                if (x < lo) lo = x;
                if (x > hi) hi = x;
            }

            public Texture2D ToTexture()
            {
                var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
                t.SetPixels(buf);
                t.Apply(false, true);
                t.filterMode = FilterMode.Bilinear;
                t.wrapMode = TextureWrapMode.Clamp;
                return t;
            }
        }
    }
}
