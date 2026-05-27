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

        // Flat glowing board cell for the given role.
        public static Material Cell(CellRole role)
        {
            // Brighter than the web (which leaned on bloom): on see-through optics a dim cell reads as
            // black/transparent, so the base grid needs real emissive presence to be visible.
            (string color, Color emissive, float ei) spec = role switch
            {
                CellRole.Move     => ("#0a3a4a", P0, 1.4f),
                CellRole.Attack   => ("#4a1410", P1, 1.4f),
                CellRole.Push     => ("#2a1a4a", Hex("#b478ff"), 1.4f),
                CellRole.Selected => ("#0a3a4a", P0, 1.0f),
                CellRole.Center   => ("#123048", Hex("#2f7fb8"), 0.7f),
                CellRole.Light    => ("#163a55", Hex("#2f86c0"), 0.8f),
                _                 => ("#0e2c44", Hex("#246a9c"), 0.55f),
            };
            var mat = new Material(Shader.Find("Standard"));
            mat.color = Hex(spec.color);
            mat.SetFloat("_Glossiness", 0.1f);
            mat.SetFloat("_Metallic", 0f);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", spec.emissive * spec.ei);
            return mat;
        }

        public static Material RimGlow()
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.color = P0;
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", P0 * 1.1f);
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
