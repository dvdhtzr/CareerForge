namespace CareerForge.Core;

public sealed class JobPosting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Provider { get; set; } = "greenhouse";
    public string BoardToken { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Location { get; set; } = "";
    public string JobUrl { get; set; } = "";
    public DateTime? SourceUpdatedAtUtc { get; set; }
    public DateTime FirstSeenAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
}

public sealed class JobScan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Provider { get; set; } = "greenhouse";
    public string BoardToken { get; set; } = "";
    public string Status { get; set; } = "Failed";
    public DateTime StartedAtUtc { get; set; }
    public DateTime FinishedAtUtc { get; set; }
    public int Found { get; set; }
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public string? Error { get; set; }
}

public sealed record CollectedJob(
    string ExternalId,
    string Title,
    string? Location,
    string JobUrl,
    DateTimeOffset? SourceUpdatedAtUtc);

public sealed record NormalizedJob(
    string ExternalId,
    string Title,
    string Location,
    string JobUrl,
    DateTimeOffset? SourceUpdatedAtUtc);

public interface IJobFeedClient
{
    Task<IReadOnlyList<CollectedJob>> GetJobsAsync(string boardToken, CancellationToken cancellationToken = default);
}
