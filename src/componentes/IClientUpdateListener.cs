using System.ComponentModel;

namespace ProgramaOTLauncher.componentes
{
    public interface IClientUpdateListener
    {
        void SetAppVersion(string version);
        void ShowDownloadButton();
        void ShowPlayButton();
        void ShowUpdateButton();
        void CloseWindow();
        void ShowProgress();
        void SetDownloadPercentage(int percentage);
        void SetDownloadStatus(string status);
        void HideProgress();
        void ShowMessage(string message, string title);
    }
}
