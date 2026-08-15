using System;
using System.IO;
using Microsoft.UI.Xaml;

namespace StarMap
{
    public partial class App : Application
    {
        public App()
        {
            App.Log.Info("App ctor entered.");
            this.InitializeComponent();
            App.Log.Info("App InitializeComponent done.");
            this.UnhandledException += App_UnhandledException;

            try
            {
                var window = new MainWindow();
                App.Log.Info("MainWindow created.");
                window.Activate();
                App.Log.Info("MainWindow activated.");
            }
            catch (Exception ex)
            {
                App.Log.Error("MainWindow init failed: " + ex);
                throw;
            }
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Log.Error($"Unhandled exception: {e.Exception}");
            e.Handled = true;
        }

        public static string LogPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StarMap",
                "starmap.log");

        public static class Log
        {
            private static readonly object _lock = new object();

            public static void Info(string message) => Write("INFO", message);
            public static void Error(string message) => Write("ERROR", message);

            private static void Write(string level, string message)
            {
                try
                {
                    lock (_lock)
                    {
                        var dir = Path.GetDirectoryName(LogPath);
                        if (dir != null) Directory.CreateDirectory(dir);
                        File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}\r\n");
                    }
                }
                catch
                {
                    // Logging must never crash the app.
                }
            }
        }
    }
}
