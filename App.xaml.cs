using System.Windows;
using SQLitePCL;

namespace ClockbusterWPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            Batteries.Init();
            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            InitialSetupWindow setup = new InitialSetupWindow();
            
            if (setup.ShowDialog() == true)
            {
                ClockbusterWPF.Properties.Settings.Default.DeviceId = setup.DeviceName;
                ClockbusterWPF.Properties.Settings.Default.DatabasePath = setup.DatabasePath;
                ClockbusterWPF.Properties.Settings.Default.Save();

                MainWindow main = new MainWindow();
                this.MainWindow = main;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                main.Show();
            }
            else
            {
                Shutdown();
            }
        }
    }
}