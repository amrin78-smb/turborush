using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TurboRush.EditorTools
{
    /// <summary>
    /// Repeatable player builds. Menu: Tools ▸ Turbo Rush. Batch: Tools/build_android.sh [release], Tools/build_linux.sh.
    /// Every Android build re-applies <see cref="ProjectConfigurator.ConfigurePlayerSettings"/> and is validated
    /// afterwards (ABIs + packaged PowerVR filter) by <see cref="BuildValidator"/>.
    /// </summary>
    public static class BuildScript
    {
        public const string AndroidDevPath = "Builds/Android/TurboRush3D-dev.apk";
        public const string AndroidReleasePath = "Builds/Android/TurboRush3D.apk";
        public const string LinuxPath = "Builds/Linux/TurboRush3D.x86_64";

        public static string[] Scenes
        {
            get
            {
                var list = new List<string>();
                foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes) if (s.enabled) list.Add(s.path);
                if (list.Count == 0) throw new BuildFailedException("No scenes in Build Settings (run Tools ▸ Turbo Rush ▸ Setup Project).");
                foreach (string s in list) if (!File.Exists(s)) throw new BuildFailedException("Build scene missing: " + s);
                return list.ToArray();
            }
        }

        [MenuItem("Tools/Turbo Rush/Build Development APK", priority = 20)]
        public static void AndroidDevelopmentMenu() => BuildAndroid(true, true);

        [MenuItem("Tools/Turbo Rush/Build Release APK", priority = 21)]
        public static void AndroidReleaseMenu() => BuildAndroid(false, true);

        [MenuItem("Tools/Turbo Rush/Build Linux Test Player", priority = 22)]
        public static void LinuxMenu() => BuildLinux();

        public static void AndroidDevelopmentBatch() => Exit(BuildAndroid(true, true));
        public static void AndroidReleaseBatch() => Exit(BuildAndroid(false, true));
        public static void LinuxBatch() => Exit(BuildLinux() != null);

        public static bool BuildAndroid(bool development, bool validate)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            ProjectConfigurator.ConfigurePlayerSettings();
            EditorUserBuildSettings.development = development; // read by AndroidManifestPostProcess
            string path = development ? AndroidDevPath : AndroidReleasePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = development ? BuildOptions.Development : BuildOptions.None
            });
            Log(report);
            if (report.summary.result != BuildResult.Succeeded) return false;
            return !validate || BuildValidator.ValidateApk(path, !development);
        }

        public static BuildReport BuildLinux()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            Directory.CreateDirectory(Path.GetDirectoryName(LinuxPath));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = LinuxPath,
                target = BuildTarget.StandaloneLinux64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            });
            Log(report);
            return report.summary.result == BuildResult.Succeeded ? report : null;
        }

        private static void Exit(bool ok) => EditorApplication.Exit(ok ? 0 : 1);

        private static void Log(BuildReport report)
        {
            BuildSummary s = report.summary;
            Debug.Log($"[TR Build] {s.platform} result={s.result} size={s.totalSize / (1024f * 1024f):0.0} MB " +
                      $"time={s.totalTime.TotalSeconds:0}s errors={s.totalErrors} warnings={s.totalWarnings} output={s.outputPath}");
            foreach (BuildStep step in report.steps)
            foreach (BuildStepMessage m in step.messages)
                if (m.type == LogType.Error || m.type == LogType.Exception) Debug.LogError("[TR Build] " + m.content);
        }
    }

    /// <summary>
    /// Runs before ANY Android build (menu, script or Build Settings window) so the Redmi A3 configuration is
    /// always re-applied; aborts the build if it cannot be.
    /// </summary>
    public sealed class AndroidBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            ProjectConfigurator.ConfigurePlayerSettings();
            string problem = ProjectConfigurator.CheckAndroidSettings();
            if (problem != null) throw new BuildFailedException("[TR Build] " + problem);
            Debug.Log("[TR Build] Android settings verified: " + ProjectConfigurator.Describe());
        }
    }
}
