using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

public class UpdaterHelper
{
    private static string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update_helper_log.txt");

    public static void Main(string[] args)
    {
        File.WriteAllText(logFilePath, string.Format("[{0}] UpdaterHelper iniciado.\n", DateTime.Now));

        if (args.Length < 3)
        {
            Log("Erro: Argumentos insuficientes. Uso: UpdaterHelper.exe <sourceDir> <targetDir> <processId>");
            return;
        }

        string? sourceDir = args[0];
        string? targetDir = args[1];
        int processId = int.Parse(args[2]);

        Log(string.Format("Source: {0}", sourceDir));
        Log(string.Format("Target: {0}", targetDir));
        Log(string.Format("Process ID: {0}", processId));

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
            return;
        }

        try
        {
            Log("A iniciar a cópia de ficheiros...");
            if (sourceDir != null && targetDir != null)
            {
                CopyDirectory(sourceDir, targetDir, 5);
            }
            Log("Cópia de ficheiros concluída.");

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
