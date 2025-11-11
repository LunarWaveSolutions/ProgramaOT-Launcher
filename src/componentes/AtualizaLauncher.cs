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

            // CORREÇÃO: Só salvar se realmente não tem update
            if (!luInfo.HasUpdate && string.IsNullOrWhiteSpace(installedLauncherTag))
            {
                try { _saveInstalledLauncherTag(_programVersion); } catch { }
            }

            try { Logger.Info($"Resultado da verificação: hasUpdate={luInfo.HasUpdate}, latestTag={luInfo.LatestTag}, versionForCompare={versionForCompare}"); } catch { }

            return luInfo;
        }

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
                    msg += $"\nVersão atual: {CleanTagLocal(_getInstalledLauncherTag() ?? _programVersion)}";
                msg += $"\nNova versão: {PendingUpdate.LatestTag}";
                msg += "\n\nO launcher será fechado e reiniciado automaticamente.";
                msg += "\n\nDeseja iniciar a atualização agora?";

                var result = MessageBox.Show(msg, "Atualização do Launcher", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                    return;
            }

            // CORREÇÃO CRÍTICA: Voltar ao fluxo original de restart do processo
            await StartUpdateProcessAsync(PendingUpdate);
        }

        // FLUXO CORRIGIDO: Reinicia o processo com argumentos de update
        private static Task StartUpdateProcessAsync(LauncherUpdateInfo luInfo)
        {
            try
            {
                var exePath = Process.GetCurrentProcess().MainModule.FileName;

                var argsList = new List<string>
                {
                    "--download-update",
                    $"--url=\"{luInfo.AssetUrl}\"",
                    $"--version=\"{luInfo.LatestTag}\""
                };

                if (!string.IsNullOrWhiteSpace(luInfo.AssetApiUrl))
                {
                    argsList.Add($"--api-url=\"{luInfo.AssetApiUrl}\"");
                }
                if (!string.IsNullOrWhiteSpace(luInfo.ChecksumUrl))
                {
                    argsList.Add($"--checksum-url=\"{luInfo.ChecksumUrl}\"");
                }

                var args = string.Join(" ", argsList);

                try { Logger.Info($"Reiniciando launcher com argumentos de update: {args}"); } catch { }

                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    UseShellExecute = true,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };

                // Verificar se precisa de elevação
                if (!IsDirectoryWritable(AppDomain.CurrentDomain.BaseDirectory))
                {
                    startInfo.Verb = "runas";
                }

                Process.Start(startInfo);

                try { Logger.Info("Processo de atualização iniciado. Encerrando launcher atual..."); } catch { }

                // Fechar a aplicação atual para liberar os arquivos
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                try { Logger.Error("Falha ao iniciar processo de atualização", ex); } catch { }
                MessageBox.Show($"Falha ao iniciar atualização: {ex.Message}", "Erro de Atualização", MessageBoxButton.OK, MessageBoxImage.Error);
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