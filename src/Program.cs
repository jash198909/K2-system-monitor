using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace K2
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnResolve;

            // Перезапуск: новый экземпляр ждёт выхода старого, чтобы успеть
            // прочитать уже сохранённый язык и не поймать занятый мьютекс.
            int waitPid = 0;
            if (args != null)
                for (int i = 0; i + 1 < args.Length; i++)
                    if (args[i] == "--wait-pid") int.TryParse(args[i + 1], out waitPid);
            if (waitPid > 0)
            {
                try { using (Process p = Process.GetProcessById(waitPid)) p.WaitForExit(15000); }
                catch { }
            }

            try { Loc.Set(Settings.Load().GetS("ui.lang")); } catch { }
            try
            {
                if (args != null && Array.IndexOf(args, "--dump") >= 0)
                {
                    Monitor.DumpMain();
                    return;
                }
                if (args != null && Array.IndexOf(args, "--preview-dump") >= 0)
                {
                    PreviewDump.Go(args);
                    return;
                }

                bool createdNew;
                using (Mutex single = new Mutex(true, @"Local\K2.SystemMonitor", out createdNew))
                {
                    if (!createdNew)
                    {
                        try
                        {
                            MessageBox.Show(Loc.T("K2 is already running — its icon is in the notification area."),
                                "K2", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch { }
                        return;
                    }
                    Runner.Start();
                    GC.KeepAlive(single);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(Monitor.LogPath("startup_error.txt"), DateTime.Now + "\r\n" + ex);
                }
                catch { }
            }
        }

        public static bool RestartApp()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath);
                psi.Arguments = "--wait-pid " + Process.GetCurrentProcess().Id;
                psi.UseShellExecute = false;
                psi.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(Monitor.LogPath("error.txt"), DateTime.Now + " restart: " + ex + "\r\n"); } catch { }
                return false;
            }
            Application.Exit();
            return true;
        }

        static Assembly OnResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                string name = new AssemblyName(args.Name).Name;
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                foreach (string f in Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories))
                {
                    if (string.Equals(Path.GetFileNameWithoutExtension(f), name, StringComparison.OrdinalIgnoreCase))
                    {
                        try { return Assembly.LoadFrom(f); }
                        catch { }
                    }
                }
            }
            catch { }
            return null;
        }
    }

    static class Runner
    {
        public static void Start()
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            System.Windows.Forms.Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e2)
            {
                try { System.IO.File.AppendAllText(Monitor.LogPath("error.txt"), System.DateTime.Now + " UI: " + e2.Exception + "\r\n"); } catch { }
            };
            System.Windows.Forms.Application.Run(new MainForm());
        }
    }
}
