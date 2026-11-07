using CareerForge.Core;

namespace CareerForge.Tests;

public sealed class DeterministicTriageTests
{
    private static CareerProfile Profile() => new()
    {
        TargetTitles = ["Senior .NET Engineer", "Lead Software Engineer"],
        SkillKeywords = ["C#", ".NET", "SQL Server"],
        PreferredLocations = ["NY", "Stamford"],
        AllowRemote = true,
        ExcludedTitleTerms = ["Recruiter"]
    };

    private static JobPosting Job(string title, string location) => new()
    {
        Title = title,
        Location = location
    };

    [Fact]
    public void Title_and_remote_location_yield_candidate()
    {
        var result = DeterministicTriage.Evaluate(Job("Senior .NET Engineer", "Remote - USA"), Profile());
        Assert.Equal(TriageVerdict.Candidate, result.Verdict);
        Assert.Contains("remote-location", result.ReasonCodes);
    }

    [Fact]
    public void Unknown_location_is_review_not_exclusion()
    {
        var result = DeterministicTriage.Evaluate(Job("Lead Software Engineer", "Unspecified"), Profile());
        Assert.Equal(TriageVerdict.Review, result.Verdict);
    }

    [Fact]
    public void Different_title_is_review_instead_of_discarded()
    {
        var result = DeterministicTriage.Evaluate(Job("Staff Platform Engineer", "NY"), Profile());
        Assert.Equal(TriageVerdict.Review, result.Verdict);
    }

    [Fact]
    public void Explicit_exclusion_only_excludes_matching_title()
    {
        var result = DeterministicTriage.Evaluate(Job("Technical Recruiter", "Remote"), Profile());
        Assert.Equal(TriageVerdict.Excluded, result.Verdict);
    }

    [Fact]
    public void Short_location_term_does_not_match_middle_of_word()
    {
        var p = Profile();
        p.AllowRemote = false;
        var result = DeterministicTriage.Evaluate(Job("Lead Software Engineer", "Sydney"), p);
        Assert.Equal(TriageVerdict.Review, result.Verdict);
    }
}
