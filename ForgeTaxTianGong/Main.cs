// Main.cs — 模组入口：生命周期、补丁安装、每帧驱动与界面绘制
using System;
using MelonLoader;
using Moon.Core;
using Moon.Data;
using Moon.Features;
using Moon.UI;
using UnityEngine;

[assembly: MelonInfo(typeof(Moon.Main), "Moon", "2.1.1", "Moon")]
[assembly: MelonGame("DefaultCompany", "ForgeTax")]

namespace Moon
{
    public sealed class Main : MelonMod
    {
        private static bool _speedApplied;
        private static bool _logged;
        private static int _guiErrors;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("==================================================");
            LoggerInstance.Msg(" 月亮 v" + MenuWindow.Version + " 已加载 / Moon Cheat Menu loaded");
            LoggerInstance.Msg(" 按 F1 打开界面 · Press F1 to open the menu");
            LoggerInstance.Msg("==================================================");

            // 预热配置，保证首次打开界面时所有键都存在
            Cfg.B("show_hud", true);
            Cfg.B("show_tooltips", true);
            Cfg.B("auto_strike", false);
            Cfg.B("never_break", false);
            Cfg.B("force_win", false);
            Cfg.I("force_outcome", 0);
            Cfg.F("gold_mult", 1f);
            Cfg.F("ui_scale", 1f);
            Cfg.SF("ui_alpha", 0.98f);

            Patches.Install();
        }

        public override void OnUpdate()
        {
            try
            {
                AutoRunner.Tick();
                ApplyPendingSpeed();
                EnsureDefaults();
            }
            catch (Exception e)
            {
                LoggerInstance.Warning("更新循环异常: " + e.Message);
            }
        }

        private void ApplyPendingSpeed()
        {
            if (_speedApplied) return;
            if (Game.Room == null) return;
            _speedApplied = true;
            float speed = Cfg.F("game_speed", 1f);
            if (Mathf.Abs(speed - 1f) > 0.01f) Time.timeScale = Mathf.Clamp(speed, 0.1f, 4f);
        }

        private void EnsureDefaults()
        {
            if (_logged) return;
            if (Game.Room == null) return;
            _logged = true;
            LoggerInstance.Msg("[月亮] 已进入锻造坊，运行就绪。");
            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder("[语言] 可用: ");
                foreach (ForgeLanguageInfo li in ForgeLocalization.Languages) sb.Append(li.code).Append(" ");
                LoggerInstance.Msg(sb.ToString());
            }
            catch (Exception) { }
            // 游戏语言：默认确保简体中文（用户明确要求不要变成英文）。
            // 走游戏自己的 SetLanguage 接口并持久化，等同于在游戏语言菜单里选择；可在「系统」页关闭。
            if (Cfg.B("ensure_chinese", true))
            {
                try
                {
                    if (ForgeLocalization.LanguageCode != "zh-CN")
                    {
                        // 先只改内存中的语言（必定成功），再尝试持久化；持久化失败也不影响本次生效
                        ForgeLocalization.SetLanguage("zh-CN", false);
                        try { ForgeLocalization.SetLanguage("zh-CN", true); }
                        catch (Exception) { LoggerInstance.Warning("[月亮] 语言设置未能写入存档（本次已生效）"); }
                        LoggerInstance.Msg("[月亮] 游戏语言已设为简体中文 (zh-CN)");
                        Toast.Push(L.T("游戏语言已设为简体中文", "Game language set to Chinese"), true);
                    }
                }
                catch (Exception e)
                {
                    LoggerInstance.Warning("[月亮] 设置游戏语言失败: " + e.Message);
                }
            }
            if (Cfg.B("auto_open", false))
            {
                UiState.Tab = Mathf.Clamp(Cfg.I("auto_tab", 0), 0, 7);
                if (!UiState.Open) MenuWindow.ToggleMenu();
            }
            Toast.Push(L.T("月亮已就绪 · F1 打开", "Moon ready · press F1"), true);
        }

        public override void OnGUI()
        {
            GUISkin prevSkin = GUI.skin;
            Color prevColor = GUI.color;
            Matrix4x4 prevMatrix = GUI.matrix;
            try
            {
                MenuWindow.Draw();
            }
            catch (Exception e)
            {
                // 界面异常会中断整帧绘制（表现为文字缺一块 / 概率不显示），所以要连同堆栈一起记下来
                if (_guiErrors < 6)
                {
                    _guiErrors++;
                    LoggerInstance.Warning("界面绘制异常第 " + _guiErrors + " 次: " + e);
                }
            }
            finally
            {
                GUI.skin = prevSkin;
                GUI.color = prevColor;
                GUI.matrix = prevMatrix;
            }
        }

        public override void OnApplicationQuit()
        {
            try
            {
                Time.timeScale = 1f;
                Cfg.Save();
            }
            catch (Exception) { }
        }

        public override void OnDeinitializeMelon()
        {
            try
            {
                Time.timeScale = 1f;
                Cfg.Save();
            }
            catch (Exception) { }
        }
    }
}
