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

        // Estado de update do Launcher para UI (ícone ao lado da versão)
        private LauncherUpdateInfo _pendingLauncherUpdate = null;

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

        // ===== Persistência da versão/tag do Launcher instalada =====
        private string LaunchVersionsJsonPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launchversions.json");
        }

        private string GetInstalledLauncherTag()
        {
            try
            {
                var path = LaunchVersionsJsonPath();
                if (!File.Exists(path)) return "";
                var text = File.ReadAllText(path);
                var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(text);
                if (obj != null && obj.ContainsKey("installedLauncherTag") && obj["installedLauncherTag"] != null)
                {
                    return obj["installedLauncherTag"].ToString();
                }
            }
            catch { }
            return "";
        }

        private void SaveInstalledLauncherTag(string tag)
        {
            try
            {
                var payload = new Dictionary<string, object>
                {
                    {"installedLauncherTag", tag ?? string.Empty},
                    {"installedAt", DateTime.UtcNow.ToString("o")}
                };
                File.WriteAllText(LaunchVersionsJsonPath(), JsonConvert.SerializeObject(payload, Formatting.Indented));
            }
            catch { }
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
            ImageLogoServer.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/assets/logo.png"));
            ImageLogoCompany.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/assets/logo_company.png"));

            progressbarDownload.Visibility = Visibility.Collapsed;
            labelClientVersion.Visibility = Visibility.Collapsed;
            labelDownloadPercent.Visibility = Visibility.Collapsed;
            // Inicialmente mostra a versão configurada; será atualizada após obter a tag da release
            labelVersion.Text = "v" + programVersion;

			// Checagem de auto-update do Launcher
            try
            {
                // Decide versão instalada do Launcher: prefere a persistida em launchversions.json; fallback para clientConfig.launcherVersion
                var installedLauncherTag = GetInstalledLauncherTag();
                var versionForCompare = string.IsNullOrWhiteSpace(installedLauncherTag) ? programVersion : installedLauncherTag;
                var luInfo = await LauncherUpdateService.CheckAsync(clientConfig, versionForCompare);
                // Atualiza UI do ícone ao lado da versão
                if (luInfo.HasUpdate)
                {
                    _pendingLauncherUpdate = luInfo;
                    buttonLauncherUpdate.Visibility = Visibility.Visible;
                    var ttUpdate = buttonLauncherUpdate.ToolTip as System.Windows.Controls.ToolTip;
                    string ttText = string.IsNullOrWhiteSpace(luInfo.LatestTag)
                        ? "Atualizar Launcher"
                        : $"Atualizar Launcher (última: {luInfo.LatestTag})";
                    if (ttUpdate != null)
                        ttUpdate.Content = ttText;
                    else
                        buttonLauncherUpdate.ToolTip = ttText;

					if (luInfo.Mandatory)
					{
						// Atualização obrigatória: executa sem perguntar
						await UpdateLauncherAsync(luInfo);
						return; // o app será encerrado para permitir a substituição
					}
				}
				else
				{
					_pendingLauncherUpdate = null;
					buttonLauncherUpdate.Visibility = Visibility.Collapsed;
				}
			}
			catch { }

            string installedTag = GetInstalledTag();
            latestReleaseTag = await GetLatestReleaseTagAsync();
            // Atualiza o label da versão para refletir a versão da release do site (Git)
            if (!string.IsNullOrWhiteSpace(latestReleaseTag))
            {
                labelVersion.Text = NormalizeTagForDisplay(latestReleaseTag);
            }

			bool isClientFolderPresent = Directory.Exists(GetLauncherPath()) &&
				(Directory.GetFiles(GetLauncherPath()).Length > 0 || Directory.GetDirectories(GetLauncherPath()).Length > 0);

			if (!isClientFolderPresent)
			{
				// No client installed: prompt Download
				buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/assets/button_update.png")));
				buttonPlayIcon.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/assets/icon_update.png"));
				labelClientVersion.Content = "Download";
				labelClientVersion.Visibility = Visibility.Visible;
				buttonPlay.Visibility = Visibility.Visible;
				buttonPlay_tooltip.Text = "Download";
				needUpdate = true;
			}
            else
            {
                // Client folder exists: compare installed tag vs latest tag
                string installedForCompare = installedTag;
                if (string.IsNullOrWhiteSpace(installedForCompare))
                {
                    // Fallback: usa versão local do cliente no launcher_config.json (base directory)
                    installedForCompare = GetClientVersion(AppDomain.CurrentDomain.BaseDirectory);
                }

                bool hasLatest = !string.IsNullOrWhiteSpace(latestReleaseTag);
                bool hasInstalled = !string.IsNullOrWhiteSpace(installedForCompare);
                bool upToDate = false;

                if (hasLatest && hasInstalled)
                {
                    // Compara removendo prefixo "v"
                    upToDate = string.Equals(CleanTag(latestReleaseTag), CleanTag(installedForCompare), StringComparison.OrdinalIgnoreCase);
                }
                else if (hasInstalled && !hasLatest)
                {
                    // Não conseguiu obter a última release; se há versão instalada conhecida, assume up-to-date para evitar re-download indevido
                    upToDate = true;
                }

                if (upToDate)
                {
                    buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/assets/button_play.png")));
                    buttonPlayIcon.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/assets/icon_play.png"));
                    buttonPlay_tooltip.Text = "Play";
                    needUpdate = false;
                    // Se está atualizado e ainda não temos versions.json, persistir a tag para as próximas execuções
                    if (string.IsNullOrWhiteSpace(installedTag) && !string.IsNullOrWhiteSpace(latestReleaseTag))
                    {
                        try { SaveInstalledTag(latestReleaseTag); } catch { }
                    }
                }
                else
                {
                    buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/assets/button_update.png")));
                    buttonPlayIcon.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/assets/icon_update.png"));
                    labelClientVersion.Content = "Update";
                    labelClientVersion.Visibility = Visibility.Visible;
                    buttonPlay.Visibility = Visibility.Visible;
                    buttonPlay_tooltip.Text = "Update";
                    needUpdate = true;
                }
            }
		}

        // Clique no ícone de update do Launcher ao lado do texto de versão
        private async void buttonLauncherUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingLauncherUpdate != null && _pendingLauncherUpdate.HasUpdate)
            {
                // Para atualizações opcionais, perguntar antes de iniciar
                if (!_pendingLauncherUpdate.Mandatory)
                {
                    string msg = "Uma atualização do Launcher está disponível.";
                    if (!string.IsNullOrWhiteSpace(_pendingLauncherUpdate.LatestTag))
                        msg += $"\nÚltima versão/tag: {_pendingLauncherUpdate.LatestTag}";
                    msg += "\n\nDeseja iniciar a atualização agora?";
                    var result = MessageBox.Show(msg, "Atualização do Launcher", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result != MessageBoxResult.Yes)
                        return;
                }

                await UpdateLauncherAsync(_pendingLauncherUpdate);
                // O app será encerrado pelo Updater
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

        // Exibe a tag com prefixo "v" quando necessário, sem duplicar prefixo
        private string NormalizeTagForDisplay(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return "";
            var t = tag.Trim();
            // Se parecer SemVer numérico, prefixa com "v"; caso contrário, exibe como está (ex.: auto-20250101-1230)
            if (LooksLikeNumericVersion(t))
            {
                if (t.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                    return t;
                return "v" + t;
            }
            return t;
        }

        private bool LooksLikeNumericVersion(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return false;
            var t = CleanTag(tag);
            foreach (var c in t)
            {
                if (!(char.IsDigit(c) || c == '.')) return false;
            }
            return t.Length > 0;
        }

        // Remove prefixo "v" para comparar versões sem diferença de formato
        private string CleanTag(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return "";
            t = t.Trim();
            if (t.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                t = t.Substring(1);
            return t;
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
			// Se houver atualização de Launcher pendente, bloquear atualização do Client e orientar usuário
			if (_pendingLauncherUpdate != null && _pendingLauncherUpdate.HasUpdate)
			{
				MessageBox.Show(
					"Uma atualização do Launcher está disponível.\n\nAtualize o Launcher antes de atualizar o cliente.",
					"Atualização necessária",
					MessageBoxButton.OK,
					MessageBoxImage.Information
				);
				// Abre um balão no ícone de atualização do Launcher para guiar o usuário
				try
				{
					var tt = buttonLauncherUpdate.ToolTip as System.Windows.Controls.ToolTip;
					if (tt != null)
					{
						System.Windows.Controls.ToolTipService.SetInitialShowDelay(buttonLauncherUpdate, 0);
						System.Windows.Controls.ToolTipService.SetShowDuration(buttonLauncherUpdate, 5000);
						tt.IsOpen = true;
					}
				}
				catch { }
				return;
			}
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
            buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/assets/button_play.png")));
            buttonPlayIcon.Source = new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/assets/icon_play.png"));

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

            // Download launcher_config.json do repositório para a pasta base do launcher (fallback local)
            WebClient webClient = new WebClient();
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher_config.json");
            try
            {
                webClient.DownloadFile(launcerConfigUrl, localPath);
            }
            catch { }

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
            buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/assets/button_hover_update.png")));
			else
            buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/assets/button_hover_play.png")));
		}

		private void buttonPlay_MouseLeave(object sender, MouseEventArgs e)
		{
			if (needUpdate)
            buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/assets/button_update.png")));
			else
            buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/assets/button_play.png")));
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

		// ===== Atualização do Launcher =====
        private async Task UpdateLauncherAsync(LauncherUpdateInfo luInfo)
        {
            try
            {
                if (luInfo == null || string.IsNullOrWhiteSpace(luInfo.AssetUrl))
                {
                    MessageBox.Show("Não foi possível localizar o pacote de atualização do Launcher.", "Atualização do Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

				// Pasta temporária
				string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ProgramaOTLauncherUpdate");
				Directory.CreateDirectory(tempDir);
				string zipPath = System.IO.Path.Combine(tempDir, "launcher-update.zip");

				// Download do ZIP
				try
				{
					// Usa HttpClient com Authorization se houver token
					httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("programaot-launcher");
					var token = ProgramaOTLauncher.UpdateConfig.GitHubToken;
					if (!string.IsNullOrWhiteSpace(token))
					{
						httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", token);
					}
					// Se houver AssetApiUrl (privado), usa-o com Accept: application/octet-stream; senão usa browser_download_url
					HttpResponseMessage resp;
					if (!string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(luInfo.AssetApiUrl))
					{
						var req = new HttpRequestMessage(HttpMethod.Get, luInfo.AssetApiUrl);
						req.Headers.Accept.ParseAdd("application/octet-stream");
						resp = await httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
					}
					else
					{
						resp = await httpClient.GetAsync(luInfo.AssetUrl);
					}
					resp.EnsureSuccessStatusCode();
					using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
					{
						await resp.Content.CopyToAsync(fs);
					}
				}
				catch (Exception ex)
				{
					MessageBox.Show($"Falha ao baixar atualização do Launcher: {ex.Message}", "Atualização do Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}

				// Validação opcional do checksum
				if (!string.IsNullOrWhiteSpace(clientConfig.launcherChecksumUrl))
				{
					try
					{
						var checksumTxt = await httpClient.GetStringAsync(clientConfig.launcherChecksumUrl);
						var expected = checksumTxt.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[0];
						var actual = ComputeSha256(zipPath);
						if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
						{
							MessageBox.Show("Checksum do pacote de atualização não confere. Atualização abortada.", "Atualização do Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
							return;
						}
					}
					catch { /* Em caso de falha de checksum, segue sem bloquear se não for obrigatório */ }
				}

                // Extrai para tempDir\payload
                string payloadDir = System.IO.Path.Combine(tempDir, "payload");
                if (Directory.Exists(payloadDir)) Directory.Delete(payloadDir, true);
                Directory.CreateDirectory(payloadDir);
                try
                {
                    using (Ionic.Zip.ZipFile zf = Ionic.Zip.ZipFile.Read(zipPath))
                    {
                        foreach (var entry in zf)
                        {
                            entry.Extract(payloadDir, ExtractExistingFileAction.OverwriteSilently);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Falha ao extrair atualização do Launcher: {ex.Message}", "Atualização do Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Persiste a tag/versão que está sendo instalada antes de iniciar a aplicação da atualização
                if (!string.IsNullOrWhiteSpace(luInfo.LatestTag))
                {
                    SaveInstalledLauncherTag(luInfo.LatestTag);
                }

                // Aplica atualização usando o próprio launcher: inicia a nova versão a partir do payload com a flag --apply-update
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string currentExeName = System.IO.Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
                string newLauncherExePath = System.IO.Path.Combine(payloadDir, currentExeName);

                if (!System.IO.File.Exists(newLauncherExePath))
                {
                    MessageBox.Show("Não foi possível localizar a nova versão do launcher dentro do pacote baixado.", "Atualização do Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                string args = $"--apply-update --source=\"{payloadDir}\" --target=\"{baseDir}\" --exe=\"{currentExeName}\" --waitpid {pid}";

                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = newLauncherExePath,
                        Arguments = args,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Falha ao iniciar a aplicação da atualização: {ex.Message}", "Atualização do Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Encerra o Launcher atual para permitir a substituição dos arquivos
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Atualização do Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

		private static string ComputeSha256(string filePath)
		{
			using (var stream = File.OpenRead(filePath))
			using (var sha = System.Security.Cryptography.SHA256.Create())
			{
				var hash = sha.ComputeHash(stream);
				return string.Concat(hash.Select(b => b.ToString("x2")));
			}
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

