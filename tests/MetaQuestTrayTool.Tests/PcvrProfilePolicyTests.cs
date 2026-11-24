using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class PcvrProfilePolicyTests
{
    [Fact]
    public void DecideRecordsThePreferenceWithoutLaunchingOrWriting()
    {
        var decision = PcvrProfilePolicy.Decide(new PcvrProfilePreferences
        {
            PreferredProvider = "ALVR",
            AutoSwitchProvider = true,
            ExperimentalGpuMaximum = true,
            ExperimentalRefreshHz = 240,
            AlvrProfile = "Quality"
        });
        Assert.Equal(ProfileStepStatus.Succeeded, decision.Status);
        Assert.Contains("ignored", decision.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("launched", decision.Summary, StringComparison.OrdinalIgnoreCase);
    }
}
