using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PixelArena.EditorTools
{
    /// <summary>
    /// 编辑器工具：自动创建/修复主场景、配置安卓构建参数、一键出 PC / APK 包。
    /// </summary>
    public static class DemoSetup
    {
        private const string SceneDir = "Assets/Scenes";
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string ProductName = "方块竞技场 Pixel Arena";
        private const string CompanyName = "PixelArena";
        private const string BundleId = "com.pixelarena.demo";
        private const string PrefKey = "PixelArena.SceneReady";

        [InitializeOnLoadMethod]
        private static void AutoSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                bool ready = EditorPrefs.GetBool(PrefKey, false);
                if (ready && File.Exists(ScenePath)) { ApplyProjectSettings(); EnsurePreview(); return; }
                try
                {
                    if (!SceneHasBootstrap())
                    {
                        CreateOrFixScene(false);
                        UnityEngine.Debug.Log("[PixelArena] 已自动创建主场景：" + ScenePath);
                    }
                    EditorPrefs.SetBool(PrefKey, true);
                    ApplyProjectSettings();
                    EnsurePreview();
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("[PixelArena] 自动初始化跳过：" + e.Message);
                }
            };
        }

        private static bool SceneHasBootstrap()
        {
            if (!File.Exists(ScenePath)) return false;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) return false;
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                if (roots[i].GetComponentInChildren<GameBootstrap>(true) != null) return true;
            return false;
        }

        [MenuItem("方块竞技场/1. 创建或修复主场景", false, 10)]
        public static void CreateOrFixSceneMenu()
        {
            CreateOrFixScene(true);
        }

        private static void CreateOrFixScene(bool focus)
        {
            if (!Directory.Exists(SceneDir)) Directory.CreateDirectory(SceneDir);

            bool exists = File.Exists(ScenePath);
            if (!exists)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            var opened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            bool has = false;
            var roots = opened.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                if (roots[i].GetComponentInChildren<GameBootstrap>(true) != null) has = true;

            if (!has)
            {
                var go = new GameObject("GameBootstrap");
                go.AddComponent<GameBootstrap>();
                EditorSceneManager.MarkSceneDirty(opened);
                EditorSceneManager.SaveScene(opened);
            }

            // 加入 Build Settings
            var scenes = new EditorBuildSettingsScene[1];
            scenes[0] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = scenes;

            ApplyProjectSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (focus)
            {
                EditorUtility.DisplayDialog("方块竞技场",
                    "主场景已就绪：Assets/Scenes/Main.unity\n\n" +
                    "⚠ 编辑模式下场景看起来是空的，这是正常的：\n" +
                    "地形、玩家、敌人、UI、音效全部在按下 Play 的瞬间由代码生成，" +
                    "工程里没有任何美术资源文件。\n\n" +
                    "我已在场景视图里生成一份不保存的世界预览，你可以直接看到地图；" +
                    "真正的游戏请点 ▶ Play。\n\n" +
                    "PC：WASD 移动 / 鼠标转视角 / 左键射击 / 右键建块 / V 切视角 / Esc 暂停。\n" +
                    "安卓：菜单「方块竞技场 > 4. 构建 Android」。", "好");
            }
        }

        [MenuItem("方块竞技场/2. 应用安卓构建设置", false, 20)]
        public static void ApplySettingsMenu()
        {
            ApplyProjectSettings();
            EditorUtility.DisplayDialog("方块竞技场", "已应用安卓构建设置：\nIL2CPP + ARM64 + 横屏 + MinSDK 23", "好");
        }

        // internal：AutoBuildPlayer 自动构建 PC / APK 前也要应用这套设置
        internal static void ApplyProjectSettings()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = "1.0";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, BundleId);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleId);
            PlayerSettings.Android.bundleVersionCode = 1;

            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
        }

        [MenuItem("方块竞技场/3. 构建 Windows x64", false, 30)]
        public static void BuildWindows()
        {
            if (!EnsureScene()) return;
            ApplyProjectSettings();
            string path = EditorUtility.SaveFolderPanel("选择输出目录", "Build", "PC");
            if (string.IsNullOrEmpty(path)) return;

            var opt = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(path, "PixelArena.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(opt);
            UnityEngine.Debug.Log("[PixelArena] Windows 构建结果：" + report.summary.result + " -> " + report.summary.outputPath);
        }

        [MenuItem("方块竞技场/4. 构建 Android (APK)", false, 40)]
        public static void BuildAndroid()
        {
            if (!EnsureScene()) return;
            ApplyProjectSettings();

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("方块竞技场",
                    "当前 Unity 未安装 Android Build Support 模块。\n请打开 Unity Hub → 给当前版本添加模块 → Android Build Support（含 SDK/NDK/JDK）。", "知道了");
                return;
            }

            string dir = "Build/Android";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var opt = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(dir, "PixelArena.apk"),
                target = BuildTarget.Android,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(opt);
            UnityEngine.Debug.Log("[PixelArena] Android 构建结果：" + report.summary.result + " -> " + report.summary.outputPath);
            if (report.summary.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(dir);
        }

        // ==================================================================
        // 编辑模式下的世界预览
        // 本 Demo 的地形/角色/UI 全部在运行时（Play）由代码生成，所以直接打开
        // 场景会“看起来什么都没有”。这里在编辑器里同步生成一份地形网格，
        // 只为预览观感，标记为 DontSave（不写进场景文件），进入 Play 前自动清除。
        // ==================================================================
        private const string PreviewName = "__WorldPreview (DontSave)";

        [MenuItem("方块竞技场/5. 在编辑器里预览世界", false, 50)]
        public static void PreviewWorldMenu()
        {
            BuildPreview();
        }

        [MenuItem("方块竞技场/6. 清除世界预览", false, 51)]
        public static void ClearPreviewMenu()
        {
            ClearPreview();
            UnityEngine.Debug.Log("[PixelArena] 世界预览已清除。");
        }

        private static void EnsurePreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorSceneManager.GetActiveScene().path != ScenePath) return;
            if (FindPreview() != null) return;
            BuildPreview();
        }

        private static GameObject FindPreview()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid()) return null;
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                if (roots[i] != null && roots[i].name == PreviewName) return roots[i];
            return null;
        }

        private static void BuildPreview()
        {
            try
            {
                ClearPreview();

                int wx = 64, wy = 48, wz = 64, seed = 20260927;
                var bsGo = GameObject.Find("GameBootstrap");
                var bs = bsGo != null ? bsGo.GetComponent<GameBootstrap>() : null;
                if (bs != null) { wx = bs.WorldX; wy = bs.WorldY; wz = bs.WorldZ; seed = bs.Seed; }

                var world = new VoxelWorld(wx, wy, wz);
                world.GenerateArena(seed);

                var root = new GameObject(PreviewName);
                root.hideFlags = HideFlags.DontSave;
                root.AddComponent<WorldView>().Build(world, BlockDef.CreateAtlas());
                MarkPreviewDontSave(root);

                var sv = SceneView.lastActiveSceneView;
                if (sv != null)
                {
                    float ground = world.GroundHeight(wx / 2, wz / 2, wy - 1);
                    sv.pivot = new Vector3(wx * 0.5f, ground + 5f, wz * 0.5f);
                    sv.size = Mathf.Max(wx, wz) * 0.7f;
                    sv.Repaint();
                }

                UnityEngine.Debug.Log("[PixelArena] 已在编辑器里生成世界预览（不写入场景文件，点 ▶ Play 时会自动清除）。" +
                                      "注意：Game 视图在 Play 之前仍是空的，这是正常的。");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("[PixelArena] 世界预览生成失败：" + e.Message);
                ClearPreview();
            }
        }

        private static void MarkPreviewDontSave(GameObject root)
        {
            var ts = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < ts.Length; i++)
                if (ts[i] != null) ts[i].gameObject.hideFlags = HideFlags.DontSave;

            var mfs = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < mfs.Length; i++)
                if (mfs[i] != null && mfs[i].sharedMesh != null) mfs[i].sharedMesh.hideFlags = HideFlags.DontSave;

            var mrs = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < mrs.Length; i++)
                if (mrs[i] != null && mrs[i].sharedMaterial != null) mrs[i].sharedMaterial.hideFlags = HideFlags.DontSave;
        }

        private static void ClearPreview()
        {
            var go = FindPreview();
            if (go == null) return;

            var mfs = go.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < mfs.Length; i++)
                if (mfs[i] != null && mfs[i].sharedMesh != null) UnityEngine.Object.DestroyImmediate(mfs[i].sharedMesh);

            var mrs = go.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < mrs.Length; i++)
                if (mrs[i] != null && mrs[i].sharedMaterial != null) UnityEngine.Object.DestroyImmediate(mrs[i].sharedMaterial);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [InitializeOnLoadMethod]
        private static void HookPreviewLifecycle()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChangedInternal;
            EditorApplication.playModeStateChanged += OnPlayModeStateChangedInternal;

            // 场景被重新加载时（Unity 启动还原上次场景 / 重新编译后重载），
            // DontSave 的预览对象会被销毁，这里补一次重建，保证预览始终在。
            EditorSceneManager.sceneOpened -= OnSceneOpenedInternal;
            EditorSceneManager.sceneOpened += OnSceneOpenedInternal;
        }

        private static void OnPlayModeStateChangedInternal(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) ClearPreview();
        }

        private static void OnSceneOpenedInternal(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
        {
            if (scene.path != ScenePath) return;
            EditorApplication.delayCall += EnsurePreview;
        }

        private static bool EnsureScene()
        {
            if (!File.Exists(ScenePath) || !SceneHasBootstrap())
            {
                CreateOrFixScene(false);
            }
            return File.Exists(ScenePath);
        }
    }
}
