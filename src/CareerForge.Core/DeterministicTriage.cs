namespace CareerForge.Core;

/// <summary>Conservative triage: unknown locations/titles are never silently discarded.</summary>
public static class DeterministicTriage
{
    public static TriageAssessment Evaluate(JobPosting job, CareerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.ExcludedTitleTerms.Any(term => !string.IsNullOrWhiteSpace(term) &&
            job.Title.Contains(term.Trim(), StringComparison.OrdinalIgnoreCase)))
            return new(TriageVerdict.Excluded, ["explicit-title-exclusion"]);

        bool targetTitle = profile.TargetTitles.Any(title =>
            !string.IsNullOrWhiteSpace(title) &&
            job.Title.Contains(title.Trim(), StringComparison.OrdinalIgnoreCase));

        bool compatibleLocation = profile.PreferredLocations.Any(location =>
            !string.IsNullOrWhiteSpace(location) &&
            ContainsLocationTerm(job.Location, location.Trim()));

        bool remote = profile.AllowRemote && job.Location.Contains("remote", StringComparison.OrdinalIgnoreCase);
        if (targetTitle && (compatibleLocation || remote))
            return new(TriageVerdict.Candidate, ["target-title-match", remote ? "remote-location" : "preferred-location"]);

        var reasons = new List<string>();
        if (!targetTitle) reasons.Add("title-needs-review");
        if (!compatibleLocation && !remote) reasons.Add("location-needs-review");
        return new(TriageVerdict.Review, reasons);
    }

    private static bool ContainsLocationTerm(string haystack, string term)
    {
        var index = haystack.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            int after = index + term.Length;
            bool beforeBoundary = index == 0 || !char.IsLetterOrDigit(haystack[index - 1]);
            bool afterBoundary = after >= haystack.Length || !char.IsLetterOrDigit(haystack[after]);
            if (beforeBoundary && afterBoundary) return true;
            index = haystack.IndexOf(term, index + 1, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
}
