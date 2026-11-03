using System.Text.Json;
using AttachmentExtractor.Core.Models;

namespace AttachmentExtractor.Core.Services;

public sealed class ProfileStore
{
    private readonly string _path;

    public ProfileStore(string? path = null)
    {
        _path = path ?? AppPaths.ProfilesFile;
    }

    public List<FilterProfile> Load()
    {
        var profiles = JsonFileStore.Load(_path, () => new List<FilterProfile>());
        return profiles
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Name))
            .OrderBy(profile => profile.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    public void SaveAll(IEnumerable<FilterProfile> profiles)
    {
        var ordered = profiles
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Name))
            .OrderBy(profile => profile.Name, StringComparer.CurrentCulture)
            .ToList();

        JsonFileStore.Save(_path, ordered);
    }

    public FilterProfile Import(string path)
    {
        var json = File.ReadAllText(path);
        var profile = JsonSerializer.Deserialize<FilterProfile>(json, JsonFileStore.Options);
        if (profile is null || string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new InvalidOperationException("فایل پروفایل معتبر نیست.");
        }

        profile.AdvancedRoot ??= FilterNode.NewGroup();
        profile.Quick ??= new QuickFilterSettings();
        return profile;
    }

    public void Export(FilterProfile profile, string path)
    {
        JsonFileStore.Save(path, profile);
    }
}
