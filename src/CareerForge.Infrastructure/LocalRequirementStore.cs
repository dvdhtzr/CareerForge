using System.Text.Json;
using CareerForge.Core;

namespace CareerForge.Infrastructure;

/// <summary>Profile-linked, description-hash-linked results outside Git checkout.</summary>
public sealed class LocalRequirementStore(string? path = null)
{
    private readonly string _path = path ??
        Path.Combine(LocalDataPaths.DirectoryPath, "requirements.json");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<IReadOnlyList<RequirementEvaluation>> ReadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_path)) return [];
        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<List<RequirementEvaluation>>(stream, JsonOptions, ct) ?? [];
    }

    public async Task SaveAsync(RequirementEvaluation entry, CancellationToken ct = default)
    {
        var all = (await ReadAsync(ct)).ToList();
        all.RemoveAll(x => x.JobId == entry.JobId && x.ProfileFingerprint == entry.ProfileFingerprint
            && x.DescriptionHash == entry.DescriptionHash && x.Model == entry.Model);
        all.Add(entry);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(all, JsonOptions), ct);
            File.Move(temp, _path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
