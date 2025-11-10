using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using LauncherConfig;
using ProgramaOTLauncher;

namespace ProgramaOTLauncher.componentes
{
    public class AtualizaLauncher
    {
        private readonly ClientConfig _clientConfig;
        private readonly Func<string> _getInstalledLauncherTag;
        private readonly Action<string> _saveInstalledLauncherTag;
        private readonly string _programVersion;

        public LauncherUpdateInfo PendingUpdate { get; private set; }

        public bool IsUpdatePending => PendingUpdate != null && PendingUpdate.HasUpdate;

        public AtualizaLauncher(ClientConfig clientConfig, string programVersion, Func<string> getInstalledLauncherTag, Action<string> saveInstalledLauncherTag)
        {
            _clientConfig = clientConfig;
            _programVersion = programVersion;
            _getInstalledLauncherTag = getInstalledLauncherTag;
            _saveInstalledLauncherTag = saveInstalledLauncherTag;
        }

        public async Task<LauncherUpdateInfo> CheckForUpdateAsync()
        {
            try { Logger.Info("Verificando atualização do launcher..."); } catch { }
            var installedLauncherTag = _getInstalledLauncherTag();
            var versionForCompare = string.IsNullOrWhiteSpace(installedLauncherTag) ? _programVersion : installedLauncherTag;
            var luInfo = await LauncherUpdateService.CheckAsync(_clientConfig, versionForCompare);

            PendingUpdate = luInfo.HasUpdate ? luInfo : null;

            if (!luInfo.HasUpdate && string.IsNullOrWhiteSpace(installedLauncherTag))
            {
                try { _saveInstalledLauncherTag(_programVersion); } catch { }
            }

            try { Logger.Info($"Resultado da verificação: hasUpdate={luInfo.HasUpdate}, latestTag={luInfo.LatestTag}"); } catch { }

            return luInfo;
        }

        public async Task TriggerUpdateAsync(bool isMandatory = false)
        {
            if (PendingUpdate == null || !PendingUpdate.HasUpdate) return;

            if (!isMandatory)
            {
                string msg = "Uma atualização do Launcher está disponível.";
                if (!string.IsNullOrWhiteSpace(PendingUpdate.LatestTag))
                    msg += $"\nÚltima versão/tag: {PendingUpdate.LatestTag}";
                msg += "\n\nDeseja iniciar a atualização agora?";
                var result = MessageBox.Show(msg, "Atualização do Launcher", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                    return;
            }

            await StartUpdateProcessAsync(PendingUpdate);
        }

        private static Task StartUpdateProcessAsync(LauncherUpdateInfo luInfo)
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var args = $"--download-update --url=\"{luInfo.AssetUrl}\" --version=\"{luInfo.LatestTag}\" --pid={currentProcess.Id}";
                if (!string.IsNullOrWhiteSpace(luInfo.AssetApiUrl))
                {
                    args += $" --api-url=\"{luInfo.AssetApiUrl}\"";
                }
                if (!string.IsNullOrWhiteSpace(luInfo.ChecksumUrl))
                {
                    args += $" --checksum-url=\"{luInfo.ChecksumUrl}\"";
                }

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = currentProcess.MainModule.FileName,
                    Arguments = args,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                try { Logger.Info($"Iniciando atualização do launcher com argumentos: {args}"); } catch { }
                Process.Start(processStartInfo);
                try { Logger.Info("Processo de atualização iniciado. Encerrando aplicação atual."); } catch { }
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                try { Logger.Error("Falha ao iniciar o processo de atualização do launcher", ex); } catch { }
                MessageBox.Show($"Falha ao iniciar o processo de atualização do launcher: {ex.Message}", "Erro de Atualização", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return Task.CompletedTask;
        }
    }
}
