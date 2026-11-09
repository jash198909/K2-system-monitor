using System;
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
                            MessageBox.Show("K2 уже запущен — его значок находится в области уведомлений.",
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
