// UI/Widgets.cs — 自绘控件库（纯色块 + 文本，避免九宫格纹理在不同 DPI / GUI 状态下失真）
using System;
using System.Collections.Generic;
using UnityEngine;
using Moon.Core;

namespace Moon.UI
{
    internal enum BtnKind
    {
        Ghost,
        Primary,
        Danger,
        Flat
    }

    internal static class Widgets
    {
        private static readonly Dictionary<string, float> _anim = new Dictionary<string, float>();

        private static Texture2D White
        {
            get { return Texture2D.whiteTexture; }
        }

        // ---------- 基础绘制 ----------

        internal static void Fill(Rect r, Color c)
        {
            if (c.a <= 0.002f) return;
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, White);
            GUI.color = prev;
        }

        /// <summary>圆角填充：中间矩形 + 四角贴图，任意尺寸都不会拉伸变形。</summary>
        internal static void RoundedFill(Rect r, Color c, int radius)
        {
            if (c.a <= 0.002f) return;
            float s = Theme.UiScale;
            float rr = Mathf.Min(radius * s, Mathf.Min(r.width, r.height) * 0.5f);
            if (rr < 1.5f)
            {
                Fill(r, c);
                return;
            }

            // 中段 + 四角（角块画大 1px 盖住接缝，避免出现 1px 的深色细线）
            Fill(new Rect(r.x + rr, r.y, r.width - 2f * rr, r.height), c);
            Fill(new Rect(r.x, r.y + rr, r.width, r.height - 2f * rr), c);

            float corner = rr + 1f;
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, corner, corner), Theme.CornerTexture(0));
            GUI.DrawTexture(new Rect(r.x + r.width - corner, r.y, corner, corner), Theme.CornerTexture(1));
            GUI.DrawTexture(new Rect(r.x, r.y + r.height - corner, corner, corner), Theme.CornerTexture(2));
            GUI.DrawTexture(new Rect(r.x + r.width - corner, r.y + r.height - corner, corner, corner), Theme.CornerTexture(3));
            GUI.color = prev;
        }

        /// <summary>窗体外框：1px 描边，不再画偏移阴影（阴影会在圆角外侧留下黑边）。</summary>
        internal static void Frame(Rect r, Color fill, int radius)
        {
            RoundedFill(r, fill, radius);
        }

        internal static void Shadow(Rect r, int radius)
        {
            // 不再绘制阴影：偏移阴影在圆角外会留下难看的深色边
        }

        internal static void Box(Rect r, Color fill, int radius)
        {
            RoundedFill(r, fill, radius);
        }

        internal static void Bordered(Rect r, Color fill, Color border, int radius)
        {
            RoundedFill(r, border, radius);
            float s = Theme.UiScale;
            RoundedFill(new Rect(r.x + s, r.y + s, r.width - 2f * s, r.height - 2f * s), fill, Mathf.Max(2, radius - 1));
        }

        internal static void VLine(Rect r, Color c)
        {
            Fill(r, c);
        }

        internal static void Divider(Rect r)
        {
            Fill(new Rect(r.x, Mathf.Round(r.y), r.width, 1f), Theme.Line);
        }

        internal static void Circle(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Theme.CircleTexture(Color.white));
            GUI.color = prev;
        }

        internal static void Lbl(Rect r, string s, int size, Color c, TextAnchor a = TextAnchor.MiddleLeft, bool bold = false)
        {
            if (string.IsNullOrEmpty(s)) return;
            GUI.Label(r, s, Theme.Label(size, c, a, bold));
        }

        internal static void LblWrap(Rect r, string s, int size, Color c, bool bold = false)
        {
            if (string.IsNullOrEmpty(s)) return;
            GUI.Label(r, s, Theme.Label(size, c, TextAnchor.UpperLeft, bold, true));
        }

        internal static float TextHeight(string s, int size, float width, bool bold = false)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            return Theme.Label(size, Theme.Text, TextAnchor.UpperLeft, bold, true).CalcHeight(new GUIContent(s), width);
        }

        internal static bool Hover(Rect r)
        {
            Event e = Event.current;
            return e != null && r.Contains(e.mousePosition);
        }

        // ---------- 标题 / 区块 ----------

        internal static void PageTitle(Rect r, string title, string desc)
        {
            Lbl(new Rect(r.x, r.y, r.width, 26f * Theme.UiScale), title, 19, Theme.Text, TextAnchor.MiddleLeft, true);
            if (!string.IsNullOrEmpty(desc))
            {
                Lbl(new Rect(r.x, r.y + 25f * Theme.UiScale, r.width, 18f * Theme.UiScale), desc, 12, Theme.Dim);
            }
        }

        internal static float SectionHeader(Rect r, string title, string desc, Color accent)
        {
            float s = Theme.UiScale;
            Fill(new Rect(r.x, r.y + 7f * s, 5f * s, 10f * s), accent);
            Lbl(new Rect(r.x + 14f * s, r.y, r.width - 14f * s, 22f * s), title, 14, Theme.Text, TextAnchor.MiddleLeft, true);
            float used = 24f * s;
            if (!string.IsNullOrEmpty(desc))
            {
                float h = TextHeight(desc, 12, r.width - 14f * s);
                LblWrap(new Rect(r.x + 14f * s, r.y + used, r.width - 14f * s, h), desc, 12, Theme.Faint);
                used += h + 2f * s;
            }
            return used;
        }

        // ---------- 按钮 ----------

        internal static bool Btn(Rect r, string label, int size = 13, BtnKind kind = BtnKind.Ghost, bool enabled = true, string tip = null)
        {
            bool hover = enabled && Hover(r);
            Color bg, fg;

            switch (kind)
            {
                case BtnKind.Primary:
                    bg = hover ? Theme.Mix(Theme.Accent, Color.white, 0.14f) : Theme.Accent;
                    fg = Theme.OnAccent;
                    break;
                case BtnKind.Danger:
                    bg = hover ? Theme.Alpha(Theme.Bad, 0.34f) : Theme.Alpha(Theme.Bad, 0.20f);
                    fg = Theme.Bad;
                    break;
                case BtnKind.Flat:
                    bg = hover ? Theme.Alpha(Theme.Text, 0.10f) : Theme.Alpha(Theme.Text, 0f);
                    fg = Theme.Dim;
                    break;
                default:
                    bg = hover ? Theme.Alpha(Theme.Accent, 0.26f) : Theme.PanelAlt;
                    fg = Theme.Text;
                    break;
            }

            if (!enabled)
            {
                bg = Theme.Alpha(Theme.Text, 0.04f);
                fg = Theme.Faint;
            }

            RoundedFill(r, bg, 9);
            Lbl(r, label, size, fg, TextAnchor.MiddleCenter, kind == BtnKind.Primary);

            if (!enabled) return false;
            return GUI.Button(r, tip == null ? GUIContent.none : new GUIContent(string.Empty, tip), GUIStyle.none);
        }

        internal static bool ChipButton(Rect r, string label, bool active, string tip = null)
        {
            bool hover = Hover(r);
            Color bg = active
                ? (hover ? Theme.Mix(Theme.Accent, Color.white, 0.14f) : Theme.Accent)
                : (hover ? Theme.Alpha(Theme.Accent, 0.22f) : Theme.PanelAlt);
            Color fg = active ? Theme.OnAccent : (hover ? Theme.Text : Theme.Dim);

            RoundedFill(r, bg, 8);
            Lbl(r, label, 12, fg, TextAnchor.MiddleCenter, active);
            return GUI.Button(r, tip == null ? GUIContent.none : new GUIContent(string.Empty, tip), GUIStyle.none);
        }

        internal static int Segmented(Rect r, string[] items, int index, int size = 12)
        {
            float s = Theme.UiScale;
            float gap = 4f * s;
            float w = (r.width - gap * (items.Length - 1)) / items.Length;
            int result = index;
            for (int i = 0; i < items.Length; i++)
            {
                Rect cell = new Rect(r.x + i * (w + gap), r.y, w, r.height);
                if (ChipButton(cell, items[i], i == index)) result = i;
            }
            return result;
        }

        // ---------- 开关 ----------

        internal static bool Switch(Rect r, string id, bool value)
        {
            float s = Theme.UiScale;
            Rect box = new Rect(r.x, r.y + (r.height - 22f * s) * 0.5f, 42f * s, 22f * s);

            bool toggled = GUI.Button(box, GUIContent.none, GUIStyle.none);

            float target = value ? 1f : 0f;
            float cur;
            if (!_anim.TryGetValue(id, out cur)) cur = target;
            cur = Mathf.Lerp(cur, target, Mathf.Clamp01(Time.unscaledDeltaTime * 18f));
            _anim[id] = cur;

            Color track = value ? Theme.Accent : Theme.Alpha(Theme.Text, Theme.LightMode ? 0.20f : 0.18f);
            if (!value) track = Theme.Mix(track, Theme.Accent, cur * 0.7f);
            RoundedFill(box, track, 11);

            float knob = 16f * s;
            float travel = box.width - knob - 6f * s;
            float kx = box.x + 3f * s + travel * cur;
            Circle(new Rect(kx, box.y + 3f * s, knob, knob),
                value ? Color.white : Theme.Mix(Theme.Text, Theme.Panel, 0.2f));

            return toggled ? !value : value;
        }

        internal static bool ToggleRow(Rect r, string id, string title, string desc, bool value, string tip = null)
        {
            float s = Theme.UiScale;
            bool hover = Hover(r);
            if (hover)
            {
                Fill(new Rect(r.x - 6f * s, r.y, r.width + 12f * s, r.height), Theme.Alpha(Theme.Text, 0.04f));
            }

            float textW = r.width - 60f * s;
            bool twoLine = !string.IsNullOrEmpty(desc);
            float titleH = 24f * s;
            float top = r.y + (r.height - (twoLine ? 46f * s : titleH)) * 0.5f;
            Lbl(new Rect(r.x, top, textW, titleH), title, 13, Theme.Text);
            if (twoLine)
            {
                Lbl(new Rect(r.x, top + titleH - 2f * s, textW, 20f * s), desc, 11, Theme.Faint);
            }

            bool result = Switch(new Rect(r.x + r.width - 42f * s, r.y, 42f * s, r.height), id, value);
            if (tip != null && hover) GUI.tooltip = tip;

            if (Event.current != null && Event.current.type == EventType.MouseDown && hover)
            {
                if (Event.current.mousePosition.x < r.x + r.width - 52f * s)
                {
                    result = !value;
                    Event.current.Use();
                }
            }
            return result;
        }

        // ---------- 滑条 ----------

        private static string _dragSlider;

        internal static void ResetDrag()
        {
            _dragSlider = null;
        }

        internal static float Slider(Rect r, float value, float min, float max, string id = "slider")
        {
            float s = Theme.UiScale;
            Rect area = new Rect(r.x, r.y, r.width, r.height);
            Rect track = new Rect(area.x, area.y + area.height * 0.5f - 2f * s, area.width, 4f * s);

            float t = Mathf.Clamp01((value - min) / Mathf.Max(1e-6f, max - min));
            Fill(track, Theme.Alpha(Theme.Text, Theme.LightMode ? 0.16f : 0.14f));
            Fill(new Rect(track.x, track.y, track.width * t, track.height), Theme.Accent);
            Circle(new Rect(track.x + track.width * t - 7f * s, track.y - 5f * s, 14f * s, 14f * s), Color.white);

            Event e = Event.current;
            if (e == null) return value;

            Rect hit = new Rect(area.x - 6f * s, area.y - 4f * s, area.width + 12f * s, area.height + 8f * s);
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0 && hit.Contains(e.mousePosition))
                    {
                        _dragSlider = id;
                        e.Use();
                        return ValueAt(e.mousePosition.x, track, min, max);
                    }
                    break;
                case EventType.MouseDrag:
                    if (_dragSlider == id)
                    {
                        e.Use();
                        return ValueAt(e.mousePosition.x, track, min, max);
                    }
                    break;
                case EventType.MouseUp:
                    if (_dragSlider == id)
                    {
                        _dragSlider = null;
                        e.Use();
                        return ValueAt(e.mousePosition.x, track, min, max);
                    }
                    break;
            }
            return value;
        }

        private static float ValueAt(float mouseX, Rect track, float min, float max)
        {
            float t = Mathf.Clamp01((mouseX - track.x) / Mathf.Max(1f, track.width));
            float v = min + (max - min) * t;
            return Mathf.Round(v * 1000f) / 1000f;
        }

        // ---------- 文本输入 ----------

        internal static string TextField(Rect r, string value, int size = 13)
        {
            RoundedFill(r, Theme.Alpha(Theme.Accent, 0.45f), 8);
            RoundedFill(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), Theme.Field, 7);
            GUIStyle st = Theme.Label(size, Theme.Text, TextAnchor.MiddleLeft);
            Rect inner = new Rect(r.x + 10f * Theme.UiScale, r.y + 2f, r.width - 16f * Theme.UiScale, r.height - 4f);
            return GUI.TextField(inner, value ?? string.Empty, 64, st);
        }

        // ---------- 进度条 ----------

        internal static void Bar(Rect r, float t, Color color, Color? back = null)
        {
            Fill(r, back ?? Theme.Alpha(Theme.Text, Theme.LightMode ? 0.12f : 0.10f));
            float w = Mathf.Clamp01(t) * r.width;
            if (w > 1f) Fill(new Rect(r.x, r.y, w, r.height), color);
        }

        internal static void StatChip(Rect r, string label, string value, Color accent)
        {
            float s = Theme.UiScale;
            Bordered(r, Theme.PanelAlt, Theme.Line, 8);
            RoundedFill(new Rect(r.x + 2f * s, r.y + 5f * s, 3f * s, r.height - 10f * s), accent, 2);
            Lbl(new Rect(r.x + 12f * s, r.y + 4f * s, r.width - 18f * s, 18f * s), label, 9, Theme.Faint);
            Lbl(new Rect(r.x + 12f * s, r.y + 22f * s, r.width - 18f * s, 24f * s), value, 13, Theme.Text, TextAnchor.MiddleLeft, true);
        }

        // ---------- 提示气泡 ----------

        internal static void Tooltip()
        {
            if (!Cfg.B("show_tooltips", true)) return;
            string tip = GUI.tooltip;
            if (string.IsNullOrEmpty(tip)) return;

            float s = Theme.UiScale;
            GUIStyle st = Theme.Label(12, Theme.Text, TextAnchor.UpperLeft, false, true);
            float maxW = 320f * s;
            float w = Mathf.Min(st.CalcSize(new GUIContent(tip)).x + 22f * s, maxW);
            float h = st.CalcHeight(new GUIContent(tip), w - 22f * s) + 16f * s;

            Vector2 m = Event.current.mousePosition;
            float x = Mathf.Min(m.x + 16f * s, Screen.width - w - 8f);
            float y = Mathf.Min(m.y + 20f * s, Screen.height - h - 8f);
            Rect r = new Rect(x, y, w, h);
            Frame(r, Theme.Panel, 8);
            GUI.Label(new Rect(r.x + 11f * s, r.y + 8f * s, r.width - 22f * s, r.height - 16f * s), tip, st);
        }
    }
}
