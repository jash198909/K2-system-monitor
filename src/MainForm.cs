using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace K2
{
    static class Autostart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool IsEnabled()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue("K2") != null;
            }
            catch { return false; }
        }

        public static void Set(bool on)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (k == null) return;
                    if (on) k.SetValue("K2", Application.ExecutablePath);
                    else k.DeleteValue("K2", false);
                }
            }
            catch { }
        }
    }

    public class MainForm : Form
    {
        static readonly Color BackColor1 = Color.FromArgb(42, 42, 46);
        // Фон самой панели оверлея (темнее, чем у служебных окон настроек).
        static readonly Color OverlayBack = Color.FromArgb(20, 22, 26);

        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UpdateLayeredWindow(IntPtr hWnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hDC);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObj);
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        const int GWL_EXSTYLE = -20;
        const int WS_EX_LAYERED = 0x80000;
        const uint SWP_NOMOVE = 0x2, SWP_NOSIZE = 0x1, SWP_NOZORDER = 0x4, SWP_FRAMECHANGED = 0x20;
        const int ULW_ALPHA = 2;
        const byte AC_SRC_OVER = 0, AC_SRC_ALPHA = 1;

        const int WM_HOTKEY = 0x0312;
        int[] HkIds = new int[] { 1, 2, 3, 4, 5 };
        string[] HkKeys = new string[] { "hk.show", "hk.reset", "hk.mode", "hk.settings", "hk.measure" };

        Timer timer;
        bool busy;
        Snapshot cur = new Snapshot();
        public Stats StatsNow = new Stats();
        public Settings St;              // применённые настройки
        public bool FpsLive { get { return fpsLive; } }
        public Snapshot Live { get { return cur; } }
        SettingsForm settingsForm;
        float S = 1f;
        NotifyIcon tray;
        ContextMenuStrip menu;
        ToolStripMenuItem miInterval;
        bool dragging;
        Point dragStart;
        ToolTip tip = new ToolTip();
        bool hiddenByUser;               // скрыто хоткеем/треем
        bool startChecked;               // «проверка при запуске» выполнена
        int gatePid;
        int lastGamePid;
        string lastGameProc = "";
        bool fpsLive;
        string selfProc = "K2";
        FpsEngine fpsEng = new FpsEngine();
        int lastCsvTick;

        public MainForm()
        {
            Text = "K2";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = BackColor1;
            TopMost = true;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            try { Icon = AppIcon(); } catch { }
            try { using (Process p = Process.GetCurrentProcess()) selfProc = p.ProcessName; } catch { }
            St = Settings.Load();
            if (SanitizeGameLists(St)) St.Save();
        }

        protected override void OnLoad(EventArgs e)
        {
            S = DeviceDpi / 96f;
            ApplyTransparencyKey();
            ApplyWindowSize();
            LoadPos();
            Monitor.LogCleanup();
            BuildTray();
            ContextMenuStrip = menu;

            timer = new Timer();
            timer.Interval = Math.Max(250, St.GetI("ov.interval"));
            StatsNow.IntervalMs = timer.Interval;
            timer.Tick += OnTick;
            timer.Start();

            Task.Run(() =>
            {
                try { var s = new Snapshot(); Monitor.Refresh(s); OnData(s); }
                catch (Exception ex) { MonitorError(ex); }
            });
            base.OnLoad(e);
        }

        void ApplyWindowSize()
        {
            ClientSize = new Size(OverlayDraw.WidthOf(St, S), ClientSize.Height > 0 ? ClientSize.Height : 620);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyTransparencyKey();
            RegisterHotkeys();
        }

        void RegisterHotkeys()
        {
            for (int i = 0; i < HkKeys.Length; i++)
            {
                try { UnregisterHotKey(Handle, HkIds[i]); } catch { }
                uint mods, vk;
                if (ParseHotkey(St.GetS(HkKeys[i]), out mods, out vk))
                    RegisterHotKey(Handle, HkIds[i], mods, vk);
            }
        }

        static bool ParseHotkey(string spec, out uint mods, out uint vk)
        {
            mods = 0; vk = 0;
            if (string.IsNullOrEmpty(spec)) return false;
            // формат: "+Ctrl+Alt+F8" (первый символ — флаг «вкл»: '' или пусто = выкл)
            if (spec.StartsWith("-")) return false;
            string[] parts = spec.TrimStart('+').Split('+');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0) continue;
                switch (p)
                {
                    case "Ctrl": mods |= 0x0002; break;
                    case "Alt": mods |= 0x0001; break;
                    case "Shift": mods |= 0x0004; break;
                    case "Win": mods |= 0x0008; break;
                    default:
                        KeysConverter kc = new KeysConverter();
                        try { vk = (uint)(Keys)kc.ConvertFromInvariantString(p); } catch { vk = 0; }
                        break;
                }
            }
            return vk != 0;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == 1) { hiddenByUser = !hiddenByUser; UpdateVisible(); }
                else if (id == 2) { ResetSession(); }
                else if (id == 3) { ToggleCompact(); }
                else if (id == 4) { OpenSettings(); }
                else if (id == 5) { StatsNow.Paused = !StatsNow.Paused; Invalidate(); }
            }
            base.WndProc(ref m);
        }

        void ToggleCompact()
        {
            bool show = !St.GetB("cpu.corefreqs");
            St.Set("cpu.corefreqs", show);
            St.Set("cpu.coregraph", show);
            St.Save();
            Invalidate();
        }

        RectangleF graphRectClient = RectangleF.Empty;
        HistoryForm histWin;

        void ToggleHistory()
        {
            if (histWin == null || histWin.IsDisposed)
            {
                histWin = new HistoryForm(StatsNow);
                histWin.StartPosition = FormStartPosition.Manual;
                histWin.Location = new Point(Left, Top + Math.Max(0, Height - histWin.Height));
            }
            if (histWin.Visible) histWin.Hide();
            else histWin.Show();
        }

        void OpenSettings()
        {
            if (settingsForm == null || settingsForm.IsDisposed)
                settingsForm = new SettingsForm(this);
            settingsForm.StartPosition = FormStartPosition.CenterScreen;
            settingsForm.Show();
            settingsForm.Activate();
        }

        void SyncIntervalChecks()
        {
            int cur = St.GetI("ov.interval");
            foreach (ToolStripItem sub in miInterval.DropDownItems)
                ((ToolStripMenuItem)sub).Checked = (sub.Tag is int) && (int)sub.Tag == cur;
            if (miEnabled != null && miEnabled.Checked != St.GetB("ov.enabled"))
                miEnabled.Checked = St.GetB("ov.enabled");
        }

        ToolStripMenuItem miEnabled;

        void SyncEnabledCheck()
        {
            if (miEnabled != null && miEnabled.Checked != St.GetB("ov.enabled"))
                miEnabled.Checked = St.GetB("ov.enabled");
        }

        // применить настройки из окна настроек
        public void ApplySettings(Settings fresh)
        {
            fresh.Normalize();
            SanitizeGameLists(fresh);
            string oldMode = St.GetS("pos.mode");
            bool modeChanged = oldMode != fresh.GetS("pos.mode");
            bool manual = fresh.GetS("pos.mode") == "manual";
            St = fresh;
            St.Save();
            timer.Interval = Math.Max(250, St.GetI("ov.interval"));
            StatsNow.IntervalMs = timer.Interval;
            SyncIntervalChecks();
            Autostart.Set(St.GetB("ov.autostart"));
            RegisterHotkeys();
            ApplyTransparencyKey();
            ApplyWindowSize();
            UpdateRegion();
            ApplyTransparencyKey();
            // Ручной режим не трогаем без смены режима; закреплённый — всегда
            // переставляем в выбранный угол/монитор.
            if (!manual) ApplyPosition();
            else if (modeChanged) LoadPos();
            else ClampToScreen();
            if (St.GetB("look.fontAll"))
            {
                try { Font nf = new Font(St.GetS("look.font", "Segoe UI"), 9.75f); this.Font = nf;
                      if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.Font = nf; }
                catch { }
            }
            SyncEnabledCheck();
            UpdateVisible();
            Invalidate(true);
            RenderLayered(); // скругление/прозрачность применяются сразу, без перезапуска
        }

        public void ReloadFromDisk()
        {
            ApplySettings(Settings.Load());
        }

        void LoadPos()
        {
            string mode = St.GetS("pos.mode");
            if (mode == "manual")
            {
                int x, y;
                if (TryLoadPos(out x, out y))
                {
                    Location = new Point(x, y);
                    ClampToScreen();
                    return;
                }
            }
            ApplyPosition();
        }

        bool TryLoadPos(out int x, out int y)
        {
            x = y = 0;
            try
            {
                string f = Monitor.LogPath("pos.txt");
                if (!File.Exists(f)) return false;
                string[] p = File.ReadAllText(f).Split(' ');
                if (p.Length < 2) return false;
                if (!int.TryParse(p[0], out x) || !int.TryParse(p[1], out y)) return false;
                Rectangle v = SystemInformation.VirtualScreen;
                return x >= v.Left - 2000 && y >= v.Top - 2000 &&
                       x <= v.Right + 2000 && y <= v.Bottom + 2000;
            }
            catch { return false; }
        }

        public void ApplyPosition()
        {
            string corner = St.GetS("pos.corner");
            int ox = St.GetI("pos.offX"), oy = St.GetI("pos.offY");
            Screen scr;
            int mi = St.GetI("pos.monitor");
            Screen[] all = Screen.AllScreens;
            // выбранный монитор учитывается всегда, а не только при pos.pin;
            // иначе оверлей со второго монитора прыгал на основной
            if (mi >= 0 && mi < all.Length) scr = all[mi];
            else scr = Screen.PrimaryScreen;
            Rectangle wa = scr.WorkingArea;
            int x, y;
            switch (corner)
            {
                case "TL": x = wa.Left; y = wa.Top; break;
                case "BL": x = wa.Left; y = wa.Bottom - Height; break;
                case "BR": x = wa.Right - Width; y = wa.Bottom - Height; break;
                case "C": x = wa.Left + (wa.Width - Width) / 2; y = wa.Top + (wa.Height - Height) / 3; break;
                default: x = wa.Right - Width; y = wa.Top; break;
            }
            Location = new Point(x + ox, y + oy);
            ClampToScreen();
        }

        void ClampToScreen()
        {
            Screen scr = Screen.FromPoint(new Point(Left + Width / 2, Top + Height / 2));
            Rectangle wa = scr.WorkingArea;
            if (Bottom > wa.Bottom) Top = Math.Max(wa.Top, wa.Bottom - Height);
            if (Right > wa.Right) Left = Math.Max(wa.Left, wa.Right - Width);
            if (Top < wa.Top) Top = wa.Top;
            if (Left < wa.Left) Left = wa.Left;
        }

        void SavePos()
        {
            if (St.GetS("pos.mode") != "manual") return;
            try { File.WriteAllText(Monitor.LogPath("pos.txt"), Left + " " + Top); } catch { }
        }

        bool GameActive(Snapshot s)
        {
            string proc = s.FgProc;
            if (proc.Length == 0 || s.FgPid <= 0 || IsShellProcess(proc)) return false;
            string always = "," + St.GetS("ov.alwaysGame") + ",";
            string never = "," + St.GetS("ov.neverGame") + ",";
            if (never.IndexOf("," + proc + ",", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (s.FgFullscreen) return true;
            if (always.IndexOf("," + proc + ",", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (IsKnownNonGame(proc)) return false;
            // Оконные игры (например NFSU2 в 1280x720) не являются fullscreen.
            // Borderless-окно — почти наверняка игра; крупное окно неизвестного
            // приложения тоже считаем игровым, но браузеры/терминалы исключены.
            if (s.FgChromeless && s.FgWidth >= 854 && s.FgHeight >= 480) return true;
            return s.FgAreaPct >= 0.35;
        }

        void UpdateVisible()
        {
            Visible = St.GetB("ov.enabled") && !hiddenByUser && AllowedByGameMode();
        }

        bool AllowedByGameMode()
        {
            if (!St.GetB("ov.onlyGame")) return true;
            return GameActive(cur);
        }

        void BuildTray()
        {
            menu = new ContextMenuStrip();
            menu.Renderer = new DarkMenuRenderer();
            menu.BackColor = Color.FromArgb(16, 22, 30);
            menu.ForeColor = Color.FromArgb(232, 240, 245);
            ToolStripMenuItem show = new ToolStripMenuItem("Показать/скрыть оверлей");
            show.Click += delegate { hiddenByUser = !hiddenByUser; UpdateVisible(); };
            menu.Items.Add(show);

            miEnabled = new ToolStripMenuItem("Оверлей включён (мастер)");
            miEnabled.CheckOnClick = true;
            miEnabled.Checked = St.GetB("ov.enabled");
            miEnabled.CheckedChanged += delegate
            {
                if (St.GetB("ov.enabled") != miEnabled.Checked)
                {
                    St.Set("ov.enabled", miEnabled.Checked);
                    St.Save();
                    UpdateVisible();
                }
            };
            menu.Items.Add(miEnabled);
            ToolStripMenuItem open = new ToolStripMenuItem("Настройки…");
            open.ShortcutKeys = Keys.F10;
            open.Click += delegate { OpenSettings(); };
            menu.Items.Add(open);

            ToolStripMenuItem top = new ToolStripMenuItem("Поверх окон");
            top.CheckOnClick = true;
            top.Checked = TopMost;
            top.CheckedChanged += delegate { TopMost = top.Checked; };
            menu.Items.Add(top);

            ToolStripMenuItem excl = new ToolStripMenuItem("Исключения «только игра»");
            ToolStripMenuItem itAlways = new ToolStripMenuItem("Пометить активное окно: ВСЕГДА игра");
            itAlways.Click += delegate { MarkGame("ov.alwaysGame"); };
            ToolStripMenuItem itNever = new ToolStripMenuItem("Пометить активное окно: НИКОГДА не игра");
            itNever.Click += delegate { MarkGame("ov.neverGame"); };
            excl.DropDownItems.Add(itAlways);
            excl.DropDownItems.Add(itNever);
            menu.Items.Add(excl);

            miInterval = new ToolStripMenuItem("Интервал обновления");
            int[] ms = new int[] { 250, 500, 1000, 2000, 5000 };
            foreach (int msec in ms)
            {
                string label = msec < 1000
                    ? msec.ToString(CultureInfo.GetCultureInfo("ru-RU")) + " мс"
                    : (msec / 1000.0).ToString("0.#", CultureInfo.GetCultureInfo("ru-RU")) + " с";
                ToolStripMenuItem it = new ToolStripMenuItem(label);
                it.Tag = msec;
                it.Click += delegate (object s, EventArgs e2)
                {
                    int v = (int)((ToolStripMenuItem)s).Tag;
                    timer.Interval = Math.Max(250, v);
                    StatsNow.IntervalMs = timer.Interval;
                    St.Set("ov.interval", v);
                    St.Save();
                    SyncIntervalChecks();
                    if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.SyncFromApplied();
                };
                miInterval.DropDownItems.Add(it);
            }
            menu.Items.Add(miInterval);
            menu.Opening += delegate { SyncIntervalChecks(); };
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem exit = new ToolStripMenuItem("Выход");
            exit.Click += delegate { Close(); };
            menu.Items.Add(exit);

            tray = new NotifyIcon();
            tray.Icon = MakeIcon();
            tray.Text = "K2 — настройки: F10 (двойной клик — показать)";
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { hiddenByUser = false; Visible = true; Activate(); };
            tray.Visible = true;
        }

        string ForegroundProcess()
        {
            try
            {
                IntPtr h = GetForegroundWindow();
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                if (pid == 0) return "";
                return Process.GetProcessById((int)pid).ProcessName;
            }
            catch { return ""; }
        }

        bool IsShellProcess(string proc)
        {
            if (string.IsNullOrEmpty(proc)) return true;
            // Само K2 и оболочка Windows не могут быть «игрой»: иначе счётчик FPS
            // запускается на рабочем столе и показывает случайные числа.
            return proc.Equals(selfProc, StringComparison.OrdinalIgnoreCase) ||
                   proc.Equals("K2", StringComparison.OrdinalIgnoreCase) ||
                   proc.Equals("explorer", StringComparison.OrdinalIgnoreCase) ||
                   proc.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase) ||
                   proc.Equals("SearchHost", StringComparison.OrdinalIgnoreCase) ||
                   proc.Equals("StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase) ||
                   proc.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase) ||
                   proc.Equals("TextInputHost", StringComparison.OrdinalIgnoreCase) ||
                   proc.Equals("LockApp", StringComparison.OrdinalIgnoreCase) ||
                   proc.Equals("Progman", StringComparison.OrdinalIgnoreCase) ||
                   proc.Equals("WorkerW", StringComparison.OrdinalIgnoreCase) ||
                   proc.Equals("dwm", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsKnownNonGame(string proc)
        {
            // Приложения, в которых FPS-счётчик заведомо не нужен и которые
            // иначе принимались бы за игру из-за большого окна.
            switch (proc.ToLowerInvariant())
            {
                case "chrome":
                case "msedge":
                case "firefox":
                case "opera":
                case "brave":
                case "vivaldi":
                case "iexplore":
                case "discord":
                case "telegram":
                case "slack":
                case "teams":
                case "ms-teams":
                case "spotify":
                case "code":
                case "opencode":
                case "cursor":
                case "windsurf":
                case "devenv":
                case "idea64":
                case "pycharm64":
                case "rider64":
                case "windowsterminal":
                case "powershell":
                case "pwsh":
                case "cmd":
                case "conhost":
                case "notepad":
                case "explorer":
                case "totalcmd64":
                case "winrar":
                case "7zfm":
                case "everything":
                case "taskmgr":
                case "systemsettings":
                case "applicationframehost":
                case "searchhost":
                case "shellexperiencehost":
                case "startmenuexperiencehost":
                case "lockapp":
                case "dwm":
                case "obs64":
                case "obs32":
                case "k2":
                    return true;
            }
            return false;
        }

        static bool ProcessAlivePid(int pid)
        {
            if (pid <= 0) return false;
            try
            {
                using (Process p = Process.GetProcessById(pid))
                    return !p.HasExited;
            }
            catch { return false; }
        }

        bool SanitizeGameLists(Settings s)
        {
            bool changed = SanitizeGameList(s, "ov.alwaysGame");
            changed |= SanitizeGameList(s, "ov.neverGame");
            return changed;
        }

        bool SanitizeGameList(Settings s, string key)
        {
            string raw = s.GetS(key);
            if (raw.Length == 0) return false;
            List<string> keep = new List<string>();
            foreach (string part in raw.Split(','))
            {
                string p = part.Trim();
                if (p.Length == 0 || IsShellProcess(p)) continue;
                bool dup = false;
                foreach (string k in keep)
                    if (k.Equals(p, StringComparison.OrdinalIgnoreCase)) { dup = true; break; }
                if (!dup) keep.Add(p);
            }
            string clean = string.Join(",", keep.ToArray());
            if (clean == raw) return false;
            s.Set(key, clean);
            return true;
        }

        void MarkGame(string key)
        {
            string proc = ForegroundProcess();
            if (IsShellProcess(proc)) proc = cur.FgProc;
            if (IsShellProcess(proc))
            {
                MessageBox.Show("Не удалось определить активное окно игры. Откройте игру и повторите.", "K2");
                return;
            }
            string list = St.GetS(key);
            if (("," + list + ",").IndexOf("," + proc + ",", StringComparison.OrdinalIgnoreCase) < 0)
                list = list.Length == 0 ? proc : list + "," + proc;
            St.Set(key, list);
            St.Save();
        }

        internal static Icon AppIcon()
        {
            if (appIcon != null) return appIcon;
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    // фон — тёмно-синий скруглённый квадрат в стиле окна настроек
                    using (GraphicsPath p = RoundPath(new RectangleF(1, 1, 30, 30), 8))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(20, 26, 34)))
                        g.FillPath(b, p);
                    using (GraphicsPath p = RoundPath(new RectangleF(1, 1, 30, 30), 8))
                    using (Pen pn = new Pen(Color.FromArgb(0, 191, 166), 1.6f))
                        g.DrawPath(pn, p);
                    // надпись K2
                    using (Font f = new Font("Segoe UI", 10.5f, FontStyle.Bold))
                    using (SolidBrush w = new SolidBrush(Color.FromArgb(0, 191, 166)))
                    {
                        StringFormat sf = new StringFormat();
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString("K2", f, w, new RectangleF(0, 0, 32, 31), sf);
                    }
                }
                appIcon = Icon.FromHandle(bmp.GetHicon());
            }
            return appIcon;
        }

        static Icon appIcon;

        static Icon MakeIcon()
        {
            return AppIcon();
        }

        void OnTick(object sender, EventArgs e)
        {
            if (busy) { return; }
            busy = true;
            Task.Run(() =>
            {
                try { var s = new Snapshot(); Monitor.Refresh(s); OnData(s); }
                catch (Exception ex) { MonitorError(ex); }
                finally { busy = false; }
            });
        }

        void OnData(Snapshot s)
        {
            if (IsDisposed) return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    cur = s;
                    if (!startChecked && s.Ready)
                    {
                        startChecked = true;
                        RunStartupCheck(s);
                    }
                    // FPS считает PresentMon по событиям презентации конкретного
                    // foreground-процесса. Никаких эвристик по статичности экрана.
                    bool ga = GameActive(cur);
                    bool selfFg = cur.FgProc.Equals(selfProc, StringComparison.OrdinalIgnoreCase) ||
                                  cur.FgProc.Equals("K2", StringComparison.OrdinalIgnoreCase);
                    bool keepForSelf = !ga && selfFg && lastGamePid > 0 && ProcessAlivePid(lastGamePid);
                    int targetPid = ga ? cur.FgPid : (keepForSelf ? lastGamePid : 0);
                    bool fpsMode = targetPid > 0;
                    fpsLive = fpsMode;

                    if (ga)
                    {
                        if (cur.FgPid != gatePid)
                        {
                            fpsEng.ResetAvg();
                            if (St.GetB("stat.autoreset")) StatsNow.Reset();
                            gatePid = cur.FgPid;
                        }
                        lastGamePid = cur.FgPid;
                        lastGameProc = cur.FgProc;
                    }
                    else if (keepForSelf)
                    {
                        cur.FgProc = lastGameProc;
                        cur.FgFullscreen = true;
                    }

                    if (fpsMode) fpsEng.Start(targetPid, ga ? cur.FgProc : lastGameProc);
                    else
                    {
                        fpsEng.Stop();
                        gatePid = 0;
                        lastGamePid = 0;
                    }

                    if (fpsMode)
                    {
                        cur.Fps = fpsEng.Fps;
                        cur.FpsAvg = fpsEng.Avg;
                        cur.FpsLatency = fpsEng.LatencyMs;
                        cur.FpsLow1 = fpsEng.Low1;
                        cur.FpsLow01 = fpsEng.Low01;
                        cur.FrameTimes = fpsEng.FrameHistory();
                        cur.PresentMode = fpsEng.PresentMode;
                        cur.RenderApi = fpsEng.RenderApi;
                    }
                    else
                    {
                        cur.Fps = null; cur.FpsAvg = null; cur.FpsLatency = null;
                        cur.FpsLow1 = null; cur.FpsLow01 = null;
                        cur.FrameTimes.Clear();
                        cur.PresentMode = ""; cur.RenderApi = "";
                    }
                    int nowTick = Environment.TickCount;
                    StatsNow.Update(s);
                    if (lastCsvTick == 0 || nowTick - lastCsvTick >= 5000)
                    {
                        lastCsvTick = nowTick;
                        Monitor.LogRow(s);
                    }
                    UpdateVisible();
                    if (settingsForm != null && !settingsForm.IsDisposed && settingsForm.Visible)
                        settingsForm.RefreshPreview();
                    UpdateHeightAndRepaint();
                    RenderLayered();
                });
            }
            catch { }
        }

        void UpdateHeightAndRepaint()
        {
            OverlayDraw probe = new OverlayDraw(null, St, cur, StatsNow, S);
            float h = probe.DrawWithWidth(null, ClientSize.Width);
            probe.DisposeFonts();
            int hi = (int)(h + PadBottom());
            if (Math.Abs(hi - ClientSize.Height) > 1)
            {
                ClientSize = new Size(ClientSize.Width, hi);
                // В ручном режиме позицию не трогаем: изменение высоты содержимого
                // не должно переставлять оверлей на другой монитор.
                if (St.GetS("pos.mode") != "manual")
                    ApplyPosition();
                else
                    ClampToScreen();
            }
            Invalidate();
        }

        float PadBottom() { return 10 * S * Math.Max(0.5f, St.GetI("look.scale") / 100f); }

        public void ResetSession()
        {
            StatsNow.Reset();
            fpsEng.ResetAvg();
            Invalidate();
        }

        void RunStartupCheck(Snapshot s)
        {
            if (!St.GetB("ov.checkStart")) return;
            List<string> missing = new List<string>();
            if (s.CpuTemp == null) missing.Add("температура CPU");
            if (s.GpuTemp == null) missing.Add("температура GPU");
            if (s.CpuPower == null) missing.Add("потребление CPU");
            if (s.CpuFreq == null) missing.Add("частоты ядер");
            if (missing.Count == 0) return;
            try
            {
                tray.ShowBalloonTip(8000, "K2: не все датчики доступны",
                    "Нет: " + string.Join(", ", missing.ToArray()) +
                    ". Проверь UAC/права админа или «Целостность памяти» (HVCI) в Windows Security.",
                    ToolTipIcon.Warning);
            }
            catch { }
        }

        void MonitorError(Exception ex)
        {
            try { File.AppendAllText(Monitor.LogPath("error.txt"), DateTime.Now + " " + ex + "\r\n"); } catch { }
        }

        // ---------- painting ----------
        protected override void OnPaint(PaintEventArgs e)
        {
            RenderLayered();
        }

        bool rendering;
        void RenderLayered()
        {
            if (rendering || !IsHandleCreated || St == null || Width <= 0 || Height <= 0) return;
            rendering = true;
            try
            {
                ApplyTransparencyKey();
                using (Bitmap bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                        g.Clear(Color.Transparent);

                        int op = Math.Max(0, Math.Min(100, St.GetI("look.opacity")));
                        int bgAlpha = (int)Math.Round(255.0 * (100 - op) / 100.0);
                        if (bgAlpha > 0)
                        {
                            using (GraphicsPath p = RoundPath(new Rectangle(0, 0, Width - 1, Height - 1), St.GetI("look.radius") * S))
                            using (Brush b = new SolidBrush(Color.FromArgb(bgAlpha, OverlayBack.R, OverlayBack.G, OverlayBack.B)))
                                g.FillPath(b, p);
                        }

                        OverlayDraw od = new OverlayDraw(g, St, cur, StatsNow, S);
                        od.DrawWithWidth(g, Width);
                        graphRectClient = od.LastGraphRect;
                        od.DisposeFonts();

                        if (!cur.Ready)
                        {
                            using (Brush b = new SolidBrush(Color.FromArgb(200, 200, 200)))
                            using (Font f = new Font("Segoe UI", 9.75f * S))
                            using (StringFormat sf = new StringFormat())
                            {
                                sf.Alignment = StringAlignment.Center;
                                g.DrawString("инициализация сенсоров…", f, b,
                                    new RectangleF(0, Height / 2f - 10, Width, 20), sf);
                            }
                        }
                    }

                    IntPtr screenDc = GetDC(IntPtr.Zero);
                    IntPtr memDc = CreateCompatibleDC(screenDc);
                    IntPtr hBmp = bmp.GetHbitmap(Color.FromArgb(0));
                    IntPtr old = SelectObject(memDc, hBmp);
                    try
                    {
                        POINT dst = new POINT(); dst.X = Left; dst.Y = Top;
                        SIZE size = new SIZE(); size.cx = Width; size.cy = Height;
                        POINT src = new POINT();
                        BLENDFUNCTION bf = new BLENDFUNCTION();
                        bf.BlendOp = AC_SRC_OVER;
                        bf.SourceConstantAlpha = 255;
                        bf.AlphaFormat = AC_SRC_ALPHA;
                        bool ok = UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref bf, ULW_ALPHA);
                        int err = ok ? 0 : Marshal.GetLastWin32Error();
                        if (!ok || Environment.GetEnvironmentVariable("K2_RENDER_DEBUG") == "1")
                        {
                            try
                            {
                                File.AppendAllText(Monitor.LogPath("error.txt"), DateTime.Now + " UpdateLayeredWindow ok=" + ok +
                                    " err=" + err + " size=" + size.cx + "x" + size.cy + " dst=" + dst.X + "," + dst.Y + "\r\n");
                            }
                            catch { }
                        }
                    }
                    finally
                    {
                        SelectObject(memDc, old);
                        DeleteObject(hBmp);
                        DeleteDC(memDc);
                        ReleaseDC(IntPtr.Zero, screenDc);
                    }
                }
            }
            catch (Exception ex) { MonitorError(ex); }
            finally { rendering = false; }
        }

        // ---------- interaction ----------
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (graphRectClient.Contains(new Point(e.X, e.Y)))
                {
                    ToggleHistory();
                    base.OnMouseDown(e);
                    return;
                }
                dragging = true;
                dragStart = e.Location;
                tip.SetToolTip(this, "Перетащите, чтобы двигать. Клик по графику — история. ПКМ — меню");
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging && e.Button == MouseButtons.Left)
                Location = new Point(Left + e.X - dragStart.X, Top + e.Y - dragStart.Y);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (dragging)
            {
                dragging = false;
                St.Set("pos.mode", "manual");
                SavePos();
                if (St.GetB("pos.pin")) PinCurrentMonitor();
                St.Save(); // ручной режим и координаты должны пережить перезапуск
                RenderLayered();
            }
            base.OnMouseUp(e);
        }

        void PinCurrentMonitor()
        {
            Screen[] all = Screen.AllScreens;
            for (int i = 0; i < all.Length; i++)
                if (all[i].Bounds.Contains(new Point(Left + Width / 2, Top + Math.Min(Height / 2, 50))))
                {
                    St.Set("pos.monitor", i);
                    St.Save();
                    break;
                }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Фон рисуется в RenderLayered() в 32-битный bitmap с альфой.
        }

        static GraphicsPath RoundPath(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            if (radius < 1) { p.AddRectangle(r); p.CloseFigure(); return p; }
            float d = radius * 2f;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            UpdateRegion();
            ApplyTransparencyKey(); // после создания окна и региона — гарантированно
            RenderLayered();        // у layered-окна первый WM_PAINT может не прийти
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
                if (args[i] == "--settings") { OpenSettings(); break; }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateRegion();
            RenderLayered();
        }

        void UpdateRegion()
        {
            if (!IsHandleCreated) return;
            using (GraphicsPath p = RoundPath(new Rectangle(0, 0, Width, Height), St.GetI("look.radius") * S))
                Region = new Region(p);
        }

        // Окно всегда layered: содержимое рисуется в 32-битный bitmap с альфой и
        // отправляется через UpdateLayeredWindow. Это даёт плавную прозрачность фона
        // (0–100%) и сохраняет непрозрачный текст.
        void ApplyTransparencyKey()
        {
            if (!IsHandleCreated || St == null) return;
            int ex = GetWindowLong(Handle, GWL_EXSTYLE);
            if ((ex & WS_EX_LAYERED) == 0)
            {
                SetWindowLong(Handle, GWL_EXSTYLE, ex | WS_EX_LAYERED);
                SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            timer.Stop();
            fpsEng.Stop();
            SavePos();
            St.Save();
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
            Monitor.Shutdown();
            base.OnFormClosing(e);
        }
    }
}
