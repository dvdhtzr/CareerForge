using System.Text.Json;
using CareerForge.Core;

namespace CareerForge.Infrastructure;

/// <summary>Explicit user-controlled configuration in local application data, not the repository.</summary>
public sealed class EmployerWatchlistStore(string? path = null)
{
    private readonly string _path = path ?? Path.Combine(LocalDataPaths.DirectoryPath, "sources.json");
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<EmployerWatchlist> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return new EmployerWatchlist();
        if (new FileInfo(_path).Length > 128_000)
            throw new InvalidDataException("Employer watchlist exceeds its size limit.");
        await using var file = File.OpenRead(_path);
        var result = await JsonSerializer.DeserializeAsync<EmployerWatchlist>(file, Options, cancellationToken)
            ?? throw new InvalidDataException("Employer watchlist must be a valid JSON object.");
        EmployerWatchlistEditor.Validate(result);
        return result;
    }

    public async Task SaveAsync(EmployerWatchlist watchlist, CancellationToken cancellationToken = default)
    {
        EmployerWatchlistEditor.Validate(watchlist);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(watchlist, Options), cancellationToken);
            File.Move(temp, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
