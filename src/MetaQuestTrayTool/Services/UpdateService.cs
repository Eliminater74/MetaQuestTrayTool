using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;
using MetaQuestTrayTool.Models;
using IOPath = System.IO.Path;

namespace MetaQuestTrayTool.Services;

public sealed class UpdateCheckResult
{
    public required Version CurrentVersion { get; init; }
    public Version? LatestVersion { get; init; }
    public string? TagName { get; init; }
    public string? ReleaseHtmlUrl { get; init; }
    public string? ReleaseNotes { get; init; }
    public string? InstallerFileName { get; init; }
    public Uri? InstallerDownloadUrl { get; init; }
    public string? Error { get; init; }

    public bool Succeeded => Error is null && LatestVersion is not null;
    public bool UpdateAvailable =>
        Succeeded
        && LatestVersion is not null
        && LatestVersion > CurrentVersion
        && InstallerDownloadUrl is not null;
    public bool IsUpToDate => Succeeded && LatestVersion is not null && LatestVersion <= CurrentVersion;
}

/// <summary>
/// Checks GitHub Releases (latest tag v*) and downloads the Setup.exe for an in-place upgrade.
/// </summary>
public sealed class UpdateService
{
    public const string Owner = "Eliminater74";
    public const string Repo = "MetaQuestTrayTool";

    private static readonly Uri LatestReleaseApi =
        new($"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");

    private readonly App _app;
    private readonly HttpClient _http;
    private int _busy;

    public UpdateService(App app)
    {
        _app = app;
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        _http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("MetaQuestTrayTool", AppInfo.Version));
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public bool IsBusy => Interlocked.CompareExchange(ref _busy, 0, 0) != 0;

    public async Task<UpdateCheckResult> CheckLatestAsync(CancellationToken cancellationToken = default)
    {
        var current = ParseVersion(AppInfo.Version) ?? new Version(0, 0, 0);
        try
        {
            using var response = await _http.GetAsync(LatestReleaseApi, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return new UpdateCheckResult
                {
                    CurrentVersion = current,
                    Error = $"GitHub returned {(int)response.StatusCode}: {Truncate(body)}"
                };
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = doc.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
            var latest = ParseVersion(tag);
            if (latest is null)
            {
                return new UpdateCheckResult
                {
                    CurrentVersion = current,
                    TagName = tag,
                    Error = "Latest GitHub release has no usable version tag (expected v1.0.1 style)."
                };
            }

            string? htmlUrl = root.TryGetProperty("html_url", out var htmlEl) ? htmlEl.GetString() : null;
            string? releaseNotes = root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() : null;
            string? fileName = null;
            Uri? downloadUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                    if (string.IsNullOrWhiteSpace(name)
                        || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        || !name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var url = asset.TryGetProperty("browser_download_url", out var urlEl)
                        ? urlEl.GetString()
                        : null;
                    if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    {
                        continue;
                    }

                    fileName = name;
                    downloadUrl = uri;
                    break;
                }
            }

            if (downloadUrl is null)
            {
                return new UpdateCheckResult
                {
                    CurrentVersion = current,
                    LatestVersion = latest,
                    TagName = tag,
                    ReleaseHtmlUrl = htmlUrl,
                    ReleaseNotes = releaseNotes,
                    Error = $"Release {tag} has no Setup.exe asset."
                };
            }

            return new UpdateCheckResult
            {
                CurrentVersion = current,
                LatestVersion = latest,
                TagName = tag,
                ReleaseHtmlUrl = htmlUrl,
                ReleaseNotes = releaseNotes,
                InstallerFileName = fileName,
                InstallerDownloadUrl = downloadUrl
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult
            {
                CurrentVersion = current,
                Error = ex.Message
            };
        }
    }

    public async Task<string> DownloadInstallerAsync(
        UpdateCheckResult update,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (update.InstallerDownloadUrl is null || string.IsNullOrWhiteSpace(update.InstallerFileName))
        {
            throw new InvalidOperationException("No installer download URL.");
        }

        var folder = IOPath.Combine(IOPath.GetTempPath(), "MetaQuestTrayTool-Updates");
        Directory.CreateDirectory(folder);
        var path = IOPath.Combine(folder, update.InstallerFileName);

        using var response = await _http.GetAsync(
            update.InstallerDownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            total += read;
            progress?.Report(total);
        }

        return path;
    }

    /// <summary>
    /// Starts the Setup.exe, then fully exits this process so the installer can overwrite Program Files.
    /// </summary>
    public void LaunchInstallerAndExit(string installerPath)
    {
        if (!File.Exists(installerPath))
        {
            throw new FileNotFoundException("Installer not found.", installerPath);
        }

        // Stop ADB polling first so a tray tick cannot spawn a new adb.exe after kill-server.
        try
        {
            _app.HeadsetWatch?.Stop();
        }
        catch
        {
            // update path
        }

        // ADB's background server holds platform-tools\adb.exe open even with no headset —
        // Inno then prompts to skip/retry that file on upgrade.
        try
        {
            var adb = _app.Adb.KillServerForUpdate();
            var wait = _app.Adb.WaitUntilProcessesExit(TimeSpan.FromSeconds(8));
            _app.Log.Info("Pre-update ADB unlock: " + adb + " " + wait);
        }
        catch (Exception ex)
        {
            _app.Log.Warn($"Pre-update ADB unlock failed: {ex.Message}");
        }

        _app.Log.Info($"Launching updater: {installerPath}");
        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true,
            WorkingDirectory = IOPath.GetDirectoryName(installerPath) ?? Environment.CurrentDirectory
        });

        // Exit after the installer has started so Program Files can be overwritten.
        try
        {
            _app.Shutdown();
        }
        catch
        {
            Environment.Exit(0);
        }
    }

    public async Task CheckInteractivelyAsync(Window? owner, bool quietIfUpToDate)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            await _app.Dispatcher.InvokeAsync(() =>
            {
                ShowMessage(
                    owner,
                    "An update check is already in progress.",
                    App.AppName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            });
            return;
        }

        try
        {
            _app.Log.Info("Checking GitHub for updates…");
            var result = await CheckLatestAsync().ConfigureAwait(false);
            if (result.Succeeded)
            {
                MarkLastCheck();
            }

            await HandleCheckResultAsync(owner, result, quietIfUpToDate).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    public void MarkLastCheck()
    {
        _app.Settings.Current.Tray.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
        _app.Settings.Save();
    }

    public string DescribeSchedule()
    {
        var tray = _app.Settings.Current.Tray;
        var interval = UpdateCheckIntervalHelper.Describe(tray.AutoUpdateCheckInterval);
        if (tray.LastUpdateCheckUtc is null)
        {
            return $"Auto-check: {interval}. Not checked yet.";
        }

        var local = tray.LastUpdateCheckUtc.Value.ToLocalTime();
        return $"Auto-check: {interval}. Last check: {local:g}.";
    }

    private async Task HandleCheckResultAsync(Window? owner, UpdateCheckResult result, bool quietIfUpToDate)
    {
        MessageBoxResult answer = MessageBoxResult.No;
        await _app.Dispatcher.InvokeAsync(() =>
        {
            if (!result.Succeeded)
            {
                _app.Log.Warn($"Update check failed: {result.Error}");
                if (!quietIfUpToDate)
                {
                    ShowMessage(
                        owner,
                        $"Could not check for updates.\n\n{result.Error}",
                        App.AppName,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                return;
            }

            if (!result.UpdateAvailable)
            {
                _app.Log.Info($"Up to date ({result.CurrentVersion}). Latest release: {result.TagName}.");
                if (!quietIfUpToDate)
                {
                    ShowMessage(
                        owner,
                        $"You are on the latest version ({result.CurrentVersion}).",
                        App.AppName,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                return;
            }

            _app.Log.Info($"Update available: {result.CurrentVersion} → {result.LatestVersion} ({result.TagName}).");
            if (_app.Settings.Current.ShowNotifications)
            {
                _app.TrayNotify("Update available", $"{result.TagName} is ready to install.");
            }

            var notes = FormatReleaseNotesForPrompt(result.ReleaseNotes);
            var prompt =
                $"A newer version is available.\n\n" +
                $"Current: {result.CurrentVersion}\n" +
                $"Latest:  {result.LatestVersion} ({result.TagName})\n\n";
            if (!string.IsNullOrWhiteSpace(notes))
            {
                prompt += "What's new:\n" + notes + "\n\n";
            }
            else if (!string.IsNullOrWhiteSpace(result.ReleaseHtmlUrl))
            {
                prompt += $"Release notes: {result.ReleaseHtmlUrl}\n\n";
            }

            prompt +=
                "Download the Setup installer and install over this copy?\n" +
                "Meta Quest Tray Tool will close so files can be replaced.\n\n" +
                "Choose No to stay on your current version.";

            answer = ShowMessage(
                owner,
                prompt,
                App.AppName,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
        });

        if (!result.UpdateAvailable || answer != MessageBoxResult.Yes)
        {
            if (result.UpdateAvailable && answer != MessageBoxResult.Yes)
            {
                _app.Log.Info("Update declined by user.");
            }

            return;
        }

        try
        {
            _app.Log.Info($"Downloading {result.InstallerFileName}…");
            await _app.Dispatcher.InvokeAsync(() =>
            {
                if (_app.Settings.Current.ShowNotifications)
                {
                    _app.TrayNotify("Updating", "Downloading installer…");
                }
            });

            var path = await DownloadInstallerAsync(result).ConfigureAwait(false);
            _app.Log.Info($"Download complete: {path}");
            await _app.Dispatcher.InvokeAsync(() => LaunchInstallerAndExit(path));
        }
        catch (Exception ex)
        {
            _app.Log.Error("Update download/install failed.", ex);
            await _app.Dispatcher.InvokeAsync(() =>
            {
                ShowMessage(
                    owner,
                    $"Could not download or start the installer.\n\n{ex.Message}",
                    App.AppName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            });
        }
    }

    private static MessageBoxResult ShowMessage(
        Window? owner,
        string message,
        string caption,
        MessageBoxButton buttons,
        MessageBoxImage icon)
    {
        // Tray-only / scheduled checks have no shell window — owner overload throws on null.
        return owner is null
            ? System.Windows.MessageBox.Show(message, caption, buttons, icon)
            : System.Windows.MessageBox.Show(owner, message, caption, buttons, icon);
    }

    /// <summary>
    /// Strip markdown noise and keep the prompt readable in a WinForms/WPF MessageBox.
    /// </summary>
    private static string FormatReleaseNotesForPrompt(string? markdown, int maxChars = 1200)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var lines = markdown
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.TrimEnd())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !line.StartsWith("---", StringComparison.Ordinal))
            .Where(line => !line.StartsWith("**Windows Setup", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.StartsWith("### Requirements", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.StartsWith("- Self-contained", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.StartsWith("- Settings persist", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.StartsWith("- Full history", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.StartsWith("- Windows 10", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.StartsWith("- Meta Quest PC", StringComparison.OrdinalIgnoreCase))
            .Select(line =>
            {
                var cleaned = line
                    .Replace("**", "", StringComparison.Ordinal)
                    .Replace("`", "", StringComparison.Ordinal);
                if (cleaned.StartsWith("## ", StringComparison.Ordinal))
                {
                    cleaned = cleaned[3..].Trim();
                }
                else if (cleaned.StartsWith("### ", StringComparison.Ordinal))
                {
                    cleaned = cleaned[4..].Trim() + ":";
                }

                return cleaned;
            })
            .ToList();

        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var text = string.Join(Environment.NewLine, lines);
        if (text.Length <= maxChars)
        {
            return text;
        }

        return text[..(maxChars - 1)].TrimEnd() + "…";
    }

    public static Version? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
        {
            trimmed = trimmed[1..];
        }

        // Strip pre-release suffix: 1.0.1-beta
        var dash = trimmed.IndexOf('-');
        if (dash >= 0)
        {
            trimmed = trimmed[..dash];
        }

        return Version.TryParse(trimmed, out var version) ? version : null;
    }

    private static string Truncate(string text)
    {
        text = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= 160 ? text : text[..160] + "…";
    }
}
