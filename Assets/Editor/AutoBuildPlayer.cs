using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PixelArena.EditorTools
{
    /// <summary>
    /// 自动打包工具（临时脚本，正式发布前可整份删除）：
    /// 打开工程后，如果发现 exe / apk 不存在、或脚本/着色器/场景比产物更新，就自动重新打包，
    /// 全部完成后自动退出编辑器。这样改完代码只需"打开一次 Unity"即可拿到新的 exe 和 apk。
    /// 未安装 Android Build Support 模块时跳过 APK 并在日志里说明，不影响 PC 构建。
    ///
    /// 想让编辑器保持打开：在工程根目录放一个名为 AutoBuild.disabled 的文件（内容随意），
    /// 或者用菜单「方块竞技场 > 7. 自动打包（开 / 关）」切换 —— 关闭后不会自动构建、也不会自动退出。
    /// 只有当至少一个构建真正成功时才退出，构建失败会保留编辑器方便排查。
    /// </summary>
    internal static class AutoBuildPlayer
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string DisableFileName = "AutoBuild.disabled";

        private static bool attempted;
        private static double nextTry;
        private static int waitTicks;

        /// <summary>工程根目录是否存在关闭标记文件。</summary>
        private static bool Disabled
        {
            get { return File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), DisableFileName)); }
        }

        [MenuItem("方块竞技场/7. 自动打包（开 / 关）", false, 70)]
        private static void ToggleAutoBuildMenu()
        {
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), DisableFileName);
            bool turnOff = !File.Exists(path);
            if (turnOff) File.WriteAllText(path, "存在本文件时，AutoBuildPlayer 不会自动打包、也不会自动退出编辑器。\n删除本文件即可恢复。\n");
            else File.Delete(path);

            EditorUtility.DisplayDialog("方块竞技场",
                turnOff
                    ? "已【关闭】自动打包。\n下次打开工程不会自动构建，也不会自动退出。"
                    : "已【开启】自动打包。\n下次打开工程若产物过期会自动构建，完成后自动退出。",
                "好");
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (attempted)
            {
                EditorApplication.update -= Tick;
                return;
            }
            if (Disabled)
            {
                attempted = true;
                EditorApplication.update -= Tick;
                Debug.Log("[PixelArena] 自动打包已关闭（工程根目录存在 " + DisableFileName +
                          "）。要恢复：删除该文件，或用菜单「方块竞技场 > 7. 自动打包（开 / 关）」。");
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                nextTry = EditorApplication.timeSinceStartup + 1.5;
                return;
            }
            if (EditorApplication.timeSinceStartup < nextTry) return;
            nextTry = EditorApplication.timeSinceStartup + 2.0;

            string root = Path.GetDirectoryName(Application.dataPath);
            string pcPath = root + "/Build/PC/PixelArena.exe";
            string apkPath = root + "/Build/Android/PixelArena.apk";
            bool androidOk = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);

            try
            {
                bool pcNeeded = NeedsBuild(root, pcPath);
                bool apkNeeded = androidOk && NeedsBuild(root, apkPath);

                if (!pcNeeded && !apkNeeded)
                {
                    attempted = true;
                    EditorApplication.update -= Tick;
                    Debug.Log("[PixelArena] 已是最新构建，无需重新打包。");
                    if (!androidOk) Debug.LogWarning("[PixelArena] 提示：未安装 Android Build Support 模块，无法产出 APK。");
                    return;
                }

                // 等 Resources 里的着色器真正导入完成再打包，否则出来的包依旧着色器缺失
                if (Resources.Load<Shader>("Shaders/VoxelBlocks") == null)
                {
                    waitTicks++;
                    if (waitTicks < 60)
                    {
                        if (waitTicks % 10 == 1) Debug.Log("[PixelArena] 等待着色器导入…");
                        return;
                    }
                    Debug.LogWarning("[PixelArena] 着色器导入等待超时，继续打包（可能缺少自定义着色器）。");
                }

                attempted = true;
                EditorApplication.update -= Tick;

                bool anySuccess = false;
                if (pcNeeded) anySuccess |= BuildPc(pcPath);
                if (apkNeeded) anySuccess |= BuildApk(apkPath);
                else if (!androidOk)
                {
                    Debug.LogWarning("[PixelArena] 未安装 Android Build Support（SDK/NDK/JDK）模块，本次跳过 APK。" +
                                     "请在 Unity Hub → 编辑器版本 → 添加模块 勾选后重开工程即可自动补打。");
                }

                if (anySuccess)
                {
                    Debug.Log("[PixelArena] 自动构建流程结束，正在退出编辑器。");
                    EditorApplication.Exit(0);
                }
                else
                {
                    Debug.LogWarning("[PixelArena] 本次没有任何构建成功，保持编辑器打开，方便你看上方错误。");
                }
            }
            catch (Exception e)
            {
                attempted = true;
                EditorApplication.update -= Tick;
                Debug.LogError("[PixelArena] 自动构建流程异常：" + e);
            }
        }

        private static bool BuildPc(string pcPath)
        {
            Debug.Log("[PixelArena] 开始自动构建 Windows x64 -> " + pcPath);
            DemoSetup.ApplyProjectSettings();
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            string dir = Path.GetDirectoryName(pcPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return RunBuild(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = pcPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            }, "Windows x64");
        }

        private static bool BuildApk(string apkPath)
        {
            Debug.Log("[PixelArena] 开始自动构建 Android APK -> " + apkPath +
                      "（IL2CPP + ARM64，首次构建要跑 IL2CPP 与 Gradle，可能 10~25 分钟，请耐心等待）");
            DemoSetup.ApplyProjectSettings();
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[PixelArena] 切换到 Android 构建目标失败。");
                return false;
            }
            string dir = Path.GetDirectoryName(apkPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return RunBuild(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            }, "Android APK");
        }

        private static bool RunBuild(BuildPlayerOptions opt, string label)
        {
            BuildReport report = BuildPipeline.BuildPlayer(opt);
            if (report == null)
            {
                Debug.LogError("[PixelArena] " + label + " 构建未返回报告。");
                return false;
            }
            string result = report.summary.result.ToString();
            Debug.Log("[PixelArena] " + label + " 自动构建结果：" + result
                      + " | 输出：" + report.summary.outputPath
                      + " | 大小：" + report.summary.totalSize + " 字节"
                      + " | 错误数：" + report.summary.totalErrors);
            if (result != "Succeeded")
                Debug.LogError("[PixelArena] " + label + " 构建未成功，请查看上方错误。");
            return result == "Succeeded";
        }

        /// <summary>产物不存在，或有比产物更新的源文件时返回 true。</summary>
        private static bool NeedsBuild(string root, string outPath)
        {
            if (!File.Exists(outPath)) return true;
            DateTime outTime = File.GetLastWriteTimeUtc(outPath);

            string assetsDir = Path.Combine(root, "Assets");
            if (Directory.Exists(assetsDir))
            {
                foreach (string f in Directory.GetFiles(assetsDir, "*.*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext != ".cs" && ext != ".shader" && ext != ".unity") continue;
                    if (File.GetLastWriteTimeUtc(f) > outTime) return true;
                }
            }

            string gfx = Path.Combine(root, "ProjectSettings/GraphicsSettings.asset");
            if (File.Exists(gfx) && File.GetLastWriteTimeUtc(gfx) > outTime) return true;

            return false;
        }
    }
}
