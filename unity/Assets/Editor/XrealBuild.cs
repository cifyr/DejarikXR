using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace XrealAR.EditorTools
{
    // Headless Android config + build. Run via:
    //   Unity -batchmode -nographics -projectPath unity -executeMethod XrealAR.EditorTools.XrealBuild.ConfigurePlayerSettings -quit
    //   Unity -batchmode -nographics -projectPath unity -executeMethod XrealAR.EditorTools.XrealBuild.BuildApk -quit
    public static class XrealBuild
    {
        const string PackageId = "com.cadenwarren.dejarik";
        const string OutputApk = "build/DejarikXR.apk";

        // Galaxy XR variant: separate bundle id so both APKs can coexist on a single device for testing,
        // and a separate output path so build artifacts don't collide.
        const string GalaxyXrPackageId = "com.cadenwarren.dejarik.galaxyxr";
        const string GalaxyXrOutputApk = "build/DejarikXR-galaxyxr.apk";
        const string GalaxyXrDefine = "DEJARIK_ANDROID_XR";

        [MenuItem("XrealAR/Configure Android Player Settings")]
        public static void ConfigurePlayerSettings()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, PackageId);
            PlayerSettings.productName = "Dejarik XR";
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            QualitySettings.vSyncCount = 0;
            AssetDatabase.SaveAssets();
            Debug.Log($"[XrealBuild] Android player settings configured (id={PackageId}, IL2CPP, ARM64, GLES3, minSdk29)");
        }

        // Assumes ConfigurePlayerSettings already ran in a prior invocation. Re-applying the scripting
        // backend in the same session as the build triggers "script class layout is incompatible".
        // Sets the build scene to XREAL's verified Anchors sample (full see-through 6DoF anchor rig).
        // Used to prove the real AR rig builds + runs on-device before wiring our own scene player.
        public static void UseAnchorsSampleScene()
        {
            const string p = "Assets/Samples/XREAL/3.1.0/AR Features/Anchors/Anchors.unity";
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(p, true) };
            Debug.Log($"[XrealBuild] build scene set to {p}");
        }

        public static void BuildApk()
        {
            var enabled = Array.FindAll(EditorBuildSettings.scenes, s => s.enabled);
            if (enabled.Length == 0)
                throw new InvalidOperationException("no scenes in Build Settings; add the AR scene before building");

            var opts = new BuildPlayerOptions
            {
                scenes = Array.ConvertAll(enabled, s => s.path),
                locationPathName = OutputApk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
            };

            Debug.Log($"[XrealBuild] building {enabled.Length} scene(s) -> {OutputApk}");
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[XrealBuild] result={s.result} totalSize={s.totalSize} errors={s.totalErrors} time={s.totalTime}");
            if (s.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        // Publishable, release-signed APK. Keystore path/alias and passwords come from environment variables
        // so secrets never persist into ProjectSettings.asset; signing is applied in-memory for this build
        // only (no SaveAssets). Version comes from env too. Env vars:
        //   DEJARIK_KS_PATH, DEJARIK_KS_PASS, DEJARIK_KEY_ALIAS, DEJARIK_KEY_PASS,
        //   DEJARIK_VERSION (default 1.0), DEJARIK_VERSION_CODE (default 1), DEJARIK_OUT (default build/DejarikXR-release.apk)
        public static void BuildReleaseApk()
        {
            PlayerSettings.bundleVersion = Env("DEJARIK_VERSION", "1.0");
            PlayerSettings.Android.bundleVersionCode = int.Parse(Env("DEJARIK_VERSION_CODE", "1"));

            string ks = Env("DEJARIK_KS_PATH", null);
            if (string.IsNullOrEmpty(ks))
                throw new InvalidOperationException("DEJARIK_KS_PATH not set; cannot produce a release-signed APK");
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = ks;
            PlayerSettings.Android.keystorePass = Env("DEJARIK_KS_PASS", "");
            PlayerSettings.Android.keyaliasName = Env("DEJARIK_KEY_ALIAS", "dejarik");
            PlayerSettings.Android.keyaliasPass = Env("DEJARIK_KEY_PASS", "");

            var enabled = Array.FindAll(EditorBuildSettings.scenes, s => s.enabled);
            if (enabled.Length == 0)
                throw new InvalidOperationException("no scenes in Build Settings; add the AR scene before building");

            string outApk = Env("DEJARIK_OUT", "build/DejarikXR-release.apk");
            var opts = new BuildPlayerOptions
            {
                scenes = Array.ConvertAll(enabled, s => s.path),
                locationPathName = outApk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None, // release (no Development flag)
            };

            Debug.Log($"[XrealBuild] RELEASE build v{PlayerSettings.bundleVersion}({PlayerSettings.Android.bundleVersionCode}) signed with {ks} -> {outApk}");
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[XrealBuild] result={s.result} totalSize={s.totalSize} errors={s.totalErrors} time={s.totalTime}");
            if (s.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        // Galaxy XR (Android XR) variant of BuildApk. Switches bundle id, sets the DEJARIK_ANDROID_XR
        // scripting define (which gates WorldDeck on / phone HoloGui off), runs AndroidXrSetup to flip
        // the XR loader to OpenXR + enable Android XR features, then builds. Output is a distinct APK
        // so both can coexist on one device for side-by-side testing.
        //
        // VERIFY: cannot be tested without Galaxy XR hardware. The build itself should succeed; runtime
        // behavior (hand tracking, anchoring) needs on-device verification before declaring it working.
        public static void BuildGalaxyXrApk()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, GalaxyXrPackageId);

            string existing = PlayerSettings.GetScriptingDefineSymbols(android) ?? "";
            if (!existing.Split(';').Contains(GalaxyXrDefine))
            {
                string updated = string.IsNullOrEmpty(existing) ? GalaxyXrDefine : existing + ";" + GalaxyXrDefine;
                PlayerSettings.SetScriptingDefineSymbols(android, updated);
                AssetDatabase.SaveAssets();
                Debug.Log($"[XrealBuild] added scripting define {GalaxyXrDefine}");
            }

            AndroidXrSetup.BaselineSetup();

            var enabled = Array.FindAll(EditorBuildSettings.scenes, s => s.enabled);
            if (enabled.Length == 0)
                throw new InvalidOperationException("no scenes in Build Settings; add the AR scene before building");

            var opts = new BuildPlayerOptions
            {
                scenes = Array.ConvertAll(enabled, s => s.path),
                locationPathName = GalaxyXrOutputApk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
            };

            Debug.Log($"[XrealBuild] GALAXY XR building {enabled.Length} scene(s) (id={GalaxyXrPackageId}) -> {GalaxyXrOutputApk}");
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[XrealBuild] result={s.result} totalSize={s.totalSize} errors={s.totalErrors} time={s.totalTime}");
            if (s.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        // Release-signed Galaxy XR build, mirroring BuildReleaseApk's env-driven shape.
        //   DEJARIK_KS_PATH, DEJARIK_KS_PASS, DEJARIK_KEY_ALIAS, DEJARIK_KEY_PASS,
        //   DEJARIK_VERSION (default 1.0), DEJARIK_VERSION_CODE (default 1),
        //   DEJARIK_OUT (default build/DejarikXR-galaxyxr-release.apk)
        public static void BuildGalaxyXrReleaseApk()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, GalaxyXrPackageId);
            PlayerSettings.bundleVersion = Env("DEJARIK_VERSION", "1.0");
            PlayerSettings.Android.bundleVersionCode = int.Parse(Env("DEJARIK_VERSION_CODE", "1"));

            string existing = PlayerSettings.GetScriptingDefineSymbols(android) ?? "";
            if (!existing.Split(';').Contains(GalaxyXrDefine))
                PlayerSettings.SetScriptingDefineSymbols(android, string.IsNullOrEmpty(existing) ? GalaxyXrDefine : existing + ";" + GalaxyXrDefine);

            string ks = Env("DEJARIK_KS_PATH", null);
            if (string.IsNullOrEmpty(ks))
                throw new InvalidOperationException("DEJARIK_KS_PATH not set; cannot produce a release-signed APK");
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = ks;
            PlayerSettings.Android.keystorePass = Env("DEJARIK_KS_PASS", "");
            PlayerSettings.Android.keyaliasName = Env("DEJARIK_KEY_ALIAS", "dejarik");
            PlayerSettings.Android.keyaliasPass = Env("DEJARIK_KEY_PASS", "");

            AndroidXrSetup.BaselineSetup();

            var enabled = Array.FindAll(EditorBuildSettings.scenes, s => s.enabled);
            if (enabled.Length == 0)
                throw new InvalidOperationException("no scenes in Build Settings; add the AR scene before building");

            string outApk = Env("DEJARIK_OUT", "build/DejarikXR-galaxyxr-release.apk");
            var opts = new BuildPlayerOptions
            {
                scenes = Array.ConvertAll(enabled, s => s.path),
                locationPathName = outApk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };

            Debug.Log($"[XrealBuild] GALAXY XR RELEASE v{PlayerSettings.bundleVersion}({PlayerSettings.Android.bundleVersionCode}) signed with {ks} -> {outApk}");
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[XrealBuild] result={s.result} totalSize={s.totalSize} errors={s.totalErrors} time={s.totalTime}");
            if (s.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        // After a Galaxy XR build, the XREAL build needs its define removed, its bundle id restored,
        // AND its XR loader swapped back — BuildGalaxyXrApk removed the XREAL loader and assigned the
        // OpenXR loader, and the XREAL build path never re-asserts the loader on its own. Symmetric
        // restore: assign XREAL loader, remove OpenXR loader, clear the define, reset the bundle id.
        public static void RestoreXrealBuildConfig()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, PackageId);
            string existing = PlayerSettings.GetScriptingDefineSymbols(android) ?? "";
            var cleaned = string.Join(";", existing.Split(';').Where(d => d != GalaxyXrDefine && !string.IsNullOrEmpty(d)));
            PlayerSettings.SetScriptingDefineSymbols(android, cleaned);

            // Loader swap: invert what BuildGalaxyXrApk did. XrealXRSetup.EnsureXrAndroid re-assigns the
            // XREAL loader; we then explicitly remove the OpenXR loader so only one is active for the build.
            XrealXRSetup.EnsureXrAndroid();
            UnityEditor.XR.Management.Metadata.XRPackageMetadataStore.RemoveLoader(
                XrGetManager(BuildTargetGroup.Android),
                "UnityEngine.XR.OpenXR.OpenXRLoader",
                BuildTargetGroup.Android);

            AssetDatabase.SaveAssets();
            Debug.Log($"[XrealBuild] restored XREAL build config (id={PackageId}, removed {GalaxyXrDefine}, re-assigned XREAL loader, removed OpenXR loader)");
        }

        // Resolve the XRManagerSettings instance for a given build target group via the XR Management
        // config object, so RestoreXrealBuildConfig can manipulate the loader list without owning a
        // reference to the per-build-target settings asset.
        static UnityEngine.XR.Management.XRManagerSettings XrGetManager(BuildTargetGroup group)
        {
            EditorBuildSettings.TryGetConfigObject(
                UnityEngine.XR.Management.XRGeneralSettings.k_SettingsKey,
                out UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget perBT);
            return perBT?.SettingsForBuildTarget(group)?.Manager;
        }

        static string Env(string key, string fallback)
        {
            string v = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrEmpty(v) ? fallback : v;
        }
    }
}
