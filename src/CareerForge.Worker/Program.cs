using CareerForge.Core;
using CareerForge.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

string[] boards = args.Length == 0 ? ["greenhouse"] : args;
if (boards.Any(board => !JobNormalizer.IsValidBoardToken(board)))
{
    Console.Error.WriteLine("Use Greenhouse board tokens only (letters, digits, hyphen, underscore).");
    return 2;
}

var builder = Host.CreateApplicationBuilder();
builder.Services.AddCareerForgeInfrastructure(builder.Configuration);
using var host = builder.Build();
var scanner = host.Services.GetRequiredService<JobScanService>();
var failures = 0;
foreach (var board in boards.Distinct(StringComparer.OrdinalIgnoreCase))
{
    var result = await scanner.ScanBoardAsync(board);
    Console.WriteLine($"{board}: {result.Status} | found={result.Found} " +
        $"added={result.Added} refreshed={result.Updated} skipped={result.Skipped}");
    if (result.Status != "Succeeded")
    {
        failures++;
        Console.Error.WriteLine($"  Error: {result.Error}");
    }
}
return failures == 0 ? 0 : 1;
