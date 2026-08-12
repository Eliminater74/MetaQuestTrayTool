using System.Windows;

namespace MetaQuestTrayTool.Services;

internal static class StartupUiHelper
{
    public static bool TryApply(Window? owner, bool startWithWindows, bool asAdministrator)
    {
        try
        {
            var result = App.Instance.StartupRegistration.Apply(startWithWindows, asAdministrator);
            var settings = App.Instance.Settings.Current;
            settings.StartWithWindows = result.StartWithWindows;
            settings.StartWithWindowsAsAdministrator = result.AsAdministrator;
            settings.AutomaticElevation = result.AsAdministrator;
            App.Instance.Settings.Save();

            App.Instance.Log.Info(result.AsAdministrator
                ? "Enabled hands-free Administrator start (elevated logon task)."
                : result.StartWithWindows
                    ? "Enabled Start with Windows (standard user)."
                    : "Disabled Start with Windows.");

            if (result.RecommendRestartElevated)
            {
                App.Instance.StartupRegistration.RestartElevated();
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            App.Instance.Log.Warn("Startup change cancelled at the UAC prompt.");
            return false;
        }
        catch (Exception ex)
        {
            App.Instance.Log.Error("Could not update Windows startup.", ex);
            System.Windows.MessageBox.Show(
                owner,
                $"Could not update Windows startup.\n\n{ex.Message}",
                App.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
    }

    public static void TryRestartElevated(Window? owner)
    {
        if (App.Instance.StartupRegistration.IsProcessElevated)
        {
            System.Windows.MessageBox.Show(
                owner,
                "This session already has Administrator rights.",
                App.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            App.Instance.StartupRegistration.RestartElevated();
        }
        catch (OperationCanceledException)
        {
            App.Instance.Log.Warn("Restart as Administrator was cancelled.");
        }
        catch (Exception ex)
        {
            App.Instance.Log.Error("Could not restart as Administrator.", ex);
            System.Windows.MessageBox.Show(
                owner,
                $"Could not restart as Administrator.\n\n{ex.Message}",
                App.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
