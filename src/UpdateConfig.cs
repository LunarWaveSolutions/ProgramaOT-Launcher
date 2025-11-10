using System;

namespace ProgramaOTLauncher
{
    // Centraliza configuração de endpoints e credenciais para atualização do cliente
    public static class UpdateConfig
    {
        // Defina aqui o repositório que hospeda as Releases do cliente
        public const string Owner = "LunarWaveSolutions";
        public const string Repo = "ProgramaOT-Cliente";

        // URLs derivadas
        public static string ReleasesApiLatest => $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
        public static string AssetClientZipLatestPublic => $"https://github.com/{Owner}/{Repo}/releases/latest/download/client-to-update.zip";
        public static string RawLauncherConfigUrl => $"https://raw.githubusercontent.com/{Owner}/{Repo}/main/launcher_config.json";

        // Token de acesso ao GitHub para repositório privado (opcional).
        // Recomendado definir via variável de ambiente: CANARY_GITHUB_TOKEN
        public static string GitHubToken
        {
            get
            {
                try { return Environment.GetEnvironmentVariable("PROGRAMAOT_GITHUB_TOKEN") ?? Environment.GetEnvironmentVariable("CANARY_GITHUB_TOKEN") ?? string.Empty; }
                catch { return string.Empty; }
            }
        }
    }
}



