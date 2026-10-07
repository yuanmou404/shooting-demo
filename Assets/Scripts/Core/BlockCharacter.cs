using UnityEngine;

namespace PixelArena
{
    /// <summary>
    /// 由方块拼出来的角色（我的世界 / 像素射击 风格），纯代码生成，无美术资源。
    /// 原点在脚底中心，总高约 1.8m。
    /// 主角（英雄）与 NPC 使用不同的配色和装备，轮廓一眼可辨。
    ///
    /// 手臂是两段式的（上臂 + 前臂，中间有肘关节），持枪姿势用两骨 IK 求解：
    /// 枪定义姿势（枪托抵肩、握把在胸前），两条手臂再"抓"到握把和护木上，
    /// 所以看起来是右手扣扳机、左手托枪，而不是两只手直勾勾地伸出去。
    /// </summary>
    public class BlockCharacter : MonoBehaviour
    {
        public Transform Head;
        public Transform Body;
        public Transform ArmL;      // 左肩枢轴（上臂）
        public Transform ArmR;      // 右肩枢轴（上臂）
        public Transform ForeL;     // 左肘枢轴（前臂）
        public Transform ForeR;     // 右肘枢轴（前臂）
        public Transform LegL;
        public Transform LegR;
        public Transform WeaponMount;
        public Transform Muzzle;        // 手上那把枪的枪口（世界模型里的 MuzzlePoint）

        // ---- 骨架尺寸（两段手臂，总长 = UpperLen + ForeLen = 0.68）----
        private const float UpperLen = 0.34f;
        private const float ForeLen = 0.34f;

        private static readonly Vector3 ShoulderL = new Vector3(-0.38f, 1.32f, 0f);
        private static readonly Vector3 ShoulderR = new Vector3(0.38f, 1.32f, 0f);

        /// <summary>
        /// 持枪时枪在角色局部空间里的挂点（模型原点）。
        /// 必须放在<b>躯干右侧、肩膀前方</b>：躯干半宽 0.29、胸前的战术背心半宽 0.235，
        /// 所以 x 取 0.27 才能让整把枪走在身体外侧，不会"从胸口中间穿过去"。
        /// z 取 0.30 让枪托正好抵在右肩前，枪身向前伸出。
        /// </summary>
        private static readonly Vector3 AimMount = new Vector3(0.28f, 1.29f, 0.30f);

        /// <summary>
        /// 瞄准时的"侧身站姿"：左肩往前内侧转（真实射手会侧身，我们的肩关节是固定的，
        /// 只能靠移动左臂枢轴来模拟）。不改这一步的话，左手根本够不到挂在右肩外侧的枪 ——
        /// 肩到肩 0.76m 的横向距离本身就超过手臂长度了。
        /// </summary>
        private static readonly Vector3 BladeShoulderL = new Vector3(0.16f, 0f, 0.18f);

        private float phase;
        private GameObject gun;
        private WeaponKind weapon = WeaponKind.Rifle;
        private bool crouched;
        private bool visible = true;

        private static readonly Color HeroShirt = new Color(0.20f, 0.34f, 0.62f);
        private static readonly Color HeroPants = new Color(0.16f, 0.19f, 0.26f);
        private static readonly Color HeroArmor = new Color(0.15f, 0.26f, 0.46f);
        private static readonly Color HeroTrim = new Color(0.35f, 0.85f, 0.95f);
        private static readonly Color Pack = new Color(0.30f, 0.34f, 0.22f);
        private static readonly Color Dark = new Color(0.12f, 0.13f, 0.15f);

        // ------------------------------------------------------------------ 构建

        public static BlockCharacter Create(Transform parent, Color skin, Color shirt, Color pants, Color hair)
        {
            var root = new GameObject("BlockCharacter");
            if (parent != null) root.transform.SetParent(parent, false);
            var bc = root.AddComponent<BlockCharacter>();

            bc.Body = VoxelAssets.MakeBox("Body", root.transform, new Vector3(0f, 1.05f, 0f), new Vector3(0.58f, 0.66f, 0.32f), shirt).transform;
            bc.Head = VoxelAssets.MakeBox("Head", root.transform, new Vector3(0f, 1.59f, 0f), new Vector3(0.44f, 0.44f, 0.44f), skin).transform;
            VoxelAssets.MakeBox("Hair", bc.Head, new Vector3(0f, 0.28f, 0f), new Vector3(1.06f, 0.42f, 1.06f), hair);

            bc.LegL = VoxelAssets.MakeLimb(root.transform, new Vector3(-0.15f, 0.72f, 0f), new Vector3(0.24f, 0.72f, 0.24f), new Vector3(0f, -0.36f, 0f), pants);
            bc.LegR = VoxelAssets.MakeLimb(root.transform, new Vector3(0.15f, 0.72f, 0f), new Vector3(0.24f, 0.72f, 0.24f), new Vector3(0f, -0.36f, 0f), pants);

            bc.ArmL = MakeArm(root.transform, ShoulderL, shirt, out bc.ForeL);
            bc.ArmR = MakeArm(root.transform, ShoulderR, shirt, out bc.ForeR);

            var mount = new GameObject("WeaponMount");
            mount.transform.SetParent(root.transform, false);
            mount.transform.localPosition = AimMount;
            mount.transform.localRotation = Quaternion.identity;
            bc.WeaponMount = mount.transform;

            return bc;
        }

        /// <summary>两段式手臂：上臂枢轴在肩，前臂枢轴在肘。</summary>
        private static Transform MakeArm(Transform parent, Vector3 shoulder, Color color, out Transform fore)
        {
            var upper = VoxelAssets.MakeLimb(parent, shoulder,
                new Vector3(0.19f, UpperLen, 0.19f), new Vector3(0f, -UpperLen * 0.5f, 0f), color);
            fore = VoxelAssets.MakeLimb(upper, new Vector3(0f, -UpperLen, 0f),
                new Vector3(0.17f, ForeLen, 0.17f), new Vector3(0f, -ForeLen * 0.5f, 0f), color);
            return upper;
        }

        /// <summary>主角：头盔 + 护目镜 + 战术背心 + 背包 + 护肩 + 战术靴，配色也和 NPC 完全不同。</summary>
        public static BlockCharacter CreateHero(Transform parent)
        {
            var bc = Create(parent, new Color(0.88f, 0.72f, 0.56f), HeroShirt, HeroPants, new Color(0.16f, 0.12f, 0.09f));
            bc.name = "HeroModel";
            Transform root = bc.transform;

            // 头盔 + 帽檐
            VoxelAssets.MakeBox("Helmet", root, new Vector3(0f, 1.72f, 0f), new Vector3(0.50f, 0.22f, 0.50f), HeroArmor);
            VoxelAssets.MakeBox("Brim", root, new Vector3(0f, 1.63f, 0.23f), new Vector3(0.46f, 0.05f, 0.08f), HeroArmor);
            // 护目镜灯带（正面）
            VoxelAssets.MakeBox("Visor", root, new Vector3(0f, 1.585f, 0.225f), new Vector3(0.34f, 0.09f, 0.03f), HeroTrim);

            // 战术背心 + 胸挂
            VoxelAssets.MakeBox("Vest", root, new Vector3(0f, 1.05f, 0.185f), new Vector3(0.47f, 0.44f, 0.07f), HeroArmor);
            VoxelAssets.MakeBox("Pouch", root, new Vector3(0f, 0.96f, 0.235f), new Vector3(0.22f, 0.15f, 0.05f), Pack);
            VoxelAssets.MakeBox("Buckle", root, new Vector3(0f, 1.24f, 0.235f), new Vector3(0.14f, 0.06f, 0.04f), HeroTrim);
            // 背包
            VoxelAssets.MakeBox("Backpack", root, new Vector3(0f, 1.12f, -0.27f), new Vector3(0.40f, 0.46f, 0.16f), Pack);
            VoxelAssets.MakeBox("Antenna", root, new Vector3(0.14f, 1.48f, -0.27f), new Vector3(0.04f, 0.30f, 0.04f), Dark);

            // 护肩
            VoxelAssets.MakeBox("PadL", root, new Vector3(-0.40f, 1.42f, 0f), new Vector3(0.24f, 0.13f, 0.30f), HeroArmor);
            VoxelAssets.MakeBox("PadR", root, new Vector3(0.40f, 1.42f, 0f), new Vector3(0.24f, 0.13f, 0.30f), HeroArmor);

            // 臂章 & 手套（挂在肘关节下面，跟着前臂动） & 靴子
            VoxelAssets.MakeBox("Band", bc.ArmL, new Vector3(0f, -0.20f, 0f), new Vector3(0.21f, 0.07f, 0.21f), HeroTrim);
            VoxelAssets.MakeBox("GloveR", bc.ForeR, new Vector3(0f, -ForeLen + 0.04f, 0f), new Vector3(0.20f, 0.12f, 0.20f), Dark);
            VoxelAssets.MakeBox("GloveL", bc.ForeL, new Vector3(0f, -ForeLen + 0.04f, 0f), new Vector3(0.20f, 0.12f, 0.20f), Dark);
            VoxelAssets.MakeBox("BootL", bc.LegL, new Vector3(0f, -0.68f, 0.02f), new Vector3(0.26f, 0.11f, 0.30f), Dark);
            VoxelAssets.MakeBox("BootR", bc.LegR, new Vector3(0f, -0.68f, 0.02f), new Vector3(0.26f, 0.11f, 0.30f), Dark);

            return bc;
        }

        /// <summary>NPC 外观：按兵种改变体型与配件，和主角明显不同。</summary>
        public void ApplyNpcVariant(EnemyKind kind)
        {
            Transform root = transform;
            if (kind == EnemyKind.Elite)
            {
                // 精英：深色重甲 + 金色装饰 + 红色目镜，一眼看出不是杂兵
                Body.localScale = new Vector3(0.66f, 0.70f, 0.42f);
                Head.localScale = new Vector3(0.48f, 0.46f, 0.48f);
                Color steel = new Color(0.20f, 0.21f, 0.26f);
                Color gold = new Color(0.78f, 0.64f, 0.24f);
                VoxelAssets.MakeBox("Helmet", root, new Vector3(0f, 1.74f, 0f), new Vector3(0.54f, 0.26f, 0.54f), steel);
                VoxelAssets.MakeBox("Visor", root, new Vector3(0f, 1.62f, 0.235f), new Vector3(0.34f, 0.07f, 0.03f), new Color(0.85f, 0.22f, 0.18f));
                VoxelAssets.MakeBox("Crest", root, new Vector3(0f, 1.92f, -0.02f), new Vector3(0.07f, 0.16f, 0.34f), gold);
                VoxelAssets.MakeBox("Chest", root, new Vector3(0f, 1.08f, 0.20f), new Vector3(0.44f, 0.40f, 0.06f), steel);
                VoxelAssets.MakeBox("Trim", root, new Vector3(0f, 1.26f, 0.235f), new Vector3(0.30f, 0.05f, 0.03f), gold);
                VoxelAssets.MakeBox("PadL", root, new Vector3(-0.42f, 1.44f, 0f), new Vector3(0.26f, 0.17f, 0.32f), steel);
                VoxelAssets.MakeBox("PadR", root, new Vector3(0.42f, 1.44f, 0f), new Vector3(0.26f, 0.17f, 0.32f), steel);
                VoxelAssets.MakeBox("PadTrimL", root, new Vector3(-0.42f, 1.53f, 0f), new Vector3(0.28f, 0.04f, 0.34f), gold);
                VoxelAssets.MakeBox("PadTrimR", root, new Vector3(0.42f, 1.53f, 0f), new Vector3(0.28f, 0.04f, 0.34f), gold);
                VoxelAssets.MakeBox("Pack", root, new Vector3(0f, 1.10f, -0.30f), new Vector3(0.42f, 0.44f, 0.18f), new Color(0.18f, 0.19f, 0.23f));
                VoxelAssets.MakeBox("Radio", root, new Vector3(0.15f, 1.36f, -0.30f), new Vector3(0.05f, 0.24f, 0.05f), gold);
                VoxelAssets.MakeBox("KneeL", LegL, new Vector3(0f, -0.42f, 0f), new Vector3(0.28f, 0.14f, 0.28f), steel);
                VoxelAssets.MakeBox("KneeR", LegR, new Vector3(0f, -0.42f, 0f), new Vector3(0.28f, 0.14f, 0.28f), steel);
            }
            else if (kind == EnemyKind.Heavy)
            {
                Body.localScale = new Vector3(0.74f, 0.70f, 0.46f);
                Head.localScale = new Vector3(0.50f, 0.48f, 0.50f);
                VoxelAssets.MakeBox("Helmet", root, new Vector3(0f, 1.74f, 0f), new Vector3(0.56f, 0.24f, 0.56f), new Color(0.36f, 0.38f, 0.42f));
                VoxelAssets.MakeBox("PadL", root, new Vector3(-0.44f, 1.44f, 0f), new Vector3(0.26f, 0.16f, 0.32f), new Color(0.40f, 0.42f, 0.46f));
                VoxelAssets.MakeBox("PadR", root, new Vector3(0.44f, 1.44f, 0f), new Vector3(0.26f, 0.16f, 0.32f), new Color(0.40f, 0.42f, 0.46f));
                VoxelAssets.MakeBox("Pack", root, new Vector3(0f, 1.10f, -0.30f), new Vector3(0.44f, 0.42f, 0.18f), new Color(0.26f, 0.30f, 0.24f));
            }
            else if (kind == EnemyKind.Rusher)
            {
                Body.localScale = new Vector3(0.48f, 0.62f, 0.26f);
                VoxelAssets.MakeBox("Band", root, new Vector3(0f, 1.70f, 0f), new Vector3(0.47f, 0.07f, 0.47f), new Color(0.85f, 0.25f, 0.18f));
                VoxelAssets.MakeBox("Pouch", root, new Vector3(0f, 0.94f, 0.18f), new Vector3(0.24f, 0.14f, 0.06f), new Color(0.30f, 0.28f, 0.20f));
            }
            else
            {
                // 步兵：一顶布帽，轮廓最"路人"
                VoxelAssets.MakeBox("Cap", root, new Vector3(0f, 1.83f, 0f), new Vector3(0.48f, 0.06f, 0.48f), new Color(0.30f, 0.32f, 0.38f));
                VoxelAssets.MakeBox("CapBrim", root, new Vector3(0f, 1.79f, 0.24f), new Vector3(0.44f, 0.04f, 0.14f), new Color(0.26f, 0.28f, 0.34f));
            }
        }

        // ------------------------------------------------------------------ 武器

        /// <summary>给角色换上指定种类的枪（会自动销毁旧模型）。</summary>
        public void SetWeapon(WeaponKind kind)
        {
            if (gun != null) Destroy(gun);
            weapon = kind;
            gun = WeaponModels.BuildWorldModel(kind, WeaponMount);
            if (gun != null)
            {
                // 模型的原点略在握把之前，往枪口方向挪一点，让"握把"正好落在挂点（手上）
                gun.transform.localPosition = new Vector3(0f, 0f, GripShift(kind));
                gun.transform.localRotation = Quaternion.identity;
                Muzzle = gun.transform.Find("MuzzlePoint");
            }
            else Muzzle = null;
            // 角色当前处于隐藏状态（例如第一人称）时，新生成的枪也必须保持隐藏
            if (!visible && gun != null)
            {
                var rs = gun.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < rs.Length; i++) rs[i].enabled = false;
            }
        }

        public WeaponKind Weapon => weapon;

        /// <summary>世界模型里握把相对模型原点往前/往后的量。</summary>
        private static float GripShift(WeaponKind k)
        {
            switch (k)
            {
                case WeaponKind.Shotgun: return 0.04f;
                case WeaponKind.Pistol: return -0.02f;
                case WeaponKind.Knife: return 0.04f;
                default: return 0.06f;
            }
        }

        /// <summary>单手武器：左手不抓枪，改成自然下垂/戒备。</summary>
        private static bool TwoHanded(WeaponKind k)
        {
            return k != WeaponKind.Pistol && k != WeaponKind.Knife;
        }

        /// <summary>
        /// 各武器握持点的微调：小刀不像长枪那样端在肩上，
        /// 而是压低到腰侧、刀尖略微上扬并往内扣（单手持刀的戒备姿势）。
        /// </summary>
        private static Vector3 MountOffset(WeaponKind k)
        {
            if (k == WeaponKind.Knife) return new Vector3(0.02f, -0.16f, -0.10f);
            if (k == WeaponKind.Pistol) return new Vector3(0.02f, -0.03f, 0f);
            return Vector3.zero;
        }

        /// <summary>握持点的额外旋转（度）。小刀：刀尖上扬 + 往内扣。</summary>
        private static Vector3 MountEuler(WeaponKind k)
        {
            if (k == WeaponKind.Knife) return new Vector3(-16f, -24f, 0f);
            if (k == WeaponKind.Pistol) return new Vector3(0f, -6f, 0f);
            return Vector3.zero;
        }

        /// <summary>
        /// 左手托握点相对挂点沿枪身（模型本地 +Z）向前的距离。
        /// 取值必须落在护木 / 泵 / 两脚架那一段上，否则手会"抓空"。
        /// </summary>
        private static float SupportOffset(WeaponKind k)
        {
            switch (k)
            {
                case WeaponKind.Shotgun: return 0.24f;   // 木质泵
                case WeaponKind.Sniper: return 0.26f;    // 枪管前段
                case WeaponKind.SMG: return 0.14f;       // 机匣前
                case WeaponKind.LMG: return 0.26f;       // 两脚架上方
                default: return 0.30f;                   // 步枪护木
            }
        }

        // ------------------------------------------------------------------ 动画

        /// <summary>
        /// 动画驱动。
        /// 参数 aimElevation 的语义是"枪口仰角"：<b>正数 = 枪口朝上、负数 = 枪口朝下</b>。
        /// 注意它不是 CameraRig.Pitch（那里抬头为负），调用方需要自己做符号转换。
        /// </summary>
        public void Animate(float speed01, float dt, bool aiming, float aimElevation = 0f, bool crouch = false)
        {
            phase += dt * (4f + speed01 * 8f);
            float swing = Mathf.Sin(phase) * 34f * speed01;
            crouched = crouch;
            float pitch = Mathf.Clamp(aimElevation, -72f, 72f);

            float bob = Mathf.Sin(phase * 2f) * 0.02f * speed01;
            float drop = crouch ? 0.26f : 0f;

            if (aiming)
            {
                // 1) 先摆枪：枪托抵肩，枪身随视角俯仰抬落（小刀 / 手枪另有一套握持姿态）
                Vector3 m = AimMount + MountOffset(weapon) + new Vector3(0f, bob - drop, 0f);
                Vector3 me = MountEuler(weapon);
                if (WeaponMount != null)
                {
                    WeaponMount.localPosition = m;
                    WeaponMount.localRotation = Quaternion.Euler(-pitch + me.x, me.y, me.z);
                }
                Quaternion mq = WeaponMount != null ? WeaponMount.localRotation : Quaternion.identity;

                // 2) 侧身：左臂枢轴前移内转，手才够得到枪；右手保持原位（枪托抵右肩）
                Vector3 sR = ShoulderR;
                Vector3 sL = ShoulderL + BladeShoulderL;
                if (ArmR != null) ArmR.localPosition = sR;
                if (ArmL != null) ArmL.localPosition = sL;

                // 3) 让两条手臂去抓枪：右手扣握把，左手托护木（握把在枪身上，所以偏移要跟着枪转）
                Vector3 grip = m + mq * new Vector3(0.03f, -0.03f, 0f);
                // pole 决定肘往哪边弯：右手肘收在体侧偏下（不要"架鸡翅"），左手肘横过胸前偏下
                SolveArm(ArmR, ForeR, sR, grip, new Vector3(0.72f, -0.70f, -0.35f));
                if (TwoHanded(weapon))
                {
                    Vector3 support = m + mq * new Vector3(-0.02f, -0.02f, SupportOffset(weapon));
                    SolveArm(ArmL, ForeL, sL, support, new Vector3(-0.55f, -0.85f, -0.15f));
                }
                else
                {
                    // 单手武器（手枪 / 小刀）：左手收在身侧做戒备，不去抓枪
                    Vector3 relax = sL + new Vector3(-0.10f, -0.50f, -0.02f) + new Vector3(0f, -drop, 0f);
                    SolveArm(ArmL, ForeL, sL, relax, new Vector3(-0.5f, -0.6f, -0.35f));
                }
            }
            else
            {
                // 没有在持枪：手臂自然下垂摆动（枢轴复位）
                if (ArmR != null) ArmR.localPosition = ShoulderR;
                if (ArmL != null) ArmL.localPosition = ShoulderL;
                if (ForeR != null) ForeR.localRotation = Quaternion.identity;
                if (ForeL != null) ForeL.localRotation = Quaternion.identity;
                ArmR.localRotation = Quaternion.Euler(-swing * 0.7f, 0f, 0f);
                ArmL.localRotation = Quaternion.Euler(swing * 0.7f, 0f, 0f);
            }

            float legSwing = crouch ? 0f : swing;
            LegL.localRotation = Quaternion.Euler(legSwing + (crouch ? 42f : 0f), 0f, crouch ? -12f : 0f);
            LegR.localRotation = Quaternion.Euler(-legSwing + (crouch ? 42f : 0f), 0f, crouch ? 12f : 0f);

            Body.localPosition = new Vector3(0f, 1.05f + bob - drop, 0f);
            Head.localPosition = new Vector3(0f, 1.59f + bob - drop * 1.15f, 0f);
        }

        /// <summary>
        /// 两骨 IK：把手臂摆成"手落在 target 上、肘朝 pole 方向弯"的姿势。
        /// 所有坐标都是角色局部空间。够不着时手臂伸直（朝目标方向），不会出现反关节。
        /// </summary>
        private static void SolveArm(Transform upper, Transform fore, Vector3 shoulder, Vector3 target, Vector3 pole)
        {
            if (upper == null || fore == null) return;
            Vector3 to = target - shoulder;
            float d = to.magnitude;
            if (d < 0.0001f) return;
            Vector3 dir = to / d;

            float reach = UpperLen + ForeLen - 0.002f;
            float dd = Mathf.Min(d, reach);
            float a = (UpperLen * UpperLen - ForeLen * ForeLen + dd * dd) / (2f * dd);
            float h = Mathf.Sqrt(Mathf.Max(0f, UpperLen * UpperLen - a * a));

            Vector3 p = Vector3.ProjectOnPlane(pole, dir);
            if (p.sqrMagnitude < 1e-6f) p = Vector3.ProjectOnPlane(Vector3.down, dir);
            p.Normalize();

            Vector3 elbow = shoulder + dir * a + p * h;
            Quaternion upQ = Quaternion.FromToRotation(Vector3.down, (elbow - shoulder).normalized);
            upper.localRotation = upQ;

            Vector3 foreDir = (shoulder + dir * dd) - elbow;
            if (foreDir.sqrMagnitude < 1e-6f) foreDir = dir;
            // 前臂盒子的横截面是正方形，绕自身长轴的自转看不出来，所以直接 FromToRotation 即可
            fore.localRotation = Quaternion.Inverse(upQ) * Quaternion.FromToRotation(Vector3.down, foreDir.normalized);
        }

        public bool Crouched => crouched;

        public void SetVisible(bool v)
        {
            visible = v;
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = v;
        }

        public void FaceDirection(Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>
        /// 限速转身：敌人不能"瞬间"把身体转过来（degPerSec 越小越迟钝）。
        /// 转身速度会直接决定视野锥扫到的快慢，所以顺带让索敌也变得合理。
        /// </summary>
        public void FaceTowards(Vector3 dir, float degPerSec, float dt)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            float want = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float cur = transform.localEulerAngles.y;
            float diff = Mathf.DeltaAngle(cur, want);
            float step = Mathf.Clamp(diff, -degPerSec * dt, degPerSec * dt);
            transform.localRotation = Quaternion.Euler(0f, cur + step, 0f);
        }

        /// <summary>手上那把枪的枪口世界坐标（第三人称弹道起点）。</summary>
        public Vector3 MuzzlePosition
        {
            get
            {
                if (Muzzle != null) return Muzzle.position;
                return transform.position + Vector3.up * 1.35f + transform.forward * 0.5f;
            }
        }
    }
}
