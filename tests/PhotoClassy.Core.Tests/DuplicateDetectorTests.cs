using PhotoClassy.Core.Duplicates;
using PhotoClassy.Core.Engine;
using Xunit;

namespace PhotoClassy.Core.Tests;

public class DuplicateDetectorTests
{
    [Fact]
    public void UniqueSizes_AreNotHashed()
    {
        var hashed = new List<string>();
        var detector = new DuplicateDetector((p, _) => { lock (hashed) hashed.Add(p); return "h"; });
        var items = new List<FileItem> { new("a", 1), new("b", 2) };

        var duplicates = detector.Detect(items, new List<ExistingFile> { new("t", 3) }, null, null, CancellationToken.None);

        Assert.Equal(0, duplicates);
        Assert.Empty(hashed);
    }

    [Fact]
    public void SameContentDifferentName_IsDuplicateOfFirst()
    {
        using var dir = new TempDir();
        var a = dir.CreateFile("a.jpg", "same");
        var b = dir.CreateFile("sub/b.jpg", "same");
        var c = dir.CreateFile("c.jpg", "diff");   // 같은 크기, 다른 내용
        var items = new List<FileItem> { new(a, 4), new(b, 4), new(c, 4) };

        var duplicates = new DuplicateDetector().Detect(items, new List<ExistingFile>(), null, null, CancellationToken.None);

        Assert.Equal(1, duplicates);
        Assert.False(items[0].IsDuplicate);
        Assert.Equal(a, items[1].DuplicateOf);
        Assert.Same(items[0], items[1].DuplicateOfItem);
        Assert.False(items[2].IsDuplicate);
    }

    [Fact]
    public void MatchesExistingTargetFile()
    {
        using var dir = new TempDir();
        var src = dir.CreateFile("src/a.jpg", "content");
        var existing = dir.CreateFile("target/2020/01/old.jpg", "content");
        var items = new List<FileItem> { new(src, 7) };

        var duplicates = new DuplicateDetector().Detect(items,
            new List<ExistingFile> { new(existing, 7) }, null, null, CancellationToken.None);

        Assert.Equal(1, duplicates);
        Assert.Equal(existing, items[0].DuplicateOf);
        Assert.Null(items[0].DuplicateOfItem);
    }
}
