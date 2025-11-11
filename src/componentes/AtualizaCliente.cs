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

            // Exibir no rodapé preferencialmente a versão INSTALADA do cliente.
            // Se ainda não houver instalada, mostrar a última release conhecida.
            string displayTag = !string.IsNullOrWhiteSpace(installedTag) ? installedTag : latestReleaseTag;
            if (!string.IsNullOrWhiteSpace(displayTag))
            {
                _listener.SetAppVersion(NormalizeTagForDisplay(displayTag));
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
            // Garante a pasta do cliente (ex.: BaseDirectory\\Tibia)
            if (!Directory.Exists(PathHelper.GetLauncherPath(_clientConfig)))
            {
                try { Directory.CreateDirectory(PathHelper.GetLauncherPath(_clientConfig)); } catch { }
            }

            _listener.ShowProgress();

            var token = ProgramaOTLauncher.UpdateConfig.GitHubToken;
            var targetFile = Path.Combine(PathHelper.GetLauncherPath(_clientConfig), "tibia.zip");
            try { Logger.Info($"Iniciando atualização de cliente. Destino do download: {targetFile}; Token presente={ !string.IsNullOrWhiteSpace(token) }"); } catch { }

            try
            {
                // 1) Decide a origem do ZIP
                string requestUrl = null;
                bool useAssetApi = false; // quando verdadeiro, envia Accept: application/octet-stream

                // Prioriza newClientUrl do launcher_config.json, se fornecido
                if (!string.IsNullOrWhiteSpace(_clientConfig?.newClientUrl))
                {
                    requestUrl = _clientConfig.newClientUrl;
                    try { Logger.Info($"Baixando cliente via newClientUrl configurado: {requestUrl}"); } catch { }
                }
                else if (!string.IsNullOrWhiteSpace(token))
                {
                    // Repositório privado: usa API para obter o asset e baixar via endpoint de asset
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", token);

                    var latestResp = await _httpClient.GetAsync(ProgramaOTLauncher.UpdateConfig.ReleasesApiLatest);
                    latestResp.EnsureSuccessStatusCode();
                    var latestJson = await latestResp.Content.ReadAsStringAsync();
                    var latestObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(latestJson);
                    if (latestObj == null || !latestObj.ContainsKey("assets"))
                        throw new Exception("Assets not found in release.");

                    var assets = latestObj["assets"] as JArray;
                    var asset = assets?.FirstOrDefault(a => a["name"]?.ToString() == "client-to-update.zip");
                    if (asset == null)
                        throw new Exception("Asset 'client-to-update.zip' not found in release.");

                    // Para downloads autenticados, preferir a URL da API do asset
                    var assetApiUrl = asset["url"]?.ToString();
                    var browserUrl = asset["browser_download_url"]?.ToString();
                    requestUrl = !string.IsNullOrWhiteSpace(assetApiUrl) ? assetApiUrl : browserUrl;
                    useAssetApi = !string.IsNullOrWhiteSpace(assetApiUrl);
                    try { Logger.Info($"Baixando cliente via {(useAssetApi ? "API privada (asset url)" : "browser_download_url")}: {requestUrl}"); } catch { }
                }
                else
                {
                    // Repositório público
                    requestUrl = ProgramaOTLauncher.UpdateConfig.AssetClientZipLatestPublic;
                    try { Logger.Info($"Baixando cliente via URL pública: {requestUrl}"); } catch { }
                }

                // 2) Baixa o arquivo com HttpClient e progresso
                await DownloadZipStreamAsync(requestUrl, targetFile, useAssetApi);

                // 3) Continua o fluxo como se o WebClient tivesse concluído
                var e = new AsyncCompletedEventArgs(null, false, null);
                await Client_DownloadFileCompleted(this, e, targetFile);
            }
            catch (Exception ex)
            {
                try { Logger.Error("Falha no download do cliente", ex); } catch { }
                System.Windows.MessageBox.Show($"Failed to download client: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task DownloadZipStreamAsync(string requestUrl, string destPath, bool assetApi)
        {
            // Configura cabeçalhos padrões
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("programaot-launcher");
            var token = ProgramaOTLauncher.UpdateConfig.GitHubToken;
            if (!string.IsNullOrWhiteSpace(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", token);
            }

            var req = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            if (assetApi && !string.IsNullOrWhiteSpace(token))
            {
                // Quando usando endpoint de asset da API do GitHub, solicitar o stream binário
                req.Headers.Accept.ParseAdd("application/octet-stream");
            }

            using (var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead))
            {
                resp.EnsureSuccessStatusCode();

                var contentLength = resp.Content.Headers.ContentLength ?? -1L;
                long totalRead = 0;
                var buffer = new byte[81920];

                using (var input = await resp.Content.ReadAsStreamAsync())
                using (var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    int read;
                    while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fs.WriteAsync(buffer, 0, read);
                        totalRead += read;
                        if (contentLength > 0)
                        {
                            var pct = (int)Math.Round(totalRead * 100.0 / contentLength);
                            _listener.SetDownloadPercentage(Math.Max(0, Math.Min(100, pct)));
                            _listener.SetDownloadStatus($"Baixando... {SizeSuffix(totalRead)} / {SizeSuffix(contentLength)}");
                        }
                        else
                        {
                            _listener.SetDownloadStatus($"Baixando... {SizeSuffix(totalRead)}");
                        }
                    }
                }
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
                try { Logger.Info("Extraindo pacote do cliente com progresso e substituição seletiva..."); } catch { }
                // Executa extração em thread de fundo, atualizando UI via Dispatcher
                await Task.Run(() => ExtractZipWithProgressSelective(filePath, PathHelper.GetLauncherPath(_clientConfig)));

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

        private void ExtractZipWithProgressSelective(string zipPath, string extractPath)
        {
            using (ZipFile zip = ZipFile.Read(zipPath))
            {
                int total = zip.Entries.Count;
                int processed = 0;

                // Mensagem inicial
                try
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        _listener.SetDownloadStatus("Preparando para aplicar atualização...");
                        _listener.SetDownloadPercentage(0);
                    });
                }
                catch { }

                foreach (var entry in zip.Entries)
                {
                    bool skipped = false;
                    try
                    {
                        // Caminho alvo do arquivo/diretório
                        var relativePath = entry.FileName.Replace('/', System.IO.Path.DirectorySeparatorChar);
                        var targetPath = System.IO.Path.Combine(extractPath, relativePath);

                        if (entry.IsDirectory)
                        {
                            // Garante o diretório
                            try { System.IO.Directory.CreateDirectory(targetPath); } catch { }
                        }
                        else
                        {
                            // Se o arquivo existir e o tamanho for igual ao tamanho descompactado do ZIP, não substituir
                            try
                            {
                                if (System.IO.File.Exists(targetPath))
                                {
                                    var fi = new System.IO.FileInfo(targetPath);
                                    long existingSize = fi.Length;
                                    long zipUncompressedSize = (long)entry.UncompressedSize;
                                    if (existingSize == zipUncompressedSize)
                                    {
                                        skipped = true; // não altera
                                    }
                                }
                            }
                            catch { }

                            if (!skipped)
                            {
                                // Extrai com overwrite silencioso
                                entry.Extract(extractPath, ExtractExistingFileAction.OverwriteSilently);
                            }
                        }
                    }
                    catch (Exception exEntry)
                    {
                        try { Logger.Error($"Falha ao processar entrada do ZIP: {entry.FileName}", exEntry); } catch { }
                        // Continua com as próximas entradas
                    }
                    finally
                    {
                        processed++;
                        int pct = Math.Min(100, (int)Math.Round(processed * 100.0 / Math.Max(1, total)));
                        try
                        {
                            Application.Current?.Dispatcher?.Invoke(() =>
                            {
                                _listener.SetDownloadPercentage(pct);
                                _listener.SetDownloadStatus($"Aplicando atualização: {processed}/{total} - {entry.FileName} {(skipped ? "(sem alterações)" : "")}");
                            });
                        }
                        catch { }
                    }
                }

                // Mensagem final
                try
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        _listener.SetDownloadPercentage(100);
                        _listener.SetDownloadStatus("Finalizando atualização...");
                    });
                }
                catch { }
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
