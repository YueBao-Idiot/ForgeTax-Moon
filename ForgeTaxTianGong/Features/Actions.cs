// Features/Actions.cs — 功能动作集：经济 / 锻造 / 回合 / 遗物 / 商人 / 玩家 / 系统
// 约定：所有动作先校验数据是否就绪，异常统一兜底并弹出提示，绝不把异常抛给游戏主循环。
using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using Moon.Core;
using Moon.Data;
using Moon.UI;
using UnityEngine;

namespace Moon.Features
{
    internal static class Actions
    {
        // ==================== 经济 ====================

        internal static bool AddWallet(double amount)
        {
            return Do(L.T("金币 +" + Game.Money(amount), "Gold +" + Game.Money(amount)), delegate
            {
                ForgeCampaign c = Game.Campaign;
                if (c == null) return false;
                c.wallet = Math.Max(0.0, c.wallet + amount);
                c.Record.goldEarned += amount;
                return true;
            });
        }

        internal static bool SetWallet(double value)
        {
            return Do(L.T("金币已设为 " + Game.Money(value), "Gold set to " + Game.Money(value)), delegate
            {
                ForgeCampaign c = Game.Campaign;
                if (c == null) return false;
                if (value > c.wallet) c.Record.goldEarned += value - c.wallet;
                c.wallet = Math.Max(0.0, value);
                return true;
            });
        }

        internal static bool AddCredits(int amount)
        {
            return Do(L.T("剑之铭刻 +" + amount, "Credits +" + amount), delegate
            {
                ForgeCampaign c = Game.Campaign;
                if (c == null) return false;
                c.swordCredits = ForgeEconomy.Increment(c.swordCredits, amount);
                c.Record.swordCreditsEarned = ForgeEconomy.Increment(c.Record.swordCreditsEarned, amount);
                return true;
            });
        }

        internal static bool SetCredits(int value)
        {
            return Do(L.T("剑之铭刻已设为 " + value, "Credits set to " + value), delegate
            {
                ForgeCampaign c = Game.Campaign;
                if (c == null) return false;
                c.swordCredits = Math.Max(0, value);
                return true;
            });
        }

        /// <summary>把本回合基础配额按百分比缩放（100 = 原样，1 = 几乎清零）。</summary>
        internal static bool SetQuotaPercent(double percent)
        {
            return Do(L.T("本回合配额 " + percent.ToString("0") + "%", "Quota " + percent.ToString("0") + "%"), delegate
            {
                ForgeCampaign c = Game.Campaign;
                if (c == null) return false;
                double baseQuota = c.BaseQuota;
                if (percent >= 99.5)
                {
                    c.resumedBaseQuota = 0.0; // 回到配置表原始配额
                }
                else
                {
                    c.resumedBaseQuota = Math.Max(1.0, baseQuota * percent / 100.0);
                }
                Game.Refresh();
                return true;
            });
        }

        internal static bool SetQuotaExact(double value)
        {
            return Do(L.T("配额锁定为 " + Game.Money(value), "Quota locked to " + Game.Money(value)), delegate
            {
                ForgeCampaign c = Game.Campaign;
                if (c == null) return false;
                c.resumedBaseQuota = Math.Max(1.0, value);
                Game.Refresh();
                return true;
            });
        }

        internal static bool PaySettlement()
        {
            return Do(L.T("已领取待结算收益", "Settlement payout claimed"), delegate
            {
                ForgeCampaign c = Game.Campaign;
                if (c == null) return false;
                c.CreditAllSettlement();
                return true;
            });
        }

        // ==================== 锻造 ====================

        internal static bool BumpGrade(int delta)
        {
            GPT6Sword s = Game.Target;
            return Do(L.T("剑等级 +" + delta, "Grade +" + delta), delegate
            {
                if (s == null) return false;
                int cap = Game.GradeCap;
                s.grade = Mathf.Clamp(s.grade + delta, 0, cap);
                s.highestVisualGrade = Math.Max(s.highestVisualGrade, s.grade);
                if (!s.broken) ForgeEconomy.RefreshValue(s, Game.Campaign, Game.Balance);
                try { if (s.Evolution != null) s.Evolution.SyncAppearance(); }
                catch (Exception) { }
                return true;
            });
        }

        internal static bool SetGrade(int grade)
        {
            GPT6Sword s = Game.Target;
            return Do(L.T("剑等级设为 " + grade, "Grade set to " + grade), delegate
            {
                if (s == null) return false;
                int cap = Game.GradeCap;
                s.grade = Mathf.Clamp(grade, 0, cap);
                s.highestVisualGrade = Math.Max(s.highestVisualGrade, s.grade);
                if (!s.broken) ForgeEconomy.RefreshValue(s, Game.Campaign, Game.Balance);
                try { if (s.Evolution != null) s.Evolution.SyncAppearance(); }
                catch (Exception) { }
                return true;
            });
        }

        internal static bool MultiplyValue(double factor)
        {
            GPT6Sword s = Game.Target;
            return Do(L.T("剑价值 x" + factor.ToString("0.#"), "Value x" + factor.ToString("0.#")), delegate
            {
                if (s == null || s.broken) return false;
                ForgeSwordEffects e = s.effects;
                double scale = Math.Max(1e-9, e.multiplier * e.greatMultiplier * e.valueScale);
                e.flat += s.value * (factor - 1.0) / scale;
                ForgeEconomy.RefreshValue(s, Game.Campaign, Game.Balance);
                return true;
            });
        }

        internal static bool SetValue(double value)
        {
            GPT6Sword s = Game.Target;
            return Do(L.T("剑价值设为 " + Game.Money(value), "Value set to " + Game.Money(value)), delegate
            {
                if (s == null || s.broken) return false;
                ForgeSwordEffects e = s.effects;
                double scale = Math.Max(1e-9, e.multiplier * e.greatMultiplier * e.valueScale);
                e.flat += (value - s.value) / scale;
                ForgeEconomy.RefreshValue(s, Game.Campaign, Game.Balance);
                return true;
            });
        }

        internal static bool Repair()
        {
            GPT6Sword s = Game.Target;
            return Do(L.T("已修复断剑", "Sword repaired"), delegate
            {
                if (s == null || !s.broken) return false;
                s.broken = false;
                s.lastOutcome = ForgeOutcome.Hold;
                s.resultStreak = 0;
                s.effects.charge = Math.Min(10, Math.Max(s.effects.charge, 3));
                ForgeEconomy.RefreshValue(s, Game.Campaign, Game.Balance);
                try { s.SetGlow(s.ForgeHeat); }
                catch (Exception) { }
                return true;
            });
        }

        internal static bool ResetStrikes()
        {
            GPT6Sword s = Game.Target;
            return Do(L.T("敲击次数已清零", "Strike count reset"), delegate
            {
                if (s == null) return false;
                s.strikes = 0;
                return true;
            });
        }

        internal static bool ChargeFull()
        {
            GPT6Sword s = Game.Target;
            return Do(L.T("狂热充能已拉满", "Fever charge filled"), delegate
            {
                if (s == null || s.broken) return false;
                s.effects.charge = 10;
                return true;
            });
        }

        internal static bool ChargeZero()
        {
            GPT6Sword s = Game.Target;
            return Do(L.T("狂热充能已清零", "Fever charge cleared"), delegate
            {
                if (s == null) return false;
                s.effects.charge = 0;
                return true;
            });
        }

        internal static bool StartFever()
        {
            GPT6Sword s = Game.Target;
            return Do(L.T("已强制进入狂热", "Fever forced"), delegate
            {
                if (s == null || s.broken) return false;
                s.effects.charge = 10;
                return ForgeEconomy.StartFever(s, Game.Campaign, Time.time);
            });
        }

        internal static bool EndFever()
        {
            GPT6Sword s = Game.Target;
            return Do(L.T("已结束狂热", "Fever ended"), delegate
            {
                if (s == null) return false;
                return ForgeEconomy.FinishFever(s, Game.Campaign, Game.Balance, Time.time);
            });
        }

        internal static bool ArmWard()
        {
            GPT6Sword s = Game.Target;
            return Do(L.T("已装备守护（下一击免损毁）", "Ward armed"), delegate
            {
                if (s == null || s.broken) return false;
                s.effects.wardArmed = true;
                return true;
            });
        }

        internal static bool ArmChalk()
        {
            GPT6Sword s = Game.Target;
            return Do(L.T("已涂粉笔", "Chalk armed"), delegate
            {
                if (s == null || s.broken) return false;
                s.effects.chalkArmed = true;
                return true;
            });
        }

        internal static bool ArmPowder()
        {
            GPT6Sword s = Game.Target;
            return Do(L.T("已撒锻粉", "Powder armed"), delegate
            {
                if (s == null || s.broken) return false;
                s.effects.powderArmed = true;
                return true;
            });
        }

        internal static bool StarCharge()
        {
            GPT6Sword s = Game.Target;
            return Do(L.T("星尘充能 +3", "Starshard +3 charge"), delegate
            {
                if (s == null || s.broken) return false;
                s.effects.charge = Math.Min(10, s.effects.charge + 3);
                return true;
            });
        }

        internal static bool HeatHeld()
        {
            return Do(L.T("已加热手持剑", "Held sword heated"), delegate
            {
                GPT6ForgeRoom r = Game.Room;
                return r != null && r.HeatHeld();
            });
        }

        internal static bool SpawnSword(int grade, int kind, int parent, int branch)
        {
            string[] kindName = L.English
                ? new[] { "Iron", "World Tree", "Seafood", "Funerary" }
                : new[] { "铁剑", "世界树", "海鲜", "殡葬" };
            return Do(L.T("已生成 " + kindName[Mathf.Clamp(kind, 0, 3)] + " +" + grade,
                          "Spawned " + kindName[Mathf.Clamp(kind, 0, 3)] + " +" + grade), delegate
            {
                return Game.SpawnSword(grade, kind, parent, branch) != null;
            }, false);
        }

        internal static bool CloneTarget()
        {
            return Do(L.T("已复制目标剑", "Target sword cloned"), delegate
            {
                return Game.CloneSword(Game.Target) != null;
            }, false);
        }

        internal static bool DestroyTarget()
        {
            return Do(L.T("已删除目标剑", "Target sword removed"), delegate
            {
                return Game.DestroySword(Game.Target);
            }, false);
        }

        internal static bool GradeAllSwords(int grade)
        {
            return Do(L.T("全部剑等级设为 " + grade, "All swords set to +" + grade), delegate
            {
                List<GPT6Sword> list = Game.AllSwords();
                if (list.Count == 0) return false;
                int cap = Game.GradeCap;
                foreach (GPT6Sword s in list)
                {
                    if (s == null || s.broken) continue;
                    s.grade = Mathf.Clamp(grade, 0, cap);
                    s.highestVisualGrade = Math.Max(s.highestVisualGrade, s.grade);
                    ForgeEconomy.RefreshValue(s, Game.Campaign, Game.Balance);
                    try { if (s.Evolution != null) s.Evolution.SyncAppearance(); }
                    catch (Exception) { }
                }
                return true;
            });
        }

        internal static bool MultiplyAllValues(double factor)
        {
            return Do(L.T("全部剑价值 x" + factor.ToString("0.#"), "All sword values x" + factor.ToString("0.#")), delegate
            {
                List<GPT6Sword> list = Game.AllSwords();
                bool any = false;
                foreach (GPT6Sword s in list)
                {
                    if (s == null || s.broken) continue;
                    ForgeSwordEffects e = s.effects;
                    double scale = Math.Max(1e-9, e.multiplier * e.greatMultiplier * e.valueScale);
                    e.flat += s.value * (factor - 1.0) / scale;
                    ForgeEconomy.RefreshValue(s, Game.Campaign, Game.Balance);
                    any = true;
                }
                return any;
            });
        }

        internal static bool CleanupFloorSwords()
        {
            return Do(L.T("已清理地上的剑", "Floor swords cleaned"), delegate
            {
                List<GPT6Sword> list = Game.AllSwords();
                int n = 0;
                foreach (GPT6Sword s in list)
                {
                    if (s == null) continue;
                    if (s.place == GPT6Sword.Place.Floor && !s.broken)
                    {
                        if (Game.DestroySword(s)) n++;
                    }
                }
                return n > 0;
            }, false);
        }

        // ==================== 回合 ====================

        internal static bool ClearMissed()
        {
            return Do(L.T("违约已清除", "Defaults cleared"), delegate
            {
                ForgeCampaign c = Game.Campaign;
                if (c == null) return false;
                c.missedTributes = 0;
                return true;
            });
        }

        internal static bool SetMissed(int n)
        {
            return Do(L.T("违约数设为 " + n, "Defaults set to " + n), delegate
            {
                ForgeCampaign c = Game.Campaign;
                if (c == null) return false;
                c.missedTributes = Mathf.Clamp(n, 0, 2);
                return true;
            });
        }

        internal static bool ExtendDeadline(int rounds)
        {
            return Do(L.T("契约期限 +" + rounds, "Deadline +" + rounds), delegate
            {
                ForgeCampaign c = Game.Campaign;
                if (c == null) return false;
                c.contractDeadlineRound = Math.Max(c.contractDeadlineRound <= 0 ? c.ContractDeadline : c.contractDeadlineRound, c.round + rounds);
                return true;
            });
        }

        internal static bool SetDeliveredFactor(double factor)
        {
            return Do(L.T("本回合交付额已调整", "Delivered amount adjusted"), delegate
            {
                GPT6ForgeRoom r = Game.Room;
                ForgeCampaign c = Game.Campaign;
                if (r == null || c == null) return false;
                r.delivered = Math.Max(0.0, c.Quota * factor);
                return true;
            });
        }

        internal static bool SetOrders(int n)
        {
            return Do(L.T("剩余订单设为 " + n, "Orders left set to " + n), delegate
            {
                GPT6ForgeRoom r = Game.Room;
                if (r == null) return false;
                r.ordersLeft = Mathf.Clamp(n, 0, 99);
                return true;
            });
        }

        internal static bool ForceWin()
        {
            return Do(L.T("已强制结算并通关本回合", "Round settled as success"), delegate
            {
                GPT6ForgeRoom r = Game.Room;
                ForgeCampaign c = Game.Campaign;
                if (r == null || c == null) return false;
                double need = c.Quota * 3.0;
                if (r.delivered < need) r.delivered = need;
                if (r.player != null && r.player.working) r.player.EndWork();
                if (!r.EndRound())
                {
                    Game.ForceBusy(false);
                    if (!r.EndRound()) return false;
                }
                return true;
            }, false);
        }

        internal static bool EndRound()
        {
            return Do(L.T("已请求结束本回合", "Round end requested"), delegate
            {
                GPT6ForgeRoom r = Game.Room;
                if (r == null) return false;
                if (r.player != null && r.player.working) r.player.EndWork();
                if (!r.EndRound())
                {
                    Game.ForceBusy(false);
                    return r.EndRound();
                }
                return true;
            }, false);
        }

        internal static bool SetEndless(bool on)
        {
            return Do(on ? L.T("无尽模式已开启", "Endless mode on") : L.T("无尽模式已关闭", "Endless mode off"), delegate
            {
                ForgeCampaign c = Game.Campaign;
                if (c == null) return false;
                c.endless = on;
                if (on)
                {
                    c.finished = false;
                    c.won = false;
                }
                return true;
            });
        }

        internal static bool ResetRerolls()
        {
            return Do(L.T("刷新次数已重置", "Rerolls reset"), delegate
            {
                ForgeCampaign c = Game.Campaign;
                if (c == null) return false;
                c.paidRerolls = 0;
                c.rerolls = 0;
                c.freeRerollUsedRound = 0;
                return true;
            });
        }

        internal static bool SetSupplyGrade(int grade)
        {
            return Do(L.T("下回合补给剑等级 " + grade, "Next supply grade " + grade), delegate
            {
                ForgeCampaign c = Game.Campaign;
                if (c == null) return false;
                c.supplyGrade = Mathf.Clamp(grade, 0, Game.GradeCap);
                return true;
            });
        }

        internal static bool RefreshShop()
        {
            return Do(L.T("商店已刷新", "Shop rerolled"), delegate
            {
                GPT6ForgeRoom r = Game.Room;
                return r != null && r.RerollShop();
            });
        }

        internal static bool RestockMerchant()
        {
            return Do(L.T("商人货架已补货", "Merchant restocked"), delegate
            {
                ForgeCampaign c = Game.Campaign;
                if (c == null) return false;
                c.merchantRound = 0;
                c.merchantSoldOut = false;
                c.PrepareMerchantStock(null);
                Game.Refresh();
                return true;
            });
        }

        internal static bool BuyAllMerchant()
        {
            return Do(L.T("已扫空商人货架", "Merchant cleared"), delegate
            {
                ForgeCampaign c = Game.Campaign;
                GPT6ForgeRoom r = Game.Room;
                if (c == null || r == null) return false;
                double backup = c.wallet;
                c.wallet = Math.Max(c.wallet, 1e12);
                int bought = 0;
                for (int i = 0; i < c.merchantOffers.Length; i++)
                {
                    try { if (r.BuyMerchant(i)) bought++; }
                    catch (Exception) { }
                }
                c.wallet = backup;
                return bought > 0;
            }, false);
        }

        // ==================== 遗物 / 图鉴 ====================

        /// <summary>所有可购买的遗物定义（排除消耗品与已停用条目）。</summary>
        internal static List<ForgeItemDefinition> RelicItems()
        {
            List<ForgeItemDefinition> list = new List<ForgeItemDefinition>();
            try
            {
                ForgeEconomyData data = ForgeEconomy.Data;
                if (data == null || data.items == null) return list;
                foreach (ForgeItemDefinition item in data.items)
                {
                    if (item == null || item.consumable) continue;
                    if (ForgeEconomy.Deferred(item.id)) continue;
                    list.Add(item);
                }
            }
            catch (Exception) { }
            return list;
        }

        internal static bool GrantRelic(string id)
        {
            ForgeItemDefinition def = null;
            try { def = ForgeEconomy.Item(id); }
            catch (Exception) { }
            string name = def == null ? id : def.LocalizedName;
            return Do(L.T("已获得遗物：" + name, "Relic granted: " + name), delegate
            {
                ForgeCampaign c = Game.Campaign;
                if (c == null || def == null) return false;
                if (c.relics == null) return false;
                int slot = -1;
                for (int i = 0; i < c.relics.Length; i++)
                {
                    if (c.relics[i] != null && c.relics[i].id == id) return false;
                }
                for (int i = 0; i < c.relics.Length; i++)
                {
                    if (c.relics[i] == null || string.IsNullOrEmpty(c.relics[i].id)) { slot = i; break; }
                }
                if (slot < 0) return false;
                c.relics[slot] = new ForgeRelic { id = id, rank = 1 };
                if (!c.unlockedRelics.Contains(id)) c.unlockedRelics.Add(id);
                try { ForgeCodex.DiscoverRelic(id); }
                catch (Exception) { }
                Game.Refresh();
                return true;
            });
        }

        internal static bool FillRelics(string id)
        {
            return Do(L.T("已填满遗物栏", "Relic slots filled"), delegate
            {
                ForgeCampaign c = Game.Campaign;
                ForgeItemDefinition def = ForgeEconomy.Item(id);
                if (c == null || c.relics == null || def == null) return false;
                bool any = false;
                for (int i = 0; i < c.relics.Length; i++)
                {
                    if (c.relics[i] == null || string.IsNullOrEmpty(c.relics[i].id))
                    {
                        bool dup = false;
                        for (int k = 0; k < c.relics.Length; k++)
                        {
                            if (c.relics[k] != null && c.relics[k].id == id) dup = true;
                        }
                        if (dup) break;
                        c.relics[i] = new ForgeRelic { id = id, rank = 1 };
                        any = true;
                    }
                }
                if (!any) return false;
                if (!c.unlockedRelics.Contains(id)) c.unlockedRelics.Add(id);
                try { ForgeCodex.DiscoverRelic(id); }
                catch (Exception) { }
                Game.Refresh();
                return true;
            });
        }

        internal static bool ClearRelics()
        {
            return Do(L.T("遗物栏已清空", "Relic slots cleared"), delegate
            {
                ForgeCampaign c = Game.Campaign;
                if (c == null || c.relics == null) return false;
                bool any = false;
                for (int i = 0; i < c.relics.Length; i++)
                {
                    if (c.relics[i] != null) { c.relics[i] = null; any = true; }
                }
                Game.Refresh();
                return any;
            });
        }

        internal static bool UnlockAllRelics()
        {
            return Do(L.T("已永久解锁全部遗物", "All relics unlocked"), delegate
            {
                ForgeCampaign c = Game.Campaign;
                List<ForgeItemDefinition> items = RelicItems();
                if (c == null || items.Count == 0) return false;
                foreach (ForgeItemDefinition item in items)
                {
                    if (!c.unlockedRelics.Contains(item.id)) c.unlockedRelics.Add(item.id);
                    try { ForgeCodex.DiscoverRelic(item.id); }
                    catch (Exception) { }
                    try { if (!string.IsNullOrEmpty(item.unlock)) ForgeAchievements.Earn(item.unlock, false); }
                    catch (Exception) { }
                }
                try { ForgeCodex.Flush(); }
                catch (Exception) { }
                Game.Refresh();
                return true;
            });
        }

        internal static bool UnlockCodex()
        {
            return Do(L.T("图鉴已全部点亮", "Codex fully unlocked"), delegate
            {
                ForgeCodexEntry[] entries = ForgeCodex.Entries;
                ForgeDiscoveries disc = ForgeCodex.Discoveries;
                if (entries == null || disc == null) return false;
                foreach (ForgeCodexEntry e in entries)
                {
                    if (e == null) continue;
                    ForgeDiscovery found = null;
                    for (int i = 0; i < disc.entries.Count; i++)
                    {
                        if (disc.entries[i] != null && disc.entries[i].id == e.id) { found = disc.entries[i]; break; }
                    }
                    if (found == null)
                    {
                        found = new ForgeDiscovery { id = e.id, best = 0 };
                        disc.entries.Add(found);
                    }
                    found.best = Math.Max(found.best, 30);
                    found.seen = true;
                }
                List<ForgeItemDefinition> relics = RelicItems();
                foreach (ForgeItemDefinition item in relics)
                {
                    try { ForgeCodex.DiscoverRelic(item.id); }
                    catch (Exception) { }
                }
                try { ForgeCodex.Flush(); }
                catch (Exception) { }
                return true;
            });
        }

        internal static bool UnlockAchievements()
        {
            return Do(L.T("已解锁全部成就", "All achievements unlocked"), delegate
            {
                ForgeAchievementDefinition[] defs = ForgeAchievements.Definitions;
                if (defs == null) return false;
                int n = 0;
                foreach (ForgeAchievementDefinition d in defs)
                {
                    if (d == null) continue;
                    try { if (ForgeAchievements.Earn(d.id, false)) n++; }
                    catch (Exception) { }
                }
                try { ForgeAchievements.Flush(); }
                catch (Exception) { }
                return n > 0;
            });
        }

        // ==================== 玩家 ====================

        internal static bool SetWalkSpeed(float value)
        {
            return Do(L.T("移动速度 " + value.ToString("0.0") + "x", "Walk speed " + value.ToString("0.0") + "x"), delegate
            {
                GPT6ForgeRoom r = Game.Room;
                if (r == null || r.player == null) return false;
                r.player.walkSpeed = 1.9f * Mathf.Clamp(value, 0.2f, 6f);
                return true;
            });
        }

        internal static bool SetSensitivity(float value)
        {
            return Do(L.T("视角灵敏度 " + value.ToString("0.00"), "Sensitivity " + value.ToString("0.00")), delegate
            {
                GPT6ForgeRoom r = Game.Room;
                if (r == null || r.player == null) return false;
                r.player.sensitivity = Mathf.Clamp(value, 0.01f, 0.5f);
                return true;
            });
        }

        internal static bool SetFov(float value)
        {
            return Do(L.T("视野 " + value.ToString("0") + "°", "FOV " + value.ToString("0") + "°"), delegate
            {
                GPT6ForgeRoom r = Game.Room;
                if (r == null || r.view == null) return false;
                r.view.fieldOfView = Mathf.Clamp(value, 40f, 120f);
                return true;
            });
        }

        internal static bool Teleport(string anchor, string label)
        {
            return Do(L.T("已传送到" + label, "Teleported to " + label), delegate
            {
                return Game.TeleportTo(anchor);
            });
        }

        // ==================== 系统 ====================

        internal static bool SaveNow()
        {
            return Do(L.T("已立即存档", "Saved now"), delegate
            {
                GPT6ForgeRoom r = Game.Room;
                return r != null && r.SaveRunNow();
            }, false);
        }

        internal static bool DiscardSave()
        {
            return Do(L.T("当前存档已删除", "Save discarded"), delegate
            {
                GPT6ForgeRoom r = Game.Room;
                return r != null && r.DiscardSavedRun();
            }, false);
        }

        internal static string BackupRoot
        {
            get { return Path.Combine(MelonEnvironment.UserDataDirectory, "Moon", "Backups"); }
        }

        internal static bool BackupSave()
        {
            return Do(L.T("存档已备份", "Save backed up"), delegate
            {
                string source = ForgeSaveStore.Root;
                if (string.IsNullOrEmpty(source) || !Directory.Exists(source)) return false;
                try { ForgeSaveStore.FlushDeferred(); }
                catch (Exception) { }
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string dest = Path.Combine(BackupRoot, stamp);
                Directory.CreateDirectory(dest);
                string[] files = Directory.GetFiles(source, "*.json");
                int n = 0;
                foreach (string f in files)
                {
                    try
                    {
                        File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), true);
                        n++;
                    }
                    catch (Exception) { }
                }
                return n > 0;
            }, false);
        }

        internal static List<string> ListBackups()
        {
            List<string> list = new List<string>();
            try
            {
                if (!Directory.Exists(BackupRoot)) return list;
                string[] dirs = Directory.GetDirectories(BackupRoot);
                Array.Sort(dirs);
                for (int i = dirs.Length - 1; i >= 0; i--) list.Add(dirs[i]);
            }
            catch (Exception) { }
            return list;
        }

        internal static bool RestoreBackup(string dir)
        {
            return Do(L.T("备份已还原，请回到主菜单重新载入", "Backup restored — return to main menu and reload"), delegate
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
                string target = ForgeSaveStore.Root;
                if (string.IsNullOrEmpty(target)) return false;
                try { ForgeSaveStore.FlushDeferred(); }
                catch (Exception) { }
                Directory.CreateDirectory(target);
                string[] files = Directory.GetFiles(dir, "*.json");
                foreach (string f in files)
                {
                    try { File.Copy(f, Path.Combine(target, Path.GetFileName(f)), true); }
                    catch (Exception) { }
                }
                try { ForgeCodex.Reload(); }
                catch (Exception) { }
                return true;
            }, false);
        }

        /// <summary>切换游戏本体语言（走游戏自己的设置接口，等同于语言菜单）。</summary>
        internal static bool SetGameLanguage(string code, string label)
        {
            return Do(L.T("游戏语言已切换为 " + label, "Game language: " + label), delegate
            {
                ForgeLocalization.SetLanguage(code, false);
                try { ForgeLocalization.SetLanguage(code, true); }
                catch (Exception) { }
                Cfg.SB("ensure_chinese", code == "zh-CN");
                return true;
            }, false);
        }

        internal static bool SetTimeScale(float scale)
        {
            return Do(L.T("游戏速度 " + scale.ToString("0.##") + "x", "Game speed " + scale.ToString("0.##") + "x"), delegate
            {
                Time.timeScale = Mathf.Clamp(scale, 0.1f, 4f);
                return true;
            });
        }

        // ==================== 通用执行器 ====================

        private static bool Do(string label, Func<bool> body, bool refresh = true)
        {
            bool ok = false;
            try
            {
                ok = body();
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[月亮] " + label + " 失败: " + e.GetType().Name + ": " + e.Message);
            }
            if (ok && refresh) Game.Refresh();
            Toast.Push(label, ok);
            return ok;
        }
    }
}
