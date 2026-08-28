namespace MetaQuestTrayTool.Models;

/// <summary>
/// Per-app settings applied when a VR game or experience is detected.
/// </summary>
public sealed class GameProfile
{
    public string Name { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public GamePlatform Platform { get; set; } = GamePlatform.Custom;
    public ProfileScope Scope { get; set; } = ProfileScope.Personal;
    public string? AppId { get; set; }
    public string? InstallPath { get; set; }
    /// <summary>Relative exe name under <see cref="InstallPath"/> (Meta / local launch).</summary>
    public string? LaunchFile { get; set; }
    /// <summary>Optional arguments passed to the Steam URI or local executable launch.</summary>
    public string? LaunchArguments { get; set; }
    /// <summary>Opt-in MSFS 2024 launcher: prepare PCVR and optionally send the VR toggle.</summary>
    public bool ExperimentalMsfsVr { get; set; }
    public bool ExperimentalMsfsVrAutoToggle { get; set; } = true;
    public int ExperimentalMsfsVrToggleDelaySeconds { get; set; } = 45;
    public string ExperimentalMsfsVrHotkey { get; set; } = "Ctrl+Tab";
    public GameSettings Settings { get; set; } = new();

    private LinkProfileOverrides _link = new();
    public LinkProfileOverrides Link
    {
        get => _link;
        set => _link = value ?? new();
    }

    public OpenXrRuntimeKind OpenXrRuntime { get; set; } = OpenXrRuntimeKind.Inherit;
    public string CpuPriority { get; set; } = "Normal";
    public string? Comments { get; set; }
    private CustomCommandSet _customCommands = new();
    public CustomCommandSet CustomCommands
    {
        get => _customCommands;
        set => _customCommands = value ?? new();
    }

    public GameProfile Clone() => new()
    {
        Name = Name,
        ProcessName = ProcessName,
        Platform = Platform,
        Scope = Scope,
        AppId = AppId,
        InstallPath = InstallPath,
        LaunchFile = LaunchFile,
        LaunchArguments = LaunchArguments,
        ExperimentalMsfsVr = ExperimentalMsfsVr,
        ExperimentalMsfsVrAutoToggle = ExperimentalMsfsVrAutoToggle,
        ExperimentalMsfsVrToggleDelaySeconds = ExperimentalMsfsVrToggleDelaySeconds,
        ExperimentalMsfsVrHotkey = ExperimentalMsfsVrHotkey,
        Settings = Settings?.Clone() ?? new(),
        Link = Link?.Clone() ?? new(),
        OpenXrRuntime = OpenXrRuntime,
        CpuPriority = CpuPriority,
        Comments = Comments,
        CustomCommands = CustomCommands?.Clone() ?? new()
    };

    public void CopyFrom(GameProfile other)
    {
        var copy = other.Clone();
        Name = copy.Name;
        ProcessName = copy.ProcessName;
        Platform = copy.Platform;
        Scope = copy.Scope;
        AppId = copy.AppId;
        InstallPath = copy.InstallPath;
        LaunchFile = copy.LaunchFile;
        LaunchArguments = copy.LaunchArguments;
        ExperimentalMsfsVr = copy.ExperimentalMsfsVr;
        ExperimentalMsfsVrAutoToggle = copy.ExperimentalMsfsVrAutoToggle;
        ExperimentalMsfsVrToggleDelaySeconds = copy.ExperimentalMsfsVrToggleDelaySeconds;
        ExperimentalMsfsVrHotkey = copy.ExperimentalMsfsVrHotkey;
        Settings = copy.Settings;
        Link = copy.Link;
        OpenXrRuntime = copy.OpenXrRuntime;
        CpuPriority = copy.CpuPriority;
        Comments = copy.Comments;
        CustomCommands = copy.CustomCommands;
    }
}
