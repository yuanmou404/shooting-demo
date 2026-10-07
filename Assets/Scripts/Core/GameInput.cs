using System.Collections.Generic;
using UnityEngine;

namespace PixelArena
{
    /// <summary>玩家设置（存 PlayerPrefs）。</summary>
    public static class GameSettings
    {
        // ---------- 可选项 ----------
        public static readonly int[] FpsOptions = { 0, 30, 60, 90, 120, 144, 240 }; // 0 = 不限
        public static readonly string[] FullscreenNames =
            { "窗口 WINDOWED", "无边框 BORDERLESS", "独占全屏 EXCLUSIVE" };

        // ---------- 操作 ----------
        public static float Sensitivity = 2.2f;
        public static bool InvertY = false;

        // ---------- 画面 ----------
        public static bool Pixelation = true;
        public static int PixelScale = 3;
        public static int ResolutionIndex = -1;  // -1 = 跟随系统 / 当前分辨率
        public static int FullscreenIndex = 1;   // 0 窗口 / 1 无边框 / 2 独占全屏
        public static int TargetFps = 0;         // 0 = 不限
        public static bool ShowFps = true;

        // ---------- 音频 / 其它 ----------
        public static float SfxVolume = 0.75f;
        public static bool AutoFireOnMobile = true;

        private static List<Resolution> resList;

        /// <summary>本机可用分辨率（按从大到小去重，过滤掉过小的）。</summary>
        public static List<Resolution> Resolutions
        {
            get
            {
                if (resList == null)
                {
                    resList = new List<Resolution>();
                    Resolution[] all = Screen.resolutions;
                    var seen = new HashSet<string>();
                    for (int i = all.Length - 1; i >= 0; i--)   // Screen.resolutions 是升序，倒着取即从大到小
                    {
                        Resolution r = all[i];
                        if (r.width < 800 || r.height < 450) continue;
                        if (!seen.Add(r.width + "x" + r.height)) continue;
                        resList.Add(r);
                    }
                    if (resList.Count == 0 && all.Length > 0)
                        resList.Add(all[all.Length - 1]);
                }
                return resList;
            }
        }

        public static void Load()
        {
            Sensitivity = PlayerPrefs.GetFloat("pa_sens", 2.2f);
            InvertY = PlayerPrefs.GetInt("pa_invert", 0) == 1;
            Pixelation = PlayerPrefs.GetInt("pa_pixel", 1) == 1;
            PixelScale = Mathf.Clamp(PlayerPrefs.GetInt("pa_pxscale", 3), 1, 6);
            SfxVolume = PlayerPrefs.GetFloat("pa_sfx", 0.75f);
            AutoFireOnMobile = PlayerPrefs.GetInt("pa_autofire", 1) == 1;
            ResolutionIndex = PlayerPrefs.GetInt("pa_res", -1);
            FullscreenIndex = Mathf.Clamp(PlayerPrefs.GetInt("pa_fsmode", 1), 0, 2);
            TargetFps = PlayerPrefs.GetInt("pa_fps", 0);
            ShowFps = PlayerPrefs.GetInt("pa_showfps", 1) == 1;
            if (ResolutionIndex >= Resolutions.Count) ResolutionIndex = -1;
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat("pa_sens", Sensitivity);
            PlayerPrefs.SetInt("pa_invert", InvertY ? 1 : 0);
            PlayerPrefs.SetInt("pa_pixel", Pixelation ? 1 : 0);
            PlayerPrefs.SetInt("pa_pxscale", PixelScale);
            PlayerPrefs.SetFloat("pa_sfx", SfxVolume);
            PlayerPrefs.SetInt("pa_autofire", AutoFireOnMobile ? 1 : 0);
            PlayerPrefs.SetInt("pa_res", ResolutionIndex);
            PlayerPrefs.SetInt("pa_fsmode", FullscreenIndex);
            PlayerPrefs.SetInt("pa_fps", TargetFps);
            PlayerPrefs.SetInt("pa_showfps", ShowFps ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void ResetDefaults()
        {
            Sensitivity = 2.2f; InvertY = false;
            Pixelation = true; PixelScale = 3;
            ResolutionIndex = -1; FullscreenIndex = 1; TargetFps = 0; ShowFps = true;
            SfxVolume = 0.75f; AutoFireOnMobile = true;
            Save(); ApplyDisplay(); ApplyPixelation();
        }

        // ---------- 文本标签 ----------

        public static string ResolutionLabel()
        {
            if (ResolutionIndex < 0 || ResolutionIndex >= Resolutions.Count) return "跟随系统 DEFAULT";
            Resolution r = Resolutions[ResolutionIndex];
            return r.width + " × " + r.height + "  " + RateOf(r) + "Hz";
        }

        public static string FullscreenLabel()
        {
            int i = Mathf.Clamp(FullscreenIndex, 0, FullscreenNames.Length - 1);
            return FullscreenNames[i];
        }

        public static string FpsLabel()
        {
            return TargetFps <= 0 ? "不限 UNLIMITED" : TargetFps + " FPS";
        }

        // ---------- 循环切换 ----------

        public static void CycleResolution(int dir)
        {
            int n = Resolutions.Count;
            if (n <= 0) { ResolutionIndex = -1; return; }
            int i = ResolutionIndex;           // -1 表示"跟随系统"，也参与循环
            i += dir;
            if (i < -1) i = n - 1;
            if (i >= n) i = -1;
            ResolutionIndex = i;
            Save(); ApplyDisplay();
        }

        public static void CycleFullscreen(int dir)
        {
            FullscreenIndex = (FullscreenIndex + dir + 3) % 3;
            Save(); ApplyDisplay();
        }

        public static void CycleFps(int dir)
        {
            int i = 0;
            for (int k = 0; k < FpsOptions.Length; k++)
                if (FpsOptions[k] == TargetFps) { i = k; break; }
            i = (i + dir + FpsOptions.Length) % FpsOptions.Length;
            TargetFps = FpsOptions[i];
            Save(); ApplyDisplay();
        }

        // ---------- 应用 ----------

        public static void ApplyDisplay()
        {
            Application.targetFrameRate = TargetFps > 0 ? TargetFps : -1;
            QualitySettings.vSyncCount = 0;

            FullScreenMode mode = FullScreenMode.Windowed;
            if (FullscreenIndex == 1) mode = FullScreenMode.FullScreenWindow;
            else if (FullscreenIndex == 2) mode = FullScreenMode.ExclusiveFullScreen;

            if (ResolutionIndex >= 0 && ResolutionIndex < Resolutions.Count)
            {
                Resolution r = Resolutions[ResolutionIndex];
                Screen.SetResolution(r.width, r.height, mode, RateOf(r));
            }
            else
            {
                Screen.fullScreenMode = mode;
            }
        }

        public static void ApplyPixelation()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            var px = cam.GetComponent<Pixelation>();
            if (px != null)
            {
                px.PixelScale = PixelScale;
                px.enabled = Pixelation;
            }
        }

        private static int RateOf(Resolution r)
        {
            int hz = Mathf.RoundToInt(r.refreshRate);
            if (hz < 30) hz = 60;
            return hz;
        }
    }

    /// <summary>
    /// 跨平台输入聚合层：PC 键鼠 / 安卓触控共用一套状态。
    /// 触控侧字段由 MobileControls 写入。
    /// </summary>
    public static class GameInput
    {
        public static bool TouchMode;

        // 输出状态
        public static Vector2 MoveAxis;
        public static Vector2 LookDelta;
        public static bool FireHeld;
        public static bool JumpHeld;
        /// <summary>跳跃只在"按下那一瞬间"触发。按住空格不会连续起跳（对着墙站着就不会原地抽风）。</summary>
        public static bool JumpPressed;
        public static bool SprintHeld;
        public static bool CrouchHeld;
        public static bool ReloadPressed;
        public static bool NextWeaponPressed;
        public static bool PrevWeaponPressed;   // 滚轮向下：切到上一把
        public static bool ToggleViewPressed;
        public static bool PlacePressed;
        public static bool PausePressed;
        public static bool FirePressed;
        public static bool InspectPressed;   // F：检视武器
        public static bool AimHeld;          // 右键：开镜瞄准
        public static int WeaponSlot = -1;   // 数字键直选武器槽（0 起，-1 = 没按）
        public static bool CrouchPressed;    // Ctrl 按下沿：冲刺中触发滑铲
        public static bool InteractPressed;  // E：登/下坦克
        public static bool CallInPressed;    // G：打开/关闭召唤面板

        // 触控输入源
        public static Vector2 TouchMoveAxis;
        public static Vector2 TouchLookDelta;
        public static bool TouchFire, TouchJump, TouchSprint, TouchReload;
        public static bool TouchNextWeapon, TouchToggleView, TouchPlace, TouchInspect, TouchAim;
        public static bool TouchCallIn, TouchInteract, TouchSlide;   // 触屏专属按钮：召唤 / 上下坦克 / 滑铲

        private static bool prevFire, prevReload, prevNext, prevView, prevPause, prevInspect, prevJump;

        public static void Init()
        {
            TouchMode = Application.isMobilePlatform;
        }

        public static void UpdateInput()
        {
            if (TouchMode)
            {
                MoveAxis = TouchMoveAxis;
                LookDelta = TouchLookDelta * (1.15f * GameSettings.Sensitivity);
                FireHeld = TouchFire;
                FirePressed = TouchFire && !prevFire;
                JumpHeld = TouchJump;
                JumpPressed = TouchJump && !prevJump;
                SprintHeld = TouchSprint;
                CrouchHeld = false;
                ReloadPressed = TouchReload && !prevReload;
                NextWeaponPressed = TouchNextWeapon && !prevNext;
                PrevWeaponPressed = false;
                ToggleViewPressed = TouchToggleView && !prevView;
                PlacePressed = TouchPlace;      // 触屏建块按钮是按住生效
                PausePressed = false;
                InspectPressed = TouchInspect && !prevInspect;
                AimHeld = TouchAim;
                CrouchPressed = TouchSlide;       // 摇杆推满冲刺中点 SLIDE = 滑铲
                InteractPressed = TouchInteract;  // RIDE：上/下坦克
                CallInPressed = TouchCallIn;      // SQUAD：打开/关闭召唤面板
                WeaponSlot = -1;
                prevFire = TouchFire; prevReload = TouchReload; prevNext = TouchNextWeapon;
                prevView = TouchToggleView; prevInspect = TouchInspect; prevJump = TouchJump;
                TouchLookDelta = Vector2.zero;
            }
            else
            {
                float x = 0f, z = 0f;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) z += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) z -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
                MoveAxis = new Vector2(x, z);
                if (MoveAxis.sqrMagnitude > 1f) MoveAxis.Normalize();

                float mx = Input.GetAxis("Mouse X");
                float my = Input.GetAxis("Mouse Y");
                if (GameSettings.InvertY) my = -my;
                LookDelta = new Vector2(mx, my) * (0.9f * GameSettings.Sensitivity);

                bool fire = Input.GetMouseButton(0);
                FireHeld = fire;
                FirePressed = fire && !prevFire;
                JumpHeld = Input.GetKey(KeyCode.Space);
                JumpPressed = Input.GetKeyDown(KeyCode.Space);   // 只在按下时触发一次
                SprintHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                CrouchHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
                ReloadPressed = Input.GetKeyDown(KeyCode.R) && !prevReload;
                NextWeaponPressed = (Input.GetKeyDown(KeyCode.Q) || Input.mouseScrollDelta.y > 0.1f) && !prevNext;
                PrevWeaponPressed = Input.mouseScrollDelta.y < -0.1f;   // 滚轮向下 = 上一把
                ToggleViewPressed = Input.GetKeyDown(KeyCode.V) && !prevView;
                // 右键 = 开镜瞄准（狙击倍镜）；建块改到 B 键 / 鼠标中键
                AimHeld = Input.GetMouseButton(1);
                PlacePressed = Input.GetKey(KeyCode.B) || Input.GetMouseButton(2);
                PausePressed = Input.GetKeyDown(KeyCode.Escape) && !prevPause;
                InspectPressed = Input.GetKeyDown(KeyCode.F) && !prevInspect;
                CrouchPressed = Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C);
                InteractPressed = Input.GetKeyDown(KeyCode.E);
                CallInPressed = Input.GetKeyDown(KeyCode.G);

                WeaponSlot = -1;
                if (Input.GetKeyDown(KeyCode.Alpha1)) WeaponSlot = 0;
                else if (Input.GetKeyDown(KeyCode.Alpha2)) WeaponSlot = 1;
                else if (Input.GetKeyDown(KeyCode.Alpha3)) WeaponSlot = 2;
                else if (Input.GetKeyDown(KeyCode.Alpha4)) WeaponSlot = 3;
                else if (Input.GetKeyDown(KeyCode.Alpha5)) WeaponSlot = 4;
                else if (Input.GetKeyDown(KeyCode.Alpha6)) WeaponSlot = 5;   // 召唤的临时武器

                prevFire = fire; prevReload = Input.GetKey(KeyCode.R);
                prevNext = Input.GetKey(KeyCode.Q); prevView = Input.GetKey(KeyCode.V);
                prevPause = Input.GetKeyDown(KeyCode.Escape);
                prevInspect = Input.GetKey(KeyCode.F);
            }
        }

        public static void ClearFrame()
        {
            ReloadPressed = NextWeaponPressed = PrevWeaponPressed = ToggleViewPressed = PlacePressed = false;
            PausePressed = FirePressed = InspectPressed = JumpPressed = false;
            CrouchPressed = InteractPressed = CallInPressed = false;
            WeaponSlot = -1;
        }
    }
}
