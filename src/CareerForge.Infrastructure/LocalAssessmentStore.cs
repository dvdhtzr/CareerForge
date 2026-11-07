using System.Text.Json;
using CareerForge.Core;

namespace CareerForge.Infrastructure;

/// <summary>A private, local JSON store. Future iterations may migrate this to SQLite.</summary>
public sealed class LocalAssessmentStore(string? path = null)
{
    private readonly string _path = path ?? LocalDataPaths.AssessmentPath;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<IReadOnlyList<LocalModelAssessment>> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return [];
        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<List<LocalModelAssessment>>(stream, JsonOptions, cancellationToken)
            ?? [];
    }

    public async Task SaveAsync(LocalModelAssessment assessment, CancellationToken cancellationToken = default)
    {
        var items = (await ReadAsync(cancellationToken)).ToList();
        items.RemoveAll(item => item.JobId == assessment.JobId &&
            item.ProfileFingerprint == assessment.ProfileFingerprint && item.Model == assessment.Model);
        items.Add(assessment);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(items, JsonOptions), cancellationToken);
            File.Move(temp, _path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
