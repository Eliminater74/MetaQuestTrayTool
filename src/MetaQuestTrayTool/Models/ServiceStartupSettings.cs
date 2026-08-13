namespace MetaQuestTrayTool.Models;

/// <summary>Oculus service automation from the old OTT "Service & Startup" tab.</summary>
public sealed class ServiceStartupSettings
{
    public bool StartServiceWhenToolStarts { get; set; } = true;
    public bool StopServiceWhenToolExits { get; set; }
    public bool RestartServiceWhenComputerWakes { get; set; } = true;
}
