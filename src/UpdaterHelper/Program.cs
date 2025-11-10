using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Linq;

public class UpdaterHelper
{
    private static string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update_helper_log.txt");

    public static void Main(string[] args)
    {
        // Preservar histórico de execução
        File.AppendAllText(logFilePath, string.Format("[{0}] UpdaterHelper iniciado.\n", DateTime.Now));
        File.AppendAllText(logFilePath, string.Format("[{0}] Args length: {1}\n", DateTime.Now, args.Length));
        foreach (var a in args)
        {
            File.AppendAllText(logFilePath, string.Format("[{0}] Arg: {1}\n", DateTime.Now, a));
        }
        
        // Suporte a dois formatos de argumentos:
        // 1) Posicionais: UpdaterHelper.exe <sourceDir> <targetDir> <processId>
        // 2) Nomeados: --source-dir <path> --target-dir <path> --pid <id>
        string? sourceDir = null;
        string? targetDir = null;
        int processId = 0;
        string? versionTag = null;
        string? sourceUrl = null;
        string? zipChecksum = null;

        // Tenta primeiro flags nomeadas em formato separado
        int srcIdx = Array.IndexOf(args, "--source-dir");
        if (srcIdx >= 0 && srcIdx + 1 < args.Length)
        {
            sourceDir = args[srcIdx + 1];
        }

        int tgtIdx = Array.IndexOf(args, "--target-dir");
        if (tgtIdx >= 0 && tgtIdx + 1 < args.Length)
        {
            targetDir = args[tgtIdx + 1];
        }

        int pidIdx = Array.IndexOf(args, "--pid");
        if (pidIdx >= 0 && pidIdx + 1 < args.Length)
        {
            int.TryParse(args[pidIdx + 1], out processId);
        }

        int verIdx = Array.IndexOf(args, "--version-tag");
        if (verIdx >= 0 && verIdx + 1 < args.Length)
        {
            versionTag = args[verIdx + 1];
        }

        int srcUrlIdx = Array.IndexOf(args, "--source-url");
        if (srcUrlIdx >= 0 && srcUrlIdx + 1 < args.Length)
        {
            sourceUrl = args[srcUrlIdx + 1];
        }

        int zipIdx = Array.IndexOf(args, "--zip-checksum");
        if (zipIdx >= 0 && zipIdx + 1 < args.Length)
        {
            zipChecksum = args[zipIdx + 1];
        }

        // Também suporta formato --flag=valor
        foreach (var a in args)
        {
            if (a.StartsWith("--source-dir="))
            {
                sourceDir = a.Substring("--source-dir=".Length);
            }
            else if (a.StartsWith("--target-dir="))
            {
                targetDir = a.Substring("--target-dir=".Length);
            }
            else if (a.StartsWith("--pid="))
            {
                int.TryParse(a.Substring("--pid=".Length), out processId);
            }
            else if (a.StartsWith("--version-tag="))
            {
                versionTag = a.Substring("--version-tag=".Length);
            }
            else if (a.StartsWith("--source-url="))
            {
                sourceUrl = a.Substring("--source-url=".Length);
            }
            else if (a.StartsWith("--zip-checksum="))
            {
                zipChecksum = a.Substring("--zip-checksum=".Length);
            }
        }

        // Fallback para argumentos posicionais
        if (string.IsNullOrWhiteSpace(sourceDir) || string.IsNullOrWhiteSpace(targetDir) || processId == 0)
        {
            if (args.Length >= 3)
            {
                sourceDir = sourceDir ?? args.ElementAtOrDefault(0);
                targetDir = targetDir ?? args.ElementAtOrDefault(1);
                if (processId == 0)
                {
                    int.TryParse(args.ElementAtOrDefault(2), out processId);
                }
            }
        }

        if (string.IsNullOrWhiteSpace(sourceDir) || string.IsNullOrWhiteSpace(targetDir) || processId == 0)
        {
            Log("Aviso: Argumentos insuficientes. Tentando inferir caminhos padrão.");
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var inferredSource = Path.Combine(baseDir, "payload");
                var inferredTarget = Directory.GetParent(baseDir)?.FullName ?? ".";

                if (Directory.Exists(inferredSource) && Directory.Exists(inferredTarget))
                {
                    sourceDir = sourceDir ?? inferredSource;
                    targetDir = targetDir ?? inferredTarget;
                    Log(string.Format("Inferência bem-sucedida. Source={0} Target={1}", sourceDir, targetDir));
                }
                else
                {
                    Log(string.Format("Falha na inferência: payload existe? {0}; target existe? {1}", Directory.Exists(inferredSource), Directory.Exists(inferredTarget)));
                }
            }
            catch (Exception iex)
            {
                Log(string.Format("Erro durante inferência de caminhos: {0}", iex.Message));
            }
        }

        if (string.IsNullOrWhiteSpace(sourceDir) || string.IsNullOrWhiteSpace(targetDir))
        {
            Log("Erro: Caminhos de origem/destino não definidos. Uso: UpdaterHelper.exe <sourceDir> <targetDir> <processId> ou --source-dir <path> --target-dir <path> --pid <id>");
            return;
        }

        Log(sourceDir != null && tgtIdx >= 0 ? "Modo argumentos nomeados" : "Modo argumentos posicionais");

        Log(string.Format("Source: {0}", sourceDir));
        Log(string.Format("Target: {0}", targetDir));
        Log(string.Format("Process ID: {0}", processId));
        if (!string.IsNullOrWhiteSpace(versionTag)) Log(string.Format("VersionTag: {0}", versionTag));
        if (!string.IsNullOrWhiteSpace(sourceUrl)) Log(string.Format("SourceUrl: {0}", sourceUrl));
        if (!string.IsNullOrWhiteSpace(zipChecksum)) Log(string.Format("ZipChecksum: {0}", zipChecksum));

        if (processId != 0)
        {
            try
            {
                Process parentProcess = Process.GetProcessById(processId);
                Log(string.Format("A aguardar que o processo principal (ID: {0}) termine...", processId));
                parentProcess.WaitForExit();
                Log("Processo principal terminado.");
            }
            catch (ArgumentException)
            {
                Log("O processo principal já não está em execução. A continuar com a atualização.");
            }
            catch (Exception ex)
            {
                Log(string.Format("Erro ao aguardar pelo processo principal: {0}", ex.Message));
            }
        }
        else
        {
            Log("ProcessId não fornecido. A aplicar atualização sem aguardar término explícito.");
            Thread.Sleep(1000);
        }

        try
        {
            Log("A iniciar a cópia de ficheiros...");
            if (sourceDir != null && targetDir != null)
            {
                CopyDirectory(sourceDir, targetDir, 5);
            }
            Log("Cópia de ficheiros concluída.");

            // Escrever arquivo de versão do launcher (versionlauncher.json)
            try
            {
                WriteVersionLauncherJson(targetDir, versionTag, sourceUrl, zipChecksum);
                Log("versionlauncher.json escrito/atualizado com sucesso.");
            }
            catch (Exception vex)
            {
                Log(string.Format("Falha ao escrever versionlauncher.json: {0}", vex.Message));
            }

            string mainAppPath = Path.Combine(targetDir ?? ".", "ProgramaOT-Launcher.exe");
            Log(string.Format("A tentar reiniciar a aplicação principal em: {0}", mainAppPath));
            if (File.Exists(mainAppPath))
            {
                Process.Start(mainAppPath);
                Log("Aplicação principal reiniciada.");
            }
            else
            {
                Log("Erro: O executável principal não foi encontrado após a atualização.");
            }
        }
        catch (Exception ex)
        {
            Log(string.Format("Ocorreu um erro durante o processo de atualização: {0}", ex.Message));
        }
        finally
        {
            try
            {
                if (sourceDir != null && Directory.Exists(sourceDir))
                {
                    Log(string.Format("A limpar o diretório temporário: {0}", sourceDir));
                    Directory.Delete(sourceDir, true);
                    Log("Limpeza concluída.");
                }
            }
            catch (Exception ex)
            {
                Log(string.Format("Erro ao limpar o diretório temporário: {0}", ex.Message));
            }
        }

        Log("UpdaterHelper a terminar.");
    }

    private static void WriteVersionLauncherJson(string? targetDir, string? versionTag, string? sourceUrl, string? zipChecksum)
    {
        if (string.IsNullOrWhiteSpace(targetDir)) return;
        var filePath = Path.Combine(targetDir, "versionlauncher.json");
        var obsoleteAlias1 = Path.Combine(targetDir, "launchversion.json");
        var obsoleteAlias2 = Path.Combine(targetDir, "launchversions.json");

        string installedAtUtc = DateTime.UtcNow.ToString("o");

        // Tentar obter a versão do executável principal
        string? appVersion = null;
        try
        {
            var exePath = Path.Combine(targetDir, "ProgramaOT-Launcher.exe");
            if (File.Exists(exePath))
            {
                var fvi = FileVersionInfo.GetVersionInfo(exePath);
                appVersion = fvi.ProductVersion ?? fvi.FileVersion;
            }
        }
        catch { }

        string Escape(string? s)
        {
            // Evita qualquer possível desreferência nula usando coalescência segura
            var nonNull = s ?? string.Empty;
            return nonNull.Replace("\\", "\\\\")
                          .Replace("\"", "\\\"")
                          .Replace("\n", "\\n")
                          .Replace("\r", "\\r");
        }

        // Normalizar tag para armazenamento: remover prefixos como 'v' e 'auto-'
        string NormalizeTagForStore(string? t)
        {
            if (string.IsNullOrWhiteSpace(t)) return "";
            var s = t.Trim();
            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s.Substring(1);
            if (s.StartsWith("auto-", StringComparison.OrdinalIgnoreCase)) s = s.Substring("auto-".Length);
            return s;
        }
        var storedTag = NormalizeTagForStore(versionTag);

        var json = "{" +
                   "\n  \"installedAtUtc\": \"" + Escape(installedAtUtc) + "\"," +
                   "\n  \"versionTag\": \"" + Escape(storedTag) + "\"," +
                   "\n  \"sourceUrl\": \"" + Escape(sourceUrl) + "\"," +
                   "\n  \"zipChecksumSha256\": \"" + Escape(zipChecksum) + "\"," +
                   "\n  \"appFileVersion\": \"" + Escape(appVersion) + "\"" +
                   "\n}";

        File.WriteAllText(filePath, json);

        // Remover aliases antigos se existirem, para unificar em um único arquivo
        try { if (File.Exists(obsoleteAlias1)) { File.Delete(obsoleteAlias1); Log("Obsoleto removido: launchversion.json"); } } catch { }
        try { if (File.Exists(obsoleteAlias2)) { File.Delete(obsoleteAlias2); Log("Obsoleto removido: launchversions.json"); } } catch { }
    }

    private static void CopyDirectory(string source, string target, int retries)
    {
        if (target != null && !Directory.Exists(target))
        {
            Directory.CreateDirectory(target);
        }

        DirectoryInfo sourceInfo = new DirectoryInfo(source);
        foreach (FileInfo file in sourceInfo.GetFiles())
        {
            string targetFilePath = Path.Combine(target ?? ".", file.Name);
            Log(string.Format("A copiar {0} para {1}", file.FullName, targetFilePath));
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    file.CopyTo(targetFilePath, true);
                    Log("Cópia bem-sucedida.");
                    break;
                }
                catch (IOException ex)
                {
                    Log(string.Format("Tentativa {0}: Erro de IO ao copiar {1} - {2}", i + 1, file.Name, ex.Message));
                    if (i == retries - 1) throw;
                    Thread.Sleep(1000);
                }
            }
        }

        foreach (DirectoryInfo subDir in sourceInfo.GetDirectories())
        {
            string newTargetDir = Path.Combine(target ?? ".", subDir.Name);
            CopyDirectory(subDir.FullName, newTargetDir, retries);
        }
    }

    private static void Log(string message)
    {
        File.AppendAllText(logFilePath, string.Format("[{0}] {1}\n", DateTime.Now, message));
    }
}
