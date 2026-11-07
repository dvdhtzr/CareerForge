using CareerForge.Core;
using CareerForge.Infrastructure;

namespace CareerForge.Tests;

public sealed class EmployerWatchlistTests
{
    [Fact]
    public void Boards_are_explicit_and_descriptions_default_off()
    {
        var list = EmployerWatchlistEditor.Add(new EmployerWatchlist(), "Example_Board", "Example Employer");
        var board = Assert.Single(list.Sources);
        Assert.Equal("example_board", board.BoardToken);
        Assert.Equal("Example Employer", board.DisplayName);
        Assert.True(board.Enabled);
        Assert.False(board.IncludeDescriptions);
    }

    [Fact]
    public void Duplicate_board_is_rejected_case_insensitively()
    {
        var list = EmployerWatchlistEditor.Add(new EmployerWatchlist(), "Example", null);
        Assert.Throws<InvalidOperationException>(() => EmployerWatchlistEditor.Add(list, "EXAMPLE", "Duplicate"));
    }

    [Fact]
    public void Invalid_tokens_and_control_characters_are_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            EmployerWatchlistEditor.Add(new EmployerWatchlist(), "https://example.com", "Example"));
        Assert.Throws<InvalidDataException>(() =>
            EmployerWatchlistEditor.Add(new EmployerWatchlist(), "safe", "Bad\nHeader"));
    }

    [Fact]
    public void User_can_disable_enable_opt_in_and_remove_without_mutating_source()
    {
        var old = EmployerWatchlistEditor.Add(new EmployerWatchlist(), "company", "Company");
        var disabled = EmployerWatchlistEditor.SetEnabled(old, "company", false);
        var described = EmployerWatchlistEditor.SetDescriptions(disabled, "COMPANY", true);
        Assert.True(old.Sources[0].Enabled);
        Assert.False(old.Sources[0].IncludeDescriptions);
        Assert.False(described.Sources[0].Enabled);
        Assert.True(described.Sources[0].IncludeDescriptions);
        Assert.Empty(EmployerWatchlistEditor.Remove(described, "company").Sources);
    }

    [Fact]
    public async Task Watchlist_is_local_persistent_and_strictly_validated()
    {
        var directory = Path.Combine(Path.GetTempPath(), "careerforge-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(directory, "sources.json");
            var store = new EmployerWatchlistStore(path);
            Assert.Empty((await store.LoadAsync()).Sources);
            await store.SaveAsync(EmployerWatchlistEditor.Add(new EmployerWatchlist(), "company", "Company"));
            Assert.Equal("Company", Assert.Single((await store.LoadAsync()).Sources).DisplayName);
            await File.WriteAllTextAsync(path,
                """{"sources":[{"boardToken":"../../evil","displayName":"Bad","enabled":true}]}""");
            await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
