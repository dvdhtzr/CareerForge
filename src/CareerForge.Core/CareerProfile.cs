using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CareerForge.Core;

/// <summary>Private, locally stored career preferences. Never included in the public repository.</summary>
public sealed class CareerProfile
{
    public List<string> TargetTitles { get; set; } = [];
    public List<string> SkillKeywords { get; set; } = [];
    public List<string> PreferredLocations { get; set; } = [];
    public List<string> ExcludedTitleTerms { get; set; } = [];
    public bool AllowRemote { get; set; } = true;

    public string Fingerprint()
    {
        var serialized = JsonSerializer.Serialize(this);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized)));
    }
}

public enum TriageVerdict { Candidate, Review, Excluded }

public sealed record TriageAssessment(TriageVerdict Verdict, IReadOnlyList<string> ReasonCodes);

public sealed record LocalModelAssessment(
    Guid JobId,
    string ProfileFingerprint,
    string Model,
    string Fit,
    string Rationale,
    string Uncertainty,
    DateTime EvaluatedAtUtc,
    string EvidenceLevel = "metadata-only");

public sealed record LocalModelVerdict(string Fit, string Rationale, string Uncertainty);
