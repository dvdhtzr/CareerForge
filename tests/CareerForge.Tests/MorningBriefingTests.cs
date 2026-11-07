using CareerForge.Core;
using CareerForge.Infrastructure;

namespace CareerForge.Tests;

public sealed class MorningBriefingTests
{
    private static readonly DateTime Today = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    private static JobPosting Job(string board, string title, string location, int firstSeenDaysAgo,
        int lastSeenDaysAgo = 0) => new()
    {
        BoardToken = board, ExternalId = Guid.NewGuid().ToString("N"),
        Title = title, Location = location,
        JobUrl = "https://job-boards.greenhouse.io/example/jobs/123",
        FirstSeenAtUtc = Today.AddDays(-firstSeenDaysAgo),
        LastSeenAtUtc = Today.AddDays(-lastSeenDaysAgo)
    };

    private static CareerProfile Profile() => new()
    {
        TargetTitles = ["Senior .NET Engineer"],
        PreferredLocations = ["New York"],
        AllowRemote = true,
        ExcludedTitleTerms = ["Recruiter"]
    };

    [Fact]
    public void Recent_jobs_are_based_on_first_observation_not_last_refresh()
    {
        var watchlist = EmployerWatchlistEditor.Add(new EmployerWatchlist(), "acme", "Acme");
        var fresh = Job("acme", "Senior .NET Engineer", "Remote", 1);
        var oldRefreshed = Job("acme", "Senior .NET Engineer", "Remote", 90);
        var unconfigured = Job("other", "Senior .NET Engineer", "Remote", 0);
        var briefing = MorningBriefingBuilder.Build(
            [fresh, oldRefreshed, unconfigured], [], watchlist, Profile(), Today, 7);
        Assert.Equal(1, briefing.RecentCount);
        Assert.Equal(1, briefing.CandidateCount);
        Assert.Equal(fresh.Id, Assert.Single(briefing.Jobs).Id);
    }

    [Fact]
    public void Exclusions_count_but_are_not_shown_as_recommendations()
    {
        var watchlist = EmployerWatchlistEditor.Add(new EmployerWatchlist(), "acme", "Acme");
        var briefing = MorningBriefingBuilder.Build(
            [Job("acme", "Technical Recruiter", "Remote", 0),
             Job("acme", "Senior .NET Engineer", "Remote", 0),
             Job("acme", "Staff Developer", "Unknown", 0)],
            [], watchlist, Profile(), Today, 7);
        Assert.Equal(3, briefing.RecentCount);
        Assert.Equal(1, briefing.CandidateCount);
        Assert.Equal(1, briefing.ReviewCount);
        Assert.Equal(1, briefing.ExcludedCount);
        Assert.Equal(2, briefing.Jobs.Count);
        Assert.Equal("Candidate", briefing.Jobs[0].Triage);
    }

    [Fact]
    public void Missing_profile_means_unclassified_not_a_negative_judgment()
    {
        var watchlist = EmployerWatchlistEditor.Add(new EmployerWatchlist(), "acme", "Acme");
        var briefing = MorningBriefingBuilder.Build(
            [Job("acme", "Senior .NET Engineer", "Remote", 1)],
            [], watchlist, null, Today);
        Assert.False(briefing.ProfileConfigured);
        Assert.Equal(1, briefing.UnclassifiedCount);
        Assert.Equal("Unclassified", briefing.Jobs[0].Triage);
    }

    [Fact]
    public void Latest_failed_scan_and_disabled_source_are_visible()
    {
        var watchlist = EmployerWatchlistEditor.Add(new EmployerWatchlist(), "acme", "Acme");
        watchlist = EmployerWatchlistEditor.Add(watchlist, "unused", "Disabled");
        watchlist = EmployerWatchlistEditor.SetEnabled(watchlist, "unused", false);
        var scans = new[]
        {
            new JobScan { BoardToken = "acme", Status = "Succeeded",
                StartedAtUtc = Today.AddDays(-2), FinishedAtUtc = Today.AddDays(-2) },
            new JobScan { BoardToken = "acme", Status = "Failed",
                StartedAtUtc = Today, FinishedAtUtc = Today, Error = "503 provider unavailable" }
        };
        var briefing = MorningBriefingBuilder.Build([], scans, watchlist, Profile(), Today);
        Assert.Equal("Failed", briefing.Sources.Single(s => s.BoardToken == "acme").LastStatus);
        Assert.Equal("Disabled", briefing.Sources.Single(s => s.BoardToken == "unused").LastStatus);
        var text = MorningBriefingService.ToPlainText(briefing);
        Assert.Contains("503 provider unavailable", text);
        Assert.Contains("Observation does not guarantee", text);
    }

    [Fact]
    public void Expired_positions_are_excluded_from_the_briefing()
    {
        var list = new EmployerWatchlist();
        var market = Job("market", "Senior .NET Engineer", "Remote", 0);
        market.Provider = "himalayas";
        market.ExpiresAtUtc = Today.AddHours(-1);
        var briefing = MorningBriefingBuilder.Build([market], [], list, Profile(), Today);
        Assert.Equal(0, briefing.RecentCount);
        Assert.Empty(briefing.Jobs);
    }

    [Fact]
    public void Display_cap_is_reported_not_silently_lost()
    {
        var watchlist = EmployerWatchlistEditor.Add(new EmployerWatchlist(), "acme", "Acme");
        var jobs = Enumerable.Range(0, 8)
            .Select(i => Job("acme", "Senior .NET Engineer", "Remote", 0)).ToArray();
        var briefing = MorningBriefingBuilder.Build(jobs, [], watchlist, Profile(), Today, 7, maxShown: 3);
        Assert.Equal(8, briefing.RecentCount);
        Assert.Equal(5, briefing.OmittedCount);
        Assert.Equal(3, briefing.Jobs.Count);
    }
}
