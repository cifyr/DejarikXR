#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace XrealAR.EditorTools
{
    // XR Management's IAndroidManifestRequirementProvider injection (which normally adds the `nreal_sdk`
    // meta-data marking the app as an XREAL MR app) does NOT fire in our headless batchmode build, so the
    // app installs as a flat 2D app and the glasses only mirror it as a grey window. We inject the required
    // entries directly here. Values mirror XREAL's XREALManifestProvider for the default device set
    // (Reality+Vision): REALITY=1 -> "XrealLight", VISION=2 -> "XrealAir".
    public class XrealManifestPatch : IPostGenerateGradleAndroidProject
    {
        const string AndroidNs = "http://schemas.android.com/apk/res/android";
        const string GalaxyXrGate = "DEJARIK_ANDROID_XR";
        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string projectPath)
        {
            // Gate: a single source produces two APKs (XREAL + Galaxy XR). Skip XREAL meta-data injection
            // when the Galaxy XR scripting define is active so the AndroidXrManifestPatch owns the build.
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android) ?? "";
            if (defines.Contains(GalaxyXrGate))
            {
                Debug.Log($"[XrealManifestPatch] skipped ({GalaxyXrGate} define active — Galaxy XR build).");
                return;
            }

            string manifestPath = Path.Combine(projectPath, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogError($"[XrealManifestPatch] manifest not found at {manifestPath}");
                return;
            }

            var doc = new XmlDocument();
            doc.Load(manifestPath);
            var mgr = new XmlNamespaceManager(doc.NameTable);
            mgr.AddNamespace("android", AndroidNs);

            if (doc.SelectSingleNode("/manifest/application", mgr) is not XmlElement app)
            {
                Debug.LogError("[XrealManifestPatch] <application> element not found");
                return;
            }

            AddMetaData(doc, app, mgr, "nreal_sdk", "true");
            AddMetaData(doc, app, mgr, "com.nreal.supportDevices", "1|XrealLight|2|XrealAir");
            AddMetaData(doc, app, mgr, "autoLog", "1");

            // Hand tracking may want camera access; harmless to declare.
            AddUsesPermission(doc, mgr, "android.permission.CAMERA");

            doc.Save(manifestPath);
            Debug.Log($"[XrealManifestPatch] injected XREAL MR meta-data (nreal_sdk) into {manifestPath}");
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

        static void AddMetaData(XmlDocument doc, XmlElement app, XmlNamespaceManager mgr, string name, string value)
        {
            if (app.SelectSingleNode($"meta-data[@android:name='{name}']", mgr) != null)
                return;
            var el = doc.CreateElement("meta-data");
            el.SetAttribute("name", AndroidNs, name);
            el.SetAttribute("value", AndroidNs, value);
            app.AppendChild(el);
        }
    }
}
#endif
