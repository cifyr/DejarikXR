#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace XrealAR.EditorTools
{
    // Galaxy XR / Android XR manifest patch. Mirrors the shape of XrealManifestPatch but injects the
    // Android XR app marker + hand-tracking declarations instead of the XREAL `nreal_sdk` meta-data.
    // Both patches are IPostGenerateGradleAndroidProject and would fire on any Android build, so each
    // one checks the active scripting defines at run-time and bails out for the wrong target — this
    // sidesteps the well-known batchmode race where toggling defines + rebuilding in one session
    // hits "script class layout is incompatible" (see XrealBuild.cs comment).
    public class AndroidXrManifestPatch : IPostGenerateGradleAndroidProject
    {
        const string AndroidNs = "http://schemas.android.com/apk/res/android";
        const string DefineGate = "DEJARIK_ANDROID_XR";
        public int callbackOrder => 101; // run after XrealManifestPatch (100); only one of the two acts.

        public void OnPostGenerateGradleAndroidProject(string projectPath)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android) ?? "";
            if (!defines.Contains(DefineGate))
            {
                Debug.Log($"[AndroidXrManifestPatch] skipped (define {DefineGate} not set — XREAL build).");
                return;
            }

            string manifestPath = Path.Combine(projectPath, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogError($"[AndroidXrManifestPatch] manifest not found at {manifestPath}");
                return;
            }

            var doc = new XmlDocument();
            doc.Load(manifestPath);
            var mgr = new XmlNamespaceManager(doc.NameTable);
            mgr.AddNamespace("android", AndroidNs);

            if (doc.SelectSingleNode("/manifest/application", mgr) is not XmlElement app)
            {
                Debug.LogError("[AndroidXrManifestPatch] <application> element not found");
                return;
            }

            // Remove any leftover XREAL meta-data so the same source can produce both APKs cleanly. (The
            // XREAL patch is gated the same way and should not have run on this build, but if some prior
            // build did leave it in the gradle output, scrub it.)
            RemoveMetaData(app, mgr, "nreal_sdk");
            RemoveMetaData(app, mgr, "com.nreal.supportDevices");

            // VERIFY: Android XR app marker. Google's Android XR uses this <application> meta-data tag to
            // identify the app as an XR-immersive experience. Name/value may need adjustment as Android XR
            // ships — confirm against Google's "Declare your app as XR" docs current at release.
            AddMetaData(doc, app, mgr, "com.android.xr.application", "true");

            // Hand tracking: feature declaration + runtime permission. VERIFY exact names against Android
            // XR docs current at build — Google iterated on these during the Android XR preview cycle.
            AddUsesFeature(doc, mgr, "android.hardware.xr.input.hand_tracking", required: false);
            AddUsesFeature(doc, mgr, "android.software.xr.immersive", required: true);
            AddUsesPermission(doc, mgr, "android.permission.HAND_TRACKING");

            // Camera was already harmless on the XREAL path; keep it for any future passthrough/visual
            // input. The XR Hands package may need it on Android XR for joint inference too.
            AddUsesPermission(doc, mgr, "android.permission.CAMERA");

            doc.Save(manifestPath);
            Debug.Log($"[AndroidXrManifestPatch] injected Android XR markers + hand-tracking permission into {manifestPath} (some values marked VERIFY in source).");
        }

        static void AddUsesPermission(XmlDocument doc, XmlNamespaceManager mgr, string name)
        {
            var manifest = doc.SelectSingleNode("/manifest", mgr) as XmlElement;
            if (manifest == null) return;
            if (manifest.SelectSingleNode($"uses-permission[@android:name='{name}']", mgr) != null) return;
            var el = doc.CreateElement("uses-permission");
            el.SetAttribute("name", AndroidNs, name);
            manifest.AppendChild(el);
        }

        static void AddUsesFeature(XmlDocument doc, XmlNamespaceManager mgr, string name, bool required)
        {
            var manifest = doc.SelectSingleNode("/manifest", mgr) as XmlElement;
            if (manifest == null) return;
            if (manifest.SelectSingleNode($"uses-feature[@android:name='{name}']", mgr) != null) return;
            var el = doc.CreateElement("uses-feature");
            el.SetAttribute("name", AndroidNs, name);
            el.SetAttribute("required", AndroidNs, required ? "true" : "false");
            manifest.AppendChild(el);
        }

        static void AddMetaData(XmlDocument doc, XmlElement app, XmlNamespaceManager mgr, string name, string value)
        {
            if (app.SelectSingleNode($"meta-data[@android:name='{name}']", mgr) != null)
                return;
            var el = doc.CreateElement("meta-data");
            el.SetAttribute("name", AndroidNs, name);
            el.SetAttribute("value", AndroidNs, value);
            app.AppendChild(el);
        }

        static void RemoveMetaData(XmlElement app, XmlNamespaceManager mgr, string name)
        {
            var node = app.SelectSingleNode($"meta-data[@android:name='{name}']", mgr);
            if (node != null) app.RemoveChild(node);
        }
    }
}
#endif
