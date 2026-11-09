using System;
using System.Collections.Generic;

namespace K2
{
    // Минимумы/максимумы за сессию + история для окна «график времени»
    public class Stats
    {
        public bool Paused;
        Dictionary<string, double[]> mm = new Dictionary<string, double[]>(); // key -> {min,max,avgSum,n}

        public struct Pt { public double CpuLoad, GpuLoad, CpuTemp, RamPct; }
        public List<Pt> History = new List<Pt>();
        public const int HistMax = 600; // ~10 мин при интервале 1 с
        public int IntervalMs = 1000;   // фактический интервал обновления

        void Feed(string key, double? v)
        {
            if (v == null) return;
            double[] a;
            if (!mm.TryGetValue(key, out a)) { a = new double[] { v.Value, v.Value, 0, 0 }; mm[key] = a; }
            if (v.Value < a[0]) a[0] = v.Value;
            if (v.Value > a[1]) a[1] = v.Value;
            a[2] += v.Value; a[3]++;
        }

        public void Update(Snapshot s)
        {
            if (Paused) return;
            Feed("gput", s.GpuTemp);
            Feed("cput", s.CpuTemp);
            Feed("vramt", s.GpuMemTemp);
            Feed("hotspot", s.GpuHotspot);
            Feed("gpuv", s.GpuVolt);
            Feed("gpup", s.GpuPower);
            Feed("cpup", s.CpuPower);
            Pt p = new Pt();
            p.CpuLoad = s.CpuLoad.HasValue ? s.CpuLoad.Value : 0;
            p.GpuLoad = s.GpuLoad.HasValue ? s.GpuLoad.Value : 0;
            p.CpuTemp = s.CpuTemp.HasValue ? s.CpuTemp.Value : 0;
            p.RamPct = s.RamPct;
            History.Add(p);
            if (History.Count > HistMax) History.RemoveAt(0);
        }

        public string Get(string key, int mode) // 0=minmax, 1=avgmax, 2=minavgmax
        {
            double[] a;
            if (!mm.TryGetValue(key, out a) || a[3] == 0) return null;
            string mn = Fmt(a[0]), mx = Fmt(a[1]), av = Fmt(a[2] / a[3]);
            if (mode == 0) return mn + " / " + mx;
            if (mode == 1) return av + " / " + mx;
            return mn + " / " + av + " / " + mx;
        }

        static string Fmt(double v)
        {
            return v >= 100 ? v.ToString("0") : v.ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
        }

        public void Reset()
        {
            mm.Clear();
            History.Clear();
        }
    }
}
