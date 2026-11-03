using AttachmentExtractor.Core.Models;

namespace AttachmentExtractor.Core.Services;

public sealed class SettingsStore
{
    private readonly string _path;

    public SettingsStore(string? path = null)
    {
        _path = path ?? AppPaths.SettingsFile;
    }

    public AppSettings Load()
    {
        return JsonFileStore.Load(_path, () => new AppSettings());
    }

    public void Save(AppSettings settings)
    {
        JsonFileStore.Save(_path, settings);
    }
}
