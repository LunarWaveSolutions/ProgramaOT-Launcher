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
                    return info;

                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("programaot-launcher");
                var token = UpdateConfig.GitHubToken;
                if (!string.IsNullOrWhiteSpace(token))
                {
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", token);
                }

                var (ownerForLauncher, repoForLauncher) = ParseOwnerRepo(config.launcherUpdateEndpoint);
                if (string.IsNullOrWhiteSpace(ownerForLauncher) || string.IsNullOrWhiteSpace(repoForLauncher))
                {
                    ownerForLauncher = UpdateConfig.Owner;
                    repoForLauncher = UpdateConfig.Repo;
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
                        info.AssetUrl = asset.Value<string>("browser_download_url") ?? "";
                        var idToken = asset.Value<int?>("id");
                        if (idToken.HasValue)
                        {
                            info.AssetApiUrl = $"https://api.github.com/repos/{ownerForLauncher}/{repoForLauncher}/releases/assets/{idToken.Value}";
                        }
                    }
                }

                info.ChecksumUrl = config.launcherChecksumUrl ?? "";

                // CORREÇÃO CRÍTICA: Comparação robusta de versões
                bool hasUpdate = CompareVersions(installedVersion, latestTag);
                info.HasUpdate = hasUpdate;

                try
                {
                    Logger.Info($"LauncherUpdateService.CheckAsync: installedVersion='{installedVersion}', latestTag='{latestTag}', hasUpdate={hasUpdate}");
                }
                catch { }

                // Mandatory se installedVersion < launcherMinVersion
                if (!string.IsNullOrWhiteSpace(config.launcherMinVersion) && !string.IsNullOrWhiteSpace(installedVersion))
                {
                    try
                    {
                        if (CompareVersions(installedVersion, config.launcherMinVersion))
                        {
                            info.Mandatory = true;
                            info.HasUpdate = true;
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Logger.Error("Erro ao verificar atualização do launcher", ex);
                }
                catch { }
            }
            return info;
        }

        /// <summary>
        /// Compara duas versões e retorna true se 'installed' é MENOR que 'latest' (precisa atualizar)
        /// </summary>
        private static bool CompareVersions(string installed, string latest)
        {
            try
            {
                var cleanInstalled = CleanTag(installed);
                var cleanLatest = CleanTag(latest);

                // Log para debug
                try
                {
                    Logger.Info($"CompareVersions: cleanInstalled='{cleanInstalled}', cleanLatest='{cleanLatest}'");
                }
                catch { }

                // Se forem exatamente iguais, não tem update
                if (string.Equals(cleanInstalled, cleanLatest, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // Tenta comparar como versões numéricas (1.0, 2.0, etc)
                var installedVer = TryParseVersion(cleanInstalled);
                var latestVer = TryParseVersion(cleanLatest);

                if (installedVer != null && latestVer != null)
                {
                    bool needsUpdate = latestVer > installedVer;
                    try
                    {
                        Logger.Info($"CompareVersions: Comparação numérica - {installedVer} vs {latestVer} = {needsUpdate}");
                    }
                    catch { }
                    return needsUpdate;
                }

                // Tenta comparar como timestamps (formato: 20251110-0756)
                var installedTimestamp = TryParseTimestamp(cleanInstalled);
                var latestTimestamp = TryParseTimestamp(cleanLatest);

                if (installedTimestamp != null && latestTimestamp != null)
                {
                    bool needsUpdate = latestTimestamp > installedTimestamp;
                    try
                    {
                        Logger.Info($"CompareVersions: Comparação timestamp - {installedTimestamp} vs {latestTimestamp} = {needsUpdate}");
                    }
                    catch { }
                    return needsUpdate;
                }

                // Se um é timestamp e outro é versão numérica, timestamp é sempre mais novo
                if (latestTimestamp != null && installedVer != null)
                {
                    try
                    {
                        Logger.Info($"CompareVersions: Latest é timestamp, installed é versão numérica - assume update=true");
                    }
                    catch { }
                    return true;
                }

                // Se não conseguiu comparar de forma estruturada, compara como string
                bool stringDiffers = !string.Equals(cleanInstalled, cleanLatest, StringComparison.OrdinalIgnoreCase);
                try
                {
                    Logger.Info($"CompareVersions: Fallback para comparação string - {stringDiffers}");
                }
                catch { }
                return stringDiffers;
            }
            catch (Exception ex)
            {
                try
                {
                    Logger.Error($"Erro ao comparar versões: '{installed}' vs '{latest}'", ex);
                }
                catch { }
                // Em caso de erro, assume que não tem update (safer)
                return false;
            }
        }

        // Tenta fazer parse de versão numérica (ex: "1.0", "2.1.3")
        private static Version TryParseVersion(string versionStr)
        {
            if (string.IsNullOrWhiteSpace(versionStr))
                return null;

            // Remove qualquer caractere não numérico ou ponto
            var cleaned = new string(versionStr.Where(c => char.IsDigit(c) || c == '.').ToArray());
            if (string.IsNullOrWhiteSpace(cleaned))
                return null;

            // Garante pelo menos major.minor
            var parts = cleaned.Split('.');
            if (parts.Length == 1)
                cleaned += ".0";

            if (Version.TryParse(cleaned, out var ver))
                return ver;

            return null;
        }

        // Tenta fazer parse de timestamp (formato: YYYYMMDD-HHMM ou variações)
        private static DateTime? TryParseTimestamp(string timestampStr)
        {
            if (string.IsNullOrWhiteSpace(timestampStr))
                return null;

            // Formato esperado: 20251110-0756 (YYYYMMDD-HHMM)
            var cleaned = new string(timestampStr.Where(c => char.IsDigit(c) || c == '-').ToArray());

            // Tenta diferentes formatos
            var formats = new[]
            {
                "yyyyMMdd-HHmm",   // 20251110-0756
                "yyyyMMdd",         // 20251110
                "yyyyMMddHHmm",     // 202511100756
                "yyyy-MM-dd-HHmm"   // 2025-11-10-0756
            };

            foreach (var format in formats)
            {
                if (DateTime.TryParseExact(cleaned, format,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var dt))
                {
                    return dt;
                }
            }

            return null;
        }

        private static (string owner, string repo) ParseOwnerRepo(string endpoint)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(endpoint)) return ("", "");
                var uri = new Uri(endpoint);
                var segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                var reposIdx = Array.IndexOf(segments, "repos");
                if (reposIdx >= 0 && reposIdx + 2 < segments.Length)
                {
                    var owner = segments[reposIdx + 1];
                    var repo = segments[reposIdx + 2];
                    return (owner, repo);
                }
            }
            catch { }
            return ("", "");
        }

        private static string CleanTag(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return "";
            t = t.Trim();
            if (t.StartsWith("auto-", StringComparison.OrdinalIgnoreCase))
                t = t.Substring("auto-".Length);
            if (t.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                t = t.Substring(1);
            return t;
        }
    }
}