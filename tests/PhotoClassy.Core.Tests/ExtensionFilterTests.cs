using PhotoClassy.Core.Filtering;
using Xunit;

namespace PhotoClassy.Core.Tests;

public class ExtensionFilterTests
{
    [Fact]
    public void Parse_NormalizesCaseDotsAndSeparators()
    {
        Assert.Equal(new[] { ".jpg", ".png", ".tiff" }, ExtensionFilter.Parse("JPG, .png;*.Tiff  jpg"));
        Assert.Empty(ExtensionFilter.Parse("   "));
    }

    [Fact]
    public void Empty_IncludesEverything()
    {
        var filter = new ExtensionFilter(null, null);
        Assert.True(filter.IsMatch("a.jpg"));
        Assert.True(filter.IsMatch("a.mov"));
        Assert.True(filter.IsMatch("noext"));
    }

    [Fact]
    public void IncludeOnly_MatchesIgnoringCase()
    {
        var filter = new ExtensionFilter(ExtensionFilter.Parse(".jpg,.png"), null);
        Assert.True(filter.IsMatch(@"C:\x\A.JPG"));
        Assert.False(filter.IsMatch("a.gif"));
        Assert.False(filter.IsMatch("noext"));
    }

    [Fact]
    public void ExcludeOnly_RemovesExcluded()
    {
        var filter = new ExtensionFilter(null, ExtensionFilter.Parse("txt"));
        Assert.False(filter.IsMatch("readme.TXT"));
        Assert.True(filter.IsMatch("a.cr2"));
    }

    [Fact]
    public void ExcludeWinsOverInclude()
    {
        var filter = new ExtensionFilter(ExtensionFilter.Parse(".jpg,.png"), ExtensionFilter.Parse(".PNG"));
        Assert.True(filter.IsMatch("a.jpg"));
        Assert.False(filter.IsMatch("a.png"));
    }
}
