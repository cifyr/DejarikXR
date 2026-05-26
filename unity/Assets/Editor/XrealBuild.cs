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
    }
}
