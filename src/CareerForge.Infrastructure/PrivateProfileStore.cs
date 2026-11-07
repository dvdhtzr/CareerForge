using System.Text.Json;
using CareerForge.Core;

namespace CareerForge.Infrastructure;

/// <summary>Reads/writes private profile outside the checkout. No cloud sync.</summary>
public sealed class PrivateProfileStore(string? path = null)
{
    private readonly string _path = path ?? LocalDataPaths.ProfilePath;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public bool Exists => File.Exists(_path);

    public async Task<CareerProfile?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return null;
        await using var stream = File.OpenRead(_path);
        var profile = await JsonSerializer.DeserializeAsync<CareerProfile>(stream, JsonOptions, cancellationToken);
        if (profile is null) throw new InvalidDataException("Career profile is empty.");
        Validate(profile);
        return profile;
    }

    public async Task SaveAsync(CareerProfile profile, CancellationToken cancellationToken = default)
    {
        Validate(profile);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(profile, JsonOptions), cancellationToken);
            File.Move(temp, _path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static void Validate(CareerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ValidateTerms(profile.TargetTitles, "target titles", 20, required: true);
        ValidateTerms(profile.SkillKeywords, "skills", 50);
        ValidateTerms(profile.PreferredLocations, "locations", 30);
        ValidateTerms(profile.ExcludedTitleTerms, "exclusions", 30);
    }

    private static void ValidateTerms(IReadOnlyList<string>? terms, string name, int maxCount, bool required = false)
    {
        if (terms is null || terms.Count > maxCount || (required && terms.Count == 0) ||
            terms.Any(s => string.IsNullOrWhiteSpace(s) || s.Length > 100))
            throw new InvalidDataException($"Profile {name} must have {(required ? "1 to " : "0 to ")}{maxCount} nonblank entries (100 characters max).");
    }
}
