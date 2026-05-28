using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;

namespace XrealAR.EditorTools
{
    // Galaxy XR / Android XR parallel of XrealXRSetup. Mirrors its shape but flips the XR loader to
    // OpenXR and asks the Android XR OpenXR provider to be active. This file deliberately depends on
    // ONLY the well-established Unity.XR.Management and OpenXR loader type string — Android XR feature
    // classes are addressed through reflection because their fully-qualified type names vary across
    // package versions and Google has not stably documented them. Anywhere a string ID or type lookup
    // is best-effort, the call is wrapped and a clear VERIFY-in-GUI message logged so the build still
    // produces a valid APK even if a feature toggle silently fails.
    public static class AndroidXrSetup
    {
        // OpenXR plug-in's loader type has been stable since the package's release.
        const string OpenXrLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";
        const string XrealLoaderType = "Unity.XR.XREAL.XREALXRLoader";
        const string XrSettingsAsset = "Assets/XR/XRGeneralSettings.asset";

        // VERIFY: these feature IDs are the conventional Google / Unity Android XR OpenXR feature IDs as
        // of androidxr-openxr 1.2. They may need adjustment if the package version moves. The build still
        // works without these — the user enables them in Project Settings -> XR Plug-in Management ->
        // OpenXR -> Android tab. We try here so a CI build can be one command.
        static readonly string[] AndroidXrFeatureIds =
        {
            "com.google.xr.session",         // VERIFY
            "com.google.xr.handtracking",    // VERIFY
            "com.google.xr.anchors",         // VERIFY
            "com.google.xr.planedetection",  // VERIFY (used if room mesh becomes desirable later)
        };

        // Swaps XREAL loader off for Android, swaps OpenXR loader on. Idempotent. Safe to run repeatedly.
        public static void EnableOpenXrLoaderAndroid()
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perBT);
            if (perBT == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/XR"))
                    AssetDatabase.CreateFolder("Assets", "XR");
                perBT = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perBT, XrSettingsAsset);
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perBT, true);
            }

            var settings = perBT.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<XRGeneralSettings>();
                perBT.SetSettingsForBuildTarget(BuildTargetGroup.Android, settings);
                AssetDatabase.AddObjectToAsset(settings, perBT);
            }
            if (settings.Manager == null)
            {
                settings.Manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                AssetDatabase.AddObjectToAsset(settings.Manager, perBT);
            }

            // Two loaders both claiming the device at startup races; remove XREAL on the Galaxy XR build.
            XRPackageMetadataStore.RemoveLoader(settings.Manager, XrealLoaderType, BuildTargetGroup.Android);
            bool ok = XRPackageMetadataStore.AssignLoader(settings.Manager, OpenXrLoaderType, BuildTargetGroup.Android);
            Debug.Log($"[AndroidXrSetup] AssignLoader({OpenXrLoaderType}) -> {ok}");
            EditorUtility.SetDirty(perBT);
            AssetDatabase.SaveAssets();
        }

        // Reflection-based feature enablement: each ID is looked up in OpenXRSettings.ActiveBuildTargetInstance
        // and enabled. If the API surface differs (renamed method, missing ID), we log and continue so the
        // build doesn't abort — the user can enable the feature manually in Project Settings.
        public static void EnableAndroidXrOpenXrFeatures()
        {
            var openXrSettingsType = Type.GetType("UnityEngine.XR.OpenXR.OpenXRSettings, Unity.XR.OpenXR");
            if (openXrSettingsType == null)
            {
                Debug.LogWarning("[AndroidXrSetup] OpenXRSettings type not found; package not imported yet? Skipping feature toggles (set them in Project Settings -> XR -> OpenXR -> Android).");
                return;
            }

            // OpenXRSettings.ActiveBuildTargetInstance returns the settings asset for the active build target.
            var activeProp = openXrSettingsType.GetProperty("ActiveBuildTargetInstance", BindingFlags.Public | BindingFlags.Static);
            var activeSettings = activeProp?.GetValue(null);
            if (activeSettings == null)
            {
                Debug.LogWarning("[AndroidXrSetup] OpenXRSettings.ActiveBuildTargetInstance returned null; ensure active build target is Android, then re-run. Skipping feature toggles.");
                return;
            }

            // OpenXRSettings.GetFeature(string id) or GetFeature(Type t) by reflection. Then OpenXRFeature.enabled = true.
            var getFeatureById = openXrSettingsType.GetMethod("GetFeature", new[] { typeof(string) });
            if (getFeatureById == null)
            {
                Debug.LogWarning("[AndroidXrSetup] OpenXRSettings.GetFeature(string) not found in this package version; enable features in Project Settings.");
                return;
            }

            int enabled = 0, missing = 0;
            foreach (var id in AndroidXrFeatureIds)
            {
                var feature = getFeatureById.Invoke(activeSettings, new object[] { id });
                if (feature == null) { missing++; Debug.LogWarning($"[AndroidXrSetup] OpenXR feature id not found: {id}  (VERIFY name; enable manually in Project Settings if needed)"); continue; }
                var enabledProp = feature.GetType().GetProperty("enabled");
                if (enabledProp == null) { missing++; Debug.LogWarning($"[AndroidXrSetup] feature {id} has no 'enabled' property in this version"); continue; }
                enabledProp.SetValue(feature, true);
                EditorUtility.SetDirty((UnityEngine.Object)feature);
                enabled++;
                Debug.Log($"[AndroidXrSetup] enabled OpenXR feature {id}");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[AndroidXrSetup] feature toggle: enabled={enabled} missing={missing} (manual GUI fallback for any missing).");
        }

        // One-call setup wired into the build script: loader + features in one go.
        public static void BaselineSetup()
        {
            EnableOpenXrLoaderAndroid();
            EnableAndroidXrOpenXrFeatures();
            Debug.Log("[AndroidXrSetup] baseline Android XR setup complete (VERIFY: open Project Settings -> XR Plug-in Management -> OpenXR -> Android tab and confirm Session, Hand Tracking, Anchors are ticked).");
        }
    }
}
