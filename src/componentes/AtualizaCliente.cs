using Ionic.Zip;
using LauncherConfig;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProgramaOTLauncher.componentes;
using ProgramaOTLauncher;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ProgramaOTLauncher.componentes
{
    public class AtualizaCliente
    {
        private readonly IClientUpdateListener _listener;
        private readonly ClientConfig _clientConfig;
        private readonly HttpClient _httpClient;

        public AtualizaCliente(IClientUpdateListener listener, ClientConfig clientConfig)
        {
            _listener = listener;
            _clientConfig = clientConfig;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("programaot-launcher");
        }

        public bool NeedUpdate { get; private set; }

        public async Task CheckForUpdateAsync()
        {
            try { Logger.Info("Verificando atualização do cliente..."); } catch { }
            string installedTag = GetInstalledClientTag();
            string latestReleaseTag = await GetLatestReleaseTagAsync();
            try { Logger.Info($"Cliente instalado tag={installedTag}; último release tag={latestReleaseTag}"); } catch { }

            if (!string.IsNullOrWhiteSpace(latestReleaseTag))
            {
                _listener.SetAppVersion(NormalizeTagForDisplay(latestReleaseTag));
            }

            bool isClientFolderPresent = Directory.Exists(PathHelper.GetLauncherPath(_clientConfig)) &&
                (Directory.GetFiles(PathHelper.GetLauncherPath(_clientConfig)).Length > 0 || Directory.GetDirectories(PathHelper.GetLauncherPath(_clientConfig)).Length > 0);

            if (!isClientFolderPresent)
            {
                _listener.ShowDownloadButton();
                NeedUpdate = true;
            }
            else
            {
                string installedForCompare = installedTag;
                if (string.IsNullOrWhiteSpace(installedForCompare))
                {
                    installedForCompare = GetClientVersion(AppDomain.CurrentDomain.BaseDirectory);
                }

                bool hasLatest = !string.IsNullOrWhiteSpace(latestReleaseTag);
                bool hasInstalled = !string.IsNullOrWhiteSpace(installedForCompare);
                bool upToDate = false;

                if (hasLatest && hasInstalled)
                {
                    upToDate = string.Equals(CleanTag(latestReleaseTag), CleanTag(installedForCompare), StringComparison.OrdinalIgnoreCase);
                }
                else if (hasInstalled && !hasLatest)
                {
                    upToDate = true;
                }

                if (upToDate)
                {
                    try { Logger.Info("Cliente está atualizado. Exibindo botão Play."); } catch { }
                    _listener.ShowPlayButton();
                    NeedUpdate = false;
                    if (string.IsNullOrWhiteSpace(installedTag) && !string.IsNullOrWhiteSpace(latestReleaseTag))
                    {
                        try { SaveInstalledClientTag(latestReleaseTag); } catch { }
                    }
                }
                else
                {
                    try { Logger.Info("Cliente desatualizado ou ausente. Exibindo botão Update."); } catch { }
                    _listener.ShowUpdateButton();
                    NeedUpdate = true;
                }
            }
        }

        public async Task HandlePlayButtonClickAsync()
        {
            if (NeedUpdate || !Directory.Exists(PathHelper.GetLauncherPath(_clientConfig)))
            {
                await TriggerUpdateAsync();
            }
            else
            {
                LaunchClient();
            }
        }

        private void LaunchClient()
        {
            try
            {
                var exePath = Path.Combine(PathHelper.GetLauncherPath(_clientConfig), "bin", _clientConfig.clientExecutable);
                try { Logger.Info($"Lançando cliente: {exePath}"); } catch { }
                Process.Start(exePath);
                _listener.CloseWindow();
            }
            catch (Exception ex)
            {
                try { Logger.Error("Falha ao iniciar cliente", ex); } catch { }
                System.Windows.MessageBox.Show($"Failed to launch client: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task UpdateClientAsync()
        {
            if (!Directory.Exists(PathHelper.GetLauncherPath(_clientConfig, true)))
            {
                Directory.CreateDirectory(PathHelper.GetLauncherPath(_clientConfig));
            }

            _listener.ShowProgress();

            var token = ProgramaOTLauncher.UpdateConfig.GitHubToken;
            var targetFile = Path.Combine(PathHelper.GetLauncherPath(_clientConfig), "tibia.zip");
            try { Logger.Info($"Iniciando atualização de cliente. Destino do download: {targetFile}; Token presente={ !string.IsNullOrWhiteSpace(token) }"); } catch { }

            try
            {
                if (!string.IsNullOrWhiteSpace(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", token);

                    var latestResp = await _httpClient.GetAsync(ProgramaOTLauncher.UpdateConfig.ReleasesApiLatest);
                    latestResp.EnsureSuccessStatusCode();
                    var latestJson = await latestResp.Content.ReadAsStringAsync();
                    var latestObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(latestJson);
                    if (latestObj == null || !latestObj.ContainsKey("assets"))
                        throw new Exception("Assets not found in release.");

                    var assets = latestObj["assets"] as Newtonsoft.Json.Linq.JArray;
                    var asset = assets?.FirstOrDefault(a => a["name"]?.ToString() == "client-to-update.zip");
                    if (asset == null)
                        throw new Exception("Asset 'client-to-update.zip' not found in release.");

                    var assetUrl = asset["browser_download_url"].ToString();
                    try { Logger.Info($"Baixando cliente via API privada: {assetUrl}"); } catch { }
                    
                    using (var webClient = new System.Net.WebClient())
                    {
                        webClient.DownloadProgressChanged += Client_DownloadProgressChanged;
                        webClient.DownloadFileCompleted += async (s, e) => await Client_DownloadFileCompleted(s, e, targetFile);
                        await webClient.DownloadFileTaskAsync(new Uri(assetUrl), targetFile);
                    }
                }
                else
                {
                    string latestZipUrl = ProgramaOTLauncher.UpdateConfig.AssetClientZipLatestPublic;
                    try { Logger.Info($"Baixando cliente via URL pública: {latestZipUrl}"); } catch { }
                    using (var webClient = new System.Net.WebClient())
                    {
                        webClient.DownloadProgressChanged += Client_DownloadProgressChanged;
                        webClient.DownloadFileCompleted += async (s, e) => await Client_DownloadFileCompleted(s, e, targetFile);
                        await webClient.DownloadFileTaskAsync(new Uri(latestZipUrl), targetFile);
                    }
                }
            }
            catch (Exception ex)
            {
                try { Logger.Error("Falha no download do cliente", ex); } catch { }
                System.Windows.MessageBox.Show($"Failed to download client: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void Client_DownloadProgressChanged(object sender, System.Net.DownloadProgressChangedEventArgs e)
        {
            _listener.SetDownloadPercentage(e.ProgressPercentage);
            if (e.ProgressPercentage == 100)
            {
                _listener.SetDownloadStatus("Finishing, wait...");
            }
            else
            {
                _listener.SetDownloadStatus(SizeSuffix(e.BytesReceived) + " / " + SizeSuffix(e.TotalBytesToReceive));
            }
        }

        private async Task Client_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e, string filePath)
        {
            if (e.Error != null)
            {
                try { Logger.Error("Erro reportado pelo WebClient ao baixar cliente", e.Error); } catch { }
                System.Windows.MessageBox.Show($"Failed to download client: {e.Error.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            try
            {
                try { Logger.Info("Extraindo pacote do cliente..."); } catch { }
                await Task.Run(() => ExtractZip(filePath, PathHelper.GetLauncherPath(_clientConfig)));

                string latestReleaseTag = await GetLatestReleaseTagAsync();
                if (!string.IsNullOrWhiteSpace(latestReleaseTag))
                {
                    SaveInstalledClientTag(latestReleaseTag);
                }

                _listener.HideProgress();
                await CheckForUpdateAsync();
                try { Logger.Info("Atualização do cliente concluída."); } catch { }
            }
            catch (Exception ex)
            {
                try { Logger.Error("Falha ao extrair cliente", ex); } catch { }
                System.Windows.MessageBox.Show($"Failed to extract client: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        private void ExtractZip(string zipPath, string extractPath)
        {
            using (ZipFile zip = ZipFile.Read(zipPath))
            {
                zip.ExtractAll(extractPath, ExtractExistingFileAction.OverwriteSilently);
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

        public async Task TriggerUpdateAsync()
        {
            await UpdateClientAsync();
        }

        // Métodos auxiliares para normalização de tags, etc.
        private string NormalizeTagForDisplay(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return "";
            var t = tag.Trim();
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

        private string CleanTag(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return "";
            t = t.Trim();
            if (t.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                t = t.Substring(1);
            return t;
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

        private string VersionsJsonPath()
		{
			return Path.Combine(PathHelper.GetLauncherPath(_clientConfig), "versions.json");
		}

		private string GetInstalledClientTag()
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

		private void SaveInstalledClientTag(string tag)
		{
			try
			{
				Directory.CreateDirectory(PathHelper.GetLauncherPath(_clientConfig));
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
                var token = ProgramaOTLauncher.UpdateConfig.GitHubToken;
                if (!string.IsNullOrWhiteSpace(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", token);
                }
                var url = ProgramaOTLauncher.UpdateConfig.ReleasesApiLatest;
                var response = await _httpClient.GetAsync(url);
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
    }
}
