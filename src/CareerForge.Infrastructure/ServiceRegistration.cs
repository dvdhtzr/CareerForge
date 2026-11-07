using CareerForge.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace CareerForge.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddCareerForgeInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("CareerForge");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CareerForge");
            Directory.CreateDirectory(dataDirectory);
            connectionString = $"Data Source={Path.Combine(dataDirectory, "careerforge.db")}";
        }

        services.AddDbContextFactory<CareerForgeDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient<IJobFeedClient, GreenhouseJobClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CareerForge/0.1 (+local personal job-search tool)");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        });
        services.AddSingleton<EmployerWatchlistStore>();
        services.AddSingleton<PrivateProfileStore>();
        services.AddHttpClient<HimalayasJobFeed>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CareerForge/0.5 (personal job search)");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHttpClient<JobicyJobFeed>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CareerForge/0.5 (personal job search)");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddTransient<IMarketJobFeed>(sp => sp.GetRequiredService<HimalayasJobFeed>());
        services.AddTransient<IMarketJobFeed>(sp => sp.GetRequiredService<JobicyJobFeed>());
        services.AddTransient<MarketScanService>();
        services.AddTransient<MorningBriefingService>();
        services.AddTransient<JobScanService>();
        return services;
    }
}
