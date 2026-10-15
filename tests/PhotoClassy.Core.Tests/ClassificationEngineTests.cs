using PhotoClassy.Core.Dates;
using PhotoClassy.Core.Engine;
using PhotoClassy.Core.Logging;
using PhotoClassy.Core.Progress;
using PhotoClassy.Core.Settings;
using Xunit;

namespace PhotoClassy.Core.Tests;

public class ClassificationEngineTests
{
    private static ClassificationResult Run(AppSettings settings, List<LogEntry> log = null,
        Func<string, long?> freeSpace = null, List<ProgressInfo> progress = null)
    {
        var engine = new ClassificationEngine(freeSpaceProvider: freeSpace ?? (_ => long.MaxValue));
        var sink = log ?? new List<LogEntry>();
        IProgress<ProgressInfo> reporter = progress == null ? null : new SyncProgress(progress);
        return engine.Run(settings, reporter, e => { lock (sink) sink.Add(e); }, CancellationToken.None);
    }

    [Fact]
    public void MovesByModifiedDate_YearMonth()
    {
        using var dir = new TempDir();
        var a = dir.CreateFile("src/a.png", "aaa", new DateTime(2026, 1, 5));
        var b = dir.CreateFile("src/sub/b.mov", "bbbb", new DateTime(2025, 12, 31));
        var settings = new AppSettings { SourceFolders = { dir.Combine("src") }, TargetFolder = dir.Combine("out") };
        var log = new List<LogEntry>();

        var result = Run(settings, log);

        Assert.Equal(RunStatus.Completed, result.Status);
        Assert.Equal(2, result.TotalFiles);
        Assert.Equal(2, result.Transferred);
        Assert.Equal(2, result.FallbackDates);
        Assert.True(File.Exists(dir.Combine("out", "2026", "01", "a.png")));
        Assert.True(File.Exists(dir.Combine("out", "2025", "12", "b.mov")));
        Assert.False(File.Exists(a));
        Assert.False(File.Exists(b));
        Assert.Contains(log, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void CopiesByExifDate_YearMonthDay()
    {
        using var dir = new TempDir();
        var src = dir.Combine("src", "p.jpg");
        Directory.CreateDirectory(dir.Combine("src"));
        File.WriteAllBytes(src, TestJpeg.WithDateTimeOriginal("2019:08:07 06:05:04"));
        var settings = new AppSettings
        {
            SourceFolders = { dir.Combine("src") },
            TargetFolder = dir.Combine("out"),
            Format = ClassificationFormat.YearMonthDay,
            Operation = FileOperation.Copy
        };

        var result = Run(settings);

        Assert.Equal(1, result.Transferred);
        Assert.Equal(0, result.FallbackDates);
        Assert.True(File.Exists(src));
        Assert.True(File.Exists(dir.Combine("out", "2019", "08", "07", "p.jpg")));
    }

    [Fact]
    public void Duplicates_GoToDuplicatesFolder_AndRerunDetectsExisting()
    {
        using var dir = new TempDir();
        var date = new DateTime(2024, 5, 6);
        dir.CreateFile("src1/a.jpg", "same", date);
        dir.CreateFile("src2/copy-of-a.jpg", "same", date);
        dir.CreateFile("src2/other.jpg", "diff", date);
        var settings = new AppSettings
        {
            SourceFolders = { dir.Combine("src1"), dir.Combine("src2") },
            TargetFolder = dir.Combine("out"),
            Operation = FileOperation.Copy
        };

        var first = Run(settings);
        Assert.Equal(2, first.Transferred);
        Assert.Equal(1, first.Duplicates);
        Assert.True(File.Exists(dir.Combine("out", "_Duplicates", "2024", "05", "copy-of-a.jpg")));

        // 재실행: 대상 폴더에 이미 있는 파일과 같으므로 모두 중복
        var second = Run(settings);
        Assert.Equal(0, second.Transferred);
        Assert.Equal(3, second.Duplicates);
    }

    [Fact]
    public void FilterSkipsExcluded()
    {
        using var dir = new TempDir();
        dir.CreateFile("src/a.jpg", "a", new DateTime(2024, 1, 1));
        dir.CreateFile("src/notes.txt", "t", new DateTime(2024, 1, 1));
        var settings = new AppSettings
        {
            SourceFolders = { dir.Combine("src") },
            TargetFolder = dir.Combine("out"),
            BarringExtensions = { ".TXT" }
        };

        var result = Run(settings);

        Assert.Equal(1, result.Transferred);
        Assert.Equal(1, result.Skipped);
        Assert.True(File.Exists(dir.Combine("src", "notes.txt")));
    }

    [Fact]
    public void InsufficientSpace_AbortsBeforeTouchingFiles()
    {
        using var dir = new TempDir();
        var a = dir.CreateFile("src/a.jpg", "aaaa", new DateTime(2024, 1, 1));
        var settings = new AppSettings
        {
            SourceFolders = { dir.Combine("src") },
            TargetFolder = dir.Combine("out"),
            Operation = FileOperation.Copy
        };

        var result = Run(settings, freeSpace: _ => 1);

        Assert.Equal(RunStatus.Aborted, result.Status);
        Assert.Contains("여유 공간", result.AbortReason);
        Assert.False(Directory.Exists(dir.Combine("out", "2024")));
        Assert.True(File.Exists(a));
    }

    [Fact]
    public void OverlappingFolders_Abort()
    {
        using var dir = new TempDir();
        dir.CreateFile("src/a.jpg", "a");
        var settings = new AppSettings { SourceFolders = { dir.Combine("src") }, TargetFolder = dir.Combine("src", "out") };

        var result = Run(settings);

        Assert.Equal(RunStatus.Aborted, result.Status);
    }

    [Fact]
    public void ReportsAllStages()
    {
        using var dir = new TempDir();
        dir.CreateFile("src/a.jpg", "a", new DateTime(2024, 1, 1));
        var progress = new List<ProgressInfo>();

        Run(new AppSettings { SourceFolders = { dir.Combine("src") }, TargetFolder = dir.Combine("out") },
            progress: progress);

        var stages = progress.Select(p => p.Stage).Distinct().ToList();
        Assert.Equal(new[]
        {
            ProcessingStage.Scanning, ProcessingStage.Filtering, ProcessingStage.ExtractingDates,
            ProcessingStage.CheckingDuplicates, ProcessingStage.Transferring, ProcessingStage.Completed
        }, stages);
    }

    [Fact]
    public void FailedOriginal_PromotesSourceDuplicate()
    {
        using var dir = new TempDir();
        var date = new DateTime(2024, 1, 1);
        var first = dir.CreateFile("src/a.jpg", "same", date);
        dir.CreateFile("src/b.jpg", "same", date);
        var engine = new ClassificationEngine(
            dateExtractor: new FailingFor(first),
            freeSpaceProvider: _ => long.MaxValue);
        // 날짜 추출 실패로 a 가 실패하면 b 는 원본으로 처리되어야 한다.
        var result = engine.Run(new AppSettings
        {
            SourceFolders = { dir.Combine("src") },
            TargetFolder = dir.Combine("out"),
            Operation = FileOperation.Copy
        }, null, null, CancellationToken.None);

        Assert.Equal(1, result.Failed);
        Assert.Equal(1, result.Transferred);
        Assert.Equal(0, result.Duplicates);
    }

    private sealed class FailingFor : IDateExtractor
    {
        private readonly string _path;
        private readonly ExifDateExtractor _inner = new();
        public FailingFor(string path) => _path = path;

        public DateResult Extract(string path) =>
            path == _path ? new DateResult { Error = "broken" } : _inner.Extract(path);
    }

    private sealed class SyncProgress : IProgress<ProgressInfo>
    {
        private readonly List<ProgressInfo> _list;
        public SyncProgress(List<ProgressInfo> list) => _list = list;
        public void Report(ProgressInfo value) { lock (_list) _list.Add(value); }
    }
}
