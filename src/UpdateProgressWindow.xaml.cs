using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Ionic.Zip;
using Newtonsoft.Json;

namespace ProgramaOTLauncher
{
    public partial class UpdateProgressWindow : Window
    {
        private readonly string[] _startupArgs;

        public UpdateProgressWindow(string[] args)
        {
            InitializeComponent();
            _startupArgs = args;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        { 
            if (_startupArgs.Contains("--download-update"))
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
            // --- Parte 1: Lógica de Download e Extração (de DownloadAndExtractUpdateAsync) ---
            string latestTag = GetArg("--version");
            string assetUrl = GetArg("--url");
            string assetApiUrl = GetArg("--api-url");

            string updateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UpdateLauncher");
            Directory.CreateDirectory(updateDir);
            string zipPath = Path.Combine(updateDir, "launcher-update.zip");
            string payloadDir = Path.Combine(updateDir, "payload");

            var logPath = Path.Combine(updateDir, "update.log");
            void Log(string msg) => File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n");

            Log("Full update process started.");

            try
            {
                // Bloco de Download
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
                    }
                }
                catch (Exception ex)
                {
                    Log($"DOWNLOAD FAILED: {ex}");
                    throw new Exception("Falha no download da atualização.", ex);
                }

                // Bloco de Extração
                try
                {
                    if (Directory.Exists(payloadDir)) Directory.Delete(payloadDir, true);
                    Directory.CreateDirectory(payloadDir);

                    SetStatus("Extraindo arquivos...");
                    Log("Extracting zip file.");
                    using (var zip = Ionic.Zip.ZipFile.Read(zipPath))
                    {
                        zip.ExtractAll(payloadDir, Ionic.Zip.ExtractExistingFileAction.OverwriteSilently);
                    }
                    Log("Extraction completed.");
                }
                catch (Exception ex)
                {
                    Log($"EXTRACTION FAILED: {ex}");
                    throw new Exception("Falha ao extrair os arquivos da atualização.", ex);
                }


                // --- Parte 2: Lógica de Aplicação (usando UpdaterHelper.exe) ---
                string sourceDir = payloadDir;
                string targetDir = AppDomain.CurrentDomain.BaseDirectory;
                string pidArg = Process.GetCurrentProcess().Id.ToString();

                Log($"Handing off to UpdaterHelper. Source: {sourceDir}, Target: {targetDir}, PID: {pidArg}");

                try
                {
                    string helperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UpdateLauncher", "UpdaterHelper.exe");
                    if (!File.Exists(helperPath))
                    {
                        // Fallback to the publish directory if not found in the expected location
                        helperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UpdateLauncher", "publish", "UpdaterHelper.exe");
                    }

                    if (!File.Exists(helperPath))
                    {
                        throw new FileNotFoundException("UpdaterHelper.exe not found.", helperPath);
                    }

                    var processInfo = new ProcessStartInfo(helperPath)
                    {
                        Arguments = $"--source-dir \"{sourceDir}\" --target-dir \"{targetDir}\" --pid {pidArg}",
                        UseShellExecute = true, // Use ShellExecute para permitir que o processo se desanexe
                        Verb = "runas" // Tenta elevar se necessário para permissões de escrita
                    };

                    Process.Start(processInfo);

                    Log("UpdaterHelper started. Shutting down current application.");
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    Log($"Failed to start UpdaterHelper.exe: {ex}");
                    throw new Exception("Falha ao iniciar o assistente de atualização.", ex);
                }
            }
            catch (Exception ex)
            {
                Log($"FATAL ERROR: {ex.ToString()}");
                SetStatus("Erro durante a atualização!", ex.Message);
                await Task.Delay(5000); // Manter a janela aberta para ver o erro
                Application.Current.Shutdown();
            }
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