using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using LauncherConfig;

namespace ProgramaOTLauncher
{
    public class LauncherUpdateInfo
    {
        public string LatestTag { get; set; } = "";
        public string AssetUrl { get; set; } = "";
        public string AssetApiUrl { get; set; } = "";
        public string AssetName { get; set; } = "";
        public string ChecksumUrl { get; set; } = "";
        public bool Mandatory { get; set; } = false;
        public bool HasUpdate { get; set; } = false;
    }

    public static class LauncherUpdateService
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public static async Task<LauncherUpdateInfo> CheckAsync(ClientConfig config, string installedVersion)
        {
            var info = new LauncherUpdateInfo();
            try
            {
                if (string.IsNullOrWhiteSpace(config.launcherUpdateEndpoint))
                    return info; // Sem endpoint, nada a fazer

                // GitHub requer User-Agent
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("programaot-launcher");
                var token = UpdateConfig.GitHubToken;
                if (!string.IsNullOrWhiteSpace(token))
                {
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", token);
                }

                var response = await httpClient.GetAsync(config.launcherUpdateEndpoint);
                if (!response.IsSuccessStatusCode)
                    return info;

                var json = await response.Content.ReadAsStringAsync();
                var obj = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(json);
                if (obj == null)
                    return info;

                var latestTag = obj.Value<string>("tag_name") ?? "";
                info.LatestTag = latestTag;

                var assets = obj["assets"] as Newtonsoft.Json.Linq.JArray;
                if (assets != null && !string.IsNullOrWhiteSpace(config.launcherAssetName))
                {
                    var asset = assets.FirstOrDefault(a => a["name"]?.ToString() == config.launcherAssetName);
                    if (asset != null)
                    {
                        info.AssetName = config.launcherAssetName;
                        // Para repositórios públicos, browser_download_url funciona; para privados, ideal é usar API de assets com Accept: application/octet-stream
                        info.AssetUrl = asset.Value<string>("browser_download_url") ?? "";
                        var idToken = asset.Value<int?>("id");
                        if (idToken.HasValue)
                        {
                            // API de assets para download com Authorization (repo privado)
                            info.AssetApiUrl = $"https://api.github.com/repos/{UpdateConfig.Owner}/{UpdateConfig.Repo}/releases/assets/{idToken.Value}";
                        }
                    }
                }

                info.ChecksumUrl = config.launcherChecksumUrl ?? "";

                // Decide se há update: somente se tag (latest) for MAIOR que a instalada
                bool tagDiffers = false;
                try
                {
                    var cur = NormalizeVersion(installedVersion);
                    var latest = NormalizeVersion(CleanTag(latestTag));
                    if (cur != null && latest != null && latest > cur)
                        tagDiffers = true;
                }
                catch { }

                // Fallback para tags não SemVer: se não conseguir normalizar, considerar diferença de texto como update
                if (!tagDiffers)
                {
                    var curText = CleanTag(installedVersion);
                    var latestText = CleanTag(latestTag);
                    if (!string.IsNullOrWhiteSpace(curText) && !string.IsNullOrWhiteSpace(latestText)
                        && !string.Equals(curText, latestText, StringComparison.OrdinalIgnoreCase))
                    {
                        tagDiffers = true;
                    }
                }

                // Atualização disponível quando detectamos diferença
                info.HasUpdate = tagDiffers;

                // Mandatory se installedVersion < launcherMinVersion
                if (!string.IsNullOrWhiteSpace(config.launcherMinVersion) && !string.IsNullOrWhiteSpace(installedVersion))
                {
                    try
                    {
                        var cur = NormalizeVersion(installedVersion);
                        var min = NormalizeVersion(config.launcherMinVersion);
                        if (cur != null && min != null && cur < min)
                        {
                            info.Mandatory = true;
                            info.HasUpdate = true; // se é obrigatório, há update
                        }
                    }
                    catch { }
                }
            }
            catch
            {
                // Não falha o launcher em caso de erro de checagem
            }
            return info;
        }

        // Normaliza versões tipo "1.0" ou "1.0.0" em System.Version
        private static Version NormalizeVersion(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return null;
            // Garante ter ao menos major.minor
            var parts = v.Split('.');
            if (parts.Length == 1) v += ".0";
            if (parts.Length == 2) v += ".0";
            if (Version.TryParse(v, out var ver)) return ver;
            return null;
        }

        // Remove prefixo "v" ou "V" de tags (ex.: "v1.0.0" -> "1.0.0") para comparação SemVer
        private static string CleanTag(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return t;
            t = t.Trim();
            if (t.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                t = t.Substring(1);
            return t;
        }
    }
}
