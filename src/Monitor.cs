using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using LibreHardwareMonitor.Hardware;

namespace K2
{
    class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) { computer.Traverse(this); }
        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (IHardware sub in hardware.SubHardware) sub.Traverse(this);
        }
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }

    public class Snapshot
    {
        public string CpuName = "";
        public string GpuName = "";
        public double? GpuLoad, GpuTemp, GpuPower, GpuFanPct, GpuMemUsedGb, GpuMemTotalGb;
        public double? CpuLoad, CpuTemp, CpuFreq, CpuEffFreq, CpuPower, CpuFanPct, CpuFanRpm;
        public double? CpuCoreTempMin, CpuCoreTempMax;
        public double? GpuClock, GpuVolt, GpuHotspot, GpuMemTemp, GpuMemClock;
        public double? AuxPower;
        public double? PsuInputPower;
        public List<double?> CoreFreqs = new List<double?>();
        public List<double?> CoreEff = new List<double?>();
        public List<double> ThreadLoads = new List<double>();
        public double RamUsedGb, RamTotalGb, RamPct;
        public double? RamSpeedMHz, RamDimmTemp;
        public double? GpuFanRpm;
        public double? Fps;                 // из FpsEngine (desktop duplication)
        public double? FpsAvg;
        public double? FpsLatency, FpsLow1, FpsLow01;
        public List<int> FrameTimes = new List<int>(); // интервалы кадров, мс (для графика)
        public int FgMonitor = -1;
        public int FgPid;
        public string FgProc = "";
        public string Resolution = "";
        public string PresentMode = "";
        public string RenderApi = "";
        public bool FgFullscreen;
        public bool FgChromeless;           // окно без заголовка/рамки (borderless)
        public double FgAreaPct;            // площадь активного окна / площадь монитора
        public int FgWidth, FgHeight;       // размеры активного окна
        public bool Ready;
    }

    [StructLayout(LayoutKind.Sequential)]
    class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
        public MEMORYSTATUSEX() { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
    }

    static class Monitor
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        static Computer computer;
        static UpdateVisitor visitor = new UpdateVisitor();
        static CultureInfo Cul { get { return Loc.Culture; } }

        public static string BaseDir()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "K2");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string LogPath(string fileName)
        {
            return Path.Combine(BaseDir(), fileName);
        }

        static string LogDir()
        {
            string dir = Path.Combine(BaseDir(), "log");
            Directory.CreateDirectory(dir);
            return dir;
        }

        // CSV-лог раз в 5 секунд: %LOCALAPPDATA%\K2\log\yyyy-MM-dd.csv
        public static void LogRow(Snapshot s)
        {
            try
            {
                string file = Path.Combine(LogDir(), DateTime.Now.ToString("yyyy-MM-dd") + ".csv");
                bool isNew = !File.Exists(file);
                using (StreamWriter w = new StreamWriter(file, true, new UTF8Encoding(true)))
                {
                    if (isNew)
                        w.WriteLine(Loc.T("Time;CPU load %;CPU t°C;CPU GHz;CPU W;GPU load %;GPU t°C;GPU W;GPU fan %;VRAM GB;RAM GB;RAM %"));
                    w.WriteLine(string.Join(";",
                        DateTime.Now.ToString("HH:mm:ss"),
                        Fix(s.CpuLoad, 0), Fix(s.CpuTemp, 0), FmtGhz(s.CpuFreq), Fix(s.CpuPower, 1),
                        Fix(s.GpuLoad, 0), Fix(s.GpuTemp, 0), Fix(s.GpuPower, 1), Fix(s.GpuFanPct, 0),
                        Fix(s.GpuMemUsedGb, 1), s.RamUsedGb.ToString("0.0", Cul), ((int)s.RamPct).ToString(Cul)));
                }
            }
            catch { }
        }

        public static void LogCleanup()
        {
            try
            {
                foreach (string f in Directory.GetFiles(LogDir(), "*.csv"))
                    if (File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddDays(-14))
                        File.Delete(f);
            }
            catch { }
        }

        static string Fix(double? v, int decimals)
        {
            if (v == null) return "";
            return v.Value.ToString(decimals == 0 ? "0" : "0.0", Cul);
        }

        static string FmtGhz(double? mhz)
        {
            if (mhz == null) return "";
            return (mhz.Value / 1000.0).ToString("0.0", Cul);
        }

        static Computer OpenComputer()
        {
            Computer c = new Computer();
            c.IsCpuEnabled = true;
            c.IsGpuEnabled = true;
            c.IsMemoryEnabled = true;
            c.IsMotherboardEnabled = true;
            c.IsStorageEnabled = false;
            c.IsControllerEnabled = false;
            c.IsNetworkEnabled = false;
            c.IsPsuEnabled = true;
            c.IsBatteryEnabled = false;
            c.Open();
            c.Accept(visitor);
            return c;
        }

        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        const int GWL_STYLE = -16;
        const int WS_MAXIMIZE = 0x01000000;
        const int WS_CAPTION = 0x00C00000;
        const int WS_THICKFRAME = 0x00040000;
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out WinRect r);
        [StructLayout(LayoutKind.Sequential)] struct WinRect { public int L, T, R, B; }

        static void FillForeground(Snapshot s)
        {
            try
            {
                WinRect wr;
                IntPtr fg = GetForegroundWindow();
                if (fg == IntPtr.Zero || !GetWindowRect(fg, out wr)) return;
                uint wpid;
                GetWindowThreadProcessId(fg, out wpid);
                if (wpid != 0)
                {
                    s.FgPid = (int)wpid;
                    using (System.Diagnostics.Process pr = System.Diagnostics.Process.GetProcessById((int)wpid))
                        s.FgProc = pr.ProcessName;
                }
                var screens = System.Windows.Forms.Screen.AllScreens;
                int cx = (wr.L + wr.R) / 2, cy = (wr.T + wr.B) / 2;
                System.Drawing.Rectangle vb = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                for (int i = 0; i < screens.Length; i++)
                    if (screens[i].Bounds.Contains(cx, cy)) { s.FgMonitor = i; vb = screens[i].Bounds; break; }
                s.Resolution = vb.Width + "x" + vb.Height;
                int wa = (wr.R - wr.L) * (wr.B - wr.T);
                int va = vb.Width * vb.Height;
                s.FgWidth = wr.R - wr.L;
                s.FgHeight = wr.B - wr.T;
                s.FgAreaPct = va > 0 ? Math.Min(1.0, (double)wa / va) : 0;
                // «игровой» экран = окно во весь монитор БЕЗ шапки/рамки и НЕ «максимизированное»
                // (максимизированный браузер — это не fullscreen-режим)
                int style = GetWindowLong(fg, GWL_STYLE);
                bool chromeless = (style & WS_CAPTION) == 0 && (style & WS_THICKFRAME) == 0;
                s.FgChromeless = chromeless;
                bool notMaximized = (style & WS_MAXIMIZE) == 0;
                s.FgFullscreen = wa >= va * 9 / 10 && chromeless && notMaximized &&
                    s.FgProc != "" &&
                    s.FgProc.IndexOf("explorer", StringComparison.OrdinalIgnoreCase) < 0 &&
                    s.FgProc.IndexOf("ShellExperience", StringComparison.OrdinalIgnoreCase) < 0 &&
                    s.FgProc.IndexOf("SearchHost", StringComparison.OrdinalIgnoreCase) < 0 &&
                    s.FgProc.IndexOf("K2", StringComparison.OrdinalIgnoreCase) < 0;
            }
            catch { }
        }

        static double? ramSpeedCache;
        static bool ramSpeedGot;
        static double? RamSpeed()
        {
            if (ramSpeedGot) return ramSpeedCache;
            ramSpeedGot = true;
            // 1) фактическая частина шины памяти: MSFT_PhysicalMemory.ConfiguredClockSpeed
            try
            {
                double best = 0;
                using (System.Management.ManagementObjectSearcher q =
                       new System.Management.ManagementObjectSearcher("root\\StandardCimv2",
                           "SELECT ConfiguredClockSpeed FROM MSFT_PhysicalMemory"))
                    foreach (System.Management.ManagementObject o in q.Get())
                    {
                        object v = o["ConfiguredClockSpeed"];
                        if (v == null) continue;
                        double d = Convert.ToDouble(v);
                        if (d > 0 && (best == 0 || d < best)) best = d; // контроллер ставит минимум
                    }
                if (best > 0) { ramSpeedCache = best; return ramSpeedCache; }
            }
            catch { }
            // 2) запасной вариант: номинал из SPD (старый метод)
            try
            {
                double best = 0;
                using (System.Management.ManagementObjectSearcher q =
                       new System.Management.ManagementObjectSearcher("SELECT Speed FROM Win32_PhysicalMemory"))
                    foreach (System.Management.ManagementObject o in q.Get())
                    {
                        object v = o["Speed"];
                        if (v != null) { double d = Convert.ToDouble(v); if (d > best) best = d; }
                    }
                if (best > 0) ramSpeedCache = best;
            }
            catch { }
            return ramSpeedCache;
        }

        public static void Refresh(Snapshot s)
        {
            if (computer == null) computer = OpenComputer();
            computer.Accept(visitor);

            IHardware cpu = null, gpu = null, board = null;
            foreach (IHardware hw in computer.Hardware)
            {
                if (hw.HardwareType == HardwareType.Cpu && cpu == null) cpu = hw;
                else if (hw.HardwareType == HardwareType.Motherboard && board == null) board = hw;
                else if (IsGpu(hw.HardwareType))
                {
                    if (gpu == null || hw.HardwareType != HardwareType.GpuIntel) gpu = hw;
                    if (hw.HardwareType == HardwareType.GpuNvidia || hw.HardwareType == HardwareType.GpuAmd) gpu = hw;
                }
            }

            if (cpu != null)
            {
                s.CpuName = ShortCpu(cpu.Name);
                s.CpuLoad = Val(Find(cpu, SensorType.Load, "Total"));
                ISensor t = Find(cpu, SensorType.Temperature, "Package");
                if (t == null) t = Find(cpu, SensorType.Temperature, "Core Max");
                s.CpuTemp = Val(t);
                ISensor p = Find(cpu, SensorType.Power, "Package");
                if (p == null) p = Find(cpu, SensorType.Power, "CPU");
                s.CpuPower = Val(p);

                // per-core clocks
                s.CoreFreqs = CollectClocks(cpu, false);
                s.CpuFreq = MaxMhz(s.CoreFreqs);
                s.ThreadLoads = CollectThreadLoads(cpu);
                // effective frequency: no direct MSR access from LHM — approximate as
                // average core clock * load (not max core * load)
                double? avgFreq = AvgMhz(s.CoreFreqs);
                if (avgFreq != null && s.CpuLoad != null)
                    s.CpuEffFreq = avgFreq * (s.CpuLoad.Value / 100.0);

                if (board != null)
                {
                    ISensor fan = Find(board, SensorType.Control, "CPU");
                    if (fan == null) fan = FirstControlFan(board);
                    s.CpuFanPct = Val(fan);
                    ISensor rpm = Find(board, SensorType.Fan, "CPU");
                    if (rpm == null) rpm = FirstFanRpm(board);
                    s.CpuFanRpm = Val(rpm);
                }
                // мин/макс температур ядер (для строки «Температуры кристаллов»)
                double? tmin = null, tmax = null;
                foreach (ISensor sn in cpu.Sensors)
                {
                    if (sn.SensorType != SensorType.Temperature || sn.Name == null || !sn.Value.HasValue) continue;
                    if (sn.Name.IndexOf("Core #", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (sn.Name.IndexOf("Distance", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (tmin == null || sn.Value < tmin) tmin = sn.Value;
                    if (tmax == null || sn.Value > tmax) tmax = sn.Value;
                }
                s.CpuCoreTempMin = tmin; s.CpuCoreTempMax = tmax;
            }

            if (gpu != null)
            {
                s.GpuName = ShortGpu(gpu.Name);
                s.GpuLoad = Val(Find(gpu, SensorType.Load, "GPU Core"));
                if (s.GpuLoad == null) s.GpuLoad = Val(Find(gpu, SensorType.Load, "Core"));
                ISensor gt = Find(gpu, SensorType.Temperature, "GPU Core");
                if (gt == null) gt = Find(gpu, SensorType.Temperature, "Hot Spot");
                if (gt == null) gt = Find(gpu, SensorType.Temperature, "Core");
                s.GpuTemp = Val(gt);
                ISensor gp = Find(gpu, SensorType.Power, "GPU Package");
                if (gp == null) gp = Find(gpu, SensorType.Power, "Package");
                if (gp == null) gp = Find(gpu, SensorType.Power, "Power");
                s.GpuPower = Val(gp);
                ISensor fp = Find(gpu, SensorType.Control, "Fan");
                s.GpuFanPct = Val(fp);
                ISensor fr = Find(gpu, SensorType.Fan, "Fan");
                s.GpuFanRpm = Val(fr);
                s.GpuClock = Val(Find(gpu, SensorType.Clock, "GPU Core"));
                s.GpuMemClock = Val(Find(gpu, SensorType.Clock, "GPU Memory"));
                s.GpuVolt = Val(Find(gpu, SensorType.Voltage, "GPU Core"));
                s.GpuHotspot = Val(Find(gpu, SensorType.Temperature, "Hot Spot"));
                s.GpuMemTemp = Val(Find(gpu, SensorType.Temperature, "GPU Memory"));
            }

            CollectAuxPower(s);

            // VRAM: в разных сборках LHM датчики памяти висят либо на GPU, либо на
            // узле [Memory]. Сканируем всё дерево и берём выделенную видеопамять;
            // D3D Shared / Virtual Memory — это ОЗУ, а не VRAM.
            double? gpuUsed = null, gpuTotal = null, dedUsed = null, dedTotal = null, anyUsed = null, anyTotal = null;
            foreach (IHardware hw in computer.Hardware)
            {
                foreach (ISensor sn in hw.Sensors)
                {
                    if (sn.Name == null || !sn.Value.HasValue) continue;
                    string n = sn.Name.ToLowerInvariant();
                    bool small = sn.SensorType == SensorType.SmallData;
                    if (!small && sn.SensorType != SensorType.Data) continue;
                    if (!n.Contains("memory")) continue;
                    if (n.Contains("shared") || n.Contains("virtual") || n.Contains("available") || n.Contains("free")) continue;
                    bool used = n.Contains("used"), total = n.Contains("total");
                    if (!used && !total) continue;
                    double gb = small ? sn.Value.Value / 1024.0 : sn.Value.Value;
                    if (n.Contains("gpu memory"))
                    {
                        if (used) gpuUsed = gb; else gpuTotal = gb;
                    }
                    else if (n.Contains("dedicated"))
                    {
                        if (used) dedUsed = gb; else dedTotal = gb;
                    }
                    else if (IsGpu(hw.HardwareType))
                    {
                        // «Memory Used/Total» без уточнения принимаем только с GPU-узла,
                        // иначе можно поймать системную память узла [Memory].
                        if (used) anyUsed = gb; else anyTotal = gb;
                    }
                }
            }
            double? memUsed = gpuUsed ?? dedUsed ?? anyUsed;
            double? memTotal = gpuTotal ?? dedTotal ?? anyTotal;
            if (memUsed != null) s.GpuMemUsedGb = memUsed;
            if (memTotal != null) s.GpuMemTotalGb = memTotal;

            // RAM via kernel
            MEMORYSTATUSEX ms = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(ms))
            {
                s.RamTotalGb = ms.ullTotalPhys / 1073741824.0;
                s.RamUsedGb = (ms.ullTotalPhys - ms.ullAvailPhys) / 1073741824.0;
                s.RamPct = ms.dwMemoryLoad;
            }
            s.RamSpeedMHz = RamSpeed();
            FillForeground(s);
            s.Ready = true;
        }

        static bool IsGpu(HardwareType t)
        {
            return t == HardwareType.GpuNvidia || t == HardwareType.GpuAmd || t == HardwareType.GpuIntel;
        }

        // parse "CPU Core #N Thread #M" load sensors -> index (N-1)*threadsPerCore + (M-1)
        static List<double> CollectThreadLoads(IHardware hw)
        {
            List<double> loads = new List<double>();
            CollectThreadLoadsRec(hw, loads);
            return loads;
        }

        static void CollectThreadLoadsRec(IHardware hw, List<double> loads)
        {
            foreach (ISensor sn in hw.Sensors)
            {
                if (sn.SensorType != SensorType.Load || sn.Name == null || !sn.Value.HasValue) continue;
                string n = sn.Name;
                int ci = n.IndexOf("Core #", StringComparison.OrdinalIgnoreCase);
                int ti = n.IndexOf("Thread #", StringComparison.OrdinalIgnoreCase);
                if (ci < 0 || ti < 0) continue;
                int core, thread;
                if (!int.TryParse(n.Substring(ci + 6).Trim().Split(' ')[0], out core)) continue;
                if (!int.TryParse(n.Substring(ti + 8).Trim().Split(' ')[0], out thread)) continue;
                int idx = (core - 1) * 2 + (thread - 1);
                while (loads.Count <= idx) loads.Add(0);
                loads[idx] = sn.Value.Value;
            }
            foreach (IHardware sub in hw.SubHardware) CollectThreadLoadsRec(sub, loads);
        }

        public static void Shutdown()
        {
            try { if (computer != null) { computer.Close(); computer = null; } }
            catch { }
        }

        static void CollectAuxPower(Snapshot s)
        {
            double aux = 0;
            bool found = false;
            double? psu = null;
            foreach (IHardware hw in computer.Hardware)
                ScanPower(hw, ref aux, ref found, ref psu);
            s.AuxPower = found ? (double?)aux : null;
            s.PsuInputPower = psu;
        }

        static void ScanPower(IHardware hw, ref double aux, ref bool found, ref double? psu)
        {
            string ht = hw.HardwareType.ToString();
            bool skip = ht == "Cpu" || ht.StartsWith("Gpu", StringComparison.OrdinalIgnoreCase);
            if (!skip)
            {
                foreach (ISensor sn in hw.Sensors)
                {
                    if (sn.SensorType != SensorType.Power || !sn.Value.HasValue) continue;
                    string n = (sn.Name ?? "").ToLowerInvariant();
                    if (ht == "Psu")
                    {
                        // Настоящее потребление из розетки, если БП отдаёт телеметрию.
                        if (n.Contains("input") || n.Contains(" ac"))
                        {
                            double v = sn.Value.Value;
                            if (v > 0 && (psu == null || v > psu.Value)) psu = v;
                        }
                    }
                    else if (ht == "Memory")
                    {
                        if (n.Contains("virtual") || n.Contains("total")) continue;
                        aux += sn.Value.Value;
                        found = true;
                    }
                    else if (ht == "Storage")
                    {
                        aux += sn.Value.Value;
                        found = true;
                    }
                    else if (ht == "Motherboard")
                    {
                        // Только чипсет и память: VRM/CPU-датчики уже входят в CPU Package.
                        if ((n.Contains("chipset") || n.Contains("pch") ||
                             n.Contains("dimm") || n.Contains("memory") || n.Contains("ram")) &&
                            !n.Contains("cpu") && !n.Contains("vrm") && !n.Contains("gpu"))
                        {
                            aux += sn.Value.Value;
                            found = true;
                        }
                    }
                }
            }
            foreach (IHardware sub in hw.SubHardware) ScanPower(sub, ref aux, ref found, ref psu);
        }

        static ISensor Find(IHardware hw, SensorType type, string namePart)
        {
            return FindIn(hw, type, namePart);
        }

        static ISensor FirstControlFan(IHardware hw)
        {
            return FirstTyped(hw, SensorType.Control, true);
        }

        static ISensor FirstFanRpm(IHardware hw)
        {
            return FirstTyped(hw, SensorType.Fan, true);
        }

        static ISensor FirstTyped(IHardware hw, SensorType type, bool requirePositive)
        {
            foreach (ISensor sn in hw.Sensors)
            {
                if (sn.SensorType == type && sn.Name != null &&
                    sn.Name.IndexOf("Fan", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    sn.Value.HasValue && (!requirePositive || sn.Value.Value > 0))
                    return sn;
            }
            foreach (IHardware sub in hw.SubHardware)
            {
                ISensor sn = FirstTyped(sub, type, requirePositive);
                if (sn != null) return sn;
            }
            return null;
        }

        static ISensor FindIn(IHardware hw, SensorType type, string namePart)
        {
            foreach (ISensor sn in hw.Sensors)
            {
                if (sn.SensorType == type && sn.Name != null &&
                    sn.Name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    sn.Value.HasValue)
                    return sn;
            }
            foreach (IHardware sub in hw.SubHardware)
            {
                ISensor sn = FindIn(sub, type, namePart);
                if (sn != null) return sn;
            }
            return null;
        }

        static List<double?> CollectClocks(IHardware hw, bool effective)
        {
            List<double?> result = new List<double?>();
            CollectClocksRec(hw, effective, result);
            return result;
        }

        static void CollectClocksRec(IHardware hw, bool effective, List<double?> result)
        {
            if (effective) return; // LHM does not expose per-core effective clocks; computed elsewhere
            foreach (ISensor sn in hw.Sensors)
            {
                if (sn.SensorType != SensorType.Clock || sn.Name == null) continue;
                if (!sn.Value.HasValue) continue;
                string n = sn.Name;
                if (n.IndexOf("Core #", StringComparison.OrdinalIgnoreCase) < 0) continue;
                int idx = IndexOf(n);
                while (result.Count <= idx) result.Add(null);
                result[idx] = sn.Value;
            }
            foreach (IHardware sub in hw.SubHardware) CollectClocksRec(sub, effective, result);
        }

        static int IndexOf(string name)
        {
            int i = name.IndexOf('#');
            if (i >= 0 && i + 1 < name.Length)
            {
                int v;
                if (int.TryParse(name.Substring(i + 1).Trim(), out v)) return v - 1;
            }
            string[] parts = name.Split(' ');
            foreach (string p in parts)
            {
                int v;
                if (int.TryParse(p, out v)) return v;
            }
            return 0;
        }

        static double? Val(ISensor sn) { return sn == null ? null : sn.Value; }

        static double? MaxMhz(List<double?> list)
        {
            double? m = null;
            foreach (double? v in list) if (v.HasValue && (m == null || v > m)) m = v;
            return m;
        }

        static double? AvgMhz(List<double?> list)
        {
            double sum = 0; int n = 0;
            foreach (double? v in list) if (v.HasValue) { sum += v.Value; n++; }
            return n == 0 ? (double?)null : sum / n;
        }

        // Лимиты контроллера памяти CPU (Intel ARK / спецификации iMC).
        // Возвращает максимальную реальную частоту DDR для данного CPU, если он известен.
        public static double? CpuMemCapMhz(string cpuName)
        {
            if (string.IsNullOrEmpty(cpuName)) return null;
            string n = cpuName.ToLowerInvariant();
            // Xeon E5 v4 / Broadwell-EP: 2400
            if (n.Contains("v4") && (n.Contains("e5") || n.Contains("e7"))) return 2400;
            // Xeon E5 v3 / Haswell-EP: 2133
            if (n.Contains("v3") && (n.Contains("e5") || n.Contains("e7"))) return 2133;
            // Xeon E5 v2 / Ivy-EP: 1866
            if (n.Contains("v2") && (n.Contains("e5") || n.Contains("e7"))) return 1866;
            // Skylake-W (7900X и т.п.): 2400
            if (n.Contains("i7-98") || n.Contains("i7-99") || n.Contains("i9-79") || n.Contains("i9-99")) return 2400;
            // клиентские: по поколению (грубая, но безопасная оценка)
            if (n.IndexOf("i3-6", StringComparison.Ordinal) >= 0 || n.IndexOf("i5-6", StringComparison.Ordinal) >= 0 || n.IndexOf("i7-6", StringComparison.Ordinal) >= 0) return 2133;
            if (n.IndexOf("i3-7", StringComparison.Ordinal) >= 0 || n.IndexOf("i5-7", StringComparison.Ordinal) >= 0 || n.IndexOf("i7-7", StringComparison.Ordinal) >= 0) return 2400;
            if (n.IndexOf("i3-8", StringComparison.Ordinal) >= 0 || n.IndexOf("i5-8", StringComparison.Ordinal) >= 0 || n.IndexOf("i7-8", StringComparison.Ordinal) >= 0 || n.IndexOf("i9-9", StringComparison.Ordinal) >= 0) return 2666;
            if (n.IndexOf("i3-10", StringComparison.Ordinal) >= 0 || n.IndexOf("i5-10", StringComparison.Ordinal) >= 0 || n.IndexOf("i7-10", StringComparison.Ordinal) >= 0 || n.IndexOf("i5-11", StringComparison.Ordinal) >= 0 || n.IndexOf("i7-11", StringComparison.Ordinal) >= 0) return 2933;
            if (n.IndexOf("i3-12", StringComparison.Ordinal) >= 0 || n.IndexOf("i5-12", StringComparison.Ordinal) >= 0 || n.IndexOf("i7-12", StringComparison.Ordinal) >= 0 || n.IndexOf("i5-13", StringComparison.Ordinal) >= 0 || n.IndexOf("i7-13", StringComparison.Ordinal) >= 0) return 3200;
            return null;
        }

        // демо-снимок для превью и --preview-dump
        public static Snapshot MakeFake()
        {
            Snapshot s = new Snapshot();
            s.GpuName = "Radeon RX 9060 XT";
            s.CpuName = "Xeon E5-2697 v4";
            s.GpuLoad = 74; s.GpuTemp = 46; s.GpuPower = 14.8; s.GpuFanPct = 38;
            s.AuxPower = 8.0;
            s.GpuMemUsedGb = 1.8; s.GpuMemTotalGb = 15.9; s.GpuClock = 1700; s.GpuVolt = 0.95;
            s.GpuHotspot = 57; s.GpuMemTemp = 52; s.GpuMemClock = 2502; s.GpuFanRpm = 1420;
            s.CpuLoad = 82; s.CpuTemp = 70; s.CpuFreq = 3100; s.CpuEffFreq = 2400; s.CpuPower = 31.5;
            s.CpuFanPct = 46; s.CpuFanRpm = 1180; s.CpuCoreTempMin = 66; s.CpuCoreTempMax = 73;
            for (int i = 0; i < 8; i++) s.CoreFreqs.Add(3100 + i * 25);
            for (int i = 0; i < 8; i++) s.ThreadLoads.Add(i == 2 ? 90 : i == 3 ? 60 : 10);
            s.RamUsedGb = 13.7; s.RamTotalGb = 31.1; s.RamPct = 44; s.RamSpeedMHz = 3200; s.RamDimmTemp = 44;
            FillDemoFps(s);
            s.Ready = true;
            return s;
        }

        // Демонстрационные значения FPS для превью настроек и --preview-dump.
        // Реальный оверлей использует их только когда FPS действительно считается.
        public static void FillDemoFps(Snapshot s)
        {
            s.Fps = 143; s.FpsAvg = 131; s.FpsLow1 = 98; s.FpsLow01 = 75; s.FpsLatency = 13.4;
            s.Resolution = "2560x1440"; s.FgProc = "game.exe";
            s.RenderApi = "DXGI"; s.PresentMode = "Composed: Flip";
            s.FrameTimes.Clear();
            for (int i = 0; i < 160; i++)
            {
                int v = 7 + (i % 11 == 0 ? 3 : 0) + (i % 37 == 0 ? 18 : 0) + (i % 53 == 0 ? 35 : 0);
                s.FrameTimes.Add(v);
            }
        }

        public static string ShortCpu(string name)
        {
            if (name == null) return "";
            string s = name.Replace("Intel", "").Replace("CPU", "").Replace("(R)", "").Replace("(TM)", "");
            s = s.Replace("  ", " ").Trim();
            return s;
        }

        public static string ShortGpu(string name)
        {
            if (name == null) return "";
            string s = name.Replace("(R)", "").Replace("(TM)", "").Replace("PCI DEV", "");
            s = s.Replace("  ", " ").Trim();
            if (s.StartsWith("AMD ", StringComparison.OrdinalIgnoreCase)) s = s.Substring(4);
            return s;
        }

        public static string FmtGHz(double? mhz)
        {
            if (mhz == null) return "—";
            return ((mhz.Value / 1000.0).ToString("0.0", Cul)) + Loc.T(" GHz");
        }
        public static string FmtPct(double? v)
        {
            if (v == null) return "—";
            return v.Value.ToString("0", Cul) + " %";
        }
        public static string FmtC(double? v)
        {
            if (v == null) return "—";
            return v.Value.ToString("0", Cul) + " °C";
        }
        public static string FmtW(double? v)
        {
            if (v == null) return "—";
            return v.Value.ToString("0.0", Cul) + Loc.T(" W");
        }
        public static string FmtGb(double? v)
        {
            if (v == null) return "—";
            return v.Value.ToString("0.0", Cul) + Loc.T(" GB");
        }

        // ----- sensor dump for diagnostics -----
        public static void DumpMain()
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                Computer c = OpenComputer();
                sb.AppendLine(Loc.T("=== computer opened ==="));
                foreach (IHardware hw in c.Hardware) DumpHw(hw, 0, sb);
                c.Close();
            }
            catch (Exception ex)
            {
                sb.AppendLine("ERROR: " + ex);
            }
            File.WriteAllText(LogPath("sensors.txt"), sb.ToString(), Encoding.UTF8);
        }

        static void DumpHw(IHardware hw, int depth, StringBuilder sb)
        {
            string pad = new string(' ', depth * 2);
            sb.AppendLine(pad + "[" + hw.HardwareType + "] " + hw.Name);
            foreach (ISensor sn in hw.Sensors)
                sb.AppendLine(pad + "    " + sn.SensorType + " | " + sn.Name + " | " + sn.Value + " | min=" + sn.Min + " max=" + sn.Max);
            foreach (IHardware sub in hw.SubHardware) DumpHw(sub, depth + 1, sb);
        }
    }
}
