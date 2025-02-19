using System.Diagnostics;

namespace sachssoft.SimpleSerialTool
{
    internal static class Utils
    {
        internal static void ShowBrowser(string url)
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = url,
                UseShellExecute = true
            });
        }

    }
}
