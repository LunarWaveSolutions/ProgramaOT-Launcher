using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Ionic.Zip;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ProgramaOTLauncher
{
    public partial class UpdateProgressWindow : Window
    {
        private readonly string[] _startupArgs;
        // Diretórios e logging
        private string UpdateDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UpdateLauncher");
        private string LogFilePath => Path.Combine(UpdateDir, "update.log");

        private void Log(string msg)
        {
            try
            {
                Directory.CreateDirectory(UpdateDir);
                File.AppendAllText(LogFilePath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            }
            catch
            {
                // Evita exceções de IO interromperem o fluxo
            }
        }

        public UpdateProgressWindow(string[] args)
        {
            InitializeComponent();
            _startupArgs = args;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        { 
            bool hasDownloadUpdate = _startupArgs.Any(a => a.Equals("--download-update", StringComparison.OrdinalIgnoreCase) || a.StartsWith("--download-update", StringComparison.OrdinalIgnoreCase));
            bool hasApplyUpdate = _startupArgs.Any(a => a.Equals("--apply-update", StringComparison.OrdinalIgnoreCase) || a.StartsWith("--apply-update", StringComparison.OrdinalIgnoreCase));

            if (hasDownloadUpdate || hasApplyUpdate)
            {
                await RunFullUpdateProcessAsync();
            }
            else
            {
                SetStatus("Erro: Ação de atualização não especificada.");
                await Task.Delay(3000);
                Application.Current.Shutdown();
            }
        }

        private async Task RunFullUpdateProcessAsync()
        {
            Log("Full update process started.");
            try
            {
                // Coleta de argumentos
                string latestTag = GetArg("--version");
                string assetUrl = GetArg("--url");
                string assetApiUrl = GetArg("--api-url");
                string checksumUrl = GetArg("--checksum-url");

                // Caminhos
                Directory.CreateDirectory(UpdateDir);
                string zipPath = Path.Combine(UpdateDir, "launcher-update.zip");
                string payloadDir = Path.Combine(UpdateDir, "payload");

                // 1) Download
                var usedDownloadUrl = await DownloadUpdateZipAsync(assetUrl, assetApiUrl, zipPath);

                // 2) Checksum
                var expectedChecksum = await ValidateChecksumAsync(zipPath, checksumUrl);

                // 3) Extração
                await ExtractZipWithProgressAsync(zipPath, payloadDir);

                // 4) Aplicação
                // Mostrar claramente a etapa de aplicação antes de iniciar o helper
                SetStatus("Preparando aplicação da atualização...");
                await Task.Delay(4000); // aumenta o tempo para permitir ao usuário visualizar a etapa (4s)
                await LaunchUpdaterHelperAsync(payloadDir, AppDomain.CurrentDomain.BaseDirectory, latestTag, usedDownloadUrl, expectedChecksum);
            }
            catch (Exception ex)
            {
                Log($"FATAL ERROR: {ex}. Prosseguindo sem interromper a aplicação.");
                SetStatus("Erro durante a atualização!", ex.Message);
                // Não encerrar a aplicação automaticamente
            }
        }

        // 1) Download da atualização
        private async Task<string> DownloadUpdateZipAsync(string assetUrl, string assetApiUrl, string zipPath)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    SetStatus("Baixando atualização do launcher...");
                    Log($"Downloading from URL: {assetUrl ?? assetApiUrl}");

                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("programaot-launcher");
                    var token = UpdateConfig.GitHubToken;
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", token);
                    }

                    var requestUrl = !string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(assetApiUrl) ? assetApiUrl : assetUrl;
                    var req = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                    if (!string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(assetApiUrl))
                    {
                        req.Headers.Accept.ParseAdd("application/octet-stream");
                    }

                    var resp = await httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                    resp.EnsureSuccessStatusCode();

                    var contentLength = resp.Content.Headers.ContentLength ?? -1L;
                    long totalRead = 0;
                    var buffer = new byte[81920];

                    using (var input = await resp.Content.ReadAsStreamAsync())
                    using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        int read;
                        while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fs.WriteAsync(buffer, 0, read);
                            totalRead += read;
                            if (contentLength > 0)
                            {
                                var pct = (int)Math.Round(totalRead * 100.0 / contentLength);
                                SetProgress(pct, "Baixando...", $"{SizeSuffix(totalRead)} / {SizeSuffix(contentLength)}");
                            }
                        }
                    }
                    Log("Download completed.");
                    return requestUrl;
                }
            }
            catch (Exception ex)
            {
                Log($"DOWNLOAD FAILED: {ex}");
                return null; // Prossegue mesmo assim
            }
        }

        // 2) Validação de checksum com barra de progresso
        private async Task<string> ValidateChecksumAsync(string zipPath, string checksumUrl)
        {
            if (string.IsNullOrWhiteSpace(checksumUrl))
            {
                Log("ChecksumUrl não fornecido. Prosseguindo sem validação.");
                return null;
            }

            try
            {
                SetStatus("Validando checksum do pacote...");
                Log($"Fetching checksum from: {checksumUrl}");

                using (var http = new HttpClient())
                {
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("programaot-launcher");
                    var token = UpdateConfig.GitHubToken;
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", token);
                    }

                    var checksumText = await http.GetStringAsync(checksumUrl);
                    var expectedHash = ExtractSha256(checksumText);
                    if (string.IsNullOrWhiteSpace(expectedHash))
                    {
                        Log("Aviso: Não foi possível extrair SHA256 do arquivo de checksum. Prosseguindo mesmo assim.");
                    }

                    // Tornar barra determinate e atualizar progresso durante cálculo do hash
                    Dispatcher.Invoke(() => { ProgressBar.IsIndeterminate = false; ProgressBar.Value = 0; });

                    string actualHash;
                    using (var fs = File.OpenRead(zipPath))
                    using (var sha = SHA256.Create())
                    {
                        long total = fs.Length;
                        long processed = 0;
                        int read;
                        var buffer = new byte[1024 * 1024]; // 1MB
                        // Usar TransformBlock/TransformFinalBlock para atualizar hash incrementalmente
                        while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            sha.TransformBlock(buffer, 0, read, null, 0);
                            processed += read;
                            var pct = total > 0 ? (int)Math.Round(processed * 100.0 / total) : 0;
                            SetProgress(pct, "Validando checksum...", $"{SizeSuffix(processed)} / {SizeSuffix(total)}");
                        }
                        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                        actualHash = BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
                    }

                    Log($"Checksum expected: {expectedHash}");
                    Log($"Checksum actual:   {actualHash}");
                    if (!string.IsNullOrWhiteSpace(expectedHash) && !string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                    {
                        Log("Aviso: Checksum inválido do pacote de atualização do launcher. Prosseguindo mesmo assim.");
                    }
                    else if (!string.IsNullOrWhiteSpace(expectedHash))
                    {
                        Log("Checksum validado com sucesso.");
                    }
                    else
                    {
                        Log("Checksum calculado (sem referência fornecida).");
                    }

                    return expectedHash;
                }
            }
            catch (Exception ex)
            {
                Log($"CHECKSUM VALIDATION FAILED: {ex}");
                return null; // Prossegue mesmo assim
            }
        }

        // 3) Extração do ZIP com barra de progresso (assíncrona para não travar o UI)
        private async Task ExtractZipWithProgressAsync(string zipPath, string payloadDir)
        {
            try
            {
                if (Directory.Exists(payloadDir)) Directory.Delete(payloadDir, true);
                Directory.CreateDirectory(payloadDir);

                SetStatus("Extraindo arquivos...");
                Log("Extracting zip file.");

                // Tornar barra determinate
                Dispatcher.Invoke(() => { ProgressBar.IsIndeterminate = false; ProgressBar.Value = 0; });

                await Task.Run(() =>
                {
                    using (var zip = Ionic.Zip.ZipFile.Read(zipPath))
                    {
                        var entries = zip.Entries.Where(e => !e.IsDirectory).ToList();
                        int totalEntries = entries.Count;
                        long totalBytes = entries.Aggregate(0L, (acc, e) => acc + (long)e.UncompressedSize);
                        long extractedBytes = 0;
                        int idx = 0;

                        foreach (var entry in entries)
                        {
                            idx++;

                            // Atualiza o UI antes de extrair o arquivo atual
                            SetProgress(totalBytes > 0 ? (int)Math.Round(extractedBytes * 100.0 / totalBytes) : (int)Math.Round((idx - 1) * 100.0 / totalEntries),
                                "Extraindo arquivos...",
                                $"[{idx}/{totalEntries}] {entry.FileName} — {SizeSuffix(extractedBytes)} / {SizeSuffix(totalBytes)}");

                            try
                            {
                                entry.Extract(payloadDir, Ionic.Zip.ExtractExistingFileAction.OverwriteSilently);
                            }
                            catch (Exception exEntry)
                            {
                                Log($"Erro ao extrair '{entry.FileName}': {exEntry.Message}. Prosseguindo com as próximas entradas.");
                                // Não interromper o fluxo de extração
                            }

                            extractedBytes += (long)entry.UncompressedSize;
                            int pct = 0;
                            if (totalBytes > 0)
                                pct = (int)Math.Round(extractedBytes * 100.0 / totalBytes);
                            else if (totalEntries > 0)
                                pct = (int)Math.Round(idx * 100.0 / totalEntries);

                            // Atualiza o UI após extrair o arquivo atual
                            SetProgress(pct, "Extraindo arquivos...",
                                $"[{idx}/{totalEntries}] {entry.FileName} — {SizeSuffix(extractedBytes)} / {SizeSuffix(totalBytes)}");
                        }
                    }
                });

                Log("Extraction completed.");
            }
            catch (Exception ex)
            {
                Log($"EXTRACTION FAILED: {ex}. Prosseguindo para etapa de aplicação, se possível.");
                // Não interromper o fluxo
            }
        }

        // 4) Inicia UpdaterHelper para aplicar a atualização (assíncrono para permitir UI atualizar)
        private async Task LaunchUpdaterHelperAsync(string sourceDir, string targetDir, string latestTag, string usedDownloadUrl, string expectedChecksum)
        {
            // Evitar problemas de parsing de argumentos quando há barra invertida no final
            string safeSourceDir = sourceDir.TrimEnd(Path.DirectorySeparatorChar);
            string safeTargetDir = targetDir.TrimEnd(Path.DirectorySeparatorChar);
            string pidArg = Process.GetCurrentProcess().Id.ToString();

            // Primeiro pede confirmação ao usuário antes de iniciar o UpdaterHelper.
            Log("Aguardando confirmação do usuário para iniciar a aplicação da atualização.");
            SetStatus("Aplicando atualização...", "Aguarde enquanto os arquivos são aplicados. O launcher será reiniciado automaticamente.");
            Dispatcher.Invoke(() => { ProgressBar.IsIndeterminate = true; });

            var res = MessageBox.Show(
                "Atualização pronta para aplicar.\nO Launcher precisa ser fechado e será reiniciado automaticamente ao término.\nDeseja fechar agora para aplicar a atualização?",
                "Confirmar reinício do Launcher",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res != MessageBoxResult.Yes)
            {
                Log("Usuário optou por não fechar agora. UpdaterHelper não será iniciado neste momento.");
                SetStatus("Atualização pendente.", "Feche o Launcher quando desejar aplicar a atualização.");
                return; // Não inicia o helper até o usuário escolher aplicar.
            }

            // Pequena pausa para o usuário visualizar o status antes de iniciar o helper
            await Task.Delay(4000);

            try
            {
                string helperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UpdateLauncher", "UpdaterHelper.exe");
                if (!File.Exists(helperPath))
                {
                    // Fallback para publish se não encontrado
                    helperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UpdateLauncher", "publish", "UpdaterHelper.exe");
                }

                if (!File.Exists(helperPath))
                {
                    Log($"UpdaterHelper.exe não encontrado em: {helperPath}");
                    SetStatus("Assistente de atualização não encontrado.", "A atualização pode não ser aplicada automaticamente. Verifique a instalação.");
                    return; // Não interromper a aplicação atual; apenas não inicia UpdaterHelper
                }

                var extraArgs = string.Empty;
                if (!string.IsNullOrWhiteSpace(latestTag))
                {
                    extraArgs += $" --version-tag \"{latestTag}\"";
                }
                if (!string.IsNullOrWhiteSpace(usedDownloadUrl))
                {
                    extraArgs += $" --source-url \"{usedDownloadUrl}\"";
                }
                if (!string.IsNullOrWhiteSpace(expectedChecksum))
                {
                    extraArgs += $" --zip-checksum {expectedChecksum}";
                }

                var processInfo = new ProcessStartInfo(helperPath)
                {
                    Arguments = $"--source-dir \"{safeSourceDir}\" --target-dir \"{safeTargetDir}\" --pid {pidArg}{extraArgs}",
                    UseShellExecute = true
                };

                // Evita UAC desnecessário: eleva apenas se o diretório alvo não for gravável
                if (!IsDirectoryWritable(targetDir))
                {
                    processInfo.Verb = "runas";
                }

                Log($"UpdaterHelper path: {helperPath}");
                Log($"UpdaterHelper args: {processInfo.Arguments}");
                Process.Start(processInfo);

                Log("UpdaterHelper iniciado após confirmação do usuário. Encerrando o Launcher...");
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Log($"Failed to start UpdaterHelper.exe: {ex}. Prosseguindo sem aplicar automaticamente.");
                SetStatus("Falha ao iniciar o assistente de atualização.", ex.Message);
                // Não lançar exceção e não encerrar a aplicação
            }
        }

        private static bool IsDirectoryWritable(string dir)
        {
            try
            {
                var testFile = System.IO.Path.Combine(dir, $".__write_test_{Guid.NewGuid():N}.tmp");
                using (var fs = System.IO.File.Create(testFile)) { }
                System.IO.File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ExtractSha256(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var match = Regex.Match(text, "[a-fA-F0-9]{64}");
            return match.Success ? match.Value.ToLowerInvariant() : null;
        }

        private string GetArg(string name)
        {
            // Prioriza o formato --chave="valor"
            string prefix = name + "=";
            var arg = _startupArgs.FirstOrDefault(a => a.StartsWith(prefix));
            if (arg != null)
            {
                return arg.Substring(prefix.Length).Trim('\"');
            }

            // Fallback para o formato --chave valor
            var flagIndex = Array.FindIndex(_startupArgs, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (flagIndex != -1 && flagIndex + 1 < _startupArgs.Length)
            {
                return _startupArgs[flagIndex + 1];
            }

            return null;
        }

        private static string SizeSuffix(long value, int decimalPlaces = 1)
        {
            string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
            if (value < 0) { return "-" + SizeSuffix(-value); }
            int i = 0;
            decimal dValue = (decimal)value;
            while (Math.Round(dValue, decimalPlaces) >= 1000)
            {
                dValue /= 1024;
                i++;
            }
            return string.Format("{0:n" + decimalPlaces + "} {1}", dValue, SizeSuffixes[i]);
        }

        public void SetProgress(double value, string status, string details = "")
        {
            Dispatcher.Invoke(() =>
            {
                ProgressBar.Value = value;
                StatusLabel.Text = status;
                DetailsLabel.Text = details;
            });
        }

        public void SetStatus(string status, string details = "")
        {
            Dispatcher.Invoke(() =>
            {
                StatusLabel.Text = status;
                DetailsLabel.Text = details;
            });
        }
    }
}
