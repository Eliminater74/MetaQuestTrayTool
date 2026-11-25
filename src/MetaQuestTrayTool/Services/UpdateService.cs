using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
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
    public string? InstallerSha256 { get; init; }
    public long? InstallerSize { get; init; }
    public string? CurrentDisplayVersion { get; init; }
    public string? Error { get; init; }

    public bool Succeeded => Error is null && LatestVersion is not null;
    public bool UpdateAvailable =>
        Succeeded
        && InstallerDownloadUrl is not null
        && UpdateService.IsNewerRelease(CurrentDisplayVersion ?? CurrentVersion.ToString(), TagName);
    public bool IsUpToDate =>
        Succeeded
        && !UpdateService.IsNewerRelease(CurrentDisplayVersion ?? CurrentVersion.ToString(), TagName);
}

/// <summary>
/// Checks GitHub's latest release and downloads that Setup.exe.
/// A beta suffix is accepted when that release is the latest public release.
/// An older numeric version is not offered.
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
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Hash, long Size)> _verifiedDownloads = new(StringComparer.OrdinalIgnoreCase);

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
        var currentLabel = AppInfo.Version;
        var current = ParseVersion(currentLabel) ?? new Version(0, 0, 0);
        try
        {
            using var response = await _http.GetAsync(LatestReleaseApi, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return new UpdateCheckResult
                {
                    CurrentVersion = current,
                    CurrentDisplayVersion = currentLabel,
                    Error = $"GitHub returned {(int)response.StatusCode}: {Truncate(body)}"
                };
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = doc.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
            if (tag is null || !IsReleaseTag(tag))
            {
                return new UpdateCheckResult
                {
                    CurrentVersion = current,
                    CurrentDisplayVersion = currentLabel,
                    TagName = tag,
                    Error = "Latest GitHub release has an invalid version tag (expected v1.2.0 or v1.2.0-beta.1)."
                };
            }

            var latest = ParseVersion(tag);
            if (latest is null)
            {
                return new UpdateCheckResult
                {
                    CurrentVersion = current,
                    CurrentDisplayVersion = currentLabel,
                    TagName = tag,
                    Error = "Latest GitHub release has no usable version tag (expected v1.2.0 or v1.2.0-beta.1)."
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
                    var expectedName = InstallerFileNameForTag(tag);
                    if (!string.Equals(name, expectedName, StringComparison.OrdinalIgnoreCase))
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

                    if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
                        || !uri.AbsolutePath.Contains($"/{Owner}/{Repo}/releases/download/{tag}/", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    fileName = name;
                    downloadUrl = uri;
                    var digest = asset.TryGetProperty("digest", out var digestEl)
                        ? digestEl.GetString()
                        : null;
                    var size = asset.TryGetProperty("size", out var sizeEl) && sizeEl.TryGetInt64(out var assetSize)
                        ? assetSize
                        : (long?)null;
                    if (!TryGetSha256(digest, out var sha256) || size is null or <= 0)
                    {
                        return new UpdateCheckResult
                        {
                            CurrentVersion = current,
                    CurrentDisplayVersion = currentLabel,
                            LatestVersion = latest,
                            TagName = tag,
                            ReleaseHtmlUrl = htmlUrl,
                            ReleaseNotes = releaseNotes,
                            Error = $"Release {tag} does not provide a usable installer digest or size."
                        };
                    }

                    return new UpdateCheckResult
                    {
                        CurrentVersion = current,
                    CurrentDisplayVersion = currentLabel,
                        LatestVersion = latest,
                        TagName = tag,
                        ReleaseHtmlUrl = htmlUrl,
                        ReleaseNotes = releaseNotes,
                        InstallerFileName = fileName,
                        InstallerDownloadUrl = downloadUrl,
                        InstallerSha256 = sha256,
                        InstallerSize = size
                    };
                }
            }

            if (downloadUrl is null)
            {
                return new UpdateCheckResult
                {
                    CurrentVersion = current,
                    CurrentDisplayVersion = currentLabel,
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
                    CurrentDisplayVersion = currentLabel,
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
                    CurrentDisplayVersion = currentLabel,
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

        if (update.LatestVersion is null
            || !string.Equals(
                update.InstallerFileName,
                $"MetaQuestTrayTool-Setup-{update.LatestVersion.Major}.{update.LatestVersion.Minor}.{update.LatestVersion.Build}.exe",
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(update.InstallerFileName, IOPath.GetFileName(update.InstallerFileName), StringComparison.Ordinal)
            || update.InstallerDownloadUrl.Scheme != Uri.UriSchemeHttps
            || !string.Equals(update.InstallerDownloadUrl.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(update.TagName)
            || !update.InstallerDownloadUrl.AbsolutePath.Contains(
                $"/{Owner}/{Repo}/releases/download/{update.TagName}/",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Installer metadata is not a trusted GitHub release asset.");
        }

        var folder = InstallerIntegrity.CreatePrivateDirectory();
        var path = IOPath.Combine(folder, update.InstallerFileName);

        using var response = await _http.GetAsync(
            update.InstallerDownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (update.InstallerSize is null or <= 0 or > 300_000_000)
        {
            throw new InvalidOperationException("Installer size is missing or exceeds the safety limit.");
        }

        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength is not null && contentLength != update.InstallerSize)
        {
            throw new InvalidOperationException("Installer size changed after the release was checked.");
        }

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        var buffer = new byte[81920];
        long total = 0;
        int read;
        try
        {
            while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                total += read;
                if (total > update.InstallerSize.Value)
                {
                    throw new InvalidOperationException("Downloaded installer is larger than the release metadata.");
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                progress?.Report(total);
            }
        }
        catch
        {
            output.Dispose();
            try
            {
                File.Delete(path);
            }
            catch
            {
                // best effort cleanup of an incomplete download
            }

            throw;
        }

        if (total != update.InstallerSize.Value)
        {
            output.Dispose();
            File.Delete(path);
            throw new InvalidOperationException("Downloaded installer size does not match GitHub metadata.");
        }

        await output.DisposeAsync();
        await using var hashFile = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var actualHash = Convert.ToHexString(
            await SHA256.HashDataAsync(hashFile, cancellationToken)).ToLowerInvariant();
        if (!string.Equals(actualHash, update.InstallerSha256, StringComparison.OrdinalIgnoreCase))
        {
            await hashFile.DisposeAsync();
            File.Delete(path);
            throw new InvalidOperationException("Installer integrity check failed (SHA-256 does not match GitHub).");
        }

        _verifiedDownloads[IOPath.GetFullPath(path)] = (actualHash, total);
        return path;
    }

    /// <summary>
    /// Starts the Setup.exe, then fully exits this process so the installer can overwrite Program Files.
    /// </summary>
    public void LaunchInstallerAndExit(string installerPath)
    {
        installerPath = IOPath.GetFullPath(installerPath);
        if (!_verifiedDownloads.TryGetValue(installerPath, out var receipt))
            throw new InvalidOperationException("Installer was not verified by this update session. Download it again.");
        using var verifiedFile = InstallerIntegrity.VerifyAndLock(installerPath, receipt.Hash, receipt.Size);

        // Installer-only. Session end must not call this. It stops our watcher, then
        // KillServerForUpdate terminates this app's packaged adb.exe so Setup can replace
        // platform-tools. It does not run `adb kill-server` against a shared server.
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

        try
        {
            SessionHelperClient.RequestQuit();
        }
        catch
        {
            // helper is best-effort so Setup can overwrite the exe
        }

        _app.Log.Info($"Launching updater: {installerPath}");
        _app.Log.Info("Shutting down for in-place update — the installer will restart the tray when it finishes.");
        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true,
            WorkingDirectory = IOPath.GetDirectoryName(installerPath) ?? Environment.CurrentDirectory
        });

        // Exit after the installer has started so Program Files can be overwritten.
        try
        {
            _app.RequestUserExit(UserExitSource.Updater);
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
                var shown = string.IsNullOrWhiteSpace(result.CurrentDisplayVersion)
                    ? result.CurrentVersion.ToString()
                    : result.CurrentDisplayVersion;
                _app.Log.Info($"Up to date ({shown}). Latest release: {result.TagName}.");
                if (!quietIfUpToDate)
                {
                    ShowMessage(
                        owner,
                        $"You are on the latest version ({shown}).",
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
            var currentLabel = string.IsNullOrWhiteSpace(result.CurrentDisplayVersion)
                ? result.CurrentVersion.ToString()
                : result.CurrentDisplayVersion;
            var prompt =
                $"A newer version is available.\n\n" +
                $"Current: {currentLabel}\n" +
                $"Latest:  {result.TagName}\n\n";
            if (IsPrereleaseTag(result.TagName))
            {
                prompt =
                    "This update is a beta. It is the latest public release, and it is still community-test software. Experimental GPU level 7 and 144/207 Hz stay off unless you opt in. They are not hardware-accepted.\n\n"
                    + prompt;
            }
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

    public static bool IsReleaseTag(string? tag) =>
        tag is not null
        && System.Text.RegularExpressions.Regex.IsMatch(tag, @"^v\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$");

    public static bool IsStableReleaseTag(string? tag) =>
        tag is not null && System.Text.RegularExpressions.Regex.IsMatch(tag, @"^v\d+\.\d+\.\d+$");

    public static bool IsPrereleaseTag(string? tag) =>
        tag is not null && System.Text.RegularExpressions.Regex.IsMatch(tag, @"^v?\d+\.\d+\.\d+-[0-9A-Za-z.-]+$");

    public static string InstallerFileNameForTag(string tag)
    {
        var label = tag.Trim();
        if (label.StartsWith('v') || label.StartsWith('V'))
        {
            label = label[1..];
        }

        return $"MetaQuestTrayTool-Setup-{label}.exe";
    }

    /// <summary>
    /// A higher numeric version is newer, including a beta such as 1.2.0-beta.1 over 1.1.37.
    /// The same number does not offer a beta over a stable build, and it does not offer an older beta.
    /// </summary>
    public static bool IsNewerRelease(string? current, string? latest)
    {
        if (!TryParseRelease(current, out var installed) || !TryParseRelease(latest, out var published))
        {
            return false;
        }

        var numeric = published.Number.CompareTo(installed.Number);
        if (numeric != 0)
        {
            return numeric > 0;
        }

        if (installed.Prerelease is null)
        {
            return false;
        }

        if (published.Prerelease is null)
        {
            return true;
        }

        return ComparePrerelease(published.Prerelease, installed.Prerelease) > 0;
    }

    private readonly record struct ReleaseIdentity(Version Number, string? Prerelease);

    private static bool TryParseRelease(string? text, out ReleaseIdentity identity)
    {
        identity = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
        {
            trimmed = trimmed[1..];
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            trimmed,
            @"^(\d+\.\d+\.\d+)(?:-([0-9A-Za-z.-]+))?$");
        if (!match.Success || !Version.TryParse(match.Groups[1].Value, out var number))
        {
            return false;
        }

        identity = new ReleaseIdentity(number, match.Groups[2].Success ? match.Groups[2].Value : null);
        return true;
    }

    private static int ComparePrerelease(string left, string right)
    {
        var a = left.Split('.');
        var b = right.Split('.');
        var count = Math.Min(a.Length, b.Length);
        for (var i = 0; i < count; i++)
        {
            var aNumeric = int.TryParse(a[i], out var aNumber);
            var bNumeric = int.TryParse(b[i], out var bNumber);
            int compared;
            if (aNumeric && bNumeric)
            {
                compared = aNumber.CompareTo(bNumber);
            }
            else if (aNumeric)
            {
                compared = -1;
            }
            else if (bNumeric)
            {
                compared = 1;
            }
            else
            {
                compared = string.Compare(a[i], b[i], StringComparison.Ordinal);
            }

            if (compared != 0)
            {
                return compared;
            }
        }

        return a.Length.CompareTo(b.Length);
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

        if (!System.Text.RegularExpressions.Regex.IsMatch(
                trimmed,
                @"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$"))
        {
            return null;
        }

        // Strip pre-release suffix: 1.0.1-beta
        var dash = trimmed.IndexOf('-');
        if (dash >= 0)
        {
            trimmed = trimmed[..dash];
        }

        return Version.TryParse(trimmed, out var version) ? version : null;
    }

    private static bool TryGetSha256(string? digest, out string sha256)
    {
        sha256 = string.Empty;
        if (string.IsNullOrWhiteSpace(digest))
        {
            return false;
        }

        var value = digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? digest["sha256:".Length..]
            : digest;
        if (value.Length != 64 || !value.All(Uri.IsHexDigit))
        {
            return false;
        }

        sha256 = value.ToLowerInvariant();
        return true;
    }

    private static string Truncate(string text)
    {
        text = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= 160 ? text : text[..160] + "…";
    }
}
