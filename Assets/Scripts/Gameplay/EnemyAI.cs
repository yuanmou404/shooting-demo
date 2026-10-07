using System.Collections.Generic;
using UnityEngine;

namespace PixelArena
{
    public enum EnemyKind { Grunt, Rusher, Heavy, Elite }

    /// <summary>
    /// 敌方方块人：巡逻 → 起疑 → 交火 → 失去目标后到"最后已知位置"搜索。
    /// 锁敌参考《最后生还者》：视野锥 + 距离衰减 + 掩体遮挡 + 警觉度累积，
    /// 只有"真的看见"才会被标记为发现；看不见时不会开天眼，只会在最后目击点附近
    /// 逐个可疑点搜索、左右环视，一段时间没结果再回到巡逻。另外会听枪声、会互相通报。
    /// </summary>
    public class EnemyAI : MonoBehaviour
    {
        public bool Alive = true;
        public float Hp = 100f;
        public float MaxHp = 100f;
        public EnemyKind Kind = EnemyKind.Grunt;

        public VoxelWorld World;
        public PlayerController Player;
        public BlockCharacter Model;

        private enum AiState { Patrol, Suspicious, Combat, Search }
        private AiState aiState = AiState.Patrol;

        private float speed;
        private float fireInterval;
        private float projDamage;
        private float projSpeed;
        private float keepDistance;

        // ---------------- 感知参数 ----------------
        private float viewDist = 27f;      // 视距（超过这个距离根本看不见）
        private float viewHalf = 55f;      // 视野锥半角（度）
        private float hearRadius = 20f;    // 听觉半径
        private float awareness;           // 警觉度 0~1，到 1 才算发现

        // ---------------- 射击能力（距离越远越打不准） ----------------
        private float fireRange = 22f;     // 超出这个距离直接不开火
        private float idealRange = 11f;    // 这个距离内基本是精准的
        private float nearSpread = 1.1f;   // 近距离散布（度）
        private float farSpread = 11f;     // 射程边缘的散布（度）
        private float aimDelay = 0.42f;    // 发现目标后的瞄准时间
        private float aimTimer;            // >0 表示还在瞄准
        private float turnRate = 140f;     // 当前转身角速度（度/秒）

        private float senseTimer;
        private bool canSee;               // 当前帧真的看得见（且已确认）
        private bool seesNow;              // 当前帧视野里有玩家（还没累积满警觉度）
        private Vector3 lastKnown;         // 最后目击 / 听到的位置
        private bool hasLastKnown;
        private float lostTime;            // 失去视野的累计时间
        private float spacing = 2.4f;      // 与同伴保持的最小间距（避免挤成一坨）
        private AllyAI visibleAlly;        // 视野里的队友（玩家看不见时会转而攻击他）
        private float allyDist;

        // ---------------- 巡逻 / 搜索 ----------------
        private Vector3 homePoint;
        private Vector3 patrolTarget;
        private float patrolWait;
        private Vector3 searchPoint;
        private bool hasSearchPoint;
        private int searchCount;
        private float searchTimer;
        private float scanTimer;           // 抵达可疑点后原地环视
        private float scanYaw;
        private float allyCd;

        // ---------------- 寻路 ----------------
        private readonly List<Vector3> path = new List<Vector3>();
        private int pathIdx;
        private Vector3 pathGoal;
        private float pathTimer;
        private bool hasPath;
        private static float pathBudgetThisFrame;   // 每帧最多做几次 A*（防止十几个人同帧重算卡帧）
        private static int pathBudgetFrame = -1;

        // ---------------- 运动 / 动画 ----------------
        private Vector3 velocity;
        private bool grounded;
        private float fireTimer;
        private float stuckTimer;
        private int stuckCount;
        private float strafeSign = 1f;
        private float aimPitchSmooth;      // 枪口俯仰也要平滑，别"啪"地一下抬起来
        private Vector3 lastPos;
        private Color shirt;
        private float hitFlash;
        private bool lastAttackerAlly;   // 最后一下是队友打的（影响小队积分归属）
        private Renderer[] skinRenderers;
        private Color[] skinColors;
        private static readonly MaterialPropertyBlock tmpBlock = new MaterialPropertyBlock();

        private static readonly Color[] ShirtColors =
        {
            new Color(0.70f, 0.22f, 0.20f),
            new Color(0.22f, 0.38f, 0.68f),
            new Color(0.30f, 0.62f, 0.30f),
            new Color(0.72f, 0.62f, 0.20f),
            new Color(0.55f, 0.28f, 0.62f)
        };

        public Vector3 AimPoint => transform.position + Vector3.up * 1.5f;
        public Vector3 BoundsMin => transform.position + new Vector3(-0.36f, 0f, -0.36f);
        public Vector3 BoundsMax => transform.position + new Vector3(0.36f, 1.82f, 0.36f);

        /// <summary>是否正在和玩家纠缠（交火 / 搜索中），供 GameManager 判断场面是否僵持。</summary>
        public bool Engaged => aiState == AiState.Combat || aiState == AiState.Search;
        public bool Hunting => aiState == AiState.Combat;

        public static EnemyAI Spawn(VoxelWorld world, PlayerController player, Transform parent, Vector3 pos, EnemyKind kind)
        {
            var go = new GameObject("Enemy_" + kind);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var ai = go.AddComponent<EnemyAI>();
            ai.World = world;
            ai.Player = player;
            ai.Kind = kind;

            Color shirt = ShirtColors[Random.Range(0, ShirtColors.Length)];
            Color pants = new Color(0.22f, 0.24f, 0.30f);
            Color skin = new Color(0.86f, 0.70f, 0.54f);
            Color hair = new Color(0.20f, 0.15f, 0.12f);
            if (kind == EnemyKind.Elite)
            {
                // 精英：血厚得多、枪法更好、射程更远、反应更快
                shirt = new Color(0.16f, 0.17f, 0.21f);
                pants = new Color(0.12f, 0.13f, 0.17f);
                ai.MaxHp = 460f; ai.speed = 4.0f; ai.fireInterval = 0.95f; ai.projDamage = 22f; ai.projSpeed = 40f; ai.keepDistance = 12f;
                ai.viewDist = 32f; ai.viewHalf = 62f; ai.hearRadius = 24f; ai.spacing = 3.0f;
                ai.fireRange = 30f; ai.idealRange = 15f; ai.nearSpread = 0.7f; ai.farSpread = 7f;
                ai.aimDelay = 0.30f; ai.turnRate = 200f;
                skin = new Color(0.80f, 0.66f, 0.52f);
                hair = new Color(0.10f, 0.10f, 0.12f);
            }
            else if (kind == EnemyKind.Heavy)
            {
                shirt = new Color(0.45f, 0.47f, 0.52f);
                ai.MaxHp = 240f; ai.speed = 2.7f; ai.fireInterval = 1.9f; ai.projDamage = 17f; ai.projSpeed = 30f; ai.keepDistance = 14f;
                ai.viewDist = 25f; ai.viewHalf = 48f; ai.hearRadius = 16f; ai.spacing = 3.2f;
                ai.fireRange = 26f; ai.idealRange = 14f; ai.nearSpread = 1.0f; ai.farSpread = 9f;
                ai.aimDelay = 0.55f; ai.turnRate = 95f;
            }
            else if (kind == EnemyKind.Rusher)
            {
                shirt = new Color(0.74f, 0.24f, 0.18f);
                ai.MaxHp = 80f; ai.speed = 5.6f; ai.fireInterval = 0.85f; ai.projDamage = 9f; ai.projSpeed = 34f; ai.keepDistance = 6f;
                ai.viewDist = 26f; ai.viewHalf = 66f; ai.hearRadius = 24f; ai.spacing = 1.9f;
                ai.fireRange = 16f; ai.idealRange = 7f; ai.nearSpread = 2.2f; ai.farSpread = 14f;
                ai.aimDelay = 0.28f; ai.turnRate = 190f;
            }
            else
            {
                ai.MaxHp = 110f; ai.speed = 3.6f; ai.fireInterval = 1.5f; ai.projDamage = 12f; ai.projSpeed = 32f; ai.keepDistance = 11f;
                ai.viewDist = 27f; ai.viewHalf = 56f; ai.hearRadius = 20f; ai.spacing = 2.5f;
                ai.fireRange = 22f; ai.idealRange = 11f; ai.nearSpread = 1.2f; ai.farSpread = 12f;
                ai.aimDelay = 0.45f; ai.turnRate = 140f;
            }
            // 每个人保持距离略有差异，站成一排会很假
            ai.keepDistance += Random.Range(-1.6f, 1.6f);
            if (ai.keepDistance < 3f) ai.keepDistance = 3f;
            ai.Hp = ai.MaxHp;
            ai.shirt = shirt;
            ai.Model = BlockCharacter.Create(go.transform, skin, shirt, pants, hair);
            ai.Model.transform.localPosition = Vector3.zero;
            ai.Model.ApplyNpcVariant(kind);                       // 体型/配件按兵种区分
            // 不同兵种拿不同的枪：步兵步枪、突击兵冲锋枪、重装兵/精英轻机枪
            ai.Model.SetWeapon(kind == EnemyKind.Rusher ? WeaponKind.SMG
                : (kind == EnemyKind.Heavy ? WeaponKind.LMG
                : (kind == EnemyKind.Elite ? WeaponKind.LMG : WeaponKind.Rifle)));
            ai.CacheColors();
            ai.lastPos = pos;
            ai.homePoint = pos;
            ai.patrolTarget = pos;
            ai.lastKnown = pos;
            ai.fireTimer = Random.Range(0.4f, 1.4f);
            ai.patrolWait = Random.Range(0.5f, 2.5f);
            ai.scanYaw = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            return ai;
        }

        private void Update()
        {
            if (!Alive) return;
            if (World == null || Player == null) return;
            // 主菜单 / 暂停 / 死亡时世界是"待机画面"：敌人不思考、不移动、不开火，
            // 否则站在菜单背景里的主角会被打得满天飞。
            var gm = GameManager.Instance;
            if (gm != null && !gm.SimulationRunning) return;
            Tick(Time.deltaTime);
        }

        // ------------------------------------------------------------------ 每帧

        public void Tick(float dt)
        {
            // 瞄准目标：优先玩家；玩家看不见但视野里有队友时打队友
            Vector3 aimPos = Player.Center;
            if (visibleAlly != null && visibleAlly.Alive && !canSee) aimPos = visibleAlly.AimPoint;
            Vector3 toPlayer = aimPos - transform.position;
            float dist = toPlayer.magnitude;

            allyCd -= dt;
            aimTimer -= dt;

            // 感知：10Hz 足够灵敏又省性能
            senseTimer -= dt;
            if (senseTimer <= 0f)
            {
                senseTimer = 0.1f;
                Sense(0.1f);
            }

            // ---------- 行为决策 ----------
            Vector3 wish = Vector3.zero;
            Vector3 faceDir = Vector3.zero;
            bool faceValid = false;

            if (aiState == AiState.Patrol) PatrolThink(dt, out wish, out faceDir, out faceValid);
            else if (aiState == AiState.Suspicious) SuspiciousThink(dt, toPlayer, out wish, out faceDir, out faceValid);
            else if (aiState == AiState.Combat) CombatThink(dt, toPlayer, out wish, out faceDir, out faceValid);
            else SearchThink(dt, out wish, out faceDir, out faceValid);

            wish = Separate(wish);
            // 走导航路径时不要再做"扇形试探"微调：那条备选方向会和路径打架，
            // 表现为贴着墙来回抖。只有在没有路径（退化成直线）+ 正面被挡时才救场。
            if (!hasPath || !PathFollowActive()) wish = AvoidObstacles(wish);

            // ---------- 卡住检测 ----------
            stuckTimer += dt;
            if (stuckTimer > 0.6f)
            {
                if ((transform.position - lastPos).sqrMagnitude < 0.02f && wish.sqrMagnitude > 0.1f)
                {
                    // 卡住了：立刻重算路线（可能是同伴挤住了，或者刚被破坏/新放的方块挡了路）。
                    // 绝对不许原地反复起跳 —— 面对高墙跳是跳不过去的，只会站在那抽风。
                    pathTimer = 0f;
                    hasPath = false;
                    strafeSign = -strafeSign;
                    stuckCount++;
                    if (stuckCount >= 3)
                    {
                        // 连着几次都走不动：这个地方八成过不去，换个目标点
                        stuckCount = 0;
                        if (aiState == AiState.Patrol)
                        {
                            patrolTarget = homePoint + Random.insideUnitSphere * 9f;
                            patrolTarget.y = homePoint.y;
                        }
                        else if (aiState == AiState.Search)
                        {
                            hasSearchPoint = false;
                            PickSearchPoint();
                        }
                        else
                        {
                            // 交火/起疑：别死盯着那个够不着的地方，先换个角度看能不能绕过去
                            pathGoal = Vector3.zero;
                            pathTimer = 0f;
                        }
                    }
                }
                else
                {
                    stuckCount = 0;
                }
                lastPos = transform.position;
                stuckTimer = 0f;
            }

            // ---------- 物理 ----------
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

            // 转身是"有速度"的：巡逻慢悠悠，交火时才会迅速转身。
            // 这同时决定了视野锥扫过玩家需要多久 —— 不会再出现"瞬间转身爆头"。
            float turnMul = 0.65f;
            if (aiState == AiState.Combat) turnMul = 1.35f;
            else if (aiState == AiState.Suspicious) turnMul = 0.95f;
            else if (aiState == AiState.Search) turnMul = 0.8f;
            if (faceValid && faceDir.sqrMagnitude > 0.0001f)
                Model.FaceTowards(faceDir, turnRate * turnMul, dt);

            // ---------- 动画 ----------
            float move01 = Mathf.Clamp01(new Vector2(velocity.x, velocity.z).magnitude / speed);
            // 巡逻/搜索时枪口压低（低姿戒备），只有起疑或交火才抬枪瞄准
            float wantPitch = -24f;
            if (aiState == AiState.Combat || aiState == AiState.Suspicious)
            {
                float flat = Mathf.Sqrt(toPlayer.x * toPlayer.x + toPlayer.z * toPlayer.z);
                if (flat > 0.001f)
                    wantPitch = Mathf.Clamp(Mathf.Atan2(toPlayer.y, flat) * Mathf.Rad2Deg, -60f, 60f);
            }
            aimPitchSmooth = Mathf.MoveTowards(aimPitchSmooth, wantPitch, dt * 150f);
            Model.Animate(move01, dt, true, aimPitchSmooth, false);

            // ---------- 开火（看得见 + 在射程内 + 已经瞄好了） ----------
            fireTimer -= dt;
            bool allyTarget = !canSee && visibleAlly != null && visibleAlly.Alive;
            if (aiState == AiState.Combat && dist <= fireRange && (canSee || allyTarget) &&
                aimTimer <= 0f && fireTimer <= 0f)
            {
                fireTimer = fireInterval * Random.Range(0.85f, 1.25f);
                ShootAt(allyTarget ? visibleAlly.AimPoint : Player.Center, dist, allyTarget);
            }

            // ---------- 受击闪白 ----------
            if (hitFlash > 0f)
            {
                hitFlash -= dt;
                float k = Mathf.Clamp01(hitFlash / 0.09f);
                for (int i = 0; i < skinRenderers.Length; i++)
                    VoxelAssets.SetColor(skinRenderers[i], Color.Lerp(skinColors[i], Color.white, k));
                if (hitFlash <= 0f) RestoreColors();
            }

            if (transform.position.y < -10f) Die(false);
        }

        // ------------------------------------------------------------------ 感知

        /// <summary>视野锥 + 遮挡 + 距离衰减的"看见玩家"判定，并按步长累积警觉度。</summary>
        private void Sense(float step)
        {
            Vector3 eyeTo = Player.Center - AimPoint;
            float d = eyeTo.magnitude;
            bool visible = false;

            if (d <= viewDist + 2f)
            {
                // 蹲下/滑铲更难被看到，冲刺更容易暴露
                float eff = d * (Player.IsCrouching ? 1.5f : 1f) * (Player.IsSprinting ? 0.85f : 1f);
                if (eff <= viewDist)
                {
                    Vector3 flat = new Vector3(eyeTo.x, 0f, eyeTo.z);
                    Vector3 face = Model != null ? Model.transform.forward : transform.forward;
                    face.y = 0f;
                    float ang = (flat.sqrMagnitude > 0.01f && face.sqrMagnitude > 0.01f)
                        ? Vector3.Angle(face, flat) : 180f;
                    // 贴脸时不看朝向也会察觉（近距离感知）
                    bool inCone = ang <= viewHalf || d < 3.6f;
                    if (inCone && World.HasLineOfSight(AimPoint, Player.Center))
                        visible = true;
                }
            }

            // 玩家看不见时扫一眼有没有队友在视野里（队友也是合法目标，但 canSee 仍只代表玩家）
            visibleAlly = null;
            {
                var gmA = GameManager.Instance;
                if (gmA != null && gmA.Allies.Count > 0)
                {
                    AllyAI best = null;
                    float bd = float.MaxValue;
                    for (int i = 0; i < gmA.Allies.Count; i++)
                    {
                        var a = gmA.Allies[i];
                        if (a == null || !a.Alive) continue;
                        Vector3 toA = a.AimPoint - AimPoint;
                        float ad = toA.magnitude;
                        if (ad > viewDist) continue;
                        Vector3 flatA = new Vector3(toA.x, 0f, toA.z);
                        Vector3 faceA = Model != null ? Model.transform.forward : transform.forward;
                        faceA.y = 0f;
                        float angA = (flatA.sqrMagnitude > 0.01f && faceA.sqrMagnitude > 0.01f)
                            ? Vector3.Angle(faceA, flatA) : 180f;
                        if (angA > viewHalf && ad > 3.6f) continue;
                        if (!World.HasLineOfSight(AimPoint, a.AimPoint)) continue;
                        if (ad < bd) { bd = ad; best = a; }
                    }
                    if (best != null)
                    {
                        visibleAlly = best;
                        allyDist = bd;
                        lastKnown = best.transform.position;
                        hasLastKnown = true;
                        if (!visible)
                        {
                            // 看见队友也会积累警觉（进 Combat），但 canSee 只认玩家
                            float rateA = Mathf.Lerp(2.6f, 0.55f, Mathf.Clamp01(bd / viewDist));
                            awareness = Mathf.Clamp01(awareness + rateA * step);
                            lostTime = 0f;
                        }
                    }
                }
            }

            if (visible)
            {
                float rate = Mathf.Lerp(2.6f, 0.55f, Mathf.Clamp01(d / viewDist));
                awareness = Mathf.Clamp01(awareness + rate * step);
                lastKnown = Player.transform.position;
                hasLastKnown = true;
                lostTime = 0f;
            }
            else
            {
                // 交火中记忆更久，巡逻时很快忘掉
                awareness = Mathf.Clamp01(awareness - (aiState == AiState.Combat ? 0.18f : 0.5f) * step);
                lostTime += step;
            }

            canSee = visible && awareness >= 0.999f;
            seesNow = visible;
            ApplyAwarenessState();
        }

        /// <summary>警觉度 → 状态迁移。</summary>
        private void ApplyAwarenessState()
        {
            if (awareness >= 0.999f)
            {
                if (aiState != AiState.Combat)
                {
                    aiState = AiState.Combat;
                    searchTimer = 0f;
                    scanTimer = 0f;
                    aimTimer = aimDelay;     // 发现到开火之间要有一个抬枪瞄准的过程
                    AudioKit.Play("spot", transform.position, 0.8f);
                    CallAllies();
                }
            }
            else if (awareness > 0.15f)
            {
                if (aiState == AiState.Patrol) aiState = AiState.Suspicious;
                // 从交火掉到"拿不准"：转搜索
                if (aiState == AiState.Combat && lostTime > 1.6f) { aiState = AiState.Search; BeginSearch(11f); }
            }
            else
            {
                if (aiState == AiState.Suspicious) { aiState = AiState.Search; BeginSearch(8f); }
                else if (aiState == AiState.Combat && lostTime > 3.0f) { aiState = AiState.Search; BeginSearch(14f); }
                else if (aiState == AiState.Search && searchTimer <= 0f)
                {
                    aiState = AiState.Patrol;
                    patrolTarget = homePoint;
                    patrolWait = Random.Range(0.5f, 2f);
                }
            }
        }

        private void BeginSearch(float time)
        {
            searchTimer = time;
            searchCount = 0;
            hasSearchPoint = false;
            PickSearchPoint();
        }

        /// <summary>在最后目击点附近挑一个可疑点（越找越远）。</summary>
        private void PickSearchPoint()
        {
            Vector3 center = hasLastKnown ? lastKnown : homePoint;
            float r = 4f + searchCount * 3.2f;
            Vector3 p = center + Random.insideUnitSphere * Mathf.Min(r, 12f);
            p.y = center.y;
            int ix = Mathf.Clamp(Mathf.FloorToInt(p.x), 2, World.SX - 3);
            int iz = Mathf.Clamp(Mathf.FloorToInt(p.z), 2, World.SZ - 3);
            float y = World.GroundHeight(ix, iz, World.SY - 1);
            searchPoint = new Vector3(ix + 0.5f, y + 0.05f, iz + 0.5f);
            hasSearchPoint = true;
            searchCount++;
            scanTimer = 0f;
        }

        /// <summary>发现玩家时向附近同伴通报（最后生还者里敌人会互相喊话）。</summary>
        private void CallAllies()
        {
            if (allyCd > 0f) return;
            allyCd = 1.5f;
            var gm = GameManager.Instance;
            if (gm == null) return;
            for (int i = 0; i < gm.Enemies.Count; i++)
            {
                var a = gm.Enemies[i];
                if (a == null || a == this || !a.Alive) continue;
                if (Vector3.Distance(a.transform.position, transform.position) > 26f) continue;
                a.OnAllySpotted(lastKnown);
            }
        }

        public void OnAllySpotted(Vector3 pos)
        {
            lastKnown = pos; hasLastKnown = true;
            if (aiState == AiState.Combat) return;
            awareness = Mathf.Max(awareness, 0.55f);
            aiState = AiState.Search;
            BeginSearch(12f);
        }

        /// <summary>听到动静（枪声 / 破块）：只知道"大概方位"，过去查看但不会直接锁定。</summary>
        public void HearNoise(Vector3 pos, float radius)
        {
            float d = Vector3.Distance(transform.position, pos);
            if (d > radius) return;
            // 声音定位是有误差的：越远越不准。否则敌人会像开了地图外挂一样直奔玩家脚下。
            lastKnown = FuzzyNear(pos, 0f, Mathf.Min(4.5f, d * 0.18f));
            hasLastKnown = true;
            awareness = Mathf.Clamp01(awareness + Mathf.Lerp(0.8f, 0.22f, Mathf.Clamp01(d / radius)));
            if (aiState != AiState.Combat)
            {
                aiState = AiState.Search;
                BeginSearch(Mathf.Max(searchTimer, 12f));
            }
            ApplyAwarenessState();
        }

        /// <summary>在 center 附近随机取一个点（minR~maxR 的环内），用于"模糊信息"。</summary>
        public static Vector3 FuzzyNear(Vector3 center, float minR, float maxR)
        {
            float a = Random.value * Mathf.PI * 2f;
            float r = Random.Range(minR, maxR);
            return new Vector3(center.x + Mathf.Cos(a) * r, center.y, center.z + Mathf.Sin(a) * r);
        }

        /// <summary>
        /// 给一个"可疑的位置"。注意：调用方给的是模糊线索（GameManager 防僵持 / 出生提示），
        /// 不要传玩家的精确坐标，否则等于给敌人开透视。
        /// </summary>
        public void SetHint(Vector3 p)
        {
            lastKnown = p;
            hasLastKnown = true;
            if (aiState != AiState.Combat)
            {
                aiState = AiState.Search;
                BeginSearch(14f);
            }
        }

        // ------------------------------------------------------------------ 各状态

        private void PatrolThink(float dt, out Vector3 wish, out Vector3 faceDir, out bool faceValid)
        {
            wish = Vector3.zero; faceDir = Vector3.zero; faceValid = false;
            patrolWait -= dt;
            bool arrived = (transform.position - patrolTarget).sqrMagnitude < 4f;
            if (arrived || patrolWait <= 0f)
            {
                patrolWait = Random.Range(3f, 6f);
                patrolTarget = homePoint + Random.insideUnitSphere * 9f;
                patrolTarget.y = homePoint.y;
                scanTimer = arrived ? Random.Range(0.8f, 1.8f) : 0f;
            }
            if (scanTimer > 0f)
            {
                // 停下四处张望
                scanTimer -= dt;
                scanYaw += dt * 55f;
                faceDir = new Vector3(Mathf.Sin(scanYaw), 0f, Mathf.Cos(scanYaw));
                faceValid = true;
                return;
            }
            // 沿巡航线走：走着走着会自然而然绕过箱子 / 隔断
            wish = Navigate(patrolTarget, 0.42f, out faceDir, dt);
            faceValid = faceDir.sqrMagnitude > 0.0001f;
        }

        private void SuspiciousThink(float dt, Vector3 toPlayer, out Vector3 wish, out Vector3 faceDir, out bool faceValid)
        {
            // 起疑：只盯"最后有线索的地方"。看不见玩家时不许看着玩家的真实坐标转头/前进。
            Vector3 focus = seesNow ? Player.transform.position
                                    : (hasLastKnown ? lastKnown : homePoint);
            Vector3 f = new Vector3(focus.x - transform.position.x, 0f, focus.z - transform.position.z);
            float flat = f.magnitude;
            faceDir = f;
            faceValid = flat > 0.001f;
            // 慢慢挪过去确认（玩家能看到敌人转头，留出反应时间）
            wish = flat > 6.5f ? Navigate(focus, 0.30f, out faceDir, dt) : Vector3.zero;
            faceValid = faceDir.sqrMagnitude > 0.0001f;
        }

        private void CombatThink(float dt, Vector3 toPlayer, out Vector3 wish, out Vector3 faceDir, out bool faceValid)
        {
            Vector3 flat = new Vector3(toPlayer.x, 0f, toPlayer.z);
            float flatDist = flat.magnitude;

            if (canSee && flatDist > 0.001f)
            {
                // 看得见才允许"看着玩家走位/射击"
                faceDir = flat;
                faceValid = true;
                Vector3 nd = flat / flatDist;
                if (flatDist < keepDistance - 1.5f) wish = -nd * speed * 0.7f;      // 太近，后退
                else if (flatDist > keepDistance + 3f) wish = nd * speed;           // 太远，靠近
                else
                {
                    Vector3 side = Vector3.Cross(Vector3.up, nd).normalized;         // 保持距离时横向绕行
                    wish = side * speed * 0.5f * strafeSign;
                }
            }
            else
            {
                // 失去视野：只面向并压向最后目击点，试图重新建立视线
                // （不能用玩家的真实坐标，否则等于透视）
                wish = Navigate(hasLastKnown ? lastKnown : homePoint, 0.9f, out faceDir, dt);
                faceValid = faceDir.sqrMagnitude > 0.0001f;
            }
        }

        private void SearchThink(float dt, out Vector3 wish, out Vector3 faceDir, out bool faceValid)
        {
            wish = Vector3.zero; faceDir = Vector3.zero; faceValid = false;
            searchTimer -= dt;
            if (!hasSearchPoint) PickSearchPoint();

            if (scanTimer > 0f)
            {
                // 到了可疑点，站在原地左右环视
                scanTimer -= dt;
                scanYaw += dt * 75f;
                faceDir = new Vector3(Mathf.Sin(scanYaw), 0f, Mathf.Cos(scanYaw));
                faceValid = true;
                return;
            }

            Vector3 flat = searchPoint - transform.position;
            flat.y = 0f;
            if (flat.magnitude < 1.6f)
            {
                if (searchCount >= 4)
                {
                    // 找够了：就地再望两秒，然后回巡逻
                    aiState = AiState.Patrol;
                    patrolTarget = homePoint;
                    patrolWait = 1f;
                    return;
                }
                scanTimer = Random.Range(1.1f, 2.0f);
                PickSearchPoint();
                return;
            }
            wish = Navigate(searchPoint, 0.72f, out faceDir, dt);
            faceValid = faceDir.sqrMagnitude > 0.0001f;
        }

        // ------------------------------------------------------------------ 寻路导航

        /// <summary>
        /// 沿导航网格走向目标：遇到墙会绕过去，而不是贴着墙一路蹭。
        /// 返回期望速度，faceDir 是"实际要走的方向"（也就是下一次到哪个航点），
        /// 敌人会朝着自己走的方向看 —— 视野锥因此会自然扫到拐角另一侧。
        /// </summary>
        private Vector3 Navigate(Vector3 target, float mul, out Vector3 faceDir, float dt)
        {
            faceDir = Vector3.zero;
            pathTimer -= dt;

            // 目标挪远了 / 到点了 / 上一条路用完了 → 重新算一次
            if (!hasPath || pathTimer <= 0f || (target - pathGoal).sqrMagnitude > 6.25f)
            {
                Repath(target);
                pathTimer = 0.55f + Random.Range(0f, 0.3f);
            }

            if (PathFollowActive())
            {
                for (int guard = 0; guard < 4 && pathIdx < path.Count; guard++)
                {
                    Vector3 wp = path[pathIdx];
                    Vector3 d = wp - transform.position;
                    d.y = 0f;
                    if (d.magnitude < 0.7f) { pathIdx++; continue; }   // 这个航点到了，去下一个
                    faceDir = d.normalized;
                    return faceDir * speed * mul;
                }
                // 航点都走完了
                hasPath = false;
                pathTimer = 0f;
            }

            // 兜底：没有可用路径（地图确实不通）时就退回直线 + 贴墙绕行
            Vector3 w = Seek(target, mul, out faceDir);
            // 面前是 2 格以上的高墙就别硬顶了（顶也上不去，只会原地磨）
            if (World != null && w.sqrMagnitude > 0.01f &&
                World.WallHeightAhead(transform.position, w, 0.34f) >= 2)
            {
                pathTimer = 0f;                 // 立刻重算一次路线，换条能走的路
                return w * 0.25f;
            }
            return w;
        }

        private bool PathFollowActive()
        {
            return hasPath && path.Count > 0 && pathIdx < path.Count;
        }

        private void Repath(Vector3 target)
        {
            hasPath = false;
            pathIdx = 0;
            pathGoal = target;
            if (World == null || World.Nav == null) return;
            if (!BudgetOk()) return;                  // 这一帧的重算名额用完了，下次再说

            bool exact = World.Nav.FindPath(transform.position, target, path);
            // FindPath 走不通时也会给"能走得最近的那条路"，照样能用
            hasPath = path.Count > 0;
            if (!exact && hasPath)
            {
                // 目标不可达：走到最接近的位置就够了，别在原地打转
                Vector3 last = path[path.Count - 1];
                if ((last - transform.position).sqrMagnitude < 1.4f) hasPath = false;
            }
        }

        /// <summary>每帧最多让 4 个敌人重算路径，避免十几个敌人同帧跑 A* 造成顿卡。</summary>
        private static bool BudgetOk()
        {
            if (Time.frameCount != pathBudgetFrame)
            {
                pathBudgetFrame = Time.frameCount;
                pathBudgetThisFrame = 0f;
            }
            if (pathBudgetThisFrame >= 4f) return false;
            pathBudgetThisFrame += 1f;
            return true;
        }

        /// <summary>朝目标点走的期望速度（同时给出朝向）。</summary>
        private Vector3 Seek(Vector3 target, float mul, out Vector3 faceDir)
        {
            Vector3 d = target - transform.position;
            d.y = 0f;
            float len = d.magnitude;
            if (len < 0.001f) { faceDir = Vector3.zero; return Vector3.zero; }
            faceDir = d / len;
            return faceDir * speed * mul;
        }

        // ------------------------------------------------------------------ 辅助

        /// <summary>互相排斥：避免几个敌人挤在同一个格子里，模型互相穿模。</summary>
        private Vector3 Separate(Vector3 wish)
        {
            var gm = GameManager.Instance;
            if (gm == null) return wish;
            var list = gm.Enemies;
            Vector3 push = Vector3.zero;
            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var o = list[i];
                if (o == null || o == this || !o.Alive) continue;
                Vector3 d = transform.position - o.transform.position;
                d.y = 0f;
                float m = d.magnitude;
                if (m > spacing) continue;
                if (m > 0.08f) push += d / m * (spacing - m);
                else
                {
                    // 完全重叠：朝随机方向推开
                    float a = Random.value * Mathf.PI * 2f;
                    push += new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * spacing * 0.6f;
                }
                n++;
            }
            if (n == 0) return wish;
            Vector3 sep = push * 2.4f;
            float cap = Mathf.Max(speed * 1.3f, 1.6f);
            if (sep.magnitude > cap) sep = sep.normalized * cap;
            return wish + sep;
        }

        /// <summary>前方被墙挡住时向两侧偏转，避免贴着掩体原地磨蹭。</summary>
        private Vector3 AvoidObstacles(Vector3 wish)
        {
            if (wish.sqrMagnitude < 0.01f) return wish;
            Vector3 nd = wish.normalized;
            if (FreeAhead(nd, 1.1f)) return wish;

            for (int k = 1; k <= 5; k++)
            {
                float a = k * 30f;
                Vector3 d1 = Quaternion.Euler(0f, a, 0f) * nd;
                if (FreeAhead(d1, 1.1f)) return d1 * wish.magnitude;
                Vector3 d2 = Quaternion.Euler(0f, -a, 0f) * nd;
                if (FreeAhead(d2, 1.1f)) return d2 * wish.magnitude;
            }
            return wish;
        }

        private bool FreeAhead(Vector3 d, float len)
        {
            int x = Mathf.FloorToInt(transform.position.x + d.x * len);
            int z = Mathf.FloorToInt(transform.position.z + d.z * len);
            int y = Mathf.FloorToInt(transform.position.y + 0.1f);
            return !World.IsSolidAt(x, y, z) && !World.IsSolidAt(x, y + 1, z);
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

        // ------------------------------------------------------------------ 战斗

        private void Shoot(float dist)
        {
            ShootAt(Player.Center, dist, false);
        }

        /// <summary>
        /// 开火（目标是玩家或队友）。命中率随距离快速衰减：在 idealRange 内基本打得很准（散布 ~1°），
        /// 越接近 fireRange 越离谱（散布 ~10°+），再加上子弹本身的飞行时间/下坠，
        /// 所以"隔着大半个地图被一枪点名"是不会再发生的。
        /// </summary>
        private void ShootAt(Vector3 targetCenter, float dist, bool atAlly)
        {
            Vector3 from = AimPoint + Model.transform.forward * 0.5f;
            Vector3 to = targetCenter;
            Vector3 dir = (to - from).normalized;

            float t = Mathf.Clamp01((dist - idealRange * 0.6f) / Mathf.Max(1f, fireRange - idealRange * 0.6f));
            float spread = Mathf.Lerp(nearSpread, farSpread, t * t);
            // 玩家全速奔跑 / 蹲伏不动都会影响被命中的概率（打队友没有这些修正）
            if (!atAlly)
            {
                if (Player.IsSprinting) spread *= 1.35f;
                if (Player.IsCrouching) spread *= 1.15f;
            }

            if (spread > 0.001f)
            {
                // 在以 dir 为轴、半角 = spread 的圆锥里随机偏一发
                Vector3 upRef = Mathf.Abs(dir.y) > 0.95f ? Vector3.forward : Vector3.up;
                Vector3 right = Vector3.Cross(dir, upRef).normalized;
                Vector3 upV = Vector3.Cross(right, dir).normalized;
                float a = Random.value * Mathf.PI * 2f;
                float deg = Mathf.Sqrt(Random.value) * spread;          // 圆锥内均匀分布
                Vector3 axis = right * Mathf.Cos(a) + upV * Mathf.Sin(a);
                dir = Quaternion.AngleAxis(deg, axis) * dir;
            }

            ProjectileSystem.Fire(World, from, dir, projSpeed, projDamage);
            AudioKit.Play("rifle", from, 0.35f, Random.Range(1.15f, 1.3f));
            if (FxPool.Instance != null) FxPool.Instance.Flash(from, new Color(1f, 0.7f, 0.35f), 0.18f);
        }

        /// <param name="byAlly">这一下是队友打出来的（击杀记小队战果，但积分打折）。</param>
        public void TakeDamage(float dmg, Vector3 point, bool head, bool byAlly = false)
        {
            if (!Alive) return;
            Hp -= dmg;
            hitFlash = 0.08f;
            lastAttackerAlly = byAlly;
            // 挨打必然警觉：知道方位但不一定知道人（没有视线就转入搜索）
            awareness = 1f;
            lastKnown = point;
            hasLastKnown = true;
            if (aiState != AiState.Combat)
            {
                aiState = AiState.Combat;
                lostTime = 0f;
                aimTimer = Mathf.Max(aimTimer, aimDelay * 0.6f);
                CallAllies();
            }
            if (Hp <= 0f) Die(true);
        }

        public void Die(bool exploded)
        {
            if (!Alive) return;
            Alive = false;
            if (FxPool.Instance != null)
            {
                FxPool.Instance.Burst(transform.position + Vector3.up * 0.9f, shirt, 16, 1.3f);
                FxPool.Instance.Burst(transform.position + Vector3.up * 1.5f, new Color(0.85f, 0.70f, 0.55f), 6, 1.0f);
            }
            AudioKit.Play("die", transform.position, 0.8f, Random.Range(0.9f, 1.1f));
            Model.SetVisible(false);
            var gm = GameManager.Instance;
            if (gm != null) gm.OnEnemyKilled(this, lastAttackerAlly);
            Destroy(gameObject, 1.2f);
        }
    }

    /// <summary>敌人子弹（可见的飞行方块）。</summary>
    public class Projectile
    {
        public Transform t;
        public Renderer r;
        public Vector3 vel;
        public float damage;
        public float gravity;
        public float life;
        public bool active;
        public bool ally;        // 友军弹（玩家/队友/坦克）：打敌人
        public bool explosive;   // 爆破弹（坦克主炮）：命中起爆
        public bool allyCredit;  // 击杀算"队友打的"（小队积分打折）；玩家自己在坦克上开的不打折
        public float scale = 1f;
    }

    public static class ProjectileSystem
    {
        private const int Max = 96;
        // 坦克主炮的爆破参数（范围比之前大一圈：7 米半径、170 破片、2.8 米拆块）
        public const float ShellRadius = 7.0f;
        public const float ShellDamage = 170f;
        public const float ShellBlockR = 2.8f;
        // 近炸引信：炮弹从敌人身边这个距离内飞过就起爆。
        // 炮口比人高，没有它的话平射炮弹会直接从头顶飞过去，玩家会觉得"只有落点才有伤害"。
        private const float ProximityFuse = 1.25f;
        private static readonly Projectile[] pool = new Projectile[Max];
        private static int cursor;
        private static Transform root;

        public static void Fire(VoxelWorld world, Vector3 from, Vector3 dir, float speed, float damage)
        {
            FireCore(from, dir, speed, damage, new Color(1f, 0.55f, 0.2f), 1f, false, false, -3.2f, 3.5f, false);
        }

        /// <summary>队友的子弹：青色 tracer，只打敌人。</summary>
        public static void FireAlly(VoxelWorld world, Vector3 from, Vector3 dir, float speed, float damage)
        {
            FireCore(from, dir, speed, damage, new Color(0.45f, 0.85f, 1f), 1f, true, false, -3.2f, 3.5f, true);
        }

        /// <summary>玩家自己在坦克上打出来的（同轴机枪）：同样只打敌人，但击杀记满分不打折。</summary>
        public static void FirePlayerAlly(VoxelWorld world, Vector3 from, Vector3 dir, float speed, float damage)
        {
            FireCore(from, dir, speed, damage, new Color(0.55f, 0.9f, 1f), 1f, true, false, -3.2f, 3.5f, false);
        }

        /// <summary>
        /// 喷火器的火焰弹：橙色团，飞不远。
        /// 尺寸/存活时间都刻意压小 —— 太大团会在第一人称糊住整个准星视野。
        /// </summary>
        public static void FireFlame(VoxelWorld world, Vector3 from, Vector3 dir, float speed, float damage)
        {
            FireCore(from, dir, speed, damage, new Color(1f, 0.5f, 0.12f), 1.15f, true, false, -2.5f, 0.38f, false);
        }

        /// <summary>
        /// 坦克主炮的爆破弹：撞方块、蹭到敌人（近炸引信）或直击敌人都会起爆。
        /// 重力要和 TankController.ShellGravity 一致，否则落点预测会算歪。
        /// </summary>
        public static void FireShell(VoxelWorld world, Vector3 from, Vector3 dir, float speed, float damage)
        {
            FireCore(from, dir, speed, damage, new Color(1f, 0.9f, 0.6f), 2.8f, true, true, -4f, 4f, false);
        }

        private static void FireCore(Vector3 from, Vector3 dir, float speed, float damage, Color color,
            float scale, bool ally, bool explosive, float gravity, float life, bool allyCredit)
        {
            if (root == null)
            {
                var go = new GameObject("Projectiles");
                root = go.transform;
                for (int i = 0; i < Max; i++)
                {
                    var b = VoxelAssets.MakeBox("proj", root, Vector3.zero, new Vector3(0.16f, 0.16f, 0.32f), new Color(1f, 0.55f, 0.2f), VoxelAssets.Unlit);
                    b.SetActive(false);
                    pool[i] = new Projectile { t = b.transform, r = b.GetComponent<Renderer>(), active = false };
                }
            }
            var p = pool[cursor];
            cursor = (cursor + 1) % Max;
            p.t.position = from;
            p.t.rotation = Quaternion.LookRotation(dir);
            p.t.localScale = new Vector3(scale, scale, Mathf.Max(1f, scale));
            p.vel = dir * speed;
            p.damage = damage;
            p.gravity = gravity;
            p.life = life;
            p.active = true;
            p.ally = ally;
            p.explosive = explosive;
            p.allyCredit = allyCredit;
            p.scale = scale;
            p.t.gameObject.SetActive(true);
            VoxelAssets.SetColor(p.r, color);
        }

        /// <summary>点到线段的距离（近炸引信用：判断这一小段弹道离敌人有多近）。</summary>
        private static float DistPointSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float l2 = ab.sqrMagnitude;
            if (l2 < 0.000001f) return Vector3.Distance(p, a);
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2);
            return Vector3.Distance(p, a + ab * t);
        }

        /// <summary>回收所有在飞的子弹（回主菜单 / 重开时用）。</summary>
        public static void Clear()
        {
            if (root == null) return;
            for (int i = 0; i < Max; i++)
            {
                var p = pool[i];
                if (p == null) continue;
                p.active = false;
                if (p.t != null) p.t.gameObject.SetActive(false);
            }
            cursor = 0;
        }

        public static void Update(VoxelWorld world, PlayerController player)
        {
            if (root == null) return;
            float dt = Time.deltaTime;
            var gm = GameManager.Instance;
            for (int i = 0; i < Max; i++)
            {
                var p = pool[i];
                if (!p.active) continue;
                p.life -= dt;
                if (p.life <= 0f) { p.active = false; p.t.gameObject.SetActive(false); continue; }

                p.vel.y += p.gravity * dt;
                Vector3 next = p.t.position + p.vel * dt;
                Vector3 seg = next - p.t.position;
                float len = seg.magnitude;
                Vector3 dir = len > 0.0001f ? seg / len : Vector3.forward;

                // 撞方块
                Vector3Int cell; Vector3Int n; Vector3 hit;
                if (world != null && world.RaycastBlocks(p.t.position, dir, len, out hit, out cell, out n))
                {
                    if (p.explosive)
                    {
                        if (gm != null) gm.ExplodeAt(hit, ShellRadius, ShellDamage, ShellBlockR);
                    }
                    else if (FxPool.Instance != null) FxPool.Instance.Burst(hit, new Color(1f, 0.7f, 0.3f), 3, 0.5f);
                    p.active = false; p.t.gameObject.SetActive(false);
                    continue;
                }

                if (p.ally)
                {
                    // 友军弹：只打敌人
                    if (gm != null)
                    {
                        // 爆破弹先走近炸引信：贴着敌人飞过就炸（炮口比人高，
                        // 没有这条的话平射炮弹会从头顶掠过，看起来"只有落点才有伤害"）
                        if (p.explosive)
                        {
                            EnemyAI near = null;
                            Vector3 at = Vector3.zero;
                            float best = ProximityFuse;
                            for (int k = 0; k < gm.Enemies.Count; k++)
                            {
                                var e = gm.Enemies[k];
                                if (e == null || !e.Alive) continue;
                                Vector3 c = e.transform.position;
                                for (int s = 0; s < 3; s++)
                                {
                                    // 小腿 / 胸口 / 头 三个采样点，等价于一根竖直胶囊
                                    Vector3 probe = c + Vector3.up * (0.45f + s * 0.6f);
                                    float d = DistPointSegment(probe, p.t.position, next);
                                    if (d < best) { best = d; near = e; at = probe; }
                                }
                            }
                            if (near != null)
                            {
                                near.TakeDamage(p.damage, at, false, false);
                                gm.ExplodeAt(at, ShellRadius, ShellDamage, ShellBlockR);
                                p.active = false; p.t.gameObject.SetActive(false);
                                continue;
                            }
                        }

                        for (int k = 0; k < gm.Enemies.Count; k++)
                        {
                            var e = gm.Enemies[k];
                            if (e == null || !e.Alive) continue;
                            float d;
                            if (RayMath.RayAABB(p.t.position, dir, e.BoundsMin, e.BoundsMax, out d) && d <= len)
                            {
                                Vector3 hp = p.t.position + dir * d;
                                if (p.explosive)
                                {
                                    // 直击伤害（秒杀除精英外的所有兵种）+ 破片范围伤害
                                    e.TakeDamage(p.damage, hp, false, false);
                                    gm.ExplodeAt(hp, ShellRadius, ShellDamage, ShellBlockR);
                                }
                                else
                                {
                                    // 队友打出来的算小队战果（积分打 6 折）；玩家自己开的不打折
                                    e.TakeDamage(p.damage, hp, false, p.allyCredit);
                                    if (p.scale > 1.05f && FxPool.Instance != null)
                                        FxPool.Instance.Flash(hp, new Color(1f, 0.55f, 0.2f), 0.4f);   // 火焰弹小爆闪
                                    else if (FxPool.Instance != null)
                                        FxPool.Instance.Burst(hp, new Color(0.7f, 0.15f, 0.12f), 4, 0.7f);
                                }
                                p.active = false; p.t.gameObject.SetActive(false);
                                break;
                            }
                        }
                    }
                    if (!p.active) continue;
                }
                else
                {
                    // 敌方弹：先看坦克（玩家在车里时优先命中车体）
                    if (gm != null && gm.TankMounted && gm.Tank != null)
                    {
                        float d;
                        Bounds tb = gm.Tank.TankBounds;
                        if (RayMath.RayAABB(p.t.position, dir, tb.min, tb.max, out d) && d <= len)
                        {
                            gm.Tank.TakeDamage(p.damage * 0.8f);
                            if (FxPool.Instance != null)
                                FxPool.Instance.Burst(p.t.position + dir * d, new Color(1f, 0.7f, 0.3f), 3, 0.5f);
                            p.active = false; p.t.gameObject.SetActive(false);
                            continue;
                        }
                    }

                    // 撞玩家
                    if (player != null)
                    {
                        float d;
                        Bounds b = player.WorldBounds;
                        if (RayMath.RayAABB(p.t.position, dir, b.min, b.max, out d) && d <= len)
                        {
                            bool inTank = gm != null && gm.TankMounted && gm.Tank != null;
                            if (gm != null)
                            {
                                // 人在车里：这发子弹打在车体上，人物不掉血
                                if (inTank) gm.Tank.TakeDamage(p.damage * 0.8f);
                                else gm.DamagePlayer(p.damage);
                            }
                            if (FxPool.Instance != null)
                                FxPool.Instance.Burst(p.t.position + dir * d,
                                    inTank ? new Color(1f, 0.72f, 0.30f) : new Color(1f, 0.30f, 0.25f), 5, 0.7f);
                            p.active = false; p.t.gameObject.SetActive(false);
                            continue;
                        }
                    }

                    // 撞队友
                    if (gm != null)
                    {
                        bool done = false;
                        for (int k = 0; k < gm.Allies.Count; k++)
                        {
                            var a = gm.Allies[k];
                            if (a == null || !a.Alive) continue;
                            float d;
                            if (RayMath.RayAABB(p.t.position, dir, a.BoundsMin, a.BoundsMax, out d) && d <= len)
                            {
                                a.TakeDamage(p.damage, p.t.position + dir * d);
                                if (FxPool.Instance != null)
                                    FxPool.Instance.Burst(p.t.position + dir * d, new Color(0.3f, 0.5f, 0.45f), 4, 0.6f);
                                p.active = false; p.t.gameObject.SetActive(false);
                                done = true;
                                break;
                            }
                        }
                        if (done) continue;
                    }
                }

                p.t.position = next;
                p.t.rotation = Quaternion.LookRotation(p.vel);
            }
        }
    }
}
