using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PixelArena
{
    /// <summary>
    /// AI 队友：跟着玩家编队推进，视野里发现敌人就开火压制。
    /// 与敌人共用 NavGrid 寻路；子弹走 ProjectileSystem 的"友军弹"
    /// （打敌人、不打玩家 / 其他队友）。阵亡后 18 秒在玩家身边归队。
    /// </summary>
    public class AllyAI : MonoBehaviour
    {
        public bool Alive = true;
        public float Hp = 160f;
        public float MaxHp = 160f;
        public int Slot;                     // 编队位：0 左翼 / 1 右翼 / 2 后卫
        public string MateName = "队友";
        public VoxelWorld World;
        public PlayerController Player;
        public BlockCharacter Model;

        private float respawnTimer;
        private Vector3 velocity;
        private bool grounded;
        private float fireTimer;
        private float senseTimer;
        private float aimTimer;
        private float aimPitchSmooth;
        private float hitFlash;
        private float strafeSign = 1f;
        private Renderer[] skinRenderers;
        private Color[] skinColors;
        private static readonly MaterialPropertyBlock tmpBlock = new MaterialPropertyBlock();

        private EnemyAI target;              // 当前交火目标
        private bool seesTarget;
        private Vector3 lastKnown;           // 目标最后出现的位置
        private bool hasLastKnown;
        private float lostTimer;
        private float repathCd;              // 附近没有敌人时也偶尔换个跟随点，别一串人排队

        // 寻路（与 EnemyAI 相同的航点跟随逻辑）
        private readonly List<Vector3> path = new List<Vector3>();
        private int pathIdx;
        private Vector3 pathGoal;
        private float pathTimer;
        private bool hasPath;
        private static float allyPathBudget;
        private static int allyPathFrame = -1;

        public Vector3 AimPoint => transform.position + Vector3.up * 1.5f;
        public Vector3 BoundsMin => transform.position + new Vector3(-0.36f, 0f, -0.36f);
        public Vector3 BoundsMax => transform.position + new Vector3(0.36f, 1.82f, 0.36f);
        /// <summary>朝向角（度，0 = 世界 +Z），小地图上的队友箭头要用。
        /// 转身是作用在模型上的（FaceTowards），所以要读模型的角度而不是根节点。</summary>
        public float FacingYaw => Model != null ? Model.transform.eulerAngles.y : transform.eulerAngles.y;

        // 头顶名字标签（战地式：友军名字常驻，好和红名的敌人区分）
        private Canvas tagCanvas;
        private Text tagText;
        private const float TagFadeDist = 52f;   // 超过这个距离就不画了，免得一团字糊在屏幕上
        private static Font tagFont;

        private static readonly string[] Callsigns = { "幽灵 GHOST", "巨汉 TITAN", "快手 FLASH" };
        private static readonly Color[] ShirtCols =
        {
            new Color(0.16f, 0.42f, 0.34f),
            new Color(0.20f, 0.40f, 0.30f),
            new Color(0.14f, 0.36f, 0.40f),
        };

        public static AllyAI Spawn(VoxelWorld world, PlayerController player, Transform parent, Vector3 pos, int slot)
        {
            var go = new GameObject("Ally_" + slot);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var ai = go.AddComponent<AllyAI>();
            ai.World = world;
            ai.Player = player;
            ai.Slot = slot;
            ai.MateName = Callsigns[Mathf.Clamp(slot, 0, Callsigns.Length - 1)];

            Color shirt = ShirtCols[Mathf.Clamp(slot, 0, ShirtCols.Length - 1)];
            ai.Model = BlockCharacter.Create(go.transform, new Color(0.86f, 0.70f, 0.54f), shirt,
                new Color(0.18f, 0.22f, 0.20f), new Color(0.14f, 0.12f, 0.10f));
            ai.Model.transform.localPosition = Vector3.zero;
            ai.ApplyAllyLook(slot);
            ai.Model.SetWeapon(WeaponKind.Rifle);
            ai.CacheColors();
            ai.BuildNameTag();
            return ai;
        }

        /// <summary>
        /// 头顶名字牌：世界空间 Canvas + 一个带描边的 Text，每帧朝向相机（billboard）。
        /// 敌人没有这个牌子 —— 看得到名字的一定是自己人。
        /// </summary>
        private void BuildNameTag()
        {
            if (tagFont == null)
            {
                string[] names = { "Microsoft YaHei", "Microsoft YaHei UI", "PingFang SC", "Noto Sans CJK SC", "Noto Sans SC", "Arial", "sans-serif" };
                tagFont = Font.CreateDynamicFontFromOSFont(names, 34);
            }
            if (tagFont == null) return;

            var go = new GameObject("NameTag");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 2.30f, 0f);
            go.transform.localScale = Vector3.one * 0.0042f;    // 画布按像素排版，缩放后约 1.4m 宽

            tagCanvas = go.AddComponent<Canvas>();
            tagCanvas.renderMode = RenderMode.WorldSpace;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(360f, 76f);
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            tagText = new GameObject("TagText").AddComponent<Text>();
            tagText.transform.SetParent(go.transform, false);
            tagText.font = tagFont;
            tagText.fontSize = 34;
            tagText.text = MateName;
            tagText.alignment = TextAnchor.MiddleCenter;
            tagText.horizontalOverflow = HorizontalWrapMode.Overflow;
            tagText.verticalOverflow = VerticalWrapMode.Overflow;
            tagText.color = new Color(0.55f, 0.95f, 1f);
            var ol = tagText.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.9f);
            ol.effectDistance = new Vector2(3f, -3f);
            var trt = tagText.rectTransform;
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            tagText.raycastTarget = false;
        }

        /// <summary>每帧把名字牌转向相机；太远就淡出，阵亡时藏起来。</summary>
        private void UpdateNameTag()
        {
            if (tagCanvas == null) return;
            var gm = GameManager.Instance;
            var cam = gm != null && gm.Rig != null ? gm.Rig.Cam : null;
            bool on = Alive && cam != null;
            float alpha = 0f;
            if (on)
            {
                float d = Vector3.Distance(cam.transform.position, transform.position + Vector3.up * 2.3f);
                on = d < TagFadeDist;
                alpha = on ? Mathf.Clamp01((TagFadeDist - d) / 12f) : 0f;
                if (on) tagCanvas.transform.rotation = cam.transform.rotation;   // billboard
            }
            if (tagCanvas.gameObject.activeSelf != on) tagCanvas.gameObject.SetActive(on);
            if (on && tagText != null)
            {
                float hurt = Alive ? Mathf.Clamp01(1f - Hp / MaxHp) : 0f;
                tagText.color = new Color(0.55f + hurt * 0.45f, 0.95f - hurt * 0.45f, 1f - hurt * 0.55f, alpha);
            }
        }

        /// <summary>队友外观：蓝绿色头盔 + 青色臂环，一眼和敌人（红/灰）区分开。</summary>
        private void ApplyAllyLook(int slot)
        {
            Transform root = Model.transform;
            Color armor = new Color(0.14f, 0.30f, 0.34f);
            Color trim = new Color(0.35f, 0.85f, 0.95f);
            VoxelAssets.MakeBox("Helmet", root, new Vector3(0f, 1.74f, 0f), new Vector3(0.50f, 0.22f, 0.50f), armor);
            VoxelAssets.MakeBox("Visor", root, new Vector3(0f, 1.63f, 0.235f), new Vector3(0.32f, 0.07f, 0.03f), trim);
            VoxelAssets.MakeBox("Vest", root, new Vector3(0f, 1.06f, 0.185f), new Vector3(0.45f, 0.40f, 0.06f), armor);
            VoxelAssets.MakeBox("Band", Model.ArmL, new Vector3(0f, -0.20f, 0f), new Vector3(0.21f, 0.07f, 0.21f), trim);
            VoxelAssets.MakeBox("Pack", root, new Vector3(0f, 1.10f, -0.28f), new Vector3(0.36f, 0.40f, 0.14f),
                new Color(0.22f, 0.28f, 0.22f));
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && !gm.SimulationRunning) return;   // 菜单/暂停/死亡时冻结
            if (World == null || Player == null) return;
            Tick(Time.deltaTime);
            UpdateNameTag();
        }

        // ------------------------------------------------------------------ 每帧

        private void Tick(float dt)
        {
            if (!Alive)
            {
                respawnTimer -= dt;
                if (respawnTimer <= 0f) Respawn();
                return;
            }

            aimTimer -= dt;
            fireTimer -= dt;
            senseTimer -= dt;
            if (senseTimer <= 0f)
            {
                senseTimer = 0.1f;
                Sense(0.1f);
            }

            // ---------- 行为 ----------
            Vector3 wish;
            Vector3 faceDir;
            bool combat = seesTarget && target != null && target.Alive;

            if (combat) CombatThink(dt, out wish, out faceDir);
            else FollowThink(dt, out wish, out faceDir);

            wish = Separate(wish);

            // 物理（和敌人同款：AABB + 重力 + 自动上台阶）
            Vector3 horiz = new Vector3(wish.x, 0f, wish.z);
            velocity.x = Mathf.MoveTowards(velocity.x, horiz.x, 30f * dt);
            velocity.z = Mathf.MoveTowards(velocity.z, horiz.z, 30f * dt);
            velocity.y -= 26f * dt;
            if (velocity.y < -40f) velocity.y = -40f;
            bool hitWall;
            Vector3 pos = World.MoveAABB(transform.position, 0.34f, 1.8f, velocity * dt, out grounded, out hitWall);
            if (grounded && velocity.y < 0f) velocity.y = 0f;
            if (hitWall) { velocity.x *= 0.2f; velocity.z *= 0.2f; }
            transform.position = pos;

            if (faceDir.sqrMagnitude > 0.0001f)
                Model.FaceTowards(faceDir, 170f, dt);

            // 动画：交火时枪口对准目标，平时低姿戒备
            float move01 = Mathf.Clamp01(new Vector2(velocity.x, velocity.z).magnitude / 4.6f);
            float wantPitch = -20f;
            if (combat)
            {
                Vector3 to = target.AimPoint - AimPoint;
                float flat = Mathf.Sqrt(to.x * to.x + to.z * to.z);
                if (flat > 0.001f) wantPitch = Mathf.Clamp(Mathf.Atan2(to.y, flat) * Mathf.Rad2Deg, -60f, 60f);
            }
            else if (faceDir.sqrMagnitude > 0.0001f) wantPitch = -20f;
            aimPitchSmooth = Mathf.MoveTowards(aimPitchSmooth, wantPitch, dt * 150f);
            Model.Animate(move01, dt, true, aimPitchSmooth, false);

            // 开火：看见 + 瞄好
            if (combat && aimTimer <= 0f && fireTimer <= 0f)
            {
                float d = Vector3.Distance(AimPoint, target.AimPoint);
                if (d < 26f)
                {
                    fireTimer = 0.5f + Random.value * 0.4f;
                    Shoot(d);
                }
            }

            // 受击闪白
            if (hitFlash > 0f)
            {
                hitFlash -= dt;
                float k = Mathf.Clamp01(hitFlash / 0.09f);
                for (int i = 0; i < skinRenderers.Length; i++)
                    VoxelAssets.SetColor(skinRenderers[i], Color.Lerp(skinColors[i], Color.white, k));
                if (hitFlash <= 0f) RestoreColors();
            }

            if (transform.position.y < -10f) Respawn();
        }

        // ------------------------------------------------------------------ 感知 / 行为

        /// <summary>视野内找最近的活敌人。队友眼神比敌人好一点（是在找目标，不是被偷袭）。</summary>
        private void Sense(float step)
        {
            var gm = GameManager.Instance;
            seesTarget = false;
            if (gm == null) return;

            EnemyAI best = null;
            float bd = float.MaxValue;
            for (int i = 0; i < gm.Enemies.Count; i++)
            {
                var e = gm.Enemies[i];
                if (e == null || !e.Alive) continue;
                Vector3 to = e.AimPoint - AimPoint;
                float d = to.magnitude;
                if (d > 36f) continue;
                Vector3 flat = new Vector3(to.x, 0f, to.z);
                Vector3 face = Model != null ? Model.transform.forward : transform.forward;
                face.y = 0f;
                float ang = (flat.sqrMagnitude > 0.01f && face.sqrMagnitude > 0.01f)
                    ? Vector3.Angle(face, flat) : 180f;
                if (ang > 105f && d > 5f) continue;                 // 视野锥宽一些
                if (!World.HasLineOfSight(AimPoint, e.AimPoint)) continue;
                if (d < bd) { bd = d; best = e; }
            }

            if (best != null)
            {
                if (target != best) aimTimer = 0.3f;                // 换目标要重新瞄
                target = best;
                seesTarget = true;
                lastKnown = best.transform.position;
                hasLastKnown = true;
                lostTimer = 0f;
            }
            else if (target != null)
            {
                lostTimer += step;
                if (lostTimer > 3f || !target.Alive) target = null;
            }
        }

        private void CombatThink(float dt, out Vector3 wish, out Vector3 faceDir)
        {
            Vector3 to = target.AimPoint - AimPoint;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            float dist = flat.magnitude;
            faceDir = flat;
            wish = Vector3.zero;

            if (dist < 1f) return;
            Vector3 nd = flat / dist;
            if (dist < 6f) wish = -nd * 3.2f;                        // 太近后退
            else if (dist > 14f) wish = nd * 4.6f;                   // 太远追一点
            else
            {
                // 保持距离横向游走
                Vector3 side = Vector3.Cross(Vector3.up, nd).normalized;
                wish = side * 2.2f * strafeSign;
                if (Random.value < 0.004f) strafeSign = -strafeSign;
            }
        }

        private void FollowThink(float dt, out Vector3 wish, out Vector3 faceDir)
        {
            wish = Vector3.zero;
            faceDir = Vector3.zero;

            // 玩家被团灭局面外：跟着走就行
            float toPlayer = Vector3.Distance(transform.position, Player.transform.position);

            // 编队锚点：相对玩家朝向的左翼 / 右翼 / 后卫
            var gm = GameManager.Instance;
            float yaw = gm != null && gm.Rig != null ? gm.Rig.Yaw : 0f;
            Vector3 off = Slot == 0 ? new Vector3(-2.6f, 0f, 0.4f)
                        : Slot == 1 ? new Vector3(2.6f, 0f, 0.4f)
                        : new Vector3(0f, 0f, -3.4f);
            Vector3 anchor = Player.transform.position + Quaternion.Euler(0f, yaw, 0f) * off;

            Vector3 goal = anchor;
            // 附近有战斗线索时先去看一眼（不会走太远，超出 16m 就放弃回归队形）
            if (hasLastKnown && target == null && lostTimer < 5f)
            {
                float toLk = Vector3.Distance(transform.position, lastKnown);
                float lkToPlayer = Vector3.Distance(lastKnown, Player.transform.position);
                if (toLk > 2.5f && lkToPlayer < 16f) goal = lastKnown;
            }

            float speed = toPlayer > 16f ? 7.0f : 4.4f;
            float toGoal = Vector3.Distance(transform.position, goal);
            if (toGoal < 1.6f)
            {
                // 到位：面向玩家前方警戒
                Vector3 f = Player.transform.position - transform.position;
                f.y = 0f;
                faceDir = f;
                return;
            }
            wish = Navigate(goal, speed, out faceDir, dt);
        }

        // ------------------------------------------------------------------ 寻路（同 EnemyAI）

        private Vector3 Navigate(Vector3 target, float speed, out Vector3 faceDir, float dt)
        {
            faceDir = Vector3.zero;
            pathTimer -= dt;
            if (!hasPath || pathTimer <= 0f || (target - pathGoal).sqrMagnitude > 6.25f)
            {
                Repath(target);
                pathTimer = 0.55f + Random.Range(0f, 0.3f);
            }

            if (hasPath && pathIdx < path.Count)
            {
                for (int guard = 0; guard < 4 && pathIdx < path.Count; guard++)
                {
                    Vector3 wp = path[pathIdx];
                    Vector3 d = wp - transform.position;
                    d.y = 0f;
                    if (d.magnitude < 0.7f) { pathIdx++; continue; }
                    faceDir = d.normalized;
                    return faceDir * speed;
                }
                hasPath = false;
                pathTimer = 0f;
            }
            // 兜底直线
            Vector3 dd = target - transform.position;
            dd.y = 0f;
            if (dd.magnitude < 0.001f) return Vector3.zero;
            faceDir = dd.normalized;
            Vector3 w = faceDir * speed;
            // 面前是 2 格以上的高墙就别硬顶（跳不过去，只会原地磨）
            if (World != null && World.WallHeightAhead(transform.position, w, 0.34f) >= 2)
            {
                pathTimer = 0f;
                return w * 0.25f;
            }
            return w;
        }

        private void Repath(Vector3 target)
        {
            hasPath = false;
            pathIdx = 0;
            pathGoal = target;
            if (World == null || World.Nav == null) return;
            if (!BudgetOk()) return;
            bool exact = World.Nav.FindPath(transform.position, target, path);
            hasPath = path.Count > 0;
            if (!exact && hasPath)
            {
                Vector3 last = path[path.Count - 1];
                if ((last - transform.position).sqrMagnitude < 1.4f) hasPath = false;
            }
        }

        private static bool BudgetOk()
        {
            if (Time.frameCount != allyPathFrame)
            {
                allyPathFrame = Time.frameCount;
                allyPathBudget = 0f;
            }
            if (allyPathBudget >= 3f) return false;
            allyPathBudget += 1f;
            return true;
        }

        /// <summary>和玩家 / 其他队友保持距离，别叠在一起。</summary>
        private Vector3 Separate(Vector3 wish)
        {
            var gm = GameManager.Instance;
            if (gm == null) return wish;
            Vector3 push = Vector3.zero;

            Vector3 dp = transform.position - Player.transform.position;
            dp.y = 0f;
            float mp = dp.magnitude;
            if (mp > 0.08f && mp < 1.6f) push += dp / mp * (1.6f - mp);
            else if (mp <= 0.08f) push += new Vector3(1f, 0f, 0.6f);

            for (int i = 0; i < gm.Allies.Count; i++)
            {
                var o = gm.Allies[i];
                if (o == null || o == this || !o.Alive) continue;
                Vector3 d = transform.position - o.transform.position;
                d.y = 0f;
                float m = d.magnitude;
                if (m > 0.08f && m < 2.0f) push += d / m * (2.0f - m);
                else if (m <= 0.08f) push += new Vector3(-1f, 0f, 0.4f);
            }
            if (push.sqrMagnitude < 0.001f) return wish;
            Vector3 sep = push * 2.2f;
            if (sep.magnitude > 3.5f) sep = sep.normalized * 3.5f;
            return wish + sep;
        }

        // ------------------------------------------------------------------ 战斗

        private void Shoot(float dist)
        {
            Vector3 from = Model != null ? Model.MuzzlePosition : AimPoint;
            Vector3 to = target.AimPoint;
            Vector3 dir = (to - from).normalized;

            // 距离越远散布越大（比敌人准一些：是来帮你的）
            float t = Mathf.Clamp01(dist / 26f);
            float spread = Mathf.Lerp(0.8f, 5.5f, t * t);
            Vector3 upRef = Mathf.Abs(dir.y) > 0.95f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(dir, upRef).normalized;
            Vector3 upV = Vector3.Cross(right, dir).normalized;
            float a = Random.value * Mathf.PI * 2f;
            float deg = Mathf.Sqrt(Random.value) * spread;
            Vector3 axis = right * Mathf.Cos(a) + upV * Mathf.Sin(a);
            dir = Quaternion.AngleAxis(deg, axis) * dir;

            ProjectileSystem.FireAlly(World, from, dir, 46f, 13f);
            AudioKit.Play("rifle", from, 0.3f, Random.Range(1.1f, 1.25f));
            if (FxPool.Instance != null) FxPool.Instance.Flash(from, new Color(0.6f, 0.9f, 1f), 0.16f);
            // 队友开枪也会惊动敌人（他们听得到枪声方位）
            var gm = GameManager.Instance;
            if (gm != null) gm.MakeNoise(from, 16f);
        }

        public void TakeDamage(float dmg, Vector3 point)
        {
            if (!Alive) return;
            Hp -= dmg;
            hitFlash = 0.08f;
            // 知道是谁打的：转过去还击
            lastKnown = point;
            hasLastKnown = true;
            lostTimer = 0f;
            if (Hp <= 0f) Die();
        }

        private void Die()
        {
            Alive = false;
            respawnTimer = 18f;
            if (FxPool.Instance != null)
            {
                FxPool.Instance.Burst(transform.position + Vector3.up * 0.9f, new Color(0.2f, 0.45f, 0.4f), 14, 1.2f);
            }
            AudioKit.Play("die", transform.position, 0.7f, 0.95f);
            Model.SetVisible(false);
            var gm = GameManager.Instance;
            if (gm != null && gm.Hud != null) gm.Hud.Announce(MateName + " 阵亡 · 18 秒后归队");
        }

        private void Respawn()
        {
            if (Player == null || World == null) return;
            // 在玩家附近找一个能站的位置
            Vector3 basePos = Player.transform.position;
            Vector3 pos = basePos + new Vector3(2f, 0f, -2f);
            for (int tries = 0; tries < 30; tries++)
            {
                float a = Random.value * Mathf.PI * 2f;
                float d = Random.Range(3f, 6f);
                int ix = Mathf.FloorToInt(basePos.x + Mathf.Cos(a) * d);
                int iz = Mathf.FloorToInt(basePos.z + Mathf.Sin(a) * d);
                if (ix < 2 || iz < 2 || ix >= World.SX - 2 || iz >= World.SZ - 2) continue;
                int y = World.GroundHeight(ix, iz, World.SY - 1);
                if (World.IsSolidAt(ix, y, iz) || World.IsSolidAt(ix, y + 1, iz)) continue;
                pos = new Vector3(ix + 0.5f, y + 0.05f, iz + 0.5f);
                break;
            }
            transform.position = pos;
            velocity = Vector3.zero;
            Hp = MaxHp;
            Alive = true;
            target = null;
            hasLastKnown = false;
            Model.SetVisible(true);
            RestoreColors();
            var gm = GameManager.Instance;
            if (gm != null && gm.Hud != null) gm.Hud.Announce(MateName + " 已归队");
        }

        /// <summary>HUD 状态行：名字 + 血量 / 重生倒计时。</summary>
        public string StatusText
        {
            get
            {
                if (Alive) return MateName + "  " + Mathf.CeilToInt(Hp) + " HP";
                return MateName + "  重生 " + Mathf.CeilToInt(Mathf.Max(0f, respawnTimer)) + "s";
            }
        }

        private void CacheColors()
        {
            var rends = Model.GetComponentsInChildren<Renderer>();
            skinRenderers = new Renderer[rends.Length];
            skinColors = new Color[rends.Length];
            int id = Shader.PropertyToID("_Color");
            for (int i = 0; i < rends.Length; i++)
            {
                skinRenderers[i] = rends[i];
                rends[i].GetPropertyBlock(tmpBlock);
                skinColors[i] = tmpBlock.GetColor(id);
            }
        }

        private void RestoreColors()
        {
            for (int i = 0; i < skinRenderers.Length; i++)
                VoxelAssets.SetColor(skinRenderers[i], skinColors[i]);
        }
    }
}
