using PhotoClassy.Core.Dates;
using Xunit;

namespace PhotoClassy.Core.Tests;

public class DateExtractorTests
{
    [Theory]
    [InlineData("2026:01:15 10:20:30", 2026, 1, 15)]
    [InlineData("2025:12:31 23:59:59+09:00", 2025, 12, 31)]
    [InlineData("2024-02-29 08:00:00", 2024, 2, 29)]
    public void ParsesExifDates(string raw, int y, int m, int d)
    {
        Assert.True(ExifDateExtractor.TryParseExifDate(raw, out var date));
        Assert.Equal(new DateTime(y, m, d), date.Date);
    }

    [Theory]
    [InlineData("0000:00:00 00:00:00")]
    [InlineData("    :  :     :  :  ")]
    [InlineData("garbage")]
    [InlineData("")]
    public void RejectsInvalidExifDates(string raw)
    {
        Assert.False(ExifDateExtractor.TryParseExifDate(raw, out _));
    }

    [Fact]
    public void FileWithoutExif_UsesModifiedDateWithWarning()
    {
        using var dir = new TempDir();
        var modified = new DateTime(2023, 7, 4, 12, 0, 0);
        var path = dir.CreateFile("screenshot.png", "not really a png", modified);

        var result = new ExifDateExtractor().Extract(path);

        Assert.True(result.Success);
        Assert.Equal(DateSource.ModifiedDate, result.Source);
        Assert.Equal(modified, result.Date);
        Assert.NotNull(result.Warning);
    }

    [Fact]
    public void ReadsDateTimeOriginalFromJpeg()
    {
        using var dir = new TempDir();
        var path = dir.Combine("photo.jpg");
        File.WriteAllBytes(path, TestJpeg.WithDateTimeOriginal("2021:03:09 07:08:09"));
        File.SetLastWriteTime(path, new DateTime(2020, 1, 1));

        var result = new ExifDateExtractor().Extract(path);

        Assert.Equal(DateSource.ExifDateTimeOriginal, result.Source);
        Assert.Equal(new DateTime(2021, 3, 9, 7, 8, 9), result.Date);
        Assert.Null(result.Warning);
    }
}
