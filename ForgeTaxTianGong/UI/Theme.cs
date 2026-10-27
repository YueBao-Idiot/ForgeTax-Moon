// UI/Theme.cs — 视觉系统：配色 / 圆角九宫格纹理 / 字体 / 样式工厂 / 滚动条皮肤
using System;
using System.Collections.Generic;
using Moon.Core;
using UnityEngine;

namespace Moon.UI
{
    internal static class Theme
    {
        internal static readonly string[] AccentNames =
        {
            "赤金", "熔红", "铜绿", "霜蓝", "紫晶", "青碧"
        };

        internal static readonly string[] AccentNamesEn =
        {
            "Amber", "Ember", "Verdigris", "Frost", "Amethyst", "Teal"
        };

        private static readonly string[] AccentHex =
        {
            "E7B04A", "E2605A", "4FB79F", "5E96E8", "A47CE8", "58B8C8"
        };

        private static readonly Color[] Accents = BuildAccents();

        private static Color[] BuildAccents()
        {
            Color[] arr = new Color[AccentHex.Length];
            for (int i = 0; i < AccentHex.Length; i++) arr[i] = Hex(AccentHex[i]);
            return arr;
        }

        // ---------- 设置读写 ----------

        internal static int AccentIndex
        {
            get { return Mathf.Clamp(Cfg.I("accent", 0), 0, Accents.Length - 1); }
        }

        internal static bool LightMode
        {
            get { return Cfg.B("light_theme", false); }
        }

        /// <summary>自动适配屏幕高度（2880x1800 之类的屏幕上界面不会小得看不清）。</summary>
        internal static bool AutoScale
        {
            get { return Cfg.B("ui_scale_auto", true); }
        }

        internal static float UiScale
        {
            get
            {
                if (AutoScale) return Mathf.Clamp(Screen.height / 1080f, 1f, 1.8f);
                return Mathf.Clamp(Cfg.F("ui_scale", 1f), 0.7f, 2.2f);
            }
        }

        internal static float UiAlpha
        {
            get { return Mathf.Clamp(Cfg.F("ui_alpha", 0.98f), 0.75f, 1f); }
        }

        // ---------- 配色 ----------

        internal static Color Accent
        {
            get { return Accents[AccentIndex]; }
        }

        internal static Color AccentAt(int index)
        {
            return Accents[Mathf.Clamp(index, 0, Accents.Length - 1)];
        }

        internal static Color Hex(string hex)
        {
            Color c;
            if (ColorUtility.TryParseHtmlString("#" + hex, out c)) return c;
            return Color.magenta;
        }

        internal static Color Bg
        {
            get { return LightMode ? Hex("F2F4F7") : Hex("0B0D12"); }
        }

        internal static Color BgSolid
        {
            get { return LightMode ? Hex("F2F4F7") : Hex("0B0D12"); }
        }

        internal static Color Panel
        {
            get { return LightMode ? Hex("FFFFFF") : Hex("10131A"); }
        }

        internal static Color PanelAlt
        {
            get { return LightMode ? Hex("F8F9FB") : Hex("1C2230"); }
        }

        internal static Color Field
        {
            get { return LightMode ? Hex("FFFFFF") : Hex("0F1218"); }
        }

        internal static Color Line
        {
            get { return LightMode ? Hex("DDE2EA") : Hex("3B455A"); }
        }

        internal static Color Text
        {
            get { return LightMode ? Hex("14171E") : Hex("FFFFFF"); }
        }

        internal static Color Dim
        {
            get { return LightMode ? Hex("5D6675") : Hex("C7D0DE"); }
        }

        internal static Color Faint
        {
            get { return LightMode ? Hex("8B94A3") : Hex("98A2B3"); }
        }

        internal static Color Ok
        {
            get { return Hex("4FC08D"); }
        }

        internal static Color Warn
        {
            get { return Hex("E7B04A"); }
        }

        internal static Color Bad
        {
            get { return Hex("E0645C"); }
        }

        internal static Color OnAccent
        {
            get
            {
                Color a = Accent;
                float lum = 0.299f * a.r + 0.587f * a.g + 0.114f * a.b;
                return lum > 0.62f ? Hex("171A20") : Color.white;
            }
        }

        internal static Color AccentSoft
        {
            get { return Alpha(Accent, LightMode ? 0.14f : 0.16f); }
        }

        internal static Color AccentHover
        {
            get { return Alpha(Accent, 0.24f); }
        }

        internal static Color Alpha(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }

        internal static Color Scaled(Color c, float k)
        {
            return new Color(c.r * k, c.g * k, c.b * k, c.a);
        }

        internal static Color Mix(Color a, Color b, float t)
        {
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        }

        internal static float FontBoost
        {
            get { return Mathf.Clamp(Cfg.F("font_boost", 0f), 0f, 4f); }
        }

        /// <summary>字号按比例缩放（不叠加固定像素），避免高分辨率下文字比排版长得快而互相压住。</summary>
        internal static int FontPx(float size)
        {
            return Mathf.RoundToInt(size * UiScale * 1.22f) + Mathf.RoundToInt(FontBoost * UiScale);
        }

        internal static int Px(float v)
        {
            return Mathf.RoundToInt(v * UiScale);
        }

        internal static float S(float v)
        {
            return v * UiScale;
        }

        // ---------- 主题版本 ----------

        internal static int Version { get; private set; } = 1;
        private static readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();

        internal static void Invalidate()
        {
            Version++;
            _styles.Clear();
            _tex.Clear();
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] != null) UnityEngine.Object.Destroy(_owned[i]);
            }
            _owned.Clear();
            _skin = null;
            _skinVersion = -1;
            _font = null;
        }

        // ---------- 字体 ----------

        private static Font _font;

        internal static Font Font
        {
            get
            {
                if (_font == null) _font = LoadFont();
                return _font;
            }
        }

        internal const string PreferredFont = "Microsoft YaHei";

        private static Font LoadFont()
        {
            // 1) 用户指定字体（已安装到系统字体目录）
            try
            {
                string wanted = Cfg.S("font_name", PreferredFont);
                if (wanted != "-" && !string.IsNullOrEmpty(wanted))
                {
                    Font os = Font.CreateDynamicFontFromOSFont(new[] { wanted, "Microsoft YaHei UI", "Microsoft YaHei" }, 20);
                    if (os != null && os.HasCharacter('月')) return os;
                }
            }
            catch (Exception) { }

            // 2) 游戏自带 Noto CJK
            try
            {
                Font f = Resources.Load<Font>("Forge/Fonts/NotoSansCJKsc-Regular");
                if (f != null) return f;
            }
            catch (Exception) { }
            try
            {
                Font f = ForgeLocalization.CurrentFont;
                if (f != null) return f;
            }
            catch (Exception) { }
            try
            {
                Font f = Font.CreateDynamicFontFromOSFont(new[]
                {
                    "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", "SimHei", "Arial Unicode MS"
                }, 16);
                if (f != null) return f;
            }
            catch (Exception) { }
            return GUI.skin.font;
        }

        // ---------- 纹理 ----------

        private static readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, GUIStyle> _styles = new Dictionary<string, GUIStyle>();

        internal static Texture2D RoundedTexture(Color color, int radius)
        {
            int r = Mathf.Clamp(radius, 2, 24);
            string key = "r" + r + ":" + ColorKey(color);
            Texture2D cached;
            if (_tex.TryGetValue(key, out cached) && cached != null) return cached;

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[size * size];
            Vector2 half = new Vector2(size * 0.5f, size * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = RoundAlpha(x + 0.5f, y + 0.5f, half, r);
                    px[y * size + x] = new Color(color.r, color.g, color.b, color.a * a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            _tex[key] = tex;
            _owned.Add(tex);
            return tex;
        }

        private static float RoundAlpha(float x, float y, Vector2 half, float radius)
        {
            float qx = Mathf.Abs(x) - half.x + radius;
            float qy = Mathf.Abs(y) - half.y + radius;
            float mx = Mathf.Max(qx, 0f);
            float my = Mathf.Max(qy, 0f);
            float dist = Mathf.Sqrt(mx * mx + my * my) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
            return Mathf.Clamp01(0.5f - dist);
        }

        internal static Texture2D CircleTexture(Color color)
        {
            string key = "c:" + ColorKey(color);
            Texture2D cached;
            if (_tex.TryGetValue(key, out cached) && cached != null) return cached;

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.5f - 1.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = (new Vector2(x + 0.5f, y + 0.5f) - center).magnitude - radius;
                    float a = Mathf.Clamp01(0.5f - d);
                    px[y * size + x] = new Color(color.r, color.g, color.b, color.a * a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            _tex[key] = tex;
            _owned.Add(tex);
            return tex;
        }

        internal static Texture2D GradientTexture(Color top, Color bottom)
        {
            string key = "g:" + ColorKey(top) + ":" + ColorKey(bottom);
            Texture2D cached;
            if (_tex.TryGetValue(key, out cached) && cached != null) return cached;

            const int h = 64;
            Texture2D tex = new Texture2D(2, h, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[2 * h];
            for (int y = 0; y < h; y++)
            {
                Color c = Color.Lerp(bottom, top, y / (float)(h - 1));
                px[y * 2] = c;
                px[y * 2 + 1] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            _tex[key] = tex;
            _owned.Add(tex);
            return tex;
        }

        private static string ColorKey(Color c)
        {
            return Mathf.RoundToInt(c.r * 255f) + "_" + Mathf.RoundToInt(c.g * 255f) + "_" +
                   Mathf.RoundToInt(c.b * 255f) + "_" + Mathf.RoundToInt(c.a * 255f);
        }

        // ---------- 样式工厂 ----------

        internal static GUIStyle Label(int size, Color color, TextAnchor anchor, bool bold = false, bool wrap = false)
        {
            string key = "l" + Version + "|" + size + "|" + ColorKey(color) + "|" + (int)anchor + "|" + bold + "|" + wrap;
            GUIStyle st;
            if (_styles.TryGetValue(key, out st) && st != null) return st;

            st = new GUIStyle(GUI.skin.label);
            st.font = Font;
            st.fontSize = FontPx(size);
            st.alignment = anchor;
            st.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            st.wordWrap = wrap;
            st.richText = true;
            st.clipping = TextClipping.Clip;
            st.padding = new RectOffset(0, 0, 0, 0);
            st.margin = new RectOffset(0, 0, 0, 0);
            st.normal.textColor = color;
            st.hover.textColor = color;
            st.active.textColor = color;
            st.focused.textColor = color;
            _styles[key] = st;
            return st;
        }

        internal static GUIStyle Round(Color fill, int radius)
        {
            string key = "rd" + Version + "|" + ColorKey(fill) + "|" + radius;
            GUIStyle st;
            if (_styles.TryGetValue(key, out st) && st != null) return st;

            Texture2D tex = RoundedTexture(fill, radius);
            st = new GUIStyle();
            st.normal.background = tex;
            st.hover.background = tex;
            st.active.background = tex;
            st.focused.background = tex;
            st.border = new RectOffset(radius + 2, radius + 2, radius + 2, radius + 2);
            st.padding = new RectOffset(0, 0, 0, 0);
            st.margin = new RectOffset(0, 0, 0, 0);
            st.overflow = new RectOffset(0, 0, 0, 0);
            _styles[key] = st;
            return st;
        }

        internal static GUIStyle Circle(Color fill)
        {
            string key = "ci" + Version + "|" + ColorKey(fill);
            GUIStyle st;
            if (_styles.TryGetValue(key, out st) && st != null) return st;

            Texture2D tex = CircleTexture(fill);
            st = new GUIStyle();
            st.normal.background = tex;
            st.hover.background = tex;
            st.active.background = tex;
            st.border = new RectOffset(0, 0, 0, 0);
            st.padding = new RectOffset(0, 0, 0, 0);
            _styles[key] = st;
            return st;
        }

        /// <summary>按钮样式：hover / active 用不同底色。</summary>
        internal static GUIStyle ButtonStyle(Color normal, Color hover, Color active, Color textColor, int radius, int fontSize, bool bold = false)
        {
            string key = "bt" + Version + "|" + ColorKey(normal) + "|" + ColorKey(hover) + "|" + ColorKey(active) +
                         "|" + ColorKey(textColor) + "|" + radius + "|" + fontSize + "|" + bold;
            GUIStyle st;
            if (_styles.TryGetValue(key, out st) && st != null) return st;

            st = new GUIStyle(GUI.skin.button);
            st.font = Font;
            st.fontSize = FontPx(fontSize);
            st.alignment = TextAnchor.MiddleCenter;
            st.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            st.richText = true;
            st.clipping = TextClipping.Clip;
            st.padding = new RectOffset(Px(6), Px(6), 0, 0);
            st.margin = new RectOffset(0, 0, 0, 0);
            st.border = new RectOffset(radius + 2, radius + 2, radius + 2, radius + 2);
            st.normal.background = RoundedTexture(normal, radius);
            st.hover.background = RoundedTexture(hover, radius);
            st.active.background = RoundedTexture(active, radius);
            st.focused.background = st.normal.background;
            st.normal.textColor = textColor;
            st.hover.textColor = textColor;
            st.active.textColor = textColor;
            st.focused.textColor = textColor;
            _styles[key] = st;
            return st;
        }

        internal static GUIStyle TextFieldStyle(int fontSize)
        {
            string key = "tf" + Version + "|" + fontSize;
            GUIStyle st;
            if (_styles.TryGetValue(key, out st) && st != null) return st;

            st = new GUIStyle(GUI.skin.textField);
            st.font = Font;
            st.fontSize = FontPx(fontSize);
            st.alignment = TextAnchor.MiddleLeft;
            st.richText = false;
            st.clipping = TextClipping.Clip;
            st.padding = new RectOffset(Px(10), Px(8), 0, 0);
            st.margin = new RectOffset(0, 0, 0, 0);
            st.border = new RectOffset(10, 10, 10, 10);
            st.normal.background = RoundedTexture(Field, 8);
            st.focused.background = RoundedTexture(Field, 8);
            st.hover.background = RoundedTexture(Field, 8);
            st.active.background = RoundedTexture(Field, 8);
            st.normal.textColor = Text;
            st.hover.textColor = Text;
            st.active.textColor = Text;
            st.focused.textColor = Text;
            _styles[key] = st;
            return st;
        }

        // ---------- 圆角角块（白色 + alpha，绘制时用 GUI.color 染色） ----------

        private static readonly Texture2D[] _corners = new Texture2D[4];

        /// <summary>corner: 0 左上 / 1 右上 / 2 左下 / 3 右下。拉伸到任意半径都不会变形。</summary>
        internal static Texture2D CornerTexture(int corner)
        {
            if (_corners[corner] != null) return _corners[corner];

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[size * size];
            Vector2 center = new Vector2(corner == 0 || corner == 2 ? size : 0f, corner < 2 ? 0f : size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = (new Vector2(x + 0.5f, y + 0.5f) - center).magnitude - size;
                    float a = Mathf.Clamp01(0.5f - d);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            _corners[corner] = tex;
            return tex;
        }
        // ---------- 皮肤 ----------

        private static GUISkin _skin;
        private static int _skinVersion = -1;

        internal static GUISkin Skin
        {
            get
            {
                if (_skin == null || _skinVersion != Version)
                {
                    _skin = BuildSkin();
                    _skinVersion = Version;
                }
                return _skin;
            }
        }

        private static GUISkin BuildSkin()
        {
            GUISkin skin = UnityEngine.Object.Instantiate(GUI.skin);
            skin.hideFlags = HideFlags.HideAndDontSave;

            skin.label = Label(13, Text, TextAnchor.MiddleLeft);
            GUIStyle flat = new GUIStyle();
            flat.normal.background = null;
            flat.hover.background = null;
            flat.active.background = null;
            skin.box = flat;

            // 滚动条：细轨道 + 圆角滑块，隐藏箭头
            int sw = Px(6);
            GUIStyle track = new GUIStyle();
            track.fixedWidth = sw;
            track.border = new RectOffset(0, 0, 0, 0);
            track.margin = new RectOffset(Px(2), Px(2), 0, 0);
            track.normal.background = Texture2D.whiteTexture;
            track.hover.background = Texture2D.whiteTexture;
            track.active.background = Texture2D.whiteTexture;
            track.normal.textColor = Alpha(Text, 0.10f);
            skin.verticalScrollbar = track;

            GUIStyle thumb = new GUIStyle();
            thumb.fixedWidth = sw;
            thumb.border = new RectOffset(0, 0, 0, 0);
            thumb.normal.background = Texture2D.whiteTexture;
            thumb.hover.background = Texture2D.whiteTexture;
            thumb.active.background = Texture2D.whiteTexture;
            thumb.normal.textColor = Alpha(Accent, 0.7f);
            thumb.hover.textColor = Alpha(Accent, 0.9f);
            skin.verticalScrollbarThumb = thumb;

            skin.verticalScrollbarUpButton.fixedWidth = 0f;
            skin.verticalScrollbarUpButton.fixedHeight = 0f;
            skin.verticalScrollbarDownButton.fixedWidth = 0f;
            skin.verticalScrollbarDownButton.fixedHeight = 0f;

            skin.scrollView.normal.background = null;
            skin.scrollView.hover.background = null;
            skin.scrollView.active.background = null;
            skin.scrollView.padding = new RectOffset(0, 0, 0, 0);
            skin.scrollView.margin = new RectOffset(0, 0, 0, 0);

            skin.window.normal.background = null;
            skin.window.onNormal.background = null;
            return skin;
        }
    }
}
