using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;

namespace K2
{
    // Простое хранилище настроек key=value (INI-подобный файл)
    public class Settings
    {
        Dictionary<string, string> kv = new Dictionary<string, string>();

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public string GetS(string k, string def = "")
        {
            string v;
            if (kv.TryGetValue(k, out v)) return v;
            string d = Default(k);
            return d ?? def;
        }
        public bool GetB(string k)
        {
            string v;
            if (!kv.TryGetValue(k, out v)) v = Default(k);
            return v == "1";
        }
        public int GetI(string k)
        {
            string v;
            if (!kv.TryGetValue(k, out v)) v = Default(k);
            int r; return int.TryParse(v, NumberStyles.Integer, Inv, out r) ? r : 0;
        }
        public void Set(string k, string v) { kv[k] = v; }
        public void Set(string k, bool v) { kv[k] = v ? "1" : "0"; }
        public void Set(string k, int v) { kv[k] = v.ToString(Inv); }

        public static readonly string[] ColorKeys = new string[]
        {
            "color.panel", "color.header", "color.label", "color.value",
            "color.line", "color.mark", "color.accent", "color.stat",
            "color.green", "color.yellow", "color.orange", "color.red", "color.ftg",
            "color.fps.header", "color.fps.label", "color.fps.value",
            "color.gpu.header", "color.gpu.label", "color.gpu.value",
            "color.cpu.header", "color.cpu.label", "color.cpu.value",
            "color.ram.header", "color.ram.label", "color.ram.value",
            "color.stat.header", "color.stat.label", "color.stat.value",
            "color.gpu.zones", "color.cpu.zones"
        };

        public Color GetColor(string k, Color def)
        {
            string v = GetS(k);
            if (!string.IsNullOrEmpty(v))
            {
                string[] p = v.Split(',');
                int r, g, b;
                if (p.Length == 3 &&
                    int.TryParse(p[0].Trim(), NumberStyles.Integer, Inv, out r) &&
                    int.TryParse(p[1].Trim(), NumberStyles.Integer, Inv, out g) &&
                    int.TryParse(p[2].Trim(), NumberStyles.Integer, Inv, out b))
                    return Color.FromArgb(C255(r), C255(g), C255(b));
            }
            return def;
        }

        public void SetColor(string k, Color c)
        {
            Set(k, c.R.ToString(Inv) + "," + c.G.ToString(Inv) + "," + c.B.ToString(Inv));
        }

        public void ResetColors()
        {
            for (int i = 0; i < ColorKeys.Length; i++) kv.Remove(ColorKeys[i]);
        }

        static int C255(int v) { return v < 0 ? 0 : v > 255 ? 255 : v; }

        public List<string> Keys() { return new List<string>(kv.Keys); }

        // значения по умолчанию; всё, чего тут нет, считается выключенным/0/""
        static string Default(string k)
        {
            switch (k)
            {
                // общие
                case "ui.lang": return "en";
                case "ov.enabled": return "1";
                case "ov.autostart": return "0";
                case "ov.checkStart": return "1";
                case "ov.onlyGame": return "0";
                case "ov.interval": return "1000";
                case "ov.tempUnit": return "C";
                case "ov.freqUnit": return "GHz";
                case "ov.memUnit": return "GB";
                // расположение
                case "pos.corner": return "TR";
                case "pos.offX": return "0";
                case "pos.offY": return "0";
                case "pos.pin": return "0";
                case "pos.mode": return "pin";
                case "pos.monitor": return "-1";
                case "pos.order": return "fps,gpu,cpu,ram,stat,ftg";
                // внешний вид
                case "look.width": return "100";
                case "look.scale": return "100";
                case "look.rowH": return "20";
                case "look.opacity": return "20";
                case "look.radius": return "16";
                case "look.font": return "Segoe UI";
                case "look.fontSize": return "100";
                case "look.showTime": return "0";
                case "look.mode": return "float";
                case "look.autoWidth": return "0";
                case "look.heightPx": return "0";
                // цвета оверлея (R,G,B)
                case "color.panel": return "20,22,26";
                case "color.header": return "240,240,240";
                case "color.label": return "166,166,166";
                case "color.value": return "240,240,240";
                case "color.line": return "82,82,88";
                case "color.mark": return "120,120,126";
                case "color.accent": return "0,191,166";
                case "color.stat": return "255,170,80";
                case "color.green": return "46,204,64";
                case "color.yellow": return "229,198,75";
                case "color.orange": return "240,140,60";
                case "color.red": return "230,80,60";
                case "color.ftg": return "245,45,38";
                case "color.gpu.zones": return "1";
                case "color.cpu.zones": return "1";
                // горячие клавиши (вкл+моды+клавиша)
                case "hk.show": return "+Alt+F8";
                case "hk.reset": return "+Alt+F5";
                case "hk.mode": return "+Alt+F7";
                case "hk.settings": return "+F10";
                case "hk.measure": return "+Alt+F11";
                // FPS
                case "fps.show": return "1";
                case "fps.avg": return "0";
                case "fps.low1": return "0";
                case "fps.low01": return "0";
                case "fps.lat": return "1";
                case "fps.proc": return "0";
                case "fps.api": return "1";
                case "fps.hideNa": return "1";
                // видеокарта
                case "gpu.show": return "1";
                case "gpu.load": return "1";
                case "gpu.temp": return "1";
                case "gpu.clock": return "0";
                case "gpu.volt": return "0";
                case "gpu.power": return "1";
                case "gpu.fanrpm": return "0";
                case "gpu.fanpct": return "1";
                case "gpu.hotspot": return "0";
                case "gpu.memtemp": return "0";
                case "gpu.memclock": return "0";
                case "gpu.vram": return "1";
                case "gpu.loadcolor": return "1";
                case "gpu.warn": return "75";
                case "gpu.crit": return "85";
                // процессор
                case "cpu.show": return "1";
                case "cpu.load": return "1";
                case "cpu.temp": return "1";
                case "cpu.clock": return "1";
                case "cpu.power": return "1";
                case "cpu.fanrpm": return "0";
                case "cpu.fanpct": return "1";
                case "cpu.ccd": return "0";
                case "cpu.eff": return "1";
                case "cpu.coregraph": return "1";
                case "cpu.corelabels": return "0";
                case "cpu.corefreqs": return "1";
                case "cpu.loadcolor": return "1";
                case "cpu.warn": return "75";
                case "cpu.crit": return "85";
                // ОЗУ
                case "ram.show": return "1";
                case "ram.used": return "1";
                case "ram.load": return "1";
                case "ram.speed": return "0";
                case "ram.dimmt": return "0";
                case "ram.format": return "usedtotal";
                case "ram.speedMode": return "hw";
                case "ram.speedManual": return "2400";
                // статистика
                case "stat.show": return "0";
                case "stat.gput": return "1";
                case "stat.cput": return "1";
                case "stat.vramt": return "0";
                case "stat.hotspot": return "0";
                case "stat.gpuv": return "1";
                case "stat.gpup": return "1";
                case "stat.cpup": return "1";
                case "stat.autoreset": return "1";
                // график времени кадра
                case "ft.show": return "0";
                case "ft.height": return "80";
                case "ft.fill": return "50";
                case "ft.showtime": return "1";
            }
            return null;
        }

        // Приводит произвольные значения из INI/импорта к безопасным диапазонам.
        // Вызывается при загрузке, сохранении и применении настроек.
        public void Normalize()
        {
            ClampInt("ov.interval", 250, 5000);
            ClampInt("look.width", 50, 200);
            ClampInt("look.scale", 50, 200);
            ClampInt("look.rowH", 16, 40);
            ClampInt("look.opacity", 0, 100);
            ClampInt("look.radius", 0, 30);
            ClampInt("look.fontSize", 60, 150);
            ClampInt("look.heightPx", 0, 4000);
            ClampInt("pos.offX", -100, 100);
            ClampInt("pos.offY", -100, 100);
            ClampInt("pos.monitor", -1, 15);
            ClampInt("gpu.warn", 50, 95);
            ClampInt("gpu.crit", 60, 110);
            ClampInt("cpu.warn", 50, 95);
            ClampInt("cpu.crit", 60, 110);
            ClampInt("ram.speedManual", 800, 8000);
            ClampInt("ft.height", 40, 160);
            ClampInt("ft.fill", 0, 100);

            if (GetI("gpu.crit") <= GetI("gpu.warn"))
                Set("gpu.crit", Math.Min(110, GetI("gpu.warn") + 1));
            if (GetI("cpu.crit") <= GetI("cpu.warn"))
                Set("cpu.crit", Math.Min(110, GetI("cpu.warn") + 1));

            EnsureOneOf("ui.lang", "en", "ru");
            EnsureOneOf("ov.tempUnit", "C", "F");
            EnsureOneOf("ov.freqUnit", "GHz", "MHz");
            EnsureOneOf("ov.memUnit", "GB", "MB");
            EnsureOneOf("pos.corner", "TR", "TL", "BR", "BL", "C");
            EnsureOneOf("pos.mode", "pin", "manual", "corner");
            EnsureOneOf("look.mode", "float", "pin");

            if (GetS("look.font").Trim().Length == 0) Set("look.font", "Segoe UI");
            if (GetS("pos.order").Trim().Length == 0) Set("pos.order", Default("pos.order"));
        }

        void ClampInt(string k, int min, int max)
        {
            string raw;
            if (!kv.TryGetValue(k, out raw)) return;
            int v;
            if (!int.TryParse(raw, NumberStyles.Integer, Inv, out v))
            {
                int.TryParse(Default(k), NumberStyles.Integer, Inv, out v);
            }
            if (v < min) v = min;
            if (v > max) v = max;
            Set(k, v);
        }

        void EnsureOneOf(string k, params string[] allowed)
        {
            string v = GetS(k);
            for (int i = 0; i < allowed.Length; i++)
                if (v == allowed[i]) return;
            Set(k, allowed[0]);
        }

        public string Path2() { return Monitor.LogPath("settings.ini"); }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                string f = s.Path2();
                if (File.Exists(f))
                    foreach (string line in File.ReadAllLines(f, Encoding.UTF8))
                    {
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        s.kv[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                    }
            }
            catch { }
            s.Normalize();
            return s;
        }

        public void Save()
        {
            Normalize();
            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<string, string> p in kv)
                    sb.AppendLine(p.Key + "=" + p.Value);
                File.WriteAllText(Path2(), sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        public void Save2(string file)
        {
            Normalize();
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, string> p in kv)
                sb.AppendLine(p.Key + "=" + p.Value);
            File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
        }

        public static Settings LoadFile(string file)
        {
            Settings s = new Settings();
            foreach (string line in File.ReadAllLines(file, Encoding.UTF8))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                s.kv[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            s.Normalize();
            return s;
        }

        public Settings Clone()
        {
            Settings s = new Settings();
            foreach (KeyValuePair<string, string> p in kv) s.kv[p.Key] = p.Value;
            return s;
        }

        // число отличий draft от saved (для статус-бара «Изменено N»)
        public int DiffCount(Settings other)
        {
            int n = 0;
            HashSet<string> all = new HashSet<string>(Keys());
            foreach (string k in other.Keys()) all.Add(k);
            foreach (string k in all)
                if (GetS(k) != other.GetS(k)) n++;
            return n;
        }
    }
}
