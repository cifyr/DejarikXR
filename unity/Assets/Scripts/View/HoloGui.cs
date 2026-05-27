using UnityEngine;

namespace Dejarik.View
{
    // Holographic IMGUI skin for the Beam Pro phone overlay, matching the web app's vibe: deep-space panel,
    // cyan/amber glow, monospace, translucent glowing buttons that bloom on press. Textures/styles are built
    // once and reused. Colors mirror globals.css (--background #03060d, --holo-p0 #38e1ff, --holo-p1 #ff8a3c).
    public static class HoloGui
    {
        public static readonly Color Bg = HoloMaterials.Hex("#03060d");
        public static readonly Color Cyan = HoloMaterials.Hex("#38e1ff");
        public static readonly Color Amber = HoloMaterials.Hex("#ff8a3c");
        public static readonly Color Foreground = HoloMaterials.Hex("#d6f3ff");

        static Texture2D _panelTex, _btnTex, _btnHoverTex, _dividerTex, _bgTex;
        static bool _built;

        // The OS-monospace font returns no glyphs on the Beam (buttons render as blank squares), so we leave
        // GUIStyle.font null and let Unity's reliable built-in font draw the text.
        public static Font Mono => null;

        public static void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            // Translucent dark space panel with a faint cyan edge (9-sliced).
            _panelTex = Bordered(new Color(0.04f, 0.086f, 0.149f, 0.55f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.5f));
            _btnTex = Bordered(new Color(Cyan.r, Cyan.g, Cyan.b, 0.12f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.6f));
            _btnHoverTex = Bordered(new Color(Cyan.r, Cyan.g, Cyan.b, 0.28f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.95f));
            _dividerTex = Solid(new Color(Cyan.r, Cyan.g, Cyan.b, 0.25f));
            _bgTex = VerticalGradient(HoloMaterials.Hex("#081628"), HoloMaterials.Hex("#03060d")); // deep space
        }

        // Opaque top->bottom gradient for the full-screen deck background.
        static Texture2D VerticalGradient(Color top, Color bottom)
        {
            const int h = 128;
            var t = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
                t.SetPixel(0, y, Color.Lerp(bottom, top, y / (float)(h - 1)));
            t.Apply();
            return t;
        }

        public static Texture2D BgTex { get { EnsureBuilt(); return _bgTex; } }

        static Texture2D _miniTex, _dotTex;
        public static Texture2D MinimapTex { get { if (_miniTex == null) _miniTex = BuildMinimap(); return _miniTex; } }
        public static Texture2D DotTex { get { if (_dotTex == null) _dotTex = BuildDot(); return _dotTex; } }

        // Top-down Dejarik board diagram: two concentric rings split into 12 sectors around a center circle.
        // Radii are BoardLayout's, normalized to the rim. Cyan lines on a faint fill, transparent outside.
        static Texture2D BuildMinimap()
        {
            const int s = 256;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float[] rings = { 0.175f, 0.206f, 0.515f, 0.536f, 0.918f, 1.0f }; // RCenter, Inner[], Outer[], Rim / Rim
            Color line = new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f);
            Color faint = new Color(Cyan.r, Cyan.g, Cyan.b, 0.10f);
            var px = new Color[s * s];
            float c = (s - 1) / 2f, R = s / 2f - 2f, lw = 1.6f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x - c, dy = y - c, d = Mathf.Sqrt(dx * dx + dy * dy) / R;
                    Color col = new Color(0, 0, 0, 0);
                    if (d <= 1.0f) col = faint;
                    foreach (var rr in rings) if (Mathf.Abs(d - rr) * R < lw) { col = line; break; }
                    if (d >= rings[1] - 0.01f && d <= 1.0f) // spokes only across the ringed band
                    {
                        float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, m = Mathf.Repeat(ang, 30f);
                        float half = lw / Mathf.Max(1f, d * R) * Mathf.Rad2Deg;
                        if (m < half || m > 30f - half) col = line;
                    }
                    px[y * s + x] = col;
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        static Texture2D BuildDot()
        {
            const int s = 32;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (s - 1) / 2f, r = s / 2f - 1f;
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    px[y * s + x] = new Color(1, 1, 1, Mathf.Clamp01(r - d));
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        // 9-slice texture: 2px glowing border over a translucent fill.
        static Texture2D Bordered(Color fill, Color border)
        {
            const int s = 16, b = 2;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    bool edge = x < b || x >= s - b || y < b || y >= s - b;
                    px[y * s + x] = edge ? border : fill;
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        public static GUIStyle Panel(int fontSize)
        {
            EnsureBuilt();
            return new GUIStyle
            {
                normal = { background = _panelTex },
                border = new RectOffset(4, 4, 4, 4),
                font = Mono,
                fontSize = fontSize,
            };
        }

        public static GUIStyle Button(int fontSize)
        {
            EnsureBuilt();
            var s = new GUIStyle
            {
                normal = { background = _btnTex, textColor = Cyan },
                hover = { background = _btnHoverTex, textColor = Color.white },
                active = { background = _btnHoverTex, textColor = Color.white },
                onNormal = { background = _btnHoverTex, textColor = Color.white },
                border = new RectOffset(4, 4, 4, 4),
                alignment = TextAnchor.MiddleCenter,
                font = Mono,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            return s;
        }

        public static GUIStyle Label(int fontSize, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle fs = FontStyle.Normal)
        {
            return new GUIStyle
            {
                normal = { textColor = color },
                alignment = anchor,
                font = Mono,
                fontSize = fontSize,
                fontStyle = fs,
                wordWrap = true,
                richText = true,   // allow <color> tags (dice line color-codes each team)
            };
        }

        public static Texture2D PanelTex { get { EnsureBuilt(); return _panelTex; } }
        public static Texture2D DividerTex { get { EnsureBuilt(); return _dividerTex; } }

        // A glow-styled title: draw the text a few times offset/faded to fake a soft bloom.
        public static void GlowLabel(Rect r, string text, GUIStyle style, Color glow, float strength = 0.5f)
        {
            var prev = GUI.color;
            GUI.color = new Color(glow.r, glow.g, glow.b, strength);
            foreach (var o in _glowOffsets)
                GUI.Label(new Rect(r.x + o.x, r.y + o.y, r.width, r.height), text, style);
            GUI.color = prev;
            GUI.Label(r, text, style);
        }

        static readonly Vector2[] _glowOffsets =
        {
            new Vector2(-1.5f, 0), new Vector2(1.5f, 0), new Vector2(0, -1.5f), new Vector2(0, 1.5f),
        };
    }
}
