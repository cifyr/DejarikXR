using UnityEditor;
using UnityEngine;

namespace XrealAR.EditorTools
{
    // Assigns the Android launcher icons from Assets/Icons via the generic PlatformIcon API (hand-editing the
    // icon YAML in ProjectSettings is error-prone, and the Android-specific AndroidPlatformIconKind type isn't
    // referenceable from a normal Editor compile). Multi-layer kinds (Adaptive) get background + foreground;
    // single-layer kinds (Round/Legacy) get the pre-composited square. Run headlessly:
    //   Unity -batchmode -nographics -buildTarget Android -projectPath unity \
    //     -executeMethod XrealAR.EditorTools.IconSetup.Apply -quit -logFile -
    public static class IconSetup
    {
        const string Fg = "Assets/Icons/ic_foreground.png";
        const string Bg = "Assets/Icons/ic_background.png";
        const string Legacy = "Assets/Icons/ic_legacy.png";

        public static void Apply()
        {
            var fg = Load(Fg);
            var bg = Load(Bg);
            var legacy = Load(Legacy);

            const BuildTargetGroup g = BuildTargetGroup.Android;
            foreach (var kind in PlayerSettings.GetSupportedIconKindsForPlatform(g))
            {
                var icons = PlayerSettings.GetPlatformIcons(g, kind);
                foreach (var icon in icons)
                {
                    if (icon.maxLayerCount >= 2) icon.SetTextures(bg, fg); // adaptive: bg layer, fg layer
                    else icon.SetTextures(legacy);
                }
                PlayerSettings.SetPlatformIcons(g, kind, icons);
                Debug.Log($"[IconSetup] {kind} -> {icons.Length} slots set");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[IconSetup] Android icons assigned.");
        }

        static Texture2D Load(string path)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null) throw new System.IO.FileNotFoundException($"[IconSetup] icon texture not found: {path}");
            return t;
        }
    }
}
