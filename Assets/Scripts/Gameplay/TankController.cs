using System.Collections.Generic;
using UnityEngine;

namespace PixelArena
{
    /// <summary>坦克上的两件家伙：主炮（爆破弹）与同轴机枪。</summary>
    public enum TankWeapon { Cannon = 0, MachineGun = 1 }

    /// <summary>
    /// 召唤坦克：体素拼装的车体 + 可旋转炮塔 + 可俯仰炮管。
    /// 玩家靠近按 E 登乘：WASD 驾驶（A/D 转车体）、鼠标转炮塔、左键开火。
    /// 武器两件（滚轮 / Q / 数字键 1、2 切换）：
    ///   主炮 —— 爆破弹，碰到敌人或方块就在那一点起爆（大范围 + 拆方块）；
    ///   同轴机枪 —— 高射速低伤害，只打敌人，不拆地形。
    /// 视角两种：V 键在"车外第三人称"和"舱内第一人称"之间切。
    /// 车体 HP 归零时进入 3 秒自爆倒计时（可跳车逃生）。
    /// </summary>
    public class TankController : MonoBehaviour
    {
        /// <summary>当前选中的车裁武器（0 = 主炮，1 = 同轴机枪）。</summary>
        public TankWeapon Weapon = TankWeapon.Cannon;
        public int WeaponIndex => (int)Weapon;
        public static int WeaponCount => 2;
        public static string SlotName(int i) => i == 0 ? "主炮 CANNON" : "同轴机枪 MG";
        public VoxelWorld World;
        public WorldView View;
        public Transform Turret;
        public Transform Barrel;
        public Transform Muzzle;

        public bool Mounted;
        /// <summary>舱内第一人称的眼位（挂在炮塔上，跟着炮塔转）。</summary>
        public Transform EyeAnchor;
        /// <summary>同轴机枪的枪口（炮盾右侧）。</summary>
        public Transform MgMuzzle;
        /// <summary>车体装甲（与玩家血量完全独立的耐久条，被打只掉这个）。</summary>
        public float Hp = 900f;
        public float MaxHp = 900f;
        /// <summary>装甲归零后进入自爆倒计时：给车上的人 3 秒逃生窗口。</summary>
        public bool Doomed;
        public float DoomTimer;
        public const float DoomDuration = 3f;

        private float hullYaw;
        private float turretYaw;
        private float barrelElev;
        private float driveSpeed;
        private float verticalVel;
        private bool grounded;
        private float fireCd;
        private bool dead;
        private float beepCd;
        private float crushCd;
        private float mgCd;
        private Transform aimMarker;
        private readonly List<Renderer> markerParts = new List<Renderer>();

        // 碾压判定用的车体 footprint（半宽 / 半长，比视觉车体略大一点，免得"明明压过去了却没死"）
        private const float CrushHalfX = 1.85f;
        private const float CrushHalfZ = 2.65f;
        // 主炮弹道参数（要和 ProjectileSystem.FireShell 里的重力一致，落点预测才算得准）
        public const float ShellSpeed = 62f;
        private const float ShellGravity = 4f;

        public bool IsAlive => !dead;
        public bool CannonReady => !Doomed && fireCd <= 0f;
        public float Hp01 => MaxHp > 0f ? Mathf.Clamp01(Hp / MaxHp) : 0f;
        public Vector3 SeatPos => transform.position + Vector3.up * 1.2f;

        public Bounds TankBounds
        {
            get { return new Bounds(transform.position + Vector3.up * 1.0f, new Vector3(3.6f, 2.4f, 5.0f)); }
        }

        // ------------------------------------------------------------------ 生成

        public static TankController Spawn(VoxelWorld world, WorldView view, Vector3 pos, Transform parent)
        {
            var go = new GameObject("Tank");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var tk = go.AddComponent<TankController>();
            tk.World = world;
            tk.View = view;
            tk.BuildModel();
            return tk;
        }

        private void BuildModel()
        {
            Transform root = transform;
            Color hull = new Color(0.32f, 0.36f, 0.25f);
            Color dark = new Color(0.15f, 0.17f, 0.14f);

            // 车体（+Z 为车头）
            VoxelAssets.MakeBox("Hull", root, new Vector3(0f, 0.85f, -0.2f), new Vector3(2.6f, 0.9f, 4.0f), hull);
            VoxelAssets.MakeBox("Glacis", root, new Vector3(0f, 0.78f, 1.9f), new Vector3(2.4f, 0.55f, 0.8f), hull);
            VoxelAssets.MakeBox("Deck", root, new Vector3(0f, 1.34f, -0.5f), new Vector3(2.2f, 0.14f, 2.4f), dark);
            VoxelAssets.MakeBox("FenderL", root, new Vector3(-1.45f, 1.05f, -0.2f), new Vector3(0.6f, 0.12f, 4.4f), hull);
            VoxelAssets.MakeBox("FenderR", root, new Vector3(1.45f, 1.05f, -0.2f), new Vector3(0.6f, 0.12f, 4.4f), hull);

            // 履带 + 负重轮
            VoxelAssets.MakeBox("TrackL", root, new Vector3(-1.5f, 0.45f, -0.2f), new Vector3(0.55f, 0.9f, 4.5f), dark);
            VoxelAssets.MakeBox("TrackR", root, new Vector3(1.5f, 0.45f, -0.2f), new Vector3(0.55f, 0.9f, 4.5f), dark);
            Color wheel = new Color(0.10f, 0.11f, 0.10f);
            for (int i = 0; i < 4; i++)
            {
                float z = -1.85f + i * 1.1f;
                VoxelAssets.MakeBox("WhlL" + i, root, new Vector3(-1.5f, 0.22f, z), new Vector3(0.62f, 0.34f, 0.5f), wheel);
                VoxelAssets.MakeBox("WhlR" + i, root, new Vector3(1.5f, 0.22f, z), new Vector3(0.62f, 0.34f, 0.5f), wheel);
            }

            // 炮塔（水平旋转）
            var tur = new GameObject("Turret");
            tur.transform.SetParent(root, false);
            tur.transform.localPosition = new Vector3(0f, 1.42f, -0.35f);
            tur.transform.localRotation = Quaternion.identity;
            Turret = tur.transform;
            VoxelAssets.MakeBox("TurretBody", Turret, new Vector3(0f, 0.30f, 0f), new Vector3(1.7f, 0.62f, 2.0f), hull);
            VoxelAssets.MakeBox("Mantlet", Turret, new Vector3(0f, 0.30f, 1.0f), new Vector3(0.72f, 0.5f, 0.34f), dark);
            VoxelAssets.MakeBox("Cupola", Turret, new Vector3(0f, 0.70f, -0.4f), new Vector3(0.7f, 0.28f, 0.7f), hull);
            VoxelAssets.MakeBox("Antenna", Turret, new Vector3(0.62f, 1.1f, -0.7f), new Vector3(0.05f, 1.1f, 0.05f), dark);

            // 炮管（俯仰枢轴在炮塔前部）
            var bar = new GameObject("Barrel");
            bar.transform.SetParent(Turret, false);
            bar.transform.localPosition = new Vector3(0f, 0.30f, 0.95f);
            bar.transform.localRotation = Quaternion.identity;
            Barrel = bar.transform;
            VoxelAssets.MakeBox("BarrelM", Barrel, new Vector3(0f, 0f, 1.25f), new Vector3(0.24f, 0.24f, 2.5f), dark);
            VoxelAssets.MakeBox("MuzzleBrake", Barrel, new Vector3(0f, 0f, 2.55f), new Vector3(0.38f, 0.38f, 0.32f), new Color(0.09f, 0.10f, 0.09f));

            var mz = new GameObject("Muzzle");
            mz.transform.SetParent(Barrel, false);
            mz.transform.localPosition = new Vector3(0f, 0f, 2.75f);
            mz.transform.localRotation = Quaternion.identity;
            Muzzle = mz.transform;

            // 同轴机枪：炮盾右侧伸出一根细管，跟着炮塔走
            VoxelAssets.MakeBox("MgBody", Turret, new Vector3(0.62f, 0.28f, 0.55f), new Vector3(0.20f, 0.22f, 0.7f), dark);
            VoxelAssets.MakeBox("MgBarrel", Turret, new Vector3(0.62f, 0.28f, 1.35f), new Vector3(0.12f, 0.12f, 1.0f), new Color(0.08f, 0.09f, 0.08f));
            var mg = new GameObject("MgMuzzle");
            mg.transform.SetParent(Turret, false);
            mg.transform.localPosition = new Vector3(0.62f, 0.28f, 1.9f);
            mg.transform.localRotation = Quaternion.identity;
            MgMuzzle = mg.transform;

            // 舱内第一人称眼位：指挥塔上方一点（低头能看到车体前装甲和炮管根部）
            var eye = new GameObject("TankEye");
            eye.transform.SetParent(Turret, false);
            eye.transform.localPosition = new Vector3(0f, 0.82f, 0.15f);
            eye.transform.localRotation = Quaternion.identity;
            EyeAnchor = eye.transform;

            BuildAimMarker();
        }

        /// <summary>落点指示器：车外视角下把炮弹会砸在哪儿画出来（十字 + 方框）。</summary>
        private void BuildAimMarker()
        {
            var go = new GameObject("AimMarker");
            go.transform.SetParent(null, false);
            go.transform.rotation = Quaternion.identity;
            aimMarker = go.transform;
            Color c = new Color(1f, 0.78f, 0.3f);
            // 中心十字
            var c1 = VoxelAssets.MakeBox("mk1", aimMarker, new Vector3(0f, 0f, 0f), new Vector3(0.7f, 0.05f, 0.05f), c);
            var c2 = VoxelAssets.MakeBox("mk2", aimMarker, new Vector3(0f, 0f, 0f), new Vector3(0.05f, 0.05f, 0.7f), c);
            // 外圈方框（四条边）
            var e1 = VoxelAssets.MakeBox("mk3", aimMarker, new Vector3(0f, 0f, 0.62f), new Vector3(1.25f, 0.05f, 0.05f), c);
            var e2 = VoxelAssets.MakeBox("mk4", aimMarker, new Vector3(0f, 0f, -0.62f), new Vector3(1.25f, 0.05f, 0.05f), c);
            var e3 = VoxelAssets.MakeBox("mk5", aimMarker, new Vector3(0.62f, 0f, 0f), new Vector3(0.05f, 0.05f, 1.25f), c);
            var e4 = VoxelAssets.MakeBox("mk6", aimMarker, new Vector3(-0.62f, 0f, 0f), new Vector3(0.05f, 0.05f, 1.25f), c);
            markerParts.Clear();
            var all = new[] { c1, c2, e1, e2, e3, e4 };
            for (int i = 0; i < all.Length; i++)
            {
                var r = all[i].GetComponent<Renderer>();
                if (r != null) markerParts.Add(r);
            }
            go.SetActive(false);
        }

        // ------------------------------------------------------------------ 驾驶（每帧由 GameManager 驱动）

        /// <summary>装甲归零后由 GameManager 每帧推进自爆倒计时；到点返回 true 表示"该炸了"。</summary>
        public bool TickDoom(float dt)
        {
            if (!Doomed || dead) return false;
            DoomTimer -= dt;
            beepCd -= dt;
            if (beepCd <= 0f)
            {
                // 越接近起爆提示越急促
                beepCd = DoomTimer > 1.5f ? 0.5f : 0.25f;
                AudioKit.Play("ui", transform.position + Vector3.up * 1.4f, 0.9f, 1.6f);
                if (FxPool.Instance != null)
                    FxPool.Instance.Flash(transform.position + Vector3.up * 1.8f, new Color(1f, 0.35f, 0.2f), 0.5f);
            }
            return DoomTimer <= 0f;
        }

        /// <param name="firePressed">左键按下沿（主炮用：一按一发，按住不会连发）。</param>
        /// <param name="fireHeld">左键按住（同轴机枪用：按住连发）。</param>
        public void Drive(Vector2 input, CameraRig rig, bool firePressed, bool fireHeld, float dt)
        {
            if (dead) return;
            fireCd -= dt;

            // 车体转向 + 前进/后退
            hullYaw += input.x * 42f * dt;
            float want = input.y * 8.2f;
            driveSpeed = Mathf.MoveTowards(driveSpeed, want, 9f * dt);

            Vector3 fwd = new Vector3(Mathf.Sin(hullYaw * Mathf.Deg2Rad), 0f, Mathf.Cos(hullYaw * Mathf.Deg2Rad));
            verticalVel -= 26f * dt;
            if (verticalVel < -40f) verticalVel = -40f;
            Vector3 vel = fwd * driveSpeed + Vector3.up * verticalVel;

            bool hitWall;
            // 坦克绝不爬台阶：撞到 1 格以上的地形就停住（不然会沿墙"跳"上去）
            Vector3 pos = World.MoveAABB(transform.position, 1.55f, 1.9f, vel * dt, out grounded, out hitWall, false);
            if (grounded) verticalVel = 0f;
            if (hitWall) driveSpeed *= 0.4f;
            transform.position = pos;
            transform.rotation = Quaternion.Euler(0f, hullYaw, 0f);

            // 炮塔朝相机方向（限速旋转，有"炮塔追瞄"的机械感；舱内视角转得更快，
            // 不然第一人称里炮管会明显跟不上准星）
            float turretRate = (rig != null && rig.TankFirstPerson) ? 300f : 130f;
            if (rig != null && Turret != null)
            {
                float wantLocal = Mathf.DeltaAngle(hullYaw, rig.Yaw);
                turretYaw = Mathf.MoveTowardsAngle(turretYaw, wantLocal, turretRate * dt);
                Turret.localRotation = Quaternion.Euler(0f, turretYaw, 0f);

                // 炮管不再死板地跟相机俯仰 —— 解算成"打中准星指的那个点"，
                // 第三人称下炮弹落点才对得上准星（这是之前落点最难瞄的根因）。
                float wantElev = SolveElevation(AimPoint(rig));
                barrelElev = Mathf.MoveTowards(barrelElev, Mathf.Clamp(wantElev, -14f, 42f), 90f * dt);
                if (Barrel != null) Barrel.localRotation = Quaternion.Euler(-barrelElev, 0f, 0f);
            }

            mgCd -= dt;
            if (!Doomed)
            {
                // 主炮一按一发，机枪按住连发
                if (Weapon == TankWeapon.Cannon) { if (firePressed) FireCannon(rig); }
                else if (fireHeld) FireMachineGun(rig);
            }

            // 落点指示器（只在车外视角显示，舱内视角有准星，不需要）
            UpdateAimMarker(rig);

            // 履带碾过谁，谁就得死（每一帧在车体移动之后判定）
            CrushEnemies(rig, dt);
        }

        /// <summary>切武器：dir = +1 下一个，-1 上一个（滚轮 / Q）。</summary>
        public void CycleWeapon(int dir)
        {
            int n = WeaponCount;
            int i = ((int)Weapon + dir) % n;
            if (i < 0) i += n;
            Weapon = (TankWeapon)i;
            AudioKit.Play2D("ui", 0.5f, i == 0 ? 0.8f : 1.25f);
        }

        /// <summary>数字键直选（0 = 主炮，1 = 同轴机枪）。</summary>
        public void SelectWeapon(int index)
        {
            if (index < 0 || index >= WeaponCount) return;
            Weapon = (TankWeapon)index;
            AudioKit.Play2D("ui", 0.5f, index == 0 ? 0.8f : 1.25f);
        }

        /// <summary>准星指着的那个世界点（地形交点，打空就给 70 米远的点）。</summary>
        private Vector3 AimPoint(CameraRig rig)
        {
            if (rig != null && World != null)
            {
                Ray ray = rig.AimRay();
                Vector3Int cell, n; Vector3 hit;
                if (World.RaycastBlocks(ray.origin, ray.direction, 110f, out hit, out cell, out n))
                    return hit;
                return ray.origin + ray.direction * 70f;
            }
            return transform.position + transform.forward * 30f;
        }

        /// <summary>
        /// 解算炮管仰角：让炮弹真正落在 target 上（含重力下坠补偿）。
        /// 炮弹初速 62、重力 4，30 米也就掉 0.5 米，补偿量很小但远射时看得出差别。
        /// </summary>
        private float SolveElevation(Vector3 target)
        {
            if (Muzzle == null) return 0f;
            Vector3 to = target - Muzzle.position;
            float flat = new Vector3(to.x, 0f, to.z).magnitude;
            if (flat < 0.01f) return 0f;
            float t = flat / ShellSpeed;
            float drop = 0.5f * ShellGravity * t * t;      // 飞行期间的下坠量
            return Mathf.Atan2(to.y + drop, flat) * Mathf.Rad2Deg;
        }

        /// <summary>按实际弹道积分算出炮弹会砸在哪（和 ProjectileSystem 的推进保持一致）。</summary>
        private Vector3 PredictImpact()
        {
            if (Muzzle == null) return transform.position;
            Vector3 pos = Muzzle.position;
            Vector3 vel = Muzzle.forward * ShellSpeed;
            const float step = 0.08f;   // 4.8 秒弹道足够（炮弹寿命 4 秒），别每帧扫太多次
            for (int i = 0; i < 60; i++)
            {
                vel.y -= ShellGravity * step;
                Vector3 next = pos + vel * step;
                Vector3 seg = next - pos;
                float len = seg.magnitude;
                if (len > 0.0001f && World != null)
                {
                    Vector3Int cell, n; Vector3 hit;
                    if (World.RaycastBlocks(pos, seg / len, len, out hit, out cell, out n)) return hit;
                }
                pos = next;
                if (pos.y < -2f) break;
            }
            return pos;
        }

        /// <summary>车外视角：把落点画在地上；装填中变灰，装甲损毁后隐藏。</summary>
        private void UpdateAimMarker(CameraRig rig)
        {
            if (aimMarker == null) return;
            bool show = Mounted && !dead && !Doomed
                && rig != null && !rig.TankFirstPerson
                && Weapon == TankWeapon.Cannon;
            if (aimMarker.gameObject.activeSelf != show) aimMarker.gameObject.SetActive(show);
            if (!show) return;

            Vector3 p = PredictImpact();
            aimMarker.position = p + Vector3.up * 0.08f;
            Color c = fireCd <= 0f ? new Color(1f, 0.78f, 0.30f) : new Color(0.42f, 0.44f, 0.46f);
            for (int i = 0; i < markerParts.Count; i++)
            {
                if (markerParts[i] != null) VoxelAssets.SetColor(markerParts[i], c);
            }
        }

        private void FireCannon(CameraRig rig)
        {
            if (fireCd > 0f || Muzzle == null) return;
            fireCd = 2.4f;
            Vector3 from = Muzzle.position;
            Vector3 dir = Muzzle.forward;
            // 直射伤害 260：步兵(110)/突击兵(80)/重装兵(240) 一炮带走，
            // 精英兵(460) 吃满直击 + 近距破片也还剩一口气 —— 想收人头得补一发。
            // 没直接蹭到人也别慌：炮弹有近炸引信，从敌人身边 1.2 米内飞过照样起爆。
            ProjectileSystem.FireShell(World, from, dir, ShellSpeed, 260f);
            AudioKit.Play("sniper", from, 1f, 0.42f);
            AudioKit.Play("shotgun", from, 0.8f, 0.5f);
            if (rig != null) rig.Shake(0.35f);
            if (FxPool.Instance != null)
            {
                FxPool.Instance.Flash(from + dir * 0.4f, new Color(1f, 0.8f, 0.4f), 0.9f);
                FxPool.Instance.Burst(from + dir * 0.5f, new Color(0.9f, 0.7f, 0.3f), 8, 1.4f);
            }
            var gm = GameManager.Instance;
            if (gm != null) gm.MakeNoise(from, 42f);
        }

        /// <summary>同轴机枪：射速快、伤害低、只打敌人、不拆地形（用友军弹通道）。</summary>
        private void FireMachineGun(CameraRig rig)
        {
            if (mgCd > 0f || MgMuzzle == null) return;
            mgCd = 0.085f;
            Vector3 from = MgMuzzle.position;
            Vector3 dir = MgMuzzle.forward;
            float spread = 1.1f;
            dir = Quaternion.AngleAxis(Random.Range(-spread, spread), Vector3.up) * dir;
            dir = Quaternion.AngleAxis(Random.Range(-spread, spread), MgMuzzle.right) * dir;

            ProjectileSystem.FirePlayerAlly(World, from, dir, 100f, 22f);
            AudioKit.Play("rifle", from, 0.28f, Random.Range(1.5f, 1.7f));
            if (FxPool.Instance != null) FxPool.Instance.Flash(from + dir * 0.3f, new Color(1f, 0.85f, 0.5f), 0.3f);
            if (rig != null) rig.Shake(0.035f);
            var gm = GameManager.Instance;
            if (gm != null && Random.value < 0.35f) gm.MakeNoise(from, 16f);
        }

        /// <summary>落点指示器是自己单独生成的对象（不挂在车体上），车没了要跟着收掉。</summary>
        private void OnDestroy()
        {
            if (aimMarker != null) Destroy(aimMarker.gameObject);
        }

        /// <summary>
        /// 碾压：车体 footprint（随车体转向的 OBB）里的敌人一律直接碾死，精英兵也不例外。
        /// 走的是正常击杀流程 —— 记分、连杀、掉落补给全都算；队友不会被自己人的车碾。
        /// </summary>
        private void CrushEnemies(CameraRig rig, float dt)
        {
            crushCd -= dt;
            var gm = GameManager.Instance;
            if (gm == null || gm.Enemies.Count == 0) return;

            float rad = hullYaw * Mathf.Deg2Rad;
            float fwdX = Mathf.Sin(rad), fwdZ = Mathf.Cos(rad);     // 车头方向
            float rgtX = Mathf.Cos(rad), rgtZ = -Mathf.Sin(rad);    // 车体右侧
            Vector3 p = transform.position;
            int n = 0;

            for (int i = 0; i < gm.Enemies.Count; i++)
            {
                var e = gm.Enemies[i];
                if (e == null || !e.Alive) continue;

                Vector3 ep = e.transform.position;
                // 高度差太大 = 在楼上 / 坑底 / 已经站在车顶了，压不到
                float dy = ep.y - p.y;
                if (dy < -1.6f || dy > 1.8f) continue;

                Vector3 rel = ep - p;
                float lz = rel.x * fwdX + rel.z * fwdZ;
                float lx = rel.x * rgtX + rel.z * rgtZ;
                if (Mathf.Abs(lx) > CrushHalfX || Mathf.Abs(lz) > CrushHalfZ) continue;

                e.TakeDamage(9999f, ep + Vector3.up * 0.9f, false, false);
                n++;
                if (FxPool.Instance != null)
                {
                    FxPool.Instance.Burst(ep + Vector3.up * 0.45f, new Color(0.75f, 0.16f, 0.14f), 10, 1.5f);
                    FxPool.Instance.Burst(ep + Vector3.up * 0.10f, new Color(0.32f, 0.30f, 0.26f), 6, 1.2f);
                }
            }

            if (n <= 0) return;
            AudioKit.Play("block", p + Vector3.up * 0.6f, 0.75f, 0.5f);   // 沉闷的"咔嚓"
            if (rig != null) rig.Shake(0.16f);
            if (crushCd <= 0f)
            {
                crushCd = 0.9f;
                gm.OnTankCrush(n);
            }
        }

        // ------------------------------------------------------------------ 受击

        /// <summary>
        /// 车体受击：只掉装甲，不掉玩家的血（玩家在车里时这条耐久就是他的"第二血条"）。
        /// 装甲归零不立刻爆 —— 进入 3 秒自爆倒计时，让驾驶员有时间跳车。
        /// </summary>
        public void TakeDamage(float d)
        {
            if (dead || Doomed) return;
            Hp -= d;
            var gm = GameManager.Instance;
            if (gm != null && gm.Hud != null && Hp > 0f && Random.value < 0.25f)
                gm.Hud.FlashHullHit();
            if (Hp > 0f) return;

            Hp = 0f;
            Doomed = true;
            DoomTimer = DoomDuration;
            beepCd = 0f;
            if (FxPool.Instance != null)
                FxPool.Instance.Burst(transform.position + Vector3.up * 1.6f, new Color(1f, 0.6f, 0.25f), 14, 1.6f);
            AudioKit.Play("explode", transform.position, 0.6f, 1.3f);
            if (gm != null) gm.OnTankDoomed();
        }
    }
}
