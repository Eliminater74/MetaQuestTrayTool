using MetaQuestTrayTool.Models;

namespace MetaQuestTrayTool.Services;

public sealed class ProfileService
{
    private readonly SettingsService _settings;

    public ProfileService(SettingsService settings)
    {
        _settings = settings;
    }

    public IList<GameProfile> All => _settings.Current.Profiles;

    public GameProfile? FindByName(string name) =>
        All.FirstOrDefault(profile => profile.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public GameProfile? FindByProcess(string processName)
    {
        var normalized = NormalizeProcessName(processName);
        return All.FirstOrDefault(profile =>
            NormalizeProcessName(profile.ProcessName) == normalized);
    }

    public void Add(GameProfile profile)
    {
        All.Add(profile);
        _settings.Save();
    }

    public void Remove(GameProfile profile)
    {
        All.Remove(profile);
        _settings.Save();
    }

    public void Save() => _settings.Save();

    public static string NormalizeProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return string.Empty;
        }

        var name = processName.Trim();
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? name[..^4]
            : name;
    }
}
