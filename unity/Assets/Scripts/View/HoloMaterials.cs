using UnityEngine;
using UnityEngine.Rendering;
using Dejarik;

namespace Dejarik.View
{
    // Holographic materials, ported in spirit from the web game's applyHoloMaterial + roleColors. Built-in
    // (Standard) shader so it survives shader-stripping (Standard is in Always Included). Emissive so the
    // creatures and cells glow on the XREAL see-through optics (black background renders transparent).
    public static class HoloMaterials
    {
        public static readonly Color P0 = Hex("#38e1ff");
        public static readonly Color P1 = Hex("#ff8a3c");

        public static Color Hex(string s) => ColorUtility.TryParseHtmlString(s, out var c) ? c : Color.magenta;

        public static Color HoloFor(Player owner) => owner == Player.P0 ? P0 : P1;

        // Tinted emissive hologram for a creature mesh. Keeps the source diffuse/normal maps, tints albedo
        // toward white, and uses the diffuse as the emission map glowing in the player's holo color.
        public static Material Creature(Texture mainTex, Texture normalMap, Player owner)
        {
            var holo = HoloFor(owner);
            var mat = new Material(Shader.Find("Standard"));
            mat.SetFloat("_Glossiness", 0.2f);
            mat.SetFloat("_Metallic", 0f);
            if (mainTex != null) mat.SetTexture("_MainTex", mainTex);
            if (normalMap != null) { mat.SetTexture("_BumpMap", normalMap); mat.EnableKeyword("_NORMALMAP"); }
            mat.color = Color.Lerp(holo, Color.white, 0.55f);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (mainTex != null) mat.SetTexture("_EmissionMap", mainTex);
            mat.SetColor("_EmissionColor", holo * 0.9f);
            SetFade(mat, 0.92f);
            return mat;
        }

        // Flat glowing board cell for the given role. Unlit/Color so it shows as a pure bright color on the
        // see-through optics regardless of scene lighting or emissive shader variants (a Standard emissive
        // cell read as black on-device). Brightness differences make the grid pattern readable.
        public static Material Cell(CellRole role)
        {
            // Dim blue base grid so the vivid move (cyan) / attack (orange) / push (purple) highlights pop.
            Color c = role switch
            {
                CellRole.Move     => Color.Lerp(P0, Color.white, 0.15f),
                CellRole.Attack   => P1,
                CellRole.Push     => Hex("#c08cff"),
                CellRole.Selected => Color.Lerp(P0, Color.white, 0.35f),
                CellRole.Center   => Hex("#1f5176"),
                CellRole.Light    => Hex("#1b4663"),
                _                 => Hex("#143247"),
            };
            return Unlit(c);
        }

        public static Material RimGlow() => Unlit(Color.Lerp(P0, Color.white, 0.25f));

        static Material Unlit(Color c)
        {
            var mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = c;
            return mat;
        }

        public static Material BoardTable()
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.color = Hex("#0b1622");
            mat.SetFloat("_Glossiness", 0.25f);
            mat.SetFloat("_Metallic", 0.25f);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", Hex("#0a2233") * 0.15f);
            return mat;
        }

        // Switch a Standard material to transparent (Fade) blending at the given alpha.
        static void SetFade(Material m, float alpha)
        {
            var c = m.color; c.a = alpha; m.color = c;
            m.SetFloat("_Mode", 2);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
