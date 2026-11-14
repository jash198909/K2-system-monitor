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
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace K2
{
    static class Autostart
    {
        // Автозапуск через Планировщик задач: K2 требует прав администратора,
        // а обычная запись в HKCU\...\Run такие программы при входе не запускает.
        const string TaskName = "K2 System Monitor";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static void Set(bool on)
        {
            if (on)
            {
                if (!TaskCurrent() && !CreateTask())
                    WriteRunValue();   // запасной вариант, если планировщик недоступен
                else
                    RemoveRunValue();
            }
            else
            {
                DeleteTask();
                RemoveRunValue();
            }
        }

        static bool TaskCurrent()
        {
            string xml;
            if (RunSchTasks("/Query /TN \"" + TaskName + "\" /XML", out xml) != 0) return false;
            return xml.IndexOf(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool CreateTask()
        {
            string file = Path.Combine(Path.GetTempPath(), "k2-autostart.xml");
            try
            {
                File.WriteAllText(file, TaskXml(), Encoding.Unicode);
                string output;
                return RunSchTasks("/Create /TN \"" + TaskName + "\" /XML \"" + file + "\" /F", out output) == 0;
            }
            catch (Exception ex) { Log(ex); return false; }
            finally { try { File.Delete(file); } catch { } }
        }

        static void DeleteTask()
        {
            string output;
            RunSchTasks("/Delete /TN \"" + TaskName + "\" /F", out output);
        }

        static bool RunValueExists()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue("K2") != null;
            }
            catch { return false; }
        }

        static void WriteRunValue()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (k == null) return;
                    k.SetValue("K2", "\"" + Application.ExecutablePath + "\"");
                }
            }
            catch (Exception ex) { Log(ex); }
        }

        static void RemoveRunValue()
        {
            if (!RunValueExists()) return;
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, true))
                    if (k != null) k.DeleteValue("K2", false);
            }
            catch { }
        }

        static string TaskXml()
        {
            string user = System.Security.SecurityElement.Escape(Environment.UserDomainName + "\\" + Environment.UserName);
            string exe = System.Security.SecurityElement.Escape(Application.ExecutablePath);
            return "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n" +
                "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\r\n" +
                "  <RegistrationInfo><Description>K2 system monitor overlay</Description></RegistrationInfo>\r\n" +
                "  <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + user + "</UserId></LogonTrigger></Triggers>\r\n" +
                "  <Principals><Principal id=\"Author\"><UserId>" + user + "</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>\r\n" +
                "  <Settings>\r\n" +
                "    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>\r\n" +
                "    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>\r\n" +
                "    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>\r\n" +
                "    <AllowHardTerminate>true</AllowHardTerminate>\r\n" +
                "    <StartWhenAvailable>true</StartWhenAvailable>\r\n" +
                "    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>\r\n" +
                "    <IdleSettings><StopOnIdleEnd>false</StopOnIdleEnd><RestartOnIdle>false</RestartOnIdle></IdleSettings>\r\n" +
                "    <AllowStartOnDemand>true</AllowStartOnDemand>\r\n" +
                "    <Enabled>true</Enabled>\r\n" +
                "    <Hidden>false</Hidden>\r\n" +
                "    <RunOnlyIfIdle>false</RunOnlyIfIdle>\r\n" +
                "    <WakeToRun>false</WakeToRun>\r\n" +
                "    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>\r\n" +
                "    <Priority>7</Priority>\r\n" +
                "  </Settings>\r\n" +
                "  <Actions Context=\"Author\"><Exec><Command>" + exe + "</Command></Exec></Actions>\r\n" +
                "</Task>";
        }

        static int RunSchTasks(string args, out string output)
        {
            output = "";
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(15000);
                    return p.HasExited ? p.ExitCode : -1;
                }
            }
            catch (Exception ex) { Log(ex); return -1; }
        }

        static void Log(Exception ex)
        {
            try { File.AppendAllText(Monitor.LogPath("error.txt"), DateTime.Now + " autostart: " + ex + "\r\n"); } catch { }
        }
    }

    public class MainForm : Form
    {
        static readonly Color BackColor1 = Color.FromArgb(42, 42, 46);
        // Фон самой панели оверлея (настраивается в разделе «Настройки цвета»).
        Color OverlayPanelColor { get { return St.GetColor("color.panel", Color.FromArgb(20, 22, 26)); } }

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
        const int WS_EX_TRANSPARENT = 0x20;
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
        ToolStripMenuItem miEdit;
        bool dragging;
        Point dragStart;
        bool editMode;
        bool resizing;
        int resizeEdge;
        Rectangle resizeStartBounds;
        Point resizeStartMouse;
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
            Loc.Set(St.GetS("ui.lang"));
            if (SanitizeGameLists(St)) St.Save();
        }

        protected override void OnLoad(EventArgs e)
        {
            S = DeviceDpi / 96f;
            ApplyTransparencyKey();
            ApplyWindowSize();
            LoadPos();
            if (RememberMonitor()) St.Save();
            Monitor.LogCleanup();
            BuildTray();
            ContextMenuStrip = menu;
            try { Autostart.Set(St.GetB("ov.autostart")); } catch { }

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
            bool posChanged =
                oldMode != fresh.GetS("pos.mode") ||
                St.GetS("pos.monitor") != fresh.GetS("pos.monitor") ||
                St.GetS("pos.corner") != fresh.GetS("pos.corner") ||
                St.GetI("pos.offX") != fresh.GetI("pos.offX") ||
                St.GetI("pos.offY") != fresh.GetI("pos.offY") ||
                St.GetB("pos.pin") != fresh.GetB("pos.pin");
            St = fresh;
            // Окно настроек могло быть открыто до перемещения/ресайза оверлея.
            // В этом случае не возвращаем его в угол из устаревшего черновика.
            if (oldMode == "manual" && !posChanged) St.Set("pos.mode", "manual");
            bool modeChanged = oldMode != St.GetS("pos.mode");
            bool manual = St.GetS("pos.mode") == "manual";
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
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.SyncFromApplied();
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
            ToolStripMenuItem show = new ToolStripMenuItem(Loc.T("Show/hide overlay"));
            show.Click += delegate { hiddenByUser = !hiddenByUser; UpdateVisible(); };
            menu.Items.Add(show);

            miEdit = new ToolStripMenuItem(Loc.T("Move / resize"));
            miEdit.CheckOnClick = true;
            miEdit.Checked = editMode;
            miEdit.CheckedChanged += delegate
            {
                editMode = miEdit.Checked;
                UpdateRegion();
                ApplyTransparencyKey();
                if (!editMode)
                {
                    Cursor = Cursors.Default;
                    SavePos();
                    St.Save();
                }
                Invalidate();
            };
            menu.Items.Add(miEdit);

            ToolStripMenuItem fit = new ToolStripMenuItem(Loc.T("Fit to content"));
            fit.Click += delegate { FitToContent(); };
            menu.Items.Add(fit);

            ToolStripMenuItem hist = new ToolStripMenuItem(Loc.T("History"));
            hist.Click += delegate { ToggleHistory(); };
            menu.Items.Add(hist);
            menu.Items.Add(new ToolStripSeparator());

            miEnabled = new ToolStripMenuItem(Loc.T("Overlay enabled (master)"));
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
            ToolStripMenuItem open = new ToolStripMenuItem(Loc.T("Settings…"));
            open.ShortcutKeys = Keys.F10;
            open.Click += delegate { OpenSettings(); };
            menu.Items.Add(open);

            ToolStripMenuItem top = new ToolStripMenuItem(Loc.T("Always on top"));
            top.CheckOnClick = true;
            top.Checked = TopMost;
            top.CheckedChanged += delegate { TopMost = top.Checked; };
            menu.Items.Add(top);

            ToolStripMenuItem langMenu = new ToolStripMenuItem(Loc.T("Language"));
            ToolStripMenuItem langEn = new ToolStripMenuItem("English");
            ToolStripMenuItem langRu = new ToolStripMenuItem("Русский");
            langEn.Checked = Loc.Lang == "en";
            langRu.Checked = Loc.Lang == "ru";
            langEn.Click += delegate
            {
                langEn.Checked = true; langRu.Checked = false;
                SetLanguage("en");
            };
            langRu.Click += delegate
            {
                langEn.Checked = false; langRu.Checked = true;
                SetLanguage("ru");
            };
            langMenu.DropDownItems.Add(langEn);
            langMenu.DropDownItems.Add(langRu);
            menu.Items.Add(langMenu);

            ToolStripMenuItem excl = new ToolStripMenuItem(Loc.T("\"Game only\" exceptions"));
            ToolStripMenuItem itAlways = new ToolStripMenuItem(Loc.T("Mark active window: ALWAYS a game"));
            itAlways.Click += delegate { MarkGame("ov.alwaysGame"); };
            ToolStripMenuItem itNever = new ToolStripMenuItem(Loc.T("Mark active window: NEVER a game"));
            itNever.Click += delegate { MarkGame("ov.neverGame"); };
            excl.DropDownItems.Add(itAlways);
            excl.DropDownItems.Add(itNever);
            menu.Items.Add(excl);

            miInterval = new ToolStripMenuItem(Loc.T("Update interval"));
            int[] ms = new int[] { 250, 500, 1000, 2000, 5000 };
            foreach (int msec in ms)
            {
                string label = msec < 1000
                    ? msec.ToString(Loc.Culture) + Loc.T(" ms")
                    : (msec / 1000.0).ToString("0.#", Loc.Culture) + Loc.T(" s");
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
            ToolStripMenuItem exit = new ToolStripMenuItem(Loc.T("Exit"));
            exit.Click += delegate { Close(); };
            menu.Items.Add(exit);

            tray = new NotifyIcon();
            tray.Icon = MakeIcon();
            tray.Text = Loc.T("K2 — settings: F10 (double-click to show)");
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { hiddenByUser = false; Visible = true; Activate(); };
            tray.Visible = true;
        }

        void SetLanguage(string lang)
        {
            St.Set("ui.lang", lang);
            St.Save();
            if (RestartPrompt.Ask(null) && !Program.RestartApp())
                MessageBox.Show(Loc.T("Could not restart K2. Start it manually."), "K2");
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
                MessageBox.Show(Loc.T("Could not detect the game window. Start the game and try again."), "K2");
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
            if (resizing) { Invalidate(); return; }
            OverlayDraw probe = new OverlayDraw(null, St, cur, StatsNow, S);
            float h = probe.DrawWithWidth(null, ClientSize.Width);
            int minW = probe.MinWidth;
            probe.DisposeFonts();

            bool widthChanged = false;
            if (St.GetB("look.autoWidth") && minW > 0)
            {
                int pct = WidthPercent(minW);
                if (pct != St.GetI("look.width"))
                {
                    St.Set("look.width", pct);
                    ApplyWindowSize();
                    widthChanged = true;
                }
            }

            int hi = (int)(h + PadBottom());
            int manual = St.GetI("look.heightPx");
            if (manual > 0) hi = Math.Max(hi, (int)Math.Round(manual * S));
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
            else if (widthChanged)
            {
                if (St.GetS("pos.mode") != "manual") ApplyPosition();
                else ClampToScreen();
            }
            Invalidate();
        }

        int WidthPercent(int px)
        {
            float scale = Math.Max(50, Math.Min(200, St.GetI("look.scale"))) / 100f;
            float unit = OverlayDraw.BaseW * scale * S;
            if (unit <= 0) return 100;
            int pct = (int)Math.Ceiling(px / unit * 100.0);
            return Math.Max(50, Math.Min(200, pct));
        }

        void FitToContent()
        {
            OverlayDraw probe = new OverlayDraw(null, St, cur, StatsNow, S);
            probe.DrawWithWidth(null, ClientSize.Width);
            int minW = probe.MinWidth;
            probe.DisposeFonts();
            if (minW > 0) St.Set("look.width", WidthPercent(minW));
            St.Set("look.heightPx", 0);
            St.Save();
            ApplyWindowSize();
            UpdateHeightAndRepaint();
            RenderLayered();
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.SyncFromApplied();
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
            if (s.CpuTemp == null) missing.Add(Loc.T("CPU temperature"));
            if (s.GpuTemp == null) missing.Add(Loc.T("GPU temperature"));
            if (s.CpuPower == null) missing.Add(Loc.T("CPU power"));
            if (s.CpuFreq == null) missing.Add(Loc.T("core clocks"));
            if (missing.Count == 0) return;
            try
            {
                tray.ShowBalloonTip(8000, Loc.T("K2: not all sensors are available"),
                    Loc.T("Missing: ") + string.Join(", ", missing.ToArray()) +
                    Loc.T(". Check UAC/admin rights or Memory Integrity (HVCI) in Windows Security."),
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
                            Color panel = OverlayPanelColor;
                            using (GraphicsPath p = RoundPath(new Rectangle(0, 0, Width - 1, Height - 1), St.GetI("look.radius") * S))
                            using (Brush b = new SolidBrush(Color.FromArgb(bgAlpha, panel.R, panel.G, panel.B)))
                                g.FillPath(b, p);
                        }

                        OverlayDraw od = new OverlayDraw(g, St, cur, StatsNow, S);
                        od.DrawWithWidth(g, Width);
                        graphRectClient = od.LastGraphRect;
                        od.DisposeFonts();

                        if (editMode) DrawEditFrame(g);

                        if (!cur.Ready)
                        {
                            using (Brush b = new SolidBrush(Color.FromArgb(200, 200, 200)))
                            using (Font f = new Font("Segoe UI", 9.75f * S))
                            using (StringFormat sf = new StringFormat())
                            {
                                sf.Alignment = StringAlignment.Center;
                                g.DrawString(Loc.T("initializing sensors…"), f, b,
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

        void DrawEditFrame(Graphics g)
        {
            Color c = Color.FromArgb(0, 191, 166);
            using (Pen p = new Pen(Color.FromArgb(235, c), 2f))
            {
                p.DashStyle = DashStyle.Dash;
                g.DrawRectangle(p, 1, 1, Width - 3, Height - 3);
            }
            int s = 6;
            using (Brush b = new SolidBrush(Color.FromArgb(255, c)))
            {
                g.FillRectangle(b, 1, 1, s, s);
                g.FillRectangle(b, Width - 1 - s, 1, s, s);
                g.FillRectangle(b, 1, Height - 1 - s, s, s);
                g.FillRectangle(b, Width - 1 - s, Height - 1 - s, s, s);
                g.FillRectangle(b, Width / 2 - s / 2, 1, s, s);
                g.FillRectangle(b, Width / 2 - s / 2, Height - 1 - s, s, s);
                g.FillRectangle(b, 1, Height / 2 - s / 2, s, s);
                g.FillRectangle(b, Width - 1 - s, Height / 2 - s / 2, s, s);
            }
        }

        // ---------- interaction ----------
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (editMode)
                {
                    int edge = EdgeAt(e.Location);
                    if (edge != 0 && edge != 3 && edge != 12)
                    {
                        StartResize(edge);
                        base.OnMouseDown(e);
                        return;
                    }
                    dragging = true;
                    dragStart = e.Location;
                    tip.SetToolTip(this, Loc.T("Drag to move. Click the graph for history. Right-click for menu"));
                    base.OnMouseDown(e);
                    return;
                }
                if (graphRectClient.Contains(new Point(e.X, e.Y)))
                {
                    ToggleHistory();
                    base.OnMouseDown(e);
                    return;
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (resizing)
            {
                Point p = Cursor.Position;
                int dx = p.X - resizeStartMouse.X;
                int dy = p.Y - resizeStartMouse.Y;
                Rectangle b = resizeStartBounds;
                float sc = Math.Max(50, Math.Min(200, St.GetI("look.scale"))) / 100f;
                int minW = (int)Math.Round(OverlayDraw.BaseW * 0.5f * sc * S);
                int minH = (int)Math.Round(80 * S);
                if ((resizeEdge & 1) != 0) { b.X += dx; b.Width -= dx; }
                if ((resizeEdge & 2) != 0) b.Width += dx;
                if ((resizeEdge & 4) != 0) { b.Y += dy; b.Height -= dy; }
                if ((resizeEdge & 8) != 0) b.Height += dy;
                if (b.Width < minW)
                {
                    if ((resizeEdge & 1) != 0) b.X = resizeStartBounds.Right - minW;
                    b.Width = minW;
                }
                if (b.Height < minH)
                {
                    if ((resizeEdge & 4) != 0) b.Y = resizeStartBounds.Bottom - minH;
                    b.Height = minH;
                }
                Bounds = b;
                RenderLayered();
                base.OnMouseMove(e);
                return;
            }
            if (editMode && !dragging) Cursor = CursorForEdge(EdgeAt(e.Location));
            if (dragging && e.Button == MouseButtons.Left)
                Location = new Point(Left + e.X - dragStart.X, Top + e.Y - dragStart.Y);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (resizing)
            {
                FinishResize();
                base.OnMouseUp(e);
                return;
            }
            if (dragging)
            {
                dragging = false;
                St.Set("pos.mode", "manual");
                RememberMonitor();
                SavePos();
                St.Save(); // ручной режим и координаты должны пережить перезапуск
                RenderLayered();
                if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.SyncFromApplied();
            }
            base.OnMouseUp(e);
        }

        int EdgeAt(Point p)
        {
            int m = 6;
            bool l = p.X <= m;
            bool r = p.X >= Width - m;
            bool t = p.Y <= m;
            bool b = p.Y >= Height - m;
            return (l ? 1 : 0) | (r ? 2 : 0) | (t ? 4 : 0) | (b ? 8 : 0);
        }

        Cursor CursorForEdge(int e)
        {
            bool l = (e & 1) != 0, r = (e & 2) != 0;
            bool t = (e & 4) != 0, b = (e & 8) != 0;
            if ((l && t) || (r && b)) return Cursors.SizeNWSE;
            if ((r && t) || (l && b)) return Cursors.SizeNESW;
            if (l || r) return Cursors.SizeWE;
            if (t || b) return Cursors.SizeNS;
            return Cursors.SizeAll;
        }

        void StartResize(int edge)
        {
            resizing = true;
            resizeEdge = edge;
            resizeStartBounds = Bounds;
            resizeStartMouse = Cursor.Position;
            if (St.GetB("look.autoWidth")) St.Set("look.autoWidth", false);
            Capture = true;
            Cursor = CursorForEdge(edge);
        }

        void FinishResize()
        {
            if (!resizing) return;
            resizing = false;
            Capture = false;
            St.Set("look.width", WidthPercent(Width));
            if ((resizeEdge & 12) != 0)
                St.Set("look.heightPx", Math.Max(0, (int)Math.Round(Height / S)));
            St.Set("pos.mode", "manual");
            RememberMonitor();
            SavePos();
            St.Save();
            ApplyWindowSize();
            UpdateHeightAndRepaint();
            RenderLayered();
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.SyncFromApplied();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            if (!Capture && resizing) FinishResize();
            base.OnMouseCaptureChanged(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (editMode && !dragging && !resizing) Cursor = Cursors.Default;
            base.OnMouseLeave(e);
        }

        bool RememberMonitor()
        {
            Screen[] all = Screen.AllScreens;
            for (int i = 0; i < all.Length; i++)
                if (all[i].Bounds.Contains(new Point(Left + Width / 2, Top + Math.Min(Height / 2, 50))))
                {
                    St.Set("pos.monitor", i);
                    return true;
                }
            return false;
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
            int rad = editMode ? 0 : (int)Math.Round(St.GetI("look.radius") * S);
            using (GraphicsPath p = RoundPath(new Rectangle(0, 0, Width, Height), rad))
                Region = new Region(p);
        }

        // Окно всегда layered: содержимое рисуется в 32-битный bitmap с альфой и
        // отправляется через UpdateLayeredWindow. Это даёт плавную прозрачность фона
        // (0–100%) и сохраняет непрозрачный текст.
        void ApplyTransparencyKey()
        {
            if (!IsHandleCreated || St == null) return;
            int ex = GetWindowLong(Handle, GWL_EXSTYLE);
            int want = ex | WS_EX_LAYERED;
            // В обычном режиме оверлей не должен перехватывать мышь:
            // клики проходят в окно под ним. В режиме правки — наоборот.
            if (editMode) want &= ~WS_EX_TRANSPARENT;
            else want |= WS_EX_TRANSPARENT;
            if (want != ex)
            {
                SetWindowLong(Handle, GWL_EXSTYLE, want);
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
