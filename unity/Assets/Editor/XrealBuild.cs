using System;
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

        static string Env(string key, string fallback)
        {
            string v = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrEmpty(v) ? fallback : v;
        }
    }
}
