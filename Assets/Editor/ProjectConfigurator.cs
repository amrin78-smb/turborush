using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace TurboRush.EditorTools
{
    /// <summary>
    /// Player, Android, graphics and quality configuration. Re-runnable and applied before EVERY Android build
    /// (<see cref="AndroidBuildGuard"/>), so the Redmi A3 requirements can never be lost by an Inspector change.
    /// </summary>
    public static class ProjectConfigurator
    {
        public const string AppVersion = "0.4.0";
        public const int AppVersionCode = 6;
        public const string ApplicationId = "com.studio.turborush3d";
        public const string ProductName = "Turbo Rush 3D";
        public const string CompanyName = "Nocvault";

        public const string VulkanFilterPath = "Assets/Settings/VulkanDeviceFilters.asset";
        public const string PowerVrPattern = ".*PowerVR.*";

        public static readonly string[] QualityNames = { "Low", "Medium", "High" };
        public static string RpAssetPath(int level) => $"Assets/Settings/TR_{QualityNames[level]}_RPAsset.asset";
        private const string TemplateRpAsset = "Assets/Settings/Mobile_RPAsset.asset";

        [MenuItem("Tools/Turbo Rush/Configure Android", priority = 10)]
        public static void ConfigureAndroidMenu()
        {
            ConfigurePlayerSettings();
            Debug.Log("[TR Config] Android configuration applied.\n" + Describe());
        }

        public static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = AppVersion;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, ApplicationId);
            PlayerSettings.Android.bundleVersionCode = Math.Max(AppVersionCode, PlayerSettings.Android.bundleVersionCode);

            // Landscape only (both ways round).
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.useAnimatedAutorotation = true;

            // IL2CPP with BOTH ABIs: the Redmi A3 runs a 32-bit Android and refuses 64-bit-only APKs.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            if ((int)PlayerSettings.Android.minSdkVersion < 25) PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.Android.renderOutsideSafeArea = false;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Minimal);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android, Il2CppCompilerConfiguration.Release);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.gpuSkinning = false;
            PlayerSettings.runInBackground = true; // desktop test players run unfocused; phones pause regardless
            PlayerSettings.SplashScreen.showUnityLogo = true;
            PlayerSettings.SplashScreen.backgroundColor = new Color32(0x1E, 0x6F, 0xD9, 0xFF);
            PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
            EditorUserBuildSettings.buildAppBundle = false;

            ConfigureAndroidGraphics();

            // Desktop test builds: windowed landscape.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Vulkan first, OpenGL ES 3 fallback, PowerVR GPUs denied Vulkan. Unity's device filter list is consulted at
        /// player start-up: a device matching a deny entry skips Vulkan and falls through to the next API in the
        /// list (GLES3). Prior project: on the Redmi A3 (PowerVR GE8320) a Vulkan build ran once, then crashed when a
        /// race started after a full close/relaunch. PowerVR → GLES3 fixed it. Root cause unconfirmed.
        /// </summary>
        public static void ConfigureAndroidGraphics()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });

            EnsureFolder(Path.GetDirectoryName(VulkanFilterPath));
            var filters = AssetDatabase.LoadAssetAtPath<VulkanDeviceFilterLists>(VulkanFilterPath);
            if (filters == null)
            {
                filters = new VulkanDeviceFilterLists();
                AssetDatabase.CreateAsset(filters, VulkanFilterPath);
            }
            filters.vulkanDeviceDenyFilters = new[] { new VulkanDeviceFilterData { deviceName = PowerVrPattern } };
            EditorUtility.SetDirty(filters);
            AssetDatabase.SaveAssets();
            PlayerSettings.Android.androidVulkanDeviceFilterListAsset = filters;

            // Fail loudly if anything did not stick.
            string problem = CheckAndroidSettings();
            if (problem != null) throw new BuildFailedException("[TR Config] " + problem);
        }

        /// <summary>Returns null when every release-blocking Android setting is correct, otherwise the first problem.</summary>
        public static string CheckAndroidSettings()
        {
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
                return "Android scripting backend must be IL2CPP";
            AndroidArchitecture a = PlayerSettings.Android.targetArchitectures;
            if ((a & AndroidArchitecture.ARMv7) == 0 || (a & AndroidArchitecture.ARM64) == 0)
                return "Android must target both ARMv7 and ARM64 (Redmi A3 is 32-bit)";
            if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android)) return "Android must not use default graphics APIs";
            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if (Array.IndexOf(apis, GraphicsDeviceType.OpenGLES3) < 0) return "OpenGLES3 must be in the Android graphics API list";
            VulkanDeviceFilterLists f = PlayerSettings.Android.androidVulkanDeviceFilterListAsset;
            if (f == null) return "Vulkan device filter asset is not assigned";
            bool hasPvr = false;
            if (f.vulkanDeviceDenyFilters != null)
                foreach (VulkanDeviceFilterData d in f.vulkanDeviceDenyFilters)
                    if (d.deviceName == PowerVrPattern) hasPvr = true;
            if (!hasPvr) return "Vulkan deny filter " + PowerVrPattern + " is missing";
            if (PlayerSettings.allowedAutorotateToPortrait || PlayerSettings.allowedAutorotateToPortraitUpsideDown)
                return "Portrait orientation must be disabled";
            return null;
        }

        public static string Describe()
        {
            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            return $"id={PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)} version={PlayerSettings.bundleVersion} " +
                   $"code={PlayerSettings.Android.bundleVersionCode} backend={PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)} " +
                   $"arch={PlayerSettings.Android.targetArchitectures} apis={string.Join(",", apis)} " +
                   $"filter={AssetDatabase.GetAssetPath(PlayerSettings.Android.androidVulkanDeviceFilterListAsset)}";
        }

        /// <summary>Three quality levels, each with its own lean URP asset. No realtime shadows, no HDR, no post.</summary>
        public static void ConfigureQuality()
        {
            var renderPipelines = new RenderPipelineAsset[3];
            for (int i = 0; i < 3; i++)
            {
                string path = RpAssetPath(i);
                if (AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path) == null && !AssetDatabase.CopyAsset(TemplateRpAsset, path))
                    throw new Exception("Could not create " + path);
                var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
                var so = new SerializedObject(rp);
                void SetI(string n, int v) { SerializedProperty p = so.FindProperty(n); if (p == null) throw new Exception("URP field " + n); p.intValue = v; }
                void SetF(string n, float v) { SerializedProperty p = so.FindProperty(n); if (p == null) throw new Exception("URP field " + n); p.floatValue = v; }
                void SetB(string n, bool v) { SerializedProperty p = so.FindProperty(n); if (p == null) throw new Exception("URP field " + n); p.boolValue = v; }
                SetF("m_RenderScale", i == 0 ? 0.75f : i == 1 ? 0.9f : 1f);
                SetI("m_MSAA", i == 0 ? 1 : i == 1 ? 2 : 4);
                SetB("m_SupportsHDR", false);
                SetB("m_MainLightShadowsSupported", false);
                SetB("m_AdditionalLightShadowsSupported", false);
                SetI("m_AdditionalLightsRenderingMode", 0);
                SetB("m_SoftShadowsSupported", false);
                SetB("m_RequireDepthTexture", false);
                SetB("m_RequireOpaqueTexture", false);
                SetB("m_UseSRPBatcher", true);
                SetB("m_EnableLODCrossFade", false);
                SetB("m_SupportsTerrainHoles", false);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rp);
                renderPipelines[i] = rp;
            }

            var qso = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            SerializedProperty levels = qso.FindProperty("m_QualitySettings");
            levels.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                SerializedProperty l = levels.GetArrayElementAtIndex(i);
                l.FindPropertyRelative("name").stringValue = QualityNames[i];
                l.FindPropertyRelative("pixelLightCount").intValue = 0;
                l.FindPropertyRelative("shadows").intValue = 0;
                l.FindPropertyRelative("shadowDistance").floatValue = 0;
                l.FindPropertyRelative("antiAliasing").intValue = 0;
                l.FindPropertyRelative("vSyncCount").intValue = 0;
                l.FindPropertyRelative("anisotropicTextures").intValue = i == 2 ? 1 : 0;
                l.FindPropertyRelative("globalTextureMipmapLimit").intValue = 0;
                l.FindPropertyRelative("realtimeReflectionProbes").boolValue = false;
                l.FindPropertyRelative("softParticles").boolValue = false;
                l.FindPropertyRelative("lodBias").floatValue = i == 0 ? 0.6f : i == 1 ? 1f : 1.5f;
                l.FindPropertyRelative("enableLODCrossFade").boolValue = false;
                l.FindPropertyRelative("skinWeights").intValue = 1;
                l.FindPropertyRelative("customRenderPipeline").objectReferenceValue = renderPipelines[i];
                SerializedProperty ex = l.FindPropertyRelative("excludedTargetPlatforms");
                if (ex != null) ex.arraySize = 0;
            }
            qso.FindProperty("m_CurrentQuality").intValue = 1;
            SerializedProperty perPlatform = qso.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; i < perPlatform.arraySize; i++)
            {
                SerializedProperty e = perPlatform.GetArrayElementAtIndex(i);
                string key = e.FindPropertyRelative("first").stringValue;
                // Android starts Low (Redmi A3 baseline); DeviceQualityManager raises it on capable phones.
                e.FindPropertyRelative("second").intValue = key == "Android" ? 0 : key == "Standalone" ? 2 : 1;
            }
            qso.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = renderPipelines[1];
            AssetDatabase.SaveAssets();
            Debug.Log("[TR Config] Quality levels Low/Medium/High configured.");
        }

        public static void ConfigureBuildScenes(params string[] scenePaths)
        {
            var list = new EditorBuildSettingsScene[scenePaths.Length];
            for (int i = 0; i < scenePaths.Length; i++) list[i] = new EditorBuildSettingsScene(scenePaths[i], true);
            EditorBuildSettings.scenes = list;
        }

        public static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
