using System.Globalization;
using PhotoClassy.Core.Progress;
using Xunit;

namespace PhotoClassy.Core.Tests;

public class TimeTests
{
    [Fact]
    public void Estimator_ComputesAverageRemainingAndEta()
    {
        var now = new DateTime(2026, 10, 15, 15, 0, 0);
        var estimate = TimeEstimator.Compute(TimeSpan.FromSeconds(100), 50, 250, now);
        Assert.Equal(TimeSpan.FromSeconds(2), estimate.AveragePerFile);
        Assert.Equal(TimeSpan.FromSeconds(500), estimate.EstimatedTotal);
        Assert.Equal(TimeSpan.FromSeconds(400), estimate.Remaining);
        Assert.Equal(now.AddSeconds(400), estimate.EstimatedCompletion);
    }

    [Fact]
    public void Estimator_NoProgress_HasNoEstimate()
    {
        var estimate = TimeEstimator.Compute(TimeSpan.FromSeconds(3), 0, 10, DateTime.Now);
        Assert.Null(estimate.AveragePerFile);
        Assert.Null(estimate.Remaining);
    }

    [Fact]
    public void Formatter_Formats()
    {
        Assert.Equal("00:05:30", TimeFormatter.Full(new TimeSpan(0, 5, 30)));
        Assert.Equal("02:15:45", TimeFormatter.Full(new TimeSpan(2, 15, 45)));
        Assert.Equal("05:30", TimeFormatter.Short(new TimeSpan(0, 5, 30)));
        Assert.Equal("2:15", TimeFormatter.Short(new TimeSpan(2, 15, 45)));
        Assert.Equal("5분 30초", TimeFormatter.Korean(new TimeSpan(0, 5, 30)));
        Assert.Equal("11분 0초", TimeFormatter.Korean(TimeSpan.FromMinutes(11)));
        Assert.Equal("2.64초/파일", TimeFormatter.PerFile(TimeSpan.FromMilliseconds(2640)));
        Assert.Equal("1.5ms/파일", TimeFormatter.PerFile(TimeSpan.FromTicks(15000)));
        Assert.Equal("0.00초/파일", TimeFormatter.PerFile(TimeSpan.Zero));
        // ICU 버전에 따라 PM 앞 공백이 U+202F 일 수 있어 정규화 후 비교
        var completion = TimeFormatter.Completion(new DateTime(2026, 1, 1, 15, 45, 0), CultureInfo.GetCultureInfo("en-US"));
        Assert.Equal("약 3:45 PM 완료 예상", completion.Replace('\u202F', ' '));
    }
}
