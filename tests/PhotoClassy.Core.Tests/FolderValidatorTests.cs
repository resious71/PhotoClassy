using PhotoClassy.Core.Settings;
using PhotoClassy.Core.Validation;
using Xunit;

namespace PhotoClassy.Core.Tests;

public class FolderValidatorTests
{
    [Fact]
    public void ValidSeparateFolders_Pass()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.Combine("src1"));
        Directory.CreateDirectory(dir.Combine("src2"));
        Directory.CreateDirectory(dir.Combine("target"));
        var result = FolderValidator.Validate(new AppSettings
        {
            SourceFolders = { dir.Combine("src1"), dir.Combine("src2") },
            TargetFolder = dir.Combine("target")
        });
        Assert.True(result.IsValid, string.Join("\n", result.Errors));
        Assert.False(result.TargetMissing);
    }

    [Fact]
    public void TargetInsideSource_Fails()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.Combine("src", "out"));
        var result = FolderValidator.Validate(new AppSettings
        {
            SourceFolders = { dir.Combine("src") },
            TargetFolder = dir.Combine("src", "out")
        });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void TargetSameAsSource_Fails()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.Combine("src"));
        var result = FolderValidator.Validate(new AppSettings
        {
            SourceFolders = { dir.Combine("src") },
            TargetFolder = dir.Combine("src") + Path.DirectorySeparatorChar
        });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void NestedSources_Fail()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.Combine("src", "sub"));
        var result = FolderValidator.Validate(new AppSettings
        {
            SourceFolders = { dir.Combine("src"), dir.Combine("src", "sub") },
            TargetFolder = dir.Combine("target")
        });
        Assert.False(result.IsValid);
        Assert.True(result.TargetMissing);
    }

    [Fact]
    public void SimilarPrefixIsNotNested()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.Combine("photos"));
        Directory.CreateDirectory(dir.Combine("photos2"));
        var result = FolderValidator.Validate(new AppSettings
        {
            SourceFolders = { dir.Combine("photos") },
            TargetFolder = dir.Combine("photos2")
        });
        Assert.True(result.IsValid, string.Join("\n", result.Errors));
    }

    [Fact]
    public void MissingSourceAndTarget_Fail()
    {
        using var dir = new TempDir();
        var result = FolderValidator.Validate(new AppSettings
        {
            SourceFolders = { dir.Combine("nope") },
            TargetFolder = ""
        });
        Assert.Equal(2, result.Errors.Count);
    }
}
