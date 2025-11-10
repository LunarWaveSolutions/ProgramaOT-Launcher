using System;
using LauncherConfig;

namespace ProgramaOTLauncher.componentes
{
    public static class PathHelper
    {
        public static string GetLauncherPath(ClientConfig clientConfig, bool onlyBaseDirectory = false)
        {
            string launcherPath = "";
            if (string.IsNullOrEmpty(clientConfig.clientFolder) || onlyBaseDirectory)
            {
                launcherPath = AppDomain.CurrentDomain.BaseDirectory.ToString();
            }
            else
            {
                launcherPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory.ToString(), clientConfig.clientFolder);
            }

            return launcherPath;
        }
    }
}
