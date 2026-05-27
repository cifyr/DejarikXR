using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils;
using Unity.XR.XREAL;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SpatialTracking;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;

namespace XrealAR.EditorTools
{
    // Headless XR provider enable + baseline scene, so a valid XREAL APK can be built without the GUI.
    // Loader type verified from the imported package: Unity.XR.XREAL.XREALXRLoader (XREALSettings already
    // defaults to MODE_6DOF + SinglePassInstanced + Controller, which is what we want).
    public static class XrealXRSetup
    {
        const string LoaderType = "Unity.XR.XREAL.XREALXRLoader";
        const string XrSettingsAsset = "Assets/XR/XRGeneralSettings.asset";
        const string ScenePath = "Assets/Scenes/Main.unity";

        public static void BaselineSetup()
        {
            EnableXrealLoader();
            CreateBaselineScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[XrealXRSetup] baseline setup complete");
        }

        // Verifies + asserts the XREAL loader is enabled for Android. Run with the active build target
        // already on Android so XR Management's manifest injection (nreal_sdk meta-data) fires at build.
        public static void EnsureXrAndroid()
        {
            EnableXrealLoader();
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perBT);
            var s = perBT.SettingsForBuildTarget(BuildTargetGroup.Android);
            bool assigned = XRPackageMetadataStore.IsLoaderAssigned(LoaderType, BuildTargetGroup.Android);
            Debug.Log($"[XrealXRSetup] active target={EditorUserBuildSettings.activeBuildTarget} XREAL loader assigned for Android={assigned} InitOnStart={s.InitManagerOnStart}");
            AssetDatabase.SaveAssets();
        }

        // Root-cause fix for "Failed to get XREAL Settings" at runtime (XR never starts -> falls back to
        // nebulaOS). XREALSettings.GetSettings() returns a static set only in the asset's Awake(), which
        // runs only if the asset is a Preloaded Asset. Headless loader-enable created the asset but never
        // registered it as an XR config object or preloaded it. Do both, and fill the sparse defaults.
        const string XREALSettingsAsset = "Assets/XR/Settings/XREALSettings.asset";
        const string VirtualControllerPrefab = "Packages/com.xreal.xr/Runtime/Prefabs/XREALVirtualController.prefab";

        // The Anchors sample UI is TextMeshPro; without the TMP essential resources (default font asset),
        // every TMP label throws NullReferenceException on Awake and the sample's Start() fails, so the
        // first frame never renders and the Unity splash hangs ("jittering forever"). Import them headlessly.
        // Builds a clean minimal scene: a 6DoF head-tracked camera rig + one static cube floating in
        // world space, with an optical-see-through camera (clears to black = transparent on the optics).
        // No sample UI/scripts, trivial render load -> isolates whether basic world-locked 6DoF is smooth.
        public static void BuildCubeScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // AR session + anchor subsystem, required for XREAL persistent spatial anchors (PIN-to-room).
            var sessionGO = new GameObject("AR Session");
            sessionGO.AddComponent<ARSession>();

            var originGO = new GameObject("XR Origin");
            var origin = originGO.AddComponent<XROrigin>();
            originGO.AddComponent<ARAnchorManager>();   // AF requires the anchor manager on the XR Origin

            var offsetGO = new GameObject("Camera Offset");
            offsetGO.transform.SetParent(originGO.transform, false);

            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            camGO.transform.SetParent(offsetGO.transform, false);
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;   // black renders transparent on the see-through optics
            cam.nearClipPlane = 0.1f;
            camGO.AddComponent<AudioListener>();  // required for any 3D audio to be heard

            var tpd = camGO.AddComponent<TrackedPoseDriver>();
            tpd.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.Center);
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;

            origin.Camera = cam;
            origin.CameraFloorOffsetObject = offsetGO;

            // Persistent spatial anchoring (PIN-to-room) for the board.
            var anchorCtrl = new GameObject("Anchor Controller");
            anchorCtrl.AddComponent<AnchorPlacementController>();

            // The game: builds the board, loads creature glbs from StreamingAssets, runs the match.
            var game = new GameObject("Dejarik Game");
            game.AddComponent<Dejarik.View.DejarikGame>();

            // Unity XR Hands official self-driving hand prefabs (XRHandTrackingEvents + XRHandSkeletonDriver
            // + rigged mesh). Parent under the Camera Offset so joints map to world like the head camera.
            const string handDir = "Assets/Samples/XRHands/Prefabs";
            foreach (var name in new[] { "Left Hand Tracking", "Right Hand Tracking" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{handDir}/{name}.prefab");
                if (prefab == null) { Debug.LogWarning($"[XrealXRSetup] missing hand prefab {name}"); continue; }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.transform.SetParent(offsetGO.transform, false);
            }

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true) };
            Debug.Log("[XrealXRSetup] built clean cube scene at Assets/Scenes/Main.unity and set as build scene");
        }

        // glTFast's runtime materials use these shaders, but nothing references them at build time so they
        // get stripped -> runtime-loaded glb meshes render invisible. Force them into the build.
        public static void AddGltfastShaders()
        {
            // glTF/* for runtime-loaded models; Standard for runtime-created primitives (hand joint
            // spheres, selection marker) — otherwise these shaders get stripped and render invisibly.
            string[] names = { "glTF/PbrMetallicRoughness", "glTF/PbrSpecularGlossiness", "glTF/Unlit", "Standard", "Unlit/Color",
                               "TextMeshPro/Distance Field", "TextMeshPro/Mobile/Distance Field" };
            var so = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in names)
            {
                var shader = Shader.Find(name);
                if (shader == null) { Debug.LogWarning($"[XrealXRSetup] shader not found: {name}"); continue; }
                bool present = false;
                for (int i = 0; i < arr.arraySize; i++)
                    if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader) present = true;
                if (!present)
                {
                    arr.InsertArrayElementAtIndex(arr.arraySize);
                    arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = shader;
                    Debug.Log($"[XrealXRSetup] added always-included shader: {name}");
                }
            }
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        public static void ImportTMPEssentials()
        {
            TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[XrealXRSetup] TMP essential resources imported");
        }

        public static void FixXrealSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<XREALSettings>(XREALSettingsAsset);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<XREALSettings>();
                AssetDatabase.CreateAsset(settings, XREALSettingsAsset);
            }

            settings.InitialTrackingType = TrackingType.MODE_6DOF;
            settings.InitialInputSource = InputSource.ControllerAndHands;
            settings.SupportMultiResume = true;
            settings.EnableAutoLogcat = true;
            settings.VirtualController = AssetDatabase.LoadAssetAtPath<GameObject>(VirtualControllerPrefab);
            EditorUtility.SetDirty(settings);

            // Register as the XR config object so editor + XR Management see it.
            EditorBuildSettings.AddConfigObject(XREALSettings.k_SettingsKey, settings, true);

            // Preload it so its Awake() runs at runtime and populates the static instance.
            var preloaded = PlayerSettings.GetPreloadedAssets().Where(a => a != null).ToList();
            if (!preloaded.Contains(settings))
            {
                preloaded.Add(settings);
                PlayerSettings.SetPreloadedAssets(preloaded.ToArray());
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[XrealXRSetup] XREALSettings registered as config object + preloaded; VirtualController={(settings.VirtualController ? "set" : "MISSING")}");
        }

        static void EnableXrealLoader()
        {
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perBT) || perBT == null)
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

            bool ok = XRPackageMetadataStore.AssignLoader(settings.Manager, LoaderType, BuildTargetGroup.Android);
            Debug.Log($"[XrealXRSetup] AssignLoader({LoaderType}) -> {ok}");
            EditorUtility.SetDirty(perBT);
        }

        static void CreateBaselineScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[XrealXRSetup] baseline scene saved to {ScenePath}, set as build scene");
        }
    }
}
