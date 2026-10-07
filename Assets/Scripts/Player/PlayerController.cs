using UnityEngine;

namespace PixelArena
{
    /// <summary>
    /// 玩家移动：完全基于体素数据的 AABB 碰撞，不使用 Rigidbody/CharacterController。
    /// transform.position 表示脚底中心。
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public VoxelWorld World;

        [Header("体型")]
        public float Radius = 0.32f;
        public float StandHeight = 1.78f;
        public float CrouchHeight = 1.15f;

        [Header("移动")]
        public float WalkSpeed = 5.4f;
        public float SprintSpeed = 8.3f;
        public float CrouchSpeed = 2.6f;
        public float GroundAccel = 70f;
        public float AirAccel = 22f;
        public float JumpSpeed = 8.6f;
        public float Gravity = -26f;
        public float MaxFallSpeed = -40f;

        [Header("滑铲")]
        public float SlideHeight = 0.95f;    // 滑铲时的碰撞高度
        public float SlideBoost = 1.42f;     // 起滑初速倍率
        public float SlideFriction = 6.2f;   // 滑铲减速（越小滑得越远）
        public float SlideMaxTime = 1.15f;   // 最长滑铲时间

        public Vector3 Velocity;
        public bool Grounded;
        public float CurrentHeight = 1.78f;
        public bool IsCrouching;
        public bool IsSprinting;
        public bool IsSliding;               // 冲刺中按 Ctrl 触发
        public float MoveAmount; // 0..1 用于动画
        public float SpeedMul = 1f;          // 外部移速系数（扛加特林会变慢）
        private float slideTimer;

        private float coyote;
        private float jumpBuffer;
        private bool wasGrounded;
        private float landTimer;

        public void Init(VoxelWorld world, Vector3 spawn)
        {
            World = world;
            transform.position = spawn;
            Velocity = Vector3.zero;
            CurrentHeight = StandHeight;
        }

        public void Tick(Vector2 input, float yaw, bool jumpPressed, bool sprint, bool crouch, bool crouchPressed, float dt)
        {
            if (World == null) return;

            // ---- 滑铲：冲刺状态下按下 Ctrl，沿惯性方向猛滑出去 ----
            Vector3 hvNow = new Vector3(Velocity.x, 0f, Velocity.z);
            if (crouchPressed && !IsSliding && Grounded && sprint && !IsCrouching && hvNow.sqrMagnitude > 22f)
            {
                IsSliding = true;
                slideTimer = 0f;
                Vector3 dir = hvNow.normalized;
                Vector3 boosted = dir * Mathf.Max(SprintSpeed * SlideBoost, hvNow.magnitude * 1.25f);
                Velocity = new Vector3(boosted.x, Velocity.y, boosted.z);
                AudioKit.Play2D("land", 0.6f, Random.Range(0.7f, 0.82f));
            }
            if (IsSliding)
            {
                slideTimer += dt;
                if (!Grounded) slideTimer += dt * 1.5f;    // 滑出边缘会更快收势
                Vector3 hv = new Vector3(Velocity.x, 0f, Velocity.z);
                if (hv.magnitude < WalkSpeed * 0.55f || slideTimer > SlideMaxTime) IsSliding = false;
            }

            // 蹲下（松开时若头顶有方块则保持蹲下）；滑铲视同蹲伏（爆头线更低、更难被瞄准）
            bool wantCrouch = crouch || IsSliding;
            if (!wantCrouch && IsCrouching)
            {
                if (World.Overlaps(transform.position.x, transform.position.y, transform.position.z, Radius, StandHeight))
                    wantCrouch = true;
            }
            IsCrouching = wantCrouch;
            float targetHeight = IsSliding ? SlideHeight : (IsCrouching ? CrouchHeight : StandHeight);
            CurrentHeight = Mathf.MoveTowards(CurrentHeight, targetHeight, dt * 6f);

            IsSprinting = sprint && !IsCrouching && input.y > 0.1f;
            float speed = IsCrouching ? CrouchSpeed : (IsSprinting ? SprintSpeed : WalkSpeed);
            speed *= SpeedMul;

            // 期望水平速度（滑铲中不接受方向输入，靠惯性滑行）
            float sy = Mathf.Sin(yaw * Mathf.Deg2Rad);
            float cy = Mathf.Cos(yaw * Mathf.Deg2Rad);
            Vector3 forward = new Vector3(sy, 0f, cy);
            Vector3 right = new Vector3(cy, 0f, -sy);
            Vector3 wish = forward * input.y + right * input.x;
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            wish *= speed;
            if (IsSliding) wish = Vector3.zero;

            MoveAmount = Mathf.Clamp01(wish.magnitude / WalkSpeed);

            float accel = Grounded ? GroundAccel : AirAccel;
            if (IsSliding && Grounded) accel = SlideFriction;   // 滑铲 = 低摩擦减速
            Vector3 horizVel = new Vector3(Velocity.x, 0f, Velocity.z);
            horizVel = Vector3.MoveTowards(horizVel, wish, accel * dt);

            // 跳跃：只在按下的那一帧生效 —— 按住空格不会变成"贴着高墙原地连跳"
            coyote = Grounded ? 0.12f : Mathf.Max(0f, coyote - dt);
            if (jumpPressed) jumpBuffer = 0.14f; else jumpBuffer = Mathf.Max(0f, jumpBuffer - dt);
            float vy = Velocity.y;
            if (jumpBuffer > 0f && coyote > 0f)
            {
                vy = JumpSpeed;
                jumpBuffer = 0f;
                coyote = 0f;
                IsSliding = false;    // 滑铲跳跃：带着惯性跳出去（滑跳）
                AudioKit.Play2D("jump", 0.6f, Random.Range(0.95f, 1.05f));
            }
            vy += Gravity * dt;
            if (vy < MaxFallSpeed) vy = MaxFallSpeed;

            Velocity = new Vector3(horizVel.x, vy, horizVel.z);

            Vector3 delta = Velocity * dt;
            bool hitWall;
            Vector3 pos = World.MoveAABB(transform.position, Radius, CurrentHeight, delta, out Grounded, out hitWall);
            if (Grounded && Velocity.y < 0f) Velocity = new Vector3(Velocity.x, 0f, Velocity.z);
            transform.position = pos;

            // 落地音效
            if (Grounded && !wasGrounded && landTimer <= 0f)
            {
                AudioKit.Play2D("land", 0.35f, Random.Range(0.9f, 1.1f));
                landTimer = 0.15f;
            }
            landTimer -= dt;
            wasGrounded = Grounded;

            // 掉出世界（保险）：回到出生点，避免卡进中央大树的树干
            if (transform.position.y < -20f)
            {
                Vector3 p = World.SpawnPoint;
                if (p.y <= 0.01f) p = new Vector3(World.SX * 0.5f, World.GroundHeight(World.SX / 2, World.SZ / 2, World.SY - 1) + 0.2f, World.SZ * 0.5f);
                RespawnAt(p);
            }
        }

        public void RespawnAt(Vector3 p)
        {
            transform.position = p;
            Velocity = Vector3.zero;
            IsSliding = false;
        }

        public Vector3 EyePosition => transform.position + Vector3.up * (CurrentHeight - 0.16f);
        public Vector3 Center => transform.position + Vector3.up * (CurrentHeight * 0.5f);

        public Bounds WorldBounds
        {
            get
            {
                Vector3 c = transform.position + Vector3.up * (CurrentHeight * 0.5f);
                return new Bounds(c, new Vector3(Radius * 2f, CurrentHeight, Radius * 2f));
            }
        }
    }
}
