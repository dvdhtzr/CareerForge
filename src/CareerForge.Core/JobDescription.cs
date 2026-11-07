namespace CareerForge.Core;

/// <summary>Local-only copy of a publicly published, explicitly requested job description.</summary>
public sealed class JobDescription
{
    public Guid JobId { get; set; }
    public string Text { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public DateTime RetrievedAtUtc { get; set; }
}

public sealed record RequirementClaim(string Quote, string? ProfileSkill);

public sealed record RequirementEvaluation(
    Guid JobId,
    string ProfileFingerprint,
    string DescriptionHash,
    string Model,
    DateTime EvaluatedAtUtc,
    IReadOnlyList<RequirementClaim> Claims,
    string EvidenceLevel = "description-quoted");

public sealed record ExtractedRequirements(IReadOnlyList<RequirementClaim> Claims);
