using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CareerForge.Core;

namespace CareerForge.Infrastructure;

public sealed class GreenhouseJobClient(HttpClient httpClient) : IDescribedJobFeedClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<CollectedJob>> GetJobsAsync(
        string boardToken, CancellationToken cancellationToken = default) =>
        GetInternalAsync(boardToken, includeDescriptions: false, cancellationToken);

    public Task<IReadOnlyList<CollectedJob>> GetJobsWithDescriptionsAsync(
        string boardToken, CancellationToken cancellationToken = default) =>
        GetInternalAsync(boardToken, includeDescriptions: true, cancellationToken);

    private async Task<IReadOnlyList<CollectedJob>> GetInternalAsync(
        string boardToken, bool includeDescriptions, CancellationToken cancellationToken)
    {
        if (!JobNormalizer.IsValidBoardToken(boardToken))
            throw new ArgumentException("Invalid Greenhouse board token.", nameof(boardToken));

        // Fixed provider hostname; a user-controlled URL is never fetched.
        using var response = await httpClient.GetAsync(
            $"https://boards-api.greenhouse.io/v1/boards/{boardToken}/jobs" +
            (includeDescriptions ? "?content=true" : ""),
            HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength > 20_000_000)
            throw new InvalidDataException("Job feed exceeds the 20 MB response limit.");

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        // Bound actual read size even when Content-Length is missing or incorrect.
        await using var bounded = new BoundedReadStream(stream, 20_000_000);
        var payload = await JsonSerializer.DeserializeAsync<GreenhouseResponse>(
            bounded, SerializerOptions, cancellationToken)
            ?? throw new InvalidDataException("Empty Greenhouse response.");

        if (payload.Jobs is null)
            throw new InvalidDataException("Greenhouse response is missing the jobs array.");
        if (payload.Jobs.Count > 20_000)
            throw new InvalidDataException("Unusually large Greenhouse jobs array.");

        return payload.Jobs.Select(job => new CollectedJob(
            job.Id.ToString(CultureInfo.InvariantCulture),
            job.Title ?? "",
            job.Location?.Name,
            job.AbsoluteUrl ?? "",
            DateTimeOffset.TryParse(job.UpdatedAt, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var updated) ? updated.ToUniversalTime() : null,
            includeDescriptions ? job.Content : null
        )).ToArray();
    }

    private sealed class GreenhouseResponse
    {
        [JsonPropertyName("jobs")]
        public List<GreenhouseJob>? Jobs { get; set; }
    }

    private sealed class GreenhouseJob
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
        [JsonPropertyName("title")]
        public string? Title { get; set; }
        [JsonPropertyName("location")]
        public GreenhouseLocation? Location { get; set; }
        [JsonPropertyName("absolute_url")]
        public string? AbsoluteUrl { get; set; }
        [JsonPropertyName("updated_at")]
        public string? UpdatedAt { get; set; }
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private sealed class GreenhouseLocation
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed class BoundedReadStream(Stream inner, long maximumBytes) : Stream
    {
        private long _read;
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var amount = inner.Read(buffer, offset, count);
            Count(amount);
            return amount;
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var amount = await inner.ReadAsync(buffer, cancellationToken);
            Count(amount);
            return amount;
        }
        private void Count(int amount)
        {
            _read += amount;
            if (_read > maximumBytes)
                throw new InvalidDataException("Job feed exceeded 20 MB response limit.");
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            await base.DisposeAsync();
        }
    }
}
