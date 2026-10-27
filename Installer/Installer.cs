// MoonInstaller — 月亮修改器一键安装器（.NET Framework WinForms，无第三方依赖）
// 功能：自动检测 Steam 游戏目录 / 手动选择 → 安装 MelonLoader（如缺失）→ 释放修改器 → 写入默认设置
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MoonInstaller
{
    internal static class Program
    {
        internal const string Version = "1.0.0";
        internal const string ModFileName = "MoonCheatMod.dll";
        internal const string MelonVersion = "v0.7.3";
        internal const string MelonUrl = "https://github.com/LavaGang/MelonLoader/releases/download/" + MelonVersion + "/MelonLoader.x64.zip";
        internal const string GameExe = "ForgeTax.exe";

        [STAThread]
        private static int Main(string[] args)
        {
            // 命令行静默安装：MoonInstaller.exe --install "D:\...\ForgeTax" [--no-melon]
            if (args != null && args.Length >= 2 && args[0] == "--install")
            {
                string log = "";
                bool ok = Installer.Install(args[1], delegate(string s) { log += s + Environment.NewLine; Console.WriteLine(s); });
                return ok ? 0 : 1;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
            return 0;
        }
    }

    internal static class Installer
    {
        /// <summary>自动定位游戏目录：Steam 注册表 + libraryfolders.vdf。</summary>
        internal static string DetectGame()
        {
            List<string> roots = new List<string>();
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (k != null)
                    {
                        object v = k.GetValue("SteamPath");
                        if (v != null) roots.Add(v.ToString().Replace('/', '\\'));
                    }
                }
            }
            catch (Exception) { }
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                {
                    if (k != null)
                    {
                        object v = k.GetValue("InstallPath");
                        if (v != null) roots.Add(v.ToString().Replace('/', '\\'));
                    }
                }
            }
            catch (Exception) { }

            List<string> libs = new List<string>(roots);
            foreach (string root in roots)
            {
                try
                {
                    string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                    if (!File.Exists(vdf)) continue;
                    string text = File.ReadAllText(vdf);
                    foreach (Match m in Regex.Matches(text, "\"path\"\\s*\"([^\"]+)\""))
                    {
                        string p = m.Groups[1].Value.Replace("\\\\", "\\");
                        if (!libs.Contains(p)) libs.Add(p);
                    }
                }
                catch (Exception) { }
            }

            foreach (string lib in libs)
            {
                try
                {
                    string dir = Path.Combine(lib, "steamapps", "common", "ForgeTax");
                    if (File.Exists(Path.Combine(dir, Program.GameExe))) return dir;
                }
                catch (Exception) { }
            }
            return "";
        }

        internal static bool IsGameDir(string path)
        {
            return !string.IsNullOrEmpty(path) && File.Exists(Path.Combine(path, Program.GameExe));
        }

        internal static bool MelonInstalled(string gameDir)
        {
            return File.Exists(Path.Combine(gameDir, "version.dll"))
                   && Directory.Exists(Path.Combine(gameDir, "MelonLoader"));
        }

        internal static bool ModInstalled(string gameDir)
        {
            return File.Exists(Path.Combine(gameDir, "Mods", Program.ModFileName));
        }

        /// <summary>完整安装。report 用于把步骤文本回传给界面。</summary>
        internal static bool Install(string gameDir, Action<string> report)
        {
            try
            {
                if (!IsGameDir(gameDir))
                {
                    report("× 这个目录里没有找到 " + Program.GameExe + "，请选择游戏根目录。");
                    return false;
                }
                if (ProcessRunning("ForgeTax"))
                {
                    report("× 游戏正在运行，请先退出游戏再安装。");
                    return false;
                }

                report("游戏目录：" + gameDir);

                if (!MelonInstalled(gameDir))
                {
                    report("正在下载 MelonLoader " + Program.MelonVersion + " …");
                    string zip = Path.Combine(Path.GetTempPath(), "melon_" + Guid.NewGuid().ToString("N") + ".zip");
                    try
                    {
                        Download(Program.MelonUrl, zip);
                    }
                    catch (Exception ex)
                    {
                        report("× 下载 MelonLoader 失败：" + ex.Message);
                        report("  请检查网络，或手动下载：" + Program.MelonUrl);
                        report("  解压后把 version.dll 和 MelonLoader 文件夹放到游戏根目录，再点一次安装。");
                        return false;
                    }
                    report("下载完成，正在解压 …");
                    ExtractOverwrite(zip, gameDir);
                    try { File.Delete(zip); } catch (Exception) { }
                    report("MelonLoader 安装完成。");
                }
                else
                {
                    report("已检测到 MelonLoader，跳过。");
                }

                string mods = Path.Combine(gameDir, "Mods");
                Directory.CreateDirectory(mods);
                string target = Path.Combine(mods, Program.ModFileName);
                WriteResource("MoonCheatMod.dll", target);
                report("修改器已写入：Mods\\" + Program.ModFileName);

                WriteDefaultConfig(gameDir, report);
                HideConsole(gameDir);

                report("✓ 安装完成，启动游戏后按 F1 打开修改器。");
                return true;
            }
            catch (Exception e)
            {
                report("× 安装失败：" + e.Message);
                return false;
            }
        }

        internal static bool Uninstall(string gameDir, bool removeMelon, Action<string> report)
        {
            try
            {
                if (ProcessRunning("ForgeTax"))
                {
                    report("× 游戏正在运行，请先退出游戏。");
                    return false;
                }
                string mod = Path.Combine(gameDir, "Mods", Program.ModFileName);
                if (File.Exists(mod))
                {
                    File.Delete(mod);
                    report("已删除 Mods\\" + Program.ModFileName);
                }
                if (removeMelon)
                {
                    TryDelete(Path.Combine(gameDir, "version.dll"));
                    TryDeleteDir(Path.Combine(gameDir, "MelonLoader"));
                    report("已删除 MelonLoader（version.dll 与 MelonLoader 文件夹）。");
                }
                report("✓ 卸载完成。");
                return true;
            }
            catch (Exception e)
            {
                report("× 卸载失败：" + e.Message);
                return false;
            }
        }

        private static void WriteDefaultConfig(string gameDir, Action<string> report)
        {
            try
            {
                string userData = Path.Combine(gameDir, "UserData");
                Directory.CreateDirectory(userData);
                string cfg = Path.Combine(userData, "MelonPreferences.cfg");
                if (!File.Exists(cfg))
                {
                    File.WriteAllText(cfg,
                        "[Moon]\r\n" +
                        "show_hud = true\r\nshow_tooltips = true\r\nauto_strike = false\r\nnever_break = false\r\n" +
                        "force_win = false\r\nforce_outcome = 0\r\ngold_mult = 1.0\r\nui_scale = 1.0\r\nui_alpha = 0.98\r\n" +
                        "light_theme = false\r\naccent = 0\r\nlang_en = false\r\nensure_chinese = true\r\nfont_boost = 0\r\n" +
                        "font_name = \"Microsoft YaHei\"\r\nkey_menu = \"F1\"\r\nkey_hud = \"F2\"\r\nkey_hud_edit = \"F3\"\r\n" +
                        "key_gold = \"F5\"\r\nkey_win = \"F6\"\r\nkey_auto = \"F7\"\r\nkey_repair = \"F8\"\r\n",
                        new UTF8Encoding(false));
                    report("已写入默认设置：UserData\\MelonPreferences.cfg");
                }
            }
            catch (Exception e)
            {
                report("提示：写入默认设置失败（不影响使用）：" + e.Message);
            }
        }

        private static void HideConsole(string gameDir)
        {
            try
            {
                string cfg = Path.Combine(gameDir, "UserData", "Loader.cfg");
                if (!File.Exists(cfg)) return;
                string text = File.ReadAllText(cfg);
                if (text.Contains("hide_console = false"))
                {
                    File.WriteAllText(cfg, text.Replace("hide_console = false", "hide_console = true"));
                }
            }
            catch (Exception) { }
        }

        private static void Download(string url, string dest)
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; }
            catch (Exception) { }
            using (WebClient wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", "MoonInstaller/" + Program.Version);
                wc.DownloadFile(url, dest);
            }
        }

        private static void ExtractOverwrite(string zipPath, string destDir)
        {
            using (ZipArchive zip = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith("/"))
                    {
                        Directory.CreateDirectory(Path.Combine(destDir, entry.FullName));
                        continue;
                    }
                    string target = Path.Combine(destDir, entry.FullName);
                    string dir = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    entry.ExtractToFile(target, true);
                }
            }
        }

        private static void WriteResource(string name, string target)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream(name))
            {
                if (s == null) throw new InvalidOperationException("安装器内缺少资源：" + name);
                using (FileStream fs = new FileStream(target, FileMode.Create, FileAccess.Write))
                {
                    s.CopyTo(fs);
                }
            }
        }

        private static bool ProcessRunning(string name)
        {
            try { return System.Diagnostics.Process.GetProcessesByName(name).Length > 0; }
            catch (Exception) { return false; }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception) { }
        }

        private static void TryDeleteDir(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch (Exception) { }
        }
    }

    internal sealed class InstallerForm : Form
    {
        private readonly TextBox _path = new TextBox();
        private readonly TextBox _log = new TextBox();
        private readonly Label _state = new Label();
        private readonly Button _install = new Button();
        private readonly Button _uninstall = new Button();
        private readonly Button _browse = new Button();
        private readonly Button _detect = new Button();
        private readonly CheckBox _removeMelon = new CheckBox();

        internal InstallerForm()
        {
            Text = "月亮 · ForgeTax 修改器 安装器 v" + Program.Version;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(660, 470);
            BackColor = Color.FromArgb(16, 19, 26);
            ForeColor = Color.FromArgb(235, 238, 245);
            Font = new Font("Microsoft YaHei UI", 9f);

            Label title = new Label();
            title.Text = "月亮 · 修改器安装器";
            title.Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(231, 176, 74);
            title.SetBounds(24, 18, 400, 34);
            Controls.Add(title);

            Label sub = new Label();
            sub.Text = "自动安装 MelonLoader 与修改器到 ForgeTax 游戏目录，无需其它运行库。";
            sub.ForeColor = Color.FromArgb(160, 170, 185);
            sub.SetBounds(26, 54, 600, 20);
            Controls.Add(sub);

            Label pl = new Label();
            pl.Text = "游戏目录";
            pl.SetBounds(26, 92, 80, 20);
            pl.ForeColor = Color.FromArgb(190, 198, 210);
            Controls.Add(pl);

            _path.SetBounds(26, 114, 470, 26);
            _path.BackColor = Color.FromArgb(12, 15, 21);
            _path.ForeColor = Color.FromArgb(235, 238, 245);
            _path.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(_path);

            _browse.SetBounds(504, 113, 60, 28);
            _browse.Text = "浏览";
            Style(_browse);
            _browse.Click += delegate { Browse(); };
            Controls.Add(_browse);

            _detect.SetBounds(570, 113, 66, 28);
            _detect.Text = "自动检测";
            Style(_detect);
            _detect.Click += delegate
            {
                string found = Installer.DetectGame();
                if (string.IsNullOrEmpty(found)) MessageBox.Show(this, "没有在 Steam 库里找到 ForgeTax，请手动选择游戏根目录。", "提示");
                else { _path.Text = found; RefreshState(); }
            };
            Controls.Add(_detect);

            _state.SetBounds(26, 150, 610, 22);
            _state.ForeColor = Color.FromArgb(160, 170, 185);
            Controls.Add(_state);

            _install.SetBounds(26, 180, 200, 40);
            _install.Text = "安装 / 更新修改器";
            _install.BackColor = Color.FromArgb(231, 176, 74);
            _install.ForeColor = Color.FromArgb(20, 23, 30);
            _install.FlatStyle = FlatStyle.Flat;
            _install.FlatAppearance.BorderSize = 0;
            _install.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
            _install.Click += delegate { RunAsync(true); };
            Controls.Add(_install);

            _uninstall.SetBounds(238, 180, 150, 40);
            _uninstall.Text = "卸载修改器";
            Style(_uninstall);
            _uninstall.Click += delegate { RunAsync(false); };
            Controls.Add(_uninstall);

            _removeMelon.SetBounds(400, 190, 240, 22);
            _removeMelon.Text = "同时卸载 MelonLoader";
            _removeMelon.ForeColor = Color.FromArgb(190, 198, 210);
            Controls.Add(_removeMelon);

            _log.SetBounds(26, 236, 610, 208);
            _log.Multiline = true;
            _log.ReadOnly = true;
            _log.ScrollBars = ScrollBars.Vertical;
            _log.BackColor = Color.FromArgb(12, 15, 21);
            _log.ForeColor = Color.FromArgb(200, 208, 220);
            _log.BorderStyle = BorderStyle.FixedSingle;
            _log.Font = new Font("Consolas", 9f);
            Controls.Add(_log);

            _path.TextChanged += delegate { RefreshState(); };
            string detected = Installer.DetectGame();
            if (!string.IsNullOrEmpty(detected)) _path.Text = detected;
            RefreshState();
        }

        private static void Style(Button b)
        {
            b.BackColor = Color.FromArgb(30, 36, 48);
            b.ForeColor = Color.FromArgb(230, 235, 242);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Color.FromArgb(52, 60, 76);
            b.FlatAppearance.BorderSize = 1;
        }

        private void Browse()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "选择 ForgeTax 游戏根目录（里面有 ForgeTax.exe）";
                if (d.ShowDialog(this) == DialogResult.OK) { _path.Text = d.SelectedPath; RefreshState(); }
            }
        }

        private void RefreshState()
        {
            string dir = _path.Text.Trim();
            if (Installer.IsGameDir(dir))
            {
                string melon = Installer.MelonInstalled(dir) ? "已装" : "未装";
                string mod = Installer.ModInstalled(dir) ? "已装" : "未装";
                _state.Text = "✓ 目录有效    MelonLoader：" + melon + "    修改器：" + mod;
                _state.ForeColor = Color.FromArgb(120, 200, 160);
            }
            else
            {
                _state.Text = "尚未选择有效的游戏目录";
                _state.ForeColor = Color.FromArgb(200, 120, 120);
            }
        }

        private void RunAsync(bool install)
        {
            string dir = _path.Text.Trim();
            if (!Installer.IsGameDir(dir))
            {
                MessageBox.Show(this, "请先选择包含 " + Program.GameExe + " 的游戏根目录。", "提示");
                return;
            }
            _install.Enabled = false;
            _uninstall.Enabled = false;
            _log.Clear();
            bool removeMelon = _removeMelon.Checked;

            ThreadPool.QueueUserWorkItem(delegate
            {
                Action<string> report = delegate(string s)
                {
                    if (InvokeRequired)
                    {
                        BeginInvoke((Action)delegate { _log.AppendText(s + Environment.NewLine); });
                    }
                    else _log.AppendText(s + Environment.NewLine);
                };
                bool ok = install ? Installer.Install(dir, report) : Installer.Uninstall(dir, removeMelon, report);
                BeginInvoke((Action)delegate
                {
                    _install.Enabled = true;
                    _uninstall.Enabled = true;
                    RefreshState();
                    if (ok && install)
                    {
                        MessageBox.Show(this, "安装完成，启动游戏后按 F1 打开修改器。", "月亮");
                    }
                });
            });
        }
    }
}
