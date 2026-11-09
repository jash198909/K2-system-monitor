using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace K2
{
    // Живое интерактивное превью оверлея: рисует тем же кодом (OverlayDraw),
    // что и настоящий виджет, поэтому показывает точный результат настроек.
    public class PreviewControl : Control
    {
        public Settings St;
        public Snapshot Snap;
        public Stats Stat;
        public const float ScaleValue = 0.75f;
        public float ScaleF = ScaleValue;

        public PreviewControl()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (St == null || Snap == null) { e.Graphics.Clear(Color.FromArgb(12, 16, 22)); return; }
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int radius = (int)(Math.Max(0, St.GetI("look.radius")) * ScaleF);
            using (GraphicsPath p = Round(new Rectangle(0, 0, Width, Height), radius))
                Region = new Region(p);

            // Шахматка показывает прозрачность: поверх неё фон рисуется с той же
            // альфой, что и в реальном оверлее (100% = фон полностью исчезает).
            DrawChecker(g);
            int opacity = Math.Max(0, Math.Min(100, St.GetI("look.opacity")));
            int a = (int)Math.Round(255.0 * (100 - opacity) / 100.0);
            if (a > 0)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(a, 42, 42, 46)))
                    g.FillRectangle(b, ClientRectangle);
            }

            OverlayDraw od = new OverlayDraw(g, St, Snap, Stat, ScaleF);
            od.DrawWithWidth(g, Width);
            od.DisposeFonts();
        }

        // Подобрать масштаб так, чтобы превью влезло в панель по ширине.
        public static float CalcScale(Settings st, int maxWidth)
        {
            float baseW = OverlayDraw.WidthOf(st, 1f);
            float sc = ScaleValue;
            if (maxWidth > 40 && baseW > 1 && baseW * sc > maxWidth)
                sc = maxWidth / baseW;
            return Math.Max(0.15f, Math.Min(ScaleValue, sc));
        }

        // измерить нужный размер превью для текущих настроек
        public static Size MeasureSize(Settings st, Snapshot snap, Stats stat, float scale)
        {
            if (scale <= 0) scale = ScaleValue;
            int w = Math.Max(140, (int)OverlayDraw.WidthOf(st, scale));
            OverlayDraw probe = new OverlayDraw(null, st, snap, stat, scale);
            float h = probe.DrawWithWidth(null, w);
            probe.DisposeFonts();
            return new Size(w, Math.Max(80, (int)h));
        }

        void DrawChecker(Graphics g)
        {
            const int cell = 10;
            using (SolidBrush a = new SolidBrush(Color.FromArgb(26, 31, 38)))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(17, 21, 27)))
            {
                for (int y = 0; y < Height; y += cell)
                    for (int x = 0; x < Width; x += cell)
                        g.FillRectangle(((x / cell + y / cell) & 1) == 0 ? a : b, x, y, cell, cell);
            }
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
    }
}
