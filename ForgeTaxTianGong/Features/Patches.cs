// Features/Patches.cs — Harmony 补丁：强制敲击结果 / 结算强制成功 / 金币倍率 / 打开菜单时暂停玩家控制
using System;
using HarmonyLib;
using MelonLoader;
using Moon.Core;

namespace Moon.Features
{
    internal static class Patches
    {
        private static HarmonyLib.Harmony _harmony;

        internal static bool ApplyOk { get; private set; }
        internal static bool SettleOk { get; private set; }
        internal static bool CreditOk { get; private set; }
        internal static bool TickOk { get; private set; }

        internal static void Install()
        {
            try
            {
                _harmony = new HarmonyLib.Harmony("com.tiangong.forgetax");
            }
            catch (Exception e)
            {
                MelonLogger.Error("[月亮] Harmony 初始化失败: " + e.Message);
                return;
            }

            ApplyOk = Try("ForgeEconomy.Apply", delegate
            {
                return AccessTools.Method(typeof(ForgeEconomy), "Apply", new[]
                {
                    typeof(GPT6Sword), typeof(ForgeOutcome), typeof(ForgeCampaign),
                    typeof(ForgeEnhancementBalance), typeof(Func<double>), typeof(bool)
                });
            }, "ApplyPrefix");

            SettleOk = Try("ForgeCampaign.Settle", delegate
            {
                return AccessTools.Method(typeof(ForgeCampaign), "Settle", new[]
                {
                    typeof(double), typeof(Func<double>), typeof(bool)
                });
            }, "SettlePrefix");

            CreditOk = Try("ForgeCampaign.CreditSettlement", delegate
            {
                return AccessTools.Method(typeof(ForgeCampaign), "CreditSettlement", new[]
                {
                    typeof(double), typeof(bool)
                });
            }, "CreditPostfix", true);

            TickOk = Try("GPT6ForgeRoom.Update", delegate
            {
                return AccessTools.Method(typeof(GPT6ForgeRoom), "Update", Type.EmptyTypes);
            }, "JumpRoomUpdate");

            Try("GPT6FirstPerson.Update", delegate
            {
                return AccessTools.Method(typeof(GPT6FirstPerson), "Update", Type.EmptyTypes);
            }, "JumpPlayerUpdate");

            MelonLogger.Msg("[月亮] 补丁状态 Apply=" + ApplyOk + " Settle=" + SettleOk +
                            " Credit=" + CreditOk + " Tick=" + TickOk);
        }

        private static bool Try(string tag, Func<System.Reflection.MethodInfo> resolve, string prefix, bool postfix = false)
        {
            try
            {
                System.Reflection.MethodInfo target = resolve();
                if (target == null)
                {
                    MelonLogger.Warning("[月亮] 找不到补丁目标: " + tag);
                    return false;
                }
                System.Reflection.MethodInfo hook = typeof(Patches).GetMethod(prefix,
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                HPatch patch = postfix ? new HPatch(null, new HarmonyMethod(hook)) : new HPatch(new HarmonyMethod(hook), null);
                _harmony.Patch(target, prefix: patch.Prefix, postfix: patch.Postfix);
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[月亮] 补丁安装失败 " + tag + ": " + e.Message);
                return false;
            }
        }

        private struct HPatch
        {
            internal readonly HarmonyMethod Prefix;
            internal readonly HarmonyMethod Postfix;

            internal HPatch(HarmonyMethod pre, HarmonyMethod post)
            {
                Prefix = pre;
                Postfix = post;
            }
        }

        // ---------- 补丁实现 ----------

        /// <summary>
        /// 敲击结果改写：在结算入口（ForgeEconomy.Apply）把结果换成玩家选定的那一档。
        /// 注意：游戏在调用 Apply 之前就已经把「掷出的结果」记录并显示在界面上，
        /// 所以这里改的是结算内容（价值 / 耐久 / 收益 / 剑是否断裂），
        /// 界面上的结果文字仍然是原生掷出的那个 —— 见 README 的说明。
        /// </summary>
        private static void ApplyPrefix(ref ForgeOutcome raw)
        {
            try
            {
                int mode = Cfg.I("force_outcome", 0);
                if (mode >= 1 && mode <= 5)
                {
                    raw = (ForgeOutcome)(mode - 1);
                    return;
                }
                if (Cfg.B("never_break", false) && raw == ForgeOutcome.Destruction)
                {
                    raw = ForgeOutcome.Hold;
                }
            }
            catch (Exception) { }
        }

        /// <summary>结算强制成功：把贡品抬到配额三倍，复用游戏原生判定（含超额奖励）。</summary>
        private static void SettlePrefix(ref double tribute, ForgeCampaign __instance)
        {
            try
            {
                if (Cfg.B("force_win", false) && __instance != null && !__instance.settled && !__instance.finished)
                {
                    tribute = Math.Max(tribute, __instance.Quota * 3.0);
                }
            }
            catch (Exception) { }
        }

        /// <summary>结算收益倍率。</summary>
        private static void CreditPostfix(ref double __result, ForgeCampaign __instance)
        {
            try
            {
                float mult = Cfg.F("gold_mult", 1f);
                if (__instance == null || mult <= 1.001f) return;
                double extra = __result * (mult - 1.0);
                if (extra <= 0.0) return;
                __instance.wallet += extra;
                __instance.Record.goldEarned += extra;
                __result += extra;
            }
            catch (Exception) { }
        }

        /// <summary>菜单打开时暂停房间输入，避免点击穿透。</summary>
        private static bool JumpRoomUpdate()
        {
            return !UiState.Open && !UiState.HudEdit;
        }

        /// <summary>菜单打开时暂停玩家移动/视角，避免鼠标拖动时镜头乱转。</summary>
        private static bool JumpPlayerUpdate()
        {
            return !UiState.Open && !UiState.HudEdit;
        }
    }
}
