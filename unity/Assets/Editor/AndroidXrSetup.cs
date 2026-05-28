using System;
using System.Linq;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

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

        // Feature IDs verified against the imported package source (PackageCache):
        //   AndroidXR-openxr 1.2 — Runtime/Features/AndroidXRSupportFeature.cs / Subsystems/*/Feature.cs
        //   OpenXR 1.16 — Runtime/CompositionLayers/OpenXRCompositionLayersFeature.cs,
        //                 Runtime/Features/Interactions/HandInteractionProfile.cs
        //   XR Hands 1.7 — Runtime/OpenXR/HandTracking.cs
        // The OpenXR build validator blocks the build unless (a) at least one interaction profile is
        // enabled (Hand Interaction Profile satisfies this for hand-driven Android XR) and
        // (b) Composition Layers Support is enabled if the Composition Layers package is in the project
        // (it gets pulled in transitively by androidxr-openxr 1.2). The Android XR Session + Anchor and
        // the Hand Tracking subsystem are the runtime features the game actually uses.
        static readonly string[] AndroidXrFeatureIds =
        {
            "com.unity.openxr.feature.androidxr-support",                 // Android XR parent feature
            "com.unity.openxr.feature.arfoundation-androidxr-session",    // AR Session (XR Origin lifecycle)
            "com.unity.openxr.feature.arfoundation-androidxr-anchor",     // AR Anchor (PIN-to-room)
            "com.unity.openxr.feature.input.handtracking",                // Hand Tracking Subsystem (XR Hands)
            "com.unity.openxr.feature.input.handinteraction",             // Hand Interaction Profile (validator)
            "com.unity.openxr.feature.compositionlayers",                 // Composition Layers Support (validator)
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

        // Direct calls to the OpenXR Editor API (FeatureHelpers) to locate the feature ScriptableObjects,
        // then SerializedObject to set the m_enabled backing field directly. We bypass the public
        // `feature.enabled` setter on purpose because in androidxr-openxr 1.2 the setter fires
        // AndroidXROpenXRFeature.OnEnabledChange -> OpenXRLifeCycleFeature.RefreshEnabledState, which
        // NPEs in batchmode (the Hidden lifecycle feature isn't yet in OpenXRSettings.features and its
        // delayCall hasn't fired). SerializedObject writes the backing field directly, so OnEnabledChange
        // never runs and the validator still sees feature.enabled==true via the getter on the next read.
        public static void EnableAndroidXrOpenXrFeatures()
        {
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);

            int enabled = 0, missing = 0, skipped = 0;
            foreach (var id in AndroidXrFeatureIds)
            {
                var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android, id);
                if (feature == null)
                {
                    missing++;
                    Debug.LogWarning($"[AndroidXrSetup] OpenXR feature not found for id={id}; the providing package may not be imported. Tick it in Project Settings -> XR -> OpenXR -> Android if needed.");
                    continue;
                }

                var so = new SerializedObject(feature);
                var prop = so.FindProperty("m_enabled");
                if (prop == null)
                {
                    skipped++;
                    Debug.LogWarning($"[AndroidXrSetup] feature {id} has no m_enabled serialized field (unexpected schema)");
                    continue;
                }
                prop.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(feature);
                enabled++;
                Debug.Log($"[AndroidXrSetup] enabled OpenXR feature {id}  ({feature.GetType().Name})");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[AndroidXrSetup] feature toggle complete: enabled={enabled} missing={missing} skipped={skipped}");
        }

        // One-call setup wired into the build script: loader + features in one go.
        public static void BaselineSetup()
        {
            EnableOpenXrLoaderAndroid();
            EnableAndroidXrOpenXrFeatures();
            Debug.Log("[AndroidXrSetup] baseline Android XR setup complete (loader + features enabled headlessly).");
        }
    }
}
