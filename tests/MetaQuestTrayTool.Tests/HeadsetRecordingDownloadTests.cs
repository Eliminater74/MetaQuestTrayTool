using MetaQuestTrayTool.Services;

namespace MetaQuestTrayTool.Tests;

public class HeadsetRecordingDownloadTests
{
    [Fact]
    public void WaitsPastPreviousRecordingAndGrowingNewFile()
    {
        var old = new HeadsetRecordingCandidate(100, 5000, "/old.mp4");
        var growing = new HeadsetRecordingCandidate(101, 2000, "/new.mp4");
        var ready = growing with { Bytes = 6000, ModifiedUnixSeconds = 102 };
        var reads = new Queue<IReadOnlyList<HeadsetRecordingCandidate>>(
            new IReadOnlyList<HeadsetRecordingCandidate>[] { [old], [old], [old, growing], [old, ready], [old, ready] });
        Assert.Equal(ready, HeadsetSettingsService.WaitForFinalizedRecording([old], () => reads.Dequeue(), _ => { }));
        Assert.Empty(reads);
    }

    [Fact]
    public void DoesNotDownloadUnchangedPreviousRecording()
    {
        var old = new HeadsetRecordingCandidate(100, 5000, "/old.mp4");
        Assert.Throws<TimeoutException>(() => HeadsetSettingsService.WaitForFinalizedRecording([old], () => [old], _ => { }));
    }

    [Fact]
    public void ExistingFileMustChangeThenStabilize()
    {
        var old = new HeadsetRecordingCandidate(100, 5000, "/current.mp4");
        var ready = old with { Bytes = 6000 };
        Assert.Equal(ready, HeadsetSettingsService.WaitForFinalizedRecording([old], () => [ready], _ => { }));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MissingOrContinuouslyGrowingVideoTimesOut(bool growing)
    {
        var size = 2000;
        Assert.Throws<TimeoutException>(() => HeadsetSettingsService.WaitForFinalizedRecording(
            [], () => growing ? [new HeadsetRecordingCandidate(100, size++, "/new.mp4")] : [], _ => { }));
    }
    [Fact]
    public void ParseRecordingListingKeepsOnlyVideoFiles()
    {
        var listing = string.Join(Environment.NewLine,
            "1770000001\t2048000\t/sdcard/Oculus/VideoShots/newest.mp4",
            "bad\tbad\t/sdcard/Oculus/VideoShots/older.MOV",
            "1770000002\t10\t/sdcard/Oculus/VideoShots/not-video.txt",
            "1770000000\t1024\t");

        var recordings = HeadsetSettingsService.ParseRecordingListing(listing);

        Assert.Equal(2, recordings.Count);
        Assert.Contains(recordings, item => item.RemotePath.EndsWith("newest.mp4", StringComparison.Ordinal));
        Assert.Contains(recordings, item => item.RemotePath.EndsWith("older.MOV", StringComparison.Ordinal));
        Assert.DoesNotContain(recordings, item => item.RemotePath.EndsWith("not-video.txt", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildRecordingFileNameSanitizesModelAndRemoteName()
    {
        var fileName = HeadsetSettingsService.BuildRecordingFileName(
            new DateTimeOffset(2026, 9, 10, 12, 34, 56, TimeSpan.Zero),
            "Quest 3/S",
            "Record Clip 01",
            "mp4");

        Assert.Equal("QuestRecording-20260910-123456-Quest-3-S-Record-Clip-01.mp4", fileName);
    }
}
