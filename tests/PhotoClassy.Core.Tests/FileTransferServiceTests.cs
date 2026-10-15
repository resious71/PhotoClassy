using PhotoClassy.Core.Operations;
using PhotoClassy.Core.Settings;
using Xunit;

namespace PhotoClassy.Core.Tests;

public class FileTransferServiceTests
{
    [Fact]
    public void Copy_KeepsSource_AndRenamesOnCollision()
    {
        using var dir = new TempDir();
        var src = dir.CreateFile("src/photo.jpg", "new");
        dir.CreateFile("target/2026/01/photo.jpg", "old");
        dir.CreateFile("target/2026/01/photo_1.jpg", "old2");

        var outcome = new FileTransferService().Transfer(src, dir.Combine("target", "2026", "01"), dir.Combine("target"),
            FileOperation.Copy, null, CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.True(outcome.Renamed);
        Assert.Equal(dir.Combine("target", "2026", "01", "photo_2.jpg"), outcome.DestinationPath);
        Assert.True(File.Exists(src));
        Assert.Equal("new", File.ReadAllText(outcome.DestinationPath));
    }

    [Fact]
    public void Move_SameVolume_RemovesSource()
    {
        using var dir = new TempDir();
        var src = dir.CreateFile("src/photo.jpg", "x");

        var outcome = new FileTransferService().Transfer(src, dir.Combine("target", "2026", "01"), dir.Combine("target"),
            FileOperation.Move, null, CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.False(File.Exists(src));
        Assert.True(File.Exists(outcome.DestinationPath));
    }

    [Fact]
    public void Move_CrossVolume_VerifiesThenDeletesSource()
    {
        using var dir = new TempDir();
        var src = dir.CreateFile("src/photo.jpg", "data");
        var service = new FileTransferService(isSameVolume: (_, _) => false);

        var outcome = service.Transfer(src, dir.Combine("target", "2026", "01"), dir.Combine("target"),
            FileOperation.Move, null, CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.False(File.Exists(src));
        Assert.Equal("data", File.ReadAllText(outcome.DestinationPath));
    }

    [Fact]
    public void Move_CrossVolume_VerificationFailure_KeepsSourceAndRemovesCopy()
    {
        using var dir = new TempDir();
        var src = dir.CreateFile("src/photo.jpg", "data");
        var calls = 0;
        // 원본/사본 해시가 다르게 나오도록 조작
        var service = new FileTransferService(isSameVolume: (_, _) => false, hasher: (_, _) => (calls++).ToString());

        var outcome = service.Transfer(src, dir.Combine("target", "2026", "01"), dir.Combine("target"),
            FileOperation.Move, null, CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Contains("검증 실패", outcome.Error);
        Assert.True(File.Exists(src));
        Assert.False(File.Exists(dir.Combine("target", "2026", "01", "photo.jpg")));
    }
}
