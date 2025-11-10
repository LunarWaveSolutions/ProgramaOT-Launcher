using System;
using System.Linq;
using System.Windows;

namespace ProgramaOTLauncher
{
    /// <summary>
    /// Lógica de interação para App.xaml. Representa a aplicação em si.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Sobrescreve o evento de inicialização para controlar o fluxo da aplicação.
        /// Decide se deve iniciar o launcher principal ou o processo de atualização.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                Logger.Info($"OnStartup. Args: {string.Join(" ", e.Args ?? new string[0])}");
                Logger.Info($"BaseDirectory: {AppDomain.CurrentDomain.BaseDirectory}");
            }
            catch { }

            // Set shutdown mode to prevent app from closing when splash screen closes
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Show splash screen
            var splashScreen = new SplashScreen();
            splashScreen.Show();

            // Close splash screen after a delay and then show the main or update window
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1); // Match the splash screen's original timer
            timer.Tick += (sender, args) =>
            {
                timer.Stop();
                splashScreen.Close();

                // Verifica se a aplicação foi iniciada com argumentos de atualização.
                bool isApplyUpdate = e.Args.Any(a => a.StartsWith("--apply-update", StringComparison.OrdinalIgnoreCase));
                bool isDownloadUpdate = e.Args.Any(a => a.StartsWith("--download-update", StringComparison.OrdinalIgnoreCase));

                if (isApplyUpdate || isDownloadUpdate)
                {
                    Logger.Info($"Fluxo de atualização detectado. isApplyUpdate={isApplyUpdate}, isDownloadUpdate={isDownloadUpdate}");
                    // For an update flow, we already have explicit shutdown mode.
                    var progressWindow = new UpdateProgressWindow(e.Args);
                    progressWindow.ShowDialog();
                
                    // Shutdown after the update process is complete.
                    Shutdown();
                }
                else
                {
                    Logger.Info("Fluxo normal (MainWindow).");
                    // For the normal flow, create and show the main window.
                    var mainWindow = new MainWindow();
                    this.MainWindow = mainWindow;
                    // Set the shutdown mode back to normal to close the app when the main window is closed.
                    this.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    mainWindow.Show();
                }
            };
            timer.Start();
        }
    }
}
