using UnityEngine;

namespace PixelArena
{
    public enum ViewMode { FirstPerson = 0, ThirdPerson = 1 }

    /// <summary>
    /// 相机与视角：支持第一人称 / 第三人称切换，含后坐力、震动、头部摆动、第三人称碰撞回退。
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Camera Cam;
        public PlayerController Player;
        public VoxelWorld World;
        public BlockCharacter ThirdPersonModel;
        public Transform WeaponHolder;

        public float Yaw;
        public float Pitch;
        public ViewMode Mode = ViewMode.FirstPerson;

        /// <summary>非空时第三人称镜头改为跟随它（登乘坦克时指向车体）。</summary>
        public Transform FollowOverride;
        /// <summary>坦克舱内第一人称的眼位（挂在炮塔上）。非空且 TankFirstPerson 时生效。</summary>
        public Transform TankEye;
        /// <summary>是否处于坦克舱内视角（这时相机跟炮塔舱口，而不是主角脑袋）。</summary>
        public bool TankFirstPerson;

        [Header("第三人称")]
        public float TpDistance = 4.4f;
        public float TpHeight = 1.9f;
        public float TpShoulder = 0.65f;

        private float recoilPitch, recoilYaw;
        private float shake;
        private float bobPhase;
        private float baseFov = 75f;
        private float fovBoost;
        private Vector3 smoothEye;
        private bool smoothEyeInit;
        private bool cinematic;
        private bool avatarVisible = true;
        private float cineTime;
        private float adsZoom;      // 开镜时缩小的 FOV 量（由 WeaponSystem 推动）

        /// <summary>主菜单的电影机位（在真实场景里缓慢环绕主角，作为菜单背景）。</summary>
        public bool IsCinematic => cinematic;

        /// <summary>换地图后调用：让机位直接"贴"到新出生点，不要从上一张图的位置滑过去。</summary>
        public void SnapCamera()
        {
            smoothEyeInit = false;
        }

        public void SetCinematic(bool on)
        {
            cinematic = on;
            // 进出主菜单都要重置相机平滑，否则会从机位"滑"到玩家眼睛
            smoothEyeInit = false;
            adsZoom = 0f;
            if (on) cineTime = 0f;
            // 菜单里不显示第一人称的枪，改看第三人称主角站在场景里
            if (WeaponHolder != null) WeaponHolder.gameObject.SetActive(!on && Mode == ViewMode.FirstPerson);
            if (ThirdPersonModel != null) ThirdPersonModel.SetVisible(on || Mode == ViewMode.ThirdPerson);
        }

        public void Init(Camera cam, PlayerController player, VoxelWorld world)
        {
            Cam = cam;
            Player = player;
            World = world;
            baseFov = cam.fieldOfView;
        }

        public void SetViewMode(ViewMode m)
        {
            Mode = m;
            if (ThirdPersonModel != null) ThirdPersonModel.SetVisible(avatarVisible && m == ViewMode.ThirdPerson);
            if (WeaponHolder != null) WeaponHolder.gameObject.SetActive(m == ViewMode.FirstPerson);
        }

        /// <summary>登乘坦克时把第三人称主角藏起来（否则会看到一个小人杵在炮塔上）。</summary>
        public void SetAvatarVisible(bool v)
        {
            avatarVisible = v;
            if (ThirdPersonModel != null) ThirdPersonModel.SetVisible(v && Mode == ViewMode.ThirdPerson);
        }

        public void ToggleView()
        {
            SetViewMode(Mode == ViewMode.FirstPerson ? ViewMode.ThirdPerson : ViewMode.FirstPerson);
        }

        public void AddLook(Vector2 delta)
        {
            Yaw += delta.x;
            Pitch -= delta.y;
            Pitch = Mathf.Clamp(Pitch, -88f, 88f);
            if (Yaw > 360f) Yaw -= 360f;
            if (Yaw < -360f) Yaw += 360f;
        }

        public void AddRecoil(float pitchKick, float yawKick)
        {
            Pitch = Mathf.Clamp(Pitch + pitchKick, -88f, 88f);
            Yaw += yawKick;
            recoilPitch += pitchKick * 0.9f;
            recoilYaw += yawKick;
        }

        public void Shake(float amount)
        {
            shake = Mathf.Min(shake + amount, 0.6f);
        }

        public Ray AimRay()
        {
            Vector3 origin = Cam.transform.position;
            Vector3 dir = Cam.transform.forward;
            return new Ray(origin, dir);
        }

        public void LateUpdateCamera(float dt)
        {
            if (Player == null || Cam == null) return;

            if (cinematic)
            {
                UpdateCinematic(dt);
                return;
            }

            // 后坐力回复
            recoilPitch = Mathf.MoveTowards(recoilPitch, 0f, dt * 55f);
            recoilYaw = Mathf.MoveTowards(recoilYaw, 0f, dt * 55f);
            shake = Mathf.MoveTowards(shake, 0f, dt * 2.2f);

            float viewPitch = Mathf.Clamp(Pitch - recoilPitch, -89f, 89f);
            float viewYaw = Yaw - recoilYaw;

            // 坦克舱内视角：眼睛在炮塔舱口，视线跟着鼠标（炮塔会追着转）
            if (TankFirstPerson && TankEye != null)
            {
                Cam.transform.position = TankEye.position + Random.insideUnitSphere * shake * 0.08f;
                Cam.transform.rotation = Quaternion.Euler(viewPitch, viewYaw, 0f);
                float tankFov = Mathf.Lerp(Cam.fieldOfView, baseFov + 4f, 1f - Mathf.Pow(0.001f, dt));
                Cam.fieldOfView = tankFov;
                return;
            }

            if (Mode == ViewMode.FirstPerson)
            {
                Vector3 eye = Player.EyePosition;
                if (!smoothEyeInit) { smoothEye = eye; smoothEyeInit = true; }
                smoothEye.y = Mathf.Lerp(smoothEye.y, eye.y, 1f - Mathf.Pow(0.0001f, dt)); // 台阶平滑
                smoothEye.x = eye.x;
                smoothEye.z = eye.z;

                // 头部摆动
                bobPhase += dt * Player.MoveAmount * 11f;
                float bobX = Mathf.Cos(bobPhase * 0.5f) * 0.045f * Player.MoveAmount;
                float bobY = Mathf.Abs(Mathf.Sin(bobPhase * 0.5f)) * 0.05f * Player.MoveAmount;

                Vector3 shakeOffset = Random.insideUnitSphere * shake * 0.12f;
                Cam.transform.position = smoothEye + new Vector3(bobX, bobY, 0f) + shakeOffset;
                Cam.transform.rotation = Quaternion.Euler(viewPitch, viewYaw, 0f);
            }
            else
            {
                // 坦克模式：镜头围绕车体；普通第三人称：围绕主角
                bool tankCam = FollowOverride != null;
                Vector3 target = tankCam
                    ? FollowOverride.position + Vector3.up * 1.7f
                    : Player.Center + new Vector3(0f, 0.35f, 0f);
                float tpDist = tankCam ? 7.2f : TpDistance;
                float shoulder = tankCam ? 0f : TpShoulder;
                Quaternion rot = Quaternion.Euler(viewPitch, viewYaw, 0f);
                Vector3 back = rot * Vector3.back;
                Vector3 side = rot * Vector3.right;

                float dist = tpDist;
                Vector3 desired = target + side * shoulder + Vector3.up * TpHeight * 0.35f + back * dist;
                Vector3 toCam = desired - target;
                float len = toCam.magnitude;
                Vector3 dir = toCam / len;
                Vector3Int cell; Vector3Int n; Vector3 p;
                if (World != null && World.RaycastBlocks(target, dir, len, out p, out cell, out n))
                {
                    dist = Mathf.Max(0.6f, Vector3.Distance(target, p) - 0.35f);
                }
                desired = target + side * shoulder + Vector3.up * TpHeight * 0.35f + dir * dist;

                smoothEye = Vector3.Lerp(smoothEyeInit ? smoothEye : desired, desired, 1f - Mathf.Pow(0.0005f, dt));
                smoothEyeInit = true;
                Cam.transform.position = smoothEye + Random.insideUnitSphere * shake * 0.12f;
                Cam.transform.rotation = rot;

                // 坦克模式不摆主角模型（人坐在车里）
                if (!tankCam && ThirdPersonModel != null)
                {
                    ThirdPersonModel.transform.position = Player.transform.position;
                    ThirdPersonModel.FaceDirection(new Vector3(Mathf.Sin(viewYaw * Mathf.Deg2Rad), 0f, Mathf.Cos(viewYaw * Mathf.Deg2Rad)));
                    // 主角始终持枪待命：手臂随俯仰抬落，蹲下/滑铲时压低身体（NPC 则由自己的 AI 驱动）
                    // 注意：这里 viewPitch 抬头为负（-pitch 才是"仰角"），
                    // 之前忘了转换符号，于是抬头时枪口反而朝下。
                    ThirdPersonModel.Animate(Player.MoveAmount, dt, true, -viewPitch, Player.IsCrouching || Player.IsSliding);
                }
            }

            // 冲刺 / 滑铲 FOV
            float maxZoom = Mathf.Min(adsZoom, baseFov - 12f);   // 别缩到 12° 以下，否则画面糊成一团
            float targetFov = baseFov + ((Player.IsSprinting || Player.IsSliding) ? 7f : 0f) + fovBoost - maxZoom;
            fovBoost = Mathf.MoveTowards(fovBoost, 0f, dt * 30f);
            Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, targetFov, 1f - Mathf.Pow(0.001f, dt));

            // 武器视图模型摆动
            if (WeaponHolder != null && WeaponHolder.gameObject.activeSelf)
            {
                bobPhase += dt * Player.MoveAmount * 2f;
                float wx = Mathf.Cos(bobPhase) * 0.02f * Player.MoveAmount;
                float wy = Mathf.Sin(bobPhase * 2f) * 0.015f * Player.MoveAmount;
                WeaponHolder.localPosition = new Vector3(0.30f + wx, -0.26f + wy, 0.52f);
                WeaponHolder.localRotation = Quaternion.Euler(-recoilPitch * 3.2f, recoilYaw * 2f, recoilPitch * 2.2f);
            }
        }

        /// <summary>
        /// 主菜单背景：围绕站在场景里的主角缓慢环绕的实机镜头（会对体素做遮挡收缩，不会穿墙）。
        /// </summary>
        private void UpdateCinematic(float dt)
        {
            cineTime += dt;
            float t = cineTime;
            Vector3 focus = Player.transform.position + new Vector3(0f, 1.35f, 0f);

            float ang = t * 0.16f;                                  // 约 40 秒一圈
            float radius = 6.2f + Mathf.Sin(t * 0.13f) * 1.2f;
            float height = 2.25f + Mathf.Sin(t * 0.09f) * 0.8f;
            Vector3 desired = focus + new Vector3(Mathf.Cos(ang) * radius, height, Mathf.Sin(ang) * radius);

            // 别让镜头穿进方块
            Vector3 toCam = desired - focus;
            float len = toCam.magnitude;
            Vector3 dirCam = toCam / Mathf.Max(0.0001f, len);
            if (World != null)
            {
                Vector3Int cell, n; Vector3 p;
                if (World.RaycastBlocks(focus, dirCam, len, out p, out cell, out n))
                {
                    float d = Mathf.Max(1.7f, Vector3.Distance(focus, p) - 0.45f);
                    desired = focus + dirCam * d;
                }
            }

            if (!smoothEyeInit) { smoothEye = desired; smoothEyeInit = true; }
            smoothEye = Vector3.Lerp(smoothEye, desired, 1f - Mathf.Pow(0.05f, dt));
            Cam.transform.position = smoothEye;

            Vector3 look = focus + new Vector3(Mathf.Sin(t * 0.21f) * 0.8f,
                0.25f + Mathf.Sin(t * 0.11f) * 0.45f, Mathf.Cos(t * 0.21f) * 0.8f);
            Cam.transform.rotation = Quaternion.LookRotation((look - Cam.transform.position).normalized, Vector3.up);
            Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, 62f, 1f - Mathf.Pow(0.01f, dt));

            // 主角站在场景里待机，面朝地图中央的大树
            if (ThirdPersonModel != null)
            {
                ThirdPersonModel.transform.position = Player.transform.position;
                if (World != null)
                {
                    Vector3 to = new Vector3(World.SX * 0.5f - Player.transform.position.x, 0f,
                                             World.SZ * 0.5f - Player.transform.position.z);
                    if (to.sqrMagnitude < 0.01f) to = Vector3.forward;
                    ThirdPersonModel.FaceDirection(to.normalized);
                }
                ThirdPersonModel.Animate(0f, dt, true, 0f, false);
            }
        }

        public void KickFov(float amount)
        {
            fovBoost += amount;
        }

        /// <summary>开镜变焦：amount 是要缩小的 FOV 度数（0 = 腰射）。</summary>
        public void SetAdsZoom(float amount)
        {
            adsZoom = amount;
        }
    }
}
