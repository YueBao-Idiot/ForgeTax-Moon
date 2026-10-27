// UI/MenuWindow.cs — 主窗口：标题栏 / 侧边导航 / 滚动内容页 / 页脚
using System;
using System.Collections.Generic;
using MelonLoader;
using Moon.Core;
using Moon.Data;
using Moon.Features;
using UnityEngine;
using static Moon.UI.Widgets;
using static Moon.UI.Theme;

namespace Moon.UI
{
    internal static class MenuWindow
    {
        internal const string Version = "2.1.1";

        private static readonly string[] TabKeys =
        {
            "overview", "economy", "forge", "round", "relics", "merchant", "player", "system"
        };

        private static readonly string[] TabZh =
        {
            "概览", "经济", "锻造", "回合", "遗物图鉴", "商人", "玩家", "系统"
        };

        private static readonly string[] TabEn =
        {
            "Overview", "Economy", "Forge", "Round", "Relics", "Merchant", "Player", "System"
        };

        private static readonly string[] TabGlyph =
        {
            "概", "钱", "锻", "回", "遗", "商", "人", "设"
        };

        private static bool _loggedGeom;
        private static bool _scrollDragging;
        private static bool _dragging;
        private static Vector2 _dragOffset;

        // ---------- 窗口几何 ----------

        private static Rect WindowRect()
        {
            float s = UiScale;
            float w = 960f * s;
            float h = Mathf.Min(600f * s, Screen.height - 24f * s);
            if (UiState.WindowX < 0f || UiState.WindowY < 0f)
            {
                UiState.WindowX = (Screen.width - w) * 0.5f;
                UiState.WindowY = (Screen.height - h) * 0.5f;
            }
            float keepX = 120f * s;
            float keepY = 44f * s;
            float x = Mathf.Clamp(UiState.WindowX, -(w - keepX), Mathf.Max(0f, Screen.width - keepX));
            float y = Mathf.Clamp(UiState.WindowY, -(h - keepY), Mathf.Max(0f, Screen.height - keepY));
            return new Rect(x, y, w, h);
        }

        // ---------- 入口 ----------

        internal static void Draw()
        {
            HandleHotkeys();

            // 游戏的 OnGUI 可能留下缩放矩阵、半透明颜色或被禁用的状态，这里全部重置，
            // 否则界面会被拉伸 / 变透明 / 无法点击（曾导致文字叠影与布局错位）。
            GUISkin prevSkin = GUI.skin;
            Color prevColor = GUI.color;
            Matrix4x4 prevMatrix = GUI.matrix;
            bool prevEnabled = GUI.enabled;
            try
            {
                GUI.skin = Theme.Skin;
                GUI.color = Color.white;
                GUI.matrix = Matrix4x4.identity;
                GUI.enabled = true;
                // 让本模组的界面绘制在游戏自身 OnGUI 之上，否则游戏 UI 会盖住菜单
                GUI.depth = Cfg.I("gui_depth", -1000);

                if (UiState.Open)
                {
                    // 背景压暗，保证菜单内容清晰可读
                    Color c = GUI.color;
                    GUI.color = Theme.Alpha(Color.black, Theme.LightMode ? 0.35f : 0.55f);
                    GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                    GUI.color = c;
                    DrawWindow();
                }

                Hud.Draw();
                Toast.Draw();
                Tooltip();
            }
            finally
            {
                GUI.skin = prevSkin;
                GUI.color = prevColor;
                GUI.matrix = prevMatrix;
                GUI.enabled = prevEnabled;
            }
        }

        // ---------- 快捷键 ----------

        private static void HandleHotkeys()
        {
            Event e = Event.current;
            if (e == null) return;

            if (UiState.CaptureKeyFor != null)
            {
                if (e.type == EventType.KeyDown)
                {
                    if (e.keyCode == KeyCode.Escape)
                    {
                        UiState.CaptureKeyFor = null;
                    }
                    else if (e.keyCode != KeyCode.None)
                    {
                        Cfg.SS("key_" + UiState.CaptureKeyFor, e.keyCode.ToString());
                        Toast.Push(L.T("快捷键已更新：" + e.keyCode, "Hotkey set: " + e.keyCode));
                        UiState.CaptureKeyFor = null;
                    }
                    e.Use();
                }
                return;
            }

            if (e.type != EventType.KeyDown) return;

            if (UiState.Open && GUIUtility.keyboardControl == 0)
            {
                int digit = -1;
                if (e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha8) digit = e.keyCode - KeyCode.Alpha1;
                else if (e.keyCode >= KeyCode.Keypad1 && e.keyCode <= KeyCode.Keypad8) digit = e.keyCode - KeyCode.Keypad1;
                if (digit >= 0)
                {
                    UiState.Tab = digit;
                    UiState.Scroll = Vector2.zero;
                    e.Use();
                    return;
                }
            }

            if (KeyHit(e, "key_menu", "F1"))
            {
                ToggleMenu();
                e.Use();
                return;
            }
            if (KeyHit(e, "key_hud", "F2"))
            {
                Cfg.SB("show_hud", !Cfg.B("show_hud", true));
                e.Use();
                return;
            }
            if (KeyHit(e, "key_hud_edit", "F3"))
            {
                UiState.HudEdit = !UiState.HudEdit;
                if (UiState.HudEdit)
                {
                    Game.SetCursor(true);
                    Toast.Push(L.T("拖动浮层到合适位置，再按 F3 完成", "Drag the HUD, press F3 when done"));
                }
                else
                {
                    Cfg.SF("hud_x", -2f);
                    Game.SetCursor(false);
                    Toast.Push(L.T("浮层位置已保存", "HUD position saved"));
                }
                e.Use();
                return;
            }
            if (KeyHit(e, "key_gold", "F5"))
            {
                Actions.AddWallet(10000);
                e.Use();
                return;
            }
            if (KeyHit(e, "key_win", "F6"))
            {
                Actions.ForceWin();
                e.Use();
                return;
            }
            if (KeyHit(e, "key_auto", "F7"))
            {
                bool v = !Cfg.B("auto_strike", false);
                Cfg.SB("auto_strike", v);
                Toast.Push(v ? L.T("自动敲击：开", "Auto strike: on") : L.T("自动敲击：关", "Auto strike: off"), v);
                e.Use();
                return;
            }
            if (KeyHit(e, "key_repair", "F8"))
            {
                Actions.Repair();
                e.Use();
            }
        }

        private static bool KeyHit(Event e, string cfgKey, string def)
        {
            string name = Cfg.S(cfgKey, def);
            KeyCode want;
            try { want = (KeyCode)Enum.Parse(typeof(KeyCode), name, true); }
            catch (Exception) { want = (KeyCode)Enum.Parse(typeof(KeyCode), def, true); }
            return e.keyCode == want;
        }

        internal static void ToggleMenu()
        {
            UiState.Open = !UiState.Open;
            if (UiState.Open)
            {
                Game.SetCursor(true);
                UiState.Scroll = Vector2.zero;
            }
            else
            {
                GPT6ForgeRoom room = Game.Room;
                bool startScreen = room != null && room.StartScreenActive;
                if (!startScreen) Game.SetCursor(false);
                Game.Refresh();
            }
        }

        // ---------- 窗口绘制 ----------

        private static void DrawWindow()
        {
            float s = UiScale;
            Rect win = WindowRect();

            if (Cfg.B("debug_solid", false))
            {
                // 诊断色块：红 / 绿 / 深灰 / 纯黑，用于判断颜色是否被改写
                Fill(new Rect(win.x + 10f, win.y + 10f, 60f, 40f), new Color(1f, 0f, 0f, 1f));
                Fill(new Rect(win.x + 80f, win.y + 10f, 60f, 40f), new Color(0f, 1f, 0f, 1f));
                Fill(new Rect(win.x + 150f, win.y + 10f, 60f, 40f), new Color(0.25f, 0.25f, 0.25f, 1f));
                Fill(new Rect(win.x + 220f, win.y + 10f, 60f, 40f), new Color(0f, 0f, 0f, 1f));
            }
            if (!_loggedGeom)
            {
                _loggedGeom = true;
                MelonLoader.MelonLogger.Msg("[月亮] 屏幕 " + Screen.width + "x" + Screen.height +
                    "  字体 " + Theme.Font.name + "  页 " + UiState.Tab + "  缩放 " + UiScale.ToString("0.00") + "  浅色=" + Theme.LightMode + " 面板=" + Theme.Panel.ToString() + "  文本=" + Theme.Text.ToString() + "  窗口 " + win.x.ToString("0") + "," + win.y.ToString("0") +
                    " " + win.width.ToString("0") + "x" + win.height.ToString("0"));
            }
            RoundedFill(win, Panel, 10);

            float headerH = 64f * s;
            float footerH = 30f * s;
            float sideW = 146f * s;

            DrawHeader(new Rect(win.x, win.y, win.width, headerH));
            DrawSidebar(new Rect(win.x + 8f * s, win.y + headerH + 8f * s, sideW, win.height - headerH - footerH - 16f * s));
            DrawContent(new Rect(win.x + sideW + 18f * s, win.y + headerH + 6f * s,
                win.width - sideW - 28f * s, win.height - headerH - footerH - 10f * s));
            DrawFooter(new Rect(win.x, win.y + win.height - footerH, win.width, footerH));

            HandleDrag(win, headerH);
        }

        private static void DrawHeader(Rect r)
        {
            float s = UiScale;


            // 标记块
            Rect mark = new Rect(r.x + 18f * s, r.y + (r.height - 32f * s) * 0.5f, 32f * s, 32f * s);
            Box(mark, Accent, 9);
            Lbl(mark, "月", 17, OnAccent, TextAnchor.MiddleCenter, true);

            Lbl(new Rect(r.x + 60f * s, r.y + 8f * s, 360f * s, 30f * s), L.T("月亮", "Moon Mod Menu"), 17, Text, TextAnchor.MiddleLeft, true);
            Lbl(new Rect(r.x + 60f * s, r.y + 36f * s, 420f * s, 20f * s), "ForgeTax  ·  v" + Version, 11, Faint);

            // 关闭按钮
            Rect close = new Rect(r.x + r.width - 44f * s, r.y + 18f * s, 28f * s, 28f * s);
            if (Btn(close, "×", 15, BtnKind.Flat, true, L.T("关闭菜单", "Close"))) ToggleMenu();

            // 状态条
            GPT6ForgeRoom room = Game.Room;
            ForgeCampaign c = Game.Campaign;
            float chipH = 50f * s;
            float chipW = 100f * s;
            float gap = 6f * s;
            float cx = r.x + r.width - 56f * s - chipW * 4 - gap * 3;
            float cy = r.y + 7f * s;

            if (room != null && c != null)
            {
                string round = L.T("第 " + c.round + " 回合", "Round " + c.round);
                StatChip(new Rect(cx, cy, chipW, chipH), L.T("回合", "Round"), round, Accent);
                StatChip(new Rect(cx + chipW + gap, cy, chipW, chipH), L.T("配额", "Quota"), Game.Money(c.Quota), Warn);
                StatChip(new Rect(cx + (chipW + gap) * 2f, cy, chipW, chipH), L.T("已交付", "Delivered"), Game.Money(room.delivered), Ok);
                GPT6Sword t = Game.Target;
                string sw = t == null ? "—" : "+" + t.grade;
                StatChip(new Rect(cx + (chipW + gap) * 3f, cy, chipW, chipH), L.T("目标剑", "Target"), sw, Hex("5E96E8"));
            }
            else
            {
                Lbl(new Rect(cx, cy, chipW * 4 + gap * 3, chipH), L.T("等待进入锻造坊…", "Waiting for the forge room…"), 12, Faint, TextAnchor.MiddleRight);
            }

            Divider(new Rect(r.x, r.y + r.height - 1f, r.width, 1f));
        }

        private static void DrawSidebar(Rect r)
        {
            float s = UiScale;
            float itemH = 44f * s;
            float gap = 4f * s;
            float y = r.y;

            for (int i = 0; i < TabKeys.Length; i++)
            {
                Rect item = new Rect(r.x, y, r.width, itemH);
                bool active = UiState.Tab == i;
                bool hover = item.Contains(Event.current.mousePosition);

                if (active)
                {
                    Box(item, AccentSoft, 9);
                    Box(new Rect(item.x, item.y + 9f * s, 3f * s, item.height - 18f * s), Accent, 2);
                }
                else if (hover)
                {
                    Box(item, Alpha(Text, 0.045f), 9);
                }

                Rect glyph = new Rect(item.x + 10f * s, item.y + (itemH - 26f * s) * 0.5f, 26f * s, 26f * s);
                Box(glyph, active ? Accent : Alpha(Text, 0.07f), 7);
                Lbl(glyph, TabGlyph[i], 13, active ? OnAccent : Dim, TextAnchor.MiddleCenter, active);
                Lbl(new Rect(item.x + 44f * s, item.y, item.width - 48f * s, itemH),
                    L.English ? TabEn[i] : TabZh[i], 13, active ? Text : Dim, TextAnchor.MiddleLeft, active);

                if (GUI.Button(item, GUIContent.none, GUIStyle.none))
                {
                    if (UiState.Tab != i)
                    {
                        UiState.Tab = i;
                        UiState.Scroll = Vector2.zero;
                    }
                }
                y += itemH + gap;
            }


        }

        private static void DrawContent(Rect area)
        {
            float s = UiScale;
            string title = L.English ? TabEn[UiState.Tab] : TabZh[UiState.Tab];
            float headerH = 52f * s;

            Lbl(new Rect(area.x, area.y, area.width, 32f * s), title, 18, Text, TextAnchor.MiddleLeft, true);
            Lbl(new Rect(area.x, area.y + 30f * s, area.width, 20f * s), TabHint(UiState.Tab), 11, Faint);

            Rect view = new Rect(area.x, area.y + headerH, area.width, area.height - headerH);
            float contentW = view.width - 16f * s;
            float measured = Mathf.Max(UiState.ContentHeight, view.height);

            HandleScrollWheel(view, measured);
            UiState.Scroll.y = Mathf.Clamp(UiState.Scroll.y, 0f, Mathf.Max(0f, measured - view.height));

            // BeginGroup / EndGroup 必须成对：一旦某页抛异常而没有 EndGroup，
            // Unity 会报 “pushing more GUIClips than popping”，之后整个界面都会画歪、点不动。
            GUI.BeginGroup(view);
            try
            {
                GUI.BeginGroup(new Rect(0f, -UiState.Scroll.y, contentW, measured));
                try
                {
                    Page p = new Page(2f, 2f, contentW - 8f);
                    try
                    {
                        switch (UiState.Tab)
                        {
                            case 0: PageOverview(p); break;
                            case 1: PageEconomy(p); break;
                            case 2: PageForge(p); break;
                            case 3: PageRound(p); break;
                            case 4: PageRelics(p); break;
                            case 5: PageMerchant(p); break;
                            case 6: PagePlayer(p); break;
                            default: PageSystem(p); break;
                        }
                    }
                    catch (Exception e)
                    {
                        ReportPageError(e);
                    }
                    p.Gap(16f);
                    UiState.ContentHeight = p.Bottom + 18f * s;
                }
                finally { GUI.EndGroup(); }
            }
            finally { GUI.EndGroup(); }

            DrawScrollbar(new Rect(view.x + view.width - 6f * s, view.y, 6f * s, view.height), measured);
        }

        private static readonly HashSet<int> _reportedPages = new HashSet<int>();

        private static void ReportPageError(Exception e)
        {
            if (!_reportedPages.Contains(UiState.Tab))
            {
                _reportedPages.Add(UiState.Tab);
                MelonLoader.MelonLogger.Warning("[月亮] 第 " + (UiState.Tab + 1) + " 页绘制异常（已跳过该页剩余内容）: " + e);
            }
        }

        private static void HandleScrollWheel(Rect view, float contentHeight)
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.ScrollWheel) return;
            if (!view.Contains(e.mousePosition)) return;
            if (contentHeight <= view.height) return;
            UiState.Scroll.y = Mathf.Clamp(UiState.Scroll.y + e.delta.y * 52f * UiScale, 0f, contentHeight - view.height);
            e.Use();
        }

        private static void DrawScrollbar(Rect track, float contentHeight)
        {
            float s = UiScale;
            if (contentHeight <= track.height + 1f) return;

            float viewH = track.height;
            float thumbH = Mathf.Max(36f * s, viewH * (viewH / contentHeight));
            float maxScroll = Mathf.Max(1f, contentHeight - viewH);
            float thumbY = track.y + (viewH - thumbH) * Mathf.Clamp01(UiState.Scroll.y / maxScroll);

            Fill(track, Alpha(Text, 0.06f));
            Rect thumb = new Rect(track.x, thumbY, track.width, thumbH);
            Fill(thumb, Alpha(Accent, 0.6f));

            Event e = Event.current;
            if (e == null) return;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0 && track.Contains(e.mousePosition))
                    {
                        _scrollDragging = true;
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (_scrollDragging)
                    {
                        float t = Mathf.Clamp01((e.mousePosition.y - track.y - thumbH * 0.5f) / Mathf.Max(1f, viewH - thumbH));
                        UiState.Scroll.y = t * maxScroll;
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (_scrollDragging)
                    {
                        _scrollDragging = false;
                        e.Use();
                    }
                    break;
            }
        }
        private static string TabHint(int tab)
        {
            switch (tab)
            {
                case 0: return L.T("实时数据与最常用的一键操作", "Live stats and the most used shortcuts");
                case 1: return L.T("金币、剑之铭刻、配额与结算收益", "Gold, credits, quota and settlement");
                case 2: return L.T("等级 / 价值 / 敲击结果 / 生成与复制", "Grade, value, strike outcome, spawn & clone");
                case 3: return L.T("违约、期限、结算、无尽模式与补给", "Defaults, deadline, settle, endless, supply");
                case 4: return L.T("一键发放遗物、点亮图鉴与成就", "Grant relics, complete codex & achievements");
                case 5: return L.T("幽灵商人的货架与一键扫货", "Ghost merchant stock and bulk buying");
                case 6: return L.T("移动、视角与快速传送", "Movement, view and teleport");
                default: return L.T("外观主题、热键、存档与游戏速度", "Theme, hotkeys, saves and game speed");
            }
        }

        private static void DrawFooter(Rect r)
        {
            float s = UiScale;
            Divider(new Rect(r.x, r.y, r.width, 1f));
            string hints = L.T("F1 开关菜单   ·   F2 数据浮层   ·   F5 加钱   ·   F6 强制通关   ·   F7 自动敲击", 
                               "F1 menu   ·   F2 HUD   ·   F5 gold   ·   F6 win   ·   F7 auto strike");
            Lbl(new Rect(r.x + 20f * s, r.y, r.width * 0.7f, r.height), hints, 11, Faint);

            Lbl(new Rect(r.x + r.width - 150f * s, r.y, 130f * s, r.height), "v" + Version, 10, Faint, TextAnchor.MiddleRight);
        }

        private static void HandleDrag(Rect win, float headerH)
        {
            float s = UiScale;
            Event e = Event.current;
            if (e == null) return;
            Rect handle = new Rect(win.x, win.y, win.width - 60f * s, headerH);

            if (e.type == EventType.MouseDown && e.button == 0 && handle.Contains(e.mousePosition))
            {
                _dragging = true;
                _dragOffset = e.mousePosition - new Vector2(win.x, win.y);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _dragging)
            {
                Vector2 p = e.mousePosition - _dragOffset;
                float keepX = 120f * s;
                float keepY = 44f * s;
                UiState.WindowX = Mathf.Clamp(p.x, -(win.width - keepX), Screen.width - keepX);
                UiState.WindowY = Mathf.Clamp(p.y, -(win.height - keepY), Screen.height - keepY);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _dragging)
            {
                _dragging = false;
                Cfg.SF("window_x", UiState.WindowX);
                Cfg.SF("window_y", UiState.WindowY);
                e.Use();
            }
        }

        // ==================== 页：概览 ====================

        private static void PageOverview(Page p)
        {
            GPT6ForgeRoom room = Game.Room;
            ForgeCampaign c = Game.Campaign;
            if (room == null || c == null)
            {
                p.Section(L.T("尚未进入锻造坊", "Not in the forge yet"));
                p.Note(L.T("进入一局游戏（或读取存档）后，本页数据会自动出现。", "Start or load a run and this page fills in automatically."));
                p.Gap(6);
                p.Buttons(new[] { L.T("立即存档", "Save now"), L.T("打开系统页", "System page") }, delegate(int i)
                {
                    if (i == 0) Actions.SaveNow();
                    else UiState.Tab = 7;
                }, 2);
                return;
            }

            float s = UiScale;
            float cardW = (p.W - 8f * s) * 0.5f;
            float cardH = 78f * s;

            // 左侧两张卡：进度 / 目标剑
            Rect left = new Rect(p.X, p.Y, cardW, cardH);
            Rect right = new Rect(p.X + cardW + 8f * s, p.Y, cardW, cardH);

            Bordered(left, PanelAlt, Line, 10);
            Lbl(new Rect(left.x + 14f * s, left.y + 9f * s, left.width - 28f * s, 18f * s), L.T("本回合进度", "Round progress"), 11, Faint);
            double quota = Math.Max(1.0, c.Quota);
            float ratio = (float)Math.Min(1.0, room.delivered / quota);
            Lbl(new Rect(left.x + 14f * s, left.y + 28f * s, left.width - 28f * s, 26f * s),
                Game.Money(room.delivered) + "  /  " + Game.Money(quota), 14, Text, TextAnchor.MiddleLeft, true);
            Bar(new Rect(left.x + 14f * s, left.y + cardH - 18f * s, left.width - 28f * s, 6f * s), ratio, ratio >= 1f ? Ok : Accent);

            GPT6Sword target = Game.Target;
            Bordered(right, PanelAlt, Line, 10);
            Lbl(new Rect(right.x + 14f * s, right.y + 9f * s, right.width - 28f * s, 18f * s), L.T("目标剑", "Target sword"), 11, Faint);
            if (target == null)
            {
                Lbl(new Rect(right.x + 14f * s, right.y + 28f * s, right.width - 28f * s, 26f * s), L.T("无（先拿起一把剑）", "None — pick up a sword"), 13, Dim);
            }
            else
            {
                Lbl(new Rect(right.x + 14f * s, right.y + 28f * s, right.width - 28f * s, 26f * s),
                    target.DisplayName + "  +" + target.grade, 14, Text, TextAnchor.MiddleLeft, true);
                Lbl(new Rect(right.x + 14f * s, right.y + 54f * s, right.width - 28f * s, 18f * s),
                    Game.Money(target.value) + " G   ·   " + L.T("敲击 " + target.strikes, "strikes " + target.strikes), 11, Dim);
            }
            p.Gap(cardH + 2f * s);

            // 概率
            p.Section(L.T("本次敲击的概率", "Current strike odds"), null);
            ForgeChances ch = room.CurrentChances;
            DrawChance(p, L.T("损毁", "Destroy"), ch.Percent(0), Bad);
            DrawChance(p, L.T("损伤", "Damage"), ch.Percent(1), Warn);
            DrawChance(p, L.T("保持", "Hold"), ch.Percent(2), Dim);
            DrawChance(p, L.T("成功", "Success"), ch.Percent(3), Ok);
            DrawChance(p, L.T("大成功", "Great"), ch.Percent(4), Accent);
            p.Gap(10f);

            // 快捷操作
            p.Section(L.T("快捷操作", "Quick actions"), L.T("不需要打开其它页面", "No need to switch pages"));
            p.Buttons(new[]
            {
                L.T("金币 +1 万", "+10K gold"),
                L.T("等级 +5", "Grade +5"),
                L.T("拉满充能", "Fill charge"),
                L.T("修复断剑", "Repair"),
            }, delegate(int i)
            {
                switch (i)
                {
                    case 0: Actions.AddWallet(10000); break;
                    case 1: Actions.BumpGrade(5); break;
                    case 2: Actions.ChargeFull(); break;
                    case 3: Actions.Repair(); break;
                }
            }, 4);

            p.Buttons(new[]
            {
                L.T("强制通关本回合", "Force round win"),
                L.T("全部遗物", "All relics"),
                L.T("立即存档", "Save now"),
                L.T("清理地上剑", "Clean floor"),
            }, delegate(int i)
            {
                switch (i)
                {
                    case 0: Actions.ForceWin(); break;
                    case 1: Actions.UnlockAllRelics(); break;
                    case 2: Actions.SaveNow(); break;
                    case 3: Actions.CleanupFloorSwords(); break;
                }
            }, 4);

            p.Gap(12f);
            p.Section(L.T("本局状态", "Run status"), null);
            p.KeyValue(L.T("回合 / 期限", "Round / deadline"), c.round + " / " + c.ContractDeadline);
            p.KeyValue(L.T("违约次数", "Defaults"), c.missedTributes + " / 3");
            p.KeyValue(L.T("金币 / 铭刻", "Gold / credits"), Game.Money(c.wallet) + "  ·  " + c.swordCredits);
            p.KeyValue(L.T("剩余订单", "Orders left"), room.ordersLeft.ToString());
            p.KeyValue(L.T("遗物数量", "Relics"), CountRelics(c) + " / 8");
            p.KeyValue(L.T("模式", "Mode"), c.endless ? L.T("无尽", "Endless") : L.T("契约", "Contract"));
            p.KeyValue(L.T("商店", "Shop"), c.MerchantOpen ? L.T("商人营业中", "Merchant open") : (c.shopping ? L.T("遗物商店", "Relic shop") : L.T("未开启", "Closed")));
        }

        private static int CountRelics(ForgeCampaign c)
        {
            int n = 0;
            if (c.relics == null) return 0;
            for (int i = 0; i < c.relics.Length; i++)
            {
                if (c.relics[i] != null && !string.IsNullOrEmpty(c.relics[i].id)) n++;
            }
            return n;
        }

        private static void DrawChance(Page p, string label, float percent, Color color)
        {
            float s = UiScale;
            Rect row = new Rect(p.X, p.Y, p.W, 22f * s);
            Lbl(new Rect(row.x, row.y, 90f * s, row.height), label, 11, Dim);
            Rect bar = new Rect(row.x + 96f * s, row.y + 8f * s, row.width - 104f * s, 6f * s);
            Bar(bar, percent / 100f, color);
            Lbl(new Rect(row.x + row.width - 50f * s, row.y, 50f * s, row.height), percent.ToString("0.0") + "%", 11, Text, TextAnchor.MiddleRight);
            p.Y += row.height + 3f * s;
        }

        // ==================== 页：经济 ====================

        private static void PageEconomy(Page p)
        {
            ForgeCampaign c = Game.Campaign;
            GPT6ForgeRoom room = Game.Room;

            p.Section(L.T("金币", "Gold"), L.T("直接修改钱袋，历史收入同步累加", "Edit the wallet, history income included"));
            if (c != null)
            {
                p.KeyValue(L.T("当前金币", "Wallet"), Game.Money(c.wallet));
            }
            p.Buttons(new[] { "+1 千", "+1 万", "+10 万", "+100 万", "+1000 万" }, delegate(int i)
            {
                double[] amounts = { 1000, 10000, 100000, 1000000, 10000000 };
                Actions.AddWallet(amounts[i]);
            }, 5);
            p.Gap(6f);
            p.TextApply(L.T("设为指定金币", "Set gold"), ref UiState.GoldInput, delegate(string v)
            {
                double amount;
                if (double.TryParse(v, out amount)) Actions.SetWallet(amount);
                else Toast.Push(L.T("请输入数字", "Numbers only"), false);
            }, L.T("应用", "Apply"));

            p.Gap(6f);
            p.SliderSetting("gold_mult", L.T("结算收益倍率", "Settlement gold multiplier"), 1f, 10f,
                L.T("只影响回合结算入账（补丁），不影响其它来源。", "Multiplies settlement payouts via patch."),
                delegate(float v) { return "x" + v.ToString("0.0"); });

            p.Section(L.T("剑之铭刻", "Sword credits"), L.T("商店里买遗物用的货币", "Spent on relics in the shop"));
            if (c != null) p.KeyValue(L.T("当前铭刻", "Credits"), c.swordCredits.ToString());
            p.Buttons(new[] { "+1", "+10", "+50", "+100" }, delegate(int i)
            {
                int[] amounts = { 1, 10, 50, 100 };
                Actions.AddCredits(amounts[i]);
            }, 4);
            p.Gap(6f);
            p.TextApply(L.T("设为指定铭刻", "Set credits"), ref UiState.CreditInput, delegate(string v)
            {
                int n;
                if (int.TryParse(v, out n)) Actions.SetCredits(n);
                else Toast.Push(L.T("请输入整数", "Integers only"), false);
            }, L.T("应用", "Apply"));

            p.Section(L.T("本回合配额", "Round quota"), L.T("降低配额等于更容易达成契约；100% 恢复原始配置", "Lower quota makes the tax easier; 100% restores the original"));
            if (c != null) p.KeyValue(L.T("当前配额", "Quota"), Game.Money(c.Quota));
            p.Buttons(new[] { "100%", "75%", "50%", "25%", "1%" }, delegate(int i)
            {
                double[] pct = { 100, 75, 50, 25, 1 };
                Actions.SetQuotaPercent(pct[i]);
            }, 5);
            p.Gap(6f);
            p.TextApply(L.T("锁定指定配额", "Lock quota"), ref UiState.QuotaInput, delegate(string v)
            {
                double n;
                if (double.TryParse(v, out n)) Actions.SetQuotaExact(n);
                else Toast.Push(L.T("请输入数字", "Numbers only"), false);
            }, L.T("应用", "Apply"));

            p.Section(L.T("其它", "Miscellaneous"), null);
            p.Buttons(new[]
            {
                L.T("领取待结算收益", "Claim payout"),
                L.T("金币翻倍", "Double gold"),
                L.T("填入 100 万铭刻", "Credits 1M"),
            }, delegate(int i)
            {
                switch (i)
                {
                    case 0: Actions.PaySettlement(); break;
                    case 1: if (c != null) Actions.SetWallet(c.wallet * 2.0); break;
                    case 2: Actions.SetCredits(1000000); break;
                }
            }, 3);
        }

        // ==================== 页：锻造 ====================

        private static void PageForge(Page p)
        {
            GPT6Sword target = Game.Target;
            GPT6ForgeRoom room = Game.Room;

            p.Section(L.T("目标剑", "Target sword"), L.T("优先级：铁砧上的剑 → 手持的剑 → 准星指向的剑", "Priority: anvil → held → focused"));
            if (target == null)
            {
                p.Note(L.T("当前没有目标剑，请先拿起或放下把剑。", "No target sword. Pick one up first."));
            }
            else
            {
                p.KeyValue(L.T("名称", "Name"), target.DisplayName + "   +" + target.grade);
                p.KeyValue(L.T("价值 / 敲击", "Value / strikes"), Game.Money(target.value) + " G   ·   " + target.strikes);
                p.KeyValue(L.T("状态", "State"), target.broken ? L.T("已断（不可敲击）", "Broken") : (room != null && room.InFever ? L.T("狂热中", "In fever") : L.T("完好", "Intact")));
                p.KeyValue(L.T("充能", "Charge"), target.effects.charge + " / 10");
            }

            p.Section(L.T("等级", "Grade"), L.T("上限 " + Game.GradeCap + "，改动会同步外观与价值", "Cap " + Game.GradeCap + ", visuals and value follow"));
            p.Buttons(new[] { "+1", "+5", "+10", L.T("设为上限", "Max") }, delegate(int i)
            {
                switch (i)
                {
                    case 0: Actions.BumpGrade(1); break;
                    case 1: Actions.BumpGrade(5); break;
                    case 2: Actions.BumpGrade(10); break;
                    case 3: Actions.SetGrade(Game.GradeCap); break;
                }
            }, 4);
            p.Gap(6f);
            p.TextApply(L.T("设为指定等级", "Set grade"), ref UiState.GradeInput, delegate(string v)
            {
                int n;
                if (int.TryParse(v, out n)) Actions.SetGrade(n);
                else Toast.Push(L.T("请输入整数", "Integers only"), false);
            }, L.T("应用", "Apply"));

            p.Section(L.T("价值", "Value"), null);
            p.Buttons(new[] { "x2", "x10", "x100", "x1000" }, delegate(int i)
            {
                double[] f = { 2, 10, 100, 1000 };
                Actions.MultiplyValue(f[i]);
            }, 4);
            p.Gap(6f);
            p.TextApply(L.T("设为指定价值", "Set value"), ref UiState.ValueInput, delegate(string v)
            {
                double n;
                if (double.TryParse(v, out n)) Actions.SetValue(n);
                else Toast.Push(L.T("请输入数字", "Numbers only"), false);
            }, L.T("应用", "Apply"));

            p.Section(L.T("状态修复", "Repair & charge"), null);
            p.Buttons(new[]
            {
                L.T("修复断剑", "Repair"),
                L.T("充能拉满", "Fill charge"),
                L.T("充能清零", "Clear charge"),
                L.T("重置敲击数", "Reset strikes"),
            }, delegate(int i)
            {
                switch (i)
                {
                    case 0: Actions.Repair(); break;
                    case 1: Actions.ChargeFull(); break;
                    case 2: Actions.ChargeZero(); break;
                    case 3: Actions.ResetStrikes(); break;
                }
            }, 4);
            p.Buttons(new[]
            {
                L.T("强制进入狂热", "Force fever"),
                L.T("结束狂热", "End fever"),
                L.T("装备守护", "Arm ward"),
                L.T("加热手持剑", "Heat held"),
            }, delegate(int i)
            {
                switch (i)
                {
                    case 0: Actions.StartFever(); break;
                    case 1: Actions.EndFever(); break;
                    case 2: Actions.ArmWard(); break;
                    case 3: Actions.HeatHeld(); break;
                }
            }, 4);

            p.Section(L.T("敲击结果控制", "Strike outcome control"), L.T("通过补丁改写每次敲击的判定；关闭即完全恢复原生概率", "Rewrites each strike outcome; off = vanilla odds"));
            int mode = Cfg.I("force_outcome", 0);
            string[] opts =
            {
                L.T("关闭", "Off"), L.T("必损毁", "Destroy"), L.T("必损伤", "Damage"),
                L.T("必保持", "Hold"), L.T("必成功", "Success"), L.T("必大成功", "Great")
            };
            int newMode = p.SegmentedRow(L.T("强制结果", "Forced outcome"), opts, mode);
            if (newMode != mode)
            {
                Cfg.SI("force_outcome", newMode);
                Toast.Push(newMode == 0
                    ? L.T("已恢复原生概率", "Vanilla odds restored")
                    : L.T("已锁定敲击结果：" + opts[newMode], "Outcome locked: " + opts[newMode]));
            }
            p.Gap(8f);
            bool neverBreak = Cfg.B("never_break", false);
            bool nb = p.Toggle(L.T("永不损毁", "Never break"), L.T("损毁结果自动改写为「保持」", "Destroy becomes Hold"),
                "never_break", neverBreak);
            if (nb != neverBreak) Toast.Push(nb ? L.T("永不损毁：开", "Never break: on") : L.T("永不损毁：关", "Never break: off"));

            bool auto = Cfg.B("auto_strike", false);
            bool na = p.Toggle(L.T("自动敲击", "Auto strike"), L.T("站在铁砧前会自动连续挥锤", "Hammers automatically while at the anvil"),
                "auto_strike", auto);
            if (na != auto) Toast.Push(na ? L.T("自动敲击：开", "Auto strike: on") : L.T("自动敲击：关", "Auto strike: off"));
            p.SliderSetting("auto_strike_interval", L.T("敲击间隔", "Strike interval"), 0.15f, 2f,
                L.T("越小越快；过小会被游戏自身冷却限制", "Lower is faster, limited by the game cooldown"),
                delegate(float v) { return v.ToString("0.00") + "s"; });

            p.Section(L.T("生成与复制", "Spawn & clone"), L.T("生成的剑标记为调试生成：不进图鉴、不影响成就，但随存档保留", "Spawned swords are debug-flagged: no codex/achievements, still persisted"));
            p.SegmentedRow(L.T("剑种", "Kind"), new[] { L.T("铁剑", "Iron"), L.T("世界树", "Tree"), L.T("海鲜", "Seafood"), L.T("殡葬", "Funerary") }, UiState.ForgeSpawnKind,
                delegate(int v) { UiState.ForgeSpawnKind = v; });
            p.Gap(6f);
            p.SegmentedRow(L.T("外观", "Appearance"), new[] { "A", "B", "C" }, UiState.ForgeSpawnParent,
                delegate(int v) { UiState.ForgeSpawnParent = v; });
            p.Gap(6f);
            p.SegmentedRow(L.T("分支", "Branch"), new[] { "0", "1" }, UiState.ForgeSpawnBranch,
                delegate(int v) { UiState.ForgeSpawnBranch = v; });
            p.Gap(6f);
            
            float grade = p.SliderRow(L.T("生成等级", "Spawn grade"), UiState.ForgeSpawnGrade, 0, Math.Max(1, Game.GradeCap),
                delegate(float v) { return Mathf.RoundToInt(v).ToString(); });
            UiState.ForgeSpawnGrade = Mathf.RoundToInt(grade);
            p.Gap(8f);
            p.Buttons(new[]
            {
                L.T("生成到面前", "Spawn"),
                L.T("复制目标剑", "Clone target"),
                L.T("删除目标剑", "Delete target"),
            }, delegate(int i)
            {
                switch (i)
                {
                    case 0: Actions.SpawnSword(UiState.ForgeSpawnGrade, UiState.ForgeSpawnKind, UiState.ForgeSpawnParent, UiState.ForgeSpawnBranch); break;
                    case 1: Actions.CloneTarget(); break;
                    case 2: Actions.DestroyTarget(); break;
                }
            }, 3);

            p.Section(L.T("批量操作", "Bulk actions"), null);
            p.Buttons(new[]
            {
                L.T("全部剑 +5", "All +5"),
                L.T("全部剑满级", "All max"),
                L.T("全部价值 x10", "All value x10"),
                L.T("清理地上剑", "Clean floor"),
            }, delegate(int i)
            {
                switch (i)
                {
                    case 0:
                        {
                            List<GPT6Sword> list = Game.AllSwords();
                            int cap = Game.GradeCap;
                            int max = 0;
                            foreach (GPT6Sword s in list) if (s != null && !s.broken) max = Math.Max(max, s.grade);
                            Actions.GradeAllSwords(Math.Min(cap, max + 5));
                            break;
                        }
                    case 1: Actions.GradeAllSwords(Game.GradeCap); break;
                    case 2: Actions.MultiplyAllValues(10); break;
                    case 3: Actions.CleanupFloorSwords(); break;
                }
            }, 4);
        }

        /// <summary>「本次敲击概率」——只对下一锤生效，锤完自动恢复原生概率。</summary>
        // ==================== 页：回合 ====================

        private static void PageRound(Page p)
        {
            ForgeCampaign c = Game.Campaign;
            GPT6ForgeRoom room = Game.Room;

            p.Section(L.T("契约与违约", "Contract & defaults"), L.T("违约 3 次即失败；期限到回合即结算", "Three defaults end the run; the contract settles at its deadline"));
            if (c != null)
            {
                p.KeyValue(L.T("当前回合", "Round"), c.round.ToString());
                p.KeyValue(L.T("契约期限", "Deadline"), c.ContractDeadline.ToString());
                p.KeyValue(L.T("违约", "Defaults"), c.missedTributes + " / 3");
            }
            p.Buttons(new[] { L.T("清除违约", "Clear defaults"), L.T("违约设为 1", "Set 1"), L.T("违约设为 2", "Set 2") }, delegate(int i)
            {
                switch (i)
                {
                    case 0: Actions.ClearMissed(); break;
                    case 1: Actions.SetMissed(1); break;
                    case 2: Actions.SetMissed(2); break;
                }
            }, 3);
            p.Buttons(new[] { L.T("期限 +1", "+1"), L.T("期限 +10", "+10"), L.T("期限 +50", "+50") }, delegate(int i)
            {
                int[] n = { 1, 10, 50 };
                Actions.ExtendDeadline(n[i]);
            }, 3);

            p.Section(L.T("结算", "Settlement"), L.T("结算会走游戏原生流程（龙收剑、商店、下一回合）", "Settlement uses the native flow (dragon, shop, next round)"));
            if (room != null && c != null)
            {
                p.KeyValue(L.T("交付 / 配额", "Delivered / quota"), Game.Money(room.delivered) + " / " + Game.Money(c.Quota));
            }
            p.Buttons(new[]
            {
                L.T("结束本回合", "End round"),
                L.T("强制通关本回合", "Force win"),
                L.T("交付额 = 配额 x3", "Delivered x3"),
                L.T("交付额归零", "Delivered = 0"),
            }, delegate(int i)
            {
                switch (i)
                {
                    case 0: Actions.EndRound(); break;
                    case 1: Actions.ForceWin(); break;
                    case 2: Actions.SetDeliveredFactor(3.0); break;
                    case 3: Actions.SetDeliveredFactor(0.0); break;
                }
            }, 4);
            p.Gap(6f);
            p.TextApply(L.T("设置剩余订单数", "Set orders left"), ref UiState.SwordCountInput, delegate(string v)
            {
                int n;
                if (int.TryParse(v, out n)) Actions.SetOrders(n);
                else Toast.Push(L.T("请输入整数", "Integers only"), false);
            }, L.T("应用", "Apply"));

            p.Section(L.T("模式与商店", "Mode & shop"), null);
            bool endless = c != null && c.endless;
            bool ne = p.Toggle(L.T("无尽模式", "Endless mode"), L.T("过关后继续下一回合，不再结算失败", "Keep playing after clearing the contract"),
                "", endless);
            if (ne != endless) Actions.SetEndless(ne);

            p.Buttons(new[]
            {
                L.T("刷新商店", "Reroll shop"),
                L.T("重置刷新次数", "Reset rerolls"),
                L.T("商人补货", "Restock merchant"),
            }, delegate(int i)
            {
                switch (i)
                {
                    case 0: Actions.RefreshShop(); break;
                    case 1: Actions.ResetRerolls(); break;
                    case 2: Actions.RestockMerchant(); break;
                }
            }, 3);

            p.Section(L.T("下回合补给", "Next supply"), L.T("下一回合发放的基础剑等级", "Grade of swords issued next round"));
            p.Buttons(new[] { "+1", "+5", "+10", L.T("清零", "Reset") }, delegate(int i)
            {
                if (c == null) return;
                int g = c.supplyGrade;
                switch (i)
                {
                    case 0: Actions.SetSupplyGrade(g + 1); break;
                    case 1: Actions.SetSupplyGrade(g + 5); break;
                    case 2: Actions.SetSupplyGrade(g + 10); break;
                    case 3: Actions.SetSupplyGrade(0); break;
                }
            }, 4);
        }

        // ==================== 页：遗物图鉴 ====================

        private static void PageRelics(Page p)
        {
            ForgeCampaign c = Game.Campaign;
            p.Section(L.T("当前遗物栏", "Equipped relics"), L.T("最多 8 格；点击列表项即可发放到空位", "Up to 8 slots; click an item below to equip"));
            if (c != null && c.relics != null)
            {
                float s = UiScale;
                float gap = 6f * s;
                float w = (p.W - gap * 3f) / 4f;
                float h = 32f * s;
                for (int i = 0; i < c.relics.Length; i++)
                {
                    int col = i % 4, row = i / 4;
                    Rect r = new Rect(p.X + col * (w + gap), p.Y + row * (h + gap), w, h);
                    ForgeRelic relic = c.relics[i];
                    bool filled = relic != null && !string.IsNullOrEmpty(relic.id);
                    Bordered(r, filled ? Alpha(Accent, 0.10f) : PanelAlt, filled ? Alpha(Accent, 0.35f) : Line, 8);
                    string name = "—";
                    if (filled)
                    {
                        ForgeItemDefinition def = ForgeEconomy.Item(relic.id);
                        name = def == null ? relic.id : def.LocalizedName;
                    }
                    Lbl(new Rect(r.x + 10f * s, r.y, r.width - 14f * s, r.height), name, 12, filled ? Text : Faint);
                }
                p.Y += h * 2f + gap * 2f + 4f * s;
            }

            p.Buttons(new[]
            {
                L.T("清空遗物", "Clear relics"),
                L.T("永久解锁全部", "Unlock all"),
                L.T("图鉴全点亮", "Full codex"),
                L.T("成就全解锁", "All achievements"),
            }, delegate(int i)
            {
                switch (i)
                {
                    case 0: Actions.ClearRelics(); break;
                    case 1: Actions.UnlockAllRelics(); break;
                    case 2: Actions.UnlockCodex(); break;
                    case 3:
                        if (!UiState.ConfirmAchievements)
                        {
                            UiState.ConfirmAchievements = true;
                            Toast.Push(L.T("再次点击以确认解锁全部成就", "Click again to confirm"), false);
                        }
                        else
                        {
                            UiState.ConfirmAchievements = false;
                            Actions.UnlockAchievements();
                        }
                        break;
                }
            }, 4);
            if (UiState.ConfirmAchievements)
            {
                p.Note(L.T("成就解锁会写入游戏存档（Steam 成就可能同步），请谨慎。", "Unlocking writes to your save (Steam may sync). Be careful."));
            }

            p.Section(L.T("全部遗物", "All relics"), L.T("按游戏内定义读取，点击直接获得一件", "Read from game data; click to grant one"));
            List<ForgeItemDefinition> items = Actions.RelicItems();
            if (items.Count == 0)
            {
                p.Note(L.T("尚未载入经济数据（先进入一局游戏）。", "Economy data not loaded yet."));
                return;
            }

            float cols = 3f;
            float gx = 6f * UiScale;
            float bw = (p.W - gx * (cols - 1f)) / cols;
            float bh = 30f * UiScale;

            for (int i = 0; i < items.Count; i += (int)cols)
            {
                for (int k = 0; k < cols; k++)
                {
                    int idx = i + k;
                    if (idx >= items.Count) break;
                    ForgeItemDefinition def = items[idx];
                    Rect r = new Rect(p.X + k * (bw + gx), p.Y, bw, bh);
                    bool owned = false;
                    if (c != null && c.relics != null)
                    {
                        for (int j = 0; j < c.relics.Length; j++)
                        {
                            if (c.relics[j] != null && c.relics[j].id == def.id) owned = true;
                        }
                    }
                    if (owned ? Btn(r, def.LocalizedName, 12, BtnKind.Primary) : Btn(r, def.LocalizedName, 12, BtnKind.Ghost))
                    {
                        if (owned) Toast.Push(L.T("已经拥有该遗物", "Already equipped"), false);
                        else Actions.GrantRelic(def.id);
                    }
                }
                p.Y += bh + gx;
            }

            p.Gap(8f);
            p.Section(L.T("快捷发放", "Quick grant"), L.T("一次性把当前选中的遗物填满空位", "Fill all empty slots with one relic"));
            if (items.Count > 0)
            {
                for (int i = 0; i < Math.Min(items.Count, 8); i++)
                {
                    // 展示前 8 个作为填满目标
                    if (i == 0) p.Gap(0f);
                }
                p.Buttons(new[]
                {
                    L.T("填满：" + items[0].LocalizedName, "Fill: " + items[0].LocalizedName),
                    items.Count > 1 ? L.T("填满：" + items[1].LocalizedName, "Fill: " + items[1].LocalizedName) : "—",
                    items.Count > 2 ? L.T("填满：" + items[2].LocalizedName, "Fill: " + items[2].LocalizedName) : "—",
                }, delegate(int i)
                {
                    if (i < items.Count) Actions.FillRelics(items[i].id);
                }, 3);
            }
        }

        // ==================== 页：商人 ====================

        private static void PageMerchant(Page p)
        {
            ForgeCampaign c = Game.Campaign;
            GPT6ForgeRoom room = Game.Room;
            p.Section(L.T("幽灵商人", "Ghost merchant"), L.T("每 3 回合出现一次（第 3、6、9… 回合）", "Appears every third round"));
            if (c == null)
            {
                p.Note(L.T("尚未进入游戏。", "Not in a run yet."));
                return;
            }

            p.KeyValue(L.T("本回合", "Round"), c.round + (ForgeCampaign.IsMerchantRound(c.round) ? L.T("（商人回合）", " (merchant round)") : ""));
            p.KeyValue(L.T("状态", "State"), c.MerchantOpen ? L.T("营业中", "Open") : L.T("未营业", "Closed"));
            p.KeyValue(L.T("剩余金币", "Wallet"), Game.Money(c.wallet));

            p.Buttons(new[]
            {
                L.T("补货", "Restock"),
                L.T("一键扫空", "Buy all"),
                L.T("金币 +10 万", "+100K gold"),
                L.T("重置刷新", "Reset rerolls"),
            }, delegate(int i)
            {
                switch (i)
                {
                    case 0: Actions.RestockMerchant(); break;
                    case 1: Actions.BuyAllMerchant(); break;
                    case 2: Actions.AddWallet(100000); break;
                    case 3: Actions.ResetRerolls(); break;
                }
            }, 4);

            p.Section(L.T("货架", "Stock"), c.MerchantOpen ? null : L.T("提示：非商人回合时购买会被游戏拒绝", "Purchases are rejected outside merchant rounds"));
            if (c.merchantOffers == null || c.merchantOffers.Length == 0)
            {
                p.Note(L.T("当前没有商人货架数据。", "No merchant offers right now."));
                return;
            }

            for (int i = 0; i < c.merchantOffers.Length; i++)
            {
                ForgeMerchantOffer offer = c.merchantOffers[i];
                if (offer == null) continue;
                float s = UiScale;
                Rect row = new Rect(p.X, p.Y, p.W, 42f * s);
                Bordered(row, PanelAlt, Line, 8);
                string name = offer.id;
                if (offer.IsSword) name = L.T("基础剑", "Basic sword");
                else
                {
                    ForgeItemDefinition def = ForgeEconomy.Item(offer.id);
                    if (def != null) name = def.LocalizedName;
                }
                Lbl(new Rect(row.x + 12f * s, row.y + 6f * s, row.width - 200f * s, 18f * s), name, 13, Text, TextAnchor.MiddleLeft, true);
                Lbl(new Rect(row.x + 12f * s, row.y + 22f * s, row.width - 200f * s, 14f * s),
                    L.T("价格 ", "Price ") + Game.Money(offer.price) + "   ·   " + L.T("库存 ", "stock ") + offer.Remaining,
                    11, Dim);

                Rect buy = new Rect(row.x + row.width - 96f * s, row.y + 7f * s, 84f * s, 28f * s);
                bool soldOut = offer.Remaining <= 0;
                if (Btn(buy, soldOut ? L.T("已售罄", "Sold out") : L.T("购买", "Buy"), 12, soldOut ? BtnKind.Flat : BtnKind.Primary, !soldOut))
                {
                    bool ok;
                    try { ok = room.BuyMerchant(i); }
                    catch (Exception) { ok = false; }
                    Toast.Push(ok ? L.T("已购买", "Purchased") : L.T("购买失败", "Purchase failed"), ok);
                }
                p.Y += row.height + 6f * s;
            }
        }

        // ==================== 页：玩家 ====================

        private static void PagePlayer(Page p)
        {
            GPT6ForgeRoom room = Game.Room;
            p.Section(L.T("移动", "Movement"), L.T("修改角色的移动速度（原生 1.9）", "Native walk speed is 1.9"));
            p.SliderSetting("walk_speed", L.T("移动速度倍率", "Walk speed multiplier"), 0.3f, 6f, null,
                delegate(float v) { return "x" + v.ToString("0.0") + "  (" + (1.9f * v).ToString("0.0") + ")"; },
                delegate(float v) { Actions.SetWalkSpeed(v); });

            p.Section(L.T("视角", "Camera"), null);
            p.SliderSetting("hit_sensitivity", L.T("鼠标灵敏度", "Mouse sensitivity"), 0.02f, 0.25f, null,
                delegate(float v) { return v.ToString("0.000"); },
                delegate(float v) { Actions.SetSensitivity(v); });

            p.SliderSetting("fov", L.T("视野角度", "Field of view"), 45f, 115f, null,
                delegate(float v) { return v.ToString("0") + "°"; },
                delegate(float v) { Actions.SetFov(v); });

            p.Section(L.T("传送", "Teleport"), L.T("快速移动到关键工作台附近", "Jump next to a station"));
            p.Buttons(new[]
            {
                L.T("铁砧", "Anvil"),
                L.T("贡品台", "Tribute"),
                L.T("熔炉", "Furnace"),
                L.T("货物点", "Issue point"),
            }, delegate(int i)
            {
                string[] anchors = { "anvil", "tribute", "furnace", "issue" };
                string[] zh = { "铁砧", "贡品台", "熔炉", "货物点" };
                string[] en = { "anvil", "tribute", "furnace", "issue point" };
                Actions.Teleport(anchors[i], L.English ? en[i] : zh[i]);
            }, 4);
            p.Buttons(new[] { L.T("传送到商人", "To merchant"), L.T("解除鼠标锁定", "Unlock cursor"), L.T("锁定鼠标", "Lock cursor") }, delegate(int i)
            {
                switch (i)
                {
                    case 0: Actions.Teleport("merchant", L.T("商人", "merchant")); break;
                    case 1: Game.SetCursor(true); break;
                    case 2: Game.SetCursor(false); break;
                }
            }, 3);

            if (room != null)
            {
                p.Gap(8f);
                p.Section(L.T("运行时信息", "Runtime"), null);
                p.KeyValue(L.T("玩家状态", "Player"), room.player != null && room.player.working ? L.T("正在工作台", "Working") : L.T("自由移动", "Free"));
                p.KeyValue(L.T("准星目标", "Focus"), room.FocusedSword == null ? "—" : room.FocusedSword.DisplayName);
                p.KeyValue(L.T("剑总数", "Swords"), Game.AllSwords().Count.ToString());
            }
        }

        // ==================== 页：系统 ====================

        private static void PageSystem(Page p)
        {
            p.Section(L.T("外观", "Appearance"), L.T("主题只影响本模组界面，不会改动游戏本身的显示设置", "Only affects this menu, never the game's own settings"));
            p.SegmentedRow(L.T("模式", "Mode"), new[] { L.T("深色", "Dark"), L.T("浅色", "Light") }, LightMode ? 1 : 0,
                delegate(int v)
                {
                    if ((v == 1) != LightMode)
                    {
                        Cfg.SB("light_theme", v == 1);
                        Theme.Invalidate();
                    }
                });
            p.Gap(8f);
            Lbl(new Rect(p.X, p.Y, p.W, 16f * UiScale), L.T("主题色", "Accent"), 11, Dim);
            p.Y += 20f * UiScale;
            p.AccentRow();

            bool autoScale = Cfg.B("ui_scale_auto", true);
            bool nAuto = p.Toggle(L.T("自动适配屏幕", "Auto scale"), L.T("按屏幕高度自动计算缩放（推荐）", "Scale from screen height (recommended)"),
                "ui_scale_auto", autoScale);
            if (nAuto != autoScale) Theme.Invalidate();

            if (!Cfg.B("ui_scale_auto", true))
            {
                p.SliderSetting("ui_scale", L.T("界面缩放", "UI scale"), 0.7f, 2.2f,
                    L.T("小屏可调小，4K 屏可调大", "Smaller for small screens, larger for 4K"),
                    delegate(float v) { return v.ToString("0.00") + "x"; },
                    delegate(float v) { Cfg.SF("ui_scale", v); Theme.Invalidate(); });
            }

            p.SliderSetting("font_boost", L.T("字体额外放大", "Extra font size"), 0f, 4f,
                L.T("觉得字小就往右拉，实时生效", "Drag right if text feels small"),
                delegate(float v) { return "+" + Mathf.RoundToInt(v) + "px"; },
                delegate(float v) { Theme.Invalidate(); });
            p.SliderSetting("ui_alpha", L.T("面板不透明度", "Panel opacity"), 0.75f, 1f, null,
                delegate(float v) { return Mathf.RoundToInt(v * 100f) + "%"; });

            p.Gap(4f);
            bool en = L.English;
            bool nen = p.Toggle(L.T("界面语言：英文", "English UI"), L.T("只切换模组界面语言，游戏本身保持原语言", "Only this menu; the game stays in its own language"),
                "lang_en", en);
            if (nen != en) L.English = nen;

            p.Gap(6f);
            p.Buttons(new[]
            {
                L.T("显示数据浮层", "Show HUD"),
                L.T("悬停提示", "Tooltips"),
                L.T("重置窗口位置", "Reset window"),
            }, delegate(int i)
            {
                switch (i)
                {
                    case 0: Cfg.SB("show_hud", !Cfg.B("show_hud", true)); break;
                    case 1: Cfg.SB("show_tooltips", !Cfg.B("show_tooltips", true)); break;
                    case 2:
                        UiState.WindowX = -1f;
                        UiState.WindowY = -1f;
                        Cfg.SF("window_x", -1f);
                        Cfg.SF("window_y", -1f);
                        Toast.Push(L.T("窗口位置已重置", "Window position reset"));
                        break;
                }
            }, 3);

            bool dbgLog = Cfg.B("debug_log", false);
            bool nDbg = p.Toggle(L.T("调试日志", "Debug log"),
                L.T("把每次敲击的判定结果写进 MelonLoader 日志，排查问题时再开", "Logs every strike decision to the MelonLoader log"),
                "debug_log", dbgLog);
            if (nDbg != dbgLog) Toast.Push(nDbg ? L.T("调试日志：开", "Debug log: on") : L.T("调试日志：关", "Debug log: off"));

            p.Section(L.T("游戏本体语言", "Game language"), L.T("改的是游戏自己的语言设置，与本修改器界面语言无关", "Changes the game's own language setting, independent of this menu"));
            bool ensureCn = Cfg.B("ensure_chinese", true);
            bool nEnsure = p.Toggle(L.T("启动时确保简体中文", "Ensure Chinese on launch"), L.T("检测到英文时自动切回中文；关掉后完全交给游戏自己记忆", "Switches back to Chinese if the game starts in English"),
                "ensure_chinese", ensureCn);
            if (nEnsure != ensureCn) Toast.Push(nEnsure ? L.T("下次启动会确保中文", "Chinese enforced on next launch") : L.T("不再自动切换语言", "Auto language switch off"), nEnsure);
            p.Gap(4f);            string currentLang = "?";
            try { currentLang = ForgeLocalization.LanguageCode; }
            catch (Exception) { }
            p.KeyValue(L.T("当前语言", "Current"), currentLang);
            p.Buttons(new[]
            {
                L.T("简体中文", "简体中文"),
                L.T("繁體中文", "繁體中文"),
                L.T("한국어", "한국어"),
                L.T("English", "English"),
            }, delegate(int i)
            {
                string[] codes = { "zh-CN", "zh-TW", "ko", "en" };
                string[] names = { "简体中文", "繁體中文", "한국어", "English" };
                Actions.SetGameLanguage(codes[i], names[i]);
            }, 4);
            p.Note(L.T("只有点击按钮才会写入；本修改器加载时不会改动任何语言设置。",
                       "Only written when you press a button; loading this mod never changes language settings."));
            p.Section(L.T("热键", "Hotkeys"), L.T("点击后按下新的按键即可绑定", "Click, then press a key"));
            p.KeybindRow(L.T("打开/关闭菜单", "Toggle menu"), "key_menu", "F1");
            p.KeybindRow(L.T("数据浮层", "HUD"), "key_hud", "F2");
            p.KeybindRow(L.T("加 1 万金币", "Add 10K gold"), "key_gold", "F5");
            p.KeybindRow(L.T("强制通关本回合", "Force round win"), "key_win", "F6");
            p.KeybindRow(L.T("自动敲击开关", "Toggle auto strike"), "key_auto", "F7");
            p.KeybindRow(L.T("修复断剑", "Repair sword"), "key_repair", "F8");

            p.Section(L.T("游戏节奏", "Game pace"), L.T("时间缩放会影响游戏内所有动画与计时", "Time scale affects all in-game timers"));
            p.SliderSetting("game_speed", L.T("游戏速度", "Game speed"), 0.25f, 3f, null,
                delegate(float v) { return v.ToString("0.00") + "x"; },
                delegate(float v) { Actions.SetTimeScale(v); });

            p.Section(L.T("存档", "Saves"), L.T("备份保存在 UserData/Moon/Backups", "Backups live in UserData/Moon/Backups"));
            p.Buttons(new[]
            {
                L.T("立即存档", "Save now"),
                L.T("备份存档", "Backup"),
                L.T("还原最新备份", "Restore latest"),
                L.T("删除当前存档", "Delete save"),
            }, delegate(int i)
            {
                switch (i)
                {
                    case 0: Actions.SaveNow(); break;
                    case 1: Actions.BackupSave(); break;
                    case 2:
                        {
                            List<string> list = Actions.ListBackups();
                            if (list.Count == 0) Toast.Push(L.T("没有备份", "No backups"), false);
                            else Actions.RestoreBackup(list[0]);
                            break;
                        }
                    case 3:
                        if (!UiState.ConfirmSaveWipe)
                        {
                            UiState.ConfirmSaveWipe = true;
                            Toast.Push(L.T("再次点击确认删除存档", "Click again to confirm"), false);
                        }
                        else
                        {
                            UiState.ConfirmSaveWipe = false;
                            Actions.DiscardSave();
                        }
                        break;
                }
            }, 4);
            if (UiState.ConfirmSaveWipe)
            {
                p.Note(L.T("删除后本局进度不可恢复（除非已备份）。", "Deleting discards the current run unless you backed it up."));
            }

            p.Section(L.T("关于", "About"), null);
            p.KeyValue(L.T("版本", "Version"), Version);
            p.KeyValue(L.T("补丁", "Patches"),
                (Patches.ApplyOk ? "Outcome " : "") + (Patches.SettleOk ? "Settle " : "") +
                (Patches.CreditOk ? "Gold " : "") + (Patches.TickOk ? "Input" : ""));
            p.KeyValue(L.T("热键说明", "Keys"), "F1 / F2 / F5 / F6 / F7 / F8");
            p.Note(L.T("本工具完全免费。仅本机生效，不修改游戏文件本体。",
                       "Free tool. Local only, does not modify game files."));
        }

        // ==================== 简易布局助手 ====================

        private sealed class Page
        {
            internal readonly float X;
            internal readonly float W;
            internal float Y;
            private readonly float _top;

            internal Page(float x, float y, float w)
            {
                X = x;
                Y = y;
                W = w;
                _top = y;
            }

            internal float Height
            {
                get { return Y - _top; }
            }

            internal float Bottom
            {
                get { return Y; }
            }

            private static float S
            {
                get { return UiScale; }
            }

            internal void Gap(float h)
            {
                Y += h * S;
            }

            internal void Note(string text)
            {
                float s = S;
                float h = Mathf.Max(26f * s, TextHeight(text, 11, W - 20f * s) + 14f * s);
                Rect r = new Rect(X, Y, W, h);
                Bordered(r, Alpha(Text, 0.045f), Line, 8);
                Box(new Rect(r.x + 1f, r.y + 1f, 3f * s, r.height - 2f), Alpha(Accent, 0.7f), 2);
                LblWrap(new Rect(r.x + 14f * s, r.y + 7f * s, r.width - 26f * s, r.height - 14f * s), text, 11, Dim);
                Y += h + 8f * s;
            }

            internal void KeyValue(string key, string value)
            {
                float s = S;
                float h = 27f * s;
                Lbl(new Rect(X, Y, W * 0.42f, h), key, 11, Dim);
                Lbl(new Rect(X + W * 0.42f, Y, W * 0.58f, h), value, 11, Text, TextAnchor.MiddleRight, true);
                Y += h;
            }

            internal void Section(string title, string desc = null)
            {
                float s = S;
                Y += 8f * s;
                Y += SectionHeader(new Rect(X, Y, W, 60f * s), title, desc, Accent);
                Y += 8f * s;
            }

            internal bool Toggle(string title, string desc, string key, bool value)
            {
                float s = S;
                float h = string.IsNullOrEmpty(desc) ? 38f * s : 58f * s;
                Rect r = new Rect(X, Y, W, h);
                bool v = ToggleRow(r, key, title, desc, value);
                Y += h + 4f * s;
                if (v != value && !string.IsNullOrEmpty(key)) Cfg.SB(key, v);
                return v;
            }

            internal void Buttons(string[] labels, Action<int> onClick, int perRow, float height = 34f, BtnKind kind = BtnKind.Ghost)
            {
                float s = S;
                float gap = 6f * s;
                float w = (W - gap * (perRow - 1)) / perRow;
                float h = height * s;
                for (int i = 0; i < labels.Length; i++)
                {
                    int col = i % perRow;
                    int row = i / perRow;
                    Rect r = new Rect(X + col * (w + gap), Y + row * (h + gap), w, h);
                    if (Btn(r, labels[i], 12, kind))
                    {
                        int idx = i;
                        if (onClick != null) onClick(idx);
                    }
                }
                int rows = (labels.Length + perRow - 1) / perRow;
                Y += rows * (h + gap) + 2f * s;
            }

            internal void TextApply(string label, ref string value, Action<string> apply, string button)
            {
                float s = S;
                Lbl(new Rect(X, Y, W, 16f * s), label, 11, Dim);
                Y += 18f * s;
                float bh = 30f * s;
                float bw = 74f * s;
                Rect field = new Rect(X, Y, W - bw - 8f * s, bh);
                value = TextField(field, value, 12);
                Rect btn = new Rect(X + W - bw, Y, bw, bh);
                if (Btn(btn, button, 12, BtnKind.Primary)) apply(value);
                Y += bh + 8f * s;
            }

            internal int SegmentedRow(string label, string[] items, int index, Action<int> onChange = null)
            {
                float s = S;
                Lbl(new Rect(X, Y, W, 16f * s), label, 11, Dim);
                Y += 18f * s;
                Rect r = new Rect(X, Y, W, 28f * s);
                int v = Segmented(r, items, index);
                Y += 28f * s + 6f * s;
                if (v != index && onChange != null) onChange(v);
                return v;
            }

            internal void SliderSetting(string key, string title, float min, float max, string desc,
                Func<float, string> fmt, Action<float> onChanged = null)
            {
                float s = S;
                float stored = Cfg.F(key, Mathf.Clamp(1f, min, max));
                if (stored < min || stored > max) stored = Mathf.Clamp(stored, min, max);
                Lbl(new Rect(X, Y, W - 120f * s, 22f * s), title, 11, Dim);
                Lbl(new Rect(X + W - 120f * s, Y, 120f * s, 22f * s), fmt(stored), 11, Text, TextAnchor.MiddleRight, true);
                Y += 20f * s;
                float v = Slider(new Rect(X, Y, W, 20f * s), stored, min, max, key);
                Y += 24f * s;
                if (Mathf.Abs(v - stored) > 0.0001f)
                {
                    Cfg.SF(key, v);
                    if (onChanged != null) onChanged(v);
                }
                if (!string.IsNullOrEmpty(desc))
                {
                    LblWrap(new Rect(X, Y, W, 16f * s), desc, 10, Faint);
                    Y += 18f * s;
                }
                Y += 2f * s;
            }

            internal float SliderRow(string title, float value, float min, float max, Func<float, string> fmt)
            {
                float s = S;
                Lbl(new Rect(X, Y, W - 120f * s, 22f * s), title, 11, Dim);
                Lbl(new Rect(X + W - 110f * s, Y, 110f * s, 18f * s), fmt(value), 11, Text, TextAnchor.MiddleRight, true);
                Y += 20f * s;
                float v = Slider(new Rect(X, Y, W, 20f * s), value, min, max, "gen_grade");
                Y += 24f * s;
                return v;
            }

            internal void KeybindRow(string label, string key, string fallback)
            {
                float s = S;
                Rect r = new Rect(X, Y, W, 28f * s);
                Lbl(new Rect(X, Y, W - 120f * s, r.height), label, 12, Text);
                bool capturing = UiState.CaptureKeyFor == key;
                string current = Cfg.S(key, fallback);
                Rect btn = new Rect(X + W - 110f * s, Y + 2f * s, 110f * s, 24f * s);
                if (Btn(btn, capturing ? L.T("请按键…", "Press key…") : current, 11, capturing ? BtnKind.Primary : BtnKind.Ghost))
                {
                    UiState.CaptureKeyFor = capturing ? null : key;
                }
                Y += r.height + 2f * s;
            }

            internal void AccentRow()
            {
                float s = S;
                float gap = 8f * s;
                float size = 32f * s;
                for (int i = 0; i < AccentNames.Length; i++)
                {
                    Rect r = new Rect(X + i * (size + gap), Y, size, size);
                    bool active = AccentIndex == i;
                    if (active)
                    {
                        Box(new Rect(r.x - 2f * s, r.y - 2f * s, r.width + 4f * s, r.height + 4f * s), Alpha(AccentAt(i), 0.35f), 9);
                    }
                    Box(r, AccentAt(i), 7);
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                    {
                        Cfg.SI("accent", i);
                        Theme.Invalidate();
                        Toast.Push(L.T("主题色：" + AccentNames[i], "Accent: " + AccentNamesEn[i]));
                    }
                }
                Y += size + 10f * s;
            }
        }
    }
}
