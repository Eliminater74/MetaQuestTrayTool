using System.Diagnostics;

namespace MetaQuestTrayTool.Services;

public static class DonateService
{
    public static void Open(System.Windows.Window? owner = null)
    {
        if (!AppInfo.HasDonateUrl)
        {
            System.Windows.MessageBox.Show(
                owner,
                $"Thanks for supporting {AppInfo.ProductName}.\n\nThe donate link is not set yet. Paste it into AppInfo.DonateUrl when the page is ready.",
                AppInfo.ProductName,
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = AppInfo.DonateUrl,
            UseShellExecute = true
        });
    }
}
