// UI/Hud.cs — 数据浮层（F2）：可拖动、记忆位置、五种结果全显示、无剑时不显示概率
using System;
using Moon.Core;
using Moon.Data;
using Moon.Features;
using UnityEngine;
using static Moon.UI.Widgets;
using static Moon.UI.Theme;

namespace Moon.UI
{
    internal static class Hud
    {
        private static bool _dragging;
        private static Vector2 _dragOffset;
        private static bool _posLoaded;
        private static float _x = 18f;
        private static float _y = 18f;

        private static void LoadPos()
        {
            if (_posLoaded) return;
            _posLoaded = true;
            _x = Cfg.F("hud_x", -1f);
            _y = Cfg.F("hud_y", 18f);
            if (_x < 0f) _x = 18f;
        }

        /// <summary>浮层里显示的剑必须和概率来自同一把剑（铁砧 → 手持），否则数字会张冠李戴。</summary>
        private static GPT6Sword Sword(GPT6ForgeRoom room)
        {
            if (room == null) return null;
            if (room.OnAnvil != null && !room.OnAnvil.broken) return room.OnAnvil;
            if (room.Held != null && !room.Held.broken) return room.Held;
            return null;
        }

        internal static bool HandleOnly()
        {
            return UiState.HudEdit;
        }

        internal static void Draw()
        {
            if (!Cfg.B("show_hud", true) && !UiState.HudEdit) return;
            if (UiState.Open) return;

            GPT6ForgeRoom room = Game.Room;
            ForgeCampaign c = Game.Campaign;
            if (room == null || c == null) return;
            if (room.StartScreenActive) return;

            LoadPos();

            float s = UiScale;
            GPT6Sword sword = Sword(room);
            bool hasSword = sword != null;

            float w = 268f * s;
            float h = (hasSword ? 148f : 86f) * s;
            float x = Mathf.Clamp(_x, 0f, Mathf.Max(0f, Screen.width - w));
            float y = Mathf.Clamp(_y, 0f, Mathf.Max(0f, Screen.height - h));
            Rect r = new Rect(x, y, w, h);

            Frame(r, Alpha(Panel, 0.92f), 12);
            if (UiState.HudEdit) RoundedFill(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), Alpha(Accent, 0.10f), 11);
            RoundedFill(new Rect(r.x + 1f, r.y + 8f * s, 3f * s, r.height - 16f * s), Accent, 2);

            float px = r.x + 14f * s;
            float pw = r.width - 24f * s;
            float py = r.y + 9f * s;

            Lbl(new Rect(px, py, pw, 14f * s), L.T("月亮", "Moon"), 10, Faint);
            py += 16f * s;

            Lbl(new Rect(px, py, pw, 20f * s), L.T("第 ", "Round ") + c.round + L.T(" 回合", ""), 14, Text, TextAnchor.MiddleLeft, true);
            Lbl(new Rect(px, py, pw, 20f * s), Game.Money(room.delivered) + " / " + Game.Money(c.Quota), 11, Dim, TextAnchor.MiddleRight);
            py += 22f * s;

            float ratio = (float)Math.Min(1.0, room.delivered / Math.Max(1.0, c.Quota));
            Bar(new Rect(px, py, pw, 4f * s), ratio, ratio >= 1f ? Ok : Accent);
            py += 10f * s;

            if (!hasSword)
            {
                Lbl(new Rect(px, py, pw, 18f * s), L.T("当前没有目标剑", "No sword in hand"), 12, Faint);
                HandleDrag(r);
                return;
            }

            Lbl(new Rect(px, py, pw - 60f * s, 18f * s), sword.DisplayName + "  +" + sword.grade, 12, Text);
            Lbl(new Rect(px, py, pw, 18f * s), sword.effects.charge + "/10", 11, Faint, TextAnchor.MiddleRight);
            py += 20f * s;

            ForgeChances ch = room.CurrentChances;
            DrawSegment(new Rect(px, py, pw, 4f * s), ch);
            py += 8f * s;

            // 五种结果：等宽五列，全部显示，永不截断
            float gap = 4f * s;
            float cellW = (pw - gap * 4f) / 5f;
            for (int i = 0; i < 5; i++)
            {
                Rect cell = new Rect(px + i * (cellW + gap), py, cellW, 34f * s);
                Fill(new Rect(cell.x, cell.y + 26f * s, cell.width, 2f * s), Alpha(PartColor(i), 0.85f));
                Lbl(new Rect(cell.x, cell.y + 2f * s, cell.width, 12f * s), Name(i), 9, Faint, TextAnchor.MiddleCenter);
                Lbl(new Rect(cell.x, cell.y + 12f * s, cell.width, 16f * s),
                    ch.Percent(i).ToString("0.#") + "%", 12, PartColor(i), TextAnchor.MiddleCenter, true);
            }
            py += 36f * s;

            string status = Status(room);
            if (status != null) Lbl(new Rect(px, py, pw, 14f * s), status, 10, StatusColor(), TextAnchor.MiddleLeft);

            HandleDrag(r);
        }

        private static string Name(int i)
        {
            switch (i)
            {
                case 0: return L.T("损毁", "X");
                case 1: return L.T("损伤", "Dmg");
                case 2: return L.T("保持", "Hold");
                case 3: return L.T("成功", "OK");
                default: return L.T("大成", "Great");
            }
        }

        private static string Status(GPT6ForgeRoom room)
        {
            int mode = Cfg.I("force_outcome", 0);
            if (mode >= 1 && mode <= 5)
            {
                string[] opts =
                {
                    "", L.T("损毁", "destroy"), L.T("损伤", "damage"), L.T("保持", "hold"),
                    L.T("成功", "success"), L.T("大成功", "great")
                };
                return L.T("强制结果：" + opts[mode] + "（仅结算生效）", "Forced: " + opts[mode] + " (settlement only)");
            }
            if (Cfg.B("never_break", false)) return L.T("永不损毁：开", "Never break: on");
            return null;
        }

        private static Color StatusColor()
        {
            if (Cfg.I("force_outcome", 0) > 0) return Warn;
            return Ok;
        }

        internal static Color PartColor(int i)
        {
            switch (i)
            {
                case 0: return Bad;
                case 1: return Warn;
                case 2: return Dim;
                case 3: return Ok;
                default: return Accent;
            }
        }

        internal static void SavePosition()
        {
            LoadPos();
            Cfg.SF("hud_x", _x);
            Cfg.SF("hud_y", _y);
        }

        internal static void ResetPosition()
        {
            _posLoaded = true;
            _x = 18f;
            _y = 18f;
            Cfg.SF("hud_x", -1f);
            Cfg.SF("hud_y", 18f);
        }

        private static void HandleDrag(Rect r)
        {
            Event e = Event.current;
            if (e == null) return;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0 && r.Contains(e.mousePosition))
                    {
                        _dragging = true;
                        _dragOffset = e.mousePosition - new Vector2(_x, _y);
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (_dragging)
                    {
                        Vector2 p = e.mousePosition - _dragOffset;
                        _x = Mathf.Clamp(p.x, 0f, Screen.width - 40f * UiScale);
                        _y = Mathf.Clamp(p.y, 0f, Screen.height - 30f * UiScale);
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (_dragging)
                    {
                        _dragging = false;
                        Cfg.SF("hud_x", _x);
                        Cfg.SF("hud_y", _y);
                        e.Use();
                    }
                    break;
            }
        }

        private static void DrawSegment(Rect r, ForgeChances ch)
        {
            float total = Mathf.Max(1f, ch.Total);
            float x = r.x;
            for (int i = 0; i < 5; i++)
            {
                float frac = ch[i] / total;
                float w = r.width * Mathf.Clamp01(frac);
                if (w > 0.5f)
                {
                    Fill(new Rect(x, r.y, w, r.height), PartColor(i));
                    x += w;
                }
            }
            float rest = r.x + r.width - x;
            if (rest > 0.5f) Fill(new Rect(x, r.y, rest, r.height), Alpha(Text, 0.15f));
        }
    }
}
