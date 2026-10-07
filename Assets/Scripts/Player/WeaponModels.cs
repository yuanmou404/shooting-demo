using UnityEngine;

namespace PixelArena
{
    /// <summary>武器种类。玩家五把（步枪/霰弹/狙击/手枪/小刀）+ NPC 两种 + 召唤临时武器两种。</summary>
    public enum WeaponKind { Rifle, Shotgun, Sniper, Pistol, Knife, SMG, LMG, Flamethrower, Minigun }

    /// <summary>
    /// 武器的方块建模（纯代码生成）。
    /// 约定：枪身长轴 = 本地 +Z（枪口在更大的 z 上），-Y 为下方（弹匣），-Z 为枪托方向。
    /// 视图模型（第一人称）比世界模型（第三人称 / NPC）大一号、细节更多。
    /// </summary>
    public static class WeaponModels
    {
        private static readonly Color MetalDark = new Color(0.13f, 0.14f, 0.16f);
        private static readonly Color Metal = new Color(0.32f, 0.34f, 0.38f);
        private static readonly Color Wood = new Color(0.44f, 0.29f, 0.15f);
        private static readonly Color Glass = new Color(0.45f, 0.85f, 0.95f);
        private static readonly Color Gold = new Color(0.85f, 0.72f, 0.30f);
        private static readonly Color DarkGrip = new Color(0.10f, 0.11f, 0.13f);
        private static readonly Color BladeColor = new Color(0.78f, 0.82f, 0.88f);

        /// <summary>各武器枪口（/刀尖）在世界模型本地坐标里的 z，用于第三人称弹道起点。</summary>
        public static float WorldMuzzleZ(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.Rifle: return 0.58f;
                case WeaponKind.Shotgun: return 0.50f;
                case WeaponKind.Sniper: return 0.62f;
                case WeaponKind.Pistol: return 0.30f;
                case WeaponKind.Knife: return 0.30f;
                case WeaponKind.SMG: return 0.34f;
                case WeaponKind.Flamethrower: return 0.62f;
                case WeaponKind.Minigun: return 0.68f;
                default: return 0.56f;   // LMG
            }
        }

        /// <summary>
        /// 第一人称视图模型：root 下挂 "Gun" 子节点（动画作用于 Gun），
        /// 双手位于 Gun 之下 —— 这样换弹 / 切枪 / 检视时手会跟着枪一起运动。
        /// </summary>
        public static GameObject BuildViewModel(WeaponKind kind, WeaponDef def, Transform parent,
            out Transform gunRoot, out Transform muzzle, out Transform mag,
            out Transform armRight, out Transform armLeft)
        {
            var root = new GameObject("VM_" + kind);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            var gun = new GameObject("Gun");
            gun.transform.SetParent(root.transform, false);
            gun.transform.localPosition = Vector3.zero;
            gun.transform.localRotation = Quaternion.identity;
            gunRoot = gun.transform;

            Color body = def.BodyColor;
            Color accent = def.AccentColor;
            Transform magT = null;

            switch (kind)
            {
                case WeaponKind.Rifle: magT = BuildRifle(gun.transform, body, accent); break;
                case WeaponKind.Shotgun: magT = BuildShotgun(gun.transform, body, accent); break;
                case WeaponKind.Pistol: magT = BuildPistol(gun.transform, body, accent); break;
                case WeaponKind.Knife: magT = null; BuildKnife(gun.transform, body, accent); break;
                case WeaponKind.Flamethrower: magT = BuildFlamethrower(gun.transform, body, accent); break;
                case WeaponKind.Minigun: magT = BuildMinigun(gun.transform, body, accent); break;
                default: magT = BuildSniper(gun.transform, body, accent); break;
            }

            mag = magT;

            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(gun.transform, false);
            muzzle.localPosition = new Vector3(0f, 0f, def.MuzzleZ);
            muzzle.localRotation = Quaternion.identity;

            BuildArms(gun.transform, kind, out armRight, out armLeft);
            return root;
        }

        // ------------------------------------------------------------------ 手臂

        /// <summary>
        /// 双手（方格手臂）。挂在 Gun 节点下面而不是武器根节点下面：
        /// 换弹时枪会下沉侧转，如果手不在同一节点下就会出现"枪动了手没动"。
        /// </summary>
        private static void BuildArms(Transform gun, WeaponKind kind, out Transform armR, out Transform armL)
        {
            Color skin = new Color(0.86f, 0.70f, 0.54f);
            // 右手握住握把
            armR = VoxelAssets.MakeBox("ArmR", gun, new Vector3(0.10f, -0.17f, 0.15f), new Vector3(0.15f, 0.15f, 0.40f), skin).transform;
            // 左手扶的位置随枪械而变
            if (kind == WeaponKind.Shotgun)
                armL = VoxelAssets.MakeBox("ArmL", gun, new Vector3(-0.11f, -0.14f, 0.56f), new Vector3(0.15f, 0.15f, 0.30f), skin).transform;
            else if (kind == WeaponKind.Sniper)
                armL = VoxelAssets.MakeBox("ArmL", gun, new Vector3(-0.13f, -0.13f, 0.66f), new Vector3(0.15f, 0.15f, 0.30f), skin).transform;
            else if (kind == WeaponKind.Pistol)
                // 单手握枪：左手在下方做支撑/戒备姿态
                armL = VoxelAssets.MakeBox("ArmL", gun, new Vector3(-0.16f, -0.26f, 0.24f), new Vector3(0.15f, 0.15f, 0.30f), skin).transform;
            else if (kind == WeaponKind.Knife)
                // 匕首是单手武器：第一人称只画握刀的右手，左手不出现（否则看着像双手握持）
                armL = null;
            else if (kind == WeaponKind.Flamethrower)
                // 喷火器：左手扶下面那根输料管
                armL = VoxelAssets.MakeBox("ArmL", gun, new Vector3(-0.13f, -0.15f, 0.48f), new Vector3(0.15f, 0.15f, 0.30f), skin).transform;
            else if (kind == WeaponKind.Minigun)
                // 加特林：左手托枪管座
                armL = VoxelAssets.MakeBox("ArmL", gun, new Vector3(-0.14f, -0.13f, 0.60f), new Vector3(0.15f, 0.15f, 0.30f), skin).transform;
            else
                armL = VoxelAssets.MakeBox("ArmL", gun, new Vector3(-0.14f, -0.12f, 0.50f), new Vector3(0.15f, 0.15f, 0.32f), skin).transform;
        }

        // ------------------------------------------------------------------ 步枪

        private static Transform BuildRifle(Transform g, Color body, Color accent)
        {
            VoxelAssets.MakeBox("Receiver", g, new Vector3(0f, -0.05f, 0.40f), new Vector3(0.11f, 0.13f, 0.50f), body);
            VoxelAssets.MakeBox("Rail", g, new Vector3(0f, 0.045f, 0.36f), new Vector3(0.05f, 0.035f, 0.30f), MetalDark);
            VoxelAssets.MakeBox("Optic", g, new Vector3(0f, 0.085f, 0.46f), new Vector3(0.055f, 0.06f, 0.12f), MetalDark);
            VoxelAssets.MakeBox("Lens", g, new Vector3(0f, 0.085f, 0.525f), new Vector3(0.05f, 0.05f, 0.015f), Glass);
            VoxelAssets.MakeBox("Handguard", g, new Vector3(0f, -0.035f, 0.70f), new Vector3(0.095f, 0.10f, 0.22f), accent);
            VoxelAssets.MakeBox("Barrel", g, new Vector3(0f, -0.02f, 0.86f), new Vector3(0.05f, 0.05f, 0.22f), Metal);
            VoxelAssets.MakeBox("FlashHider", g, new Vector3(0f, -0.02f, 0.98f), new Vector3(0.07f, 0.07f, 0.09f), MetalDark);
            VoxelAssets.MakeBox("FrontSight", g, new Vector3(0f, 0.065f, 0.80f), new Vector3(0.03f, 0.05f, 0.03f), MetalDark);
            VoxelAssets.MakeBox("Grip", g, new Vector3(0f, -0.20f, 0.24f), new Vector3(0.09f, 0.18f, 0.11f), body);
            VoxelAssets.MakeBox("Trigger", g, new Vector3(0f, -0.14f, 0.31f), new Vector3(0.05f, 0.03f, 0.10f), MetalDark);
            VoxelAssets.MakeBox("Stock", g, new Vector3(0f, -0.07f, 0.09f), new Vector3(0.10f, 0.13f, 0.30f), body);
            VoxelAssets.MakeBox("StockPad", g, new Vector3(0f, -0.07f, -0.07f), new Vector3(0.10f, 0.14f, 0.06f), MetalDark);
            var m = VoxelAssets.MakeBox("Mag", g, new Vector3(0f, -0.19f, 0.39f), new Vector3(0.09f, 0.22f, 0.14f), accent);
            VoxelAssets.MakeBox("MagBase", m.transform, new Vector3(0f, -0.58f, 0f), new Vector3(1.05f, 0.18f, 1.06f), MetalDark);
            return m.transform;
        }

        // ------------------------------------------------------------------ 霰弹枪

        private static Transform BuildShotgun(Transform g, Color body, Color accent)
        {
            VoxelAssets.MakeBox("Receiver", g, new Vector3(0f, -0.05f, 0.34f), new Vector3(0.13f, 0.15f, 0.42f), body);
            VoxelAssets.MakeBox("BarrelR", g, new Vector3(0.045f, -0.01f, 0.66f), new Vector3(0.055f, 0.055f, 0.44f), Metal);
            VoxelAssets.MakeBox("BarrelL", g, new Vector3(-0.045f, -0.01f, 0.66f), new Vector3(0.055f, 0.055f, 0.44f), Metal);
            VoxelAssets.MakeBox("Rib", g, new Vector3(0f, 0.035f, 0.66f), new Vector3(0.13f, 0.025f, 0.44f), MetalDark);
            VoxelAssets.MakeBox("Muzzle", g, new Vector3(0f, -0.01f, 0.89f), new Vector3(0.145f, 0.075f, 0.06f), MetalDark);
            VoxelAssets.MakeBox("Bead", g, new Vector3(0f, 0.075f, 0.88f), new Vector3(0.025f, 0.03f, 0.025f), Gold);
            VoxelAssets.MakeBox("Pump", g, new Vector3(0f, -0.10f, 0.60f), new Vector3(0.14f, 0.10f, 0.20f), Wood);
            VoxelAssets.MakeBox("Tube", g, new Vector3(0f, -0.135f, 0.56f), new Vector3(0.10f, 0.05f, 0.30f), MetalDark);
            VoxelAssets.MakeBox("Grip", g, new Vector3(0f, -0.19f, 0.19f), new Vector3(0.095f, 0.17f, 0.12f), Wood);
            VoxelAssets.MakeBox("Stock", g, new Vector3(0f, -0.09f, 0.07f), new Vector3(0.115f, 0.17f, 0.30f), Wood);
            VoxelAssets.MakeBox("StockPad", g, new Vector3(0f, -0.09f, -0.09f), new Vector3(0.115f, 0.18f, 0.06f), MetalDark);
            // 霰弹枪没有弹匣，用"管式弹仓"作为抽壳动作的视觉焦点
            var m = VoxelAssets.MakeBox("Mag", g, new Vector3(0f, -0.135f, 0.42f), new Vector3(0.10f, 0.06f, 0.14f), accent);
            return m.transform;
        }

        // ------------------------------------------------------------------ 狙击枪

        private static Transform BuildSniper(Transform g, Color body, Color accent)
        {
            VoxelAssets.MakeBox("Receiver", g, new Vector3(0f, -0.05f, 0.38f), new Vector3(0.10f, 0.12f, 0.54f), body);
            VoxelAssets.MakeBox("Barrel", g, new Vector3(0f, -0.02f, 0.86f), new Vector3(0.05f, 0.05f, 0.48f), Metal);
            VoxelAssets.MakeBox("MuzzleBrake", g, new Vector3(0f, -0.02f, 1.11f), new Vector3(0.075f, 0.075f, 0.11f), MetalDark);
            VoxelAssets.MakeBox("Scope", g, new Vector3(0f, 0.085f, 0.42f), new Vector3(0.07f, 0.07f, 0.34f), MetalDark);
            VoxelAssets.MakeBox("ScopeBell", g, new Vector3(0f, 0.085f, 0.60f), new Vector3(0.09f, 0.09f, 0.08f), MetalDark);
            VoxelAssets.MakeBox("ScopeEye", g, new Vector3(0f, 0.085f, 0.25f), new Vector3(0.085f, 0.085f, 0.08f), MetalDark);
            VoxelAssets.MakeBox("Lens", g, new Vector3(0f, 0.085f, 0.645f), new Vector3(0.07f, 0.07f, 0.015f), Glass);
            VoxelAssets.MakeBox("MountF", g, new Vector3(0f, 0.045f, 0.50f), new Vector3(0.05f, 0.04f, 0.06f), body);
            VoxelAssets.MakeBox("MountR", g, new Vector3(0f, 0.045f, 0.30f), new Vector3(0.05f, 0.04f, 0.06f), body);
            VoxelAssets.MakeBox("Bolt", g, new Vector3(0.075f, -0.03f, 0.30f), new Vector3(0.06f, 0.04f, 0.05f), Metal);
            VoxelAssets.MakeBox("Grip", g, new Vector3(0f, -0.19f, 0.22f), new Vector3(0.09f, 0.17f, 0.11f), body);
            VoxelAssets.MakeBox("Stock", g, new Vector3(0f, -0.07f, 0.05f), new Vector3(0.10f, 0.14f, 0.34f), body);
            VoxelAssets.MakeBox("Cheek", g, new Vector3(0f, 0.025f, 0.10f), new Vector3(0.09f, 0.06f, 0.20f), body);
            VoxelAssets.MakeBox("BipodBar", g, new Vector3(0f, -0.15f, 0.84f), new Vector3(0.07f, 0.03f, 0.03f), MetalDark);
            VoxelAssets.MakeBox("BipodL", g, new Vector3(0.085f, -0.23f, 0.86f), new Vector3(0.03f, 0.14f, 0.03f), MetalDark);
            VoxelAssets.MakeBox("BipodR", g, new Vector3(-0.085f, -0.23f, 0.86f), new Vector3(0.03f, 0.14f, 0.03f), MetalDark);
            var m = VoxelAssets.MakeBox("Mag", g, new Vector3(0f, -0.18f, 0.37f), new Vector3(0.08f, 0.16f, 0.11f), accent);
            return m.transform;
        }

        // ------------------------------------------------------------------ 手枪

        private static Transform BuildPistol(Transform g, Color body, Color accent)
        {
            VoxelAssets.MakeBox("Slide", g, new Vector3(0f, -0.02f, 0.34f), new Vector3(0.085f, 0.11f, 0.40f), body);
            VoxelAssets.MakeBox("SlideGroove", g, new Vector3(0f, 0.04f, 0.30f), new Vector3(0.05f, 0.02f, 0.24f), MetalDark);
            VoxelAssets.MakeBox("Barrel", g, new Vector3(0f, -0.02f, 0.52f), new Vector3(0.05f, 0.05f, 0.10f), Metal);
            VoxelAssets.MakeBox("MuzzleRing", g, new Vector3(0f, -0.02f, 0.575f), new Vector3(0.065f, 0.065f, 0.035f), MetalDark);
            VoxelAssets.MakeBox("FrontSight", g, new Vector3(0f, 0.045f, 0.50f), new Vector3(0.025f, 0.035f, 0.03f), MetalDark);
            VoxelAssets.MakeBox("RearSight", g, new Vector3(0f, 0.045f, 0.16f), new Vector3(0.05f, 0.035f, 0.04f), MetalDark);
            VoxelAssets.MakeBox("Frame", g, new Vector3(0f, -0.08f, 0.28f), new Vector3(0.08f, 0.07f, 0.26f), MetalDark);
            VoxelAssets.MakeBox("TriggerGuard", g, new Vector3(0f, -0.14f, 0.25f), new Vector3(0.05f, 0.03f, 0.12f), MetalDark);
            VoxelAssets.MakeBox("Trigger", g, new Vector3(0f, -0.11f, 0.28f), new Vector3(0.035f, 0.05f, 0.03f), Metal);
            VoxelAssets.MakeBox("Grip", g, new Vector3(0f, -0.20f, 0.19f), new Vector3(0.085f, 0.20f, 0.12f), DarkGrip);
            VoxelAssets.MakeBox("GripCap", g, new Vector3(0f, -0.30f, 0.19f), new Vector3(0.09f, 0.03f, 0.13f), MetalDark);
            VoxelAssets.MakeBox("Hammer", g, new Vector3(0f, 0.03f, 0.13f), new Vector3(0.04f, 0.05f, 0.05f), Metal);
            VoxelAssets.MakeBox("BeaverTail", g, new Vector3(0f, -0.06f, 0.10f), new Vector3(0.07f, 0.09f, 0.06f), body);
            var m = VoxelAssets.MakeBox("Mag", g, new Vector3(0f, -0.25f, 0.21f), new Vector3(0.075f, 0.15f, 0.10f), accent);
            return m.transform;
        }

        // ------------------------------------------------------------------ 小刀（近战）

        private static Transform BuildKnife(Transform g, Color body, Color accent)
        {
            VoxelAssets.MakeBox("Handle", g, new Vector3(0f, -0.12f, 0.20f), new Vector3(0.065f, 0.065f, 0.18f), DarkGrip);
            VoxelAssets.MakeBox("Pommel", g, new Vector3(0f, -0.12f, 0.10f), new Vector3(0.08f, 0.08f, 0.035f), MetalDark);
            VoxelAssets.MakeBox("Guard", g, new Vector3(0f, -0.12f, 0.30f), new Vector3(0.17f, 0.045f, 0.05f), Metal);
            VoxelAssets.MakeBox("Blade", g, new Vector3(0f, -0.10f, 0.50f), new Vector3(0.05f, 0.13f, 0.36f), BladeColor);
            VoxelAssets.MakeBox("Spine", g, new Vector3(0f, -0.035f, 0.50f), new Vector3(0.055f, 0.025f, 0.36f), Metal);
            VoxelAssets.MakeBox("Edge", g, new Vector3(0f, -0.155f, 0.50f), new Vector3(0.03f, 0.03f, 0.36f), new Color(0.92f, 0.95f, 1.0f));
            VoxelAssets.MakeBox("Tip", g, new Vector3(0f, -0.075f, 0.70f), new Vector3(0.04f, 0.06f, 0.10f), new Color(0.92f, 0.95f, 1.0f));
            VoxelAssets.MakeBox("Fuller", g, new Vector3(0.028f, -0.09f, 0.50f), new Vector3(0.012f, 0.06f, 0.30f), MetalDark);
            return null;
        }

        // ------------------------------------------------------------------ 喷火器（召唤武器）

        private static Transform BuildFlamethrower(Transform g, Color body, Color accent)
        {
            VoxelAssets.MakeBox("Receiver", g, new Vector3(0f, -0.05f, 0.34f), new Vector3(0.12f, 0.14f, 0.42f), body);
            VoxelAssets.MakeBox("Barrel", g, new Vector3(0f, -0.02f, 0.72f), new Vector3(0.06f, 0.06f, 0.38f), Metal);
            VoxelAssets.MakeBox("Nozzle", g, new Vector3(0f, -0.02f, 0.93f), new Vector3(0.09f, 0.09f, 0.12f), MetalDark);
            // 长明火（枪口上那粒小橙块，一眼看出这是喷火器）
            VoxelAssets.MakeBox("Pilot", g, new Vector3(0f, 0.05f, 0.98f), new Vector3(0.035f, 0.035f, 0.035f), new Color(1f, 0.55f, 0.15f));
            // 输料管（从前握把弯回燃料罐）
            VoxelAssets.MakeBox("Hose", g, new Vector3(-0.10f, -0.16f, 0.46f), new Vector3(0.05f, 0.05f, 0.34f), DarkGrip);
            VoxelAssets.MakeBox("Grip", g, new Vector3(0f, -0.20f, 0.20f), new Vector3(0.09f, 0.16f, 0.10f), body);
            VoxelAssets.MakeBox("Stock", g, new Vector3(0f, -0.08f, 0.02f), new Vector3(0.10f, 0.12f, 0.24f), body);
            // 燃料罐挂在枪身下（换弹动作的视觉焦点）
            var m = VoxelAssets.MakeBox("FuelTank", g, new Vector3(0f, -0.24f, 0.32f), new Vector3(0.16f, 0.20f, 0.30f), accent);
            VoxelAssets.MakeBox("TankCap", m.transform, new Vector3(0f, 0.62f, 0f), new Vector3(0.5f, 0.16f, 0.9f), MetalDark);
            return m.transform;
        }

        // ------------------------------------------------------------------ 加特林（召唤武器）

        private static Transform BuildMinigun(Transform g, Color body, Color accent)
        {
            VoxelAssets.MakeBox("Receiver", g, new Vector3(0f, -0.04f, 0.34f), new Vector3(0.14f, 0.16f, 0.44f), body);
            VoxelAssets.MakeBox("BarrelHub", g, new Vector3(0f, -0.02f, 0.62f), new Vector3(0.17f, 0.17f, 0.14f), MetalDark);
            // 六根枪管围成一圈
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                VoxelAssets.MakeBox("Brl" + i, g,
                    new Vector3(Mathf.Cos(a) * 0.055f, -0.02f + Mathf.Sin(a) * 0.055f, 0.86f),
                    new Vector3(0.032f, 0.032f, 0.46f), Metal);
            }
            VoxelAssets.MakeBox("MuzzleRing", g, new Vector3(0f, -0.02f, 1.08f), new Vector3(0.19f, 0.19f, 0.06f), MetalDark);
            VoxelAssets.MakeBox("HandleTop", g, new Vector3(0f, 0.09f, 0.42f), new Vector3(0.07f, 0.05f, 0.22f), DarkGrip);
            VoxelAssets.MakeBox("Grip", g, new Vector3(0f, -0.20f, 0.20f), new Vector3(0.09f, 0.16f, 0.10f), body);
            VoxelAssets.MakeBox("Stock", g, new Vector3(0f, -0.08f, 0.04f), new Vector3(0.11f, 0.13f, 0.26f), body);
            // 弹药箱（"弹匣"）
            var m = VoxelAssets.MakeBox("AmmoBox", g, new Vector3(0f, -0.24f, 0.30f), new Vector3(0.18f, 0.18f, 0.26f), accent);
            VoxelAssets.MakeBox("Belt", m.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.4f, 0.14f, 0.9f), accent);
            return m.transform;
        }

        // ------------------------------------------------------------------ 世界模型（第三人称 / NPC）

        /// <summary>
        /// 挂在角色身上的小一号枪械模型，不同种类轮廓明显不同。
        /// 挂在角色的 WeaponMount 上，该挂点就在"握把 + 枪托抵肩"的位置，
        /// 因此模型保持 +Z 朝前、不加额外旋转（手臂由 BlockCharacter 用 IK 抓上去）。
        /// </summary>
        public static GameObject BuildWorldModel(WeaponKind kind, Transform parent)
        {
            var root = new GameObject("Gun_" + kind);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            switch (kind)
            {
                case WeaponKind.Rifle: WorldRifle(root.transform); break;
                case WeaponKind.Shotgun: WorldShotgun(root.transform); break;
                case WeaponKind.Sniper: WorldSniper(root.transform); break;
                case WeaponKind.Pistol: WorldPistol(root.transform); break;
                case WeaponKind.Knife: WorldKnife(root.transform); break;
                case WeaponKind.SMG: WorldSmg(root.transform); break;
                case WeaponKind.Flamethrower: WorldFlame(root.transform); break;
                case WeaponKind.Minigun: WorldMinigun(root.transform); break;
                default: WorldLmg(root.transform); break;
            }

            // 枪口（/刀尖）锚点：第三人称时子弹必须从这里飞出去，而不是从相机里凭空出现
            var mp = new GameObject("MuzzlePoint");
            mp.transform.SetParent(root.transform, false);
            mp.transform.localPosition = new Vector3(0f, 0f, WorldMuzzleZ(kind));
            mp.transform.localRotation = Quaternion.identity;
            return root;
        }

        private static void WorldRifle(Transform g)
        {
            Color body = new Color(0.20f, 0.22f, 0.26f), accent = new Color(0.50f, 0.33f, 0.16f);
            VoxelAssets.MakeBox("Receiver", g, new Vector3(0f, 0f, 0.08f), new Vector3(0.10f, 0.11f, 0.40f), body);
            VoxelAssets.MakeBox("Handguard", g, new Vector3(0f, 0f, 0.32f), new Vector3(0.09f, 0.09f, 0.18f), accent);
            VoxelAssets.MakeBox("Barrel", g, new Vector3(0f, 0f, 0.45f), new Vector3(0.05f, 0.05f, 0.16f), Metal);
            VoxelAssets.MakeBox("Muzzle", g, new Vector3(0f, 0f, 0.54f), new Vector3(0.07f, 0.07f, 0.05f), MetalDark);
            VoxelAssets.MakeBox("Sight", g, new Vector3(0f, 0.075f, 0.30f), new Vector3(0.04f, 0.05f, 0.08f), MetalDark);
            VoxelAssets.MakeBox("Mag", g, new Vector3(0f, -0.11f, 0.06f), new Vector3(0.08f, 0.18f, 0.10f), accent);
            VoxelAssets.MakeBox("Grip", g, new Vector3(0f, -0.11f, -0.06f), new Vector3(0.08f, 0.15f, 0.09f), body);
            VoxelAssets.MakeBox("Stock", g, new Vector3(0f, -0.01f, -0.15f), new Vector3(0.09f, 0.11f, 0.24f), body);
        }

        private static void WorldShotgun(Transform g)
        {
            Color body = new Color(0.30f, 0.22f, 0.14f), metal = Metal;
            VoxelAssets.MakeBox("Receiver", g, new Vector3(0f, 0f, 0.06f), new Vector3(0.12f, 0.13f, 0.34f), body);
            VoxelAssets.MakeBox("BarrelR", g, new Vector3(0.04f, 0.01f, 0.28f), new Vector3(0.05f, 0.05f, 0.34f), metal);
            VoxelAssets.MakeBox("BarrelL", g, new Vector3(-0.04f, 0.01f, 0.28f), new Vector3(0.05f, 0.05f, 0.34f), metal);
            VoxelAssets.MakeBox("Muzzle", g, new Vector3(0f, 0.01f, 0.46f), new Vector3(0.13f, 0.07f, 0.05f), MetalDark);
            VoxelAssets.MakeBox("Pump", g, new Vector3(0f, -0.09f, 0.24f), new Vector3(0.13f, 0.09f, 0.16f), Wood);
            VoxelAssets.MakeBox("Grip", g, new Vector3(0f, -0.11f, -0.04f), new Vector3(0.09f, 0.14f, 0.10f), Wood);
            VoxelAssets.MakeBox("Stock", g, new Vector3(0f, -0.02f, -0.15f), new Vector3(0.11f, 0.15f, 0.24f), Wood);
        }

        private static void WorldSniper(Transform g)
        {
            Color body = new Color(0.16f, 0.20f, 0.16f), accent = new Color(0.35f, 0.40f, 0.30f);
            VoxelAssets.MakeBox("Receiver", g, new Vector3(0f, 0f, 0.06f), new Vector3(0.09f, 0.10f, 0.42f), body);
            VoxelAssets.MakeBox("Barrel", g, new Vector3(0f, 0f, 0.38f), new Vector3(0.045f, 0.045f, 0.36f), Metal);
            VoxelAssets.MakeBox("Muzzle", g, new Vector3(0f, 0f, 0.57f), new Vector3(0.07f, 0.07f, 0.08f), MetalDark);
            VoxelAssets.MakeBox("Scope", g, new Vector3(0f, 0.085f, 0.20f), new Vector3(0.06f, 0.06f, 0.26f), MetalDark);
            VoxelAssets.MakeBox("ScopeBell", g, new Vector3(0f, 0.085f, 0.34f), new Vector3(0.08f, 0.08f, 0.06f), MetalDark);
            VoxelAssets.MakeBox("Bolt", g, new Vector3(0.07f, -0.02f, 0.04f), new Vector3(0.05f, 0.035f, 0.04f), Metal);
            VoxelAssets.MakeBox("Mag", g, new Vector3(0f, -0.10f, 0.04f), new Vector3(0.07f, 0.14f, 0.09f), accent);
            VoxelAssets.MakeBox("Grip", g, new Vector3(0f, -0.11f, -0.06f), new Vector3(0.08f, 0.14f, 0.09f), body);
            VoxelAssets.MakeBox("Stock", g, new Vector3(0f, -0.02f, -0.17f), new Vector3(0.09f, 0.12f, 0.26f), body);
        }

        private static void WorldPistol(Transform g)
        {
            Color body = new Color(0.22f, 0.24f, 0.28f), accent = new Color(0.45f, 0.40f, 0.22f);
            VoxelAssets.MakeBox("Slide", g, new Vector3(0f, 0.01f, 0.08f), new Vector3(0.075f, 0.08f, 0.22f), body);
            VoxelAssets.MakeBox("Barrel", g, new Vector3(0f, 0.01f, 0.21f), new Vector3(0.04f, 0.04f, 0.07f), Metal);
            VoxelAssets.MakeBox("Muzzle", g, new Vector3(0f, 0.01f, 0.25f), new Vector3(0.055f, 0.055f, 0.03f), MetalDark);
            VoxelAssets.MakeBox("Sight", g, new Vector3(0f, 0.065f, 0.14f), new Vector3(0.03f, 0.03f, 0.05f), MetalDark);
            VoxelAssets.MakeBox("Frame", g, new Vector3(0f, -0.03f, 0.06f), new Vector3(0.07f, 0.05f, 0.18f), MetalDark);
            VoxelAssets.MakeBox("Grip", g, new Vector3(0f, -0.11f, -0.01f), new Vector3(0.075f, 0.16f, 0.09f), DarkGrip);
            VoxelAssets.MakeBox("Mag", g, new Vector3(0f, -0.17f, 0.01f), new Vector3(0.06f, 0.09f, 0.07f), accent);
        }

        private static void WorldKnife(Transform g)
        {
            VoxelAssets.MakeBox("Handle", g, new Vector3(0f, -0.02f, 0.02f), new Vector3(0.05f, 0.05f, 0.12f), DarkGrip);
            VoxelAssets.MakeBox("Pommel", g, new Vector3(0f, -0.02f, -0.05f), new Vector3(0.06f, 0.06f, 0.03f), MetalDark);
            VoxelAssets.MakeBox("Guard", g, new Vector3(0f, -0.02f, 0.10f), new Vector3(0.13f, 0.035f, 0.03f), Metal);
            VoxelAssets.MakeBox("Blade", g, new Vector3(0f, -0.01f, 0.22f), new Vector3(0.035f, 0.10f, 0.20f), BladeColor);
            VoxelAssets.MakeBox("Tip", g, new Vector3(0f, 0.0f, 0.33f), new Vector3(0.025f, 0.05f, 0.06f), new Color(0.92f, 0.95f, 1.0f));
        }

        private static void WorldSmg(Transform g)
        {
            Color body = new Color(0.24f, 0.25f, 0.28f), accent = new Color(0.60f, 0.30f, 0.22f);
            VoxelAssets.MakeBox("Receiver", g, new Vector3(0f, 0f, 0.04f), new Vector3(0.10f, 0.12f, 0.26f), body);
            VoxelAssets.MakeBox("Barrel", g, new Vector3(0f, 0f, 0.23f), new Vector3(0.045f, 0.045f, 0.12f), Metal);
            VoxelAssets.MakeBox("Muzzle", g, new Vector3(0f, 0f, 0.30f), new Vector3(0.06f, 0.06f, 0.04f), MetalDark);
            VoxelAssets.MakeBox("Mag", g, new Vector3(0f, -0.12f, 0.0f), new Vector3(0.07f, 0.20f, 0.09f), accent);
            VoxelAssets.MakeBox("Grip", g, new Vector3(0f, -0.10f, -0.06f), new Vector3(0.08f, 0.14f, 0.09f), body);
            VoxelAssets.MakeBox("Stock", g, new Vector3(0f, 0.03f, -0.10f), new Vector3(0.05f, 0.05f, 0.16f), MetalDark);
        }

        private static void WorldLmg(Transform g)
        {
            Color body = new Color(0.26f, 0.27f, 0.30f), accent = new Color(0.52f, 0.44f, 0.20f);
            VoxelAssets.MakeBox("Receiver", g, new Vector3(0f, 0f, 0.08f), new Vector3(0.14f, 0.15f, 0.46f), body);
            VoxelAssets.MakeBox("Barrel", g, new Vector3(0f, 0f, 0.38f), new Vector3(0.06f, 0.06f, 0.24f), Metal);
            VoxelAssets.MakeBox("Muzzle", g, new Vector3(0f, 0f, 0.51f), new Vector3(0.09f, 0.09f, 0.07f), MetalDark);
            VoxelAssets.MakeBox("BoxMag", g, new Vector3(0f, -0.13f, 0.06f), new Vector3(0.14f, 0.18f, 0.20f), accent);
            VoxelAssets.MakeBox("Belt", g, new Vector3(0.10f, -0.05f, 0.14f), new Vector3(0.05f, 0.07f, 0.16f), accent);
            VoxelAssets.MakeBox("Bipod", g, new Vector3(0f, -0.14f, 0.34f), new Vector3(0.10f, 0.04f, 0.04f), MetalDark);
            VoxelAssets.MakeBox("Grip", g, new Vector3(0f, -0.13f, -0.08f), new Vector3(0.09f, 0.16f, 0.10f), body);
            VoxelAssets.MakeBox("Stock", g, new Vector3(0f, -0.01f, -0.19f), new Vector3(0.11f, 0.13f, 0.26f), body);
        }

        private static void WorldFlame(Transform g)
        {
            Color body = new Color(0.24f, 0.26f, 0.24f), accent = new Color(0.65f, 0.40f, 0.16f);
            VoxelAssets.MakeBox("Receiver", g, new Vector3(0f, 0f, 0.06f), new Vector3(0.11f, 0.12f, 0.32f), body);
            VoxelAssets.MakeBox("Barrel", g, new Vector3(0f, 0f, 0.34f), new Vector3(0.05f, 0.05f, 0.24f), Metal);
            VoxelAssets.MakeBox("Nozzle", g, new Vector3(0f, 0f, 0.50f), new Vector3(0.08f, 0.08f, 0.07f), MetalDark);
            VoxelAssets.MakeBox("Pilot", g, new Vector3(0f, 0.06f, 0.53f), new Vector3(0.03f, 0.03f, 0.03f), new Color(1f, 0.55f, 0.15f));
            VoxelAssets.MakeBox("FuelTank", g, new Vector3(0f, -0.14f, 0.08f), new Vector3(0.14f, 0.16f, 0.26f), accent);
            VoxelAssets.MakeBox("Grip", g, new Vector3(0f, -0.11f, -0.06f), new Vector3(0.08f, 0.14f, 0.09f), body);
            VoxelAssets.MakeBox("Stock", g, new Vector3(0f, 0f, -0.16f), new Vector3(0.09f, 0.11f, 0.22f), body);
        }

        private static void WorldMinigun(Transform g)
        {
            Color body = new Color(0.22f, 0.24f, 0.27f), accent = new Color(0.55f, 0.48f, 0.22f);
            VoxelAssets.MakeBox("Receiver", g, new Vector3(0f, 0f, 0.06f), new Vector3(0.14f, 0.15f, 0.36f), body);
            VoxelAssets.MakeBox("BarrelHub", g, new Vector3(0f, 0f, 0.28f), new Vector3(0.15f, 0.15f, 0.10f), MetalDark);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                VoxelAssets.MakeBox("Brl" + i, g,
                    new Vector3(Mathf.Cos(a) * 0.05f, Mathf.Sin(a) * 0.05f, 0.46f),
                    new Vector3(0.028f, 0.028f, 0.30f), Metal);
            }
            VoxelAssets.MakeBox("MuzzleRing", g, new Vector3(0f, 0f, 0.62f), new Vector3(0.17f, 0.17f, 0.05f), MetalDark);
            VoxelAssets.MakeBox("AmmoBox", g, new Vector3(0f, -0.14f, 0.02f), new Vector3(0.15f, 0.16f, 0.20f), accent);
            VoxelAssets.MakeBox("Grip", g, new Vector3(0f, -0.12f, -0.08f), new Vector3(0.09f, 0.15f, 0.09f), body);
            VoxelAssets.MakeBox("Stock", g, new Vector3(0f, 0f, -0.18f), new Vector3(0.10f, 0.12f, 0.24f), body);
        }
    }
}
