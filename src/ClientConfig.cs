using System;
using System.Windows;
using System.Net;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ProgramaOTLauncher;

namespace LauncherConfig
{
	public class ClientConfig
	{
		public string clientVersion { get; set; }
		public string launcherVersion { get; set; }
		public bool replaceFolders { get; set; }
		public ReplaceFolderName[] replaceFolderName { get; set; }
		public string clientFolder { get; set; }
		public string newClientUrl { get; set; }
		public string newConfigUrl { get; set; }
		public string clientExecutable { get; set; }

		public static ClientConfig loadFromFile(string url)
		{
			try
			{
				using (HttpClient client = new HttpClient())
				{
					// GitHub requer User-Agent; adiciona token se existir para acessar repo privado
					client.DefaultRequestHeaders.UserAgent.ParseAdd("programaot-launcher");
					var token = UpdateConfig.GitHubToken;
					if (!string.IsNullOrWhiteSpace(token))
					{
						client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", token);
					}
					Task<string> jsonTask = client.GetStringAsync(url);
					string jsonString = jsonTask.Result;
					return JsonConvert.DeserializeObject<ClientConfig>(jsonString);
				}
			}
			catch
			{
				// Fallback: tenta ler arquivo local launcher_config.json na pasta do launcher
				try
				{
					string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher_config.json");
					if (File.Exists(localPath))
					{
						string jsonString = File.ReadAllText(localPath);
						return JsonConvert.DeserializeObject<ClientConfig>(jsonString);
					}
				}
				catch { }
				// Se nada funcionar, retorna um config mínimo para evitar crash
				return new ClientConfig
				{
					clientVersion = "",
					launcherVersion = "",
					replaceFolders = false,
					replaceFolderName = new ReplaceFolderName[0],
					clientFolder = "",
					newClientUrl = "",
					newConfigUrl = "",
					clientExecutable = "client.exe"
				};
			}
		}
	}

	public class ReplaceFolderName
	{
		public string name { get; set; }
	}
}


