using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace UpdaterHelperUI
{
    public partial class ProgressWindow : Window
    {
        private readonly string _sourceDir;
        private readonly string _targetDir;
        private readonly int _processId;
        private readonly string? _versionTag;
        private readonly string? _sourceUrl;
        private readonly string? _zipChecksum;

        public ProgressWindow(string sourceDir, string targetDir, int processId, string? versionTag, string? sourceUrl, string? zipChecksum)
        {
            InitializeComponent();
            _sourceDir = sourceDir;
            _targetDir = targetDir;
            _processId = processId;
            _versionTag = versionTag;
            _sourceUrl = sourceUrl;
            _zipChecksum = zipChecksum;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _ = StartUpdateAsync();
        }

        public void SetStatus(string text)
        {
            Dispatcher.Invoke(() => StatusText.Text = text);
        }

        public void SetDetails(string text)
        {
            Dispatcher.Invoke(() => DetailsText.Text = text);
        }

        public void SetProgress(int percent)
        {
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            Dispatcher.Invoke(() => ProgressBar.Value = percent);
        }

        private async Task StartUpdateAsync()
        {
            // Orquestrador principal com etapas separadas para facilitar manutenção e debugging
            try
            {
                UpdaterHelper.Log("[Update] Iniciando orquestração do processo de atualização...");

                await HideWindowUntilLauncherClosedAsync();

                ShowProgressWindow();

                SetStatus("Preparando aplicação de arquivos...");
                UpdaterHelper.Log("[Update] Enumerando arquivos do pacote de atualização...");
                var (allFiles, totalBytes) = await EnumerateSourceFilesAsync();

                UpdaterHelper.Log($"[Update] Encontrados {allFiles.Count} arquivos. Total de bytes: {totalBytes}");
                SetStatus("Aplicando arquivos do update...");
                await CopyFilesWithProgressAsync(allFiles, totalBytes);

                await WriteVersionInfoAsync();

                var shouldRestart = await PromptRestartAsync();
                if (shouldRestart)
                {
                    await UpdateConfigAndRestartAsync();
                }

                await CleanupSourceDirSafeAsync();

                await FinishAndCloseAsync();
                UpdaterHelper.Log("[Update] Processo de atualização concluído.");
            }
            catch (Exception ex)
            {
                UpdaterHelper.Log($"[Update] Falha na orquestração: {ex.Message}");
                SetStatus("Falha durante a aplicação da atualização.");
                SetDetails(ex.Message);
                try { await Task.Delay(5000); } catch { }
                try { Dispatcher.Invoke(Close); } catch { }
            }
        }

        // Etapa: Ocultar janela e aguardar encerramento do processo principal
        private async Task HideWindowUntilLauncherClosedAsync()
        {
            try { Dispatcher.Invoke(() => { this.Hide(); this.ShowInTaskbar = false; }); } catch { }

            if (_processId != 0)
            {
                try
                {
                    var pid = _processId;
                    await Task.Run(() =>
                    {
                        try
                        {
                            var parentProcess = Process.GetProcessById(pid);
                            SetStatus($"Aguardando o launcher fechar (PID {pid})...");
                            UpdaterHelper.Log($"[Update] Aguardando término do processo principal PID={pid}...");
                            parentProcess.WaitForExit();
                            UpdaterHelper.Log("[Update] Processo principal terminado.");
                        }
                        catch (ArgumentException)
                        {
                            UpdaterHelper.Log("[Update] Processo principal já não está em execução. Prosseguindo.");
                        }
                    });

                    SetStatus("Aguardando liberação de arquivo (1,5s)...");
                    UpdaterHelper.Log("[Update] Grace period após término do processo: 1500 ms");
                    await Task.Delay(1500);
                }
                catch (Exception ex)
                {
                    UpdaterHelper.Log($"[Update] Erro ao aguardar pelo processo principal: {ex.Message}");
                }
            }
            else
            {
                UpdaterHelper.Log("[Update] ProcessId não fornecido. Aplicando atualização sem aguardar término explícito.");
                await Task.Delay(1000);
            }
        }

        // Etapa: Mostrar janela de progresso
        private void ShowProgressWindow()
        {
            try { Dispatcher.Invoke(() => { this.ShowInTaskbar = true; this.Show(); this.Activate(); }); } catch { }
        }

        // Etapa: Enumerar arquivos do diretório de origem e calcular total de bytes
        private async Task<(System.Collections.Generic.List<string> files, long totalBytes)> EnumerateSourceFilesAsync()
        {
            return await Task.Run(() =>
            {
                var allFiles = Directory.EnumerateFiles(_sourceDir, "*", SearchOption.AllDirectories).ToList();

                // Filtrar arquivos legados que não devem mais ser distribuídos/copiados
                var filtered = allFiles
                    .Where(f =>
                    {
                        var name = Path.GetFileName(f);
                        if (name.Equals("launchversion.json", StringComparison.OrdinalIgnoreCase)) return false;
                        if (name.Equals("launchversions.json", StringComparison.OrdinalIgnoreCase)) return false;
                        return true;
                    })
                    .ToList();

                int removedLegacy = allFiles.Count - filtered.Count;
                if (removedLegacy > 0)
                {
                    UpdaterHelper.Log($"[Update] Filtrando arquivos legados: {removedLegacy} removido(s) (launchversion(s).json).");
                }

                long totalBytes = 0;
                foreach (var f in filtered)
                {
                    try { totalBytes += new FileInfo(f).Length; } catch { }
                }
                return (filtered, totalBytes);
            });
        }

        // Etapa: Copiar arquivos com progresso e logs
        private async Task CopyFilesWithProgressAsync(System.Collections.Generic.List<string> allFiles, long totalBytes)
        {
            await Task.Run(() =>
            {
                int totalFiles = allFiles.Count;
                long copiedBytes = 0;
                int index = 0;
                int skippedCount = 0;
                int copiedCount = 0;

                foreach (var srcFile in allFiles)
                {
                    index++;
                    string relPath = srcFile.Substring(_sourceDir.Length).TrimStart('\\', '/');
                    string destFile = Path.Combine(_targetDir, relPath);
                    string destDir = Path.GetDirectoryName(destFile) ?? _targetDir;

                    try { Directory.CreateDirectory(destDir); } catch { }

                    bool skipped = false;

                    try
                    {
                        if (File.Exists(destFile))
                        {
                            long existingSize = new FileInfo(destFile).Length;
                            long srcSize = new FileInfo(srcFile).Length;
                            if (existingSize == srcSize)
                            {
                                skipped = true;
                            }
                        }
                    }
                    catch { }

                    SetDetails($"[{index}/{totalFiles}] {relPath} {(skipped ? "(sem alterações)" : "")}");

                    const int retries = 5;
                    for (int i = 0; i < retries; i++)
                    {
                        try
                        {
                            if (!skipped)
                            {
                                File.Copy(srcFile, destFile, true);
                                UpdaterHelper.Log($"[Update] Copiado: {srcFile} -> {destFile}");
                                copiedCount++;
                                try { copiedBytes += new FileInfo(srcFile).Length; } catch { }
                            }
                            else
                            {
                                UpdaterHelper.Log($"[Update] Ignorado (sem alterações): {srcFile}");
                                skippedCount++;
                            }
                            break;
                        }
                        catch (IOException ex)
                        {
                            UpdaterHelper.Log($"[Update] Tentativa {i + 1}: Erro de IO ao copiar {srcFile} - {ex.Message}");
                            if (i == retries - 1) throw;
                            Thread.Sleep(1000);
                        }
                    }

                    int pct = 0;
                    if (totalBytes > 0)
                    {
                        pct = (int)Math.Round(copiedBytes * 100.0 / totalBytes);
                    }
                    else if (totalFiles > 0)
                    {
                        pct = (int)Math.Round(index * 100.0 / totalFiles);
                    }
                    SetProgress(pct);
                }

                UpdaterHelper.Log($"[Update] Cópia concluída. Copiados: {copiedCount}, Ignorados: {skippedCount}");
            });
        }

        // Etapa: Escrever versionlauncher.json com segurança
        private async Task WriteVersionInfoAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    UpdaterHelper.WriteVersionLauncherJson(_targetDir, _versionTag, _sourceUrl, _zipChecksum);
                    UpdaterHelper.Log("[Update] versionlauncher.json escrito/atualizado com sucesso.");
                }
                catch (Exception vex)
                {
                    UpdaterHelper.Log($"[Update] Falha ao escrever versionlauncher.json: {vex.Message}");
                }
            });
        }

        // Etapa: Perguntar se deve reiniciar
        private async Task<bool> PromptRestartAsync()
        {
            SetStatus("Concluído. Clique OK para reiniciar...");
            var restartRes = MessageBoxResult.OK;
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    restartRes = MessageBox.Show(this, "Atualização concluída.\nClique OK para reiniciar o Launcher agora.", "ProgramaOT", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch { }
            return restartRes == MessageBoxResult.OK;
        }

        // Etapa: Atualizar config e reiniciar executável principal (com fallback .bat)
        private async Task UpdateConfigAndRestartAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    UpdaterHelper.UpdateLauncherConfigVersion(_targetDir, _versionTag);
                }
                catch { }

                string mainAppPath = Path.Combine(_targetDir ?? ".", "ProgramaOT-Launcher.exe");
                UpdaterHelper.Log($"[Update] Preparando reinício do launcher. Caminho: {mainAppPath}");

                if (File.Exists(mainAppPath))
                {
                    try
                    {
                        var psi = new ProcessStartInfo(mainAppPath)
                        {
                            WorkingDirectory = _targetDir ?? ".",
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                        UpdaterHelper.Log($"[Update] Aplicação principal reiniciada com WorkingDirectory={psi.WorkingDirectory}.");
                    }
                    catch (Exception rex)
                    {
                        UpdaterHelper.Log($"[Update] Falha ao reiniciar aplicação principal diretamente: {rex.Message}");
                        try
                        {
                            var batPath = Path.Combine(_targetDir ?? ".", "restart_launcher.bat");
                            var batContent = "@echo off\r\n" +
                                             "cd /d \"" + (_targetDir ?? ".") + "\"\r\n" +
                                             "start \"\" \"ProgramaOT-Launcher.exe\"\r\n";
                            File.WriteAllText(batPath, batContent);
                            var batPsi = new ProcessStartInfo(batPath)
                            {
                                WorkingDirectory = _targetDir ?? ".",
                                UseShellExecute = true
                            };
                            Process.Start(batPsi);
                            UpdaterHelper.Log($"[Update] Fallback .bat executado: {batPath}");
                        }
                        catch (Exception bex)
                        {
                            UpdaterHelper.Log($"[Update] Falha no fallback .bat: {bex.Message}");
                        }
                    }
                }
                else
                {
                    UpdaterHelper.Log("[Update] Erro: O executável principal não foi encontrado após a atualização.");
                    SetStatus("Erro: executável principal não encontrado.");
                }
            });
        }

        // Etapa: Limpeza do diretório de origem
        private async Task CleanupSourceDirSafeAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    if (_sourceDir != null && Directory.Exists(_sourceDir))
                    {
                        UpdaterHelper.Log($"[Update] Limpando diretório temporário: {_sourceDir}");
                        Directory.Delete(_sourceDir, true);
                        UpdaterHelper.Log("[Update] Limpeza concluída.");
                    }
                }
                catch (Exception ex)
                {
                    UpdaterHelper.Log($"[Update] Erro ao limpar o diretório temporário: {ex.Message}");
                }
            });
        }

        // Etapa: finalizar e fechar janela
        private async Task FinishAndCloseAsync()
        {
            try { await Task.Delay(800); } catch { }
            try { Dispatcher.Invoke(Close); } catch { }
        }
    }
}
