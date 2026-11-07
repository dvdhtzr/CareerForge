using CareerForge.Core;
using CareerForge.Infrastructure;

namespace CareerForge.Tests;

public sealed class WatchlistScanCoordinatorTests
{
    [Fact]
    public async Task Only_enabled_boards_are_scanned_and_descriptions_are_opt_in()
    {
        var watchlist = EmployerWatchlistEditor.Add(new EmployerWatchlist(), "first", null);
        watchlist = EmployerWatchlistEditor.Add(watchlist, "second", null);
        watchlist = EmployerWatchlistEditor.Add(watchlist, "disabled", null);
        watchlist = EmployerWatchlistEditor.SetDescriptions(watchlist, "second", true);
        watchlist = EmployerWatchlistEditor.SetEnabled(watchlist, "disabled", false);
        var requested = new List<(string Board, bool Descriptions)>();
        var coordinator = new WatchlistScanCoordinator((board, include, ct) =>
        {
            requested.Add((board, include));
            return Task.FromResult(new JobScan { BoardToken = board, Status = "Succeeded", Added = 3 });
        }, TimeSpan.Zero);

        var result = await coordinator.RunAsync(watchlist);
        Assert.False(result.HasFailures);
        Assert.Equal(6, result.Added);
        Assert.Equal([("first", false), ("second", true)], requested);
    }

    [Fact]
    public async Task Failure_on_one_board_does_not_prevent_next_board()
    {
        var watchlist = EmployerWatchlistEditor.Add(new EmployerWatchlist(), "broken", null);
        watchlist = EmployerWatchlistEditor.Add(watchlist, "working", null);
        var coordinator = new WatchlistScanCoordinator((board, include, ct) =>
        {
            if (board == "broken") throw new HttpRequestException("Source offline");
            return Task.FromResult(new JobScan { BoardToken = board, Status = "Succeeded", Added = 2 });
        }, TimeSpan.Zero);
        var result = await coordinator.RunAsync(watchlist);
        Assert.Equal(2, result.Results.Count);
        Assert.True(result.HasFailures);
        Assert.Equal("Failed", result.Results[0].Scan.Status);
        Assert.Equal("Succeeded", result.Results[1].Scan.Status);
        Assert.Equal(2, result.Added);
    }

    [Fact]
    public async Task Empty_watchlist_is_a_failure_not_successful_noop()
    {
        var coordinator = new WatchlistScanCoordinator(
            (board, include, ct) => Task.FromResult(new JobScan()), TimeSpan.Zero);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.RunAsync(new EmployerWatchlist()));
    }
}
