using System.Net;
using System.Text.Json;
using CareerForge.Core;
using CareerForge.Infrastructure;

namespace CareerForge.Tests;

public sealed class MarketFeedTests
{
    [Fact]
    public void Keywords_come_from_the_private_profile_without_company_tokens()
    {
        var profile = new CareerProfile
        {
            TargetTitles = ["Senior .NET Engineer", "Backend Developer"],
            SkillKeywords = [".NET", "C#", "SQL Server"]
        };
        Assert.Equal(["Senior .NET Engineer", "Backend Developer", ".NET", "C#"],
            MarketSearchTerms.FromProfile(profile));
    }

    [Fact]
    public async Task Himalayas_searches_us_eligible_jobs_using_profile_terms_and_apply_link()
    {
        var urls = new List<string>();
        var feed = new HimalayasJobFeed(new HttpClient(new Handler(request =>
        {
            urls.Add(request.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "jobs":[
                    {"guid":"https://himalayas.app/companies/blue/jobs/engineer",
                    "title":"Senior .NET Engineer","companyName":"Blue Systems",
                    "applicationLink":"https://himalayas.app/apply/abc",
                    "locationRestrictions":["United States"],
                    "pubDate":1794050000}
                  ]
                }
                """)
            };
        })));
        var rows = await feed.SearchAsync("Senior .NET Engineer");
        var job = Assert.Single(rows);
        Assert.Equal("himalayas", job.Provider);
        Assert.Equal("Blue Systems", job.Company);
        Assert.Equal("Remote — United States", job.Location);
        Assert.Equal("https://himalayas.app/apply/abc", job.JobUrl);
        Assert.Single(urls);
        Assert.Contains("q=Senior%20.NET%20Engineer", urls[0]);
        Assert.Contains("country=US", urls[0]);
        Assert.Contains("page=1", urls[0]);
    }

    [Fact]
    public async Task Jobicy_searches_multiple_companies_without_sending_paid_credentials()
    {
        var urls = new List<string>();
        var feed = new JobicyJobFeed(new HttpClient(new Handler(request =>
        {
            Assert.Null(request.Headers.Authorization);
            urls.Add(request.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "success":true,"nextCursor":null,
                  "jobs":[
                    {"id":101,"jobTitle":"Software Developer","companyName":"Company A",
                     "jobGeo":"USA","url":"https://jobicy.com/jobs/one","pubDate":"2026-11-07T06:00:00Z"},
                    {"id":102,"jobTitle":"Cloud Engineer","companyName":"Company B",
                     "jobGeo":"Anywhere","url":"https://jobicy.com/jobs/two","pubDate":"2026-11-07T06:00:00Z"}
                  ]
                }
                """)
            };
        })));
        var results = await feed.SearchAsync("Developer");
        Assert.Equal(2, results.Count);
        Assert.Equal(["Company A", "Company B"], results.Select(r => r.Company).ToArray());
        Assert.Equal("https://jobicy.com/jobs/one", results[0].JobUrl);
        Assert.Contains("geo=usa", Assert.Single(urls));
        Assert.Contains("tag=Developer", urls[0]);
    }

    [Fact]
    public async Task Malformed_feed_is_not_reported_as_empty_success()
    {
        var feed = new JobicyJobFeed(new HttpClient(new Handler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") })));
        await Assert.ThrowsAsync<InvalidDataException>(() => feed.SearchAsync("Developer"));
    }

    [Fact]
    public async Task Short_keyword_is_skipped_for_jobicy_public_tag_filter()
    {
        var feed = new JobicyJobFeed(new HttpClient(new Handler(_ =>
            throw new InvalidOperationException("Should not send a request"))));
        Assert.Empty(await feed.SearchAsync("C#"));
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(response(request));
    }
}
