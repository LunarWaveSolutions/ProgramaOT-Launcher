using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;
using Ionic.Zip;
using LauncherConfig;

namespace ProgramaOTLauncher
{
    public partial class MainWindow : Window
    {
        // URL global do launcher_config.json (pode ser repo privado se houver token)
        static string launcerConfigUrl = ProgramaOTLauncher.UpdateConfig.RawLauncherConfigUrl;
        // Load infos do launcher_config.json (pastas/executável etc). Não usa versão remota para decidir update.
        static ClientConfig clientConfig = ClientConfig.loadFromFile(launcerConfigUrl);

		static string clientExecutableName = clientConfig.clientExecutable;
		static string programVersion = clientConfig.launcherVersion;

		// Always fetch latest release tag from GitHub, compare with locally installed tag stored in versions.json
		string latestReleaseTag = "";
		bool clientDownloaded = false;
		bool needUpdate = false;

        static readonly HttpClient httpClient = new HttpClient();
		WebClient webClient = new WebClient();

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

		public MainWindow()
		{
			InitializeComponent();
		}

		static void CreateShortcut()
		{
			string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
			string shortcutPath = Path.Combine(desktopPath, clientConfig.clientFolder + ".lnk");
			Type t = Type.GetTypeFromProgID("WScript.Shell");
			dynamic shell = Activator.CreateInstance(t);
			var lnk = shell.CreateShortcut(shortcutPath);
			try
			{
				lnk.TargetPath = Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");
				lnk.Description = clientConfig.clientFolder;
				lnk.Save();
			}
			finally
			{
				System.Runtime.InteropServices.Marshal.FinalReleaseComObject(lnk);
			}
		}

		private async void TibiaLauncher_Load(object sender, RoutedEventArgs e)
		{
			ImageLogoServer.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/logo.png"));
			ImageLogoCompany.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/logo_company.png"));

			progressbarDownload.Visibility = Visibility.Collapsed;
			labelClientVersion.Visibility = Visibility.Collapsed;
			labelDownloadPercent.Visibility = Visibility.Collapsed;
			labelVersion.Text = "v" + programVersion;

			string installedTag = GetInstalledTag();
			latestReleaseTag = await GetLatestReleaseTagAsync();

			bool isClientFolderPresent = Directory.Exists(GetLauncherPath()) &&
				(Directory.GetFiles(GetLauncherPath()).Length > 0 || Directory.GetDirectories(GetLauncherPath()).Length > 0);

			if (!isClientFolderPresent)
			{
				// No client installed: prompt Download
				buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_update.png")));
				buttonPlayIcon.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/icon_update.png"));
				labelClientVersion.Content = "Download";
				labelClientVersion.Visibility = Visibility.Visible;
				buttonPlay.Visibility = Visibility.Visible;
				buttonPlay_tooltip.Text = "Download";
				needUpdate = true;
			}
			else
			{
				// Client folder exists: compare installed tag vs latest tag
				if (!string.IsNullOrEmpty(latestReleaseTag) && !string.IsNullOrEmpty(installedTag) && latestReleaseTag == installedTag)
				{
					buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_play.png")));
					buttonPlayIcon.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/icon_play.png"));
					buttonPlay_tooltip.Text = "Play";
					needUpdate = false;
				}
				else
				{
					buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_update.png")));
					buttonPlayIcon.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/icon_update.png"));
					labelClientVersion.Content = "Update";
					labelClientVersion.Visibility = Visibility.Visible;
					buttonPlay.Visibility = Visibility.Visible;
					buttonPlay_tooltip.Text = "Update";
					needUpdate = true;
				}
			}
		}

		static string GetClientVersion(string path)
		{
			try
			{
				string json = Path.Combine(path, "launcher_config.json");
				if (!File.Exists(json))
				{
					return "";
				}

				using (StreamReader stream = new StreamReader(json))
				{
					var jsonString = stream.ReadToEnd();
					// Read by property name to avoid dependency on the first key order
					var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
					if (obj != null && obj.ContainsKey("clientVersion") && obj["clientVersion"] != null)
					{
						return obj["clientVersion"].ToString();
					}
				}

				return "";
			}
			catch
			{
				return "";
			}
		}

		// versions.json management
		private string VersionsJsonPath()
		{
			return Path.Combine(GetLauncherPath(), "versions.json");
		}

		private string GetInstalledTag()
		{
			try
			{
				string path = VersionsJsonPath();
				if (!File.Exists(path)) return "";
				var text = File.ReadAllText(path);
				var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(text);
				if (obj != null && obj.ContainsKey("installedTag") && obj["installedTag"] != null)
				{
					return obj["installedTag"].ToString();
				}
			}
			catch { }
			return "";
		}

		private void SaveInstalledTag(string tag)
		{
			try
			{
				Directory.CreateDirectory(GetLauncherPath());
				var payload = new Dictionary<string, object>
				{
					{"installedTag", tag},
					{"installedAt", DateTime.UtcNow.ToString("o")}
				};
				File.WriteAllText(VersionsJsonPath(), JsonConvert.SerializeObject(payload, Formatting.Indented));
			}
			catch { }
		}

        private async Task<string> GetLatestReleaseTagAsync()
        {
            try
            {
                // GitHub API requires a User-Agent header
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("programaot-launcher");
                // Adiciona Authorization se existir token (acesso a repo privado)
                var token = ProgramaOTLauncher.UpdateConfig.GitHubToken;
                if (!string.IsNullOrWhiteSpace(token))
                {
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", token);
                }
                var url = ProgramaOTLauncher.UpdateConfig.ReleasesApiLatest;
                var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return "";
                var json = await response.Content.ReadAsStringAsync();
                var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (obj != null && obj.ContainsKey("tag_name"))
                {
                    return obj["tag_name"].ToString();
                }
            }
            catch { }
            return "";
        }

		private void AddReadOnly()
		{
			// If the files "eventschedule/boostedcreature/onlinenumbers" exist, set them as read-only
			string eventSchedulePath = GetLauncherPath() + "/cache/eventschedule.json";
			if (File.Exists(eventSchedulePath)) {
				File.SetAttributes(eventSchedulePath, FileAttributes.ReadOnly);
			}
			string boostedCreaturePath = GetLauncherPath() + "/cache/boostedcreature.json";
			if (File.Exists(boostedCreaturePath)) {
				File.SetAttributes(boostedCreaturePath, FileAttributes.ReadOnly);
			}
			string onlineNumbersPath = GetLauncherPath() + "/cache/onlinenumbers.json";
			if (File.Exists(onlineNumbersPath)) {
				File.SetAttributes(onlineNumbersPath, FileAttributes.ReadOnly);
			}
		}

        private async void UpdateClient()
        {
            if (!Directory.Exists(GetLauncherPath(true)))
            {
                Directory.CreateDirectory(GetLauncherPath());
            }
			labelDownloadPercent.Visibility = Visibility.Visible;
			progressbarDownload.Visibility = Visibility.Visible;
			labelClientVersion.Visibility = Visibility.Collapsed;
			buttonPlay.Visibility = Visibility.Collapsed;
			webClient.DownloadProgressChanged += Client_DownloadProgressChanged;
			webClient.DownloadFileCompleted += Client_DownloadFileCompleted;
            // Baixa o ZIP da última release
            // Se houver token, usa o endpoint de asset com Authorization (repo privado); caso contrário, usa a URL pública
            var token = ProgramaOTLauncher.UpdateConfig.GitHubToken;
            var targetFile = Path.Combine(GetLauncherPath(), "tibia.zip");

            if (!string.IsNullOrWhiteSpace(token))
            {
                try
                {
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("programaot-launcher");
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", token);

                    // 1) Obter release mais recente
                    var latestResp = await httpClient.GetAsync(ProgramaOTLauncher.UpdateConfig.ReleasesApiLatest);
                    latestResp.EnsureSuccessStatusCode();
                    var latestJson = await latestResp.Content.ReadAsStringAsync();
                    var latestObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(latestJson);
                    if (latestObj == null || !latestObj.ContainsKey("assets"))
                        throw new Exception("Assets não encontrados na release.");

                    var assets = latestObj["assets"] as Newtonsoft.Json.Linq.JArray;
                    var asset = assets?.FirstOrDefault(a => a["name"]?.ToString() == "client-to-update.zip");
                    if (asset == null)
                        throw new Exception("Asset 'client-to-update.zip' não encontrado na release.");

                    var assetId = asset["id"].ToObject<int>();
                    // 2) Baixar asset via API (evita problemas de redirect sem header Authorization)
                    var assetUrl = $"https://api.github.com/repos/{ProgramaOTLauncher.UpdateConfig.Owner}/{ProgramaOTLauncher.UpdateConfig.Repo}/releases/assets/{assetId}";
                    var req = new HttpRequestMessage(HttpMethod.Get, assetUrl);
                    req.Headers.Accept.ParseAdd("application/octet-stream");
                    var assetResp = await httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                    assetResp.EnsureSuccessStatusCode();
                    using (var fs = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await assetResp.Content.CopyToAsync(fs);
                    }
                    // dispara evento de concluído manualmente
                    Client_DownloadFileCompleted(this, new System.ComponentModel.AsyncCompletedEventArgs(null, false, null));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Falha ao baixar cliente privado: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                // Repositório público: usa URL padrão de download
                string latestZipUrl = ProgramaOTLauncher.UpdateConfig.AssetClientZipLatestPublic;
                webClient.DownloadFileAsync(new Uri(latestZipUrl), targetFile);
            }
        }

		private void buttonPlay_Click(object sender, RoutedEventArgs e)
		{
			if (needUpdate == true || !Directory.Exists(GetLauncherPath()))
			{
				try
				{
					UpdateClient();
				}
				catch (Exception ex)
				{
					labelVersion.Text = ex.ToString();
				}
			}
			else
			{
				if (clientDownloaded == true || !Directory.Exists(GetLauncherPath(true)))
				{
					Process.Start(GetLauncherPath() + "/bin/" + clientExecutableName);
					this.Close();
				}
				else
				{
					try
					{
						UpdateClient();
					}
					catch (Exception ex)
					{
						labelVersion.Text = ex.ToString();
					}
				}
			}
		}

		private void ExtractZip(string path, ExtractExistingFileAction existingFileAction)
		{
			using (ZipFile modZip = ZipFile.Read(path))
			{
				foreach (ZipEntry zipEntry in modZip)
				{
					zipEntry.Extract(GetLauncherPath(), existingFileAction);
				}
			}
		}

		private async void Client_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
		{
			buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_play.png")));
			buttonPlayIcon.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/icon_play.png"));

			if (clientConfig.replaceFolders)
			{
				foreach (ReplaceFolderName folderName in clientConfig.replaceFolderName)
				{
					string folderPath = Path.Combine(GetLauncherPath(), folderName.name);
					if (Directory.Exists(folderPath))
					{
						Directory.Delete(folderPath, true);
					}
				}
			}

			// Adds the task to a secondary task to prevent the program from crashing while this is running
			await Task.Run(() =>
			{
				Directory.CreateDirectory(GetLauncherPath());
				ExtractZip(GetLauncherPath() + "/tibia.zip", ExtractExistingFileAction.OverwriteSilently);
				File.Delete(GetLauncherPath() + "/tibia.zip");
			});
			progressbarDownload.Value = 100;

			// Download launcher_config.json from url to the launcher path
			WebClient webClient = new WebClient();
			string localPath = Path.Combine(GetLauncherPath(true), "launcher_config.json");
			webClient.DownloadFile(launcerConfigUrl, localPath);

			// Persist installed latest tag
			if (string.IsNullOrEmpty(latestReleaseTag))
			{
				// Try retrieving again quickly; if still empty, skip
				latestReleaseTag = await GetLatestReleaseTagAsync();
			}
			if (!string.IsNullOrEmpty(latestReleaseTag))
			{
				SaveInstalledTag(latestReleaseTag);
			}

			AddReadOnly();
			CreateShortcut();

			needUpdate = false;
			clientDownloaded = true;
			labelClientVersion.Visibility = Visibility.Collapsed;
			buttonPlay.Visibility = Visibility.Visible;
			progressbarDownload.Visibility = Visibility.Collapsed;
			labelDownloadPercent.Visibility = Visibility.Collapsed;
		}

		private void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
			progressbarDownload.Value = e.ProgressPercentage;
			if (progressbarDownload.Value == 100) {
				labelDownloadPercent.Content = "Finishing, wait...";
			} else {
				labelDownloadPercent.Content = SizeSuffix(e.BytesReceived) + " / " + SizeSuffix(e.TotalBytesToReceive);
			}
		}

		static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
		static string SizeSuffix(Int64 value, int decimalPlaces = 1)
		{
			if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
			if (value < 0) { return "-" + SizeSuffix(-value, decimalPlaces); }
			if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

			int mag = (int)Math.Log(value, 1024);
			decimal adjustedSize = (decimal)value / (1L << (mag * 10));

			if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
			{
				mag += 1;
				adjustedSize /= 1024;
			}
			return string.Format("{0:n" + decimalPlaces + "} {1}",
				adjustedSize,
				SizeSuffixes[mag]);
		}

		private void buttonPlay_MouseEnter(object sender, MouseEventArgs e)
		{
			if (needUpdate)
				buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_hover_update.png")));
			else
				buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_hover_play.png")));
		}

		private void buttonPlay_MouseLeave(object sender, MouseEventArgs e)
		{
			if (needUpdate)
				buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_update.png")));
			else
				buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/Assets/button_play.png")));
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void RestoreButton_Click(object sender, RoutedEventArgs e)
		{
			if (ResizeMode != ResizeMode.NoResize)
			{
				if (WindowState == WindowState.Normal)
					WindowState = WindowState.Maximized;
				else
					WindowState = WindowState.Normal;
			}
		}

		private void MinimizeButton_Click(object sender, RoutedEventArgs e)
		{
			WindowState = WindowState.Minimized;
		}

		// Open Discord link from Hyperlink in TextBlock
		private void Discord_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = e.Uri.AbsoluteUri,
					UseShellExecute = true
				});
				e.Handled = true;
			}
			catch (Exception ex)
			{
				labelVersion.Text = ex.Message;
			}
		}

	}
}

