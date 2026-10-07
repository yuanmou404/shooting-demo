using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixelArena
{
    public enum GameState { Menu, Playing, Paused, Dead }

    /// <summary>玩法模式：小队模式（3 名 AI 队友 + 小队积分 + 召唤）/ 单人突击（原本的孤狼波次生存）。</summary>
    public enum GameMode { Squad, Solo }

    /// <summary>游戏主循环：状态、波次生成、计分、玩家生命。</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        public VoxelWorld World;
        public WorldView View;
        public PlayerController Player;
        public CameraRig Rig;
        public WeaponSystem Weapons;
        public GameHUD Hud;
        public MobileControls Mobile;

        public readonly List<EnemyAI> Enemies = new List<EnemyAI>();
        public readonly List<AllyAI> Allies = new List<AllyAI>();
        public GameState State = GameState.Menu;
        /// <summary>当前玩法模式（开始游戏时选）。小队模式才有队友 / 小队积分 / G 召唤。</summary>
        public GameMode Mode = GameMode.Squad;
        public bool HasSquad => Mode == GameMode.Squad;
        public int Score;
        public int Kills;
        public int Wave = 1;
        public float PlayerHp = 100f;
        public float PlayerMaxHp = 100f;
        public int Combo;
        public float ComboTimer;

        // ---------------- 小队系统（战地式：击杀攒分 → 花分召唤） ----------------
        public int SquadPoints;                 // 小队积分（召唤消耗）
        public bool CallInOpen;                 // G 键召唤面板（打开时世界暂停，方便点选）
        public TankController Tank;             // 场上唯一的一辆坦克（null = 没召唤）
        public bool TankMounted;                // 玩家是否在车里
        public const int CostArtillery = 600;
        public const int CostFlame = 350;
        public const int CostMinigun = 500;
        public const int CostTank = 1200;
        public const float TempWeaponTime = 60f;   // 召唤武器持续时间（秒）
        private const int AllyCount = 3;
        private float tankExitCd;
        private bool restoreFirstPerson;      // 登车前是第一人称 → 下车后还原
        private readonly List<ArtyShell> artyShells = new List<ArtyShell>();

        private Transform enemyRoot;
        private Transform allyRoot;
        private Transform artyRoot;
        private int pendingSpawn;
        private float spawnTimer;
        private float waveBreak;
        private bool waveAnnounced;
        private float deadTimer;
        private int seed = 20260927;
        private int lastHunt = -1;
        private float noContactTimer;   // 没有任何敌人在搜/打玩家的累计时间（防场面僵持）

        // 刷怪距离：太近会被贴脸，太远玩家找不到
        private const float SpawnMinDist = 15f;
        private const float SpawnMaxDist = 30f;
        private const int SpawnMaxY = 12;   // 不刷在塔顶等高处平台上

        public bool SimulationRunning => State == GameState.Playing;
        public int PendingSpawn => pendingSpawn;
        public int AliveEnemies
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Enemies.Count; i++) if (Enemies[i] != null && Enemies[i].Alive) n++;
                return n;
            }
        }

        public void Init(VoxelWorld world, WorldView view, PlayerController player, CameraRig rig, WeaponSystem weapons)
        {
            Instance = this;
            World = world; View = view; Player = player; Rig = rig; Weapons = weapons;
            enemyRoot = new GameObject("Enemies").transform;
            allyRoot = new GameObject("Allies").transform;
            PlayerHp = PlayerMaxHp;
        }

        private void Update()
        {
            if (Mobile != null) Mobile.Tick();
            GameInput.UpdateInput();

            // 召唤面板优先吃掉 Esc / 数字键，不然会顺手把游戏暂停掉
            if (CallInOpen)
            {
                if (GameInput.PausePressed || GameInput.CallInPressed) CloseCallIn();
                else if (GameInput.WeaponSlot >= 0 && GameInput.WeaponSlot < 4) RequestCallIn(GameInput.WeaponSlot);
                GameInput.ClearFrame();
                return;
            }

            if (GameInput.PausePressed)
            {
                if (State == GameState.Playing) Pause();
                else if (State == GameState.Paused) Resume();
            }

            if (GameInput.CallInPressed && State == GameState.Playing)
            {
                if (HasSquad) OpenCallIn();
                else AnnounceSafe("单人突击模式 · 没有小队支援");
            }

            if (State == GameState.Playing)
            {
                float dt = Time.deltaTime;

                // 视角：鼠标/触控增量必须在移动之前应用，否则移动方向会慢一帧
                // 开镜时灵敏度要降下来（狙击 4 倍镜不降根本没法瞄）
                if (Rig != null)
                    Rig.AddLook(GameInput.LookDelta * (Weapons != null ? Weapons.AdsFactor : 1f));

                if (GameInput.InteractPressed) ToggleTankMount();

                if (TankMounted && Tank != null)
                {
                    // 在车里：V 切舱内/车外视角，滚轮 / Q / 数字键 1、2 换主炮与同轴机枪
                    if (GameInput.ToggleViewPressed) ToggleTankView();
                    if (GameInput.NextWeaponPressed) Tank.CycleWeapon(1);
                    if (GameInput.PrevWeaponPressed) Tank.CycleWeapon(-1);
                    if (GameInput.WeaponSlot >= 0) Tank.SelectWeapon(GameInput.WeaponSlot);

                    // WASD 开车、鼠标转炮塔、左键开火；人物只跟着座位走
                    Tank.Drive(GameInput.MoveAxis, Rig, GameInput.FirePressed, GameInput.FireHeld, dt);
                    Player.transform.position = Tank.SeatPos;
                    Player.Velocity = Vector3.zero;
                    // 装甲归零：3 秒自爆倒计时，玩家可以按 E 跳车逃生
                    if (Tank.TickDoom(dt)) BlowUpTank();
                }
                else if (Tank != null && Tank.Doomed)
                {
                    // 人已经跳车了，空车照样会炸
                    if (Tank.TickDoom(dt)) BlowUpTank();
                }
                else
                {
                    // 扛着加特林会明显变慢（CarrySlow），滑铲靠 CrouchPressed 这个"按下沿"触发
                    Player.SpeedMul = Weapons != null ? Weapons.CarrySlow : 1f;
                    Player.Tick(GameInput.MoveAxis, Rig.Yaw, GameInput.JumpPressed, GameInput.SprintHeld,
                        GameInput.CrouchHeld, GameInput.CrouchPressed, dt);
                    Weapons.Tick(GameInput.FirePressed, GameInput.FireHeld, GameInput.ReloadPressed,
                        GameInput.NextWeaponPressed, GameInput.PrevWeaponPressed, GameInput.PlacePressed,
                        GameInput.InspectPressed, GameInput.AimHeld, dt);
                    if (GameInput.ToggleViewPressed) Rig.ToggleView();
                    // 数字键直选武器
                    if (GameInput.WeaponSlot >= 0 && Weapons != null) Weapons.SelectSlot(GameInput.WeaponSlot);
                }
                tankExitCd -= dt;

                UpdateWaves(dt);
                UpdateAntiStall(dt);
                UpdateArtillery(dt);
                UpdateAimHighlight();
                PickupSystem.Update(World, Player, this, dt);

                if (ComboTimer > 0f)
                {
                    ComboTimer -= dt;
                    if (ComboTimer <= 0f) Combo = 0;
                }
            }
            else if (State == GameState.Dead)
            {
                deadTimer -= Time.unscaledDeltaTime;
                if (deadTimer <= 0f && Hud != null) Hud.ShowDeathPanel(true);
            }

            ProjectileSystem.Update(World, Player);
            GameInput.ClearFrame();
        }

        private void LateUpdate()
        {
            if (Rig != null) Rig.LateUpdateCamera(Time.unscaledDeltaTime);
        }

        // ------------------------------------------------------------ 波次

        private void UpdateWaves(float dt)
        {
            int alive = AliveEnemies;
            for (int i = Enemies.Count - 1; i >= 0; i--)
                if (Enemies[i] == null) Enemies.RemoveAt(i);

            if (!waveAnnounced)
            {
                waveAnnounced = true;
                pendingSpawn = Mathf.Min(4 + Wave * 2, 16);
                if (Hud != null) Hud.Announce("第 " + Wave + " 波 / WAVE " + Wave);
                AudioKit.Play2D("wave", 0.8f);
            }

            // 最后几名敌人：提示玩家去哪找（小地图红点 + 屏幕边缘箭头）
            int total = alive + pendingSpawn;
            if (total > 0 && total <= 5)
            {
                if (total != lastHunt)
                {
                    lastHunt = total;
                    if (Hud != null)
                    {
                        Hud.SetHunt(total);
                        if (total == 5) Hud.Announce("最后 5 名敌人 · 位置已标在小地图");
                        else if (total == 1) Hud.Announce("最后 1 名敌人！");
                    }
                }
            }
            else if (lastHunt != -1)
            {
                lastHunt = -1;
                if (Hud != null) Hud.SetHunt(0);
            }

            if (pendingSpawn > 0)
            {
                spawnTimer -= dt;
                if (spawnTimer <= 0f && alive < 10)
                {
                    spawnTimer = Random.Range(0.6f, 1.6f);
                    if (SpawnOne()) pendingSpawn--;
                    else spawnTimer = 0.35f;   // 暂时找不到合法点，稍后重试
                }
            }
            else if (alive == 0)
            {
                waveBreak -= dt;
                if (waveBreak <= 0f)
                {
                    Wave++;
                    waveAnnounced = false;
                    waveBreak = 3.2f;
                    Score += 50;
                    AwardSquad(60);      // 清完一波也攒点分，不然开局攒不出召唤
                }
            }
            else
            {
                waveBreak = 3.2f;
            }
        }

        /// <summary>
        /// 敌人靠"视线 + 警觉度"发现玩家，理论上可能被玩家彻底躲开。
        /// 这里做兜底：太久没人理玩家时，让最近的两名敌人去查看玩家附近（模拟听到动静），
        /// 免得最后几名敌人在场边干等、波次卡住。
        /// </summary>
        private void UpdateAntiStall(float dt)
        {
            bool engaged = false;
            for (int i = 0; i < Enemies.Count; i++)
                if (Enemies[i] != null && Enemies[i].Alive && Enemies[i].Engaged) { engaged = true; break; }

            int alive = AliveEnemies;
            if (alive == 0 || pendingSpawn > 0) { noContactTimer = 0f; return; }

            noContactTimer = engaged ? 0f : noContactTimer + dt;
            if (noContactTimer < 12f) return;
            noContactTimer = 0f;

            // 注意：给的是"模糊位置"而不是玩家真实坐标 —— 只是让他们往这边走来看看，
            // 否则就成了无视视野的定位跟踪。
            Vector3 p = Player.transform.position;
            Vector3 fuzzy = EnemyAI.FuzzyNear(p, 5f, 10f);
            int given = 0;
            float best = float.MaxValue;
            EnemyAI nearest = null;
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e == null || !e.Alive) continue;
                float d = Vector3.Distance(e.transform.position, p);
                if (d < best) { best = d; nearest = e; }
            }
            if (nearest != null) { nearest.SetHint(fuzzy); given++; }
            for (int i = 0; i < Enemies.Count && given < 3; i++)
            {
                var e = Enemies[i];
                if (e == null || !e.Alive || e == nearest) continue;
                if (Vector3.Distance(e.transform.position, p) < 34f) { e.SetHint(EnemyAI.FuzzyNear(p, 5f, 10f)); given++; }
            }
            if (Hud != null) Hud.Announce("有人在朝你的方向搜索…");
        }

        /// <summary>玩家开枪 / 破坏方块会发出声音：附近敌人过来查看（但不会直接锁定）。</summary>
        public void MakeNoise(Vector3 pos, float radius)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e != null && e.Alive) e.HearNoise(pos, radius);
            }
        }

        /// <summary>清空当前所有敌人与待生成的敌人（回主菜单 / 重开时用）。</summary>
        private void ClearEnemies()
        {
            for (int i = 0; i < Enemies.Count; i++)
                if (Enemies[i] != null) Destroy(Enemies[i].gameObject);
            Enemies.Clear();
            pendingSpawn = 0;
            waveAnnounced = false;
            waveBreak = 3.2f;
            lastHunt = -1;
            noContactTimer = 0f;
        }

        /// <summary>
        /// 在玩家周围的环形区域里找一个合法出生点：
        /// 距离适中、地面平整、头顶两格净空、不在高台上，并优先选能看到玩家的位置。
        /// </summary>
        private bool FindSpawnPoint(out Vector3 pos)
        {
            pos = Vector3.zero;
            if (World == null || Player == null) return false;
            Vector3 p = Player.transform.position;

            for (int tries = 0; tries < 80; tries++)
            {
                float ang = Random.value * Mathf.PI * 2f;
                float dist = Random.Range(SpawnMinDist, SpawnMaxDist);
                int ix = Mathf.FloorToInt(p.x + Mathf.Cos(ang) * dist);
                int iz = Mathf.FloorToInt(p.z + Mathf.Sin(ang) * dist);
                if (ix < 3 || iz < 3 || ix >= World.SX - 3 || iz >= World.SZ - 3) continue;

                int y = World.GroundHeight(ix, iz, World.SY - 1);
                if (y < 1 || y > SpawnMaxY) continue;

                // 需要两格净空，且身体不能卡在方块里
                if (World.IsSolidAt(ix, y, iz) || World.IsSolidAt(ix, y + 1, iz)) continue;
                if (World.Overlaps(ix + 0.5f, y + 0.05f, iz + 0.5f, 0.36f, 1.8f)) continue;

                Vector3 cand = new Vector3(ix + 0.5f, y + 0.05f, iz + 0.5f);

                // 不要刷在别的敌人身上（否则会出现两个模型重叠在一起）
                if (TooCloseToEnemy(cand, 3.6f)) continue;

                // 前 50 次只接受“看得到玩家”的点，保证刷出来就会交火；之后放宽
                if (tries < 50 && !World.HasLineOfSight(cand + Vector3.up * 1.5f, Player.Center)) continue;

                pos = cand;
                return true;
            }
            return false;
        }

        private bool TooCloseToEnemy(Vector3 p, float minDist)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e == null || !e.Alive) continue;
                Vector3 d = e.transform.position - p;
                d.y = 0f;
                if (d.sqrMagnitude < minDist * minDist) return true;
            }
            return false;
        }

        private bool SpawnOne()
        {
            Vector3 pos;
            if (!FindSpawnPoint(out pos)) return false;

            EnemyKind kind = EnemyKind.Grunt;
            float r = Random.value;
            if (Wave >= 2 && r < 0.26f) kind = EnemyKind.Rusher;
            else if (Wave >= 3 && r > 0.86f)
            {
                // 精英出现概率随波次上升（第 3 波 5%，封顶 40%）
                float eliteChance = Mathf.Min(0.40f, 0.05f + (Wave - 3) * 0.05f);
                kind = Random.value < eliteChance ? EnemyKind.Elite : EnemyKind.Heavy;
            }

            var e = EnemyAI.Spawn(World, Player, enemyRoot, pos, kind);
            // 出生时只给"玩家大概在哪个方向"的模糊线索（不是精确坐标），
            // 免得敌人像开了地图挂一样直线奔向玩家。
            if (e != null && Player != null)
                e.SetHint(EnemyAI.FuzzyNear(Player.transform.position, 4f, 9f));
            Enemies.Add(e);

            if (kind == EnemyKind.Elite && Hud != null)
            {
                Hud.Announce("精英敌人出现！血量极高");
                AudioKit.Play2D("wave", 0.7f, 0.7f);
            }
            return true;
        }

        private void UpdateAimHighlight()
        {
            if (View == null) return;
            Ray ray = Rig.AimRay();
            Vector3Int cell; Vector3Int n; Vector3 p;
            // 用较短的距离：射击距离内 & 建造距离内
            if (World.RaycastBlocks(ray.origin, ray.direction, 7f, out p, out cell, out n))
            {
                float dist = Vector3.Distance(ray.origin, p);
                if (dist <= 6f) { View.SetHighlight(cell); return; }
            }
            View.HideHighlight();
        }

        // ------------------------------------------------------------ 战斗事件

        public void DamagePlayer(float dmg)
        {
            if (State != GameState.Playing) return;
            // 人坐在坦克里：伤害一律由车体装甲扛，人物自身一滴血都不掉
            // （装甲打穿后是 3 秒自爆倒计时，不是直接掉人物血）
            if (TankMounted && Tank != null)
            {
                bool wasDoomed = Tank.Doomed;
                Tank.TakeDamage(dmg * 0.6f);
                if (Hud != null && !wasDoomed) Hud.FlashHullHit();
                AudioKit.Play2D("hit", 0.35f, 0.7f);
                return;
            }
            PlayerHp -= dmg;
            if (Hud != null) Hud.FlashDamage();
            AudioKit.Play2D("hurt", 0.5f);
            if (PlayerHp <= 0f)
            {
                PlayerHp = 0f;
                Die();
            }
        }

        /// <summary>拾取医疗包回血。</summary>
        public void HealPlayer(float amount)
        {
            if (State != GameState.Playing) return;
            PlayerHp = Mathf.Min(PlayerHp + amount, PlayerMaxHp);
        }

        /// <param name="byAlly">被队友打死的：算你小队的战果，但小队积分打折（鼓励自己补刀）。</param>
        public void OnEnemyKilled(EnemyAI e, bool byAlly = false)
        {
            Kills++;
            Combo++;
            ComboTimer = 3.5f;
            int baseScore;
            if (e.Kind == EnemyKind.Elite) baseScore = 300;
            else if (e.Kind == EnemyKind.Heavy) baseScore = 150;
            else if (e.Kind == EnemyKind.Rusher) baseScore = 100;
            else baseScore = 80;
            Score += baseScore + Mathf.Min(Combo, 10) * 10;
            // 小队积分：自己打满分，队友打 6 折（轰炸 / 坦克炮击算你自己的）
            AwardSquad(Mathf.RoundToInt(baseScore * (byAlly ? 0.6f : 1f)));
            if (Hud != null) Hud.PushKill(e.Kind, Combo);

            // 击杀补给弹药
            if (Weapons != null)
            {
                for (int i = 0; i < Weapons.Weapons.Length; i++)
                {
                    int add = Mathf.CeilToInt(Weapons.Weapons[i].MagSize * 0.6f);
                    // 通过公开接口补给
                    Weapons.AddAmmo(i, add);
                }
            }
        }

        private void Die()
        {
            if (TankMounted) DismountTank();     // 死在车里也要先被请下车
            State = GameState.Dead;
            deadTimer = 1.6f;
            if (!GameInput.TouchMode) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (Hud != null) Hud.ShowDeathPanel(false);
            if (FxPool.Instance != null) FxPool.Instance.Burst(Player.Center, new Color(0.8f, 0.2f, 0.2f), 18, 1.4f);
            AudioKit.Play2D("die", 0.9f, 0.8f);
        }

        // ------------------------------------------------------------ 小队 / 队友 / 召唤

        public void AwardSquad(int amount)
        {
            if (State != GameState.Playing) return;
            if (!HasSquad) return;      // 单人突击模式没有小队，也就没有积分
            SquadPoints += amount;
        }

        /// <summary>开局在玩家身边放 3 个队友（左翼 / 右翼 / 后卫）。</summary>
        public void SpawnAllies()
        {
            if (World == null || Player == null || allyRoot == null) return;
            ClearAllies();
            for (int i = 0; i < AllyCount; i++)
            {
                var a = AllyAI.Spawn(World, Player, allyRoot, FindAllySpawn(i), i);
                if (a != null) Allies.Add(a);
            }
        }

        private Vector3 FindAllySpawn(int slot)
        {
            Vector3 basePos = Player != null ? Player.transform.position : World.SpawnPoint;
            float a0 = (slot / (float)AllyCount) * Mathf.PI * 2f;
            for (int t = 0; t < 40; t++)
            {
                float ang = a0 + Random.Range(-0.6f, 0.6f);
                float d = Random.Range(2.6f, 5.5f);
                int ix = Mathf.FloorToInt(basePos.x + Mathf.Cos(ang) * d);
                int iz = Mathf.FloorToInt(basePos.z + Mathf.Sin(ang) * d);
                if (ix < 2 || iz < 2 || ix >= World.SX - 2 || iz >= World.SZ - 2) continue;
                int y = World.GroundHeight(ix, iz, World.SY - 1);
                if (y < 1 || y > SpawnMaxY + 2) continue;
                if (World.IsSolidAt(ix, y, iz) || World.IsSolidAt(ix, y + 1, iz)) continue;
                return new Vector3(ix + 0.5f, y + 0.05f, iz + 0.5f);
            }
            return basePos + new Vector3(Mathf.Cos(a0) * 3f, 0.3f, Mathf.Sin(a0) * 3f);
        }

        private void ClearAllies()
        {
            for (int i = 0; i < Allies.Count; i++)
                if (Allies[i] != null) Destroy(Allies[i].gameObject);
            Allies.Clear();
        }

        // ------------------------------------------------------------ 召唤面板（G）

        public void OpenCallIn()
        {
            if (CallInOpen) return;
            CallInOpen = true;
            Time.timeScale = 0f;
            if (!GameInput.TouchMode) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (Mobile != null) Mobile.SetVisible(false);
            if (Hud != null) Hud.ShowCallIn(true);
            AudioKit.Play2D("ui", 0.6f);
        }

        public void CloseCallIn()
        {
            if (!CallInOpen) return;
            CallInOpen = false;
            Time.timeScale = 1f;
            if (State == GameState.Playing && !GameInput.TouchMode)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (Mobile != null && State == GameState.Playing) Mobile.SetVisible(true);
            if (Hud != null) Hud.ShowCallIn(false);
        }

        /// <summary>0 区域轰炸 / 1 喷火器 / 2 加特林 / 3 坦克。</summary>
        public void RequestCallIn(int index)
        {
            if (State != GameState.Playing) return;
            int cost = CallInCost(index);
            if (SquadPoints < cost)
            {
                AnnounceSafe("小队积分不足 · 还差 " + (cost - SquadPoints));
                AudioKit.Play2D("empty", 0.5f);
                return;
            }
            bool ok;
            switch (index)
            {
                case 0: ok = CallArtillery(); break;
                case 1: ok = GrantTempWeapon(WeaponKind.Flamethrower); break;
                case 2: ok = GrantTempWeapon(WeaponKind.Minigun); break;
                default: ok = CallTank(); break;
            }
            if (!ok) return;
            SquadPoints -= cost;
            CloseCallIn();
        }

        public static int CallInCost(int index)
        {
            switch (index)
            {
                case 0: return CostArtillery;
                case 1: return CostFlame;
                case 2: return CostMinigun;
                default: return CostTank;
            }
        }

        private bool GrantTempWeapon(WeaponKind kind)
        {
            if (Weapons == null) return false;
            Weapons.GrantTemp(kind, TempWeaponTime);
            AnnounceSafe(kind == WeaponKind.Flamethrower ? "喷火器已下发 · 60 秒" : "加特林已下发 · 60 秒");
            return true;
        }

        /// <summary>区域轰炸：以准星落点为圆心，14 发炮弹在 ~9m 半径内依次砸下来。</summary>
        private bool CallArtillery()
        {
            if (World == null || Rig == null) return false;
            Ray ray = Rig.AimRay();
            Vector3Int cell; Vector3Int n; Vector3 hit;
            Vector3 center = World.RaycastBlocks(ray.origin, ray.direction, 90f, out hit, out cell, out n)
                ? hit : ray.origin + ray.direction * 45f;
            center.x = Mathf.Clamp(center.x, 4f, World.SX - 4f);
            center.z = Mathf.Clamp(center.z, 4f, World.SZ - 4f);
            int gy = World.GroundHeight(Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.z), World.SY - 1);
            center.y = Mathf.Max(gy + 0.4f, 1f);

            if (artyRoot == null) artyRoot = new GameObject("Artillery").transform;
            const int count = 14;
            for (int i = 0; i < count; i++)
            {
                Vector2 off = Random.insideUnitCircle * 9f;
                Vector3 p = center + new Vector3(off.x, 0f, off.y);
                int ix = Mathf.Clamp(Mathf.FloorToInt(p.x), 2, World.SX - 3);
                int iz = Mathf.Clamp(Mathf.FloorToInt(p.z), 2, World.SZ - 3);
                int y = World.GroundHeight(ix, iz, World.SY - 1);
                Vector3 impact = new Vector3(ix + 0.5f, y + 0.4f, iz + 0.5f);
                var shell = VoxelAssets.MakeBox("Shell" + i, artyRoot,
                    impact + Vector3.up * (38f + Random.Range(0f, 14f)),
                    new Vector3(0.5f, 1.0f, 0.5f), new Color(0.85f, 0.70f, 0.30f));
                artyShells.Add(new ArtyShell { t = shell.transform, impact = impact, delay = 1.2f + i * 0.17f });
            }
            AnnounceSafe("炮击已标定 · 火力覆盖中");
            AudioKit.Play2D("wave", 0.8f, 0.75f);
            return true;
        }

        private void UpdateArtillery(float dt)
        {
            for (int i = artyShells.Count - 1; i >= 0; i--)
            {
                var s = artyShells[i];
                s.delay -= dt;
                if (s.delay > 0f) continue;
                Vector3 p = s.t.position;
                p.y -= 74f * dt;
                s.t.position = p;
                if (p.y <= s.impact.y)
                {
                    ExplodeAt(s.impact, 5.6f, 210f, 1.5f);
                    Destroy(s.t.gameObject);
                    artyShells.RemoveAt(i);
                }
            }
        }

        /// <summary>球形爆炸：拆方块 + 范围伤害（敌人吃满，自伤打三折，队友不吃自家火力）。</summary>
        public void ExplodeAt(Vector3 center, float radius, float damage, float blockRadius, bool creditAlly = false)
        {
            // 拆方块
            if (World != null && View != null && blockRadius > 0f)
            {
                int r = Mathf.CeilToInt(blockRadius);
                int cx = Mathf.FloorToInt(center.x), cy = Mathf.FloorToInt(center.y), cz = Mathf.FloorToInt(center.z);
                byte dummy;
                for (int x = cx - r; x <= cx + r; x++)
                    for (int y = cy - r; y <= cy + r; y++)
                        for (int z = cz - r; z <= cz + r; z++)
                        {
                            if (!World.InBounds(x, y, z)) continue;
                            if (World.Get(x, y, z) == (byte)BlockId.Air) continue;
                            float d = new Vector3(x + 0.5f - center.x, y + 0.5f - center.y, z + 0.5f - center.z).magnitude;
                            if (d > blockRadius) continue;
                            View.DamageBlock(new Vector3Int(x, y, z), 999f, out dummy);
                        }
            }

            // 敌人
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e == null || !e.Alive) continue;
                float d = DistToBounds(center, e.BoundsMin, e.BoundsMax);
                if (d > radius) continue;
                e.TakeDamage(damage * (1f - d / radius), e.transform.position + Vector3.up * 0.9f, false, creditAlly);
            }

            // 坦克：空车吃 5 折，人在车里只吃 1.5 折（不然自己开炮会把自己车拆了）
            if (Tank != null)
            {
                float d = DistToBounds(center, Tank.TankBounds.min, Tank.TankBounds.max);
                if (d < radius) Tank.TakeDamage(damage * (1f - d / radius) * (TankMounted ? 0.15f : 0.5f));
            }

            // 玩家：炮击有自伤风险（三折），不致命但别站在弹幕里
            if (Player != null && !TankMounted)
            {
                Bounds pb = Player.WorldBounds;
                float d = DistToBounds(center, pb.min, pb.max);
                if (d < radius * 0.75f) DamagePlayer(damage * (1f - d / radius) * 0.3f);
            }

            if (FxPool.Instance != null)
            {
                FxPool.Instance.Flash(center, new Color(1f, 0.75f, 0.35f), 1.7f);
                FxPool.Instance.Burst(center, new Color(1f, 0.55f, 0.20f), 18, 2.4f);
                FxPool.Instance.Burst(center + Vector3.up * 0.7f, new Color(0.36f, 0.33f, 0.30f), 10, 1.7f);
            }
            AudioKit.Play("explode", center, 0.9f, Random.Range(0.9f, 1.1f));
            if (Rig != null)
            {
                float d = Vector3.Distance(Rig.transform.position, center);
                if (d < 34f) Rig.Shake(0.55f * (1f - d / 34f));
            }
            MakeNoise(center, 46f);
        }

        private static float DistToBounds(Vector3 p, Vector3 min, Vector3 max)
        {
            var c = new Vector3(Mathf.Clamp(p.x, min.x, max.x), Mathf.Clamp(p.y, min.y, max.y), Mathf.Clamp(p.z, min.z, max.z));
            return Vector3.Distance(p, c);
        }

        // ------------------------------------------------------------ 坦克

        private bool CallTank()
        {
            if (World == null || Player == null) return false;
            if (Tank != null && Tank.IsAlive)
            {
                AnnounceSafe("场上已经有一辆坦克了");
                return false;
            }
            Tank = TankController.Spawn(World, View, FindTankSpot(), transform);
            AnnounceSafe("坦克已空投 · 走到车边按 E 登乘");
            AudioKit.Play2D("wave", 0.8f, 0.7f);
            return true;
        }

        /// <summary>找一块 5×5 的平地放坦克（不然车体会卡在墙里）。</summary>
        private Vector3 FindTankSpot()
        {
            Vector3 basePos = Player.transform.position;
            for (int t = 0; t < 50; t++)
            {
                float ang = Random.value * Mathf.PI * 2f;
                float d = Random.Range(6f, 11f);
                int ix = Mathf.FloorToInt(basePos.x + Mathf.Cos(ang) * d);
                int iz = Mathf.FloorToInt(basePos.z + Mathf.Sin(ang) * d);
                if (ix < 4 || iz < 4 || ix >= World.SX - 4 || iz >= World.SZ - 4) continue;
                int y = World.GroundHeight(ix, iz, World.SY - 1);
                if (y < 1 || y > 12) continue;
                bool flat = true;
                for (int dx = -2; dx <= 2 && flat; dx++)
                    for (int dz = -2; dz <= 2 && flat; dz++)
                    {
                        if (Mathf.Abs(World.GroundHeight(ix + dx, iz + dz, World.SY - 1) - y) > 1) flat = false;
                        else if (World.IsSolidAt(ix + dx, y + 1, iz + dz)) flat = false;
                        else if (World.IsSolidAt(ix + dx, y + 2, iz + dz)) flat = false;
                    }
                if (!flat) continue;
                return new Vector3(ix + 0.5f, y + 0.1f, iz + 0.5f);
            }
            int fx = Mathf.Clamp(Mathf.FloorToInt(basePos.x), 4, World.SX - 5);
            int fz = Mathf.Clamp(Mathf.FloorToInt(basePos.z) + 6, 4, World.SZ - 5);
            return new Vector3(fx + 0.5f, World.GroundHeight(fx, fz, World.SY - 1) + 0.1f, fz + 0.5f);
        }

        private void ToggleTankMount()
        {
            // 自爆倒计时里随时可以跳车（不受下车冷却限制，否则人会被锁死在车里）
            if (TankMounted && Tank != null && Tank.Doomed) { DismountTank(); return; }
            if (tankExitCd > 0f) return;
            if (TankMounted) DismountTank();
            else MountTank();
        }

        private void MountTank()
        {
            if (Tank == null || !Tank.IsAlive || Tank.Doomed) return;
            if (Player == null) return;
            if (Vector3.Distance(Player.transform.position, Tank.transform.position) > 7f)
            {
                AnnounceSafe("离坦克太远 · 走到车边再按 E");
                return;
            }
            TankMounted = true;
            Tank.Mounted = true;
            // 车里看不到自己的枪，也别让一个小人杵在炮塔上
            if (Weapons != null) Weapons.SetMounted(true);
            if (Rig != null)
            {
                restoreFirstPerson = Rig.Mode == ViewMode.FirstPerson;
                if (restoreFirstPerson) Rig.SetViewMode(ViewMode.ThirdPerson);
                Rig.SetAvatarVisible(false);
                // 车外视角：镜头绕着车体转（比绕着座位上的小人稳，也看得更远）
                Rig.FollowOverride = Tank.transform;
                Rig.TankEye = Tank.EyeAnchor;
                Rig.TankFirstPerson = false;
            }
            AnnounceSafe("已登乘 · WASD 驾驶 · 左键开火 · 滚轮/Q 切主炮与机枪 · V 切舱内视角 · E 下车");
            AudioKit.Play2D("ui", 0.7f);
        }

        private void DismountTank()
        {
            if (Weapons != null) Weapons.SetMounted(false);
            if (Rig != null)
            {
                Rig.SetAvatarVisible(true);
                Rig.FollowOverride = null;
                Rig.TankEye = null;
                Rig.TankFirstPerson = false;
                if (restoreFirstPerson) { Rig.SetViewMode(ViewMode.FirstPerson); restoreFirstPerson = false; }
            }
            TankMounted = false;
            tankExitCd = 0.8f;
            if (Tank == null || Player == null || World == null) return;
            Tank.Mounted = false;
            Vector3 p = Tank.transform.position - Tank.transform.right * 3.3f;
            int ix = Mathf.Clamp(Mathf.FloorToInt(p.x), 1, World.SX - 2);
            int iz = Mathf.Clamp(Mathf.FloorToInt(p.z), 1, World.SZ - 2);
            p.y = World.GroundHeight(ix, iz, World.SY - 1) + 0.1f;
            Player.transform.position = p;
            Player.Velocity = Vector3.zero;
        }

        /// <summary>履带碾死敌人（由 TankController 回调，用于提示与统计）。</summary>
        public void OnTankCrush(int count)
        {
            AnnounceSafe(count > 1 ? ("履带碾压 ×" + count) : "履带碾压");
        }

        /// <summary>装甲归零：进入 3 秒自爆倒计时（这时还能跳车）。</summary>
        public void OnTankDoomed()
        {
            AnnounceSafe("装甲损毁！3 秒后自爆 · 按 E 立刻跳车");
            AudioKit.Play2D("wave", 0.9f, 1.2f);
        }

        /// <summary>自爆：大范围爆炸。没跳车的人会被炸飞并重伤，跳车及时就只吃一点余波。</summary>
        private void BlowUpTank()
        {
            if (Tank == null) return;
            Vector3 p = Tank.transform.position + Vector3.up * 1.1f;
            bool wasMounted = TankMounted;
            TankMounted = false;
            if (Weapons != null) Weapons.SetMounted(false);
            if (Rig != null)
            {
                Rig.SetAvatarVisible(true);
                Rig.FollowOverride = null;
                Rig.TankEye = null;
                Rig.TankFirstPerson = false;
                if (restoreFirstPerson) { Rig.SetViewMode(ViewMode.FirstPerson); restoreFirstPerson = false; }
            }
            if (wasMounted && Player != null)
            {
                Vector3 out1 = Tank.transform.position - Tank.transform.right * 3.6f;
                int ix = Mathf.Clamp(Mathf.FloorToInt(out1.x), 1, World.SX - 2);
                int iz = Mathf.Clamp(Mathf.FloorToInt(out1.z), 1, World.SZ - 2);
                out1.y = World.GroundHeight(ix, iz, World.SY - 1) + 0.1f;
                Player.transform.position = out1;
                Player.Velocity = Vector3.zero;
                DamagePlayer(85f);      // 没在 3 秒内跳车 = 跟着车一起炸（基本必死）
            }
            Destroy(Tank.gameObject);
            Tank = null;
            ExplodeAt(p, 8.5f, 260f, 3.0f);
            AnnounceSafe("坦克自爆");
        }

        /// <summary>清掉坦克（回菜单 / 重开）。</summary>
        private void ClearTank()
        {
            TankMounted = false;
            if (Tank != null) { Destroy(Tank.gameObject); Tank = null; }
            for (int i = artyShells.Count - 1; i >= 0; i--)
            {
                if (artyShells[i].t != null) Destroy(artyShells[i].t.gameObject);
                artyShells.RemoveAt(i);
            }
            if (Weapons != null) Weapons.SetMounted(false);
            if (Rig != null)
            {
                Rig.SetAvatarVisible(true);
                Rig.FollowOverride = null;
                Rig.TankEye = null;
                Rig.TankFirstPerson = false;
                restoreFirstPerson = false;
            }
            tankExitCd = 0f;
        }

        /// <summary>V 键：在"车外第三人称"和"舱内第一人称"之间切。</summary>
        private void ToggleTankView()
        {
            if (Rig == null || Tank == null) return;
            bool fp = !Rig.TankFirstPerson;
            Rig.TankFirstPerson = fp;
            Rig.TankEye = fp ? Tank.EyeAnchor : null;
            AudioKit.Play2D("ui", 0.6f);
            AnnounceSafe(fp ? "舱内视角 · 再按 V 回到车外" : "车外视角");
        }

        private void AnnounceSafe(string text)
        {
            if (Hud != null) Hud.Announce(text);
        }

        /// <summary>正在下落的炮击弹（先悬在空中当预警，再砸下来）。</summary>
        private class ArtyShell
        {
            public Transform t;
            public Vector3 impact;
            public float delay;
        }

        // ------------------------------------------------------------ 状态切换

        /// <param name="mode">小队模式 / 单人突击模式（点主菜单选项时决定）。</param>
        public void StartGame(GameMode mode)
        {
            Mode = mode;
            StartGame();
        }

        public void StartGame()
        {
            State = GameState.Playing;
            if (Rig != null) Rig.SetCinematic(false);
            if (Mobile != null) Mobile.SetVisible(true);
            if (!GameInput.TouchMode) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            Score = 0; Kills = 0; Wave = 1; Combo = 0;
            SquadPoints = 0;
            PlayerHp = PlayerMaxHp;
            waveAnnounced = false;
            waveBreak = 3.2f;
            pendingSpawn = 0;
            lastHunt = -1;
            // 每局都换一张随机地图（主题 + 布局都由新的随机种子决定）
            RollNewMap();
            // 队友要在换图之后再生（出生点得跟着新地形走）；单人突击模式没有队友
            if (HasSquad) SpawnAllies(); else ClearAllies();
            if (Hud != null) { Hud.SetHunt(0); Hud.SetPlaying(true); Hud.SetSquadVisible(HasSquad); }
            AudioKit.Play2D("ui", 0.8f);
            if (Hud != null)
                Hud.Announce(HasSquad ? "小队模式 · 地图 " + World.MapName : "单人突击 · 地图 " + World.MapName);
        }

        /// <summary>随机换图：重新生成体素世界、重建区块、重铺补给、把玩家送回新出生点。</summary>
        private void RollNewMap()
        {
            if (World == null) return;
            seed = Random.Range(1, 999999999);
            World.GenerateArena(seed);
            if (View != null) { View.RebuildAll(); View.ApplyLook(World.AmbientColor, World.SunColor, World.FogColor); }
            // 相机背景色跟着主题走（天空）
            if (Rig != null && Rig.Cam != null) Rig.Cam.backgroundColor = World.SkyColor;

            ClearEnemies();
            ClearTank();
            ProjectileSystem.Clear();
            PickupSystem.Reset(World);

            if (Player != null)
            {
                Vector3 spawn = World.SpawnPoint;
                if (spawn.y <= 0.01f) spawn = FallbackSpawnPos();
                Player.RespawnAt(spawn);
            }
            // 换图后机位直接落到新出生点（否则会从上一张图的位置"滑"过去），并朝向场地中心
            if (Rig != null)
            {
                if (Player != null)
                {
                    Vector3 toCenter = new Vector3(World.SX * 0.5f, 0f, World.SZ * 0.5f) - Player.transform.position;
                    if (toCenter.sqrMagnitude > 0.01f)
                        Rig.Yaw = Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg;
                }
                Rig.Pitch = 0f;
                Rig.SnapCamera();
            }
            if (Weapons != null) { Weapons.ClearTemp(); Weapons.RefillAll(); }
            if (Hud != null) Hud.SetMapName(World.MapName);
        }

        private Vector3 FallbackSpawnPos()
        {
            var w = new Vector3(World.SX * 0.5f, 0f, World.SZ * 0.5f);
            w.y = World.GroundHeight(World.SX / 2, World.SZ / 2, World.SY - 1) + 0.2f;
            return w;
        }

        public void Pause()
        {
            State = GameState.Paused;
            Time.timeScale = 0f;
            if (Mobile != null) Mobile.SetVisible(false);
            if (!GameInput.TouchMode) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (Hud != null) Hud.ShowPause(true);
        }

        public void Resume()
        {
            State = GameState.Playing;
            Time.timeScale = 1f;
            if (Mobile != null) Mobile.SetVisible(true);
            if (!GameInput.TouchMode) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (Hud != null) Hud.ShowPause(false);
        }

        public void ReturnToMenu()
        {
            EnterMenu();
        }

        /// <summary>
        /// 回到主菜单（待机画面）：清掉本局残留的敌人与子弹，并把主角放回出生点。
        /// 否则电影机位会绕着"上一次死亡的地点"转，而且在菜单里主角还会被残兵追着打。
        /// </summary>
        public void EnterMenu()
        {
            State = GameState.Menu;
            Time.timeScale = 1f;
            if (!GameInput.TouchMode) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (CallInOpen) CloseCallIn();

            ClearEnemies();
            ClearAllies();
            ClearTank();
            ProjectileSystem.Clear();
            PickupSystem.Reset(World);

            PlayerHp = PlayerMaxHp;
            Combo = 0; ComboTimer = 0f;
            if (Weapons != null) Weapons.RefillAll();
            if (Player != null && World != null)
            {
                Vector3 spawn = World.SpawnPoint;
                if (spawn.y <= 0.01f)
                {
                    spawn = new Vector3(World.SX * 0.5f, 0f, World.SZ * 0.5f);
                    spawn.y = World.GroundHeight(World.SX / 2, World.SZ / 2, World.SY - 1) + 0.2f;
                }
                Player.RespawnAt(spawn);
            }

            if (Rig != null) { Rig.Pitch = 0f; Rig.SetCinematic(true); }
            if (Mobile != null) Mobile.SetVisible(false);
            // SetPlaying(false) 会顺手关掉暂停/死亡/设置等面板并显示主菜单
            if (Hud != null) { Hud.ShowPause(false); Hud.ShowDeathPanel(false); Hud.SetPlaying(false); }
        }

        /// <summary>重开一局：沿用当前模式，也会重新抽一张地图，所以每局地形都不一样。</summary>
        public void Restart()
        {
            Time.timeScale = 1f;
            if (Hud != null) { Hud.ShowDeathPanel(false); Hud.ShowPause(false); Hud.SetPlaying(true); }
            StartGame();
        }

        /// <summary>主菜单：显示模式选择面板（小队 / 单人突击）。</summary>
        public void OpenModeSelect()
        {
            if (Hud != null) Hud.ShowModeSelect(true);
        }

        public void SetSeed(int s) { seed = s; }
    }
}
