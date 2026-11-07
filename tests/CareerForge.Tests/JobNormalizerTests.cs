using CareerForge.Core;

namespace CareerForge.Tests;

public sealed class JobNormalizerTests
{
    [Theory]
    [InlineData("greenhouse", true)]
    [InlineData("fancy_company-1", true)]
    [InlineData("../../evil", false)]
    [InlineData("https://example.com", false)]
    [InlineData("", false)]
    public void Validates_board_tokens(string token, bool expected) =>
        Assert.Equal(expected, JobNormalizer.IsValidBoardToken(token));

    [Fact]
    public void Cleans_title_and_requires_https()
    {
        Assert.True(JobNormalizer.TryNormalize(
            new CollectedJob("42", " Senior   C# &amp; .NET  Engineer ", "Remote\nUSA",
                "https://job-boards.greenhouse.io/example/jobs/42", null),
            out var result));
        Assert.NotNull(result);
        Assert.Equal("Senior C# & .NET Engineer", result.Title);
        Assert.Equal("Remote USA", result.Location);
        Assert.False(JobNormalizer.TryNormalize(
            new CollectedJob("42", "Engineer", "Remote", "javascript:alert(1)", null), out _));
        Assert.False(JobNormalizer.TryNormalize(
            new CollectedJob("42", "Engineer", "Remote", "http://example.com", null), out _));
    }
}
