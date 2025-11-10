using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ProgramaOTLauncher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public class Program
	{
		[STAThread]
		public static void Main(string[] args)
		{
			// Se o launcher foi iniciado para aplicar atualização, faz o processo e encerra
			if (SelfUpdateHelper.TryApplyUpdate(args))
			{
				return;
			}

			App app = new App();
			app.InitializeComponent();
			app.Run();
		}
	}

	internal static class SelfUpdateHelper
	{
		public static bool TryApplyUpdate(string[] args)
		{
			string mode = GetArg(args, "--apply-update");
			bool doUpdate = !string.IsNullOrEmpty(mode) || args.Any(a => string.Equals(a, "--apply-update", StringComparison.OrdinalIgnoreCase));
			if (!doUpdate) return false;

			string source = GetArg(args, "--source");
			string target = GetArg(args, "--target");
			string exe = GetArg(args, "--exe");
			string waitPidStr = GetArg(args, "--waitpid");

			int waitPid = 0;
			int.TryParse(waitPidStr, out waitPid);

			// Aguarda o processo anterior encerrar (se fornecido)
			if (waitPid > 0)
			{
				try
				{
					while (true)
					{
						var proc = Process.GetProcesses().FirstOrDefault(p => p.Id == waitPid);
						if (proc == null) break;
						System.Threading.Thread.Sleep(200);
					}
				}
				catch { }
			}

			if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
			{
				// Parâmetros inválidos; não processa como atualização
				return false;
			}

			try
			{
				CopyDirectory(source, target);
			}
			catch
			{
				// Se falhar, ainda encerra para evitar ficar preso; poderia registrar log
			}

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

			// Como este processo foi iniciado apenas para aplicar update, encerra sem abrir UI
			return true;
		}

		private static string GetArg(string[] args, string name)
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

		private static void CopyDirectory(string sourceDir, string targetDir)
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

