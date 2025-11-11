using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

public class UpdaterHelper
{
    private static string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update_helper_log.txt");

    [STAThread]
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

        // Iniciar UI WPF de progresso
        var app = new Application();
        var window = new UpdaterHelperUI.ProgressWindow(sourceDir!, targetDir!, processId, versionTag, sourceUrl, zipChecksum);
        app.Run(window);

        Log("UpdaterHelper a terminar.");
    }

    public static void WriteVersionLauncherJson(string? targetDir, string? versionTag, string? sourceUrl, string? zipChecksum)
    {
        if (string.IsNullOrWhiteSpace(targetDir)) return;
        // Após a verificação acima, garantimos que targetDir não é nulo/whitespace
        var target = targetDir!;
        var filePath = Path.Combine(target, "versionlauncher.json");

        string installedAtUtc = DateTime.UtcNow.ToString("o");

        // Tentar obter a versão do executável principal
        string? appVersion = null;
        try
        {
            var exePath = Path.Combine(target, "ProgramaOT-Launcher.exe");
            if (File.Exists(exePath))
            {
                var fvi = FileVersionInfo.GetVersionInfo(exePath);
                if (fvi != null)
                {
                    appVersion = fvi.ProductVersion ?? fvi.FileVersion;
                }
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
            var s = t?.Trim() ?? string.Empty;
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
        try { Log($"versionlauncher.json salvo em: {filePath}\nConteúdo: {json}"); } catch { }

        // Padronização: utilizar apenas versionlauncher.json
    }

    /// <summary>
    /// Atualiza o arquivo local launcher_config.json no diretório de destino, garantindo que
    /// os campos relacionados à versão do launcher reflitam a versão instalada.
    /// Não depende de bibliotecas externas de JSON: usa Regex para atualizar os valores.
    /// </summary>
    /// <param name="targetDir">Diretório onde o launcher foi instalado/atualizado.</param>
    /// <param name="versionTag">Tag de versão recebida (pode incluir prefixos visuais como 'v').</param>
    public static void UpdateLauncherConfigVersion(string? targetDir, string? versionTag)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(targetDir))
            {
                Log("UpdateLauncherConfigVersion: targetDir vazio.");
                return;
            }

            // targetDir é garantido não nulo/whitespace pelo retorno acima
            var target = targetDir!;
            var configPath = Path.Combine(target, "launcher_config.json");
            if (!File.Exists(configPath))
            {
                Log($"UpdateLauncherConfigVersion: arquivo não encontrado em {configPath}. Nada a atualizar.");
                return;
            }

            string NormalizeTagForStore(string? t)
            {
                var s = t?.Trim() ?? string.Empty;
                if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s.Substring(1);
                if (s.StartsWith("auto-", StringComparison.OrdinalIgnoreCase)) s = s.Substring("auto-".Length);
                return s;
            }
            var cleanTag = NormalizeTagForStore(versionTag);

            var original = File.ReadAllText(configPath);

            // Usa Regex para substituir valores de launcherVersion e launcherMinVersion, se existirem
            // Padrões tolerantes a espaços e diferentes formatos
            string ReplaceJsonStringValue(string input, string field, string newValue)
            {
                try
                {
                    // Importante: em strings C#, a barra invertida precisa ser duplicada para gerar \s no regex
                    // Assim evitamos o erro CS1009 (sequência de escape não reconhecida)
                    var pattern = $"\"{field}\"\\s*:\\s*\"[^\"]*\"";
                    var replacement = $"\"{field}\": \"{newValue}\"";
                    var result = System.Text.RegularExpressions.Regex.Replace(input, pattern, replacement);
                    return result;
                }
                catch (Exception rex)
                {
                    Log($"Regex replace falhou para {field}: {rex.Message}");
                    return input;
                }
            }

            var updated = ReplaceJsonStringValue(original, "launcherVersion", cleanTag);
            // Atualiza launcherMinVersion apenas se existir no arquivo; manter compatibilidade
            updated = ReplaceJsonStringValue(updated, "launcherMinVersion", cleanTag);

            if (!string.Equals(original, updated, StringComparison.Ordinal))
            {
                File.WriteAllText(configPath, updated);
                Log($"launcher_config.json atualizado com versão {cleanTag} em: {configPath}");
            }
            else
            {
                Log("UpdateLauncherConfigVersion: nenhum campo alterado (possivelmente campos ausentes no JSON).");
            }
        }
        catch (Exception ex)
        {
            Log($"Falha ao atualizar launcher_config.json: {ex.Message}");
        }
    }

    // Método antigo mantido apenas caso seja reutilizado em outros cenários
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

    public static void Log(string message)
    {
        File.AppendAllText(logFilePath, string.Format("[{0}] {1}\n", DateTime.Now, message));
    }
}
