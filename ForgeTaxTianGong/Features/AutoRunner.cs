// Features/AutoRunner.cs — 每帧驱动：自动敲击等持续型功能
using System;
using MelonLoader;
using Moon.Core;
using Moon.Data;
using UnityEngine;

namespace Moon.Features
{
    internal static class AutoRunner
    {
        private static float _nextStrike;
        private static GPT6Sword _lastSword;

        internal static void Tick()
        {
            try
            {
                TickAutoStrike();
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[月亮] 自动敲击异常: " + e.Message);
                Cfg.SB("auto_strike", false);
            }
        }

        private static void TickAutoStrike()
        {
            if (!Cfg.B("auto_strike", false)) return;
            GPT6ForgeRoom room = Game.Room;
            if (room == null) return;

            GPT6Sword sword = room.OnAnvil;
            if (sword == null || sword.broken)
            {
                _lastSword = null;
                return;
            }
            if (sword != _lastSword)
            {
                _lastSword = sword;
                _nextStrike = 0f;
            }

            float interval = Mathf.Clamp(Cfg.F("auto_strike_interval", 0.7f), 0.15f, 3f);
            if (Time.unscaledTime < _nextStrike) return;
            if (!room.StrikeReady) return;
            _nextStrike = Time.unscaledTime + interval;
            room.Strike();
        }

        internal static void Reset()
        {
            _nextStrike = 0f;
            _lastSword = null;
        }
    }
}
