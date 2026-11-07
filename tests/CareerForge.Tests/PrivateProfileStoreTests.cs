using CareerForge.Core;
using CareerForge.Infrastructure;

namespace CareerForge.Tests;

public sealed class PrivateProfileStoreTests
{
    [Fact]
    public async Task Saves_and_reads_profile_from_explicit_local_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), "careerforge-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new PrivateProfileStore(Path.Combine(dir, "profile.json"));
            Assert.Null(await store.LoadAsync());
            var profile = new CareerProfile
            {
                TargetTitles = ["Lead Engineer"], SkillKeywords = ["C#"],
                PreferredLocations = ["New York"], AllowRemote = true
            };
            await store.SaveAsync(profile);
            Assert.True(store.Exists);
            var restored = await store.LoadAsync();
            Assert.NotNull(restored);
            Assert.Equal(profile.Fingerprint(), restored.Fingerprint());
            Assert.Equal("Lead Engineer", Assert.Single(restored.TargetTitles));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Rejects_empty_target_titles()
    {
        var store = new PrivateProfileStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(new CareerProfile()));
    }

    [Fact]
    public async Task Assessment_store_overwrites_same_job_profile_model_key()
    {
        var dir = Path.Combine(Path.GetTempPath(), "careerforge-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LocalAssessmentStore(Path.Combine(dir, "assessments.json"));
            var id = Guid.NewGuid();
            var first = new LocalModelAssessment(id, "hash1", "model", "possible", "First", "Unknown requirements", DateTime.UtcNow);
            await store.SaveAsync(first);
            await store.SaveAsync(first with { Fit = "strong", Rationale = "Second" });
            var rows = await store.ReadAsync();
            var only = Assert.Single(rows);
            Assert.Equal("strong", only.Fit);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }
}
