using MetaQuestTrayTool.Models;
using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class OpenXrRuntimeServiceTests
{
    [Fact]
    public void DescribeViewsShowsMismatched64BitAnd32BitRuntimes()
    {
        var text = OpenXrRuntimeService.DescribeViews(
            new OpenXrRuntimeViewState("64-bit", OpenXrRuntimeKind.SteamVr, @"C:\Steam\steamapps\common\SteamVR\steamxr_win64.json"),
            new OpenXrRuntimeViewState("32-bit", OpenXrRuntimeKind.Meta, @"C:\Program Files\Oculus\Support\oculus-runtime\oculus_openxr_32.json"));

        Assert.Contains("64-bit: SteamVR", text);
        Assert.Contains("32-bit: Meta / Oculus", text);
    }

    [Fact]
    public void DescribeViewsCollapsesMatchingPath()
    {
        var text = OpenXrRuntimeService.DescribeViews(
            new OpenXrRuntimeViewState("64-bit", OpenXrRuntimeKind.Meta, @"C:\Program Files\Oculus\Support\oculus-runtime\oculus_openxr_64.json"),
            new OpenXrRuntimeViewState("32-bit", OpenXrRuntimeKind.Meta, @"C:\Program Files\Oculus\Support\oculus-runtime\oculus_openxr_64.json"));

        Assert.Equal(@"OpenXR: Meta / Oculus (C:\Program Files\Oculus\Support\oculus-runtime\oculus_openxr_64.json)", text);
    }

    [Fact]
    public void DescribeViewsReportsUnset32BitView()
    {
        var text = OpenXrRuntimeService.DescribeViews(
            new OpenXrRuntimeViewState("64-bit", OpenXrRuntimeKind.SteamVr, @"C:\Steam\steamapps\common\SteamVR\steamxr_win64.json"),
            new OpenXrRuntimeViewState("32-bit", null, null));

        Assert.Contains("64-bit: SteamVR", text);
        Assert.Contains("32-bit: not set", text);
    }
}
