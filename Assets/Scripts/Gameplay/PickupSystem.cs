using System.Collections.Generic;
using UnityEngine;

namespace PixelArena
{
    /// <summary>
    /// 场地上的医疗包：地面/掩体上会散布几个缓慢旋转的方块医疗箱，
    /// 走上去自动拾取（回血 + 提示），被拿走后过一段时间会在别处重新刷出来。
    /// 纯代码生成，没有预制体。
    /// </summary>
    public static class PickupSystem
    {
        private const int MaxItems = 6;
        private const float HealAmount = 35f;
        private const float RespawnTime = 22f;
        private const float PickRadius = 1.15f;
        private const float MinDistFromPlayer = 7f;

        private class Item
        {
            public Transform t;
            public Renderer[] renderers;
            public bool active;
            public float respawn;
            public float spin;
            public float baseY;
        }

        private static readonly List<Item> items = new List<Item>();
        private static Transform root;
        private static VoxelWorld world;

        private static readonly Color BoxColor = new Color(0.92f, 0.93f, 0.95f);
        private static readonly Color CrossColor = new Color(0.90f, 0.22f, 0.22f);
        private static readonly Color GlowColor = new Color(0.45f, 0.95f, 0.55f);

        // ------------------------------------------------------------------ 生命周期

        /// <summary>（重新）在世界里铺设医疗包。重开一局 / 回主菜单时调用。</summary>
        public static void Reset(VoxelWorld w)
        {
            world = w;
            if (world == null) return;

            if (root == null)
            {
                var go = new GameObject("Pickups");
                root = go.transform;
            }
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var c = root.GetChild(i);
                if (c != null) UnityEngine.Object.Destroy(c.gameObject);
            }
            items.Clear();

            for (int i = 0; i < MaxItems; i++)
            {
                var it = CreateItem(root);
                Vector3 p;
                if (FindSpot(out p))
                {
                    it.t.position = p;
                    it.baseY = p.y;
                    it.active = true;
                    it.t.gameObject.SetActive(true);
                }
                else
                {
                    it.active = false;
                    it.respawn = 3f;
                    it.t.gameObject.SetActive(false);
                }
                items.Add(it);
            }
        }

        private static Item CreateItem(Transform parent)
        {
            var go = new GameObject("Medkit");
            go.transform.SetParent(parent, false);

            Color cross = CrossColor;
            // 箱体
            VoxelAssets.MakeBox("Case", go.transform, new Vector3(0f, 0.22f, 0f), new Vector3(0.46f, 0.34f, 0.46f), BoxColor);
            VoxelAssets.MakeBox("Lid", go.transform, new Vector3(0f, 0.41f, 0f), new Vector3(0.50f, 0.06f, 0.50f), new Color(0.80f, 0.83f, 0.88f));
            // 红十字（正面 + 顶面）
            VoxelAssets.MakeBox("CrossV1", go.transform, new Vector3(0f, 0.22f, 0.235f), new Vector3(0.09f, 0.24f, 0.02f), cross);
            VoxelAssets.MakeBox("CrossH1", go.transform, new Vector3(0f, 0.22f, 0.235f), new Vector3(0.24f, 0.09f, 0.02f), cross);
            VoxelAssets.MakeBox("CrossV2", go.transform, new Vector3(0f, 0.22f, -0.235f), new Vector3(0.09f, 0.24f, 0.02f), cross);
            VoxelAssets.MakeBox("CrossH2", go.transform, new Vector3(0f, 0.22f, -0.235f), new Vector3(0.24f, 0.09f, 0.02f), cross);
            VoxelAssets.MakeBox("CrossTop1", go.transform, new Vector3(0f, 0.455f, 0f), new Vector3(0.09f, 0.02f, 0.24f), cross);
            VoxelAssets.MakeBox("CrossTop2", go.transform, new Vector3(0f, 0.455f, 0f), new Vector3(0.24f, 0.02f, 0.09f), cross);
            // 底下的绿色光环，远远就能看见
            VoxelAssets.MakeBox("Glow", go.transform, new Vector3(0f, 0.03f, 0f), new Vector3(0.62f, 0.04f, 0.62f), GlowColor, VoxelAssets.Unlit);

            var it = new Item
            {
                t = go.transform,
                renderers = go.GetComponentsInChildren<Renderer>(true),
                active = true,
                spin = Random.Range(25f, 45f)
            };
            return it;
        }

        // ------------------------------------------------------------------ 每帧

        public static void Update(VoxelWorld w, PlayerController player, GameManager gm, float dt)
        {
            if (w == null || player == null) return;
            if (root == null || items.Count == 0) { Reset(w); if (items.Count == 0) return; }

            Vector3 pp = player.transform.position;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it == null || it.t == null) continue;

                if (!it.active)
                {
                    it.respawn -= dt;
                    if (it.respawn <= 0f)
                    {
                        Vector3 p;
                        if (FindSpot(out p))
                        {
                            it.t.position = p;
                            it.baseY = p.y;
                            it.active = true;
                            it.t.gameObject.SetActive(true);
                        }
                        else it.respawn = 2f;
                    }
                    continue;
                }

                // 悬浮 + 旋转，方便在方块堆里一眼认出来
                it.t.Rotate(0f, it.spin * dt, 0f);
                float bob = Mathf.Sin(Time.time * 2f + i * 1.7f) * 0.07f;
                it.t.position = new Vector3(it.t.position.x, it.baseY + bob, it.t.position.z);

                // 拾取判定（只看水平距离，站在箱子上也算）
                Vector3 d = it.t.position - pp;
                d.y = 0f;
                if (d.sqrMagnitude > PickRadius * PickRadius) continue;
                if (Mathf.Abs(it.t.position.y - pp.y) > 2.2f) continue;

                Collect(it, gm, player);
            }
        }

        private static void Collect(Item it, GameManager gm, PlayerController player)
        {
            it.active = false;
            it.respawn = RespawnTime;
            it.t.gameObject.SetActive(false);

            if (gm == null) return;
            AudioKit.Play2D("heal", 0.75f);
            if (FxPool.Instance != null)
                FxPool.Instance.Burst(it.t.position + Vector3.up * 0.3f, GlowColor, 12, 1.1f);

            if (gm.PlayerHp >= gm.PlayerMaxHp - 0.5f)
            {
                if (gm.Hud != null) gm.Hud.Announce("医疗包已拾取（生命已满）");
                return;
            }
            gm.HealPlayer(HealAmount);
            if (gm.Hud != null) gm.Hud.Announce("拾取医疗包  +" + Mathf.RoundToInt(HealAmount) + " HP");
        }

        // ------------------------------------------------------------------ 辅助

        /// <summary>找一个平整、不卡方块、离其它医疗包有点距离的落点。</summary>
        private static bool FindSpot(out Vector3 pos)
        {
            pos = Vector3.zero;
            if (world == null) return false;

            var gmRef = GameManager.Instance;
            Vector3 pp = gmRef != null && gmRef.Player != null ? gmRef.Player.transform.position : Vector3.zero;
            bool hasPlayer = gmRef != null && gmRef.Player != null;

            for (int tries = 0; tries < 60; tries++)
            {
                int ix = Random.Range(4, world.SX - 4);
                int iz = Random.Range(4, world.SZ - 4);
                int y = world.GroundHeight(ix, iz, world.SY - 1);
                // 别刷到房顶 / 横梁上（玩家够不着等于白刷）
                if (y < 1 || y > 12) continue;
                if (world.IsSolidAt(ix, y, iz) || world.IsSolidAt(ix, y + 1, iz)) continue;

                Vector3 cand = new Vector3(ix + 0.5f, y + 0.02f, iz + 0.5f);

                // 别刷在玩家脚边（否则等于站着不动白嫖回血）
                if (hasPlayer)
                {
                    Vector3 dp = cand - pp;
                    dp.y = 0f;
                    if (dp.sqrMagnitude < MinDistFromPlayer * MinDistFromPlayer) continue;
                }

                bool tooClose = false;
                for (int i = 0; i < items.Count; i++)
                {
                    var o = items[i];
                    if (o == null || !o.active) continue;
                    Vector3 d = o.t.position - cand;
                    d.y = 0f;
                    if (d.sqrMagnitude < 64f) { tooClose = true; break; }   // 8m 内不重复
                }
                if (tooClose) continue;

                pos = cand;
                return true;
            }
            return false;
        }
    }
}
