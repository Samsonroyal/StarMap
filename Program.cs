using System;
using Microsoft.UI.Xaml;

namespace StarMap
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            App.Log.Info("Main entered.");
            try
            {
                Application.Start(p => new App());
            }
            catch (Exception ex)
            {
                App.Log.Error("Application.Start failed: " + ex);
                throw;
            }
            App.Log.Info("Main exited.");
        }
    }
}
