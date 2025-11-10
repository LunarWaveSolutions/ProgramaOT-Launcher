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
using LauncherConfig;
using ProgramaOTLauncher.componentes;

namespace ProgramaOTLauncher
{
    public partial class MainWindow : Window, IClientUpdateListener
    {
        // URL global do launcher_config.json (pode ser repo privado se houver token)
        static string launcerConfigUrl = ProgramaOTLauncher.UpdateConfig.RawLauncherConfigUrl;
        // Load infos do launcher_config.json (pastas/executável etc). Não usa versão remota para decidir update.
        static ClientConfig clientConfig = ClientConfig.loadFromFile(launcerConfigUrl);

		static string clientExecutableName = clientConfig.clientExecutable;
		static string programVersion = clientConfig.launcherVersion;

        // Componente de atualização do Launcher
        private AtualizaLauncher _atualizaLauncher;
        private AtualizaCliente _atualizaCliente;


        static readonly HttpClient httpClient = new HttpClient();
		WebClient webClient = new WebClient();

		public MainWindow()
		{
			InitializeComponent();
            _atualizaLauncher = new AtualizaLauncher(clientConfig, programVersion, GetInstalledLauncherTag, SaveInstalledLauncherTag);
            _atualizaCliente = new AtualizaCliente(this, clientConfig);

            // Garantir que os arquivos de versão existam no primeiro run mesmo sem atualização
            EnsureVersionFilesPresentIfMissing();
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
            
            // Exibir a versão do Launcher com base no versionlauncher.json (fonte única de verdade).
            // Fallback para programVersion apenas se o arquivo ainda não existir.
            var installedTag = GetInstalledLauncherTag();
            var cleanInstalled = CleanLauncherTag(installedTag);
            labelLauncherVersion.Text = "v" + (string.IsNullOrWhiteSpace(cleanInstalled) ? CleanLauncherTag(programVersion) : cleanInstalled);

            CreateShortcut();
            AddReadOnly();

            // Checagem de auto-update do Launcher
            await _atualizaLauncher.CheckForUpdateAsync();
            var vis = _atualizaLauncher.IsUpdatePending ? Visibility.Visible : Visibility.Collapsed;
            buttonLauncherUpdate.Visibility = vis;
            // Há dois botões sobrepostos no XAML (buttonLauncherUpdate e buttonLauncherUpdate_Copiar).
            // Precisamos sincronizar ambos para evitar que o ícone de update apareça indevidamente.
            try { buttonLauncherUpdate_Copiar.Visibility = vis; } catch { }

            // Checagem de atualização do Cliente
            await _atualizaCliente.CheckForUpdateAsync();
		}

        // Clique no ícone de update do Launcher ao lado do texto de versão
        private async void buttonLauncherUpdate_Click(object sender, RoutedEventArgs e)
        {
            await _atualizaLauncher.TriggerUpdateAsync();
        }

        private async void buttonPlay_Click(object sender, RoutedEventArgs e)
        {
            if (_atualizaLauncher.IsUpdatePending)
            {
                MessageBox.Show(
                    "Uma atualização do Launcher está disponível.\n\nAtualize o Launcher antes de prosseguir.",
                    "Atualização necessária",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return;
            }
            await _atualizaCliente.HandlePlayButtonClickAsync();
        }

        private void buttonPlay_MouseEnter(object sender, MouseEventArgs e)
        {
            if (_atualizaCliente.NeedUpdate)
                buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/assets/button_hover_update.png")));
            else
                buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/assets/button_hover_play.png")));
        }

        private void buttonPlay_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_atualizaCliente.NeedUpdate)
                buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/assets/button_update.png")));
            else
                buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri(BaseUriHelper.GetBaseUri(this), "pack://application:,,,/assets/button_play.png")));
        }

        private void AddReadOnly()
        {
            string eventSchedulePath = Path.Combine(PathHelper.GetLauncherPath(clientConfig), "cache", "eventschedule.json");
            if (File.Exists(eventSchedulePath)) { File.SetAttributes(eventSchedulePath, FileAttributes.ReadOnly); }

            string boostedCreaturePath = Path.Combine(PathHelper.GetLauncherPath(clientConfig), "cache", "boostedcreature.json");
            if (File.Exists(boostedCreaturePath)) { File.SetAttributes(boostedCreaturePath, FileAttributes.ReadOnly); }

            string onlineNumbersPath = Path.Combine(PathHelper.GetLauncherPath(clientConfig), "cache", "onlinenumbers.json");
            if (File.Exists(onlineNumbersPath)) { File.SetAttributes(onlineNumbersPath, FileAttributes.ReadOnly); }
        }

        private string LauncherVersionsJsonPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "versions.json");
        }

        private string VersionLauncherJsonPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "versionlauncher.json");
        }


        private string GetInstalledLauncherTag()
        {
            try
            {
                // Preferir o novo arquivo versionlauncher.json (criado pelo UpdaterHelper)
                string vpath = VersionLauncherJsonPath();
                if (File.Exists(vpath))
                {
                    var text = File.ReadAllText(vpath);
                    var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(text);
                    if (obj != null && obj.ContainsKey("versionTag") && obj["versionTag"] != null)
                    {
                        return CleanLauncherTag(obj["versionTag"].ToString());
                    }
                }

                // Fallback para o arquivo legado versions.json
                string path = LauncherVersionsJsonPath();
                if (File.Exists(path))
                {
                    var text = File.ReadAllText(path);
                    var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(text);
                    if (obj != null && obj.ContainsKey("installedLauncherTag") && obj["installedLauncherTag"] != null)
                    {
                        return CleanLauncherTag(obj["installedLauncherTag"].ToString());
                    }
                }
            }
            catch { }
            return "";
        }

        private void SaveInstalledLauncherTag(string tag)
        {
            try
            {
                // Escrever no novo arquivo versionlauncher.json
                var payload = new Dictionary<string, object>
                {
                    {"versionTag", CleanLauncherTag(tag)},
                    {"installedAtUtc", DateTime.UtcNow.ToString("o")}
                };
                File.WriteAllText(VersionLauncherJsonPath(), JsonConvert.SerializeObject(payload, Formatting.Indented));

            }
            catch { }
        }

        // Cria versionlauncher.json se estiver ausente, usando a melhor fonte disponível
        private void EnsureVersionFilesPresentIfMissing()
        {
            try
            {
                var vpath = VersionLauncherJsonPath();
                // Remover artefatos antigos se existirem
                try { var alias1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launchversion.json"); if (File.Exists(alias1)) File.Delete(alias1); } catch { }
                try { var alias2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launchversions.json"); if (File.Exists(alias2)) File.Delete(alias2); } catch { }
                bool hasV = File.Exists(vpath);
                if (!hasV)
                {
                    string tag = GetInstalledLauncherTag();
                    if (string.IsNullOrWhiteSpace(tag))
                    {
                        // Se não houver nada salvo, usar a versão declarada no clientConfig
                        tag = programVersion;
                        if (string.IsNullOrWhiteSpace(tag))
                        {
                            try
                            {
                                var exePath = Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");
                                tag = FileVersionInfo.GetVersionInfo(exePath).FileVersion;
                            }
                            catch { tag = "unknown"; }
                        }
                    }
                    SaveInstalledLauncherTag(tag);
                }
            }
            catch { }
        }

        private string CleanLauncherTag(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return t;
            var s = t.Trim();
            if (s.StartsWith("auto-", StringComparison.OrdinalIgnoreCase)) s = s.Substring("auto-".Length);
            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s.Substring(1);
            return s;
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
                labelLauncherVersion.Text = ex.Message;
			}
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

        public void SetAppVersion(string version)
        {
            // Esta versão se refere ao CLIENTE (rodapé). O Launcher usa labelLauncherVersion.
            labelClientTag.Text = version;
        }

        public void ShowDownloadButton()
        {
            buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/assets/button_update.png")));
            buttonPlayIcon.Source = new BitmapImage(new Uri("pack://application:,,,/assets/icon_update.png"));
            labelClientVersion.Content = "Download";
            labelClientVersion.Visibility = Visibility.Visible;
            buttonPlay.Visibility = Visibility.Visible;
            buttonPlay_tooltip.Text = "Download";
        }

        public void ShowPlayButton()
        {
            buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/assets/button_play.png")));
            buttonPlayIcon.Source = new BitmapImage(new Uri("pack://application:,,,/assets/icon_play.png"));
            buttonPlay_tooltip.Text = "Play";
            labelClientVersion.Visibility = Visibility.Collapsed;
        }

        public void ShowUpdateButton()
        {
            buttonPlay.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/assets/button_update.png")));
            buttonPlayIcon.Source = new BitmapImage(new Uri("pack://application:,,,/assets/icon_update.png"));
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
    }
}
