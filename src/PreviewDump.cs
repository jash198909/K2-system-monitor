using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace K2
{
    // Автотест рендера: K2.exe --preview-dump [ini] [png]
    // рисует демо-оверлей с указанными настройками в PNG (без админ-прав)
    static class PreviewDump
    {
        public static void Go(string[] args)
        {
            string ini = null;
            string outp = Path.Combine(Monitor.BaseDir(), "preview_dump.png");
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i].EndsWith(".ini", StringComparison.OrdinalIgnoreCase)) ini = args[i];
                else if (args[i].EndsWith(".png", StringComparison.OrdinalIgnoreCase)) outp = args[i];
            }
            Settings st = ini != null ? Settings.LoadFile(ini) : Settings.Load();
            Loc.Set(st.GetS("ui.lang"));
            Snapshot s = Monitor.MakeFake();
            Stats stats = new Stats();
            for (int i = 0; i < 40; i++) stats.Update(s);

            int w = OverlayDraw.WidthOf(st, 1f);
            OverlayDraw probe = new OverlayDraw(null, st, s, stats, 1f);
            int h = (int)probe.DrawWithWidth(null, w);
            probe.DisposeFonts();
            if (h < 40) h = 40;

            using (Bitmap bmp = new Bitmap(w, h))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Black);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(42, 42, 46)))
                        g.FillRectangle(b, 0, 0, w, h);
                    OverlayDraw od = new OverlayDraw(g, st, s, stats, 1f);
                    od.DrawWithWidth(g, w);
                    od.DisposeFonts();
                }
                bmp.Save(outp, ImageFormat.Png);
            }
            Console.WriteLine("dump: " + outp + " " + w + "x" + h);
        }
    }
}
