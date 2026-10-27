// Core/Config.cs — 配置层：MelonPreferences 封装（懒创建 + 统一类型访问）+ 运行时 UI 状态
using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace Moon.Core
{
    /// <summary>轻量配置读写：所有设置都落在 UserData/MelonPreferences.cfg。</summary>
    internal static class Cfg
    {
        internal const string CatId = "Moon";
        private static MelonPreferences_Category _cat;
        private static readonly Dictionary<string, object> _cache = new Dictionary<string, object>();

        private static MelonPreferences_Category Cat
        {
            get
            {
                if (_cat == null)
                {
                    _cat = MelonPreferences.CreateCategory(CatId, "月亮 · Moon");
                }
                return _cat;
            }
        }

        /// <summary>
        /// 单项配置。同一个键如果曾经用别的类型读过（例如老版本把 font_boost 当 int），
        /// MelonPreferences 会抛 InvalidCastException；这里兜住并退回内存值，绝不让界面崩掉。
        /// </summary>
        internal sealed class Slot<T>
        {
            private MelonPreferences_Entry<T> _entry;
            private T _local;
            private bool _warned;

            internal Slot(string key, T def, string desc)
            {
                _local = def;
                try
                {
                    _entry = Cat.CreateEntry<T>(key, def, null, desc);
                }
                catch (Exception e)
                {
                    _entry = null;
                    if (!_warned)
                    {
                        _warned = true;
                        MelonLogger.Warning("[月亮] 配置项 " + key + " 类型与配置文件不一致，本次改用内存值（删掉该行即可恢复保存）: " + e.Message);
                    }
                }
            }

            internal T Value
            {
                get
                {
                    if (_entry != null)
                    {
                        try { return _entry.Value; }
                        catch (Exception) { _entry = null; }
                    }
                    return _local;
                }
                set
                {
                    _local = value;
                    if (_entry != null)
                    {
                        try { _entry.Value = value; }
                        catch (Exception) { _entry = null; }
                    }
                }
            }
        }

        internal static Slot<T> E<T>(string key, T def, string desc = null)
        {
            string cacheKey = typeof(T).Name + ":" + key;
            object o;
            if (_cache.TryGetValue(cacheKey, out o)) return (Slot<T>)o;
            Slot<T> s = new Slot<T>(key, def, desc);
            _cache[cacheKey] = s;
            return s;
        }

        internal static bool B(string key, bool def = false)
        {
            return E<bool>(key, def).Value;
        }

        internal static int I(string key, int def = 0)
        {
            return E<int>(key, def).Value;
        }

        internal static float F(string key, float def = 0f)
        {
            return E<float>(key, def).Value;
        }

        internal static string S(string key, string def = "")
        {
            return E<string>(key, def).Value;
        }

        internal static void SB(string key, bool v) { E<bool>(key, false).Value = v; }
        internal static void SI(string key, int v) { E<int>(key, 0).Value = v; }
        internal static void SF(string key, float v) { E<float>(key, 0f).Value = v; }
        internal static void SS(string key, string v) { E<string>(key, "").Value = v; }

        internal static void Save()
        {
            try { MelonPreferences.Save(); }
            catch (Exception) { }
        }
    }

    /// <summary>不落盘的临时界面状态。</summary>
    internal static class UiState
    {
        internal static bool Open;
        internal static bool HudEdit;
        internal static int Tab;
        internal static float WindowX = -1f;
        internal static float WindowY = -1f;
        internal static Vector2 Scroll;
        internal static float ContentHeight;
        internal static string CaptureKeyFor;
        internal static string GoldInput = "50000";
        internal static string CreditInput = "50";
        internal static string GradeInput = "10";
        internal static string ValueInput = "100000";
        internal static string QuotaInput = "1000";
        internal static string RoundInput = "10";
        internal static string SwordCountInput = "3";
        internal static bool ConfirmAchievements;
        internal static bool ConfirmSaveWipe;
        internal static int ForgeSpawnGrade = 10;
        internal static int ForgeSpawnKind;
        internal static int ForgeSpawnParent;
        internal static int ForgeSpawnBranch;
        internal static string Root;
    }
}
