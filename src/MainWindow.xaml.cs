using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using LauncherConfig;
using Newtonsoft.Json;
using ProgramaOTLauncher.componentes;

namespace ProgramaOTLauncher
{
    public partial class MainWindow : Window, IClientUpdateListener
    {
        #region Campos e Propriedades

        private readonly ClientConfig _clientConfig;
        private readonly string _programVersion;
        private readonly AtualizaLauncher _atualizaLauncher;
        private readonly AtualizaCliente _atualizaCliente;

        private const string VersionLauncherJsonFileName = "versionlauncher.json";

        #endregion

        #region Construtor e Inicialização

        public MainWindow()
        {
            InitializeComponent();

            _clientConfig = ClientConfig.loadFromFile(UpdateConfig.RawLauncherConfigUrl);
            _programVersion = _clientConfig.launcherVersion;

            _atualizaLauncher = new AtualizaLauncher(
                _clientConfig,
                _programVersion,
                GetInstalledLauncherTag,
                SaveInstalledLauncherTag
            );

            _atualizaCliente = new AtualizaCliente(this, _clientConfig);

            EnsureVersionFilesPresentIfMissing();
        }

        private async void TibiaLauncher_Load(object sender, RoutedEventArgs e)
        {
            InitializeBrandingImages();
            InitializeUiDefaults();

            var installedTag = TryGetInstalledTagAndLogStatus();
            UpdateLauncherVersionLabel(installedTag);

            ApplyPostStartupEnvironmentSettings();

            // CORREÇÃO: Verificar updates em ordem e com delay para garantir estabilidade
            await Task.Delay(500); // Pequeno delay para garantir UI estável
            // DESATIVADO TEMPORARIAMENTE: não verificar atualização do Launcher.
            // Para reativar, remova o comentário da linha abaixo.
            // await CheckLauncherUpdateAndSyncButtonsAsync();
            await CheckClientUpdateAsync();
        }

        #endregion

        #region Inicialização de UI

        private void InitializeBrandingImages()
        {
            ImageLogoServer.Source = LoadImage("pack://application:,,,/assets/logo.png");
            ImageLogoCompany.Source = LoadImage("pack://application:,,,/assets/logo_company.png");
        }

        private void InitializeUiDefaults()
        {
            progressbarDownload.Visibility = Visibility.Collapsed;
            labelClientVersion.Visibility = Visibility.Collapsed;
            labelDownloadPercent.Visibility = Visibility.Collapsed;
            buttonLauncherUpdate.Visibility = Visibility.Collapsed; // Sempre começa oculto
        }

        private BitmapImage LoadImage(string uri)
        {
            return new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), uri));
        }

        #endregion

        #region Gerenciamento de Versão do Launcher

        private string TryGetInstalledTagAndLogStatus()
        {
            var installedTag = GetInstalledLauncherTag();
            var versionPath = GetVersionLauncherJsonPath();

            try
            {
                var exists = File.Exists(versionPath);
                Logger.Info($"VersionLauncherJsonPath={versionPath} exists={exists}");

                if (exists)
                {
                    TryLogFileContent(versionPath);
                }
            }
            catch (Exception ex)
            {
                LogError("Falha ao verificar existência de versionlauncher.json", ex);
            }

            return installedTag;
        }

        private void TryLogFileContent(string path)
        {
            try
            {
                var content = File.ReadAllText(path);
                Logger.Info($"versionlauncher.json={content}");
            }
            catch (Exception ex)
            {
                LogError("Falha ao ler versionlauncher.json", ex);
            }
        }

        private void UpdateLauncherVersionLabel(string installedTag)
        {
            var cleanInstalled = CleanLauncherTag(installedTag);
            var displayVersion = string.IsNullOrWhiteSpace(cleanInstalled)
                ? CleanLauncherTag(_programVersion)
                : cleanInstalled;

            labelLauncherVersion.Text = string.IsNullOrWhiteSpace(displayVersion)
                ? "Launcher v?"
                : $"Launcher v{displayVersion}";

            try
            {
                Logger.Info($"Launcher version label set to '{labelLauncherVersion.Text}' " +
                           $"(installedTag='{installedTag}', programVersion='{_programVersion}')");
            }
            catch (Exception ex)
            {
                LogError("Falha ao registrar labelLauncherVersion", ex);
            }
        }

        private string GetInstalledLauncherTag()
        {
            try
            {
                var path = GetVersionLauncherJsonPath();
                Logger.Info($"GetInstalledLauncherTag: Lendo de {path}");

                // CORREÇÃO: Forçar re-leitura do arquivo para pegar valores atualizados
                var tag = TryGetTagFromVersionLauncherJson();
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    Logger.Info($"GetInstalledLauncherTag: Lido do versionlauncher.json = '{tag}'");
                    return tag;
                }

                // Fallback para programVersion se arquivo não existe (primeira execução)
                Logger.Info($"GetInstalledLauncherTag: versionlauncher.json não existe, usando programVersion = '{_programVersion}'");
                return _programVersion;
            }
            catch (Exception ex)
            {
                LogError("Erro ao obter tag do launcher instalado", ex);
            }

            return _programVersion;
        }

        private string TryGetTagFromVersionLauncherJson()
        {
            var path = GetVersionLauncherJsonPath();
            if (!File.Exists(path))
                return null;

            try
            {
                var content = File.ReadAllText(path);
                var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(content);

                if (obj?.ContainsKey("versionTag") == true && obj["versionTag"] != null)
                {
                    var tag = obj["versionTag"].ToString();
                    return CleanLauncherTag(tag);
                }
            }
            catch (Exception ex)
            {
                LogError($"Erro ao ler versionTag de {path}", ex);
            }

            return null;
        }

        private void SaveInstalledLauncherTag(string tag)
        {
            try
            {
                var cleanTag = CleanLauncherTag(tag);
                var payload = new Dictionary<string, object>
                {
                    { "versionTag", cleanTag },
                    { "installedAtUtc", DateTime.UtcNow.ToString("o") }
                };

                var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
                var path = GetVersionLauncherJsonPath();

                File.WriteAllText(path, json);
                Logger.Info($"SaveInstalledLauncherTag: Salvou versionTag='{cleanTag}' em {path}");
            }
            catch (Exception ex)
            {
                LogError("Erro ao salvar tag do launcher", ex);
            }
        }

        private void EnsureVersionFilesPresentIfMissing()
        {
            try
            {
                var versionPath = GetVersionLauncherJsonPath();
                if (File.Exists(versionPath))
                {
                    Logger.Info("EnsureVersionFilesPresentIfMissing: versionlauncher.json já existe");
                    return;
                }

                Logger.Info("EnsureVersionFilesPresentIfMissing: Criando versionlauncher.json inicial");

                var tag = GetFallbackVersionTag();
                SaveInstalledLauncherTag(tag);
            }
            catch (Exception ex)
            {
                LogError("Erro ao garantir arquivos de versão", ex);
            }
        }

        private string GetFallbackVersionTag()
        {
            if (!string.IsNullOrWhiteSpace(_programVersion))
                return _programVersion;

            try
            {
                var exePath = Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");
                return FileVersionInfo.GetVersionInfo(exePath).FileVersion;
            }
            catch
            {
                return "1.0";
            }
        }

        private string CleanLauncherTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return tag;

            var cleanTag = tag.Trim();

            if (cleanTag.StartsWith("auto-", StringComparison.OrdinalIgnoreCase))
                cleanTag = cleanTag.Substring("auto-".Length);

            if (cleanTag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                cleanTag = cleanTag.Substring(1);

            return cleanTag;
        }

        #endregion

        #region Caminhos de Arquivo

        private string GetVersionLauncherJsonPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, VersionLauncherJsonFileName);
        }

        #endregion

        #region Verificação de Atualizações

        private async Task CheckLauncherUpdateAndSyncButtonsAsync()
        {
            try
            {
                Logger.Info("CheckLauncherUpdateAndSyncButtonsAsync: Iniciando verificação...");

                await _atualizaLauncher.CheckForUpdateAsync();

                var visibility = _atualizaLauncher.IsUpdatePending
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                buttonLauncherUpdate.Visibility = visibility;

                Logger.Info($"CheckLauncherUpdateAndSyncButtonsAsync: IsUpdatePending={_atualizaLauncher.IsUpdatePending}, Visibility={visibility}");
            }
            catch (Exception ex)
            {
                LogError("Erro ao verificar atualização do launcher", ex);
            }
        }

        private async Task CheckClientUpdateAsync()
        {
            try
            {
                await _atualizaCliente.CheckForUpdateAsync();
            }
            catch (Exception ex)
            {
                LogError("Erro ao verificar atualização do cliente", ex);
            }
        }

        #endregion

        #region Manipuladores de Eventos de Botões

        private async void buttonLauncherUpdate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Logger.Info("buttonLauncherUpdate_Click: Usuário clicou no botão de atualização");
                await _atualizaLauncher.TriggerUpdateAsync();
            }
            catch (Exception ex)
            {
                LogError("Erro ao disparar atualização do launcher", ex);
            }
        }

        private async void buttonPlay_Click(object sender, RoutedEventArgs e)
        {
            if (_atualizaLauncher.IsUpdatePending)
            {
                ShowUpdateRequiredMessage();
                return;
            }

            await _atualizaCliente.HandlePlayButtonClickAsync();
        }

        private void ShowUpdateRequiredMessage()
        {
            MessageBox.Show(
                "Uma atualização do Launcher está disponível.\n\nAtualize o Launcher antes de prosseguir.",
                "Atualização necessária",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        private void buttonPlay_MouseEnter(object sender, MouseEventArgs e)
        {
            var imageName = _atualizaCliente.NeedUpdate
                ? "button_hover_update.png"
                : "button_hover_play.png";

            buttonPlay.Background = new ImageBrush(LoadImage($"pack://application:,,,/assets/{imageName}"));
        }

        private void buttonPlay_MouseLeave(object sender, MouseEventArgs e)
        {
            var imageName = _atualizaCliente.NeedUpdate
                ? "button_update.png"
                : "button_play.png";

            buttonPlay.Background = new ImageBrush(LoadImage($"pack://application:,,,/assets/{imageName}"));
        }

        #endregion

        #region Manipuladores de Eventos de Janela

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResizeMode == ResizeMode.NoResize)
                return;

            WindowState = WindowState == WindowState.Normal
                ? WindowState.Maximized
                : WindowState.Normal;
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

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
                labelLauncherVersion.Text = ex.Message;
            }
        }

        #endregion

        #region Configurações Pós-Inicialização

        private void ApplyPostStartupEnvironmentSettings()
        {
            CreateShortcut();
            AddReadOnlyAttributeToFiles();
        }

        private static void CreateShortcut()
        {
            try
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var clientConfig = ClientConfig.loadFromFile(UpdateConfig.RawLauncherConfigUrl);
                var shortcutPath = Path.Combine(desktopPath, $"{clientConfig.clientFolder}.lnk");

                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                var shortcut = shell.CreateShortcut(shortcutPath);

                try
                {
                    shortcut.TargetPath = Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");
                    shortcut.Description = clientConfig.clientFolder;
                    shortcut.Save();
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                }
            }
            catch (Exception ex)
            {
                LogError("Erro ao criar atalho", ex);
            }
        }

        private void AddReadOnlyAttributeToFiles()
        {
            var launcherPath = PathHelper.GetLauncherPath(_clientConfig);
            var cacheFolder = Path.Combine(launcherPath, "cache");

            var filesToProtect = new[]
            {
                Path.Combine(cacheFolder, "eventschedule.json"),
                Path.Combine(cacheFolder, "boostedcreature.json"),
                Path.Combine(cacheFolder, "onlinenumbers.json")
            };

            foreach (var file in filesToProtect)
            {
                TrySetReadOnly(file);
            }
        }

        private static void TrySetReadOnly(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.SetAttributes(filePath, FileAttributes.ReadOnly);
                }
            }
            catch (Exception ex)
            {
                LogError($"Erro ao definir atributo readonly para {filePath}", ex);
            }
        }

        #endregion

        #region Implementação de IClientUpdateListener

        public void SetAppVersion(string version)
        {
            labelClientTag.Text = version;
        }

        public void ShowDownloadButton()
        {
            buttonPlay.Background = new ImageBrush(LoadImage("pack://application:,,,/assets/button_update.png"));
            buttonPlayIcon.Source = LoadImage("pack://application:,,,/assets/icon_update.png");
            labelClientVersion.Content = "Download";
            labelClientVersion.Visibility = Visibility.Visible;
            buttonPlay.Visibility = Visibility.Visible;
            buttonPlay_tooltip.Text = "Download";
        }

        public void ShowPlayButton()
        {
            buttonPlay.Background = new ImageBrush(LoadImage("pack://application:,,,/assets/button_play.png"));
            buttonPlayIcon.Source = LoadImage("pack://application:,,,/assets/icon_play.png");
            buttonPlay_tooltip.Text = "Play";
            labelClientVersion.Visibility = Visibility.Collapsed;
            // Garantir que o botão volte a aparecer após HideProgress ter colapsado
            buttonPlay.Visibility = Visibility.Visible;
        }

        public void ShowUpdateButton()
        {
            buttonPlay.Background = new ImageBrush(LoadImage("pack://application:,,,/assets/button_update.png"));
            buttonPlayIcon.Source = LoadImage("pack://application:,,,/assets/icon_update.png");
            labelClientVersion.Content = "Update";
            labelClientVersion.Visibility = Visibility.Visible;
            buttonPlay.Visibility = Visibility.Visible;
            buttonPlay_tooltip.Text = "Update";
        }

        public void ShowProgress()
        {
            labelDownloadPercent.Visibility = Visibility.Visible;
            progressbarDownload.Visibility = Visibility.Visible;
            labelClientVersion.Visibility = Visibility.Collapsed;
            buttonPlay.Visibility = Visibility.Collapsed;
        }

        public void HideProgress()
        {
            labelDownloadPercent.Visibility = Visibility.Collapsed;
            progressbarDownload.Visibility = Visibility.Collapsed;
        }

        public void SetDownloadPercentage(int percentage)
        {
            progressbarDownload.Value = percentage;
        }

        public void SetDownloadStatus(string status)
        {
            labelDownloadPercent.Content = status;
        }

        public void CloseWindow()
        {
            Close();
        }

        public void ShowMessage(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Métodos Auxiliares

        private static void LogError(string message, Exception ex)
        {
            try
            {
                Logger.Error(message, ex);
            }
            catch
            {
                // Falha ao logar não deve interromper a execução
            }
        }

        #endregion
    }
}
