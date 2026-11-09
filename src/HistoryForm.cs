using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace K2
{
    // Окно истории: кривые загрузки/температур за ~10 минут (клик по графику оверлея)
    public class HistoryForm : Form
    {
        Stats stats;
        Timer rep;
        float S = 1f;
        Font fHead, fLab;
        bool dragging; Point dragStart; RectangleF closeRect;

        static readonly Color BackC = Color.FromArgb(20, 26, 34);
        static readonly Color GridC = Color.FromArgb(38, 50, 65);
        static readonly Color TextC = Color.FromArgb(200, 212, 224);
        static readonly Color CpuC = Color.FromArgb(46, 204, 64);
        static readonly Color GpuC = Color.FromArgb(0, 191, 166);
        static readonly Color TempC = Color.FromArgb(240, 140, 60);
        static readonly Color RamC = Color.FromArgb(120, 160, 220);

        public HistoryForm(Stats stats)
        {
            this.stats = stats;
            Text = "K2 — история";
            FormBorderStyle = FormBorderStyle.None;
            BackColor = BackC;
            TopMost = true;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            try { Icon = MainForm.AppIcon(); } catch { }
            S = DeviceDpi / 96f;
            ClientSize = new Size((int)(460 * S), (int)(240 * S));
            fHead = new Font("Segoe UI", 10.5f * S, FontStyle.Bold);
            fLab = new Font("Segoe UI", 8f * S);
            KeyDown += delegate(object s2, KeyEventArgs e2) { if (e2.KeyCode == Keys.Escape) Close(); };
            rep = new Timer();
            rep.Interval = 1000;
            rep.Tick += delegate { Invalidate(); };
            rep.Start();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            using (GraphicsPath p = Round(new Rectangle(0, 0, Width, Height), (int)(12 * S)))
                Region = new Region(p);
        }

        static GraphicsPath Round(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackC);
            float x = 14 * S, w = ClientSize.Width - 28 * S;
            float y = 12 * S;
            TextRenderer.DrawText(g, "История за " + PeriodText(), fHead, new Rectangle((int)x, (int)y, (int)(w * 0.7f), (int)(22 * S)),
                TextC, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            closeRect = new RectangleF(x + w - 20 * S, y, 20 * S, 20 * S);
            using (Brush b = new SolidBrush(TextC)) g.DrawString("✕", fHead, b, closeRect.X, closeRect.Y);

            y += 24 * S;
            y = Legend(g, x, y, "загр CPU", CpuC); y = Legend(g, x + 110 * S, y, "загр GPU", GpuC);
            Legend(g, x + 230 * S, y - 16 * S, "t° CPU", TempC);
            Legend(g, x + 330 * S, y - 16 * S, "ОЗУ %", RamC);

            float cx = x + 28 * S, cy = y + 4 * S;
            float cw = w - 28 * S, ch = ClientSize.Height - cy - 22 * S;
            using (Pen gp = new Pen(GridC, 1f))
            {
                gp.DashStyle = DashStyle.Dot;
                float[] lv = new float[] { 25, 50, 75, 100 };
                for (int i = 0; i < lv.Length; i++)
                {
                    float ly = cy + ch - lv[i] / 100f * ch;
                    g.DrawLine(gp, cx, ly, cx + cw, ly);
                }
            }
            using (Brush tb = new SolidBrush(Color.FromArgb(150, 164, 180)))
            {
                g.DrawString("0", fLab, tb, cx - 20 * S, cy + ch - 6 * S);
                g.DrawString("100", fLab, tb, cx - 28 * S, cy - 6 * S);
            }
            List<Stats.Pt> h = stats.History;
            if (h.Count >= 2)
            {
                Series(g, h, cx, cy, cw, ch, true, false, CpuC);
                Series(g, h, cx, cy, cw, ch, false, false, GpuC);
                Series(g, h, cx, cy, cw, ch, false, true, TempC);
                Series(g, h, cx, cy, cw, ch, true, true, RamC);
            }
        }

        string PeriodText()
        {
            double sec = Stats.HistMax * Math.Max(250, stats.IntervalMs) / 1000.0;
            if (sec < 90) return Math.Round(sec) + " с";
            double min = sec / 60.0;
            return min.ToString("0.#", CultureInfo.GetCultureInfo("ru-RU")) + " мин";
        }

        void Series(Graphics g, List<Stats.Pt> h, float cx, float cy, float cw, float ch, bool cpu, bool temp, Color c)
        {
            using (Pen p = new Pen(c, 1.7f * S))
            {
                PointF prev = PointF.Empty; bool has = false;
                for (int i = 0; i < h.Count; i++)
                {
                    float px = cx + (i / (float)(Stats.HistMax - 1)) * cw;
                    Stats.Pt pt = h[i];
                    double v = temp ? pt.CpuTemp : (cpu ? pt.CpuLoad : pt.GpuLoad);
                    if (cpu && temp) v = pt.RamPct;
                    float py = cy + ch - (float)(v / 100.0) * ch;
                    if (py < cy) py = cy; if (py > cy + ch) py = cy + ch;
                    if (has) g.DrawLine(p, prev, new PointF(px, py));
                    prev = new PointF(px, py); has = true;
                }
            }
        }

        float Legend(Graphics g, float x, float y, string text, Color c)
        {
            using (Brush b = new SolidBrush(c)) g.FillRectangle(b, x, y + 3 * S, 9 * S, 9 * S);
            using (Brush t = new SolidBrush(Color.FromArgb(180, 192, 206)))
                g.DrawString(text, fLab, t, x + 12 * S, y);
            return y + 18 * S;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right || closeRect.Contains(new PointF(e.X, e.Y))) { Close(); return; }
            dragging = true; dragStart = e.Location;
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging && e.Button == MouseButtons.Left)
                Location = new Point(Left + e.X - dragStart.X, Top + e.Y - dragStart.Y);
            base.OnMouseMove(e);
        }
        protected override void OnMouseUp(MouseEventArgs e) { dragging = false; base.OnMouseUp(e); }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            rep.Stop(); rep.Dispose();
            base.OnFormClosed(e);
        }
    }
}
