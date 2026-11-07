using System.Net;
using System.Text.Json;
using CareerForge.Core;
using CareerForge.Infrastructure;

namespace CareerForge.Tests;

public sealed class OllamaTitleEvaluatorTests
{
    private static JobPosting Job() => new()
    {
        Title = "Lead .NET Engineer", Location = "Remote", BoardToken = "sample"
    };

    private static CareerProfile Profile() => new() { TargetTitles = ["Lead Engineer"], SkillKeywords = ["C#"] };

    [Fact]
    public async Task Sends_structured_request_to_loopback_and_parses_verdict()
    {
        var handler = new StubHandler(async request =>
        {
            Assert.Equal("127.0.0.1", request.RequestUri!.Host);
            Assert.Equal("/api/chat", request.RequestUri.AbsolutePath);
            var body = await request.Content!.ReadAsStringAsync();
            using var payload = JsonDocument.Parse(body);
            Assert.False(payload.RootElement.GetProperty("stream").GetBoolean());
            Assert.False(payload.RootElement.GetProperty("think").GetBoolean());
            Assert.Equal("object", payload.RootElement.GetProperty("format").GetProperty("type").GetString());
            Assert.Equal("qwen3.5:9b", payload.RootElement.GetProperty("model").GetString());
            var modelReply = JsonSerializer.Serialize(new
            {
                fit = "possible",
                rationale = "Job title overlaps target role titles.",
                uncertainty = "Actual job requirements were not evaluated."
            });
            var envelope = JsonSerializer.Serialize(new { message = new { content = modelReply } });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(envelope) };
        });
        var evaluator = new OllamaTitleEvaluator(new HttpClient(handler));
        var result = await evaluator.EvaluateAsync(Job(), Profile(), "qwen3.5:9b");
        Assert.Equal("possible", result.Fit);
        Assert.Contains("requirements", result.Uncertainty);
    }

    [Fact]
    public async Task Rejects_unsupported_verdict()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {"message":{"content":"{\"fit\":\"excellent\",\"rationale\":\"Reason\",\"uncertainty\":\"Unknown requirements\"}"}}
            """)
        }));
        var evaluator = new OllamaTitleEvaluator(new HttpClient(handler));
        await Assert.ThrowsAsync<InvalidDataException>(
            () => evaluator.EvaluateAsync(Job(), Profile(), "qwen3.5:9b"));
    }

    [Fact]
    public async Task Rejects_invalid_model_name()
    {
        var evaluator = new OllamaTitleEvaluator(new HttpClient(new StubHandler(_ =>
            throw new InvalidOperationException("Must not send request"))));
        await Assert.ThrowsAsync<ArgumentException>(
            () => evaluator.EvaluateAsync(Job(), Profile(), "http://evil.invalid/payload?x=1"));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request);
    }
}
