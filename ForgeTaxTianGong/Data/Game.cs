// Data/Game.cs — 数据访问层：房间/战役/平衡/目标剑的统一入口 + 反射工具 + 生成与传送
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace Moon.Data
{
    internal static class Game
    {
        private static GPT6ForgeRoom _room;

        internal static GPT6ForgeRoom Room
        {
            get
            {
                if (_room == null)
                {
                    _room = UnityEngine.Object.FindAnyObjectByType<GPT6ForgeRoom>();
                }
                return _room;
            }
        }

        internal static ForgeCampaign Campaign
        {
            get
            {
                GPT6ForgeRoom r = Room;
                if (r == null) return null;
                try { return r.Campaign; }
                catch (Exception) { return null; }
            }
        }

        internal static ForgeEnhancementBalance Balance
        {
            get
            {
                GPT6ForgeRoom r = Room;
                return r == null ? null : r.enhancementBalance;
            }
        }

        /// <summary>目标剑优先级：铁砧 → 手持 → 准星。</summary>
        internal static GPT6Sword Target
        {
            get
            {
                GPT6ForgeRoom r = Room;
                if (r == null) return null;
                if (r.OnAnvil != null) return r.OnAnvil;
                if (r.Held != null) return r.Held;
                return r.FocusedSword;
            }
        }

        internal static bool Ready
        {
            get { return Room != null && Campaign != null && Balance != null; }
        }

        internal static int GradeCap
        {
            get
            {
                ForgeEnhancementBalance b = Balance;
                return b == null ? 99 : ForgeEconomy.GradeCap(b);
            }
        }

        /// <summary>当前所有剑（含地上、铁砧、手持、架子上）。</summary>
        internal static List<GPT6Sword> AllSwords()
        {
            List<GPT6Sword> list = new List<GPT6Sword>();
            GPT6ForgeRoom r = Room;
            if (r == null) return list;
            try
            {
                IEnumerator<GPT6Sword> it = r.Swords.GetEnumerator();
                while (it.MoveNext())
                {
                    GPT6Sword s = it.Current;
                    if (s != null) list.Add(s);
                }
            }
            catch (Exception) { }
            return list;
        }

        internal static void Refresh()
        {
            try
            {
                GPT6ForgeRoom r = Room;
                if (r != null) r.RefreshDisplays();
            }
            catch (Exception) { }
        }

        // ---------- 反射工具 ----------

        private static readonly FieldInfo SwordsField =
            typeof(GPT6ForgeRoom).GetField("swords", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo NextSerialMethod =
            typeof(GPT6ForgeRoom).GetMethod("NextSwordSerial", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo BusyField =
            typeof(GPT6ForgeRoom).GetField("busy", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static int NextSerial(GPT6ForgeRoom room)
        {
            try
            {
                if (room != null && NextSerialMethod != null)
                {
                    object v = NextSerialMethod.Invoke(room, null);
                    if (v is int) return (int)v;
                }
            }
            catch (Exception) { }
            return UnityEngine.Random.Range(1000, 9999);
        }

        internal static bool RoomRegistered(GPT6Sword sword)
        {
            GPT6ForgeRoom r = Room;
            if (r == null || sword == null) return false;
            try
            {
                List<GPT6Sword> list = SwordsField == null ? null : SwordsField.GetValue(r) as List<GPT6Sword>;
                if (list == null) return false;
                return list.Contains(sword);
            }
            catch (Exception) { return false; }
        }

        internal static void Register(GPT6Sword sword)
        {
            GPT6ForgeRoom r = Room;
            if (r == null || sword == null) return;
            try
            {
                List<GPT6Sword> list = SwordsField == null ? null : SwordsField.GetValue(r) as List<GPT6Sword>;
                if (list != null && !list.Contains(sword)) list.Add(sword);
            }
            catch (Exception) { }
        }

        internal static void Unregister(GPT6Sword sword)
        {
            GPT6ForgeRoom r = Room;
            if (r == null || sword == null) return;
            try
            {
                List<GPT6Sword> list = SwordsField == null ? null : SwordsField.GetValue(r) as List<GPT6Sword>;
                if (list != null) list.Remove(sword);
            }
            catch (Exception) { }
        }

        internal static void ForceBusy(bool busy)
        {
            GPT6ForgeRoom r = Room;
            if (r == null) return;
            try
            {
                if (BusyField != null) BusyField.SetValue(r, busy);
            }
            catch (Exception) { }
        }

        // ---------- 生成 / 复制 ----------

        /// <summary>在玩家面前生成一把剑。kind: 0 普通 / 1 世界树 / 2 海鲜 / 3 殡葬。</summary>
        internal static GPT6Sword SpawnSword(int grade, int kind, int parent, int branch)
        {
            GPT6ForgeRoom room = Room;
            ForgeCampaign c = Campaign;
            ForgeEnhancementBalance b = Balance;
            if (room == null || room.swordPrefab == null || room.view == null) return null;

            Transform cam = room.view.transform;
            Vector3 pos = cam.position + cam.forward * 1.25f - cam.up * 0.42f;
            Quaternion rot = Quaternion.Euler(0f, cam.eulerAngles.y, 80f);

            bool tree = kind == 1, sea = kind == 2, funeral = kind == 3;
            int bit = Mathf.Clamp(branch, 0, 1);
            GPT6Sword sword = UnityEngine.Object.Instantiate(room.swordPrefab, pos, rot);
            sword.name = "TianGong sword";
            sword.Initialize(NextSerial(room), room.sound, b,
                Mathf.Clamp(parent, 0, 2), bit, tree, sea, funeral);

            int g = Mathf.Clamp(grade, 0, GradeCap);
            if (g > 0)
            {
                sword.grade = g;
                sword.highestVisualGrade = g;
                if (c != null && b != null) ForgeEconomy.RefreshValue(sword, c, b);
                try { if (sword.Evolution != null) sword.Evolution.SyncAppearance(); }
                catch (Exception) { }
            }
            sword.debugSpawned = true;
            sword.SetPlace(GPT6Sword.Place.Floor);
            Register(sword);
            try { sword.IgnorePlayerCollision(); }
            catch (Exception) { }
            return sword;
        }

        /// <summary>把一把剑的等级与价值同步到另一把新剑上（复制）。</summary>
        internal static GPT6Sword CloneSword(GPT6Sword src)
        {
            if (src == null) return null;
            GPT6Sword copy = SpawnSword(src.grade, src.funerary ? 3 : (src.seafood ? 2 : (src.worldTree ? 1 : 0)),
                src.visualParent, src.visualBranch);
            if (copy == null) return null;
            ForgeCampaign c = Campaign;
            ForgeEnhancementBalance b = Balance;
            try
            {
                if (c != null && b != null)
                {
                    double scale = Math.Max(1e-9, copy.effects.multiplier * copy.effects.greatMultiplier * copy.effects.valueScale);
                    copy.effects.flat = src.effects.flat + (src.value - copy.value) / scale;
                    ForgeEconomy.RefreshValue(copy, c, b);
                }
            }
            catch (Exception) { }
            return copy;
        }

        internal static bool DestroySword(GPT6Sword sword)
        {
            if (sword == null) return false;
            Unregister(sword);
            try
            {
                sword.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(sword.gameObject);
                return true;
            }
            catch (Exception) { return false; }
        }

        // ---------- 传送 ----------

        internal static bool TeleportTo(string anchor)
        {
            GPT6ForgeRoom room = Room;
            if (room == null || room.player == null || room.view == null) return false;
            Transform t = null;
            switch (anchor)
            {
                case "anvil": t = room.anvilSocket; break;
                case "tribute": t = room.tributeEnd; break;
                case "furnace": t = room.furnaceMouth; break;
                case "issue": t = room.issuePoint; break;
                case "merchant": t = room.merchant != null ? room.merchant.transform : null; break;
            }
            if (t == null) return false;
            try
            {
                Vector3 pos = t.position;
                Vector3 flat = new Vector3(pos.x, 0f, pos.z);
                Vector3 origin = room.transform.position;
                // 站到目标点前方 1.1 米，避免卡在模型里
                Vector3 dir = (origin - pos);
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
                dir.Normalize();
                Vector3 stand = flat + dir * 1.1f;
                stand.y = room.player.transform.position.y;
                room.player.AdoptViewPose(stand, room.player.transform.rotation);
                return true;
            }
            catch (Exception) { return false; }
        }

        internal static void SetCursor(bool unlocked)
        {
            try
            {
                if (unlocked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    GPT6ForgeRoom room = Room;
                    if (room != null && room.player != null) room.player.Capture();
                    else { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
                }
            }
            catch (Exception) { }
        }

        internal static string Money(double v)
        {
            try { return ForgeEnhancementBalance.Money(v); }
            catch (Exception) { return v.ToString("F0"); }
        }
    }
}
