using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace K2
{
    public class SettingsForm : Form
    {
        // ---- палитра настроек (тёмно-синяя с бирюзовым) ----
        static readonly Color WBack = Color.FromArgb(20, 26, 34);
        static readonly Color NavBack = Color.FromArgb(16, 22, 30);
        static readonly Color NavSel = Color.FromArgb(34, 49, 66);
        static readonly Color NavText = Color.FromArgb(232, 240, 245);
        static readonly Color NavDim = Color.FromArgb(120, 140, 158);
        static readonly Color Acc = Color.FromArgb(0, 191, 166);
        static readonly Color AccDim = Color.FromArgb(30, 90, 84);
        static readonly Color LineC = Color.FromArgb(38, 50, 65);
        static readonly Color CtlBack = Color.FromArgb(30, 41, 54);
        static readonly Color CtlBorder = Color.FromArgb(52, 70, 92);
        static readonly Color OffC = Color.FromArgb(70, 86, 104);
        static readonly Color WarnText = Color.FromArgb(255, 138, 96);

        class SRow
        {
            public string Kind;      // toggle, slider, combo, hotkey, buttons, info, section, order
            public string Label;
            public string Key;
            public string[] Options;
            public int Min, Max;
            public Rectangle Rect, Ctl;
            public string[] Buttons;
            public int OrderIndex;
            public object Tag;
            public string FallbackKey;
            public Color DefaultColor;
        }

        MainForm owner;
        Settings draft;
        string section = "general";
        List<SRow> rows = new List<SRow>();
        int scroll, scrollMax;

        SRow popupRow;
        Rectangle popupRect;
        string[] popupItems;
        List<Rectangle> popupHits = new List<Rectangle>();
        List<int> popupHitIdx = new List<int>();
        int popupScroll;

        SRow dragRow;
        SRow captureRow;
        bool topDrag;
        Point topDragOff;

        PreviewControl prev;
        Stats fakeStats = new Stats();
        Timer anim;
        Random rnd = new Random();
        const int PreviewW = 340, PreviewH = 560;
        Snapshot fake = null;

        RectangleF closeRect;
        RectangleF maxRect;
        bool maxed;
        Rectangle restoreBounds = Rectangle.Empty;
        Rectangle rectApply, rectApplyClose, rectCancel, rectExport, rectImport, rectReset;
        Rectangle rectCoffee;
        const string CoffeeUrl = "https://www.donationalerts.com/r/jash_k2";
        Font fTitle, fBread, fNav, fNavCat, fLabel, fValue, fSmall, fBig2, fClose;
        static CultureInfo Cul { get { return Loc.Culture; } }

        static readonly string[] SectionNames = new string[]
        {
            "general", "pos", "look", "colors", "keys", "fps", "gpu", "cpu", "ram", "power", "stat", "ftg"
        };
        static readonly string[] SectionTitles = new string[]
        {
            "General", "Position", "Appearance", "Colors", "Hotkeys",
            "FPS", "GPU", "CPU", "RAM", "Power supply", "Statistics", "Frame time graph"
        };

        public SettingsForm(MainForm owner)
        {
            this.owner = owner;
            this.draft = owner.St.Clone();
            Text = Loc.T("K2 — Settings");
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = WBack;
            TopMost = false;
            ShowInTaskbar = true;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            ClientSize = new Size(1100, 720);
            MinimumSize = new Size(920, 560);
            try { Icon = MainForm.AppIcon(); } catch { }
            fTitle = new Font("Segoe UI", 11.5f, FontStyle.Bold);
            fBread = new Font("Segoe UI", 9.75f);
            fNav = new Font("Segoe UI", 9.75f);
            fNavCat = new Font("Segoe UI", 8.25f, FontStyle.Bold);
            fLabel = new Font("Segoe UI", 10f);
            fValue = new Font("Segoe UI", 9.5f);
            fSmall = new Font("Segoe UI", 8.5f);
            fBig2 = new Font("Segoe UI", 20f, FontStyle.Bold);
            fClose = new Font("Segoe UI", 11f);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            BuildRows();
            RebuildPreview();
            StartPreviewAnimation();
        }

        public void RefreshPreview() { RebuildPreview(); Invalidate(); }

        // значение из трей-меню может измениться, пока окно открыто — подтягиваем
        public void SyncFromApplied()
        {
            draft.Set("ov.interval", owner.St.GetS("ov.interval"));
            draft.Set("look.width", owner.St.GetS("look.width"));
            draft.Set("look.heightPx", owner.St.GetS("look.heightPx"));
            draft.Set("look.autoWidth", owner.St.GetS("look.autoWidth"));
            draft.Set("pos.mode", owner.St.GetS("pos.mode"));
            draft.Set("pos.monitor", owner.St.GetS("pos.monitor"));
            draft.Set("pos.corner", owner.St.GetS("pos.corner"));
            draft.Set("pos.offX", owner.St.GetS("pos.offX"));
            draft.Set("pos.offY", owner.St.GetS("pos.offY"));
            draft.Set("pos.pin", owner.St.GetS("pos.pin"));
            Invalidate();
        }

        // ---------- структура разделов ----------
        SRow R(string kind, string label, string key)
        {
            SRow r = new SRow(); r.Kind = kind; r.Label = Loc.T(label); r.Key = key;
            rows.Add(r); return r;
        }
        SRow T(string label, string key) { return R("toggle", label, key); }
        SRow SL(string label, string key, int min, int max)
        {
            SRow r = R("slider", label, key); r.Min = min; r.Max = max; return r;
        }
        SRow CB(string label, string key, string opts, string[] vals)
        {
            SRow r = R("combo", label, key);
            string[] parts = opts.Split(',');
            if (key != "ui.lang")
                for (int i = 0; i < parts.Length; i++) parts[i] = Loc.T(parts[i].Trim());
            r.Options = parts; r.Tag = vals; return r;
        }
        SRow INFO(string text) { return R("info", text, null); }
        SRow SEC(string text) { return R("section", text, null); }
        SRow HK(string label, string key) { return R("hotkey", label, key); }
        SRow COLOR(string label, string key, Color def)
        {
            SRow r = R("color", label, key);
            r.DefaultColor = def;
            return r;
        }
        SRow COLOR(string label, string key, string fallbackKey, Color def)
        {
            SRow r = R("color", label, key);
            r.FallbackKey = fallbackKey;
            r.DefaultColor = def;
            return r;
        }

        void BuildRows()
        {
            rows.Clear(); scroll = 0; popupRow = null; captureRow = null;
            switch (section)
            {
                case "general":
                    SEC("Main settings");
                    CB("Language", "ui.lang", "English,Русский", new string[] { "en", "ru" });
                    INFO("Restart K2 to apply the language.");
                    T("Enable overlay", "ov.enabled");
                    T("Run at Windows startup", "ov.autostart");
                    T("Check sensors at startup", "ov.checkStart");
                    T("\"Game only\" mode", "ov.onlyGame");
                    CB("Update interval", "ov.interval", "250 ms,500 ms,1 s,2 s,5 s", new string[] { "250", "500", "1000", "2000", "5000" });
                    SEC("Units");
                    CB("Temperature", "ov.tempUnit", "°C (Celsius),°F (Fahrenheit)", new string[] { "C", "F" });
                    CB("Frequency", "ov.freqUnit", "GHz,MHz", new string[] { "GHz", "MHz" });
                    CB("Memory units", "ov.memUnit", "GB,MB", new string[] { "GB", "MB" });
                    INFO("Game only exceptions are marked from the tray icon menu while the overlay is active.");
                    break;

                case "pos":
                    SEC("Overlay position");
                    R("buttons", "", "pos.movebtn").Buttons = new string[] { "Move overlay", "Reset position" };
                    CB("Overlay anchoring", "pos.mode", "Primary monitor,Manual position", new string[] { "pin", "manual" });
                    T("Pin overlay to monitor", "pos.pin");
                    string[] monOpts = new string[Screen.AllScreens.Length];
                    string[] monVals = new string[Screen.AllScreens.Length];
                    for (int i = 0; i < monOpts.Length; i++) { monOpts[i] = Loc.T("Monitor ") + (i + 1); monVals[i] = i.ToString(); }
                    CB("Monitor", "pos.monitor", string.Join(",", monOpts), monVals);
                    INFO("A pinned overlay stays on the selected monitor. You can still drag it with the mouse — \"Reset position\" removes the pin.");
                    CB("Screen corner", "pos.corner", "Top right,Top left,Bottom right,Bottom left,Center", new string[] { "TR", "TL", "BR", "BL", "C" });
                    SL("Offset X", "pos.offX", -100, 100);
                    SL("Offset Y", "pos.offY", -100, 100);
                    SEC("Block order");
                    AddOrderRows();
                    break;

                case "look":
                    SEC("Shape and size");
                    CB("Overlay mode", "look.mode", "Floating,Pinned to position", new string[] { "float", "pin" });
                    SL("Overlay width", "look.width", 50, 200);
                    SL("Window scale", "look.scale", 60, 200);
                    SL("Row height (\"Row\")", "look.rowH", 16, 40);
                    T("Auto width (fit to content)", "look.autoWidth");
                    INFO("When enabled, the overlay width follows the visible blocks. Turn it off to set the width manually. The tray menu also has \"Move / resize\" and \"Fit to content\".");
                    SEC("Overlay background");
                    SL("Background transparency", "look.opacity", 0, 100);
                    SL("Corner radius", "look.radius", 0, 30);
                    INFO("At 100% the background disappears completely — only the values and overlay elements remain. The corner radius is applied right after \"Apply\".");
                    SEC("Fonts");
                    string[] fams = FontFamilies();
                    CB("Font", "look.font", JoinNoComma(fams), fams);
                    T("Apply font to program windows too", "look.fontAll");
                    SL("Font size", "look.fontSize", 60, 150);
                    SEC("Overlay elements");
                    T("Show time", "look.showTime");
                    break;

                case "colors":
                    Color cPanel = Color.FromArgb(20, 22, 26);
                    Color cHeader = Color.FromArgb(240, 240, 240);
                    Color cLabel = Color.FromArgb(166, 166, 166);
                    Color cValue = Color.FromArgb(240, 240, 240);
                    Color cLine = Color.FromArgb(82, 82, 88);
                    Color cMark = Color.FromArgb(120, 120, 126);
                    Color cAccent = Color.FromArgb(0, 191, 166);
                    Color cStat = Color.FromArgb(255, 170, 80);
                    Color cGreen = Color.FromArgb(46, 204, 64);
                    Color cYellow = Color.FromArgb(229, 198, 75);
                    Color cOrange = Color.FromArgb(240, 140, 60);
                    Color cRed = Color.FromArgb(230, 80, 60);
                    Color cFtg = Color.FromArgb(245, 45, 38);

                    R("buttons", "", "color.resetbtn").Buttons = new string[] { "Defaults" };
                    INFO("Colors apply to the overlay only; the settings window and tray menu keep their current style.");

                    SEC("Overlay background");
                    COLOR("Panel background", "color.panel", cPanel);
                    SL("Background transparency", "look.opacity", 0, 100);
                    INFO("At 100% the panel background is fully transparent; lower the value to see the selected color.");
                    SEC("Base text");
                    COLOR("Header text", "color.header", cHeader);
                    COLOR("Labels", "color.label", cLabel);
                    COLOR("Values", "color.value", cValue);
                    SEC("Separators and hints");
                    COLOR("Separators", "color.line", cLine);
                    COLOR("Time and hints", "color.mark", cMark);
                    SEC("Accents");
                    COLOR("K2 accent", "color.accent", cAccent);
                    COLOR("Accent values", "color.stat", cStat);
                    SEC("Load and temperature zones");
                    COLOR("Normal", "color.green", cGreen);
                    COLOR("Warning", "color.yellow", cYellow);
                    COLOR("High", "color.orange", cOrange);
                    COLOR("Critical", "color.red", cRed);
                    SEC("Graphs");
                    COLOR("Frame time graph", "color.ftg", cFtg);

                    SEC("FPS block");
                    COLOR("Header text", "color.fps.header", "color.header", cHeader);
                    COLOR("Labels", "color.fps.label", "color.label", cLabel);
                    COLOR("Values", "color.fps.value", "color.stat", cStat);

                    SEC("GPU block");
                    COLOR("Header text", "color.gpu.header", "color.header", cHeader);
                    COLOR("Labels", "color.gpu.label", "color.label", cLabel);
                    COLOR("Values", "color.gpu.value", "color.value", cValue);
                    T("Use zone colors", "color.gpu.zones");

                    SEC("CPU block");
                    COLOR("Header text", "color.cpu.header", "color.header", cHeader);
                    COLOR("Labels", "color.cpu.label", "color.label", cLabel);
                    COLOR("Values", "color.cpu.value", "color.value", cValue);
                    T("Use zone colors", "color.cpu.zones");

                    SEC("RAM block");
                    COLOR("Header text", "color.ram.header", "color.header", cHeader);
                    COLOR("Labels", "color.ram.label", "color.label", cLabel);
                    COLOR("Values", "color.ram.value", "color.value", cValue);

                    SEC("Power block");
                    COLOR("Header text", "color.power.header", "color.header", cHeader);
                    COLOR("Labels", "color.power.label", "color.label", cLabel);
                    COLOR("Values", "color.power.value", "color.value", cValue);

                    SEC("Statistics block");
                    COLOR("Header text", "color.stat.header", "color.header", cHeader);
                    COLOR("Labels", "color.stat.label", "color.label", cLabel);
                    COLOR("Values", "color.stat.value", "color.stat", cStat);

                    INFO("Zone colors apply to load and temperature. Turn them off in the GPU or CPU block to use the block value color for every reading.");
                    break;

                case "keys":
                    SEC("Global hotkeys");
                    HK("Show / hide overlay", "hk.show");
                    HK("Reset statistics", "hk.reset");
                    HK("Toggle overlay mode", "hk.mode");
                    HK("Open settings", "hk.settings");
                    HK("Start / stop measurement", "hk.measure");
                    INFO("If the shortcut is taken by another app, it simply won't register after \"Apply\".");
                    INFO("Click the key field and press a new shortcut; Esc cancels input.");
                    break;

                case "fps":
                    SEC("Displayed metrics");
                    T("Show FPS", "fps.show");
                    T("AVG", "fps.avg");
                    T("1% Low", "fps.low1");
                    T("0.1% Low", "fps.low01");
                    T("Frame time", "fps.lat");
                    T("Process/window", "fps.proc");
                    T("API and resolution", "fps.api");
                    T("Hide metrics when FPS = N/A", "fps.hideNa");
                    INFO("FPS is measured via Desktop Duplication — the frames actually shown on the active window's monitor. With VSync it does not exceed the display refresh rate. A static image produces no new frames, so the counter stays silent and the block is hidden by \"Hide if N/A\". The preview always shows demo FPS values so you can see how the toggles work; the real overlay shows FPS only while it is actually being measured (a game is active).");
                    break;

                case "gpu":
                    SEC("Metrics");
                    T("Show \"GPU\" block", "gpu.show");
                    T("Load", "gpu.load");
                    T("Temperature", "gpu.temp");
                    T("Hotspot temperature", "gpu.hotspot");
                    T("Memory temperature", "gpu.memtemp");
                    T("Clock", "gpu.clock");
                    T("Memory clock", "gpu.memclock");
                    T("Voltage", "gpu.volt");
                    T("Power", "gpu.power");
                    T("Fan speed", "gpu.fanrpm");
                    T("Fan speed, %", "gpu.fanpct");
                    T("VRAM", "gpu.vram");
                    SEC("Warning thresholds");
                    T("Color-code GPU load", "gpu.loadcolor");
                    SL("Warning temperature", "gpu.warn", 50, 95);
                    SL("Critical temperature", "gpu.crit", 60, 110);
                    break;

                case "cpu":
                    SEC("Metrics");
                    T("Show \"CPU\" block", "cpu.show");
                    T("Load", "cpu.load");
                    T("Temperature", "cpu.temp");
                    T("Core temperatures", "cpu.ccd");
                    T("Clock (max.)", "cpu.clock");
                    T("Effective clock", "cpu.eff");
                    T("Power", "cpu.power");
                    T("Fan speed", "cpu.fanrpm");
                    T("Fan speed, %", "cpu.fanpct");
                    SEC("Cores");
                    T("Per-core clocks", "cpu.corefreqs");
                    T("Per-core load graph", "cpu.coregraph");
                    T("Labels under graph", "cpu.corelabels");
                    SEC("Warning thresholds");
                    T("Color-code CPU load", "cpu.loadcolor");
                    SL("Warning temperature", "cpu.warn", 50, 95);
                    SL("Critical temperature", "cpu.crit", 60, 110);
                    break;

                case "ram":
                    SEC("Main settings");
                    T("Show \"RAM\" block", "ram.show");
                    T("Used", "ram.used");
                    T("Load", "ram.load");
                    T("Show memory speed in MHz", "ram.speed");
                    CB("Memory speed source", "ram.speedMode", "Auto (system data),Auto+ (CPU memory controller limit),Manual", new string[] { "auto", "hw", "manual" });
                    SL("Manual speed, MHz", "ram.speedManual", 800, 8000);
                    INFO("\"Auto+\" caps the SPD rating at the CPU memory controller limit (per Intel spec) — a Xeon v4 with 3200 modules will show the actual 2400. \"Manual\" lets you set the number yourself.");
                    T("RAM DIMM temperatures", "ram.dimmt");
                    CB("Memory format", "ram.format", "Used / total,Used only,Percent", new string[] { "usedtotal", "used", "pct" });
                    break;

                case "power":
                    SEC("Power estimate");
                    T("Show power block", "power.show");
                    SL("Other components, W", "power.other", 0, 300);
                    SL("PSU efficiency, %", "power.eff", 50, 100);
                    INFO("The total is approximate: CPU + GPU + other components. Additional power sensors (RAM, chipset, PSU) are used automatically when available. Wall power also accounts for PSU efficiency; if the PSU reports input power, that value is used.");
                    break;

                case "stat":
                    SEC("Block contents");
                    T("Show statistics block", "stat.show");
                    T("GPU temperature", "stat.gput");
                    T("CPU temperature", "stat.cput");
                    T("VRAM temperature", "stat.vramt");
                    T("Hotspot", "stat.hotspot");
                    T("GPU voltage", "stat.gpuv");
                    T("GPU power", "stat.gpup");
                    T("CPU power", "stat.cpup");
                    T("Auto-reset on game change", "stat.autoreset");
                    SEC("Current session");
                    R("buttons", "", "stat.resetbtn").Buttons = new string[] { "Reset statistics" };
                    break;

                case "ftg":
                    SEC("Main settings");
                    T("Show graph", "ft.show");
                    SL("Height", "ft.height", 40, 160);
                    SL("Fill opacity", "ft.fill", 0, 100);
                    T("Show frame time", "ft.showtime");
                    INFO("The frame time graph will appear once enough samples have been collected.");
                    break;
            }
        }

        void AddOrderRows()
        {
            string[] blocks = OrderList();
            for (int i = 0; i < blocks.Length; i++)
            {
                SRow r = R("order", OrderName(blocks[i]), blocks[i].Trim());
                r.OrderIndex = i;
            }
        }

        string[] OrderList()
        {
            string o = draft.GetS("pos.order");
            if (o.Length == 0) o = "fps,gpu,cpu,ram,stat,ftg";
            return o.Split(',');
        }

        string OrderName(string b)
        {
            switch (b.Trim())
            {
                case "fps": return "FPS";
                case "gpu": return "GPU";
                case "cpu": return "CPU";
                case "ram": return "RAM";
                case "power": return "Power supply";
                case "stat": return "Statistics";
                case "ftg": return "Frame time graph";
            }
            return b;
        }

        bool BlockShown(string b)
        {
            switch (b.Trim())
            {
                case "fps": return draft.GetB("fps.show");
                case "gpu": return draft.GetB("gpu.show");
                case "cpu": return draft.GetB("cpu.show");
                case "ram": return draft.GetB("ram.show");
                case "power": return draft.GetB("power.show");
                case "stat": return draft.GetB("stat.show");
                case "ftg": return draft.GetB("ft.show");
            }
            return true;
        }

        static string JoinNoComma(string[] a)
        {
            List<string> clean = new List<string>();
            foreach (string s in a) clean.Add(s.Replace(",", " "));
            return string.Join(",", clean.ToArray());
        }

        static string[] FontFamilies()
        {
            List<string> names = new List<string>();
            foreach (FontFamily ff in FontFamily.Families) names.Add(ff.Name);
            if (!names.Contains("Segoe UI")) names.Insert(0, "Segoe UI");
            return names.ToArray();
        }

        // ---------- значения ----------
        string Val(SRow r)
        {
            string[] vals = r.Tag as string[];
            if (r.Kind != "combo" || vals == null) return draft.GetS(r.Key);
            string v = draft.GetS(r.Key);
            for (int i = 0; i < vals.Length; i++)
                if (vals[i] == v) return r.Options[i];
            return r.Options.Length > 0 ? r.Options[0] : v;
        }

        void OpenCombo(SRow r)
        {
            string[] vals = r.Tag as string[];
            if (vals == null || vals.Length == 0) return;
            popupRow = r;
            popupItems = r.Options;
            popupScroll = 0;
            string cur = draft.GetS(r.Key);
            for (int i = 0; i < vals.Length; i++)
                if (vals[i] == cur) { popupScroll = Math.Max(0, i - 5); break; }
        }

        void AfterChange()
        {
            RebuildPreview();
            Invalidate();
        }

        // растягивание окна без рамки за края: нативный hit-test
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;
            if (m.Msg == WM_NCHITTEST && !maxed)
            {
                base.WndProc(ref m);
                long lp = m.LParam.ToInt64();
                int sx = (short)(lp & 0xFFFF);
                int sy = (short)((lp >> 16) & 0xFFFF);
                Point p = PointToClient(new Point(sx, sy));
                int b = 7;
                bool L = p.X >= 0 && p.X < b;
                bool R = p.X >= ClientSize.Width - b && p.X < ClientSize.Width;
                bool T = p.Y >= 0 && p.Y < b;
                bool B = p.Y >= ClientSize.Height - b && p.Y < ClientSize.Height;
                int code = 0;
                if (T && L) code = 13;        // HTTOPLEFT
                else if (T && R) code = 14;   // HTTOPRIGHT
                else if (B && L) code = 16;   // HTBOTTOMLEFT
                else if (B && R) code = 17;   // HTBOTTOMRIGHT
                else if (L) code = 10;        // HTLEFT
                else if (R) code = 11;        // HTRIGHT
                else if (T) code = 12;        // HTTOP
                else if (B) code = 15;        // HTBOTTOM
                if (code != 0) m.Result = (IntPtr)code;
                return;
            }
            base.WndProc(ref m);
        }

        // ---------- превью ----------
        static Snapshot FakeSnapshot()
        {
            return Monitor.MakeFake();
        }

        void SyncPreviewFps()
        {
            if (fake == null) return;
            // Пока оверлей реально считает FPS (игра активна), превью показывает живые
            // значения. Иначе подставляем демо-значения, чтобы блок FPS и его настройки
            // были видны в предпросмотре.
            if (owner.FpsLive)
            {
                Snapshot l = owner.Live;
                fake.Fps = l.Fps;
                fake.FpsAvg = l.FpsAvg;
                fake.FpsLow1 = l.FpsLow1;
                fake.FpsLow01 = l.FpsLow01;
                fake.FpsLatency = l.FpsLatency;
                fake.FgProc = l.FgProc;
                fake.Resolution = l.Resolution;
                fake.RenderApi = l.RenderApi;
                fake.PresentMode = l.PresentMode;
            }
            else
            {
                // Вне игры реального FPS нет, но превью должно показывать, как выглядит
                // блок и что меняют переключатели, поэтому берём демонстрационные значения.
                Monitor.FillDemoFps(fake);
            }
        }

        void RebuildPreview()
        {
            if (fake == null) fake = FakeSnapshot();
            SyncPreviewFps();
            for (int i = 0; i < 40; i++) fakeStats.Update(fake);
            try
            {
                UpdatePreview();
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(Monitor.LogPath("error.txt"), DateTime.Now + " preview: " + ex + "\r\n"); } catch { }
            }
        }

        void UpdatePreview()
        {
            if (fake == null) fake = FakeSnapshot();
            if (prev == null || prev.IsDisposed)
            {
                prev = new PreviewControl();
                Controls.Add(prev);
            }
            prev.St = draft;
            prev.Snap = fake;
            prev.Stat = fakeStats;
            // сначала размер/позиция, потом перерисовка
            float sc = PreviewControl.CalcScale(draft, PreviewW - 16);
            Size need = PreviewControl.MeasureSize(draft, fake, fakeStats, sc);
            int maxH = Math.Max(120, Height - TopH() - BottomH() - 80);
            if (need.Height > maxH)
            {
                sc = Math.Max(0.15f, sc * maxH / (float)need.Height);
                need = PreviewControl.MeasureSize(draft, fake, fakeStats, sc);
            }
            prev.ScaleF = sc;
            prev.Size = need;
            LayoutPreview();
            prev.Invalidate();
            prev.BringToFront();
        }

        void ToggleMax()
        {
            if (!maxed)
            {
                restoreBounds = new Rectangle(Location, Size);
                Screen scr = Screen.FromControl(this);
                Rectangle wa = scr.WorkingArea;
                Location = wa.Location;
                Size = wa.Size;
                maxed = true;
            }
            else
            {
                Location = restoreBounds.Location;
                Size = restoreBounds.Size;
                maxed = false;
            }
            LayoutPreview();
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (e.Y < TopH() && e.X < Width - 90 && !maxRect.Contains(e.Location) && !closeRect.Contains(e.Location))
            {
                ToggleMax();
                return;
            }
            base.OnMouseDoubleClick(e);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            LayoutPreview();
            Invalidate();
        }

        bool PreviewOn() { return Width >= 1100; }

        void LayoutPreview()
        {
            if (prev == null || prev.IsDisposed) return;
            prev.Visible = PreviewOn();
            if (!prev.Visible) return;
            int px = Width - PreviewPanelW() + 10;
            int x = px + (PreviewW - prev.Width) / 2;
            if (x < px) x = px;
            int boxBottom = Height - BottomH() - 24;
            int y = TopH() + 50;
            if (y + prev.Height > boxBottom) y = Math.Max(TopH() + 50, boxBottom - prev.Height);
            prev.Location = new Point(x, y);
        }

        void StartPreviewAnimation()
        {
            anim = new Timer();
            anim.Interval = 900;
            anim.Tick += delegate
            {
                if (prev == null || !Visible) return;
                fake.CpuLoad = 5 + rnd.Next(80);
                fake.GpuLoad = 10 + rnd.Next(75);
                SyncPreviewFps();
                // вне игры слегка «оживляем» демо-FPS, чтобы было видно формат строк
                if (!owner.FpsLive) fake.Fps = 120 + rnd.Next(60);
                fake.CpuFreq = 2600 + rnd.Next(700);
                fake.CpuEffFreq = fake.CpuFreq * fake.CpuLoad.Value / 100.0;
                fake.CpuPower = 15 + rnd.Next(300) / 10.0;
                for (int i = 0; i < fake.ThreadLoads.Count; i++)
                    fake.ThreadLoads[i] = rnd.Next(100) < 35 ? rnd.Next(100) : rnd.Next(12);
                for (int i = 0; i < fake.CoreFreqs.Count; i++)
                    fake.CoreFreqs[i] = 2600 + rnd.Next(700);
                fake.RamPct = 40 + rnd.Next(12);
                fake.RamUsedGb = fake.RamTotalGb * fake.RamPct / 100.0;
                fakeStats.Update(fake);
                prev.Invalidate();
            };
            anim.Start();
        }

        // ---------- layout ----------
        int NavW() { return 190; }
        int TopH() { return 48; }
        int BottomH() { return 44; }
        int PreviewPanelW() { return PreviewOn() ? PreviewW + 40 : 0; }
        Rectangle ContentRect()
        {
            return new Rectangle(NavW() + 20, TopH() + 8, Width - NavW() - 20 - PreviewPanelW() - 30, Height - TopH() - BottomH() - 16);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(WBack);
            DrawTopBar(g);
            DrawNav(g);
            DrawRows(g);
            DrawPreview(g);
            DrawBottom(g);
            if (popupRow != null) DrawPopup(g);
        }

        static string VersionLabel()
        {
            try
            {
                Version v = typeof(SettingsForm).Assembly.GetName().Version;
                return "v " + v.Major + "." + v.Minor + "." + Math.Max(0, v.Build);
            }
            catch { return "v 1.1.0"; }
        }

        void DrawTopBar(Graphics g)
        {
            using (Brush b = new SolidBrush(NavBack)) g.FillRectangle(b, 0, 0, Width, TopH());
            using (Brush b = new SolidBrush(Acc)) g.DrawString("K2", fTitle, b, 16, 14);
            SizeF szK = g.MeasureString("K2", fTitle);
            Rectangle chip = new Rectangle((int)(16 + szK.Width + 12), 14, 52, 22);
            using (Pen p = new Pen(CtlBorder)) g.DrawRectangle(p, chip);
            using (Brush b = new SolidBrush(NavDim)) g.DrawString(VersionLabel(), fSmall, b, chip.Left + 7, chip.Top + 4);
            using (Brush b = new SolidBrush(NavDim))
                g.DrawString(Loc.T(" / Settings / ") + SectionTitle(section), fBread, b, chip.Right + 10, 16);
            RectangleF mrect = maxed ? new RectangleF(Width - 78, 12, 28, 24) : new RectangleF(Width - 78, 12, 28, 24);
            maxRect = mrect;
            using (Brush b = new SolidBrush(NavDim))
                g.DrawString(maxed ? "❐" : "▢", fClose, b, mrect.X - 3, mrect.Y);
            closeRect = new RectangleF(Width - 40, 12, 28, 24);
            using (Brush b = new SolidBrush(NavText)) g.DrawString("✕", fClose, b, closeRect.X - 4, closeRect.Y);
        }

        string SectionTitle(string sec)
        {
            for (int i = 0; i < SectionNames.Length; i++) if (SectionNames[i] == sec) return Loc.T(SectionTitles[i]);
            return sec;
        }

        void DrawNav(Graphics g)
        {
            using (Brush b = new SolidBrush(NavBack)) g.FillRectangle(b, 0, TopH(), NavW(), Height - TopH());
            int y = TopH() + 16;
            y = NavGroup(g, y, "OVERLAY", new string[] { "general", "pos", "look", "colors", "keys" });
            y = NavGroup(g, y, "METRICS", new string[] { "fps", "gpu", "cpu", "ram", "power", "stat", "ftg" });

            // Ссылка «Автору на кофе» — сразу под пунктом «График времени кадра».
            rectCoffee = new Rectangle(6, y + 4, NavW() - 12, 26);
            string coffee = Loc.T("☕ Support the author");
            using (Brush b = new SolidBrush(Acc))
                g.DrawString(coffee, fNav, b, rectCoffee.X + 12, rectCoffee.Y + 5);
            SizeF cs = g.MeasureString(coffee, fNav);
            using (Pen p = new Pen(Color.FromArgb(150, Acc)))
                g.DrawLine(p, rectCoffee.X + 12, rectCoffee.Y + 7 + cs.Height,
                    rectCoffee.X + 12 + cs.Width, rectCoffee.Y + 7 + cs.Height);

            using (Brush b = new SolidBrush(Acc))
                g.DrawString("by Jash/K2", fSmall, b, 16, Height - 106);

            rectExport = new Rectangle(4, Height - 88, 60, 30);
            rectImport = new Rectangle(66, Height - 88, 60, 30);
            rectReset = new Rectangle(128, Height - 88, 60, 30);
            FooterBtn(g, rectExport, "Export");
            FooterBtn(g, rectImport, "Import");
            FooterBtn(g, rectReset, "Reset");
        }

        int NavGroup(Graphics g, int y, string cat, string[] items)
        {
            bool compact = Height < 700;
            int step = compact ? 25 : 36;
            int itemH = compact ? 24 : 34;
            using (Brush b = new SolidBrush(NavDim)) g.DrawString(Loc.T(cat), fNavCat, b, 16, y);
            y += compact ? 18 : 22;
            for (int i = 0; i < items.Length; i++)
            {
                Rectangle r = new Rectangle(6, y, NavW() - 12, itemH);
                bool sel = section == items[i];
                if (sel) { using (Brush b = new SolidBrush(NavSel)) FillRound(g, r, 6, b); }
                using (Brush b = new SolidBrush(sel ? NavText : Color.FromArgb(190, 202, 214)))
                    g.DrawString(SectionTitle(items[i]), fNav, b, r.X + 12, r.Y + (compact ? 4 : 8));
                y += step;
            }
            y += compact ? 4 : 10;
            return y;
        }

        void FooterBtn(Graphics g, Rectangle r, string text)
        {
            using (Brush b = new SolidBrush(CtlBack)) FillRound(g, r, 6, b);
            using (Pen p = new Pen(CtlBorder)) DrawRound(g, p, r, 6);
            TextRenderer.DrawText(g, Loc.T(text), fValue, r, NavText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        void DrawRows(Graphics g)
        {
            Rectangle cr = ContentRect();
            g.SetClip(cr);
            int y = cr.Top - scroll;
            foreach (SRow r in rows)
            {
                if (r.Kind == "section")
                {
                    y += 14;
                    r.Rect = new Rectangle(cr.X, y, cr.Width, 24);
                    if (y + 24 > cr.Top && y < cr.Bottom)
                        using (Brush b = new SolidBrush(NavDim))
                            g.DrawString(r.Label.ToUpper(Cul), fNavCat, b, cr.X + 4, y);
                    y += 28;
                    continue;
                }
                int rh = 48;
                if (r.Kind == "info")
                {
                    SizeF ts = g.MeasureString(r.Label, fSmall, new SizeF(cr.Width - 8, 400));
                    rh = (int)ts.Height + 8;
                    if (rh < 24) rh = 24;
                }
                r.Rect = new Rectangle(cr.X, y, cr.Width, rh);
                if (y + rh > cr.Top && y < cr.Bottom)
                {
                    if (r.Kind == "info")
                    {
                        using (Brush b = new SolidBrush(NavDim))
                        {
                            StringFormat sf = new StringFormat();
                            sf.LineAlignment = StringAlignment.Near;
                            g.DrawString(r.Label, fSmall, b, new RectangleF(cr.X + 4, y + 6, cr.Width - 8, rh), sf);
                        }
                    }
                    else if (r.Kind == "buttons")
                    {
                        int bx = cr.X + 4;
                        List<Rectangle> bs = new List<Rectangle>();
                        foreach (string bt in r.Buttons)
                        {
                            Size s2 = TextRenderer.MeasureText(Loc.T(bt), fValue);
                            Rectangle b2 = new Rectangle(bx, y + 8, s2.Width + 28, 32);
                            bs.Add(b2);
                            FooterBtn(g, b2, bt);
                            bx += b2.Width + 12;
                        }
                        r.Tag = bs;
                    }
                    else if (r.Kind == "order")
                    {
                        OrderRowPaint(g, r, cr, y);
                    }
                    else if (r.Kind == "color")
                    {
                        ColorRowPaint(g, r, cr, y);
                    }
                    else
                    {
                        using (Brush b = new SolidBrush(NavText))
                            g.DrawString(r.Label, fLabel, b, cr.X + 4, y + 14);
                        PaintCtl(g, r, new Rectangle(cr.Right - 250, y + 8, 246, 32));
                    }
                }
                y += rh + 6;
                using (Pen p = new Pen(LineC)) g.DrawLine(p, cr.X + 4, y, cr.Right - 4, y);
            }
            y += 10;
            scrollMax = Math.Max(0, (y + scroll) - cr.Height);
            g.ResetClip();
        }

        void OrderRowPaint(Graphics g, SRow r, Rectangle cr, int y)
        {
            bool shown = BlockShown(r.Key);
            Color tc = shown ? NavText : Color.FromArgb(110, 124, 140);
            using (Brush b = new SolidBrush(NavDim))
                g.DrawString((r.OrderIndex + 1).ToString("00"), fValue, b, cr.X + 10, y + 12);
            using (Brush b = new SolidBrush(tc))
                g.DrawString(Loc.T(OrderName(r.Key)), fLabel, b, cr.X + 44, y + 12);
            Rectangle up = new Rectangle(cr.Right - 88, y + 6, 36, 36);
            Rectangle dn = new Rectangle(cr.Right - 46, y + 6, 36, 36);
            r.Ctl = up;
            r.Tag = dn;
            ArrowBtn(g, up, true);
            ArrowBtn(g, dn, false);
        }

        Color RowColor(SRow r)
        {
            Color fallback = r.FallbackKey != null
                ? draft.GetColor(r.FallbackKey, r.DefaultColor)
                : r.DefaultColor;
            return draft.GetColor(r.Key, fallback);
        }

        void ColorRowPaint(Graphics g, SRow r, Rectangle cr, int y)
        {
            using (Brush b = new SolidBrush(NavText))
                g.DrawString(r.Label, fLabel, b, cr.X + 4, y + 14);
            Rectangle sw = new Rectangle(cr.Right - 250, y + 8, 246, 32);
            r.Ctl = sw;
            Color c = RowColor(r);
            using (Brush b = new SolidBrush(c)) FillRound(g, sw, 6, b);
            using (Pen p = new Pen(CtlBorder)) DrawRound(g, p, sw, 6);
            int lum = c.R * 299 + c.G * 587 + c.B * 114;
            Color txt = lum > 140000 ? Color.FromArgb(20, 26, 34) : Color.White;
            string hex = "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
            TextRenderer.DrawText(g, hex, fValue, sw, txt,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        void PickColor(SRow r)
        {
            using (ColorDialog d = new ColorDialog())
            {
                d.FullOpen = true;
                d.AnyColor = true;
                d.Color = RowColor(r);
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    draft.SetColor(r.Key, d.Color);
                    AfterChange();
                }
            }
        }

        void ArrowBtn(Graphics g, Rectangle r, bool up)
        {
            using (Brush b = new SolidBrush(CtlBack)) FillRound(g, r, 6, b);
            using (Pen p = new Pen(CtlBorder)) DrawRound(g, p, r, 6);
            using (Brush br = new SolidBrush(NavText))
            {
                PointF tip = new PointF(r.X + r.Width / 2f, up ? r.Y + 10 : r.Y + r.Height - 10);
                PointF a = new PointF(tip.X - 7, up ? tip.Y + 10 : tip.Y - 10);
                PointF b2 = new PointF(tip.X + 7, a.Y);
                g.FillPolygon(br, new PointF[] { tip, a, b2 });
            }
        }

        void PaintCtl(Graphics g, SRow r, Rectangle area)
        {
            r.Ctl = area;
            switch (r.Kind)
            {
                case "toggle":
                    DrawToggle(g, area, draft.GetB(r.Key));
                    break;
                case "slider":
                {
                    Rectangle track = new Rectangle(area.X, area.Y + 10, 160, 12);
                    Rectangle valBox = new Rectangle(area.Right - 76, area.Y, 76, 32);
                    int v = draft.GetI(r.Key);
                    using (Brush b = new SolidBrush(CtlBack)) FillRound(g, track, 6, b);
                    float f = (v - r.Min) / (float)(r.Max - r.Min);
                    Rectangle fill = new Rectangle(track.X, track.Y, Math.Max(6, (int)(track.Width * f)), track.Height);
                    using (Brush b = new SolidBrush(Acc)) FillRound(g, fill, 6, b);
                    using (Brush b = new SolidBrush(Color.White))
                        g.FillEllipse(b, track.X + fill.Width - 8, track.Y - 2, 16, 16);
                    using (Brush b = new SolidBrush(CtlBack)) FillRound(g, valBox, 6, b);
                    using (Pen p = new Pen(CtlBorder)) DrawRound(g, p, valBox, 6);
                    TextRenderer.DrawText(g, SliderText(r, v), fValue, valBox, NavText,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    r.Tag = track;
                    break;
                }
                case "combo":
                {
                    using (Brush b = new SolidBrush(CtlBack)) FillRound(g, area, 6, b);
                    using (Pen p = new Pen(CtlBorder)) DrawRound(g, p, area, 6);
                    string txt = Val(r);
                    TextRenderer.DrawText(g, txt, fValue, new Rectangle(area.X + 8, area.Y, area.Width - 30, area.Height),
                        NavText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                    using (Brush br = new SolidBrush(NavDim))
                    {
                        PointF a = new PointF(area.Right - 18, area.Y + 13);
                        g.FillPolygon(br, new PointF[] { new PointF(a.X - 6, a.Y), new PointF(a.X + 6, a.Y), new PointF(a.X, a.Y + 7) });
                    }
                    break;
                }
                case "hotkey":
                {
                    string spec = draft.GetS(r.Key);
                    bool en = spec.Length > 0 && !spec.StartsWith("-");
                    Rectangle tog = new Rectangle(area.X, area.Y + 4, 44, 24);
                    DrawToggleSmall(g, tog, en);
                    string txt = captureRow == r ? Loc.T("press keys…") : HotkeyDisplay(spec);
                    Rectangle kb = new Rectangle(area.X + 54, area.Y, area.Width - 54, 32);
                    using (Brush b = new SolidBrush(captureRow == r ? AccDim : CtlBack)) FillRound(g, kb, 6, b);
                    using (Pen p = new Pen(captureRow == r ? Acc : CtlBorder)) DrawRound(g, p, kb, 6);
                    TextRenderer.DrawText(g, txt, fValue, new Rectangle(kb.X + 10, kb.Y, kb.Width - 20, kb.Height),
                        NavText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                    r.Ctl = kb;
                    break;
                }
            }
        }

        string SliderText(SRow r, int v)
        {
            switch (r.Key)
            {
                case "look.width":
                case "look.scale":
                case "look.opacity":
                case "look.fontSize":
                case "ft.fill": return v + "%";
                default: return v.ToString(Cul);
            }
        }

        string HotkeyDisplay(string spec)
        {
            if (spec.Length == 0 || spec.StartsWith("-")) return "—";
            string[] p = spec.TrimStart('+').Split('+');
            List<string> mods = new List<string>();
            string key = "";
            foreach (string x0 in p)
            {
                string x = x0.Trim();
                if (x == "Ctrl") mods.Add("Ctrl");
                else if (x == "Alt") mods.Add("Alt");
                else if (x == "Shift") mods.Add("Shift");
                else if (x == "Win") mods.Add("Win");
                else if (x.Length > 0) key = x;
            }
            string m = string.Join(" + ", mods.ToArray());
            return (m.Length > 0 ? m + " + " : "") + key;
        }

        void DrawToggle(Graphics g, Rectangle area, bool on)
        {
            Rectangle pill = new Rectangle(area.Right - 60, area.Y + 4, 56, 24);
            DrawToggleSmall(g, pill, on);
        }

        void DrawToggleSmall(Graphics g, Rectangle pill, bool on)
        {
            using (Brush b = new SolidBrush(on ? Acc : OffC)) FillRound(g, pill, pill.Height / 2, b);
            int d = pill.Height - 6;
            int cx2 = on ? pill.Right - d - 3 : pill.X + 3;
            using (Brush b = new SolidBrush(Color.White))
                g.FillEllipse(b, cx2, pill.Y + 3, d, d);
        }

        void DrawPreview(Graphics g)
        {
            if (!PreviewOn()) return;
            int px = Width - PreviewPanelW() + 10;
            using (Brush b = new SolidBrush(NavDim))
                g.DrawString(Loc.T(owner.FpsLive ? "PREVIEW" : "PREVIEW · DEMO"), fNavCat, b, px, TopH() + 16);
            Rectangle box = new Rectangle(px, TopH() + 44, PreviewW, Height - TopH() - BottomH() - 64);
            using (Pen p = new Pen(LineC)) DrawRound(g, p, box, 10);
        }

        void DrawBottom(Graphics g)
        {
            Rectangle bar = new Rectangle(0, Height - BottomH(), Width, BottomH());
            using (Brush b = new SolidBrush(NavBack)) g.FillRectangle(b, bar);
            int diff = owner.St.DiffCount(draft);
            using (Brush br = new SolidBrush(diff > 0 ? WarnText : NavDim))
                g.DrawString(Loc.T("Changed ") + diff, fValue, br, 16, bar.Y + 13);
            using (Brush br = new SolidBrush(NavDim))
            {
                g.DrawString(Loc.T("Esc — cancel    Ctrl+S — apply"), fSmall, br, 160, bar.Y + 15);
                // при узком окне правая подпись наезжает на кнопки — не рисуем её
                if (Width >= 1180) g.DrawString(Loc.T("K2 — overlay settings"), fSmall, br, 440, bar.Y + 15);
            }
            rectCancel = new Rectangle(Width - 470, bar.Y + 6, 110, 32);
            rectApply = new Rectangle(Width - 350, bar.Y + 6, 140, 32);
            rectApplyClose = new Rectangle(Width - 200, bar.Y + 6, 184, 32);
            FooterBtn(g, rectCancel, "Cancel");
            // «Применить» — применяет, оставляя окно открытым
            using (Brush b = new SolidBrush(diff > 0 ? AccDim : CtlBack)) FillRound(g, rectApply, 6, b);
            using (Pen p = new Pen(diff > 0 ? Acc : CtlBorder)) DrawRound(g, p, rectApply, 6);
            TextRenderer.DrawText(g, Loc.T("Apply"), fValue, rectApply,
                diff > 0 ? Color.White : Color.FromArgb(110, 130, 128),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            // «Применить и закрыть» — основная кнопка
            using (Brush b = new SolidBrush(diff > 0 ? Acc : AccDim)) FillRound(g, rectApplyClose, 6, b);
            TextRenderer.DrawText(g, Loc.T("Apply and close"), fValue, rectApplyClose,
                diff > 0 ? Color.FromArgb(4, 22, 20) : Color.FromArgb(110, 130, 128),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        void DrawPopup(Graphics g)
        {
            popupHits.Clear();
            popupHitIdx.Clear();
            int itemH = 28;
            int total = popupItems.Length;
            if (total == 0) return;

            int belowTop = popupRow.Ctl.Bottom;
            int spaceBelow = Height - BottomH() - 6 - belowTop;
            int spaceAbove = popupRow.Ctl.Top - TopH() - 6;
            // у нижнего края список раскрывается вверх, чтобы все пункты были доступны
            bool up = spaceBelow < Math.Min(total, 6) * itemH && spaceAbove > spaceBelow;
            int space = Math.Max(0, (up ? spaceAbove : spaceBelow) - 8);
            int visible = Math.Min(total, Math.Max(1, space / itemH));
            int maxScroll = Math.Max(0, total - visible);
            if (popupScroll < 0) popupScroll = 0;
            if (popupScroll > maxScroll) popupScroll = maxScroll;

            int h = itemH * visible + 8;
            int top = up ? popupRow.Ctl.Top - h : belowTop;
            if (top < TopH() + 4) top = TopH() + 4;
            Rectangle pr = new Rectangle(popupRow.Ctl.X, top, popupRow.Ctl.Width, h);
            using (Brush b = new SolidBrush(NavSel)) FillRound(g, pr, 6, b);
            using (Pen p = new Pen(CtlBorder)) DrawRound(g, p, pr, 6);

            string[] vals = popupRow.Tag as string[];
            string sel = draft.GetS(popupRow.Key);
            for (int i = 0; i < visible; i++)
            {
                int idx = i + popupScroll;
                Rectangle ir = new Rectangle(pr.X + 4, pr.Y + 4 + i * itemH, pr.Width - 14, itemH);
                bool cur2 = vals != null && idx < vals.Length && vals[idx] == sel;
                if (cur2) { using (Brush b = new SolidBrush(AccDim)) FillRound(g, ir, 4, b); }
                TextRenderer.DrawText(g, popupItems[idx], fValue, ir, cur2 ? Color.White : NavText,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis);
                popupHits.Add(ir);
                popupHitIdx.Add(idx);
            }

            // индикатор прокрутки
            if (total > visible)
            {
                int trackX = pr.Right - 7;
                int trackH = pr.Height - 10;
                int thumbH = Math.Max(18, trackH * visible / total);
                int thumbY = pr.Y + 5 + (trackH - thumbH) * popupScroll / Math.Max(1, maxScroll);
                using (Brush b = new SolidBrush(CtlBorder))
                {
                    g.FillRectangle(b, trackX, pr.Y + 5, 2, trackH);
                    g.FillRectangle(b, trackX - 1, thumbY, 4, thumbH);
                }
            }
            popupRect = pr;
        }

        static GraphicsPath Round(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            int d = rad * 2;
            if (d <= 1) { p.AddRectangle(r); p.CloseFigure(); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        static void FillRound(Graphics g, Rectangle r, int rad, Brush b)
        {
            using (GraphicsPath p = Round(r, rad)) g.FillPath(b, p);
        }

        static void DrawRound(Graphics g, Pen p, Rectangle r, int rad)
        {
            using (GraphicsPath pp = Round(r, rad)) g.DrawPath(p, pp);
        }

        // ---------- interaction ----------
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            if (closeRect.Contains(e.Location) || rectCancel.Contains(e.Location)) { Close(); return; }
            if (maxRect.Contains(e.Location)) { ToggleMax(); return; }
            if (rectApply.Contains(e.Location)) { ApplyNow(); return; }
            if (rectApplyClose.Contains(e.Location)) { ApplyNow(); Close(); return; }
            if (rectExport.Contains(e.Location)) { ExportNow(); return; }
            if (rectImport.Contains(e.Location)) { ImportNow(); return; }
            if (rectReset.Contains(e.Location)) { draft = new Settings(); BuildRows(); AfterChange(); return; }

            if (e.Y < TopH()) { topDrag = true; topDragOff = e.Location; return; }

            if (popupRow != null)
            {
                for (int i = 0; i < popupHits.Count; i++)
                {
                    if (popupHits[i].Contains(e.Location))
                    {
                        string[] vals = popupRow.Tag as string[];
                        int idx = i < popupHitIdx.Count ? popupHitIdx[i] : i;
                        if (vals != null && idx < vals.Length)
                        {
                            draft.Set(popupRow.Key, vals[idx]);
                            if (popupRow.Key == "look.font" && draft.GetB("look.fontAll"))
                                Font = new Font(draft.GetS("look.font"), 9.75f);
                        }
                        popupRow = null;
                        popupHits.Clear();
                        AfterChange();
                        return;
                    }
                }
                if (!popupRect.Contains(e.Location)) { popupRow = null; popupHits.Clear(); Invalidate(); }
                return;
            }

            if (e.X < NavW())
            {
                if (rectCoffee.Contains(e.Location)) { OpenUrl(CoffeeUrl); return; }
                string sec = NavAt(e.Y);
                if (sec != null && sec != section) { section = sec; BuildRows(); AfterChange(); }
                return;
            }

            foreach (SRow r in rows)
            {
                if (!r.Rect.Contains(e.Location)) continue;
                switch (r.Kind)
                {
                    case "toggle":
                        if (r.Ctl.Contains(e.Location)) { draft.Set(r.Key, !draft.GetB(r.Key)); AfterChange(); }
                        break;
                    case "slider":
                        dragRow = r;
                        SetSlider(r, e.X);
                        break;
                    case "combo":
                        OpenCombo(r);
                        Invalidate();
                        break;
                    case "hotkey":
                        if (r.Ctl.Contains(e.Location)) { captureRow = r; Invalidate(); }
                        else
                        {
                            Rectangle tog = new Rectangle(r.Ctl.X - 54, r.Ctl.Y + 4, 44, 24);
                            if (tog.Contains(e.Location))
                            {
                                string sp = draft.GetS(r.Key);
                                if (sp.Length == 0) sp = "+Alt+F12";
                                draft.Set(r.Key, sp.StartsWith("-") ? sp.Substring(1) : "-" + sp);
                                AfterChange();
                            }
                        }
                        break;
                    case "buttons":
                    {
                        List<Rectangle> bs = r.Tag as List<Rectangle>;
                        if (bs != null)
                            for (int i = 0; i < bs.Count; i++)
                                if (bs[i].Contains(e.Location)) { DoButton(r, i); return; }
                        break;
                    }
                    case "order":
                    {
                        object t = r.Tag;
                        Rectangle dn = (t is Rectangle) ? (Rectangle)t : Rectangle.Empty;
                        if (r.Ctl.Contains(e.Location)) MoveOrder(r.OrderIndex, -1);
                        else if (dn.Width > 0 && dn.Contains(e.Location)) MoveOrder(r.OrderIndex, 1);
                        break;
                    }
                    case "color":
                        if (r.Ctl.Contains(e.Location)) PickColor(r);
                        break;
                }
                return;
            }
        }

        void SetSlider(SRow r, int mouseX)
        {
            object t = r.Tag;
            if (!(t is Rectangle)) return;
            Rectangle track = (Rectangle)t;
            if (track.Width <= 0) return;
            float f = (mouseX - track.X) / (float)track.Width;
            f = Math.Max(0, Math.Min(1, f));
            int v = r.Min + (int)Math.Round(f * (r.Max - r.Min));
            draft.Set(r.Key, v);
            AfterChange();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (topDrag)
            {
                Point gp = Cursor.Position;
                Location = new Point(gp.X - topDragOff.X, gp.Y - topDragOff.Y);
                return;
            }
            if (dragRow != null) SetSlider(dragRow, e.X);

            Cursor = rectCoffee.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragRow = null;
            topDrag = false;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (popupRow != null)
            {
                popupScroll -= Math.Sign(e.Delta) * 3;
                Invalidate();
                return;
            }
            scroll = Math.Max(0, Math.Min(scrollMax, scroll - e.Delta / 2));
            Invalidate();
        }

        string NavAt(int y)
        {
            bool compact = Height < 700;
            int step = compact ? 25 : 36;
            int itemH = compact ? 24 : 34;
            int yy = TopH() + 16 + (compact ? 18 : 22);
            string[] grp1 = new string[] { "general", "pos", "look", "colors", "keys" };
            foreach (string s in grp1) { if (y >= yy && y < yy + itemH) return s; yy += step; }
            yy += (compact ? 4 : 10) + (compact ? 18 : 22);
            string[] grp2 = new string[] { "fps", "gpu", "cpu", "ram", "power", "stat", "ftg" };
            foreach (string s in grp2) { if (y >= yy && y < yy + itemH) return s; yy += step; }
            return null;
        }

        void MoveOrder(int idx, int dir)
        {
            List<string> list = new List<string>(OrderList());
            int to = idx + dir;
            if (to < 0 || to >= list.Count) return;
            string tmp = list[idx]; list[idx] = list[to]; list[to] = tmp;
            draft.Set("pos.order", string.Join(",", list.ToArray()));
            BuildRows();
            AfterChange();
        }

        void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.T("Could not open link: ") + ex.Message, "K2");
            }
        }

        void DoButton(SRow r, int which)
        {
            if (r.Key == "pos.movebtn")
            {
                if (which == 0)
                {
                    owner.Visible = true;
                    owner.TopMost = false; owner.TopMost = true;
                    owner.Invalidate();
                }
                else
                {
                    try { File.Delete(Monitor.LogPath("pos.txt")); } catch { }
                    draft.Set("pos.pin", false);
                    draft.Set("pos.mode", "corner");
                    owner.ApplySettings(draft.Clone());
                    BuildRows();
                }
            }
            else if (r.Key == "stat.resetbtn")
            {
                owner.ResetSession();
            }
            else if (r.Key == "color.resetbtn")
            {
                draft.ResetColors();
                BuildRows();
                AfterChange();
            }
        }

        void ApplyNow()
        {
            bool langChanged = draft.GetS("ui.lang") != Loc.Lang;
            owner.ApplySettings(draft.Clone());
            Invalidate();
            if (langChanged)
            {
                if (RestartPrompt.Ask(this) && !Program.RestartApp())
                    MessageBox.Show(this, Loc.T("Could not restart K2. Start it manually."), "K2");
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                if (captureRow != null) { captureRow = null; Invalidate(); return; }
                if (popupRow != null) { popupRow = null; Invalidate(); return; }
                Close();
                return;
            }
            if (e.Control && e.KeyCode == Keys.S) { ApplyNow(); return; }
            if (captureRow != null)
            {
                bool ctrl = (e.Modifiers & Keys.Control) != 0;
                bool alt = (e.Modifiers & Keys.Alt) != 0;
                bool shift = (e.Modifiers & Keys.Shift) != 0;
                Keys key = e.KeyCode;
                if (key == Keys.None) return;
                e.SuppressKeyPress = true;
                string spec = "+";
                if (ctrl) spec += "Ctrl+";
                if (alt) spec += "Alt+";
                if (shift) spec += "Shift+";
                spec += new KeysConverter().ConvertToString(key);
                draft.Set(captureRow.Key, spec);
                captureRow = null;
                AfterChange();
            }
        }

        void ExportNow()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = Loc.T("K2 settings (*.ini)|*.ini");
                d.FileName = "k2-settings.ini";
                if (d.ShowDialog(this) == DialogResult.OK)
                    try { draft.Save2(d.FileName); } catch { }
            }
        }

        void ImportNow()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = Loc.T("K2 settings (*.ini)|*.ini");
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    try { draft = Settings.LoadFile(d.FileName); }
                    catch (Exception ex) { MessageBox.Show(this, Loc.T("Could not load file: ") + ex.Message, "K2"); return; }
                    BuildRows();
                    AfterChange();
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (anim != null) { anim.Stop(); anim.Dispose(); anim = null; }
            base.OnFormClosing(e);
        }
    }
}
