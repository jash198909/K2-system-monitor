using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace K2
{
    // FPS/время кадра через официальный Intel PresentMon (MIT).
    // PresentMon получает ETW-события презентации конкретного процесса,
    // поэтому счётчик не зависит от статичности экрана, смены режима и фокуса.
    public class FpsEngine
    {
        struct Sample
        {
            public long TimeMs;
            public int FrameMs;
        }

        readonly object gate = new object();
        readonly Queue<Sample> history = new Queue<Sample>();
        readonly int[] histogram = new int[1001]; // сессионные интервалы, 1..1000 мс

        Process pm;
        Thread reader;
        Thread errReader;
        volatile bool stop;
        bool running;
        string lastError = "";

        int targetPid;
        string targetProc = "";
        int lastStartTick;
        long sessionStartMs;
        long lastFrameAtMs;
        int sessionFrames;
        int avgFrames;
        double avgFrameMsSum;
        int histogramTotal;

        double _fps;
        double _avg;
        double _frameMs;
        string _presentMode = "";
        string _renderApi = "";

        public double? Fps
        {
            get
            {
                lock (gate)
                {
                    if (lastFrameAtMs == 0 || NowMs() - lastFrameAtMs > 10000) return null;
                    return _fps > 0 ? (double?)_fps : null;
                }
            }
        }

        public double? Avg
        {
            get { lock (gate) { return _avg > 0 ? (double?)_avg : null; } }
        }

        public double? LatencyMs
        {
            get
            {
                lock (gate)
                {
                    if (lastFrameAtMs == 0 || NowMs() - lastFrameAtMs > 10000) return null;
                    return _frameMs > 0.5 ? (double?)_frameMs : null;
                }
            }
        }

        public double? Low1 { get { return Quantile(0.01); } }
        public double? Low01 { get { return Quantile(0.001); } }
        public string PresentMode { get { lock (gate) { return _presentMode; } } }
        public string RenderApi { get { lock (gate) { return _renderApi; } } }
        public string LastError { get { lock (gate) { return lastError; } } }
        public bool Running { get { lock (gate) { return running; } } }

        static long NowMs()
        {
            return DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        }

        static string ToolPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "PresentMon-2.4.1-x64.exe");
        }

        // PresentMon не всегда снимает ETW-сессию при аварийном завершении.
        // Явно останавливаем её, иначе следующий запуск не сможет создать сессию.
        static void CleanupTraceSession()
        {
            try
            {
                string logman = Path.Combine(Environment.SystemDirectory, "logman.exe");
                if (!File.Exists(logman)) return;
                ProcessStartInfo psi = new ProcessStartInfo(logman, "stop K2-PresentMon -ets");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    try { p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd(); } catch { }
                    p.WaitForExit(600);
                }
            }
            catch { }
        }

        public void Start(int pid, string procName)
        {
            if (pid <= 0) { Stop(); return; }

            lock (gate)
            {
                if (pid == targetPid && running) return;
                if (pid == targetPid && Environment.TickCount - lastStartTick < 1500) return;
            }

            Stop();
            stop = false;
            CleanupTraceSession();

            string exe = ToolPath();
            if (!File.Exists(exe))
            {
                SetError("PresentMon not found: " + exe);
                Log("ERROR: PresentMon not found: " + exe);
                return;
            }

            lock (gate)
            {
                targetPid = pid;
                targetProc = procName ?? "";
                lastStartTick = Environment.TickCount;
                lastError = "";
                ResetDataLocked();
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = exe;
                psi.Arguments =
                    "--process_id " + pid +
                    " --output_stdout --no_console_stats --no_track_gpu --no_track_input" +
                    " --v2_metrics" +
                    " --terminate_on_proc_exit" +
                    " --session_name K2-PresentMon";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;

                Process p = new Process();
                p.StartInfo = psi;
                p.EnableRaisingEvents = true;
                p.Exited += delegate(object s, EventArgs e)
                {
                    lock (gate)
                    {
                        if (pm == p) running = false;
                    }
                };
                if (!p.Start())
                {
                    p.Dispose();
                    SetError("PresentMon did not start");
                    return;
                }

                lock (gate) { pm = p; running = true; }
                Log("start pid=" + pid + " (" + procName + ")");

                reader = new Thread(delegate() { ReadOutput(p); });
                reader.IsBackground = true;
                reader.Name = "K2.PresentMon.out";
                reader.Start();

                errReader = new Thread(delegate() { ReadErrors(p); });
                errReader.IsBackground = true;
                errReader.Name = "K2.PresentMon.err";
                errReader.Start();
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
                Log("EXCEPTION start: " + ex);
                lock (gate) { running = false; }
            }
        }

        public void Stop()
        {
            Process p;
            Thread r;
            Thread er;
            lock (gate)
            {
                if (pm == null && reader == null && !running && targetPid == 0) return;
                stop = true;
                p = pm;
                r = reader;
                er = errReader;
                pm = null;
                reader = null;
                errReader = null;
                running = false;
                targetPid = 0;
                targetProc = "";
            }

            try
            {
                if (p != null && !p.HasExited) p.Kill();
            }
            catch { }
            try
            {
                if (p != null) p.WaitForExit(1000);
            }
            catch { }
            try
            {
                if (r != null && r != Thread.CurrentThread) r.Join(600);
                if (er != null && er != Thread.CurrentThread) er.Join(300);
            }
            catch { }
            try { if (p != null) p.Dispose(); } catch { }
            CleanupTraceSession();

            lock (gate) { ResetDataLocked(); }
        }

        public void ResetAvg()
        {
            lock (gate)
            {
                sessionFrames = 0;
                avgFrames = 0;
                avgFrameMsSum = 0;
                sessionStartMs = 0;
                Array.Clear(histogram, 0, histogram.Length);
                histogramTotal = 0;
                _avg = 0;
                _frameMs = 0;
                history.Clear();
                lastFrameAtMs = 0;
                _fps = 0;
            }
        }

        // История для графика: последние 5 секунд, не больше 1200 точек.
        public List<int> FrameHistory()
        {
            lock (gate)
            {
                long from = NowMs() - 5000;
                List<Sample> tmp = new List<Sample>();
                foreach (Sample s in history)
                    if (s.TimeMs >= from) tmp.Add(s);
                int stride = Math.Max(1, (tmp.Count + 1199) / 1200);
                List<int> res = new List<int>();
                for (int i = 0; i < tmp.Count; i += stride) res.Add(tmp[i].FrameMs);
                return res;
            }
        }

        void SetError(string msg)
        {
            lock (gate) { lastError = msg; }
        }

        void ReadErrors(Process p)
        {
            try
            {
                string line;
                while ((line = p.StandardError.ReadLine()) != null)
                {
                    if (line.Length > 0)
                    {
                        SetError(line);
                        Log("stderr: " + line);
                    }
                }
            }
            catch { }
        }

        void ReadOutput(Process p)
        {
            try
            {
                string header = p.StandardOutput.ReadLine();
                if (header == null)
                {
                    SetError("PresentMon produced no output");
                    return;
                }

                Dictionary<string, int> col = ParseHeader(header);
                if (!col.ContainsKey("ProcessID") && !col.ContainsKey("Application"))
                {
                    SetError("Unexpected PresentMon CSV header");
                    Log("header: " + header);
                    return;
                }

                string line;
                while (!stop && (line = p.StandardOutput.ReadLine()) != null)
                {
                    if (line.Length == 0) continue;
                    try { ParseRow(line, col); }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                if (!stop) { SetError(ex.Message); Log("reader: " + ex.Message); }
            }
            finally
            {
                lock (gate)
                {
                    if (pm == p) running = false;
                }
                Log("PresentMon exited");
            }
        }

        static Dictionary<string, int> ParseHeader(string header)
        {
            Dictionary<string, int> d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string[] parts = SplitCsv(header);
            for (int i = 0; i < parts.Length; i++)
            {
                string name = parts[i].Trim().TrimStart('\uFEFF');
                if (name.Length > 0) d[name] = i;
            }
            return d;
        }

        static string[] SplitCsv(string line)
        {
            if (line.IndexOf('"') < 0) return line.Split(',');
            List<string> parts = new List<string>();
            StringBuilder sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else quoted = !quoted;
                }
                else if (c == ',' && !quoted)
                {
                    parts.Add(sb.ToString());
                    sb.Length = 0;
                }
                else sb.Append(c);
            }
            parts.Add(sb.ToString());
            return parts.ToArray();
        }

        static string GetStr(string[] f, Dictionary<string, int> c, string name)
        {
            int i;
            if (!c.TryGetValue(name, out i) || i < 0 || i >= f.Length) return "";
            return f[i].Trim();
        }

        static double GetNum(string[] f, Dictionary<string, int> c, string name)
        {
            string s = GetStr(f, c, name);
            if (s.Length == 0 || s == "NA") return double.NaN;
            double v;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN;
        }

        void ParseRow(string line, Dictionary<string, int> c)
        {
            string[] f = SplitCsv(line);
            if (f.Length == 0) return;

            double pid = GetNum(f, c, "ProcessID");
            if (!double.IsNaN(pid) && targetPid > 0 && (int)pid != targetPid) return;

            // Сначала время презентации приложения (FrameTime / MsBetweenPresents):
            // именно его показывает игровой счётчик. DisplayedTime может быть в 2 раза
            // больше из-за Magpie/FSR/композитора.
            double frame = GetNum(f, c, "FrameTime");
            if (double.IsNaN(frame) || frame <= 0)
                frame = GetNum(f, c, "MsBetweenPresents");
            if (double.IsNaN(frame) || frame <= 0)
                frame = GetNum(f, c, "MsBetweenSimulationStart");
            if (double.IsNaN(frame) || frame <= 0)
                frame = GetNum(f, c, "DisplayedTime");
            if (double.IsNaN(frame) || frame <= 0)
                frame = GetNum(f, c, "MsBetweenDisplayChange");
            if (double.IsNaN(frame) || frame <= 0 || frame > 2000) return;

            string mode = GetStr(f, c, "PresentMode");
            string runtime = GetStr(f, c, "PresentRuntime");

            AddFrame((int)Math.Round(frame), mode, runtime);
        }

        void AddFrame(int frameMs, string mode, string runtime)
        {
            if (frameMs < 1) frameMs = 1;
            if (frameMs > 1000) frameMs = 1000;

            long now = NowMs();
            lock (gate)
            {
                lastFrameAtMs = now;
                if (sessionStartMs == 0) sessionStartMs = now;
                sessionFrames++;
                // загрузочные паузы >250 мс не должны превращать AVG в «1 FPS»
                if (frameMs <= 250)
                {
                    avgFrames++;
                    avgFrameMsSum += frameMs;
                }

                histogram[frameMs]++;
                histogramTotal++;

                history.Enqueue(new Sample { TimeMs = now, FrameMs = frameMs });
                while (history.Count > 0 && now - history.Peek().TimeMs > 10000) history.Dequeue();
                if (history.Count > 6000) history.Dequeue();

                double sum = 0;
                int cnt = 0;
                foreach (Sample s in history)
                {
                    if (s.TimeMs >= now - 1000) { sum += s.FrameMs; cnt++; }
                }
                if (cnt > 0)
                {
                    _frameMs = sum / cnt;
                    if (_frameMs > 0.1) _fps = 1000.0 / _frameMs;
                }

                if (avgFrames > 0)
                {
                    double avgFrameMs = avgFrameMsSum / avgFrames;
                    if (avgFrameMs > 0.1) _avg = 1000.0 / avgFrameMs;
                }

                if (mode.Length > 0) _presentMode = mode;
                if (runtime.Length > 0 && runtime != "NA") _renderApi = runtime;
            }
        }

        double? Quantile(double p)
        {
            lock (gate)
            {
                if (histogramTotal < 30) return null;
                int target = (int)Math.Ceiling(histogramTotal * p);
                if (target < 1) target = 1;
                int cum = 0;
                for (int ms = 1000; ms >= 1; ms--)
                {
                    cum += histogram[ms];
                    if (cum >= target) return 1000.0 / ms;
                }
                return null;
            }
        }

        void ResetDataLocked()
        {
            history.Clear();
            Array.Clear(histogram, 0, histogram.Length);
            histogramTotal = 0;
            sessionFrames = 0;
            avgFrames = 0;
            avgFrameMsSum = 0;
            sessionStartMs = 0;
            lastFrameAtMs = 0;
            _fps = 0;
            _avg = 0;
            _frameMs = 0;
            _presentMode = "";
            _renderApi = "";
        }

        void Log(string s)
        {
            try
            {
                string p = Monitor.LogPath("fps_status.txt");
                FileInfo fi = new FileInfo(p);
                if (fi.Exists && fi.Length > 512 * 1024)
                {
                    string old = Monitor.LogPath("fps_status.old.txt");
                    try { if (File.Exists(old)) File.Delete(old); } catch { }
                    try { File.Move(p, old); } catch { try { File.Delete(p); } catch { } }
                }
                File.AppendAllText(p, DateTime.Now.ToString("HH:mm:ss") + " " + s + "\r\n");
            }
            catch { }
        }
    }
}
