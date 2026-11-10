using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace K2
{
    // Параметризованный рендер оверлея: используется и главным окном, и превью настроек
    public class OverlayDraw
    {
        public static CultureInfo Cul { get { return Loc.Culture; } }

        // палитра оверлея (цвета задаются в настройках)
        Color HeaderC { get { return st.GetColor("color.header", Color.FromArgb(240, 240, 240)); } }
        Color LabelC { get { return st.GetColor("color.label", Color.FromArgb(166, 166, 166)); } }
        Color ValueC { get { return st.GetColor("color.value", Color.FromArgb(240, 240, 240)); } }
        Color LineC { get { return st.GetColor("color.line", Color.FromArgb(82, 82, 88)); } }
        Color GreenC { get { return st.GetColor("color.green", Color.FromArgb(46, 204, 64)); } }
        Color YellowC { get { return st.GetColor("color.yellow", Color.FromArgb(229, 198, 75)); } }
        Color OrangeC { get { return st.GetColor("color.orange", Color.FromArgb(240, 140, 60)); } }
        Color RedC { get { return st.GetColor("color.red", Color.FromArgb(230, 80, 60)); } }
        Color MarkC { get { return st.GetColor("color.mark", Color.FromArgb(120, 120, 126)); } }
        Color FtgRed { get { return st.GetColor("color.ftg", Color.FromArgb(245, 45, 38)); } }
        Color AccentC { get { return st.GetColor("color.accent", Color.FromArgb(0, 191, 166)); } }

        // цвета текущего блока (переопределяются в «Настройках цвета»)
        Color curHeader, curLabel, curValue, curBig;

        // измерение минимальной ширины по содержимому (g == null)
        public int MinWidth;
        Bitmap measureBmp;
        Graphics measureG;

        Graphics g;               // null = только измерение
        Settings st;
        Snapshot s;
        Stats stats;
        float S;                  // dpi-масштаб
        float US;                 // масштаб из настроек
        float y;

        Font fH, fL, fV, fC, fM, fBig;

        // координаты графика по ядрам (для клика «история»)
        public RectangleF LastGraphRect = RectangleF.Empty;

        public OverlayDraw(Graphics g, Settings st, Snapshot s, Stats stats, float dpiScale)
        {
            this.g = g; this.st = st; this.s = s; this.stats = stats;
            this.S = dpiScale > 0 ? dpiScale : 1f;
            curHeader = HeaderC; curLabel = LabelC; curValue = ValueC; curBig = OrangeAccent();
            if (g == null)
            {
                measureBmp = new Bitmap(1, 1);
                measureG = Graphics.FromImage(measureBmp);
            }
            this.US = Math.Max(50, Math.Min(200, st.GetI("look.scale"))) / 100f;
            int fontPct = Math.Max(50, Math.Min(200, st.GetI("look.fontSize")));
            float fs = S * US * fontPct / 100f;
            string fam = st.GetS("look.font", "Segoe UI");
            if (fam.Length == 0) fam = "Segoe UI";
            try
            {
                fH = new Font(fam, 11.5f * fs, FontStyle.Bold);
                fL = new Font(fam, 9.75f * fs, FontStyle.Regular);
                fV = new Font(fam, 9.75f * fs, FontStyle.Bold);
                fC = new Font(fam, 8.5f * fs, FontStyle.Regular);
                fM = new Font(fam, 8.5f * fs, FontStyle.Bold);
                fBig = new Font(fam, 16f * fs, FontStyle.Bold);
            }
            catch { fam = "Segoe UI";
                fH = new Font(fam, 11.5f * fs, FontStyle.Bold);
                fL = new Font(fam, 9.75f * fs, FontStyle.Regular);
                fV = new Font(fam, 9.75f * fs, FontStyle.Bold);
                fC = new Font(fam, 8.5f * fs, FontStyle.Regular);
                fM = new Font(fam, 8.5f * fs, FontStyle.Bold);
                fBig = new Font(fam, 16f * fs, FontStyle.Bold);
            }
        }

        public void DisposeFonts()
        {
            fH.Dispose(); fL.Dispose(); fV.Dispose(); fC.Dispose(); fM.Dispose(); fBig.Dispose();
            if (measureG != null) { measureG.Dispose(); measureG = null; }
            if (measureBmp != null) { measureBmp.Dispose(); measureBmp = null; }
        }

        float TW(string text, Font f)
        {
            if (measureG == null || text == null) return 0;
            return measureG.MeasureString(text, f).Width;
        }

        void Need(float w)
        {
            int v = (int)Math.Ceiling(w);
            if (v > MinWidth) MinWidth = v;
        }

        public const float BaseW = 420f;
        public static int WidthOf(Settings st, float dpiScale)
        {
            float us = Math.Max(50, Math.Min(200, st.GetI("look.scale"))) / 100f;
            return (int)(BaseW * Math.Max(50, Math.Min(200, st.GetI("look.width"))) / 100f * us * dpiScale);
        }

        float Pad() { return 16 * S * US; }
        float RowH()
        {
            int row = Math.Max(16, Math.Min(40, st.GetI("look.rowH")));
            return Math.Max(row * S * US, fL.Height + 2 * S);
        }
        float HdrH() { return Math.Max(27 * S * US, fH.Height + 6 * S); }
        float CoreH() { return Math.Max(18 * S * US, fC.Height + 2 * S); }
        float Gap() { return 13 * S * US; }
        float GraphH() { return 50 * S * US; }

        public float Draw(float w)
        {
            if (w < 1) w = WidthOf(st, S);
            y = Pad();
            float x = Pad();
            float cw = Math.Max(1f, w - 2 * x);

            Need(160 * S);
            if (measureG != null)
            {
                float mw = TW("K2", fM) + TW("00:00:00", fC) + Pad() * 2 + 30 * S;
                Need(mw);
            }

            // Шапка: K2 слева, время справа — отдельная строка, не пересекается с блоками.
            float lineH = Math.Max(fM.Height, fC.Height);
            float ty = y + Math.Max(0, (HdrH() - lineH) / 2f);
            Text(x, ty, cw * 0.5f, "K2", fM, AccentC, false);
            if (st.GetB("look.showTime"))
                Text(x + cw * 0.5f, ty, cw * 0.5f, DateTime.Now.ToString("HH:mm:ss"), fC, MarkC, true);
            y += HdrH();
            Draw(g2 =>
            {
                using (Pen p = new Pen(LineC, 1f))
                    g2.DrawLine(p, x, y - 4 * S, x + cw, y - 4 * S);
            });
            y += 6 * S * US;

            // порядок блоков
            string order = st.GetS("pos.order");
            if (order.Length == 0) order = "fps,gpu,cpu,ram,stat,ftg";
            string[] parts = order.Split(',');
            bool first = true;
            for (int i = 0; i < parts.Length; i++)
            {
                string b = parts[i].Trim();
                bool drew = false;
                if (b == "fps") drew = DrawFps(x, cw);
                else if (b == "gpu") drew = DrawGpu(x, cw);
                else if (b == "cpu") drew = DrawCpu(x, cw);
                else if (b == "ram") drew = DrawRam(x, cw);
                else if (b == "stat") drew = DrawStat(x, cw);
                else if (b == "ftg") drew = DrawFtg(x, cw);
                if (drew && !first) { /* разделитель уже добавлен внутри */ }
                if (drew) first = false;
            }
            y += 10 * S * US;
            return y;
        }

        // ---------- блоки ----------

        bool DrawFps(float x, float cw)
        {
            if (!st.GetB("fps.show")) return false;
            if (s.Fps == null && st.GetB("fps.hideNa")) return false;
            Color statC = OrangeAccent();
            UseBlock("fps", HeaderC, LabelC, statC, statC);
            Header(x, cw, "FPS", FpsBig());
            List<string> parts = new List<string>();
            if (st.GetB("fps.avg")) parts.Add("AVG: " + (s.FpsAvg != null ? ((int)s.FpsAvg.Value).ToString(Cul) : "—"));
            if (st.GetB("fps.low1") && s.FpsLow1 != null) parts.Add("1% Low: " + ((int)s.FpsLow1.Value).ToString(Cul));
            if (st.GetB("fps.low01") && s.FpsLow01 != null) parts.Add("0.1% Low: " + ((int)s.FpsLow01.Value).ToString(Cul));
            if (st.GetB("fps.lat") && s.FpsLatency != null) parts.Add(Loc.T("Frame: ") + s.FpsLatency.Value.ToString("0.0", Cul) + Loc.T(" ms"));
            if (parts.Count > 0) SubRow(string.Join("   ", parts.ToArray()));
            if (st.GetB("fps.proc") && s.FgProc.Length > 0) SubRow(Loc.T("Window: ") + s.FgProc);
            if (st.GetB("fps.api"))
            {
                string meta = s.Resolution;
                if (s.RenderApi.Length > 0) meta += (meta.Length > 0 ? " · " : "") + s.RenderApi;
                if (meta.Length > 0) SubRow(meta);
                if (s.PresentMode.Length > 0) SubRow(Loc.T("Mode: ") + ShortPresentMode(s.PresentMode));
            }
            Sep();
            return true;
        }

        string FpsBig()
        {
            return s.Fps != null ? ((int)s.Fps.Value).ToString(Cul) : "N/A";
        }

        bool DrawGpu(float x, float cw)
        {
            if (!st.GetB("gpu.show")) return false;
            UseBlock("gpu", HeaderC, LabelC, ValueC, ValueC);
            bool zones = st.GetB("color.gpu.zones");
            Header(x, cw, s.GpuName.Length == 0 ? "GPU" : s.GpuName, null);
            if (st.GetB("gpu.load")) Row("Load", Pct(s.GpuLoad), zones ? LoadColored(s.GpuLoad, st.GetB("gpu.loadcolor")) : curValue);
            if (st.GetB("gpu.temp")) Row("Temperature", Temp(s.GpuTemp), zones ? TempColored(s.GpuTemp, st.GetI("gpu.warn"), st.GetI("gpu.crit")) : curValue);
            if (st.GetB("gpu.hotspot")) Row("Hotspot", Temp(s.GpuHotspot), zones ? TempColored(s.GpuHotspot, st.GetI("gpu.warn"), st.GetI("gpu.crit")) : curValue);
            if (st.GetB("gpu.memtemp")) Row("Memory temperature", Temp(s.GpuMemTemp), zones ? TempColored(s.GpuMemTemp, st.GetI("gpu.warn"), st.GetI("gpu.crit")) : curValue);
            if (st.GetB("gpu.clock")) Row("Clock", Freq(s.GpuClock), curValue);
            if (st.GetB("gpu.memclock")) Row("Memory clock", Freq(s.GpuMemClock), curValue);
            if (st.GetB("gpu.volt")) Row("Voltage", s.GpuVolt != null ? s.GpuVolt.Value.ToString("0.000", Cul) + Loc.T(" V") : "—", curValue);
            if (st.GetB("gpu.power")) Row("Power", W(s.GpuPower), curValue);
            if (st.GetB("gpu.fanrpm")) Row("Fan speed", s.GpuFanRpm != null ? ((int)s.GpuFanRpm.Value).ToString(Cul) + Loc.T(" RPM") : "—", curValue);
            if (st.GetB("gpu.fanpct")) Row("Fan speed %", Pct(s.GpuFanPct), curValue);
            if (st.GetB("gpu.vram")) Row("VRAM", Mem(s.GpuMemUsedGb, s.GpuMemTotalGb), curValue);
            Sep();
            return true;
        }

        bool DrawCpu(float x, float cw)
        {
            if (!st.GetB("cpu.show")) return false;
            UseBlock("cpu", HeaderC, LabelC, ValueC, ValueC);
            bool zones = st.GetB("color.cpu.zones");
            Header(x, cw, s.CpuName.Length == 0 ? "CPU" : s.CpuName, null);
            if (st.GetB("cpu.load")) Row("Load", Pct(s.CpuLoad), zones ? LoadColored(s.CpuLoad, st.GetB("cpu.loadcolor")) : curValue);
            if (st.GetB("cpu.temp")) Row("Temperature", Temp(s.CpuTemp), zones ? TempColored(s.CpuTemp, st.GetI("cpu.warn"), st.GetI("cpu.crit")) : curValue);
            if (st.GetB("cpu.ccd")) Row("Core temperatures", s.CpuCoreTempMin != null
                ? Temp(s.CpuCoreTempMin) + "–" + Temp(s.CpuCoreTempMax) : "—", curValue);
            if (st.GetB("cpu.clock")) Row("Clock (max.)", Freq(s.CpuFreq), curValue);
            if (st.GetB("cpu.eff")) Row("Clock (eff.)", Freq(s.CpuEffFreq), curValue);
            if (st.GetB("cpu.power")) Row("Power", W(s.CpuPower), curValue);
            if (st.GetB("cpu.fanrpm")) Row("Fan speed", s.CpuFanRpm != null ? ((int)s.CpuFanRpm.Value).ToString(Cul) + Loc.T(" RPM") : "—", curValue);
            if (st.GetB("cpu.fanpct")) Row("Fan speed %", Pct(s.CpuFanPct), curValue);

            // таблица частот по ядрам: левая половина / правая половина
            int n = s.CoreFreqs.Count;
            if (st.GetB("cpu.corefreqs") && n > 0)
            {
                int rows = (n + 1) / 2;
                float half = cw / 2f;
                for (int r = 0; r < rows; r++)
                {
                    CoreCell(x, half - 14 * S, r);
                    CoreCell(x + half + 4 * S, half - 4 * S, r + rows);
                    y += CoreH();
                }
            }
            // график загрузки по ядрам
            if (st.GetB("cpu.coregraph"))
            {
                y += Gap();
                DrawCoreGraph(x, cw, GraphH());
                y += 6 * S;
            }
            Sep();
            return true;
        }

        bool DrawRam(float x, float cw)
        {
            if (!st.GetB("ram.show")) return false;
            UseBlock("ram", HeaderC, LabelC, ValueC, ValueC);
            Header(x, cw, "RAM", null);
            string fmt = st.GetS("ram.format");
            if (st.GetB("ram.used"))
            {
                string v;
                if (fmt == "used") v = Mem(s.RamUsedGb, null);
                else if (fmt == "pct") v = ((int)s.RamPct).ToString(Cul) + " %";
                else v = Mem(s.RamUsedGb, s.RamTotalGb);
                Row("Used", v, curValue);
            }
            if (st.GetB("ram.load")) Row("Load", ((int)s.RamPct).ToString(Cul) + " %", curValue);
            double? sp = s.RamSpeedMHz;
            string mode = st.GetS("ram.speedMode");
            if (mode == "manual") sp = st.GetI("ram.speedManual");
            else if (mode == "hw")
            {
                double? cap = Monitor.CpuMemCapMhz(s.CpuName);
                if (cap != null && sp != null && sp.Value > cap.Value) sp = cap;
            }
            if (st.GetB("ram.speed") && sp != null)
                Row("Speed", ((int)sp.Value).ToString(Cul) + Loc.T(" MHz"), curValue);
            if (st.GetB("ram.dimmt") && s.RamDimmTemp != null) Row("DIMM temperature", Temp(s.RamDimmTemp), TempColored(s.RamDimmTemp, 75, 85));
            Sep();
            return true;
        }

        bool DrawStat(float x, float cw)
        {
            if (!st.GetB("stat.show") || stats == null) return false;
            Color statC = OrangeAccent();
            UseBlock("stat", HeaderC, LabelC, statC, statC);
            Header(x, cw, "Statistics", stats.Paused ? "paused" : null);
            StatRow("stat.gput", "Min/Max GPU", "gput", true);
            StatRow("stat.cput", "Min/Max CPU", "cput", true);
            StatRow("stat.vramt", "Min/Max VRAM", "vramt", true);
            StatRow("stat.hotspot", "Min/Max Hotspot", "hotspot", true);
            StatRow("stat.gpuv", "Min/Max GPU voltage", "gpuv", false, Loc.T(" V"));
            StatRow("stat.gpup", "Min/Max GPU power", "gpup", false, Loc.T(" W"));
            StatRow("stat.cpup", "Min/Max CPU power", "cpup", false, Loc.T(" W"));
            Sep();
            return true;
        }

        void StatRow(string key, string label, string statKey, bool temp, string suffix = "")
        {
            if (!st.GetB(key)) return;
            int mode = Mode(st.GetS(key + ".mode"));
            string v = stats.Get(statKey, mode);
            if (v == null) return;
            if (temp) v = TempPair(statKey, mode);
            else if (suffix.Length > 0) v += suffix;
            Row(label, v, curValue);
        }

        static int Mode(string m)
        {
            if (m == "avgmax") return 1;
            if (m == "minavgmax") return 2;
            return 0;
        }

        // статистика температур с учётом единиц
        string TempPair(string statKey, int mode)
        {
            string v = stats.Get(statKey, mode);
            if (v == null) return "—";
            if (st.GetS("ov.tempUnit") != "F") return v + " °C";
            string[] seg = v.Split('/');
            for (int i = 0; i < seg.Length; i++)
            {
                double d;
                if (double.TryParse(seg[i].Trim(), NumberStyles.Float, Cul, out d))
                    seg[i] = " " + ((d * 9.0 / 5.0 + 32.0)).ToString("0.0", Cul) + " ";
            }
            return string.Join("/", seg) + " °F";
        }

        Color OrangeAccent() { return st.GetColor("color.stat", Color.FromArgb(255, 170, 80)); }

        void UseBlock(string block, Color header, Color label, Color value, Color big)
        {
            curHeader = st.GetColor("color." + block + ".header", header);
            curLabel = st.GetColor("color." + block + ".label", label);
            curValue = st.GetColor("color." + block + ".value", value);
            curBig = st.GetColor("color." + block + ".value", big);
        }

        bool DrawFtg(float x, float cw)
        {
            if (!st.GetB("ft.show")) return false;
            if (measureG != null) Need(220 * S);
            if (measureG != null) Need(TW("00.0" + Loc.T(" ms"), fC) + Pad() * 2);
            float h = Math.Max(40, Math.Min(160, st.GetI("ft.height"))) * S * US;
            if (g != null)
            {
                using (Pen p = new Pen(LineC, 1f))
                    g.DrawRectangle(p, x, y, cw - 1, h - 1);

                float pad = 3 * S;
                float labelH = st.GetB("ft.showtime") ? 15 * S : 0;
                float baseY = y + h - pad - labelH;
                float gx = x + pad;
                float gw = cw - 2 * pad;
                float gh = Math.Max(4, baseY - (y + pad));

                List<int> hist = s.FrameTimes;
                if (hist != null && hist.Count >= 2)
                {
                    float maxMs = 50f;
                    for (int i = 0; i < hist.Count; i++)
                        if (hist[i] > maxMs) maxMs = hist[i];
                    maxMs = Math.Max(20f, Math.Min(1000f, maxMs * 1.15f));

                    PointF[] pts = new PointF[hist.Count];
                    float step = gw / (hist.Count - 1);
                    for (int i = 0; i < hist.Count; i++)
                    {
                        float v = Math.Min(hist[i], maxMs);
                        pts[i] = new PointF(gx + i * step, baseY - v / maxMs * gh);
                    }

                    int fill = Math.Max(0, Math.Min(100, st.GetI("ft.fill")));
                    if (fill > 0)
                    {
                        using (GraphicsPath path = new GraphicsPath())
                        {
                            path.AddLines(pts);
                            path.AddLine(pts[pts.Length - 1].X, baseY, pts[0].X, baseY);
                            path.CloseFigure();
                            using (Brush fb = new SolidBrush(Color.FromArgb((int)(fill * 1.4), FtgRed.R, FtgRed.G, FtgRed.B)))
                                g.FillPath(fb, path);
                        }
                    }

                    using (Pen line = new Pen(FtgRed, Math.Max(1f, 1.15f * S)))
                        g.DrawLines(line, pts);
                }

                using (Pen basePen = new Pen(FtgRed, Math.Max(1f, 1.2f * S)))
                    g.DrawLine(basePen, gx, baseY, gx + gw, baseY);

                if (st.GetB("ft.showtime"))
                {
                    string msText = Loc.T("— ms");
                    if (s.FpsLatency != null) msText = s.FpsLatency.Value.ToString("0.0", Cul) + Loc.T(" ms");
                    else if (hist != null && hist.Count > 0) msText = hist[hist.Count - 1].ToString(Cul) + Loc.T(" ms");
                    Text(gx, y + h - labelH - pad, cw - 2 * pad, msText, fC, FtgRed, false);
                }
            }
            y += h;
            Sep();
            return true;
        }

        // ---------- меловка ----------

        void Header(float x, float cw, string text, string bigValue)
        {
            int h = (int)HdrH();
            text = Loc.T(text);
            float hw = bigValue != null ? cw * 0.62f : cw;
            if (measureG != null)
            {
                float mw = TW(text, fH) + Pad() * 2 + 8 * S;
                if (bigValue != null) mw += TW(bigValue, fBig) + 16 * S;
                Need(mw);
            }
            Draw(g2 => GText(g2, text, fH, new Rectangle((int)x, (int)y, (int)hw, h), curHeader, false));
            if (bigValue != null)
                Draw(g2 => GText(g2, bigValue, fBig, new Rectangle((int)x, (int)y, (int)cw, h), curBig, true));
            y += HdrH();
        }

        void Row(string label, string value, Color vc)
        {
            int h = (int)RowH();
            float x = Pad();
            float cw = CurrentW;
            if (measureG != null) Need(TW(Loc.T(label), fL) + TW(value, fV) + Pad() * 2 + 16 * S);
            Draw(g2 => GText(g2, Loc.T(label), fL, new Rectangle((int)x, (int)y, (int)(cw * 0.7f), h), curLabel, false));
            Draw(g2 => GText(g2, value, fV, new Rectangle((int)x, (int)y, (int)cw, h), vc, true));
            y += RowH();
        }

        void SubRow(string text)
        {
            int h = (int)RowH();
            float x = Pad();
            if (measureG != null) Need(TW(text, fL) + Pad() * 2 + 8 * S);
            Draw(g2 => GText(g2, text, fL, new Rectangle((int)x, (int)y, (int)CurrentW, h), curLabel, false));
            y += h;
        }

        void CoreCell(float x, float w, int idx)
        {
            if (idx >= s.CoreFreqs.Count) return;
            string label = Loc.T("Core ") + (idx + 1).ToString("00", Cul) + ":";
            int hh = (int)CoreH();
            float cx = Pad();
            if (measureG != null)
            {
                float cell = TW(label, fC) + TW(Freq(s.CoreFreqs[idx]), fC) + 12 * S;
                Need(cell * 2 + Pad() * 2);
            }
            Draw(g2 => GText(g2, label, fC, new Rectangle((int)x, (int)y, (int)(w * 0.55f), hh), curLabel, false));
            Draw(g2 => GText(g2, Freq(s.CoreFreqs[idx]), fC, new Rectangle((int)x, (int)y, (int)w, hh), curValue, true));
        }

        void DrawCoreGraph(float x, float w, float h)
        {
            LastGraphRect = new RectangleF(x, y, w, h);
            if (measureG != null) Need(220 * S);
            float baseline = y + h * 0.8f;
            Draw(g2 =>
            {
                using (Pen p = new Pen(GreenC, 1.2f * S))
                {
                    p.DashStyle = DashStyle.Custom;
                    p.DashPattern = new float[] { 3f, 3f };
                    g2.DrawLine(p, x, baseline, x + w, baseline);
                }
            });
            List<double> loads = s.ThreadLoads;
            if (loads.Count == 0) { y += h; return; }
            int n = loads.Count;
            float slot = w / n;
            float bw = Math.Max(2f * S, slot * 0.6f);
            float maxH = h * 0.74f;
            for (int i = 0; i < n; i++)
            {
                float pct = (float)loads[i];
                float bh; Color c;
                if (pct < 2f) { bh = 3f * S; c = GreenC; }
                else
                {
                    bh = pct / 100f * maxH;
                    if (bh < 4f * S) bh = 4f * S;
                    c = pct < 30 ? GreenC : pct < 60 ? YellowC : pct < 85 ? OrangeC : RedC;
                }
                float bx = x + i * slot + (slot - bw) / 2f;
                Draw(g2 =>
                {
                    using (Brush br = new SolidBrush(c))
                        g2.FillRectangle(br, bx, baseline - bh, bw, bh);
                });
                if (st.GetB("cpu.corelabels"))
                    Draw(g2 => GText(g2, "P" + i, fC,
                        new Rectangle((int)(bx - slot), (int)(baseline + 2 * S), (int)(slot * 2f), (int)(10 * S)),
                        curLabel, false));
            }
            y += h;
        }

        void Sep()
        {
            y += Gap();
            float x = Pad();
            Draw(g2 =>
            {
                using (Pen p = new Pen(LineC, 1f))
                    g2.DrawLine(p, x, y, x + CurrentW, y);
            });
            y += Gap();
        }

        float CurrentW;
        public float DrawWithWidth(Graphics gg, float w) { CurrentW = w - 2 * Pad(); return Draw(w); }

        // форматтеры с учётом единиц
        string Temp(double? v)
        {
            if (v == null) return "—";
            if (st.GetS("ov.tempUnit") == "F")
                return (v.Value * 9.0 / 5.0 + 32.0).ToString("0", Cul) + " °F";
            return v.Value.ToString("0", Cul) + " °C";
        }
        string Pct(double? v) { return v == null ? "—" : v.Value.ToString("0", Cul) + " %"; }
        string W(double? v) { return v == null ? "—" : v.Value.ToString("0.0", Cul) + Loc.T(" W"); }
        string Freq(double? mhz)
        {
            if (mhz == null) return "—";
            if (st.GetS("ov.freqUnit") == "MHz") return ((int)Math.Round(mhz.Value)).ToString(Cul) + Loc.T(" MHz");
            // ниже 1 ГГц показываем МГц, иначе получается «0,0 ГГц»
            if (mhz.Value < 1000) return ((int)Math.Round(mhz.Value)).ToString(Cul) + Loc.T(" MHz");
            return (mhz.Value / 1000.0).ToString("0.00", Cul) + Loc.T(" GHz");
        }

        static string ShortPresentMode(string mode)
        {
            int i = mode.IndexOf(':');
            return (i > 0 ? mode.Substring(0, i) : mode).Trim();
        }
        string Mem(double? used, double? total)
        {
            if (used == null) return "—";
            bool mb = st.GetS("ov.memUnit") == "MB";
            string u = mb ? Loc.T(" MB") : Loc.T(" GB");
            double uu = mb ? used.Value * 1024 : used.Value;
            if (total == null) return uu.ToString(mb ? "0" : "0.0", Cul) + u;
            double tt = mb ? total.Value * 1024 : total.Value;
            return uu.ToString(mb ? "0" : "0.0", Cul) + " / " + tt.ToString(mb ? "0" : "0.0", Cul) + u;
        }
        string Volt(double? v) { return v == null ? "—" : v.Value.ToString("0.000", Cul) + Loc.T(" V"); }

        Color TempColored(double? t, int warn, int crit)
        {
            if (t == null) return curValue;
            if (t.Value >= crit) return RedC;
            if (t.Value >= warn) return OrangeC;
            return GreenC;
        }
        Color LoadColored(double? v, bool enabled)
        {
            if (v == null || !enabled) return curValue;
            double x = v.Value;
            if (x >= 90) return RedC;
            if (x >= 75) return OrangeC;
            if (x >= 50) return YellowC;
            return GreenC;
        }

        // ---------- инфраструктура отрисовки ----------
        void Draw(Action<Graphics> a)
        {
            if (g != null) a(g);
        }

        // GDI+ вместо TextRenderer: сохраняет альфа-канал при рендере в layered-окно.
        void GText(Graphics g2, string t, Font f, Rectangle r, Color c, bool right)
        {
            if (g2 == null || t == null) return;
            using (Brush b = new SolidBrush(c))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = right ? StringAlignment.Far : StringAlignment.Near;
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                g2.DrawString(t, f, b, r, sf);
            }
        }

        void Text(float x, float y, float w, string t, Font f, Color c, bool right)
        {
            Draw(gg =>
            {
                using (StringFormat sf = new StringFormat())
                using (Brush br = new SolidBrush(c))
                {
                    sf.Alignment = right ? StringAlignment.Far : StringAlignment.Near;
                    sf.LineAlignment = StringAlignment.Near;
                    float tw = Math.Max(20 * S, w);
                    RectangleF r = new RectangleF(x, y, tw, Math.Max(20 * S, f.Height));
                    gg.DrawString(t, f, br, r, sf);
                }
            });
        }
    }
}
