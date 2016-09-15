﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

using Shadowsocks.Controller;
using Shadowsocks.Util;
using Shadowsocks.View;

namespace Shadowsocks
{
    static class Program
    {
        private static ShadowsocksController _controller;
        // XXX: Don't change this name
        private static MenuViewController _viewController;

        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Check OS since we are using dual-mode socket
            if (!Utils.IsWinVistaOrHigher())
            {
                MessageBox.Show(I18N.GetString("Unsupported operating system, use Windows Vista at least."),
                "Shadowsocks Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Utils.ReleaseMemory(true);
            using (Mutex mutex = new Mutex(false, "Global\\Shadowsocks_" + Application.StartupPath.GetHashCode()))
            {
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                Application.ApplicationExit += Application_ApplicationExit;
                SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                if (!mutex.WaitOne(0, false))
                {
                    Process[] oldProcesses = Process.GetProcessesByName("Shadowsocks");
                    if (oldProcesses.Length > 0)
                    {
                        Process oldProcess = oldProcesses[0];
                    }
                    MessageBox.Show(I18N.GetString("Find Shadowsocks icon in your notify tray.") + "\n" +
                        I18N.GetString("If you want to start multiple Shadowsocks, make a copy in another directory."),
                        I18N.GetString("Shadowsocks is already running."));
                    return;
                }
                Directory.SetCurrentDirectory(Application.StartupPath);
#if DEBUG
                Logging.OpenLogFile();

                // truncate privoxy log file while debugging
                string privoxyLogFilename = Utils.GetTempPath("privoxy.log");
                if (File.Exists(privoxyLogFilename))
                    using (new FileStream(privoxyLogFilename, FileMode.Truncate)) { }
#else
                Logging.OpenLogFile();
#endif
                _controller = new ShadowsocksController();
                _viewController = new MenuViewController(_controller);
                _controller.Start();
                Application.Run();
            }
        }

        private static int exited = 0;
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (Interlocked.Increment(ref exited) == 1)
            {
                Logging.Error(e.ExceptionObject?.ToString());
                MessageBox.Show(I18N.GetString("Unexpected error, shadowsocks will exit. Please report to") +
                    " https://github.com/shadowsocks/shadowsocks-windows/issues " +
                    Environment.NewLine + (e.ExceptionObject?.ToString()),
                    "Shadowsocks Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }
        }

        private static void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case PowerModes.Resume:
                    Logging.Info("os wake up");
                    _controller?.Start();
                    break;
                case PowerModes.Suspend:
                    _controller?.Stop();
                    Logging.Info("os suspend");
                    break;
            }
        }

        private static void Application_ApplicationExit(object sender, EventArgs e)
        {
            if (_controller != null)
            {
                _controller.Stop();
                _controller = null;
            }
        }
    }
}
