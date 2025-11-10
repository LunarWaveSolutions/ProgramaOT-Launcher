using System;
using System.Windows;
using System.IO;
using System.Net;
using System.Windows.Threading;
using System.Net.Http;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Diagnostics;
using Newtonsoft.Json;
using LauncherConfig;

namespace ProgramaOTLauncher
{
    public partial class SplashScreen : Window
    {
        static string launcerConfigUrl = ProgramaOTLauncher.UpdateConfig.RawLauncherConfigUrl;
        // Load informations of launcher_config.json file
        static ClientConfig clientConfig = ClientConfig.loadFromFile(launcerConfigUrl);

		static string clientExecutableName = clientConfig.clientExecutable;

		static readonly HttpClient httpClient = new HttpClient();
		DispatcherTimer timer = new DispatcherTimer();

		private string GetLauncherPath(bool onlyBaseDirectory = false)
		{
			string launcherPath = "";
			if (string.IsNullOrEmpty(clientConfig.clientFolder) || onlyBaseDirectory) {
				launcherPath = AppDomain.CurrentDomain.BaseDirectory.ToString();
			} else {
				launcherPath = AppDomain.CurrentDomain.BaseDirectory.ToString() + "/" + clientConfig.clientFolder;
			}

			return launcherPath;
		}



		public SplashScreen()
		{
			// Always show launcher first; do not auto-start client based on versions
			InitializeComponent();
			timer.Tick += new EventHandler(timer_SplashScreen);
			// Short splash duration
			timer.Interval = new TimeSpan(0, 0, 1);
			timer.Start();
		}

		public void timer_SplashScreen(object sender, EventArgs e)
		{
			if (!Directory.Exists(GetLauncherPath()))
			{
				Directory.CreateDirectory(GetLauncherPath());
			}
			MainWindow mainWindow = new MainWindow();
			this.Close();
			mainWindow.Show();
			timer.Stop();
		}
	}
}

