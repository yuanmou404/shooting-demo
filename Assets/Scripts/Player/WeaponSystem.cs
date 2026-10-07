using System;
using UnityEngine;

namespace PixelArena
{
    [Serializable]
    public class WeaponDef
    {
        public WeaponKind Kind = WeaponKind.Rifle;
        public string Name = "RIFLE";
        public float Damage = 24f;
        public float BlockDamage = 26f;
        public float FireInterval = 0.105f;
        public int MagSize = 30;
        public int MaxReserve = 180;
        public float ReloadTime = 1.6f;
        public float Spread = 0.9f;
        public int Pellets = 1;
        public float Range = 120f;
        public bool Auto = true;
        public float RecoilPitch = 0.55f;
        public float RecoilYaw = 0.22f;
        public float Shake = 0.10f;
        public Color BodyColor = new Color(0.22f, 0.24f, 0.28f);
        public Color AccentColor = new Color(0.55f, 0.35f, 0.18f);
        public string Sfx = "rifle";
        public float MuzzleZ = 1.02f;   // 枪口在枪模型本地坐标里的 z
        public bool Melee;              // 近战武器：不吃弹药、不换弹
        public bool Flame;              // 喷火器：持续喷射火焰弹，不吃弹药
        public bool IsMinigun;          // 加特林：枪管先转起来才能开火，不吃弹药
                                        // 注意：不能叫 Minigun —— 会和下面的 static Minigun() 工厂方法撞名（CS0102）
        public float AdsZoom = 8f;      // 开镜时 FOV 缩小量（0 = 不能开镜）
        public float AdsSpreadMul = 0.35f;
        public bool CanAds => AdsZoom > 0.5f;

        public static WeaponDef Rifle()
        {
            return new WeaponDef
            {
                Kind = WeaponKind.Rifle,
                Name = "步枪 RIFLE", Damage = 24f, BlockDamage = 26f, FireInterval = 0.105f,
                MagSize = 30, MaxReserve = 210, ReloadTime = 1.55f, Spread = 0.9f, Pellets = 1,
                Range = 120f, Auto = true, RecoilPitch = 0.55f, RecoilYaw = 0.22f, Shake = 0.09f,
                BodyColor = new Color(0.20f, 0.22f, 0.26f), AccentColor = new Color(0.50f, 0.33f, 0.16f),
                Sfx = "rifle", MuzzleZ = 1.02f, AdsZoom = 11f, AdsSpreadMul = 0.35f
            };
        }

        public static WeaponDef Shotgun()
        {
            return new WeaponDef
            {
                Kind = WeaponKind.Shotgun,
                Name = "霰弹 SHOTGUN", Damage = 14f, BlockDamage = 12f, FireInterval = 0.72f,
                MagSize = 6, MaxReserve = 54, ReloadTime = 2.1f, Spread = 4.2f, Pellets = 9,
                Range = 48f, Auto = false, RecoilPitch = 1.5f, RecoilYaw = 0.4f, Shake = 0.28f,
                BodyColor = new Color(0.30f, 0.22f, 0.14f), AccentColor = new Color(0.62f, 0.55f, 0.42f),
                Sfx = "shotgun", MuzzleZ = 0.94f, AdsZoom = 6f, AdsSpreadMul = 0.55f
            };
        }

        public static WeaponDef Sniper()
        {
            return new WeaponDef
            {
                Kind = WeaponKind.Sniper,
                Name = "狙击 SNIPER", Damage = 95f, BlockDamage = 95f, FireInterval = 1.35f,
                MagSize = 5, MaxReserve = 40, ReloadTime = 2.4f, Spread = 0.12f, Pellets = 1,
                Range = 220f, Auto = false, RecoilPitch = 2.2f, RecoilYaw = 0.35f, Shake = 0.36f,
                BodyColor = new Color(0.16f, 0.20f, 0.16f), AccentColor = new Color(0.35f, 0.40f, 0.30f),
                Sfx = "sniper", MuzzleZ = 1.18f, AdsZoom = 46f, AdsSpreadMul = 0.12f
            };
        }

        public static WeaponDef Pistol()
        {
            return new WeaponDef
            {
                Kind = WeaponKind.Pistol,
                Name = "手枪 PISTOL", Damage = 27f, BlockDamage = 16f, FireInterval = 0.19f,
                MagSize = 12, MaxReserve = 120, ReloadTime = 1.15f, Spread = 0.75f, Pellets = 1,
                Range = 90f, Auto = false, RecoilPitch = 0.7f, RecoilYaw = 0.25f, Shake = 0.11f,
                BodyColor = new Color(0.26f, 0.27f, 0.31f), AccentColor = new Color(0.42f, 0.40f, 0.24f),
                Sfx = "pistol", MuzzleZ = 0.60f, AdsZoom = 9f, AdsSpreadMul = 0.3f
            };
        }

        public static WeaponDef Knife()
        {
            return new WeaponDef
            {
                Kind = WeaponKind.Knife,
                Name = "小刀 KNIFE", Damage = 62f, BlockDamage = 22f, FireInterval = 0.46f,
                MagSize = 1, MaxReserve = 0, ReloadTime = 0.4f, Spread = 0f, Pellets = 1,
                Range = 2.9f, Auto = false, RecoilPitch = 0.2f, RecoilYaw = 0.1f, Shake = 0.05f,
                BodyColor = new Color(0.28f, 0.30f, 0.34f), AccentColor = new Color(0.78f, 0.82f, 0.88f),
                Sfx = "knife", MuzzleZ = 0.72f, Melee = true, AdsZoom = 0f, AdsSpreadMul = 1f
            };
        }

        /// <summary>喷火器（召唤武器）：短距离持续火焰，不破坏方块、不吃弹药。</summary>
        public static WeaponDef Flamethrower()
        {
            return new WeaponDef
            {
                Kind = WeaponKind.Flamethrower,
                Name = "喷火器 FLAME", Damage = 7f, BlockDamage = 1f, FireInterval = 0.05f,
                MagSize = 9999, MaxReserve = 0, ReloadTime = 1f, Spread = 2.5f, Pellets = 1,
                Range = 14f, Auto = true, RecoilPitch = 0.05f, RecoilYaw = 0.02f, Shake = 0.02f,
                BodyColor = new Color(0.24f, 0.26f, 0.24f), AccentColor = new Color(0.65f, 0.40f, 0.16f),
                Sfx = "flame", MuzzleZ = 0.96f, Flame = true, AdsZoom = 0f, AdsSpreadMul = 1f
            };
        }

        /// <summary>加特林（召唤武器）：枪管预热后泼弹幕，移速变慢、不吃弹药。</summary>
        public static WeaponDef Minigun()
        {
            return new WeaponDef
            {
                Kind = WeaponKind.Minigun,
                Name = "加特林 GATLING", Damage = 9f, BlockDamage = 8f, FireInterval = 0.045f,
                MagSize = 9999, MaxReserve = 0, ReloadTime = 1f, Spread = 2.6f, Pellets = 1,
                Range = 100f, Auto = true, RecoilPitch = 0.14f, RecoilYaw = 0.08f, Shake = 0.04f,
                BodyColor = new Color(0.22f, 0.24f, 0.27f), AccentColor = new Color(0.55f, 0.48f, 0.22f),
                Sfx = "rifle", MuzzleZ = 1.08f, IsMinigun = true, AdsZoom = 0f, AdsSpreadMul = 1f
            };
        }
    }

    /// <summary>
    /// 射击、换弹、切枪、检视、破坏/放置方块。
    /// 视图模型带一套状态机动画（切枪下沉/抬起、换弹抽插弹匣、F 检视转身），
    /// 动作进行中不能开火，和 CS 的手感一致。
    /// </summary>
    public class WeaponSystem : MonoBehaviour
    {
        public CameraRig Rig;
        public VoxelWorld World;
        public WorldView View;
        public PlayerController Player;
        public Transform WeaponHolder;
        public BlockCharacter ThirdPersonModel;   // 第三人称主角模型（跟随当前武器换模型）

        public WeaponDef[] Weapons;
        public int Current;
        private int[] mag;
        private int[] reserve;
        private float cooldown;
        private float switchLock;
        private GameObject[] models;
        private Transform[] gunRoots;
        private Transform[] muzzles;
        private Transform[] mags;
        private Vector3[] magBase;
        private Transform[] armRs;
        private Transform[] armLs;
        private Vector3[] armRBase;
        private Vector3[] armLBase;
        public byte BuildBlock = (byte)BlockId.Crate;
        public float BuildCooldown;

        // ---------------- 开镜（ADS） ----------------
        private float ads01;          // 0 = 腰射，1 = 完全开镜
        private bool adsWanted;
        private float lunge;          // 近战突刺动画计时
        private float minigunSpin;    // 加特林枪管转速 0~1（>0.85 才能开火）
        private float flameSfxCd;     // 喷火器音效节流
        private bool mounted;         // 坐在坦克里

        // ---------------- 视图模型动画 ----------------
        private enum Vm { Idle, SwitchOut, SwitchIn, Reload, Inspect }
        private Vm state = Vm.Idle;
        private float vmTime;
        private float vmDur;
        private int visualIndex;      // 当前显示在手上的模型索引
        private int pendingWeapon = -1;
        private bool magSfxPlayed;

        /// <summary>开镜程度 0~1（供 HUD 画狙击镜遮罩、GameManager 降低鼠标灵敏度）。</summary>
        public float Ads01 => ads01;
        public bool IsAds => ads01 > 0.55f;
        /// <summary>开镜时鼠标灵敏度要降下来，否则高倍镜根本压不住。</summary>
        public float AdsFactor => Mathf.Lerp(1f, 0.42f, ads01);

        public int MagAmmo => mag != null ? mag[Current] : 0;
        public int ReserveAmmo => reserve != null ? reserve[Current] : 0;
        /// <summary>显示在 HUD 上的当前槽位（切枪动画刚开始时就指向新武器，手感更跟手）。</summary>
        public int DisplaySlot => pendingWeapon >= 0 ? pendingWeapon : Current;
        public int WeaponCount => Weapons != null ? Weapons.Length : 0;
        public string SlotName(int i) => (Weapons != null && i >= 0 && i < Weapons.Length && Weapons[i] != null) ? Weapons[i].Name : "";
        public bool IsReloading => state == Vm.Reload;
        public bool IsSwitching => state == Vm.SwitchOut || state == Vm.SwitchIn;
        public bool IsInspecting => state == Vm.Inspect;
        public bool Busy => state != Vm.Idle;
        public WeaponDef Def => Weapons[Current];
        public WeaponKind CurrentKind => Weapons != null ? Weapons[Current].Kind : WeaponKind.Rifle;

        /// <summary>临时武器（召唤来的喷火器/加特林）剩余秒数，0 = 没有。</summary>
        public float TempTimer { get; private set; }
        /// <summary>加特林枪管预热程度 0~1。</summary>
        public float MinigunSpin01 => minigunSpin;
        /// <summary>当前武器的移速系数（扛加特林会明显变慢）。</summary>
        public float CarrySlow
        {
            get
            {
                if (Weapons == null || Current >= Weapons.Length || Weapons[Current] == null) return 1f;
                var d = Weapons[Current];
                if (d.IsMinigun) return minigunSpin > 0.2f ? 0.55f : 0.75f;
                if (d.Flame) return 0.85f;
                return 1f;
            }
        }

        /// <summary>槽位 5 是召唤临时武器专用位（平时为 null，切枪循环会跳过空槽）。</summary>
        public const int TempSlot = 5;

        public void Init(CameraRig rig, VoxelWorld world, WorldView view, PlayerController player, Transform holder)
        {
            Rig = rig; World = world; View = view; Player = player; WeaponHolder = holder;
            // 1 步枪 / 2 霰弹 / 3 狙击 / 4 手枪 / 5 小刀（数字键直选，Q 循环）；槽位 6 = 召唤临时武器
            Weapons = new WeaponDef[6];
            Weapons[0] = WeaponDef.Rifle();
            Weapons[1] = WeaponDef.Shotgun();
            Weapons[2] = WeaponDef.Sniper();
            Weapons[3] = WeaponDef.Pistol();
            Weapons[4] = WeaponDef.Knife();
            Weapons[TempSlot] = null;
            mag = new int[Weapons.Length];
            reserve = new int[Weapons.Length];
            models = new GameObject[Weapons.Length];
            gunRoots = new Transform[Weapons.Length];
            muzzles = new Transform[Weapons.Length];
            mags = new Transform[Weapons.Length];
            magBase = new Vector3[Weapons.Length];
            armRs = new Transform[Weapons.Length];
            armLs = new Transform[Weapons.Length];
            armRBase = new Vector3[Weapons.Length];
            armLBase = new Vector3[Weapons.Length];

            for (int i = 0; i < Weapons.Length; i++)
            {
                if (Weapons[i] == null) continue;
                mag[i] = Weapons[i].MagSize;
                reserve[i] = Weapons[i].MaxReserve;
                Transform g, mz, mg, ar, al;
                models[i] = WeaponModels.BuildViewModel(Weapons[i].Kind, Weapons[i], WeaponHolder, out g, out mz, out mg, out ar, out al);
                gunRoots[i] = g;
                muzzles[i] = mz;
                mags[i] = mg;
                magBase[i] = mg != null ? mg.localPosition : Vector3.zero;
                armRs[i] = ar;
                armLs[i] = al;
                armRBase[i] = ar != null ? ar.localPosition : Vector3.zero;
                armLBase[i] = al != null ? al.localPosition : Vector3.zero;
                models[i].SetActive(i == 0);
            }
            Current = 0;
            visualIndex = 0;
            TempTimer = 0f;
            if (ThirdPersonModel != null) ThirdPersonModel.SetWeapon(Weapons[0].Kind);
        }

        /// <summary>绑定第三人称主角模型（在 Init 之后调用也可以）。</summary>
        public void BindThirdPersonModel(BlockCharacter model)
        {
            ThirdPersonModel = model;
            if (model != null && Weapons != null) model.SetWeapon(Weapons[Current].Kind);
        }

        // ------------------------------------------------------------------ 主循环

        public void Tick(bool firePressed, bool fireHeld, bool reloadPressed, bool nextPressed,
            bool prevPressed, bool placeHeld, bool inspectPressed, bool aimHeld, float dt)
        {
            if (Weapons == null) return;
            if (mounted) return;            // 在坦克里：手上的枪收起来了
            cooldown -= dt;
            BuildCooldown -= dt;
            switchLock -= dt;
            if (lunge > 0f) lunge -= dt;

            // 召唤的临时武器到点回收
            if (TempTimer > 0f)
            {
                TempTimer -= dt;
                if (TempTimer <= 0f) ClearTemp();
            }

            AdvanceAnim(dt);

            var cur = Weapons[Current];

            // 加特林：按住扳机枪管先转起来，转到位才出弹
            if (cur != null && cur.IsMinigun)
            {
                if (fireHeld && state == Vm.Idle) minigunSpin = Mathf.Min(1f, minigunSpin + dt / 0.6f);
                else minigunSpin = Mathf.Max(0f, minigunSpin - dt / 0.8f);
            }
            else minigunSpin = 0f;

            // 开镜：动作中 / 近战武器不能开镜
            adsWanted = aimHeld && state == Vm.Idle && cur.CanAds && !cur.Melee;
            ads01 = Mathf.MoveTowards(ads01, adsWanted ? 1f : 0f, dt / 0.13f);
            if (Rig != null) Rig.SetAdsZoom(ads01 * cur.AdsZoom);

            // 检视中开火 / 移动中开火：立即结束检视（CS 行为）
            if (state == Vm.Inspect && (firePressed || fireHeld)) state = Vm.Idle;

            if (state != Vm.SwitchOut && state != Vm.SwitchIn)
            {
                // Q / 滚轮上 = 下一把，滚轮下 = 上一把（环形循环，跳过空槽位）
                if (nextPressed) StartSwitch(NextSlot(Current, 1));
                else if (prevPressed) StartSwitch(NextSlot(Current, -1));
            }
            if (reloadPressed && state == Vm.Idle) StartReload();
            if (inspectPressed && state == Vm.Idle) StartInspect();

            bool infinite = cur.Flame || cur.IsMinigun;    // 召唤武器不吃弹药
            bool wantFire = cur.Auto ? fireHeld : firePressed;
            bool spinOk = !cur.IsMinigun || minigunSpin >= 0.85f;
            if (wantFire && state == Vm.Idle && cooldown <= 0f && switchLock <= 0f && spinOk)
            {
                if (cur.Melee) MeleeAttack(cur);
                else if (cur.Flame) FireFlame(cur);
                else if (infinite || mag[Current] > 0) Fire();
                else
                {
                    AudioKit.Play2D("empty", 0.5f);
                    cooldown = 0.25f;
                    StartReload();
                }
            }

            if (placeHeld && BuildCooldown <= 0f && state == Vm.Idle)
            {
                TryPlace();
                BuildCooldown = 0.18f;
            }

            ApplyPose(dt);
        }

        /// <summary>从 from 槽位沿 dir 方向找下一个非空槽（召唤武器过期后槽位 5 为空，循环要跳过去）。</summary>
        public int NextSlot(int from, int dir)
        {
            if (Weapons == null) return from;
            int n = Weapons.Length;
            for (int k = 1; k <= n; k++)
            {
                int s = ((from + dir * k) % n + n) % n;
                if (Weapons[s] != null) return s;
            }
            return from;
        }

        /// <summary>数字键 / 滚轮直选武器槽位（0 起）。</summary>
        public void SelectSlot(int slot)
        {
            if (Weapons == null) return;
            if (slot < 0 || slot >= Weapons.Length) return;
            if (Weapons[slot] == null) return;
            if (slot == Current) return;
            if (state != Vm.Idle) return;
            StartSwitch(slot);
        }

        // ------------------------------------------------------------------ 召唤的临时武器

        /// <summary>登乘坦克时把枪收起来（车里用的是主炮，手里不该还杵着一把步枪）。</summary>
        public void SetMounted(bool on)
        {
            if (mounted == on) return;
            mounted = on;
            if (models == null) return;
            for (int i = 0; i < models.Length; i++)
                if (models[i] != null) models[i].SetActive(!on && i == visualIndex);
        }

        public void GrantTemp(WeaponKind kind, float duration)
        {
            ClearTemp();
            var def = kind == WeaponKind.Flamethrower ? WeaponDef.Flamethrower() : WeaponDef.Minigun();
            Weapons[TempSlot] = def;
            mag[TempSlot] = def.MagSize;
            reserve[TempSlot] = 0;

            Transform g, mz, mg, ar, al;
            models[TempSlot] = WeaponModels.BuildViewModel(def.Kind, def, WeaponHolder, out g, out mz, out mg, out ar, out al);
            gunRoots[TempSlot] = g;
            muzzles[TempSlot] = mz;
            mags[TempSlot] = mg;
            magBase[TempSlot] = mg != null ? mg.localPosition : Vector3.zero;
            armRs[TempSlot] = ar;
            armLs[TempSlot] = al;
            armRBase[TempSlot] = ar != null ? ar.localPosition : Vector3.zero;
            armLBase[TempSlot] = al != null ? al.localPosition : Vector3.zero;
            models[TempSlot].SetActive(false);

            TempTimer = duration;
            minigunSpin = 0f;
            // 不管正在干什么，立刻切过去
            state = Vm.Idle;
            vmTime = 0f;
            StartSwitch(TempSlot);
        }

        /// <summary>回收临时武器：销毁模型、清空槽位，正在用的话切回步枪。</summary>
        public void ClearTemp()
        {
            TempTimer = 0f;
            if (Weapons[TempSlot] == null) return;
            Weapons[TempSlot] = null;
            mag[TempSlot] = 0;
            reserve[TempSlot] = 0;
            if (models[TempSlot] != null) Destroy(models[TempSlot]);
            models[TempSlot] = null;
            gunRoots[TempSlot] = null;
            muzzles[TempSlot] = null;
            mags[TempSlot] = null;
            armRs[TempSlot] = null;
            armLs[TempSlot] = null;

            bool wasCurrent = Current == TempSlot || pendingWeapon == TempSlot;
            if (wasCurrent)
            {
                if (pendingWeapon == TempSlot) pendingWeapon = -1;
                Current = 0;
                visualIndex = 0;
                if (models[0] != null) models[0].SetActive(true);
                state = Vm.Idle;
                vmTime = 0f;
                if (ThirdPersonModel != null && Weapons[0] != null) ThirdPersonModel.SetWeapon(Weapons[0].Kind);
            }
            minigunSpin = 0f;
        }

        // ------------------------------------------------------------------ 动画状态机

        private void AdvanceAnim(float dt)
        {
            if (state == Vm.Idle) { vmTime = 0f; return; }
            vmTime += dt;

            switch (state)
            {
                case Vm.Reload:
                    if (!magSfxPlayed && vmTime >= vmDur * 0.55f)
                    {
                        magSfxPlayed = true;
                        AudioKit.Play2D("reload", 0.55f, 1.35f);   // 弹匣插入声
                    }
                    if (vmTime >= vmDur)
                    {
                        FinishReload();
                        state = Vm.Idle;
                        vmTime = 0f;
                    }
                    break;

                case Vm.SwitchOut:
                    if (vmTime >= vmDur)
                    {
                        ActivatePending();
                        state = Vm.SwitchIn;
                        vmTime = 0f;
                        vmDur = 0.30f;
                    }
                    break;

                case Vm.SwitchIn:
                    if (vmTime >= vmDur) { state = Vm.Idle; vmTime = 0f; }
                    break;

                case Vm.Inspect:
                    if (vmTime >= vmDur) { state = Vm.Idle; vmTime = 0f; }
                    break;
            }
        }

        private void StartSwitch(int target)
        {
            pendingWeapon = target;
            state = Vm.SwitchOut;
            vmTime = 0f;
            vmDur = 0.20f;
            switchLock = 0.5f;
            cooldown = 0.5f;
            AudioKit.Play2D("ui", 0.45f, 1.35f);
        }

        private void ActivatePending()
        {
            if (pendingWeapon < 0) return;
            if (Weapons[pendingWeapon] == null)
            {
                // 目标槽位已被回收（临时武器到期）：直接回idle
                pendingWeapon = -1;
                state = Vm.Idle;
                vmTime = 0f;
                return;
            }
            if (models[visualIndex] != null) models[visualIndex].SetActive(false);
            Current = pendingWeapon;
            visualIndex = pendingWeapon;
            pendingWeapon = -1;
            if (models[visualIndex] != null) models[visualIndex].SetActive(true);
            if (ThirdPersonModel != null) ThirdPersonModel.SetWeapon(Weapons[Current].Kind);
            AudioKit.Play2D("ui", 0.5f, 0.9f);
        }

        private void StartReload()
        {
            if (state != Vm.Idle) return;
            if (Weapons[Current].Melee) return;      // 小刀不换弹
            if (Weapons[Current].Flame || Weapons[Current].IsMinigun) return;   // 召唤武器自带无限弹药
            if (mag[Current] >= Weapons[Current].MagSize) return;
            if (reserve[Current] <= 0) return;
            state = Vm.Reload;
            vmTime = 0f;
            vmDur = Mathf.Max(0.35f, Weapons[Current].ReloadTime);
            magSfxPlayed = false;
            AudioKit.Play2D("reload", 0.7f);   // 卸弹匣声
        }

        private void FinishReload()
        {
            var def = Weapons[Current];
            int need = def.MagSize - mag[Current];
            int take = Mathf.Min(need, reserve[Current]);
            mag[Current] += take;
            reserve[Current] -= take;
        }

        private void StartInspect()
        {
            state = Vm.Inspect;
            vmTime = 0f;
            vmDur = 1.7f;
            AudioKit.Play2D("ui", 0.35f, 0.75f);
        }

        /// <summary>把状态机的结果写进视图模型的 transform。</summary>
        private void ApplyPose(float dt)
        {
            int i = visualIndex;
            if (i < 0 || i >= models.Length || models[i] == null) return;
            Transform root = models[i].transform;
            Transform gun = gunRoots[i];

            Vector3 rp = Vector3.zero;
            Vector3 rr = Vector3.zero;
            Vector3 gp = Vector3.zero;
            Vector3 gr = Vector3.zero;
            Vector3 hp = Vector3.zero;      // 右手（扳机手）相对原始位置的位移
            Vector3 hr = Vector3.zero;      // 右手额外的旋转
            Vector3 lp = Vector3.zero;      // 左手（扶枪手）的微量调整
            float magDrop = 0f;

            switch (state)
            {
                case Vm.SwitchOut:
                {
                    float t = Ease(Mathf.Clamp01(vmTime / vmDur), true);
                    rp = new Vector3(0f, -0.55f * t, -0.06f * t);
                    rr = new Vector3(0f, 12f * t, 62f * t);
                    break;
                }
                case Vm.SwitchIn:
                {
                    float t = Mathf.Clamp01(vmTime / vmDur);
                    float k = 1f - OutBack(t);
                    rp = new Vector3(0f, -0.55f * k, -0.06f * k);
                    rr = new Vector3(0f, 12f * k, 62f * k);
                    break;
                }
                case Vm.Reload:
                {
                    float t = Mathf.Clamp01(vmTime / vmDur);
                    // 下沉 -> 抽插弹匣 -> 回位
                    float w = Smooth(0f, 0.22f, t) * (1f - Smooth(0.70f, 1f, t));
                    gp = new Vector3(0.07f * w, -0.14f * w, -0.03f * w);
                    gr = new Vector3(-26f * w, 30f * w, 40f * w);
                    float mt = Mathf.Clamp01((t - 0.22f) / 0.48f);
                    magDrop = Mathf.Sin(Mathf.PI * Mathf.Clamp01(mt)) * 0.16f;

                    // 右手：松开握把 → 伸到弹匣口 → 插好弹匣后抓回握把。
                    // 手必须和弹匣保持同一个目标点，否则会出现"枪动了手还在原地"。
                    float grab = Mathf.Clamp01(Smooth(0.05f, 0.28f, t) * (1f - Smooth(0.62f, 0.92f, t)));
                    Vector3 magPos = magBase[i];
                    // 弹匣往下抽时手跟着往下走，看得出确实是这只手在换弹匣
                    Vector3 handTarget = magPos + new Vector3(0.085f, 0.10f - magDrop * 0.85f, 0.02f);
                    hp = (handTarget - armRBase[i]) * grab;
                    hr = new Vector3(-16f * grab, 0f, 24f * grab);
                    // 左手始终扶着护木，跟着枪一起下沉/抬起
                    lp = new Vector3(0f, -0.015f * w, 0.04f * w);
                    break;
                }
                case Vm.Inspect:
                {
                    float t = Mathf.Clamp01(vmTime / vmDur);
                    float w = Smooth(0f, 0.18f, t) * (1f - Smooth(0.78f, 1f, t));
                    float spin = Mathf.Sin(vmTime * 3.2f) * 14f;
                    gp = new Vector3(-0.17f * w, -0.03f * w, -0.10f * w);
                    gr = new Vector3(-20f * w, (-42f + spin) * w, 30f * w);
                    break;
                }
            }

            // 开镜：把枪抬到屏幕正中（准星位置），否则高倍镜会歪到一边
            if (ads01 > 0.001f)
            {
                rp += new Vector3(-0.30f, 0.105f, -0.155f) * ads01;
                rr += new Vector3(-1.5f, -3.5f, 5f) * ads01;
            }

            // 近战突刺：刀往前送一下再收回来
            if (lunge > 0f)
            {
                float k = Mathf.Sin(Mathf.PI * Mathf.Clamp01(lunge / 0.30f));
                gp += new Vector3(0f, -0.015f, 0.34f) * k;
                gr += new Vector3(-22f, 0f, -30f) * k;
            }

            float damp = 1f - Mathf.Pow(0.0001f, dt);
            root.localPosition = Vector3.Lerp(root.localPosition, rp, damp);
            root.localRotation = Quaternion.Slerp(root.localRotation, Quaternion.Euler(rr), damp);
            if (gun != null)
            {
                gun.localPosition = Vector3.Lerp(gun.localPosition, gp, damp);
                gun.localRotation = Quaternion.Slerp(gun.localRotation, Quaternion.Euler(gr), damp);
            }
            if (mags[i] != null)
                mags[i].localPosition = Vector3.Lerp(mags[i].localPosition,
                    magBase[i] + new Vector3(0f, -magDrop, 0f), damp);

            // 双手：挂 Gun 之下，默认完全跟随枪；换弹时右手再叠加"去拿弹匣"的位移
            if (armRs[i] != null)
            {
                armRs[i].localPosition = Vector3.Lerp(armRs[i].localPosition, armRBase[i] + hp, damp);
                armRs[i].localRotation = Quaternion.Slerp(armRs[i].localRotation, Quaternion.Euler(hr), damp);
            }
            if (armLs[i] != null)
                armLs[i].localPosition = Vector3.Lerp(armLs[i].localPosition, armLBase[i] + lp, damp);
        }

        private static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / Mathf.Max(0.0001f, b - a));
            return t * t * (3f - 2f * t);
        }

        private static float Ease(float t, bool inCurve)
        {
            return inCurve ? t * t : 1f - (1f - t) * (1f - t);
        }

        private static float OutBack(float t)
        {
            float c1 = 1.9f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        // ------------------------------------------------------------------ 弹药

        public void AddAmmo(int index, int amount)
        {
            if (reserve == null || index < 0 || index >= reserve.Length) return;
            if (Weapons[index] == null) return;
            reserve[index] = Mathf.Min(reserve[index] + amount, Weapons[index].MaxReserve);
        }

        public void RefillAll()
        {
            for (int i = 0; i < Weapons.Length; i++)
            {
                if (Weapons[i] == null) continue;
                mag[i] = Weapons[i].MagSize;
                reserve[i] = Weapons[i].MaxReserve;
            }
            state = Vm.Idle;
            vmTime = 0f;
            cooldown = 0f;
            switchLock = 0f;
        }

        // ------------------------------------------------------------------ 射击

        private void Fire()
        {
            var def = Weapons[Current];
            mag[Current]--;
            cooldown = def.FireInterval;

            Ray ray = Rig.AimRay();
            // 子弹起点：第一人称用视图模型枪口；第三人称必须用主角手里那把枪的枪口，
            // 否则子弹会从相机（主角后上方）凭空飞出来，看着像"从空中射出"。
            Vector3 origin = MuzzleWorldPosition(ray);
            Vector3 dir = ray.direction;

            // 移动端轻微辅助瞄准
            if (GameInput.TouchMode) dir = AssistAim(ray.origin, dir, def.Range);

            float adsRecoil = 1f - ads01 * 0.35f;
            Rig.AddRecoil(def.RecoilPitch * adsRecoil, def.RecoilYaw * adsRecoil);
            Rig.Shake(def.Shake * adsRecoil);
            Rig.KickFov(def.Kind == WeaponKind.Sniper ? 5f : 1.2f);
            AudioKit.Play2D(def.Sfx, 0.85f, UnityEngine.Random.Range(0.96f, 1.04f));
            // 枪声会把附近的敌人引过来查看（狙击最响、步枪次之）
            var noise = GameManager.Instance;
            if (noise != null)
                noise.MakeNoise(origin, def.Kind == WeaponKind.Sniper ? 34f : (def.Kind == WeaponKind.Shotgun ? 30f : 26f));
            if (FxPool.Instance != null) FxPool.Instance.Flash(origin + dir * 0.25f, new Color(1f, 0.9f, 0.5f), 0.26f);

            float spread = def.Spread * Mathf.Lerp(1f, def.AdsSpreadMul, ads01);
            for (int i = 0; i < def.Pellets; i++)
            {
                Vector3 d = dir;
                if (spread > 0f)
                {
                    float sx = UnityEngine.Random.Range(-spread, spread);
                    float sy = UnityEngine.Random.Range(-spread, spread);
                    d = Quaternion.Euler(sy, sx, 0f) * d;
                }
                ShootOne(origin, ray.origin, d, def);
            }

            // 弹壳
            if (FxPool.Instance != null)
                FxPool.Instance.Burst(origin + Vector3.up * 0.05f, new Color(0.85f, 0.75f, 0.3f), 1, 0.6f);
        }

        /// <summary>
        /// 子弹的真正起点。第三人称时相机在主角身后上方，如果还用相机位置当枪口，
        /// 子弹就会"从半空中射出去"——这里改成取主角手里那把枪的枪口。
        /// </summary>
        private Vector3 MuzzleWorldPosition(Ray ray)
        {
            if (Rig != null && Rig.Mode == ViewMode.ThirdPerson && ThirdPersonModel != null && ThirdPersonModel.Muzzle != null)
                return ThirdPersonModel.MuzzlePosition;

            Transform mz = muzzles != null && visualIndex >= 0 && visualIndex < muzzles.Length ? muzzles[visualIndex] : null;
            return mz != null ? mz.position : ray.origin;
        }

        // ------------------------------------------------------------------ 喷火

        /// <summary>喷火器：喷出短命火焰弹（不破坏方块，命中持续掉血）。</summary>
        private void FireFlame(WeaponDef def)
        {
            cooldown = def.FireInterval;
            Ray ray = Rig.AimRay();
            Vector3 dir = ray.direction;

            // 火焰团以前直接从相机跟前的枪口喷出来，一大团橙色方块正好糊在准星上，
            // 等于把自己视野全挡了。改成从相机右下方、身前 1.3 米以外开始生成 ——
            // 看上去像"从枪口斜着喷出去"，准星附近始终是干净的。
            Vector3 origin = MuzzleWorldPosition(ray);
            var cam = Rig != null ? Rig.Cam : null;
            if (cam != null)
            {
                Vector3 side = Vector3.Cross(dir, Vector3.up);
                if (side.sqrMagnitude < 0.001f) side = cam.transform.right;
                side.Normalize();
                Vector3 c = cam.transform.position;
                Vector3 want = c + dir * 1.35f + side * 0.36f - Vector3.up * 0.32f;
                // 贴墙时那个点会在方块里，退回靠近相机的位置（否则一喷出来就撞墙消失）
                origin = (World != null && World.IsSolidAt(
                    Mathf.FloorToInt(want.x), Mathf.FloorToInt(want.y), Mathf.FloorToInt(want.z)))
                    ? c + dir * 0.75f : want;
            }

            // 一发一颗（原来两颗），颗颗更大更狠：粒子少一半，视野自然清爽，DPS 不变
            Vector3 d = Quaternion.Euler(UnityEngine.Random.Range(-2.8f, 2.8f),
                UnityEngine.Random.Range(-2.8f, 2.8f), 0f) * dir;
            ProjectileSystem.FireFlame(World, origin, d, 17f, def.Damage * 2f);

            // 音效节流：每 0.2s 响一声，否则每帧都是噪音
            flameSfxCd -= def.FireInterval;
            if (flameSfxCd <= 0f)
            {
                flameSfxCd = 0.2f;
                AudioKit.Play("flame", origin, 0.4f, UnityEngine.Random.Range(0.9f, 1.1f));
            }
            if (FxPool.Instance != null)
                FxPool.Instance.Flash(origin + dir * 0.9f, new Color(1f, 0.55f, 0.2f), 0.22f);
            var gm = GameManager.Instance;
            if (gm != null) gm.MakeNoise(origin, 16f);
        }

        // ------------------------------------------------------------------ 近战

        private void MeleeAttack(WeaponDef def)
        {
            cooldown = def.FireInterval;
            lunge = 0.30f;

            Ray ray = Rig.AimRay();
            AudioKit.Play2D(def.Sfx, 0.7f, UnityEngine.Random.Range(0.92f, 1.1f));

            var gm = GameManager.Instance;
            EnemyAI hit = null;
            float bestD = float.MaxValue;
            if (gm != null)
            {
                for (int i = 0; i < gm.Enemies.Count; i++)
                {
                    var e = gm.Enemies[i];
                    if (e == null || !e.Alive) continue;
                    Vector3 c = e.AimPoint;
                    Vector3 to = c - ray.origin;
                    float d = to.magnitude;
                    if (d > def.Range + 0.8f || d < 0.001f) continue;
                    if (Vector3.Angle(ray.direction, to / d) > 58f) continue;
                    if (World != null && !World.HasLineOfSight(ray.origin, c)) continue;
                    if (d < bestD) { bestD = d; hit = e; }
                }
            }

            if (hit != null)
            {
                // 从背后捅：伤害更高（潜行玩法）
                Vector3 away = (ray.origin - hit.transform.position);
                away.y = 0f;
                bool back = hit.Model != null && away.sqrMagnitude > 0.01f &&
                            Vector3.Dot(hit.Model.transform.forward, away.normalized) < -0.25f;
                float dmg = def.Damage * (back ? 1.9f : 1f);
                hit.TakeDamage(dmg, hit.AimPoint, false);
                if (FxPool.Instance != null)
                {
                    FxPool.Instance.Burst(hit.AimPoint, new Color(0.85f, 0.15f, 0.15f), back ? 10 : 6, 0.9f);
                    FxPool.Instance.Tracer(MuzzleWorldPosition(ray), hit.AimPoint, new Color(0.95f, 0.97f, 1f), 0.035f);
                }
                AudioKit.Play(back ? "headshot" : "hit", hit.AimPoint, 0.8f);
                if (gm != null && gm.Hud != null) gm.Hud.ShowHitmarker(back);
                return;
            }

            // 没捅到人：砍方块
            if (World != null && View != null)
            {
                Vector3Int cell, n; Vector3 p;
                if (World.RaycastBlocks(ray.origin, ray.direction, def.Range, out p, out cell, out n))
                {
                    byte broken = (byte)BlockId.Air;
                    if (View.DamageBlock(cell, def.BlockDamage, out broken))
                    {
                        if (FxPool.Instance != null)
                            FxPool.Instance.Burst(new Vector3(cell.x + 0.5f, cell.y + 0.5f, cell.z + 0.5f), BlockDef.DebrisColor(broken), 7, 1.0f);
                        AudioKit.Play("block", p, 0.5f, UnityEngine.Random.Range(0.9f, 1.1f));
                    }
                    else if (FxPool.Instance != null)
                    {
                        FxPool.Instance.Flash(p, new Color(0.9f, 0.95f, 1f), 0.12f);
                    }
                }
            }
        }

        private Vector3 AssistAim(Vector3 origin, Vector3 dir, float range)
        {
            var gm = GameManager.Instance;
            if (gm == null) return dir;
            EnemyAI best = null;
            float bestAngle = 8f;
            for (int i = 0; i < gm.Enemies.Count; i++)
            {
                var e = gm.Enemies[i];
                if (e == null || !e.Alive) continue;
                Vector3 to = e.AimPoint - origin;
                float dist = to.magnitude;
                if (dist > range) continue;
                float ang = Vector3.Angle(dir, to / dist);
                if (ang < bestAngle && gm.World.HasLineOfSight(origin, e.AimPoint))
                {
                    bestAngle = ang;
                    best = e;
                }
            }
            if (best != null) return (best.AimPoint - origin).normalized;
            return dir;
        }

        private void ShootOne(Vector3 origin, Vector3 rayOrigin, Vector3 dir, WeaponDef def)
        {
            // 注意：out 参数写在 && 之后属于“可能不执行”，必须给初值，否则 CS0165
            Vector3Int cell = Vector3Int.zero;
            Vector3Int normal = Vector3Int.zero;
            Vector3 point = rayOrigin;
            bool hitBlock = World != null && World.RaycastBlocks(rayOrigin, dir, def.Range, out point, out cell, out normal);
            float blockDist = hitBlock ? Vector3.Distance(rayOrigin, point) : float.MaxValue;

            // 命中敌人
            var gm = GameManager.Instance;
            EnemyAI hitEnemy = null;
            float enemyDist = float.MaxValue;
            Vector3 enemyPoint = Vector3.zero;
            if (gm != null)
            {
                for (int i = 0; i < gm.Enemies.Count; i++)
                {
                    var e = gm.Enemies[i];
                    if (e == null || !e.Alive) continue;
                    float d;
                    if (RayMath.RayAABB(rayOrigin, dir, e.BoundsMin, e.BoundsMax, out d) && d < enemyDist)
                    {
                        enemyDist = d;
                        hitEnemy = e;
                        enemyPoint = rayOrigin + dir * d;
                    }
                }
            }

            if (hitEnemy != null && enemyDist < blockDist)
            {
                bool head = enemyPoint.y > hitEnemy.BoundsMax.y - 0.42f;
                float dmg = def.Damage * (head ? 2.2f : 1f);
                hitEnemy.TakeDamage(dmg, enemyPoint, head);
                if (FxPool.Instance != null)
                    FxPool.Instance.Burst(enemyPoint, new Color(0.75f, 0.12f, 0.12f), head ? 8 : 4, 0.8f);
                AudioKit.Play(head ? "headshot" : "hit", enemyPoint, 0.7f);
                if (gm != null && gm.Hud != null) gm.Hud.ShowHitmarker(head);
                if (FxPool.Instance != null) FxPool.Instance.Tracer(origin, enemyPoint, new Color(1f, 0.95f, 0.6f), 0.045f);
                return;
            }

            if (hitBlock)
            {
                byte broken = (byte)BlockId.Air;
                bool broke = View != null && View.DamageBlock(cell, def.BlockDamage, out broken);
                if (broke)
                {
                    if (FxPool.Instance != null)
                        FxPool.Instance.Burst(new Vector3(cell.x + 0.5f, cell.y + 0.5f, cell.z + 0.5f), BlockDef.DebrisColor(broken), 9, 1.1f);
                    AudioKit.Play("block", point, 0.55f, UnityEngine.Random.Range(0.9f, 1.1f));
                }
                else
                {
                    if (FxPool.Instance != null)
                    {
                        FxPool.Instance.Burst(point, BlockDef.DebrisColor(World.Get(cell.x, cell.y, cell.z)), 2, 0.5f);
                        FxPool.Instance.Flash(point, new Color(1f, 0.85f, 0.55f), 0.14f);
                    }
                }
                if (FxPool.Instance != null) FxPool.Instance.Tracer(origin, point, new Color(1f, 0.95f, 0.6f), 0.04f);
            }
            else if (FxPool.Instance != null)
            {
                FxPool.Instance.Tracer(origin, rayOrigin + dir * def.Range, new Color(1f, 0.95f, 0.6f), 0.03f);
            }
        }

        private void TryPlace()
        {
            if (World == null || View == null) return;
            Ray ray = Rig.AimRay();
            Vector3Int cell; Vector3Int normal; Vector3 point;
            if (!World.RaycastBlocks(ray.origin, ray.direction, 7f, out point, out cell, out normal)) return;
            Bounds b = Player.WorldBounds;
            if (View.PlaceBlock(cell, normal, BuildBlock, b.min, b.max))
            {
                AudioKit.Play("place", point, 0.7f, UnityEngine.Random.Range(0.95f, 1.05f));
                if (FxPool.Instance != null)
                    FxPool.Instance.Burst(point + (Vector3)normal * 0.5f, BlockDef.DebrisColor(BuildBlock), 3, 0.4f);
            }
        }
    }
}
