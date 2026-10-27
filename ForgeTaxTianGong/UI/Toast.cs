// UI/Toast.cs — 右下角浮动提示
using System.Collections.Generic;
using UnityEngine;
using Moon.Core;

namespace Moon.UI
{
    internal static class Toast
    {
        private sealed class Item
        {
            internal string Text;
            internal float Life;
            internal bool Good;
        }

        private const float Duration = 2.6f;
        private static readonly List<Item> _items = new List<Item>();

        internal static void Push(string text, bool good = true)
        {
            if (string.IsNullOrEmpty(text)) return;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Text == text)
                {
                    _items[i].Life = Duration;
                    _items[i].Good = good;
                    return;
                }
            }
            if (_items.Count >= 4) _items.RemoveAt(0);
            _items.Add(new Item { Text = text, Life = Duration, Good = good });
        }

        internal static void Clear()
        {
            _items.Clear();
        }

        internal static void Draw()
        {
            if (_items.Count == 0) return;
            float scale = Theme.UiScale;
            float pad = 10f * scale;
            float y = Screen.height - 18f * scale;

            for (int i = _items.Count - 1; i >= 0; i--)
            {
                Item it = _items[i];
                it.Life -= Time.unscaledDeltaTime;
                if (it.Life <= 0f)
                {
                    _items.RemoveAt(i);
                    continue;
                }

                GUIStyle st = Theme.Label(13, Theme.Text, TextAnchor.MiddleLeft);
                float tw = st.CalcSize(new GUIContent(it.Text)).x;
                float w = Mathf.Min(tw + pad * 3.4f, 460f * scale);
                float h = 38f * scale;
                float x = Screen.width - w - 18f * scale;
                y -= h + 8f * scale;

                float fade = Mathf.Clamp01(it.Life / 0.45f);
                Color accent = it.Good ? Theme.Ok : Theme.Bad;

                Color prev = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, fade);

                Rect r = new Rect(x, y, w, h);
                Widgets.Frame(r, Theme.Panel, 10);
                Widgets.RoundedFill(new Rect(r.x + 1f, r.y + 8f * scale, 3f * scale, r.height - 16f * scale), accent, 2);
                Widgets.Lbl(new Rect(r.x + pad * 1.6f, r.y, r.width - pad * 2.6f, r.height), it.Text, 13, Theme.Text);

                GUI.color = prev;
            }
        }
    }
}
