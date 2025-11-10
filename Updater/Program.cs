using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace ProgramaOTLauncherUpdater
{
    class Program
    {
        static int Main(string[] args)
        {
            string source = GetArg(args, "--source");
            string target = GetArg(args, "--target");
            string exe = GetArg(args, "--exe");
            string waitPidStr = GetArg(args, "--waitpid");

            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
            {
                Console.Error.WriteLine("Parâmetros inválidos. Uso: Updater.exe --source <dir> --target <dir> --exe <nome.exe> --waitpid <pid>");
                return 2;
            }

            int waitPid = 0;
            int.TryParse(waitPidStr, out waitPid);

            // Aguarda o processo do Launcher encerrar
            if (waitPid > 0)
            {
                try
                {
                    while (true)
                    {
                        var proc = Process.GetProcesses().FirstOrDefault(p => p.Id == waitPid);
                        if (proc == null) break;
                        Thread.Sleep(300);
                    }
                }
                catch { }
            }

            // Tenta substituir arquivos
            try
            {
                CopyDirectory(source, target);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Falha ao atualizar arquivos: " + ex.Message);
                return 3;
            }

            // Relança o Launcher
            try
            {
                if (!string.IsNullOrWhiteSpace(exe))
                {
                    string exePath = Path.Combine(target, exe);
                    if (File.Exists(exePath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = exePath,
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch { }

            return 0;
        }

        static string GetArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length) return args[i + 1];
                    return string.Empty;
                }
                if (args[i].StartsWith(name + "="))
                {
                    return args[i].Substring(name.Length + 1).Trim('"');
                }
            }
            return string.Empty;
        }

        static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relative = file.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var dest = Path.Combine(targetDir, relative);
                var destDir = Path.GetDirectoryName(dest);
                if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                File.Copy(file, dest, true);
            }
        }
    }
}

