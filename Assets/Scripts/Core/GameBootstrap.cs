using UnityEngine;
using UnityEngine.EventSystems;

namespace PixelArena
{
    /// <summary>
    /// 运行时装配整个游戏（场景里只需要挂这一个脚本）。
    /// 所有资源（贴图、模型、音效、UI）均为程序化生成，工程不含任何美术文件。
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("世界")]
        public int WorldX = 64;
        public int WorldY = 48;
        public int WorldZ = 64;
        public int Seed = 20260927;

        [Header("表现")]
        public int PixelScalePc = 3;
        public int PixelScaleMobile = 2;

        public VoxelWorld World;
        public WorldView View;
        public PlayerController Player;
        public CameraRig Rig;
        public WeaponSystem Weapons;
        public GameManager Gm;
        public GameHUD Hud;
        public MobileControls Mobile;

        private void Awake()
        {
            Setup();
        }

        /// <summary>分阶段执行，任一阶段失败只记录日志、不中断后续装配（避免"进游戏什么都没有"）。</summary>
        private static void Safe(string phase, System.Action act)
        {
            try
            {
                act();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[PixelArena] 阶段「" + phase + "」初始化失败：" + e);
            }
        }

        private void Setup()
        {
            GameSettings.Load();
            GameInput.Init();
            AudioKit.Init();

            // 分辨率 / 全屏 / 帧率上限，全部按玩家设置走
            GameSettings.ApplyDisplay();

            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.shadowResolution = ShadowResolution.Low;
            QualitySettings.pixelLightCount = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            QualitySettings.antiAliasing = 0;
            QualitySettings.vSyncCount = 0;
            RenderSettings.fog = false;

            if (Application.isMobilePlatform)
            {
                Screen.orientation = ScreenOrientation.AutoRotation;
                Screen.autorotateToLandscapeLeft = true;
                Screen.autorotateToLandscapeRight = true;
                Screen.autorotateToPortrait = false;
                Screen.autorotateToPortraitUpsideDown = false;
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }

            // ---------------- 世界
            World = new VoxelWorld(WorldX, WorldY, WorldZ);
            World.GenerateArena(Seed);

            var worldGo = new GameObject("World");
            View = worldGo.AddComponent<WorldView>();
            Safe("体素世界渲染", () => View.Build(World, BlockDef.CreateAtlas()));
            Safe("地图配色", () => View.ApplyLook(World.AmbientColor, World.SunColor, World.FogColor));

            // ---------------- 相机
            // 场景里可能带有 Unity 默认相机（Main Camera，且自带 AudioListener）：
            // 先全部关掉，避免双相机叠加渲染与"两个 AudioListener"的警告。
            var staleCams = FindObjectsOfType<Camera>();
            for (int i = 0; i < staleCams.Length; i++)
                if (staleCams[i] != null) staleCams[i].gameObject.SetActive(false);

            var camGo = new GameObject("PlayerCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = World.SkyColor;   // 天空颜色跟着地图主题走
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 400f;
            cam.fieldOfView = Application.isMobilePlatform ? 70f : 75f;
            camGo.AddComponent<AudioListener>();
            cam.tag = "MainCamera";

            var px = camGo.AddComponent<Pixelation>();
            px.PixelScale = GameSettings.PixelScale;   // 设置里可调 1~6
            px.enabled = GameSettings.Pixelation;

            // ---------------- 玩家
            var playerGo = new GameObject("Player");
            Player = playerGo.AddComponent<PlayerController>();
            Vector3 spawn = World.SpawnPoint;
            if (spawn.y <= 0.01f)
            {
                spawn = new Vector3(WorldX * 0.5f, 0f, WorldZ * 0.5f);
                spawn.y = World.GroundHeight(WorldX / 2, WorldZ / 2, WorldY - 1) + 0.2f;
            }
            Player.Init(World, spawn);

            Rig = camGo.AddComponent<CameraRig>();
            Rig.Init(cam, Player, World);

            // 第三人称主角模型（第一人称时隐藏）：装备与配色和 NPC 明显不同
            var tpModel = BlockCharacter.CreateHero(playerGo.transform);
            tpModel.transform.localPosition = Vector3.zero;
            Rig.ThirdPersonModel = tpModel;

            // 第一人称武器视图模型
            var holderGo = new GameObject("WeaponHolder");
            holderGo.transform.SetParent(camGo.transform, false);
            holderGo.transform.localPosition = new Vector3(0.30f, -0.26f, 0.52f);
            holderGo.transform.localRotation = Quaternion.identity;
            // 视图模型跟随相机旋转，不随身体
            Rig.WeaponHolder = holderGo.transform;

            Weapons = playerGo.AddComponent<WeaponSystem>();
            Weapons.Init(Rig, World, View, Player, holderGo.transform);
            Weapons.BindThirdPersonModel(tpModel);   // 换枪时主角手里的模型同步更换

            Rig.SetViewMode(ViewMode.FirstPerson);

            // ---------------- 特效 / 管理器
            Safe("特效池", () => FxPool.Create(World));

            var gmGo = new GameObject("GameManager");
            Gm = gmGo.AddComponent<GameManager>();
            Gm.Init(World, View, Player, Rig, Weapons);
            Gm.SetSeed(Seed);
            Safe("HUD", () => Gm.Hud = GameHUD.Create(Gm));

            if (GameInput.TouchMode)
            {
                Safe("移动端触控", () => { Mobile = MobileControls.Create(); Gm.Mobile = Mobile; });
            }

            // UI 事件系统（按钮点击必需）
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            // 主菜单背景是真实场景里环绕主角的缓慢运镜（最后生还者重制版那种实机背景）
            Safe("进入主菜单", () => Gm.EnterMenu());
            Debug.Log("[PixelArena] 初始化完成。世界 " + WorldX + "x" + WorldY + "x" + WorldZ + "，平台：" + (GameInput.TouchMode ? "移动端触控" : "PC 键鼠"));
        }
    }
}
