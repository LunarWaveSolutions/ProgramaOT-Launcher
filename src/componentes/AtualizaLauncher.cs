using System;
using System.Diagnostics;
using System.Collections.Generic;
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
            // Garantir que a versão usada para comparação não inclua prefixo 'v' ou 'auto-'
            var versionForCompare = string.IsNullOrWhiteSpace(installedLauncherTag) ? CleanTagLocal(_programVersion) : CleanTagLocal(installedLauncherTag);
            var luInfo = await LauncherUpdateService.CheckAsync(_clientConfig, versionForCompare);

            PendingUpdate = luInfo.HasUpdate ? luInfo : null;

            if (!luInfo.HasUpdate && string.IsNullOrWhiteSpace(installedLauncherTag))
            {
                try { _saveInstalledLauncherTag(_programVersion); } catch { }
            }

            try { Logger.Info($"Resultado da verificação: hasUpdate={luInfo.HasUpdate}, latestTag={luInfo.LatestTag}"); } catch { }

            return luInfo;
        }

        // Remover prefixos usados apenas visualmente para permitir comparação correta
        private static string CleanTagLocal(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return t;
            var s = t.Trim();
            if (s.StartsWith("auto-", StringComparison.OrdinalIgnoreCase)) s = s.Substring("auto-".Length);
            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s.Substring(1);
            return s;
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

            // Alteração de fluxo: abrir a UpdateProgressWindow diretamente no mesmo processo
            await StartUpdateInlineAsync(PendingUpdate);
        }

        // Novo fluxo: inicia a UpdateProgressWindow diretamente (sem relançar o executável)
        private static Task StartUpdateInlineAsync(LauncherUpdateInfo luInfo)
        {
            try
            {
                var argsList = new List<string>
                {
                    "--download-update",
                    $"--url={luInfo.AssetUrl}",
                    $"--version={luInfo.LatestTag}",
                    $"--pid={Process.GetCurrentProcess().Id}"
                };
                if (!string.IsNullOrWhiteSpace(luInfo.AssetApiUrl))
                {
                    argsList.Add($"--api-url={luInfo.AssetApiUrl}");
                }
                if (!string.IsNullOrWhiteSpace(luInfo.ChecksumUrl))
                {
                    argsList.Add($"--checksum-url={luInfo.ChecksumUrl}");
                }

                try { Logger.Info($"Abrindo UpdateProgressWindow inline com args: {string.Join(" ", argsList)}"); } catch { }
                var progressWindow = new UpdateProgressWindow(argsList.ToArray());
                // Opcional: definir Owner para manter o foco
                try { progressWindow.Owner = Application.Current?.MainWindow; } catch { }
                progressWindow.ShowDialog();
                try { Logger.Info("UpdateProgressWindow finalizada."); } catch { }
            }
            catch (Exception ex)
            {
                try { Logger.Error("Falha ao abrir a UpdateProgressWindow inline", ex); } catch { }
                MessageBox.Show($"Falha ao abrir a janela de progresso da atualização: {ex.Message}", "Erro de Atualização", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return Task.CompletedTask;
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
    }
}
