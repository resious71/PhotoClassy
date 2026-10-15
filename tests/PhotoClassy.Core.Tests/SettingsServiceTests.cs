using PhotoClassy.Core.Settings;
using Xunit;

namespace PhotoClassy.Core.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void MissingFile_ReturnsDefaults()
    {
        using var dir = new TempDir();
        var settings = new SettingsService(dir.Combine("config.json")).Load(out var error);
        Assert.Null(error);
        Assert.Empty(settings.SourceFolders);
        Assert.Equal(ClassificationFormat.YearMonth, settings.Format);
        Assert.Equal(FileOperation.Move, settings.Operation);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsWithSpecKeys()
    {
        using var dir = new TempDir();
        var service = new SettingsService(dir.Combine("config.json"));
        var settings = new AppSettings
        {
            SourceFolders = { @"C:\Users\User\Pictures", @"C:\Users\User\Downloads" },
            TargetFolder = @"D:\ClassifiedPhotos",
            Format = ClassificationFormat.YearMonthDay,
            Operation = FileOperation.Copy,
            IncludingExtensions = { ".jpg" },
            BarringExtensions = { ".txt" }
        };
        service.Save(settings);

        var json = File.ReadAllText(service.FilePath);
        Assert.Contains("\"sourceFolders\"", json);
        Assert.Contains("\"classificationFormat\": \"YYYY/MM/DD\"", json);
        Assert.Contains("\"fileOperation\": \"Copy\"", json);
        Assert.Contains("\"includingExtensions\"", json);
        Assert.Contains("\"BarringExtensions\"", json);

        var loaded = service.Load();
        Assert.Equal(settings.SourceFolders, loaded.SourceFolders);
        Assert.Equal(settings.TargetFolder, loaded.TargetFolder);
        Assert.Equal(ClassificationFormat.YearMonthDay, loaded.Format);
        Assert.Equal(FileOperation.Copy, loaded.Operation);
        Assert.Equal(new[] { ".jpg" }, loaded.IncludingExtensions);
        Assert.Equal(new[] { ".txt" }, loaded.BarringExtensions);
    }

    [Fact]
    public void LoadsSpecExampleJson()
    {
        using var dir = new TempDir();
        var path = dir.CreateFile("config.json", """
            {
              "sourceFolders": ["C:\\Users\\User\\Pictures", "C:\\Users\\User\\Downloads"],
              "targetFolder": "D:\\ClassifiedPhotos",
              "classificationFormat": "YYYY/MM",
              "fileOperation": "Move",
              "includingExtensions": [],
              "BarringExtensions": []
            }
            """);
        var loaded = new SettingsService(path).Load();
        Assert.Equal(2, loaded.SourceFolders.Count);
        Assert.Equal(ClassificationFormat.YearMonth, loaded.Format);
        Assert.Equal(FileOperation.Move, loaded.Operation);
    }

    [Fact]
    public void CorruptFile_ReturnsDefaultsWithError()
    {
        using var dir = new TempDir();
        var path = dir.CreateFile("config.json", "{ not json");
        var loaded = new SettingsService(path).Load(out var error);
        Assert.NotNull(error);
        Assert.Empty(loaded.SourceFolders);
    }

    [Fact]
    public void Reset_WritesDefaults()
    {
        using var dir = new TempDir();
        var service = new SettingsService(dir.Combine("config.json"));
        service.Save(new AppSettings { TargetFolder = "X", Operation = FileOperation.Copy });
        service.Reset();
        var loaded = service.Load();
        Assert.Equal(string.Empty, loaded.TargetFolder);
        Assert.Equal(FileOperation.Move, loaded.Operation);
    }
}
