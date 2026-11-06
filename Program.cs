using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DshBossKey;

internal sealed class Config
{
    public string TargetProcess { get; set; } = "DeepSeek Harness";
    public string HideHotkey { get; set; } = "Ctrl+Alt+Q";
    public string TabHotkey { get; set; } = "Ctrl+Alt+W";
    public bool StartWithWindows { get; set; } = false;
}

internal static class Native
{
    public const int SW_HIDE = 0;
    public const int SW_SHOW = 5;
    public const int SW_RESTORE = 9;
    public const uint GW_OWNER = 4;
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8;

    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int idx);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("kernel32.dll")] public static extern bool AttachConsole(int pid);
}

internal static class Taskbar
{
    [ComImport, Guid("56FDF342-FD6D-11d0-958A-006097C9A090"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ITaskbarList
    {
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);
    }

    [DllImport("ole32.dll")]
    static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, uint ctx, ref Guid iid, out IntPtr ppv);

    static ITaskbarList _list;

    static ITaskbarList List()
    {
        if (_list != null) return _list;
        var clsid = new Guid("56FDF344-FD6D-11d0-958A-006097C9A090");
        var iid = new Guid("56FDF342-FD6D-11d0-958A-006097C9A090");
        int hr = CoCreateInstance(ref clsid, IntPtr.Zero, 1, ref iid, out var p);
        if (hr != 0) throw new COMException("CoCreateInstance(ITaskbarList) 失败", hr);
        _list = (ITaskbarList)Marshal.GetObjectForIUnknown(p);
        _list.HrInit();
        return _list;
    }

    public static void Hide(IntPtr hwnd)
    {
        try { List().DeleteTab(hwnd); } catch { }
    }

    public static void Show(IntPtr hwnd)
    {
        try { List().AddTab(hwnd); } catch { }
    }
}

internal sealed class HotkeyForm : Form
{
    public event Action<int> Hotkey;

    public HotkeyForm()
    {
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-4000, -4000);
        Size = new Size(1, 1);
        Opacity = 0;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312) Hotkey?.Invoke(m.WParam.ToInt32());
        base.WndProc(ref m);
    }
}

internal static class Program
{
    const int HOTKEY_HIDE = 0xB001;
    const int HOTKEY_TAB = 0xB002;

    static Config cfg;
    static NotifyIcon tray;
    static HotkeyForm form;
    static readonly List<IntPtr> targets = new();
    static bool isHidden;
    static bool isTabHidden;
    static bool hotkeyHideOk;
    static bool hotkeyTabOk;
    static bool warnedHide;
    static bool warnedTab;
    static System.Windows.Forms.Timer hotkeyWatchdog;

    static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DshBossKey", "config.json");

    [STAThread]
    static void Main(string[] args)
    {
        cfg = LoadConfig();
        ParseTarget(args);

        var cli = args.FirstOrDefault(a => a.StartsWith("--"));
        if (cli != null)
        {
            SetupConsole();
            RunCli(args);
            return;
        }

        // ---- 托盘常驻模式 ----
        using var mutex = new Mutex(true, @"Local\DshBossKey", out bool first);
        if (!first) return;

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();

        form = new HotkeyForm();
        _ = form.Handle;
        form.Hotkey += OnHotkey;

        var hide = ParseHotkey(cfg.HideHotkey);
        var tab = ParseHotkey(cfg.TabHotkey);
        hotkeyHideOk = Native.RegisterHotKey(form.Handle, HOTKEY_HIDE, hide.mods, hide.vk);
        hotkeyTabOk = Native.RegisterHotKey(form.Handle, HOTKEY_TAB, tab.mods, tab.vk);

        tray = new NotifyIcon
        {
            Icon = BuildIcon(),
            Text = $"老板键 · {cfg.TargetProcess}",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };
        tray.ContextMenuStrip.Opening += (_, _) => RebuildMenu();
        RebuildMenu();
        tray.DoubleClick += (_, _) => ToggleHide();

        WarnIfHotkeyMissing();
        StartHotkeyWatchdog();

        Application.ApplicationExit += (_, _) => RestoreEverything();
        Application.Run(form);
    }

    // ---------- 核心动作 ----------

    static IEnumerable<IntPtr> TargetWindows(bool includeHidden = false)
    {
        var pids = new HashSet<uint>();
        foreach (var p in Process.GetProcessesByName(cfg.TargetProcess))
        {
            pids.Add((uint)p.Id);
            p.Dispose();
        }
        if (pids.Count == 0) yield break;

        var found = new List<IntPtr>();
        Native.EnumWindows((h, _) =>
        {
            Native.GetWindowThreadProcessId(h, out uint pid);
            if (!pids.Contains(pid)) return true;
            if (!includeHidden && !Native.IsWindowVisible(h)) return true;
            if (Native.GetWindowTextLength(h) == 0) return true;
            if ((Native.GetWindowLong(h, Native.GWL_EXSTYLE) & Native.WS_EX_TOOLWINDOW) != 0) return true;
            if (Native.GetWindow(h, Native.GW_OWNER) != IntPtr.Zero) return true;
            if (!Native.GetWindowRect(h, out var rect)) return true;
            // 过滤 .NET 广播窗口 / GDI+ 消息窗口这类看不见的幽灵窗口
            if (rect.Right - rect.Left < 160 || rect.Bottom - rect.Top < 90) return true;
            found.Add(h);
            return true;
        }, IntPtr.Zero);

        foreach (var h in found) yield return h;
    }

    static bool HideWindows()
    {
        targets.Clear();
        targets.AddRange(TargetWindows());
        if (targets.Count == 0) return false;
        foreach (var h in targets) Native.ShowWindow(h, Native.SW_HIDE);
        isHidden = true;
        return true;
    }

    static bool ShowWindows()
    {
        targets.RemoveAll(h => !Native.IsWindow(h));
        if (targets.Count == 0) targets.AddRange(TargetWindows(includeHidden: true));
        if (targets.Count == 0) { isHidden = false; return false; }
        foreach (var h in targets)
        {
            if (!Native.IsWindow(h)) continue;
            Native.ShowWindow(h, Native.SW_SHOW);
            if (Native.IsIconic(h)) Native.ShowWindow(h, Native.SW_RESTORE);
        }
        Native.SetForegroundWindow(targets[0]);
        isHidden = false;
        return true;
    }

    static bool HideTab()
    {
        targets.Clear();
        targets.AddRange(TargetWindows());
        if (targets.Count == 0) return false;
        foreach (var h in targets) Taskbar.Hide(h);
        isTabHidden = true;
        return true;
    }

    static bool ShowTab()
    {
        targets.RemoveAll(h => !Native.IsWindow(h));
        if (targets.Count == 0) targets.AddRange(TargetWindows(includeHidden: true));
        if (targets.Count == 0) return false;
        foreach (var h in targets)
        {
            if (!Native.IsWindow(h)) continue;
            Taskbar.Show(h);
        }
        isTabHidden = false;
        return true;
    }

    static void OnHotkey(int id)
    {
        if (id == HOTKEY_HIDE) ToggleHide();
        else if (id == HOTKEY_TAB) ToggleTab();
    }

    /// <summary>
    /// RegisterHotKey 失败时系统不会给任何提示，热键就是静默失效。
    /// 这里启动时提示一次，之后每 15 秒重试，占用方退出后能自动恢复。
    /// </summary>
    static void WarnIfHotkeyMissing()
    {
        var missing = new List<string>();
        if (!hotkeyHideOk) missing.Add(cfg.HideHotkey);
        if (!hotkeyTabOk) missing.Add(cfg.TabHotkey);
        if (missing.Count == 0) return;

        warnedHide = !hotkeyHideOk;
        warnedTab = !hotkeyTabOk;
        Notify($"{string.Join("、", missing)} 被其他程序占用，暂时不能用。\n" +
               "程序每 15 秒自动重试；也可以右键托盘图标直接操作，或改配置换一组热键。",
               ToolTipIcon.Warning, 10000);
    }

    static void StartHotkeyWatchdog()
    {
        hotkeyWatchdog = new System.Windows.Forms.Timer { Interval = 15000 };
        hotkeyWatchdog.Tick += (_, _) => RetryHotkeys();
        hotkeyWatchdog.Start();
    }

    static void RetryHotkeys()
    {
        if (hotkeyHideOk && hotkeyTabOk) return;

        if (!hotkeyHideOk)
        {
            var k = ParseHotkey(cfg.HideHotkey);
            Native.UnregisterHotKey(form.Handle, HOTKEY_HIDE);
            if (Native.RegisterHotKey(form.Handle, HOTKEY_HIDE, k.mods, k.vk))
            {
                hotkeyHideOk = true;
                if (warnedHide)
                {
                    warnedHide = false;
                    Notify($"热键 {cfg.HideHotkey} 已恢复使用", ToolTipIcon.Info, 3000);
                }
            }
        }

        if (!hotkeyTabOk)
        {
            var k = ParseHotkey(cfg.TabHotkey);
            Native.UnregisterHotKey(form.Handle, HOTKEY_TAB);
            if (Native.RegisterHotKey(form.Handle, HOTKEY_TAB, k.mods, k.vk))
            {
                hotkeyTabOk = true;
                if (warnedTab)
                {
                    warnedTab = false;
                    Notify($"热键 {cfg.TabHotkey} 已恢复使用", ToolTipIcon.Info, 3000);
                }
            }
        }
    }

    static void ToggleHide()
    {
        bool ok = isHidden ? ShowWindows() : HideWindows();
        if (!ok) { Notify($"没找到 {cfg.TargetProcess} 的窗口", ToolTipIcon.Info, 2500); return; }
        UpdateTip();
    }

    static void ToggleTab()
    {
        bool ok = isTabHidden ? ShowTab() : HideTab();
        if (!ok) { Notify($"没找到 {cfg.TargetProcess} 的窗口", ToolTipIcon.Info, 2500); return; }
        UpdateTip();
    }

    static void RestoreEverything()
    {
        try { hotkeyWatchdog?.Stop(); } catch { }
        try { if (isTabHidden) ShowTab(); } catch { }
        try { if (isHidden) ShowWindows(); } catch { }
        Native.UnregisterHotKey(form?.Handle ?? IntPtr.Zero, HOTKEY_HIDE);
        Native.UnregisterHotKey(form?.Handle ?? IntPtr.Zero, HOTKEY_TAB);
        if (tray != null) { tray.Visible = false; tray.Dispose(); }
    }

    // ---------- 托盘 ----------

    static void RebuildMenu()
    {
        var m = tray.ContextMenuStrip;
        m.Items.Clear();
        m.Items.Add(new ToolStripMenuItem(
            (isHidden ? $"显示 {cfg.TargetProcess}" : $"隐藏 {cfg.TargetProcess}") + (hotkeyHideOk ? "" : "（热键被占用）"),
            null, (_, _) => ToggleHide()) { ShortcutKeyDisplayString = cfg.HideHotkey });
        m.Items.Add(new ToolStripMenuItem(
            (isTabHidden ? "恢复任务栏图标" : "隐藏任务栏图标（窗口保持可见）") + (hotkeyTabOk ? "" : "（热键被占用）"),
            null, (_, _) => ToggleTab()) { ShortcutKeyDisplayString = cfg.TabHotkey });
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(new ToolStripMenuItem("开机自启", null, (_, _) => ToggleStartup())
            { Checked = IsStartupEnabled() });
        m.Items.Add(new ToolStripMenuItem("打开配置文件", null, (_, _) => OpenConfig()));
        m.Items.Add(new ToolStripMenuItem("退出（恢复所有窗口）", null, (_, _) => Application.Exit()));
    }

    static void UpdateTip()
    {
        string state = isHidden ? "已隐藏" : (isTabHidden ? "窗口可见·无任务栏图标" : "正常");
        tray.Text = $"老板键 · {cfg.TargetProcess} · {state}";
    }

    static void Notify(string text, ToolTipIcon icon = ToolTipIcon.Info, int ms = 3000)
    {
        if (tray == null) return;
        tray.BalloonTipTitle = "DSH 老板键";
        tray.BalloonTipText = text;
        tray.BalloonTipIcon = icon;
        tray.ShowBalloonTip(ms);
    }

    static void ToggleStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        if (key == null) return;
        if (IsStartupEnabled())
        {
            key.DeleteValue("DshBossKey", throwOnMissingValue: false);
            cfg.StartWithWindows = false;
        }
        else
        {
            key.SetValue("DshBossKey", $"\"{Environment.ProcessPath}\"");
            cfg.StartWithWindows = true;
        }
        SaveConfig(cfg);
    }

    static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run");
        return key?.GetValue("DshBossKey") != null;
    }

    static void OpenConfig()
    {
        SaveConfig(cfg);
        try { Process.Start(new ProcessStartInfo(ConfigPath) { UseShellExecute = true }); } catch { }
    }

    // ---------- 配置 ----------

    static Config LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
                return JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigPath)) ?? new Config();
        }
        catch { }
        var c = new Config();
        SaveConfig(c);
        return c;
    }

    static void SaveConfig(Config c)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath,
                JsonSerializer.Serialize(c, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    static void ParseTarget(string[] args)
    {
        int i = Array.IndexOf(args, "--target");
        if (i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("--"))
            cfg.TargetProcess = args[i + 1];
    }

    static (uint mods, uint vk) ParseHotkey(string text)
    {
        uint mods = 0, vk = 0;
        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            var part = raw.Trim();
            switch (part.ToLowerInvariant())
            {
                case "ctrl": case "control": mods |= Native.MOD_CONTROL; break;
                case "alt": mods |= Native.MOD_ALT; break;
                case "shift": mods |= Native.MOD_SHIFT; break;
                case "win": mods |= Native.MOD_WIN; break;
                default:
                    if (part.Length == 1) vk = char.ToUpperInvariant(part[0]);
                    else if (part.Length >= 2 && (part[0] is 'F' or 'f') &&
                             int.TryParse(part[1..], out int fn) && fn is >= 1 and <= 24)
                        vk = (uint)(0x70 + fn - 1);
                    break;
            }
        }
        if (vk == 0) vk = (uint)Keys.Q;
        return (mods, vk);
    }

    static Icon BuildIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(Color.FromArgb(232, 28, 92, 214));
            g.FillEllipse(brush, 1, 1, 30, 30);
            using var pen = new Pen(Color.White, 2.6f);
            g.DrawArc(pen, 9, 8, 14, 14, 200, 160);
            g.DrawLine(pen, 16, 20, 16, 26);
            g.DrawLine(pen, 16, 23, 20, 23);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    // ---------- 命令行 ----------

    [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int n);
    [DllImport("kernel32.dll")] static extern uint GetFileType(IntPtr h);

    /// <summary>
    /// WinExe 默认没有控制台。父进程若做了重定向（管道/文件），直接用现成的句柄；
    /// 否则附着到父控制台，这样从 cmd/PowerShell 里手动跑也能看到输出。
    /// </summary>
    static void SetupConsole()
    {
        var handle = GetStdHandle(-11);
        if (GetFileType(handle) != 0) return;          // 0 = FILE_TYPE_UNKNOWN，说明没有可用句柄
        if (!Native.AttachConsole(-1)) return;
        var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        Console.SetOut(stdout);
    }

    static void RunCli(string[] args)
    {
        bool wantsHide = args.Contains("--hide");
        bool wantsShow = args.Contains("--show");
        bool wantsToggle = args.Contains("--toggle");
        bool wantsTabOff = args.Contains("--tab-off");
        bool wantsTabOn = args.Contains("--tab-on");
        bool wantsTabToggle = args.Contains("--tab-toggle");
        bool wantsStatus = args.Contains("--status");

        if (wantsStatus)
        {
            var wins = TargetWindows().ToList();
            Console.WriteLine($"target : {cfg.TargetProcess}");
            Console.WriteLine($"pid    : {string.Join(",", Process.GetProcessesByName(cfg.TargetProcess).Select(p => p.Id))}");
            Console.WriteLine($"windows: {wins.Count}");
            foreach (var h in wins)
            {
                var sb = new StringBuilder(256);
                Native.GetWindowText(h, sb, 256);
                Console.WriteLine($"  {h} visible={Native.IsWindowVisible(h)} title={sb}");
            }
            Console.WriteLine($"config : {ConfigPath}");
            return;
        }

        if (wantsHide) Console.WriteLine(HideWindows() ? "hidden" : "no-window");
        if (wantsShow) Console.WriteLine(ShowWindows() ? "shown" : "no-window");
        if (wantsToggle)
        {
            var wins = TargetWindows().ToList();
            if (wins.Count == 0) Console.WriteLine("no-window");
            else Console.WriteLine(HideWindows() ? "hidden" : "no-window");
        }
        if (wantsTabOff) Console.WriteLine(HideTab() ? "tab-off" : "no-window");
        if (wantsTabOn) Console.WriteLine(ShowTab() ? "tab-on" : "no-window");
        if (wantsTabToggle) Console.WriteLine(ToggleTabCli() ? "tab-toggled" : "no-window");
    }

    static bool ToggleTabCli()
    {
        var wins = TargetWindows().ToList();
        if (wins.Count == 0) return false;
        targets.Clear();
        targets.AddRange(wins);
        foreach (var h in wins) Taskbar.Hide(h);
        return true;
    }
}

