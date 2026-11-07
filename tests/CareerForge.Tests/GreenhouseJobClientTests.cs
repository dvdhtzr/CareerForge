using System.Net;
using CareerForge.Infrastructure;

namespace CareerForge.Tests;

public sealed class GreenhouseJobClientTests
{
    [Fact]
    public async Task Parses_greenhouse_fixture()
    {
        const string payload = """
        {"jobs":[{"id":789,"title":"Senior Software Engineer","location":{"name":"New York"},
          "updated_at":"2026-11-01T10:00:00Z","absolute_url":"https://job-boards.greenhouse.io/test/jobs/789"}]}
        """;
        var handler = new StubHandler((request) =>
        {
            Assert.Equal("boards-api.greenhouse.io", request.RequestUri!.Host);
            Assert.Equal("/v1/boards/test/jobs", request.RequestUri.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload) };
        });
        var client = new GreenhouseJobClient(new HttpClient(handler));
        var jobs = await client.GetJobsAsync("test");
        Assert.Single(jobs);
        Assert.Equal("789", jobs[0].ExternalId);
        Assert.Equal("New York", jobs[0].Location);
    }

    [Fact]
    public async Task Malformed_response_is_an_error()
    {
        var client = new GreenhouseJobClient(new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") })));
        await Assert.ThrowsAsync<InvalidDataException>(() => client.GetJobsAsync("test"));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(response(request));
    }
}
