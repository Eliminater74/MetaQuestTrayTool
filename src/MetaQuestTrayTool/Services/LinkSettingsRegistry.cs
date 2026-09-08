using Microsoft.Win32;

namespace MetaQuestTrayTool.Services;

/// <summary>Platform seam for Link overrides; tests never need Meta software or the real registry.</summary>
public interface ILinkSettingsRegistry
{
    ILinkSettingsRegistryKey? Open(bool writable);
}

public interface ILinkSettingsRegistryKey : IDisposable
{
    object? GetValue(string name);
    void SetValue(string name, int value);
    void DeleteValue(string name);
}

internal sealed class LinkSettingsRegistry : ILinkSettingsRegistry
{
    public ILinkSettingsRegistryKey? Open(bool writable)
    {
        var key = writable
            ? Registry.CurrentUser.CreateSubKey(LinkSettingsService.RegistryPath, writable: true)
            : Registry.CurrentUser.OpenSubKey(LinkSettingsService.RegistryPath, writable: false);
        return key is null ? null : new Key(key);
    }

    private sealed class Key(RegistryKey key) : ILinkSettingsRegistryKey
    {
        public object? GetValue(string name) => key.GetValue(name);
        public void SetValue(string name, int value) => key.SetValue(name, value, RegistryValueKind.DWord);
        public void DeleteValue(string name) => key.DeleteValue(name, throwOnMissingValue: false);
        public void Dispose() => key.Dispose();
    }
}

/// <summary>Checks every intended write/deletion against a fresh registry read, including value types.</summary>
internal sealed class LinkRegistryWriteBatch(ILinkSettingsRegistryKey key) : ILinkSettingsRegistryKey
{
    private readonly Dictionary<string, int?> _expected = new(StringComparer.OrdinalIgnoreCase);

    public object? GetValue(string name) => key.GetValue(name);

    public void SetValue(string name, int value)
    {
        _expected[name] = value;
        key.SetValue(name, value);
    }

    public void DeleteValue(string name)
    {
        _expected[name] = null;
        key.DeleteValue(name);
    }

    public IReadOnlyList<string> Verify(ILinkSettingsRegistryKey? current)
    {
        var mismatches = new List<string>();
        foreach (var (name, expected) in _expected)
        {
            var actual = current?.GetValue(name);
            if (expected.HasValue ? actual is not int number || number != expected.Value : actual is not null)
            {
                var observed = actual is null ? "absent" : actual is int value ? $"DWORD {value}" : "non-DWORD value";
                mismatches.Add($"{name}: requested {(expected.HasValue ? $"DWORD {expected.Value}" : "absent")}, read back {observed}");
            }
        }

        return mismatches;
    }

    public void Dispose() => key.Dispose();
}
