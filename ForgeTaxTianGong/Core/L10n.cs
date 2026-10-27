// Core/L10n.cs — 界面文案：默认中文（跟随用户要求），可在设置里切换英文（仅影响本模组，不动游戏语言）
using Moon.Core;

namespace Moon.Core
{
    internal static class L
    {
        internal static bool English
        {
            get { return Cfg.B("lang_en", false); }
            set { Cfg.SB("lang_en", value); }
        }

        internal static string T(string zh, string en)
        {
            return English ? en : zh;
        }
    }
}
