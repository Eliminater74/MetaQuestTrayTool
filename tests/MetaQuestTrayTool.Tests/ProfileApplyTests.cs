using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class ProfileApplyTests
{
    [Theory]
    [InlineData(ProfileStepStatus.Succeeded, ProfileStepStatus.Succeeded, ProfileApplyStatus.Success)]
    [InlineData(ProfileStepStatus.Succeeded, ProfileStepStatus.Failed, ProfileApplyStatus.PartialSuccess)]
    [InlineData(ProfileStepStatus.Succeeded, ProfileStepStatus.Skipped, ProfileApplyStatus.PartialSuccess)]
    [InlineData(ProfileStepStatus.Failed, ProfileStepStatus.Skipped, ProfileApplyStatus.Failed)]
    public void AggregatesNonThrowingFailures(ProfileStepStatus odt, ProfileStepStatus link, ProfileApplyStatus expected)
    {
        var result = new ProfileApplyResult([new("ODT", odt, "ODT result"), new("Link", link, "Link result")]);
        Assert.Equal(expected, result.Status);
        Assert.Contains("Link result", result.Summary);
    }

    [Fact]
    public void ExceptionDoesNotHideLaterComponents()
    {
        var coordinator = new ProfileApplyCoordinator();
        coordinator.Run("ODT", () => throw new InvalidOperationException("ODT unavailable"));
        coordinator.Run("Link", () => new("Link", ProfileStepStatus.Succeeded, "Verified read-back"));
        Assert.Equal(ProfileApplyStatus.PartialSuccess, coordinator.Result.Status);
        Assert.Equal(2, coordinator.Result.Steps.Count);
        Assert.Contains("ODT unavailable", coordinator.Result.Summary);
    }
}
